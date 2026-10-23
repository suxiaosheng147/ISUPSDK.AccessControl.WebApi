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

    /// <summary>
    /// 对账 / 高频轮询补的事件是否用 Digest 下载抓拍图（落盘后文件名随事件入库，
    /// <c>GET /api/events</c> 的 <c>pictures</c> / <c>pictureUrls</c> 才有值）。
    /// </summary>
    public bool DownloadPictures { get; set; } = true;

    /// <summary>
    /// 单设备单轮最多发起多少次抓拍图下载（0=不限）。
    /// 启动补历史（默认回看 60 天）时设备上的图多半已被循环覆盖，限流避免拖慢对账。
    /// </summary>
    public int MaxPictureDownloadsPerRun { get; set; } = 200;

    /// <summary>为空表示对所有在线 OTAP 设备对账；否则仅对列表内的设备 ID。</summary>
    public List<string> Devices { get; set; } = new();

    /// <summary>
    /// 启动时若事件表为空，是否自动补一次历史（首次部署 / 换库后回填用）。
    /// <para>只在表里确实 0 行时触发；补历史区间不推进定时对账的增量基准。</para>
    /// </summary>
    public bool StartupBackfillWhenEmpty { get; set; } = true;

    /// <summary>启动补历史的回看天数（<see cref="StartupBackfillWhenEmpty"/> 为 true 时使用）。</summary>
    public int StartupBackfillDays { get; set; } = 60;
}
