namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// ACS 门禁事件 HTTP 推送接收配置（对应 appsettings.json 的 <c>ISUP:ACSHttpPush</c>）。
///
/// 海康 C# Demo 对 ACS 产品线的官方做法：设备侧在网页"HTTP 监听 / 上传"中配置平台地址，
/// 设备以 <c>multipart/form-data</c> 主动 POST 事件（<c>text/json</c> 或 XML 正文 + <c>image/jpeg</c> 抓拍）。
/// 本服务用一个独立 TCP 监听接收并解析，<b>不依赖 AMS 组件与 <c>OTAP_CMS_StartAlarm</c></b>。
/// </summary>
public sealed class AcsHttpPushOptions
{
    public const string SectionName = "ISUP:ACSHttpPush";

    /// <summary>是否启用 HTTP 推送接收监听。</summary>
    public bool Enabled { get; set; }

    /// <summary>监听地址，0.0.0.0 表示监听全部网卡。</summary>
    public string ListenIp { get; set; } = "0.0.0.0";

    /// <summary>监听端口，需与设备端"HTTP 监听"上报配置的端口一致。</summary>
    public ushort ListenPort { get; set; } = 8083;

    /// <summary>只在请求路径等于该值时才接收（如 <c>/test</c>）；留空表示接收任意路径。</summary>
    public string? UrlPath { get; set; }

    /// <summary>
    /// 是否在设备上线后自动向其下发 HTTP 上报配置
    /// （<c>PUT /ISAPI/Event/notification/httpHosts</c>）。
    ///
    /// 注意：会让设备把门禁事件上报到本服务，<b>并覆盖设备侧第 1 条上报配置</b>。
    /// 若设备上已有指向其它平台的上报配置，请关闭此项并用
    /// <c>POST /api/alarms/devices/{deviceId}/enable-http-push</c> 手动控制。
    /// </summary>
    public bool AutoConfigureOnDeviceOnline { get; set; } = true;

    /// <summary>
    /// 下发给设备的上报目标 IP。留空时依次回退到 <c>AMS:AlarmServerAddress</c>、
    /// <c>ISUP:ListenIp</c>（需为具体 IP，不能是 0.0.0.0）。
    /// </summary>
    public string? AdvertiseAddress { get; set; }

    /// <summary>是否把设备上报的抓拍图片保存到本地目录。</summary>
    public bool SavePictures { get; set; } = true;

    /// <summary>抓拍图片保存目录。</summary>
    public string PictureDirectory { get; set; } = "./logs/acs-push/pictures/";

    /// <summary>事件落盘目录（JSONL，按天切分）。用于"先落盘再回 200"与重启后恢复去重键。</summary>
    public string SpoolDirectory { get; set; } = "./logs/acs-push/spool/";

    /// <summary>
    /// 是否"先落盘成功再返回 200"：落盘失败时返回 500，让设备重传。
    /// 这是断网/进程崩溃不丢事件的关键（关掉则退回"先回 200"，可能丢）。
    /// </summary>
    public bool PersistBeforeAck { get; set; } = true;

    /// <summary>去重缓存条数上限（按 (deviceId, serialNo) 去重，防止重传产生重复事件）。</summary>
    public int DedupCacheSize { get; set; } = 200000;

    /// <summary>允许的最大请求体（字节）。</summary>
    public int MaxBodyBytes { get; set; } = 32 * 1024 * 1024;
}
