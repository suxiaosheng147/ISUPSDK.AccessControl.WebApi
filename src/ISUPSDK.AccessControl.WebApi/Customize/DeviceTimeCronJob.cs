using ISUPSDK.AccessControl.WebApi.Services;

namespace ISUPSDK.AccessControl.WebApi.Customize;

/// <summary>
/// Customize：设备校时定时任务。
/// 按 <c>Crontab:DeviceTime</c> 周期遍历所有在线设备，下发一次当前（北京）时间。
/// </summary>
public sealed class DeviceTimeCronJob : CronBackgroundService
{
    private readonly IsupSdkRuntime _runtime;

    public DeviceTimeCronJob(
        IsupSdkRuntime runtime,
        IConfiguration configuration,
        ILogger<DeviceTimeCronJob> logger)
        : base(configuration, logger, "Crontab:DeviceTime", "设备校时")
    {
        _runtime = runtime;
    }

    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
        => _runtime.SyncDeviceTimeAllAsync(cancellationToken);
}
