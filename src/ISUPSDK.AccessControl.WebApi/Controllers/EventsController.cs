using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using ISUPSDK.AccessControl.WebApi.Services.Events;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 门禁事件入库（消息队列消费侧）控制器。
///
/// 事件链路：设备上报（HTTP 推送 / AMS 回调）→ <c>AcsEventSpool</c> 落盘 →
/// 消息队列（<c>EventQueue:Provider</c>：None / MSMQ / RabbitMQ）→
/// 消费者批量写入数据库（<c>EventStore:Provider</c>：Sqlite / MySql / SqlServer）。
///
/// 本控制器提供入库事件的查询与链路状态，队列/入库的实时计数见 <c>/health</c>。
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
[Tags("10. 门禁事件入库（消息队列消费）")]
public sealed class EventsController : ControllerBase
{
    private readonly AcsEventStore _store;
    private readonly AcsEventPublisher _publisher;
    private readonly AcsEventPublishWorker _publishWorker;
    private readonly AcsEventQueueConsumerService _consumer;
    private readonly AcsEventSpoolReplayer _replayer;
    private readonly AcsManualEventService _manual;
    private readonly AcsEventReconcileService _reconcile;
    private readonly AcsAlarmForwardService _forwarder;
    private readonly IEventQueue _queue;
    private readonly ILogger<EventsController> _logger;

    public EventsController(
        AcsEventStore store,
        AcsEventPublisher publisher,
        AcsEventPublishWorker publishWorker,
        AcsEventQueueConsumerService consumer,
        AcsEventSpoolReplayer replayer,
        AcsManualEventService manual,
        AcsEventReconcileService reconcile,
        AcsAlarmForwardService forwarder,
        IEventQueue queue,
        ILogger<EventsController> logger)
    {
        _store = store;
        _publisher = publisher;
        _publishWorker = publishWorker;
        _consumer = consumer;
        _replayer = replayer;
        _manual = manual;
        _reconcile = reconcile;
        _forwarder = forwarder;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>
    /// 读取事件抓拍图（Digest 直连下载后落盘的图片）。
    /// </summary>
    /// <remarks>
    /// 转发报文里的 <c>ExtEventPictureURL</c> 就指向本接口，形如
    /// <c>GET /api/events/pictures/FZ7955795_20261016091756_1_449789.jpeg</c>。
    /// 只接受纯文件名：含路径分隔符或上级目录的请求返回 400；文件不存在返回 404。
    /// </remarks>
    [HttpGet("pictures/{fileName}")]
    [Produces("image/jpeg")]
    public IActionResult Picture(string fileName)
    {
        var full = _forwarder.ResolvePictureFile(fileName);
        if (full is null)
        {
            throw new IsupApiException("文件名非法（只允许纯文件名，不含路径分隔符）。");
        }

        if (!System.IO.File.Exists(full))
        {
            throw new IsupApiException($"抓拍图不存在：{fileName}", StatusCodes.Status404NotFound);
        }

        return PhysicalFile(full, "image/jpeg", enableRangeProcessing: true);
    }

    /// <summary>事件管道状态：队列后端、生产/消费计数、入库统计、落盘重放情况。</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var storeStats = await _store.GetStatisticsAsync(ct).ConfigureAwait(false);

        return Ok(ApiResponse<object>.Ok(new
        {
            queue = new
            {
                enabled = _publisher.IsEnabled,
                provider = _queue.ProviderName,
                queueName = _queue.QueueName,
                statistics = _queue.GetStatistics(),
            },
            producer = new
            {
                pending = _publisher.PendingCount,
                enqueued = _publisher.Enqueued,
                skipped = _publisher.Skipped,
                dropped = _publisher.Dropped,
                published = _publishWorker.Published,
                failed = _publishWorker.Failed,
                lastPublishedAt = _publishWorker.LastPublishedAt,
                lastError = _publishWorker.LastError,
                lastErrorAt = _publishWorker.LastErrorAt,
            },
            consumer = new
            {
                enabled = _consumer.IsEnabled,
                consumed = _consumer.Consumed,
                stored = _consumer.Stored,
                duplicates = _consumer.Skipped,
                retried = _consumer.Retried,
                deadLettered = _consumer.DeadLettered,
                failedBatches = _consumer.Failed,
                // 消费者侧的报警转发统计（判定 + 投递到 Settings:MessageQueueAddress）。
                forwarded = _consumer.Forwarded,
                forwardSkipped = _consumer.ForwardSkipped,
                forwardDuplicates = _consumer.ForwardDuplicates,
                forwardFailed = _consumer.ForwardFailed,
                lastConsumedAt = _consumer.LastConsumedAt,
                lastError = _consumer.LastError,
                lastErrorAt = _consumer.LastErrorAt,
            },
            store = new
            {
                enabled = storeStats.Enabled,
                provider = storeStats.Provider,
                tableName = storeStats.TableName,
                schemaReady = _store.IsSchemaReady,
                schemaError = _store.SchemaError,
                inserted = storeStats.Inserted,
                duplicates = storeStats.Duplicates,
                failed = storeStats.Failed,
                totalRows = storeStats.TotalRows,
                lastStoredAt = storeStats.LastStoredAt,
                lastError = storeStats.LastError,
                lastErrorAt = storeStats.LastErrorAt,
            },
            replay = new
            {
                spoolDirectory = _replayer.SpoolDirectory,
                runs = _replayer.Runs,
                inserted = _replayer.Inserted,
                skipped = _replayer.Skipped,
                errors = _replayer.Errors,
                lastRunAt = _replayer.LastRunAt,
            },
        }));
    }

    /// <summary>
    /// 查询已入库的（门禁）事件，按事件时间倒序，支持分页与多条件过滤。
    /// </summary>
    /// <remarks>
    /// 示例：<c>GET /api/events?deviceId=DS-K1T670M&amp;accessControlOnly=true&amp;from=2026-10-01T00:00:00%2B08:00&amp;limit=50</c>
    /// </remarks>
    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] AcsEventQuery query, CancellationToken ct)
    {
        var (items, total) = await _store.QueryAsync(query, ct).ConfigureAwait(false);

        return Ok(ApiResponse<object>.Ok(new
        {
            total,
            limit = Math.Clamp(query.Limit, 1, 1000),
            offset = Math.Max(query.Offset, 0),
            items = items.Select(AcsEventRecordDto.From).ToList(),
        }));
    }

    /// <summary>按主键查询单条事件（<paramref name="includePayload"/>=false 时省略报文原文）。</summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, [FromQuery] bool includePayload = true, CancellationToken ct = default)
    {
        var record = await _store.GetByIdAsync(id, includePayload, ct).ConfigureAwait(false);
        if (record is null)
        {
            throw new IsupApiException($"未找到 ID 为 {id} 的事件。", StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<AcsEventRecordDto>.Ok(AcsEventRecordDto.From(record)));
    }

    /// <summary>
    /// 取事件可选值清单（事件类型 / 来源 / 认证方式，含出现次数），供前端下拉使用。
    /// </summary>
    /// <remarks>
    /// 值来自库里**实际出现过的数据**，因此下拉选项与现场一致，不需要人工维护枚举。
    /// 示例：<c>GET /api/events/facets?limit=50</c>
    /// </remarks>
    [HttpGet("facets")]
    public async Task<IActionResult> Facets([FromQuery] int limit = 50, CancellationToken ct = default)
    {
        var facets = await _store.GetFacetsAsync(limit, ct).ConfigureAwait(false);

        return Ok(ApiResponse<AcsEventFacets>.Ok(facets));
    }

    /// <summary>
    /// 手动重放 <c>AcsEventSpool</c> 的落盘事件文件（补齐队列/进程异常期间未入库的事件）。
    /// </summary>
    /// <remarks>
    /// 入库按去重键幂等，重复重放不会产生重复行。
    /// <paramref name="days"/> 留空时用配置 <c>EventQueue:Consumer:ReplaySpoolDays</c>；0 表示重放全部文件。
    /// </remarks>
    [HttpPost("replay-spool")]
    public async Task<IActionResult> ReplaySpool([FromQuery] int? days = null, CancellationToken ct = default)
    {
        var result = await _consumer.ReplaySpoolAsync(days, ct).ConfigureAwait(false);

        _logger.LogInformation(
            "手动重放事件落盘文件：文件 {Files} 个，记录 {Records} 条，新增 {Inserted} 行。",
            result.Files,
            result.Records,
            result.Inserted);

        return Ok(ApiResponse<AcsEventReplayResult>.Ok(result, "重放完成"));
    }

    /// <summary>
    /// 手动触发一次设备历史事件对账（补齐"平台漏收、设备本地仍存着"的门禁事件）。
    /// </summary>
    /// <remarks>
    /// 与后台定时对账（<c>EventReconcile</c>，默认每 300 秒）的区别：可显式指定查询区间（或回看窗口）与设备，立即执行。
    /// 补齐的事件走与实时上报相同的链路（去重 → 落盘 outbox → 消息队列 → 入库），因此不会产生重复行。
    ///
    /// 三种用法（优先级从高到低）：
    /// 1. **指定区间**（补历史某段时间）：
    ///    <c>POST /api/events/reconcile?from=2026-10-15T00:00:00&amp;to=2026-10-16T00:00:00</c>
    ///    —— 不带时区偏移的时间按 <c>ISUP:Time:TimeZone</c>（默认北京时间）解释；只给 <c>from</c> 时结束时间取"现在"，
    ///    只给 <c>to</c> 时开始时间按回看窗口倒推；补历史区间**不推进**定时对账的增量基准；
    /// 2. **指定回看窗口**（从"现在"回看 N 秒）：<c>?lookbackSeconds=3600</c>；
    /// 3. **都不给**：用配置的增量窗口（上次对账时间 − 重叠秒数 → 现在）与设备白名单。
    ///
    /// <c>deviceId</c> 可重复传多次；不传时对账所有在线 OTAP 设备（或配置的设备白名单）。
    /// 单台设备每轮最多取 <c>EventReconcile:MaxPagesPerRun</c> 页，响应里的 <c>truncated=true</c>
    /// 表示还有更早的事件没取完（把区间拆小或调大该配置后重试）。
    /// </remarks>
    [HttpPost("reconcile")]
    public async Task<IActionResult> Reconcile(
        [FromQuery] string? from = null,
        [FromQuery] string? to = null,
        [FromQuery] int? lookbackSeconds = null,
        [FromQuery] string[]? deviceId = null,
        [FromQuery] bool dryRun = false,
        [FromQuery] int? maxPages = null,
        CancellationToken ct = default)
    {
        if (!_reconcile.TryParseWindowTime(from, out var fromTime, out var fromError))
        {
            throw new IsupApiException(fromError!);
        }

        if (!_reconcile.TryParseWindowTime(to, out var toTime, out var toError))
        {
            throw new IsupApiException(toError!);
        }

        if (fromTime is { } start && toTime is { } end && start >= end)
        {
            throw new IsupApiException("开始时间必须早于结束时间（from < to）。");
        }

        var result = await _reconcile
            .ReconcileNowAsync(lookbackSeconds, deviceId, fromTime, toTime, dryRun, maxPages, ct)
            .ConfigureAwait(false);

        var window = fromTime is null && toTime is null
            ? (result.LookbackSeconds is { } seconds ? $"回看 {seconds}s" : "增量")
            : $"{fromTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(最早)"} → {toTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? "现在"}";

        // 一条都没补齐时给出下一步：看逐设备的 error/detail（设备原始响应也在里面）。
        var hint = result.Recovered == 0 && result.Queried == 0
            ? "；设备未返回事件，请看 items[].error / items[].detail（含 subStatusCode 与设备原始响应）"
            : string.Empty;

        return Ok(ApiResponse<AcsReconcileRunResult>.Ok(
            result,
            $"{(result.DryRun ? "对账诊断（未写库）" : "对账完成")}：设备返回 {result.Queried} 条，补齐 {result.Recovered} 条，重复 {result.Duplicates} 条"
            + $"（设备 {result.Devices} 台，区间 {window}）"
            + (result.Truncated ? $"；仍有更早事件未取完（本轮页数上限 {result.MaxPages}）：调大 maxPages，或用 items[].nextFrom 作为新的 from 继续补" : string.Empty)
            + hint));
    }

    /// <summary>
    /// 手工补录门禁事件（设备/网络故障期间丢报，或从其它系统导入历史通行记录）。
    /// </summary>
    /// <remarks>
    /// 请求体可以是**单个事件对象**，也可以是**事件数组**（单次上限 1000 条）。字段：
    /// <c>deviceId</c>（必填）、<c>serialNo</c>、<c>eventTime</c>、<c>major</c>、<c>minor</c>、
    /// <c>employeeNo</c>、<c>personName</c>、<c>cardNo</c>、<c>doorNo</c>、<c>verifyMode</c>、
    /// <c>eventType</c>、<c>source</c>、<c>isAccessControl</c>、<c>payload</c>（可选，给了就用你的原文）。
    ///
    /// 默认走与真实上报**相同**的链路（去重 → 落盘 outbox → 消息队列 → 消费者入库）：
    /// 同一 <c>deviceId:serialNo</c>（未给 <c>serialNo</c> 时按报文哈希）重复提交只落一行。
    /// 队列长时间不可用时可用 <c>?direct=true</c> 绕过队列直接写库。
    /// </remarks>
    [HttpPost("manual")]
    public async Task<IActionResult> Manual(
        [FromBody] JsonElement body,
        [FromQuery] bool direct = false,
        [FromQuery] bool force = false,
        CancellationToken ct = default)
    {
        var requests = ParseManualRequests(body);

        if (requests.Count == 0)
        {
            throw new IsupApiException("请求体为空：请提交一个事件对象或事件数组。");
        }

        if (requests.Count > AcsManualEventService.MaxBatchSize)
        {
            throw new IsupApiException($"单次最多补录 {AcsManualEventService.MaxBatchSize} 条，当前 {requests.Count} 条。");
        }

        var result = await _manual
            .SubmitAsync(requests, direct, force, ct)
            .ConfigureAwait(false);

        // 表不存在时"受理成功但永远不入库"很容易被误判为正常，这里显式提示。
        var schemaHint = _store.IsSchemaReady
            ? string.Empty
            : "；注意：事件表当前不可用，写入会失败（详见 GET /api/events/status 的 store.schemaError）";

        return Ok(ApiResponse<AcsManualEventResult>.Ok(
            result,
            $"受理 {result.Accepted} 条，重复 {result.Duplicates} 条，拒绝 {result.Rejected} 条" +
            (result.DirectToStore ? $"，直接入库 {result.Stored} 行" : string.Empty) +
            schemaHint));
    }

    /// <summary>解析手工补录请求体：单个对象或对象数组都支持。</summary>
    private static List<AcsManualEventRequest> ParseManualRequests(JsonElement body)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var requests = new List<AcsManualEventRequest>();

        switch (body.ValueKind)
        {
            case JsonValueKind.Array:
                requests.AddRange(body.Deserialize<List<AcsManualEventRequest>>(options) ?? new List<AcsManualEventRequest>());
                break;

            case JsonValueKind.Object:
                var single = body.Deserialize<AcsManualEventRequest>(options);
                if (single is not null)
                {
                    requests.Add(single);
                }

                break;
        }

        return requests;
    }
}
