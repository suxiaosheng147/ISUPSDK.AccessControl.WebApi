namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// ISAPI 报文发往设备时使用的通道（对应 appsettings.json 的 <c>ISUP:IsapiChannel</c>）。
/// <para>
/// 四条通道的<b>请求路径与报文体完全相同</b>，区别只在于「用哪个通道把这条 HTTP 请求发到设备」：
/// <list type="bullet">
/// <item><c>http-sdk</c>：SDK 转发 HTTP 请求 —— <c>NET_ECMS_ISAPIPassThrough</c>，请求行带方法
/// （<c>"POST /ISAPI/AccessControl/UserInfo/Record?format=json"</c>）；默认通道。</item>
/// <item><c>http-IsapiDigest</c>：平台直连设备的 HTTP 请求 —— <c>HttpClient</c> + Digest 认证
/// （设备 IP/账号密码取自 <c>ISUP:IsapiDigest:Devices</c>），不经 SDK。</item>
/// <item><c>http-http</c>：<see cref="IsapiChannel.HttpDigest"/> 的别名，效果完全相同（同样带 Digest 认证直连）。</item>
/// <item><c>http-xml</c>：透传 —— SDK 的 PTXML 接口 <c>NET_ECMS_Get/Put/Post/DeletePTXMLConfig</c>
/// （XML 报文透传，按 HTTP 方法调对应接口，请求行只给路径）。</item>
/// </list>
/// </para>
/// <para>
/// 部分设备/固件只认其中某一条（例如旧机型不支持 ISAPI 透传、或 PTXML 取事件返回空），
/// 因此这里支持按设备编号逐台切换，未列出的设备走 <see cref="DefaultChannel"/>。
/// </para>
/// </summary>
public sealed class IsapiChannelOptions
{
    public const string SectionName = "ISUP:IsapiChannel";

    /// <summary>通道取值：SDK 转发 HTTP 请求（<c>NET_ECMS_ISAPIPassThrough</c>）。</summary>
    public const string HttpSdkValue = "http-sdk";

    /// <summary>通道取值：平台 Digest 直连设备（<c>HttpClient</c>）。</summary>
    public const string IsapiDigestValue = "http-IsapiDigest";

    /// <summary>通道取值：<see cref="IsapiDigestValue"/> 的别名。</summary>
    public const string HttpValue = "http-http";

    /// <summary>通道取值：透传（XML 报文走 SDK 的 PTXML 接口）。</summary>
    public const string HttpXmlValue = "http-xml";

    /// <summary>
    /// 未在 <see cref="Devices"/> 中列出的设备使用的通道。
    /// <para>默认 <c>http-sdk</c>；取值见 <see cref="HttpSdkValue"/> / <see cref="IsapiDigestValue"/> /
    /// <see cref="HttpValue"/> / <see cref="HttpXmlValue"/>（忽略大小写与连字符）。</para>
    /// </summary>
    public string DefaultChannel { get; set; } = HttpSdkValue;

    /// <summary>按设备编号指定通道（可配多台）；未列出或编号为空的设备走 <see cref="DefaultChannel"/>。</summary>
    public List<IsapiChannelDevice> Devices { get; set; } = [];

    /// <summary>
    /// 按设备编号取通道：命中 <see cref="Devices"/> 用该设备的配置，否则用 <see cref="DefaultChannel"/>。
    /// 取值无法识别时回退 <see cref="IsapiChannel.HttpSdk"/>。
    /// </summary>
    /// <param name="deviceId">设备编号（与设备表 / 报警回调里的 <c>DeviceId</c> 一致，忽略大小写）。</param>
    public IsapiChannel Resolve(string? deviceId)
    {
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            foreach (var device in Devices)
            {
                if (string.Equals(device.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                {
                    return TryParseChannel(device.Channel, out var configured) ? configured : IsapiChannel.HttpSdk;
                }
            }
        }

        return TryParseChannel(DefaultChannel, out var fallback) ? fallback : IsapiChannel.HttpSdk;
    }

    /// <summary>
    /// 解析通道取值。接受 <c>http-sdk</c> / <c>http-IsapiDigest</c> / <c>http-http</c> / <c>http-xml</c>
    /// 及其常见别名（<c>sdk</c> / <c>digest</c> / <c>isapidigest</c> / <c>http</c> / <c>xml</c> / <c>ptxml</c>），
    /// 忽略大小写与 <c>-</c> / <c>_</c>。
    /// </summary>
    public static bool TryParseChannel(string? value, out IsapiChannel channel)
    {
        channel = IsapiChannel.HttpSdk;

        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var key = value.Trim().Replace("-", string.Empty).Replace("_", string.Empty).ToLowerInvariant();

        switch (key)
        {
            case "httpsdk":
            case "sdk":
            case "isapipassthrough":
                channel = IsapiChannel.HttpSdk;
                return true;

            case "httpisapidigest":
            case "isapidigest":
            case "digest":
            case "httphttp":
            case "http":
                channel = IsapiChannel.HttpDigest;
                return true;

            case "httpxml":
            case "xml":
            case "ptxml":
                channel = IsapiChannel.HttpXml;
                return true;

            default:
                return false;
        }
    }

    /// <summary>通道的配置取值（用于日志与接口返回）。</summary>
    public static string Describe(IsapiChannel channel) => channel switch
    {
        IsapiChannel.HttpDigest => IsapiDigestValue,
        IsapiChannel.HttpXml => HttpXmlValue,
        _ => HttpSdkValue,
    };

    /// <summary>配置里无法识别的通道取值（启动时记一条告警，避免写错后静默走默认通道）。</summary>
    public IEnumerable<string> EnumerateUnknownValues()
    {
        if (!TryParseChannel(DefaultChannel, out _))
        {
            yield return $"DefaultChannel={DefaultChannel}";
        }

        foreach (var device in Devices)
        {
            if (!TryParseChannel(device.Channel, out _))
            {
                yield return $"Devices[{device.DeviceId}].Channel={device.Channel}";
            }
        }
    }
}

/// <summary>一台设备的通道配置。</summary>
public sealed class IsapiChannelDevice
{
    /// <summary>设备编号（序列号），需与设备表 / 报警回调中的设备 ID 一致。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>该设备使用的通道，取值同 <see cref="IsapiChannelOptions.DefaultChannel"/>。</summary>
    public string Channel { get; set; } = IsapiChannelOptions.HttpSdkValue;
}

/// <summary>ISAPI 报文的设备侧通道（配置取值见 <see cref="IsapiChannelOptions"/>）。</summary>
public enum IsapiChannel
{
    /// <summary>
    /// <c>http-sdk</c>：SDK 转发 HTTP 请求 —— <c>NET_ECMS_ISAPIPassThrough</c>，
    /// 请求行带方法（<c>"POST /ISAPI/..."</c>）。
    /// </summary>
    HttpSdk = 0,

    /// <summary>
    /// <c>http-IsapiDigest</c> / <c>http-http</c>：平台直连设备的 HTTP 请求 ——
    /// <c>HttpClient</c> + Digest 认证，设备地址与账号密码取自 <c>ISUP:IsapiDigest:Devices</c>。
    /// </summary>
    HttpDigest = 1,

    /// <summary>
    /// <c>http-xml</c>：透传 —— SDK 的 PTXML 接口 <c>NET_ECMS_Get/Put/Post/DeletePTXMLConfig</c>，
    /// 按 HTTP 方法调对应接口，请求行只给路径。
    /// </summary>
    HttpXml = 2,
}
