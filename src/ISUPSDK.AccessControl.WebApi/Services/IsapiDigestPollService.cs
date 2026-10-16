using System.Collections.Concurrent;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISAPI Digest 高频轮询：按 <c>ISUP:IsapiDigest:PollIntervalSeconds</c> 周期直连每台已配置设备，
/// 拉取增量事件送入现有事件链路（去重 → 落盘 → 队列 → 入库 → 转发）。
/// <para>
/// 为什么需要它：<b>人脸抓拍图只存在设备本地且会被循环覆盖</b>，越早拉越能拿到；
/// 定时对账（默认 5 分钟）粒度太粗，等设备把图冲掉就再也拿不到了。
/// 本服务只负责<b>已配置 Digest 的设备</b>，未配置的设备仍由 SDK 对账负责；
/// 两条路径最终都走同一个去重键（<c>deviceId:serialNo:事件时间</c>），不会重复入库或重发下游。
/// </para>
/// </summary>
public sealed class IsapiDigestPollService : BackgroundService
{
    private readonly IsapiDigestClient _digest;
    private readonly AcsEventReconcileService _reconcile;
    private readonly IsapiDigestOptions _options;
    private readonly IsupTimeOptions _time;
    private readonly ILogger<IsapiDigestPollService> _logger;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastPolled = new(StringComparer.OrdinalIgnoreCase);

    private long _totalPolls;
    private long _totalAccepted;
    private DateTimeOffset? _lastRunAt;
    private string? _lastError;

    public IsapiDigestPollService(
        IsapiDigestClient digest,
        AcsEventReconcileService reconcile,
        IOptions<IsapiDigestOptions> options,
        IOptions<IsupTimeOptions> timeOptions,
        ILogger<IsapiDigestPollService> logger)
    {
        _digest = digest;
        _reconcile = reconcile;
        _options = options.Value;
        _time = timeOptions.Value;
        _logger = logger;
    }

    /// <summary>轮询是否生效（配置了 Digest 设备且间隔 &gt; 0）。</summary>
    public bool IsEnabled => _digest.IsEnabled && _options.PollIntervalSeconds > 0;

    /// <summary>已执行轮次。</summary>
    public long TotalPolls => Interlocked.Read(ref _totalPolls);

    /// <summary>累计补齐（新受理）的事件数。</summary>
    public long TotalAccepted => Interlocked.Read(ref _totalAccepted);

    /// <summary>最近一轮时间。</summary>
    public DateTimeOffset? LastRunAt => _lastRunAt;

    /// <summary>最近一次错误。</summary>
    public string? LastError => _lastError;

    /// <summary>参与轮询的设备编号。</summary>
    public IReadOnlyList<string> DeviceIds => _digest.ConfiguredDeviceIds;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_digest.IsEnabled)
        {
            _logger.LogInformation("设备 ISAPI Digest 直连未启用（ISUP:IsapiDigest:Enabled=false 或无可用设备），轮询不启动。");
            return;
        }

        if (_options.PollIntervalSeconds <= 0)
        {
            _logger.LogInformation("Digest 轮询已关闭（PollIntervalSeconds<=0）：只在对账时使用直连。");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Clamp(_options.PollIntervalSeconds, 10, 3600));

        _logger.LogInformation(
            "Digest 轮询已启动：间隔={Interval}s，设备 {Devices} 台（{DeviceIds}），首轮回看={Lookback}s，重叠={Overlap}s，单轮最多 {Pages} 页。",
            interval.TotalSeconds,
            _digest.ConfiguredDevices.Count,
            string.Join(",", _digest.ConfiguredDeviceIds),
            _options.PollLookbackSeconds,
            _options.PollOverlapSeconds,
            _options.PollMaxPages);

        // 稍等片刻，让网络/设备就绪，避免启动瞬间刷失败日志。
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _logger.LogWarning(ex, "Digest 轮询执行失败。");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Digest 轮询已退出：轮次 {Polls}，累计补齐 {Accepted} 条。",
            TotalPolls,
            TotalAccepted);
    }

    /// <summary>执行一轮轮询：每台已配置设备各拉一次增量窗口。</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
    {
        if (!IsEnabled)
        {
            return 0;
        }

        var devices = _digest.ConfiguredDevices;
        var accepted = 0;

        Interlocked.Increment(ref _totalPolls);
        _lastRunAt = DateTimeOffset.UtcNow;
        _lastError = null;

        var maxPages = Math.Max(_options.PollMaxPages, 1);
        var overlap = Math.Max(_options.PollOverlapSeconds, 0);
        var lookback = Math.Max(_options.PollLookbackSeconds, 60);

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var now = _time.Now();
            var from = _lastPolled.TryGetValue(device.DeviceId, out var last)
                ? last.AddSeconds(-overlap)
                : now.AddSeconds(-lookback);

            try
            {
                var pulled = await _reconcile
                    .PullDigestEventsAsync(device, from, now, maxPages, dryRun: false, ct)
                    .ConfigureAwait(false);

                if (pulled is { } result)
                {
                    accepted += result.Accepted;

                    if (result.Error is { Length: > 0 } error)
                    {
                        _lastError = error;
                    }
                    else
                    {
                        // 只有查成功的窗口才推进游标，失败时下轮会重拉同一段（去重保证不会重复入库）。
                        _lastPolled[device.DeviceId] = now;
                    }
                }
                else
                {
                    _lastError = $"设备 {device.DeviceId} ISAPI Digest 查询失败（详见日志）";
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _lastError = $"{device.DeviceId}：{ex.Message}";
                _logger.LogWarning(ex, "Digest 轮询设备 {DeviceId} 失败。", device.DeviceId);
            }
        }

        if (accepted > 0)
        {
            Interlocked.Add(ref _totalAccepted, accepted);
            _logger.LogInformation(
                "Digest 轮询：本轮补齐 {Accepted} 条（设备 {Devices} 台，窗口回看 {Overlap}s，最多 {Pages} 页）。",
                accepted,
                devices.Count,
                overlap,
                maxPages);
        }

        return accepted;
    }
}
