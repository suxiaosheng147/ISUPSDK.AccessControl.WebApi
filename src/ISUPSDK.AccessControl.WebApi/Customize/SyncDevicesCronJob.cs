using ISUPSDK.AccessControl.WebApi.Services;

namespace ISUPSDK.AccessControl.WebApi.Customize;

/// <summary>
/// Customize：设备信息同步定时任务。
/// 按 <c>Crontab:SyncDevices</c> 周期遍历所有在线设备，先询问平台该设备走「认证规则」还是「普通同步」，
/// 再分别执行人员 / 卡 / 人脸 / 门禁 / 请假等同步。
/// </summary>
public sealed class SyncDevicesCronJob : CronBackgroundService
{
    private readonly IsupSdkRuntime _runtime;

    public SyncDevicesCronJob(
        IsupSdkRuntime runtime,
        IConfiguration configuration,
        ILogger<SyncDevicesCronJob> logger)
        : base(configuration, logger, "Crontab:SyncDevices", "设备信息同步")
    {
        _runtime = runtime;
    }

    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
        => _runtime.SyncOnlineDevicesAsync(cancellationToken);
}
