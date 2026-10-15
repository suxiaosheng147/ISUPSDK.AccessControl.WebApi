namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 日志配置（appsettings.json 的 ISUP:Logging 节）。
/// <para>
/// 日志级别与输出目标由标准的 "Logging" 节控制（Console / Debug 等）；
/// 这里只决定业务日志是否仍按原控制台习惯落盘。
/// </para>
/// </summary>
public sealed class IsupLoggingOptions
{
    public const string SectionName = "ISUP:Logging";

    /// <summary>
    /// 是否保留文件落盘：true（默认）时业务日志在进入 ILogger 的同时，
    /// 仍写入 <c>&lt;ISUP:LogDirectory&gt;/log/log_yyyyMMdd/</c>，便于现场排查。
    /// </summary>
    public bool FileFallback { get; set; } = true;
}
