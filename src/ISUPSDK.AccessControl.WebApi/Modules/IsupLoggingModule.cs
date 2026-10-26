using System.Text;
using ISUPSDK.AccessControl.WebApi.Options;
using NLog;
using NLog.Config;
using NLog.Targets;
using NLog.Web;

namespace ISUPSDK.AccessControl.WebApi.Modules;

/// <summary>
/// 日志模块：整个进程的日志只走 <b>NLog</b>。
/// <para>
/// 做三件事：
/// 1. 解析日志根目录（<c>ISUP:Logging:Directory</c> → <c>ISUP:LogDirectory</c> → 程序目录），
///    并以 <c>${var:logDir}</c> 注入 NLog 配置；
/// 2. 加载 <c>nlog.config</c>（缺失/写坏时退回代码内置的等价默认配置，保证日志不会因为配置文件丢了就停掉）；
/// 3. <c>ClearProviders()</c> 掉内置 Console / Debug / EventSource，再 <c>UseNLog()</c>
///    （NLog.Web.AspNetCore 集成）。此后宿主 <c>ILogger&lt;T&gt;</c>、EF Core、Kestrel 与业务侧
///    <c>IsupLog</c>（原 <c>Common.WriteFile*</c>）全部由 NLog 决定去向与格式。
/// </para>
/// <para>
/// 落盘布局：业务日志 <c>logs\log\&lt;类名&gt;\&lt;yyyy-MM-dd&gt;\&lt;HH&gt;.log</c>，
/// 宿主日志 <c>logs\log\&lt;yyyy-MM-dd&gt;\app.log</c>。
/// </para>
/// <para>
/// 与 <c>web.config</c> 的关系：ANCM 的 <c>stdoutLogEnabled</c> 已关闭，不再生成 <c>logs\stdout_*.log</c>；
/// IIS 承载时 stdout 没有接收者，日志以 <c>nlog.config</c> 里的文件目标为准。
/// </para>
/// </summary>
public static class IsupLoggingModule
{
    /// <summary>NLog 变量名：日志根目录（nlog.config 里的 <c>${var:logDir}</c>）。</summary>
    public const string LogDirectoryVariable = "logDir";

    private const string BizLayout =
        "${longdate} [${level:uppercase=true}] ${message}${onexception:${newline}${exception:format=ToString}}";

    private const string AppLayout =
        "${longdate} [${level:uppercase=true}] ${logger}: ${message}${onexception:${newline}${exception:format=ToString}}";

    private const string DebugLayout =
        "${level:uppercase=true} ${logger}: ${message}${onexception:${newline}${exception:format=ToString}}";

    /// <summary>注册 NLog（必须最先调用，保证启动早期日志也进统一管道）。</summary>
    public static WebApplicationBuilder AddIsupNLog(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var loggingOptions = builder.Configuration.GetSection(IsupLoggingOptions.SectionName).Get<IsupLoggingOptions>()
                             ?? new IsupLoggingOptions();
        var sdkOptions = builder.Configuration.GetSection(IsupSdkOptions.SectionName).Get<IsupSdkOptions>()
                         ?? new IsupSdkOptions();

        var logRoot = ResolveLogRoot(loggingOptions, sdkOptions);
        EnsureDirectory(logRoot);

        var config = LoadConfiguration(builder, loggingOptions) ?? CreateDefaultConfiguration();

        // ISUP:LogDirectory 里的相对路径已按程序目录解析为绝对路径，交给 nlog.config 的文件目标。
        config.Variables[LogDirectoryVariable] = logRoot;
        LogManager.Configuration = config;

        // 清掉 Host 默认的 Console / Debug / EventSource provider：日志出口只剩 NLog。
        builder.Logging.ClearProviders();

        // NLog 的 ASP.NET Core 集成（NLog.Web.AspNetCore）：注册 NLog provider，并把 HttpContext
        // 接到 NLog 的 ScopeContext 上（可用 ${aspnet-*} 布局、请求内日志按请求归集）。
        builder.UseNLog();

        return builder;
    }

    /// <summary>
    /// 正常停机时 flush / 关闭 NLog target（IIS 回收、进程被强杀时由 NLog 的进程退出钩子兜底）。
    /// </summary>
    public static WebApplication UseIsupNLog(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Lifetime.ApplicationStopped.Register(LogManager.Shutdown);

        return app;
    }

    #region 日志根目录

    /// <summary><c>ISUP:Logging:Directory</c> 优先，其次沿用 <c>ISUP:LogDirectory</c>，都为空则用程序目录。</summary>
    private static string ResolveLogRoot(IsupLoggingOptions loggingOptions, IsupSdkOptions sdkOptions)
    {
        var configured = string.IsNullOrWhiteSpace(loggingOptions.Directory)
            ? sdkOptions.LogDirectory
            : loggingOptions.Directory;

        if (string.IsNullOrWhiteSpace(configured))
        {
            return AppContext.BaseDirectory;
        }

        try
        {
            return Path.GetFullPath(configured, AppContext.BaseDirectory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[LOG] 日志根目录配置无效（{configured}），改用程序目录：{ex.Message}");
            return AppContext.BaseDirectory;
        }
    }

    private static void EnsureDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex)
        {
            // 目录建不出来时 NLog 首次写入会再试一次；日志系统自身不能用日志报告故障。
            Console.WriteLine($"[LOG] 日志目录创建失败：{directory}（{ex.Message}）");
        }
    }

    #endregion

    #region NLog 配置加载

    /// <summary>
    /// 依次在内容根目录、程序目录下找 <c>ISUP:Logging:ConfigFile</c>（默认 <c>nlog.config</c>）。
    /// 找不到或解析失败返回 null，由调用方退回内置默认配置。
    /// </summary>
    private static LoggingConfiguration? LoadConfiguration(WebApplicationBuilder builder, IsupLoggingOptions loggingOptions)
    {
        var fileName = string.IsNullOrWhiteSpace(loggingOptions.ConfigFile)
            ? IsupLoggingOptions.DefaultConfigFile
            : loggingOptions.ConfigFile.Trim();

        foreach (var candidate in ResolveConfigCandidates(builder, fileName))
        {
            if (!File.Exists(candidate))
            {
                continue;
            }

            try
            {
                return new XmlLoggingConfiguration(candidate);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LOG] NLog 配置解析失败，继续下一个候选项：{candidate}（{ex.Message}）");
            }
        }

        Console.WriteLine($"[LOG] 未找到可用的 NLog 配置文件（{fileName}），使用内置默认配置。");
        return null;
    }

    private static IEnumerable<string> ResolveConfigCandidates(WebApplicationBuilder builder, string fileName)
    {
        if (Path.IsPathRooted(fileName))
        {
            yield return fileName;
            yield break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // run.ps1 把工作目录设为项目目录、发布后是站点目录，两者都可能是内容根。
        foreach (var root in new[] { builder.Environment.ContentRootPath, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            string candidate;
            try
            {
                candidate = Path.GetFullPath(fileName, root);
            }
            catch (Exception)
            {
                continue;
            }

            if (seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    /// <summary>与随发布带的 nlog.config 等价的兜底配置：防止配置文件缺失后日志彻底静默。</summary>
    private static LoggingConfiguration CreateDefaultConfiguration()
    {
        var config = new LoggingConfiguration();
        config.Variables[LogDirectoryVariable] = "${basedir}/logs";

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var bizFile = new FileTarget("bizFile")
        {
            FileName = "${var:logDir}/log/${logger:shortName=true}/${date:format=yyyy-MM-dd}/${date:format=HH}.log",
            Layout = BizLayout,
            Encoding = encoding,
            CreateDirs = true,
            KeepFileOpen = true,
        };

        var appFile = new FileTarget("appFile")
        {
            FileName = "${var:logDir}/log/${date:format=yyyy-MM-dd}/app.log",
            Layout = AppLayout,
            Encoding = encoding,
            CreateDirs = true,
            KeepFileOpen = true,
        };

        var console = new ConsoleTarget("console") { Layout = AppLayout };
        var debugger = new DebuggerTarget("debugger") { Layout = DebugLayout };

        config.AddTarget(bizFile);
        config.AddTarget(appFile);
        config.AddTarget(console);
        config.AddTarget(debugger);

        // 业务日志（IsupLog 门面）：文件 + 控制台，命中后不再重复落到 app.log。
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, bizFile, "IsupLog*");
        config.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, console, "IsupLog*", final: true);

        // 宿主日志：文件 + 控制台（级别同时受 appsettings.json 的 Logging:LogLevel 过滤）。
        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, appFile);
        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, console);

        // 本地调试：Debug 级别只进 VS 输出窗口，不落盘。
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, debugger);

        return config;
    }

    #endregion
}
