using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Builders;
using ISUPSDK.AccessControl.WebApi.Services.Events;
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
    private readonly AcsEventStore _store;
    private readonly IsapiDigestClient _digest;
    private readonly IsupTimeOptions _time;
    private readonly ILogger<AcsEventReconcileService> _logger;

    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastReconciled = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>入参末尾的时区偏移（<c>±hh:mm</c> / <c>±hhmm</c>）。</summary>
    private static readonly Regex OffsetSuffix = new(@"[+-]\d{2}:?\d{2}$", RegexOptions.Compiled);

    /// <summary>
    /// 显式区间（补历史）模式下的默认页数上限：一年区间可能上万条，
    /// 若沿用定时任务的 <see cref="AcsEventReconcileOptions.MaxPagesPerRun"/>（20 页 × 30 条 = 600 条）根本补不完。
    /// </summary>
    internal const int DefaultHistoryMaxPages = 500;

    /// <summary>Digest 直连查询门禁事件的 ISAPI 路径。</summary>
    private const string DigestEventPath = "/ISAPI/AccessControl/AcsEvent?format=json";

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
        AcsEventStore store,
        IsapiDigestClient digest,
        ILogger<AcsEventReconcileService> logger)
    {
        _options = options.Value;
        _time = timeOptions.Value;
        _cms = cms;
        _accessControl = accessControl;
        _spool = spool;
        _store = store;
        _digest = digest;
        _logger = logger;
    }

    /// <summary>
    /// 启动补历史：事件表为空（首次部署 / 换库）时，按 <c>StartupBackfillDays</c> 补一次历史。
    /// 返回 true 表示确实执行了补历史。
    /// </summary>
    internal async Task<bool> TryStartupBackfillAsync(CancellationToken ct = default)
    {
        if (!_options.StartupBackfillWhenEmpty)
        {
            return false;
        }

        if (!_store.IsEnabled)
        {
            _logger.LogInformation("事件落库未启用（ISUP:EventStore:Enabled=false），跳过启动补历史。");
            return false;
        }

        try
        {
            await _store.EnsureCreatedAsync(ct).ConfigureAwait(false);

            var stats = await _store.GetStatisticsAsync(ct).ConfigureAwait(false);
            if (stats.TotalRows != 0)
            {
                _logger.LogInformation(
                    "事件表已有 {Rows} 行数据，跳过启动补历史（表为空时才补）。",
                    stats.TotalRows?.ToString() ?? "未知");
                return false;
            }

            var days = Math.Clamp(_options.StartupBackfillDays, 1, 3650);
            var now = _time.Now();
            var from = now.AddDays(-days);

            _logger.LogInformation(
                "事件表为空，启动补历史：回看 {Days} 天（{From} → {Now}）。",
                days,
                from.ToString("yyyy-MM-dd HH:mm:ss"),
                now.ToString("yyyy-MM-dd HH:mm:ss"));

            var result = await RunCoreAsync(
                    lookbackSeconds: null,
                    deviceIds: null,
                    explicitFrom: from,
                    explicitTo: now,
                    dryRun: false,
                    maxPages: null,
                    ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "启动补历史完成：设备 {Devices} 台，补齐 {Recovered} 条，重复 {Duplicates} 条，耗时 {Elapsed}ms{Truncated}。",
                result.Devices,
                result.Recovered,
                result.Duplicates,
                result.ElapsedMs,
                result.Truncated ? "（仍有更早事件未取完，可手动再补）" : string.Empty);

            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            // 启动补历史失败不影响后续定时对账。
            _logger.LogWarning(ex, "启动补历史失败（不影响定时对账）。");
            return false;
        }
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

        // 事件表为空（首次部署 / 换库）时先补一次历史，避免新库看起来"一直没数据"。
        try
        {
            await TryStartupBackfillAsync(stoppingToken).ConfigureAwait(false);
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
        => (await RunCoreAsync(
                lookbackSeconds: null,
                deviceIds: null,
                explicitFrom: null,
                explicitTo: null,
                dryRun: false,
                maxPages: null,
                ct)
            .ConfigureAwait(false)).Recovered;

    /// <summary>
    /// 手动触发一次对账（<c>POST /api/events/reconcile</c>）。
    ///
    /// 与定时任务的区别：可以显式指定查询区间（<paramref name="from"/> / <paramref name="to"/>，
    /// 用于补历史某段时间）或回看窗口（<paramref name="lookbackSeconds"/>），以及设备清单，
    /// 立即执行而不必等下一个对账周期。补齐的事件走与实时上报相同的入口（去重 → 落盘 → 队列 → 入库）。
    /// <para>
    /// <paramref name="dryRun"/> 为 true 时只查询与统计（不落盘、不入库、不推进增量基准），用于排查设备返回了什么。
    /// </para>
    /// <para>
    /// 注意：显式区间（<paramref name="from"/> / <paramref name="to"/>）<b>不推进</b>定时对账的增量基准，
    /// 因此补历史不会让下一次定时对账跳过"上次对账 → 现在"这段。
    /// </para>
    /// </summary>
    public Task<AcsReconcileRunResult> ReconcileNowAsync(
        int? lookbackSeconds = null,
        IReadOnlyCollection<string>? deviceIds = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        bool dryRun = false,
        int? maxPages = null,
        CancellationToken ct = default)
        => RunCoreAsync(lookbackSeconds, deviceIds, from, to, dryRun, maxPages, ct);

    /// <summary>
    /// 解析本轮单设备的页数上限：显式给了 <paramref name="requested"/> 就用它；
    /// 补历史（显式区间）用 <see cref="DefaultHistoryMaxPages"/>，定时/增量模式用配置值。
    /// </summary>
    internal static int ResolveMaxPages(int? requested, bool explicitWindow, int configuredMaxPages)
    {
        if (requested is { } value && value > 0)
        {
            return value;
        }

        return explicitWindow
            ? Math.Max(DefaultHistoryMaxPages, configuredMaxPages)
            : Math.Max(configuredMaxPages, 1);
    }

    /// <summary>
    /// 解析 <c>from</c> / <c>to</c> 入参（ISO8601）。不带偏移的写法（如 <c>2026-10-16T00:00:00</c>）
    /// 按业务时区（<c>ISUP:Time:TimeZone</c>）解释；留空返回 null（表示未指定）。
    /// </summary>
    public bool TryParseWindowTime(string? text, out DateTimeOffset? value, out string? error)
        => TryParseWindowTime(text, _time.ResolveOffset(), out value, out error);

    /// <summary>
    /// <see cref="TryParseWindowTime(string?, out DateTimeOffset?, out string?)"/> 的实现
    /// （业务时区偏移作为参数传入，便于单测）。
    /// </summary>
    internal static bool TryParseWindowTime(
        string? text,
        TimeSpan businessOffset,
        out DateTimeOffset? value,
        out string? error)
    {
        value = null;
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            error = $"时间格式无法解析：{text}（示例：2026-10-16T00:00:00 或 2026-10-16T00:00:00+08:00）";
            return false;
        }

        value = HasExplicitOffset(text)
            ? parsed
            : new DateTimeOffset(parsed.DateTime, businessOffset);

        return true;
    }

    /// <summary>入参是否自带时区偏移（<c>Z</c> 或 <c>±hh:mm</c>）；不带时按业务时区解释。</summary>
    private static bool HasExplicitOffset(string text)
        => text.EndsWith('Z') || text.EndsWith('z') || OffsetSuffix.IsMatch(text);

    private async Task<AcsReconcileRunResult> RunCoreAsync(
        int? lookbackSeconds,
        IReadOnlyCollection<string>? deviceIds,
        DateTimeOffset? explicitFrom,
        DateTimeOffset? explicitTo,
        bool dryRun,
        int? maxPages,
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

        // 对账目标 = 在线 SDK 设备 ∪ Digest 直连设备（后者不依赖 SDK 注册，正是直连通道的价值）。
        var targetIds = BuildTargetDeviceIds(
            online.Select(d => d.DeviceId),
            _digest.IsEnabled ? _digest.ConfiguredDeviceIds : [],
            filter);

        Interlocked.Increment(ref _totalRuns);
        _lastRunAt = DateTimeOffset.UtcNow;
        _lastError = null;

        var items = new List<AcsReconcileDeviceResult>(targetIds.Count);
        var recovered = 0;
        var duplicates = 0;
        var queried = 0;
        var anyTruncated = false;

        // 补历史（显式区间）默认给更大的页数上限：一年区间可能上万条，20 页（600 条）远远不够。
        var explicitWindow = explicitFrom is not null || explicitTo is not null;
        var effectiveMaxPages = ResolveMaxPages(maxPages, explicitWindow, _options.MaxPagesPerRun);

        foreach (var deviceId in targetIds)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var window = BuildWindow(
                    _time.Now(),
                    explicitFrom,
                    explicitTo,
                    lookback,
                    _lastReconciled.TryGetValue(deviceId, out var lastReconciled) ? lastReconciled : null,
                    _options.OverlapSeconds,
                    _options.LookbackSeconds);

                // 仅诊断：只看设备返回了什么，不推进增量基准（否则"看一眼"会影响定时对账的进度）。
                if (dryRun)
                {
                    window = window with { AdvanceCursor = false };
                }

                // Digest 直连优先；SDK 设备对象用于回退（可能为 null：设备未注册/离线）。
                var sdkDevice = online.FirstOrDefault(d =>
                    string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));

                var outcome = await ReconcileDeviceAsync(deviceId, sdkDevice, window, dryRun, effectiveMaxPages, ct)
                    .ConfigureAwait(false);

                recovered += outcome.Accepted;
                duplicates += outcome.Duplicates;
                queried += outcome.Queried;
                anyTruncated |= outcome.Truncated;

                items.Add(new AcsReconcileDeviceResult
                {
                    DeviceId = deviceId,
                    Channel = outcome.Channel,
                    Recovered = outcome.Accepted,
                    Duplicates = outcome.Duplicates,
                    Queried = outcome.Queried,
                    From = window.From,
                    To = window.To,
                    Truncated = outcome.Truncated,
                    Error = outcome.Error,
                    Detail = outcome.Detail,
                    RawResponse = outcome.RawResponse,
                    NextFrom = outcome.NextFrom,
                });
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                _logger.LogWarning(ex, "设备 {DeviceId} 对账失败。", deviceId);

                items.Add(new AcsReconcileDeviceResult
                {
                    DeviceId = deviceId,
                    Error = ex.Message,
                });
            }
        }

        // 指定了设备但该设备当前不可对账时，给出明确原因（而不是静默跳过）。
        if (deviceIds is { Count: > 0 })
        {
            foreach (var requested in deviceIds)
            {
                if (!targetIds.Contains(requested, StringComparer.OrdinalIgnoreCase))
                {
                    items.Add(new AcsReconcileDeviceResult
                    {
                        DeviceId = requested,
                        Error = "设备未在线/未注册到 SDK，且未配置可用的 ISAPI Digest 直连（对账需要其一）",
                    });
                }
            }
        }

        Interlocked.Add(ref _totalRecovered, recovered);

        var windowText = explicitFrom is null && explicitTo is null
            ? (lookback is null ? "增量" : $"{lookback}s")
            : $"{explicitFrom?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(最早)"} → {explicitTo?.ToString("yyyy-MM-dd HH:mm:ss") ?? "现在"}";

        if (recovered > 0 || queried > 0)
        {
            _logger.LogInformation(
                "事件对账补漏：设备返回 {Queried} 条，补齐 {Count} 条（设备 {Devices} 台，窗口 {Window}{DryRun}{Truncated}）。",
                queried,
                recovered,
                targetIds.Count,
                windowText,
                dryRun ? "，仅诊断" : string.Empty,
                anyTruncated ? "，已达单轮页数上限" : string.Empty);
        }
        else
        {
            // 0 条也要留痕：可能是"设备确实没有事件"，也可能是查询失败（原因见逐设备结果的 error）。
            _logger.LogInformation(
                "事件对账补漏：设备未返回事件（设备 {Devices} 台，窗口 {Window}）；逐设备原因见结果 error/detail。",
                targetIds.Count,
                windowText);
        }

        return new AcsReconcileRunResult
        {
            IsEnabled = _options.Enabled,
            Devices = targetIds.Count,
            Recovered = recovered,
            Duplicates = duplicates,
            Queried = queried,
            LookbackSeconds = lookback,
            From = explicitFrom,
            To = explicitTo,
            DryRun = dryRun,
            Truncated = anyTruncated,
            MaxPages = effectiveMaxPages,
            ElapsedMs = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            Items = items,
        };
    }

    /// <summary>
    /// 对一台设备执行一轮对账：优先 ISAPI Digest 直连，失败回退 SDK 透传兜底。
    /// <paramref name="dryRun"/> 为 true 时只查询与统计（不落盘、不入库、不推进增量基准）。
    /// </summary>
    private async Task<DeviceReconcileResult> ReconcileDeviceAsync(
        string deviceId,
        IsupDevice? sdkDevice,
        ReconcileWindow window,
        bool dryRun,
        int maxPages,
        CancellationToken ct)
    {
        // ① 配置了 Digest 直连的设备：走直连并跳过 SDK 对账（报文里带 pictureURL，且不依赖 SDK 注册）。
        if (_digest.TryResolveDevice(deviceId, out var digestDevice))
        {
            var viaDigest = await PullDigestEventsAsync(digestDevice, window.From, window.To, maxPages, dryRun, ct)
                .ConfigureAwait(false);

            if (viaDigest is { } digestResult)
            {
                // 直连成功：按窗口语义推进增量基准（显式区间 / 仅诊断不推进）。
                if (window.AdvanceCursor && !dryRun)
                {
                    _lastReconciled[deviceId] = _time.Now();
                }

                return digestResult;
            }

            // 直连失败：按 FallbackToSdk 决定是否用 SDK 对账兜底（false 时该设备本轮只走直连）。
            if (!_digest.FallbackToSdk)
            {
                return new DeviceReconcileResult(
                    Accepted: 0,
                    Duplicates: 0,
                    Queried: 0,
                    Truncated: false,
                    Error: "ISAPI Digest 直连查询失败，且配置为不回退 SDK 对账（ISUP:IsapiDigest:FallbackToSdk=false）",
                    Detail: null,
                    RawResponse: null,
                    NextFrom: null,
                    Channel: "IsapiDigest");
            }

            _logger.LogWarning("设备 {DeviceId} 的 ISAPI Digest 直连查询未成功，回退 SDK 对账兜底。", deviceId);
        }

        // ② 未配置 Digest（或配置了且允许回退）的设备：SDK 透传对账，需要设备已注册到 CMS 且在线。
        if (sdkDevice is null)
        {
            return new DeviceReconcileResult(
                Accepted: 0,
                Duplicates: 0,
                Queried: 0,
                Truncated: false,
                Error: "设备未在线/未注册到 SDK，且 ISAPI Digest 直连不可用或查询失败",
                Detail: null,
                RawResponse: null,
                NextFrom: null,
                Channel: "None");
        }

        return await ReconcileViaSdkAsync(sdkDevice, window, dryRun, maxPages, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 用 ISAPI Digest 直连拉取窗口内的事件并送入事件链路（<b>对账与高频轮询共用</b>）。
    /// 返回 null 表示"这一段没查成功"（连接 / 认证 / 非 2xx / 首页解析失败），调用方据此回退 SDK 对账。
    /// <para>本方法只负责查询与投递，不推进对账的增量基准（由调用方按窗口语义决定）。</para>
    /// </summary>
    internal async Task<DeviceReconcileResult?> PullDigestEventsAsync(
        IsapiDigestDevice device,
        DateTimeOffset from,
        DateTimeOffset to,
        int maxPages,
        bool dryRun,
        CancellationToken ct)
    {

        var searchId = Guid.NewGuid().ToString("N");
        var pageSize = Math.Clamp(_options.PageSize, 1, 1000);
        var position = 0;
        var pages = 0;
        var accepted = 0;
        var duplicates = 0;
        var queried = 0;
        var lastHasMore = false;
        var lastSummary = string.Empty;
        var lastEventTime = (DateTimeOffset?)null;

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
                endTime = Format(to),
            };

            var body = IsupJson.Serialize(IsapiBodyBuilder.WrapCondition("AcsEventCond", condition));

            var response = await _digest
                .SendAsync(device, HttpMethod.Post, DigestEventPath, body, "application/json", ct)
                .ConfigureAwait(false);

            if (!response.Success || string.IsNullOrWhiteSpace(response.Text))
            {
                lastSummary = $"HTTP {response.StatusCode}，{response.Error ?? "无响应体"}";

                if (pages == 1)
                {
                    // 首页就没成功：交给调用方回退 SDK 对账。
                    _logger.LogWarning(
                        "设备 {DeviceId} ISAPI Digest 查询失败（{Summary}），回退 SDK 兜底。",
                        device.DeviceId,
                        lastSummary);

                    return null;
                }

                // 后续页失败：保留已取到的部分，并让结果体现"没取完"。
                _logger.LogWarning(
                    "设备 {DeviceId} ISAPI Digest 第 {Pages} 页失败（{Summary}），本轮停止分页。",
                    device.DeviceId,
                    pages,
                    lastSummary);

                lastHasMore = true;
                break;
            }

            var (infos, hasMore) = ExtractInfoList(response.Text);
            lastSummary = $"responseStatusStrg={(hasMore ? "MORE" : "OK")}，本页 {infos.Count} 条，页码 {pages}，页大小 {pageSize}";

            if (infos.Count == 0)
            {
                break;
            }

            queried += infos.Count;

            if (!dryRun)
            {
                foreach (var info in infos)
                {
                    var evt = BuildReconcileEvent(device.DeviceId, info, "IsapiDigest");
                    if (_spool.TryAccept(evt))
                    {
                        accepted++;
                    }
                    else
                    {
                        duplicates++;
                    }
                }
            }

            lastEventTime = ReadTime(infos[^1]) ?? lastEventTime;
            lastHasMore = hasMore;

            if (!ShouldFetchNextPage(hasMore, infos.Count, pages, maxPages))
            {
                break;
            }

            position += infos.Count;
        }

        var truncated = lastHasMore;
        var detail = queried == 0 ? $"设备未返回事件（{lastSummary}）" : null;

        return new DeviceReconcileResult(
            Accepted: accepted,
            Duplicates: duplicates,
            Queried: queried,
            Truncated: truncated,
            Error: null,
            Detail: detail,
            RawResponse: null,
            NextFrom: truncated ? lastEventTime : null,
            Channel: "IsapiDigest");
    }

    /// <summary>SDK 透传对账（经 CMS 发 <c>POST /ISAPI/AccessControl/AcsEvent</c>，需要设备注册在线）。</summary>
    private async Task<DeviceReconcileResult> ReconcileViaSdkAsync(
        IsupDevice device,
        ReconcileWindow window,
        bool dryRun,
        int maxPages,
        CancellationToken ct)
    {
        // 查询窗口由 BuildWindow 算好（业务时区）：下发设备的 startTime/endTime 会带该偏移。
        var from = window.From;
        var to = window.To;

        var searchId = Guid.NewGuid().ToString("N");
        var pageSize = Math.Clamp(_options.PageSize, 1, 1000);
        var position = 0;
        var pages = 0;
        var accepted = 0;
        var duplicates = 0;
        var queried = 0;
        var lastHasMore = false;
        var lastSummary = string.Empty;
        var lastEventTime = (DateTimeOffset?)null;

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
                endTime = Format(to),
            };

            var result = await _accessControl
                .SearchEventsAsync(device, condition, _options.TimeoutMs, ct)
                .ConfigureAwait(false);

            var raw = Truncate(result.RawResponse, 1000);
            lastSummary =
                $"responseStatusStrg={result.ResponseStatusString ?? "(无)"}，" +
                $"numOfMatches={result.NumOfMatches}，totalMatches={result.TotalMatches}，" +
                $"页码={pages}，页大小={pageSize}";

            if (!result.Success)
            {
                // 查询失败必须暴露给调用方：早期这里只写 Debug 日志，导致"补齐 0 条"看不出原因。
                var error =
                    $"设备事件查询未成功：subStatusCode={result.Status?.SubStatusCode ?? "(无)"}，{lastSummary}，响应={raw ?? "(空)"}";

                _logger.LogWarning("设备 {DeviceId} {Error}", device.DeviceId, error);

                return new DeviceReconcileResult(accepted, duplicates, queried, false, error, null, raw, null, "Sdk");
            }

            var (infos, hasMore) = ExtractInfoList(result.RawResponse);
            if (infos.Count == 0)
            {
                break;
            }

            queried += infos.Count;

            // 仅诊断模式：只统计设备返回的条数，不落盘、不入库。
            if (!dryRun)
            {
                foreach (var info in infos)
                {
                    var evt = BuildReconcileEvent(device.DeviceId, info, "Reconcile");
                    if (_spool.TryAccept(evt))
                    {
                        accepted++;
                    }
                    else
                    {
                        duplicates++;
                    }
                }
            }

            // 记住本页最后一条的事件时间：到页数上限时用它作为"下一段从哪继续"。
            lastEventTime = ReadTime(infos[^1]) ?? lastEventTime;

            lastHasMore = hasMore;

            // 只信任设备返回的 responseStatusStrg=MORE：多数门禁设备有单页条数上限（例如 30 条），
            // 用"本页条数 < 请求页大小"判断会误以为已取完，导致只补到第一页（此前正是这个 bug）。
            if (!ShouldFetchNextPage(hasMore, infos.Count, pages, maxPages))
            {
                break;
            }

            position += infos.Count;
        }

        // 只有增量窗口（定时任务 / 回看）且非"仅诊断"才推进基准：显式补历史不能把基准挪到"现在"，
        // 否则下一次定时对账会跳过"上次对账 → 现在"这段。
        if (window.AdvanceCursor && !dryRun)
        {
            _lastReconciled[device.DeviceId] = _time.Now();
        }

        // 取到单轮页数上限但设备侧还有更多 → 本次区间没补完（调用方据此提示拆小区间）。
        var truncated = lastHasMore && pages >= maxPages;

        // 一条都没查到时给出原因（查询成功但无数据 vs 查询失败已在上面的 Error 里说明）。
        var detail = queried == 0 ? $"设备未返回事件（{lastSummary}）" : null;

        // 被页数上限截断时给出"下一段从这里继续"：设备按时间升序返回，本页最后一条的时间即边界。
        var nextFrom = truncated ? lastEventTime : null;

        return new DeviceReconcileResult(accepted, duplicates, queried, truncated, null, detail, null, nextFrom, "Sdk");
    }

    /// <summary>单台设备一轮对账（或一轮直连拉取）的结果（含排查用的诊断信息）。</summary>
    internal readonly record struct DeviceReconcileResult(
        int Accepted,
        int Duplicates,
        int Queried,
        bool Truncated,
        string? Error,
        string? Detail,
        string? RawResponse,
        DateTimeOffset? NextFrom,
        string Channel);

    /// <summary>
    /// 计算本轮对账的目标设备编号：在线 SDK 设备 ∪ 已配置的 Digest 直连设备（按 <paramref name="filter"/> 过滤）。
    /// </summary>
    internal static IReadOnlyList<string> BuildTargetDeviceIds(
        IEnumerable<string> onlineDeviceIds,
        IEnumerable<string> digestDeviceIds,
        IReadOnlyCollection<string>? filter)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var id in onlineDeviceIds.Concat(digestDeviceIds))
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
            {
                continue;
            }

            if (filter is { Count: > 0 } && !filter.Contains(id, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(id);
        }

        return result;
    }

    /// <summary>
    /// 是否继续取下一页。
    /// <para>
    /// 只信任设备返回的 <c>responseStatusStrg=MORE</c>（<paramref name="hasMore"/>），并防空转与页数上限：
    /// <b>不能</b>因为"本页条数 &lt; 请求的页大小"就停止 —— 多数门禁设备有单页条数上限（例如 30 条），
    /// 那样会把第一页当成全部（此前正是这个判断导致只补到一页）。
    /// </para>
    /// </summary>
    internal static bool ShouldFetchNextPage(bool hasMore, int receivedCount, int pages, int maxPages)
        => hasMore && receivedCount > 0 && pages < maxPages;

    private static string? Truncate(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        return text.Length <= maxLength ? text : text[..maxLength] + "…";
    }

    /// <summary>
    /// 计算某台设备本轮的查询窗口（设备侧 <c>startTime</c> / <c>endTime</c>）。
    /// <list type="bullet">
    /// <item>显式区间（<paramref name="explicitFrom"/> / <paramref name="explicitTo"/>）：按历史区间补漏，<b>不推进</b>增量基准；
    /// 只给 <paramref name="explicitFrom"/> 时结束时间取 <paramref name="now"/>，只给 <paramref name="explicitTo"/> 时按回看窗口倒推开始时间；</item>
    /// <item><paramref name="lookbackSeconds"/>：从"现在"回看 N 秒（推进增量基准）；</item>
    /// <item>都不给：从"上次对账时间 − 重叠秒数"到现在（定时任务，推进增量基准）。</item>
    /// </list>
    /// </summary>
    internal static ReconcileWindow BuildWindow(
        DateTimeOffset now,
        DateTimeOffset? explicitFrom,
        DateTimeOffset? explicitTo,
        int? lookbackSeconds,
        DateTimeOffset? lastReconciled,
        int overlapSeconds,
        int defaultLookbackSeconds)
    {
        if (explicitFrom is { } start)
        {
            var end = explicitTo ?? now;

            // 区间非法（start >= end）时给 1 分钟窗口，避免下发 startTime > endTime。
            return new ReconcileWindow(start, start < end ? end : start.AddMinutes(1), AdvanceCursor: false);
        }

        if (explicitTo is { } onlyEnd)
        {
            var lookback = lookbackSeconds is { } seconds && seconds > 0
                ? seconds
                : Math.Max(defaultLookbackSeconds, 60);

            return new ReconcileWindow(onlyEnd.AddSeconds(-lookback), onlyEnd, AdvanceCursor: false);
        }

        if (lookbackSeconds is { } lookbackValue && lookbackValue > 0)
        {
            return new ReconcileWindow(now.AddSeconds(-lookbackValue), now, AdvanceCursor: true);
        }

        var begin = lastReconciled is { } last
            ? last.AddSeconds(-Math.Max(overlapSeconds, 0))
            : now.AddSeconds(-Math.Max(defaultLookbackSeconds, 60));

        return new ReconcileWindow(begin, now, AdvanceCursor: true);
    }

    /// <summary>对账查询窗口（设备侧 startTime/endTime）与是否推进增量基准。</summary>
    internal readonly record struct ReconcileWindow(DateTimeOffset From, DateTimeOffset To, bool AdvanceCursor);

    private static string Format(DateTimeOffset value)
        => value.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture);

    private (List<JsonElement> Infos, bool HasMore) ExtractInfoList(string? json)
    {
        var list = new List<JsonElement>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return (list, false);
        }

        // 设备响应常在合法 JSON 后面跟垃圾（0x03 结束符、SDK 缓冲区残留的 '1'/'5'/0xEF 等）：
        // 不截断会让 JsonDocument 直接抛异常，把"设备返回了事件但没解析出来"伪装成"0 条"。
        var text = IsapiResponseNormalizer.TruncateToFirstJsonValue(json);

        try
        {
            using var doc = JsonDocument.Parse(text);
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
        catch (JsonException ex)
        {
            // 解析失败要留痕：否则会与"设备确实没有事件"混在一起。
            _logger.LogWarning(
                "设备事件查询响应无法解析为 JSON：{Error}；响应前 200 字符：{Head}",
                ex.Message,
                text.Length <= 200 ? text : text[..200]);

            return (list, false);
        }
    }

    /// <summary>
    /// 把设备返回的一条事件转成 <see cref="IsupAlarmEvent"/>；
    /// <paramref name="source"/> 区分通道（<c>Reconcile</c> = SDK 透传对账，<c>IsapiDigest</c> = Digest 直连）。
    /// </summary>
    private static IsupAlarmEvent BuildReconcileEvent(string deviceId, JsonElement info, string source)
    {
        var payload = info.GetRawText();
        var major = ReadInt(info, "major");
        var minor = ReadInt(info, "minor");

        var evt = new IsupAlarmEvent
        {
            Source = source,
            DeviceId = deviceId,
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
