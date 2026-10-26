namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// Web API 宿主自身的配置（appsettings.json 的 ISUP 节）。
/// 业务配置仍沿用原控制台项目的 Settings 节，节名由 <see cref="SettingsSection"/> 指定。
/// </summary>
public sealed class IsupSdkOptions
{
    public const string SectionName = "ISUP";

    /// <summary>业务配置所在节名，默认 "Settings"（与原控制台项目一致）。</summary>
    public string SettingsSection { get; set; } = "Settings";

    /// <summary>
    /// ISUP SDK 原生库目录（HCISUPCMS.dll / HCISUPAlarm.dll / HCISUPSS.dll / HCISUPStream.dll 等）。
    /// 留空则自动探测。
    /// </summary>
    public string NativeDirectory { get; set; } = string.Empty;

    /// <summary>启动时自动初始化 SDK 并监听设备注册。</summary>
    public bool AutoStartListen { get; set; } = true;

    /// <summary>SDK 各组件日志目录（对应 SDK 的 SetLogToFile）。</summary>
    public string SdkLogDirectory { get; set; } = "./logs/isupsdk/";

    /// <summary>离线设备记录保留天数（设备表清理；&lt;=0 表示不清理，默认 7 天）。</summary>
    public int DeviceOfflineRetentionDays { get; set; } = 7;

    /// <summary>
    /// 日志根目录（NLog 落盘位置）：业务日志
    /// <c>&lt;LogDirectory&gt;/log/&lt;类名&gt;/&lt;yyyy-MM-dd&gt;/&lt;HH&gt;.log</c>，
    /// 宿主日志 <c>&lt;LogDirectory&gt;/log/&lt;yyyy-MM-dd&gt;/app.log</c>；相对路径按程序目录解析。
    /// </summary>
    public string LogDirectory { get; set; } = "./logs/";
}
