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

    /// <summary>本次使用的回看窗口（秒）；null 表示用了定时任务的增量窗口。</summary>
    public int? LookbackSeconds { get; init; }

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

    /// <summary>该设备补齐的事件数。</summary>
    public int Recovered { get; init; }

    /// <summary>该设备查回但已存在的事件数。</summary>
    public int Duplicates { get; init; }

    /// <summary>失败原因（例如设备离线、不支持 ISAPI 查询）。</summary>
    public string? Error { get; init; }
}
