using ISUPSDK.AccessControl.WebApi.Customize;
using ISUPSDK.AccessControl.WebApi.Hosting;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Logging;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Modules;

/// <summary>
/// ISUP SDK 模块：配置绑定、统一日志门面、SDK 运行时与后台初始化。
/// <para>
/// 该模块只负责"设备接入"这条链路（CMS 注册 / 报警 / 图片存储 / 定时任务），
/// 与 Web 宿主（CORS、MVC、Swagger）解耦，便于单独启用或替换。
/// </para>
/// </summary>
public static class IsupSdkModule
{
    /// <summary>注册 ISUP SDK 运行时与后台初始化服务。</summary>
    public static WebApplicationBuilder AddIsupSdk(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<IsupSdkOptions>(builder.Configuration.GetSection(IsupSdkOptions.SectionName));
        builder.Services.Configure<IsupLoggingOptions>(builder.Configuration.GetSection(IsupLoggingOptions.SectionName));

        // 运行时承载原控制台 Program.cs 的全部业务逻辑，进程内单例即可。
        builder.Services.AddSingleton<IsupSdkRuntime>();
        // SDK 初始化必须先于接口可用；初始化失败不阻断进程，/health 会呈现 degraded。
        builder.Services.AddHostedService<IsupSdkHostedService>();

        // crontab 定时任务（Customize/CronBackgroundService）：按周期触发一轮任务体。
        builder.Services.AddHostedService<SyncDevicesCronJob>();   // 遍历在线设备做权限同步
        builder.Services.AddHostedService<DeviceTimeCronJob>();    // 遍历在线设备做 NTP 校时

        return builder;
    }

    /// <summary>
    /// 接入统一日志门面并初始化日志目录。
    /// <para>
    /// 必须在应用构建之后、任何业务日志产生之前调用：此后
    /// <c>Common.WriteFile*</c> 与 SDK 回调内的日志都会进入 <see cref="ILogger"/>。
    /// </para>
    /// </summary>
    public static WebApplication UseIsupSdk(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<IsupSdkOptions>>().Value;
        var loggingOptions = app.Services.GetRequiredService<IOptions<IsupLoggingOptions>>().Value;

        var logRoot = string.IsNullOrWhiteSpace(options.LogDirectory)
            ? AppContext.BaseDirectory
            : Path.GetFullPath(options.LogDirectory, AppContext.BaseDirectory);

        try
        {
            Directory.CreateDirectory(logRoot);
        }
        catch (Exception ex)
        {
            // 门面尚未接入，这里只能用控制台提示。
            Console.WriteLine($"[LOG] 业务日志目录初始化失败，沿用程序目录：{ex.Message}");
            logRoot = AppContext.BaseDirectory;
        }

        IsupLog.LogRootDirectory = logRoot;
        IsupLog.Attach(app.Services.GetRequiredService<ILoggerFactory>(), loggingOptions.FileFallback);

        return app;
    }
}
