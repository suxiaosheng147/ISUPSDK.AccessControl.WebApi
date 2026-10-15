using System.Collections.Concurrent;
using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Acs;

/// <summary>
/// 历史事件对账补漏后台服务。
///
/// 周期性地对每台在线 OTAP 设备，用 ISAPI 查询"上次对账时间 → 现在"的门禁事件
/// （<c>POST /ISAPI/AccessControl/AcsEvent</c>），把平台没收到的事件补齐到
/// <see cref="AcsEventSpool"/>（与 HTTP 推送共用去重键，因此不会重复）。
///
/// 用途：当设备侧不支持"断网续传"（无法缓存重发）时，用平台侧主动拉取兜底漏报。
/// </summary>
public sealed class AcsEventReconcileService : BackgroundService
{
    private readonly AcsEventReconcileOptions _options;
    private readonly IsupCmsService _cms;
    private readonly IsupIsapiService _accessControl;
    private readonly AcsEventSpool _spool;
    private readonly IsupTimeOptions _time;
    private readonly ILogger<AcsEventReconcileService> _logger;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastReconciled = new(StringComparer.OrdinalIgnoreCase);

    private long _totalRuns;
    private long _totalRecovered;
    private DateTimeOffset? _lastRunAt;
    private string? _lastError;

    public AcsEventReconcileService(
        IOptions<AcsEventReconcileOptions> options,
        IOptions<IsupTimeOptions> timeOptions,
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsEventSpool spool,
        ILogger<AcsEventReconcileService> logger)
    {
        _options = options.Value;
        _time = timeOptions.Value;
        _cms = cms;
        _accessControl = accessControl;
        _spool = spool;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    /// <summary>已执行轮数。</summary>
    public long TotalRuns => Interlocked.Read(ref _totalRuns);

    /// <summary>累计补齐的事件数。</summary>
    public long TotalRecovered => Interlocked.Read(ref _totalRecovered);

    /// <summary>最近一轮时间。</summary>
    public DateTimeOffset? LastRunAt => _lastRunAt;

    /// <summary>最近一次错误（成功轮会清空）。</summary>
    public string? LastError => _lastError;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("事件对账补漏未启用（ISUP:EventReconcile:Enabled=false）。");
            return;
        }

        _logger.LogInformation(
            "事件对账补漏已启用：周期={Interval}s，首轮回看={Lookback}s，重叠={Overlap}s。",
            _options.IntervalSeconds,
            _options.LookbackSeconds,
            _options.OverlapSeconds);

        // 稍等片刻，让设备注册/上线就绪。
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
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
                _logger.LogWarning(ex, "事件对账补漏执行失败。");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.IntervalSeconds, 30)), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>执行一轮对账（后台定时任务使用：按配置的增量窗口与设备白名单）。</summary>
    public async Task<int> RunOnceAsync(CancellationToken ct = default)
        => (await RunCoreAsync(lookbackSeconds: null, deviceIds: null, ct).ConfigureAwait(false)).Recovered;

    /// <summary>
    /// 手动触发一次对账（<c>POST /api/events/reconcile</c>）。
    ///
    /// 与定时任务的区别：可以显式指定回看窗口（秒）与设备清单，
    /// 用于"设备离线期间漏收的事件"立即补回来，而不必等下一个对账周期。
    /// 补齐的事件走与实时上报相同的入口（去重 → 落盘 → 队列 → 入库）。
    /// </summary>
    public Task<AcsReconcileRunResult> ReconcileNowAsync(
        int? lookbackSeconds = null,
        IReadOnlyCollection<string>? deviceIds = null,
        CancellationToken ct = default)
        => RunCoreAsync(lookbackSeconds, deviceIds, ct);

    private async Task<AcsReconcileRunResult> RunCoreAsync(
        int? lookbackSeconds,
        IReadOnlyCollection<string>? deviceIds,
        CancellationToken ct)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var lookback = lookbackSeconds is { } seconds && seconds > 0
            ? Math.Clamp(seconds, 60, 86400 * 30)
            : (int?)null;

        var online = _cms.Devices.Where(d => d.Online && IsupCmsService.IsIsupDevice(d)).ToArray();

        var filter = deviceIds is { Count: > 0 }
            ? deviceIds
            : _options.Devices.Count > 0
                ? _options.Devices.ToArray()
                : null;

        var devices = filter is null
            ? online
            : online.Where(d => filter.Contains(d.DeviceId, StringComparer.OrdinalIgnoreCase)).ToArray();

        Interlocked.Increment(ref _totalRuns);
        _lastRunAt = DateTimeOffset.UtcNow;
        _lastError = null;

        var items = new List<AcsReconcileDeviceResult>(devices.Length);
        var recovered = 0;
        var duplicates = 0;

        foreach (var device in devices)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var (fresh, dup) = await ReconcileDeviceAsync(device, lookback, ct).ConfigureAwait(false);
                recovered += fresh;
                duplicates += dup;

                items.Add(new AcsReconcileDeviceResult
                {
                    DeviceId = device.DeviceId,
                    Recovered = fresh,
                    Duplicates = dup,
                });
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _logger.LogWarning(ex, "设备 {DeviceId} 对账失败。", device.DeviceId);

                items.Add(new AcsReconcileDeviceResult
                {
                    DeviceId = device.DeviceId,
                    Error = ex.Message,
                });
            }
        }

        // 指定了设备但该设备当前不可对账时，给出明确原因（而不是静默跳过）。
        if (deviceIds is { Count: > 0 })
        {
            foreach (var requested in deviceIds)
            {
                if (!devices.Any(d => string.Equals(d.DeviceId, requested, StringComparison.OrdinalIgnoreCase)))
                {
                    items.Add(new AcsReconcileDeviceResult
                    {
                        DeviceId = requested,
                        Error = "设备不在线或不是 OTAP 协议设备（对账需要 ISAPI 透传）",
                    });
                }
            }
        }

        Interlocked.Add(ref _totalRecovered, recovered);

        if (recovered > 0)
        {
            _logger.LogInformation(
                "事件对账补漏：本轮补齐 {Count} 条（设备 {Devices} 台，回看窗口 {Lookback}）。",
                recovered,
                devices.Length,
                lookback is null ? "增量" : $"{lookback}s");
        }
        else
        {
            _logger.LogDebug("事件对账补漏：本轮无缺口（设备 {Devices} 台）。", devices.Length);
        }

        return new AcsReconcileRunResult
        {
            IsEnabled = _options.Enabled,
            Devices = devices.Length,
            Recovered = recovered,
            Duplicates = duplicates,
            LookbackSeconds = lookback,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            Items = items,
        };
    }

    private async Task<(int Accepted, int Duplicates)> ReconcileDeviceAsync(IsupDevice device, int? lookbackSeconds, CancellationToken ct)
    {
        // 查询窗口用业务时区的时间（下发设备的 startTime/endTime 会带该偏移，与设备本地时间一致）。
        var now = _time.Now();

        DateTimeOffset from;
        if (lookbackSeconds is { } lookback)
        {
            // 手动触发且显式给了窗口：按窗口回看（不受上次对账时间影响）。
            from = now.AddSeconds(-lookback);
        }
        else
        {
            from = _lastReconciled.TryGetValue(device.DeviceId, out var last)
                ? last.AddSeconds(-Math.Max(_options.OverlapSeconds, 0))
                : now.AddSeconds(-Math.Max(_options.LookbackSeconds, 60));
        }

        if (from >= now)
        {
            from = now.AddMinutes(-1);
        }

        var searchId = Guid.NewGuid().ToString("N");
        var pageSize = Math.Clamp(_options.PageSize, 1, 1000);
        var position = 0;
        var pages = 0;
        var accepted = 0;
        var duplicates = 0;
        var maxPages = Math.Max(_options.MaxPagesPerRun, 1);

        while (pages < maxPages && !ct.IsCancellationRequested)
        {
            pages++;

            var condition = new
            {
                searchID = searchId,
                searchResultPosition = position,
                maxResults = pageSize,
                major = Math.Max(_options.Major, 0),
                minor = Math.Max(_options.Minor, 0),
                startTime = Format(from),
                endTime = Format(now),
            };

            var result = await _accessControl
                .SearchEventsAsync(device, condition, _options.TimeoutMs, ct)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogDebug(
                    "设备 {DeviceId} 事件查询未成功：状态={Status}，SDK 成功={Sdk}。",
                    device.DeviceId,
                    result.Status?.SubStatusCode ?? result.ResponseStatusString,
                    result.Success);
                break;
            }

            var (infos, hasMore) = ExtractInfoList(result.RawResponse);
            if (infos.Count == 0)
            {
                break;
            }

            foreach (var info in infos)
            {
                var evt = BuildReconcileEvent(device, info);
                if (_spool.TryAccept(evt))
                {
                    accepted++;
                }
                else
                {
                    duplicates++;
                }
            }

            if (!hasMore || infos.Count < pageSize)
            {
                break;
            }

            position += infos.Count;
        }

        _lastReconciled[device.DeviceId] = now;
        return (accepted, duplicates);
    }

    private static string Format(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", System.Globalization.CultureInfo.InvariantCulture);

    private static (List<JsonElement> Infos, bool HasMore) ExtractInfoList(string? json)
    {
        var list = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return (list, false);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var container = root;
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("AcsEvent", out var acsEvent) &&
                acsEvent.ValueKind == JsonValueKind.Object)
            {
                container = acsEvent;
            }

            if (container.ValueKind != JsonValueKind.Object)
            {
                return (list, false);
            }

            var hasMore = container.TryGetProperty("responseStatusStrg", out var status) &&
                          status.ValueKind == JsonValueKind.String &&
                          string.Equals(status.GetString(), "MORE", StringComparison.OrdinalIgnoreCase);

            if (container.TryGetProperty("InfoList", out var infos))
            {
                if (infos.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in infos.EnumerateArray())
                    {
                        list.Add(item.Clone());
                    }
                }
                else if (infos.ValueKind == JsonValueKind.Object)
                {
                    list.Add(infos.Clone());
                }
            }

            return (list, hasMore);
        }
        catch (JsonException)
        {
            return (list, false);
        }
    }

    private static IsupAlarmEvent BuildReconcileEvent(IsupDevice device, JsonElement info)
    {
        var payload = info.GetRawText();
        var major = ReadInt(info, "major");
        var minor = ReadInt(info, "minor");

        var evt = new IsupAlarmEvent
        {
            Source = "Reconcile",
            DeviceId = device.DeviceId,
            AlarmType = (byte)(major ?? 0),
            PayloadFormat = "json",
            Payload = payload,
            EventType = "AcsEvent",
            SubEventType = minor?.ToString(),
            IsAccessControl = major == 5,   // 与 ISUP 回调、ISAPI 语义一致：majorEventType=5 才是门禁事件
            PayloadBytes = System.Text.Encoding.UTF8.GetByteCount(payload),
            ReceivedAt = ReadTime(info) ?? DateTimeOffset.UtcNow,
        };

        return evt;
    }

    private static DateTimeOffset? ReadTime(JsonElement info)
    {
        if (!info.TryGetProperty("time", out var time) || time.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateTimeOffset.TryParse(time.GetString(), System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }
}
