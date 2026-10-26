namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 日志配置（appsettings.json 的 ISUP:Logging 节）。
/// <para>
/// 输出目标、格式、滚动方式都在 <c>nlog.config</c> 里配（NLog 是本项目唯一的日志框架）；
/// 这里只留"从哪儿读配置"和"日志放哪儿"两个引导参数。
/// 日志级别仍按 ASP.NET Core 的习惯配在 "Logging:LogLevel"（NLog 之前的那层过滤）。
/// </para>
/// </summary>
public sealed class IsupLoggingOptions
{
    public const string SectionName = "ISUP:Logging";

    /// <summary>NLog 配置文件名（相对内容根目录，使用 <c>nlog.config</c>，随项目复制到输出目录）。</summary>
    public const string DefaultConfigFile = "nlog.config";

    /// <summary>
    /// NLog 配置文件路径：相对路径会在「内容根目录」和「程序目录」下依次查找；
    /// 留空用 <see cref="DefaultConfigFile"/>；文件缺失或写坏时退回代码内置的等价默认配置。
    /// </summary>
    public string ConfigFile { get; set; } = DefaultConfigFile;

    /// <summary>
    /// 日志根目录：业务日志落 <c>&lt;Directory&gt;/log/&lt;类名&gt;/&lt;yyyy-MM-dd&gt;/&lt;HH&gt;.log</c>，
    /// 宿主日志落 <c>&lt;Directory&gt;/log/&lt;yyyy-MM-dd&gt;/app.log</c>。
    /// 留空时沿用 <c>ISUP:LogDirectory</c>（默认 <c>./logs/</c>）；相对路径按程序目录解析。
    /// </summary>
    public string Directory { get; set; } = string.Empty;
}
