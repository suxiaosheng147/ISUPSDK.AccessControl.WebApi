namespace ISUPSDK.AccessControl.WebApi.Models.EventStore;

/// <summary>
/// 一次（手动触发的）历史事件对账结果（<c>POST /api/events/reconcile</c>）。
/// </summary>
public sealed record AcsReconcileRunResult
{
    /// <summary>对账功能是否启用（<c>EventReconcile:Enabled</c>；手动触发不受此限制）。</summary>
    public bool IsEnabled { get; init; }

    /// <summary>实际参与对账的设备数（在线且 OTAP 协议）。</summary>
    public int Devices { get; init; }

    /// <summary>本轮补齐（新受理）的事件数。</summary>
    public int Recovered { get; init; }

    /// <summary>从设备查回但平台已存在（去重命中）的事件数。</summary>
    public int Duplicates { get; init; }

    /// <summary>本次使用的回看窗口（秒）；null 表示用了定时任务的增量窗口或显式区间。</summary>
    public int? LookbackSeconds { get; init; }

    /// <summary>设备返回的事件条数（含被去重命中的；"仅诊断"模式下就是设备实际返回的条数）。</summary>
    public int Queried { get; init; }

    /// <summary>本次显式指定的查询区间开始时间（<c>from</c>）；null 表示未指定（增量/回看模式）。</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>本次显式指定的查询区间结束时间（<c>to</c>）；null 表示未指定（结束时间取"现在"）。</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>是否"仅诊断"模式（只查询统计，不落盘、不入库、不推进增量基准）。</summary>
    public bool DryRun { get; init; }

    /// <summary>
    /// 本次单台设备的页数上限（补历史默认 <c>AcsEventReconcileService.DefaultHistoryMaxPages</c> 页，
    /// 定时/增量模式用 <c>EventReconcile:MaxPagesPerRun</c>）。<see cref="Truncated"/> 为 true 时说明就是撞到它了。
    /// </summary>
    public int MaxPages { get; init; }

    /// <summary>
    /// 是否因单轮页数上限（<c>EventReconcile:MaxPagesPerRun</c>）而没取完：
    /// true 表示设备侧还有更早的事件，需要拆小区间或调大该配置后重新对账。
    /// </summary>
    public bool Truncated { get; init; }

    /// <summary>耗时（毫秒）。</summary>
    public long ElapsedMs { get; init; }

    /// <summary>逐设备结果。</summary>
    public IReadOnlyList<AcsReconcileDeviceResult> Items { get; init; } = Array.Empty<AcsReconcileDeviceResult>();
}

/// <summary>对账中单台设备的结果。</summary>
public sealed record AcsReconcileDeviceResult
{
    /// <summary>设备 ID。</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>该设备本轮使用的通道：<c>IsapiDigest</c>（直连）/ <c>Sdk</c>（透传兜底）/ <c>None</c>（两者都不可用）。</summary>
    public string? Channel { get; init; }

    /// <summary>该设备补齐的事件数。</summary>
    public int Recovered { get; init; }

    /// <summary>该设备查回但已存在的事件数。</summary>
    public int Duplicates { get; init; }

    /// <summary>该设备返回的事件条数（含被去重命中的；"仅诊断"模式下就是设备实际返回的条数）。</summary>
    public int Queried { get; init; }

    /// <summary>查询成功但设备未返回事件时的说明（含 <c>responseStatusStrg</c> / <c>numOfMatches</c> 等）。</summary>
    public string? Detail { get; init; }

    /// <summary>设备原始响应（截断到 1000 字符，便于排查固件字段差异）。</summary>
    public string? RawResponse { get; init; }

    /// <summary>该设备本轮实际使用的查询区间开始时间（业务时区）。</summary>
    public DateTimeOffset From { get; init; }

    /// <summary>该设备本轮实际使用的查询区间结束时间（业务时区）。</summary>
    public DateTimeOffset To { get; init; }

    /// <summary>该设备是否因单轮页数上限而没取完（还有更早的事件）。</summary>
    public bool Truncated { get; init; }

    /// <summary>
    /// 被 <see cref="Truncated"/> 截断时的续补起点（本页最后一条事件的时间）：
    /// 用 <c>from=nextFrom</c> 再跑一次即可接着补（设备按时间升序返回）。
    /// </summary>
    public DateTimeOffset? NextFrom { get; init; }

    /// <summary>失败原因（例如设备离线、不支持 ISAPI 查询）。</summary>
    public string? Error { get; init; }
}
