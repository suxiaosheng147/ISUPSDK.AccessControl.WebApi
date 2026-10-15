using ISUPSDK.AccessControl.WebApi.Interop;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Hosting;

/// <summary>
/// 把原控制台程序 <c>Main</c> 的启动流程搬进 Web 宿主：
/// 读取 ISUPDemo.xml → 初始化 CMS/Alarm/SS/Stream 并监听 → 清理过期文件 → 注册定时任务。
/// <para>
/// 与控制台不同，SDK 初始化失败不会让进程退出，服务以降级方式继续运行，
/// 状态通过 <c>/health</c> 与 <c>/api/system/sdk</c> 呈现。
/// </para>
/// </summary>
public sealed class IsupSdkHostedService : IHostedService
{
    private readonly IsupSdkRuntime _runtime;
    private readonly IsupSdkOptions _options;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IsupSdkHostedService> _logger;

    public IsupSdkHostedService(
        IsupSdkRuntime runtime,
        IOptions<IsupSdkOptions> options,
        IConfiguration configuration,
        ILogger<IsupSdkHostedService> logger)
    {
        _runtime = runtime;
        _options = options.Value;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var nativeDirectory = NativeLibraryResolver.Resolve(_options.NativeDirectory);
        if (nativeDirectory is null)
        {
            _logger.LogWarning(
                "未定位到 ISUP SDK 原生库目录（需要 HCISUPCMS.dll / HCISUPAlarm.dll 等）。" +
                "请把 SDK 目录拷贝到输出目录，或配置 ISUP:NativeDirectory / 环境变量 ISUP_SDK_DIR。");
        }
        else
        {
            _logger.LogInformation("ISUP SDK 原生库目录：{Directory}", nativeDirectory);
        }

        if (!_options.AutoStartListen)
        {
            _logger.LogWarning("ISUP:AutoStartListen=false，跳过 SDK 初始化与设备注册监听。");
            return;
        }

        try
        {
            await _runtime.InitializeAsync(_configuration, _options, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "ISUP SDK 初始化完成。CMS 监听句柄={Handle}，设备注册端口={Port}，当前在线设备={Count}。",
                _runtime.ListenHandle,
                _runtime.CurrentSettings.InitPort,
                _runtime.DeviceIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ISUP SDK 初始化失败，服务以降级方式继续运行（/health 会显示 degraded）。");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
