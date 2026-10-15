namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 历史事件对账补漏配置（对应 appsettings.json 的 <c>ISUP:EventReconcile</c>）。
///
/// 定时用 ISAPI 查询设备历史门禁事件（<c>POST /ISAPI/AccessControl/AcsEvent</c>），
/// 与已接收事件按 <c>(deviceId, serialNo)</c> 去重后补齐 —— 用于兜底 HTTP 推送漏报
/// （当设备侧不支持"断网续传"时，这是可靠的补漏手段）。
/// </summary>
public sealed class AcsEventReconcileOptions
{
    public const string SectionName = "ISUP:EventReconcile";

    /// <summary>是否启用对账补漏。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>对账周期（秒）。</summary>
    public int IntervalSeconds { get; set; } = 300;

    /// <summary>首次对账（无上次记录）时的回看窗口（秒）。</summary>
    public int LookbackSeconds { get; set; } = 900;

    /// <summary>与上次对账重叠的窗口（秒），避免时间边界漏报。</summary>
    public int OverlapSeconds { get; set; } = 60;

    /// <summary>每次查询的每页条数。</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>单设备单轮最多翻页数（防止一次性拉取过多）。</summary>
    public int MaxPagesPerRun { get; set; } = 20;

    /// <summary>透传查询超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 30000;

    /// <summary>事件主类型（0=全部；1-报警、2-异常、3-操作、5-门事件）。</summary>
    public int Major { get; set; }

    /// <summary>事件次类型（0=全部）。</summary>
    public int Minor { get; set; }

    /// <summary>为空表示对所有在线 OTAP 设备对账；否则仅对列表内的设备 ID。</summary>
    public List<string> Devices { get; set; } = new();
}
