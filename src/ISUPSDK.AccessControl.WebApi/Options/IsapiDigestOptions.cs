namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 设备 ISAPI 直连（HTTP Digest 认证）配置（对应 appsettings.json 的 <c>ISUP:IsapiDigest</c>）。
/// <para>
/// 用途：不依赖 SDK 设备注册，直接用设备 IP + 账号密码访问 ISAPI
/// （<c>POST /ISAPI/AccessControl/AcsEvent?format=json</c>）拉取门禁事件；
/// 相比 SDK 透传，这类报文里带 <c>pictureURL</c>（人脸事件的抓拍图），可以取回图片。
/// </para>
/// <para>
/// 对账优先走本通道：Digest 查询成功就不再用 SDK 对账，失败（连不上 / 认证失败 / 解析失败）才回退 SDK 对账兜底。
/// </para>
/// </summary>
public sealed class IsapiDigestOptions
{
    public const string SectionName = "ISUP:IsapiDigest";

    /// <summary>是否启用 ISAPI Digest 直连通道（false 时对账直接用 SDK 透传）。</summary>
    public bool Enabled { get; set; }

    /// <summary>单次 HTTP 请求超时（秒）。</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>是否忽略 HTTPS 证书错误（设备多为自签名证书，默认忽略）。</summary>
    public bool IgnoreSslErrors { get; set; } = true;

    /// <summary>是否下载 <c>pictureURL</c> 指向的抓拍图（下载后随转发报文给出本地路径）。</summary>
    public bool DownloadPictures { get; set; } = true;

    /// <summary>
    /// 抓拍图落盘目录。留空时沿用 <c>Settings:Photo</c>；相对路径按程序运行目录解析。
    /// </summary>
    public string PictureDirectory { get; set; } = string.Empty;

    /// <summary>
    /// 抓拍图对外 URL 前缀（转发报文里 <c>ExtEventPictureURL</c> 与事件查询的 <c>pictureUrls</c> 都用它）。
    /// <para>
    /// 这里是<b>唯一</b>来源，不再按 <c>Settings:ServerAddress</c> 推导（那个地址通常是设备网段的内网 IP，
    /// 浏览器不一定能访问）。配成 <c>http://10.0.0.5:5190/api/events/pictures</c>；
    /// 反代 / 域名场景写 <c>https://acs.example.com/acs-pictures</c>（末尾不要带 <c>/</c>）。
    /// </para>
    /// <para>留空时用相对路径 <c>/api/events/pictures</c>：前端按页面同源解析，可直接显示图片。</para>
    /// </summary>
    public string PictureUrlPrefix { get; set; } = string.Empty;

    /// <summary>单张图片大小上限（字节），超过则只保留 URL 不落盘。</summary>
    public int MaxPictureBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    /// 配置了 Digest 的设备，在 Digest 查询失败（连不上 / 认证失败 / 解析失败）时是否回退 SDK 对账兜底。
    /// <para>
    /// true（默认）：失败即回退，尽量不漏事件；false：配置了 Digest 的设备只走直连，失败也不跑 SDK 对账。
    /// 未配置 Digest 但在线的设备始终走 SDK 对账。
    /// </para>
    /// </summary>
    public bool FallbackToSdk { get; set; } = true;

    /// <summary>
    /// 独立的 Digest 轮询间隔（秒）。人脸抓拍图只存在设备本地且会被循环覆盖，越早拉越能拿到；
    /// <c>&lt;= 0</c> 表示关闭这路轮询（只在对账时用直连）。
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>轮询首轮的增量回看（秒）。</summary>
    public int PollLookbackSeconds { get; set; } = 300;

    /// <summary>轮询与上次轮询时间的重叠（秒），避免边界漏事件。</summary>
    public int PollOverlapSeconds { get; set; } = 30;

    /// <summary>轮询单轮单设备最多取的页数（页大小沿用 <c>EventReconcile:PageSize</c>）。</summary>
    public int PollMaxPages { get; set; } = 10;

    /// <summary>设备清单（可配多台）。</summary>
    public List<IsapiDigestDevice> Devices { get; set; } = [];
}

/// <summary>一台可通过 ISAPI Digest 直连的设备。</summary>
public sealed class IsapiDigestDevice
{
    /// <summary>设备编号，需与 SDK/报警回调里的设备 ID 一致（用于事件归属与去重）。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>设备 IP 或主机名。</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>端口；留空/0 时按 <see cref="UseHttps"/> 取 443 / 80。</summary>
    public int Port { get; set; }

    /// <summary>是否走 HTTPS（海康设备 ISAPI 一般用 https）。</summary>
    public bool UseHttps { get; set; } = true;

    /// <summary>设备账号。</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>设备密码。</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>实际使用的端口。</summary>
    public int ResolvePort() => Port > 0 ? Port : UseHttps ? 443 : 80;

    /// <summary>基地址（形如 <c>https://192.168.1.167:443</c>，端口为默认值时省略）。</summary>
    public string ResolveBaseUrl()
    {
        var scheme = UseHttps ? "https" : "http";
        var port = ResolvePort();
        var isDefault = (UseHttps && port == 443) || (!UseHttps && port == 80);

        return isDefault
            ? $"{scheme}://{Host}"
            : $"{scheme}://{Host}:{port}";
    }

    /// <summary>是否可用于直连（编号/地址/账号密码齐全）。</summary>
    public bool IsUsable()
        => !string.IsNullOrWhiteSpace(DeviceId)
           && !string.IsNullOrWhiteSpace(Host)
           && !string.IsNullOrWhiteSpace(UserName)
           && !string.IsNullOrWhiteSpace(Password);
}
