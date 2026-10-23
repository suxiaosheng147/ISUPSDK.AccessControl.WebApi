namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>门远程控制（RemoteControl）报文的编码格式。</summary>
public enum AcsDoorControlFormat
{
    /// <summary>
    /// JSON 报文：<c>PUT /ISAPI/AccessControl/RemoteControl/door/&lt;doorNo&gt;?format=json</c>，
    /// 报文体 <c>{"RemoteControlDoor":{"cmd":"open"}}</c>。海康通用格式。
    /// </summary>
    Json = 0,

    /// <summary>
    /// XML 报文（请求行不带 <c>format=json</c>）：
    /// <c>&lt;RemoteControlDoor&gt;&lt;cmd&gt;open&lt;/cmd&gt;&lt;/RemoteControlDoor&gt;</c>。
    /// 部分固件只认这种（实测 DS-K1T670M 收 JSON 会回 <c>MessageParametersLack</c>）。
    /// </summary>
    Xml = 1,

    /// <summary>
    /// 先发 JSON，设备以 <c>MessageParametersLack</c> / <c>badParameters</c> / <c>notSupport</c>
    /// 拒绝时自动重发 XML（SDK 层失败不重试，那与报文格式无关）。
    /// </summary>
    Auto = 2,
}

/// <summary>
/// 门禁业务行为配置（对应 appsettings.json 的 <c>ISUP:AccessControl</c>）。
/// </summary>
public sealed class IsupAccessControlOptions
{
    public const string SectionName = "ISUP:AccessControl";

    /// <summary>
    /// 门远程控制（开门/关门/常开/常闭）报文格式：<c>Json</c> / <c>Xml</c> / <c>Auto</c>
    /// （不区分大小写，无法识别时回退 <c>Json</c>）。
    /// </summary>
    public string DoorControlFormat { get; set; } = nameof(AcsDoorControlFormat.Json);

    /// <summary>解析 <see cref="DoorControlFormat"/>（无法识别时回退 <see cref="AcsDoorControlFormat.Json"/>）。</summary>
    public AcsDoorControlFormat ResolveDoorControlFormat()
    {
        if (Enum.TryParse<AcsDoorControlFormat>(DoorControlFormat, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AcsDoorControlFormat.Json;
    }
}
