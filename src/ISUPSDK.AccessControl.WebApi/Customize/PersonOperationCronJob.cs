using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Services;

namespace ISUPSDK.AccessControl.WebApi.Customize;

/// <summary>
/// 人员下发队列的一次性初始化（幂等）。
/// <para>
/// 队列的数据库配置来自 <c>Settings</c> 节，而 <see cref="IsupSdkRuntime.CurrentSettings"/> 要到宿主启动
/// （<c>IsupSdkHostedService</c>）之后才有值，所以初始化推迟到定时任务第一次运行时。
/// </para>
/// </summary>
internal static class PersonOperationQueueStartup
{
    private static int _initialized;

    public static void EnsureInitialized(IsupSdkRuntime runtime)
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        PersonOperationQueue.Initialize(runtime.CurrentSettings);
    }
}

/// <summary>
/// Customize：人员下发队列 —— <b>入队</b>任务。
/// 按 <c>Crontab:PersonOperationQueue</c> 周期取队列（<c>isup_person_queue.db</c>）里待下发的记录，
/// 拆成人员 / 卡 / 人脸请求交给本服务控制器入队（AcsTask），拿到任务 ID 即结束，<b>不等设备执行</b>。
/// </summary>
public sealed class PersonOperationCronJob : CronBackgroundService
{
    private readonly IsupSdkRuntime _runtime;
    private readonly PersonOperationExecutor _executor;

    public PersonOperationCronJob(
        IsupSdkRuntime runtime,
        PersonOperationExecutor executor,
        IConfiguration configuration,
        ILogger<PersonOperationCronJob> logger)
        : base(configuration, logger, "Crontab:PersonOperationQueue", "人员下发队列（入队）")
    {
        _runtime = runtime;
        _executor = executor;
    }

    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        PersonOperationQueueStartup.EnsureInitialized(_runtime);
        return _executor.PublishAsync(cancellationToken);
    }
}

/// <summary>
/// Customize：人员下发队列 —— <b>结果收集</b>任务。
/// 按 <c>Crontab:PersonOperationResult</c> 周期查询已入队人员对应的控制器任务（<c>/api/tasks/{id}</c>），
/// 全部结束后把最终结果回传平台。与入队任务相互独立，控制器任务下发慢也不会挡住新的入队。
/// </summary>
public sealed class PersonOperationResultCronJob : CronBackgroundService
{
    private readonly IsupSdkRuntime _runtime;
    private readonly PersonOperationExecutor _executor;

    public PersonOperationResultCronJob(
        IsupSdkRuntime runtime,
        PersonOperationExecutor executor,
        IConfiguration configuration,
        ILogger<PersonOperationResultCronJob> logger)
        : base(configuration, logger, "Crontab:PersonOperationResult", "人员下发队列（收结果）")
    {
        _runtime = runtime;
        _executor = executor;
    }

    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        PersonOperationQueueStartup.EnsureInitialized(_runtime);
        return _executor.CollectAsync(cancellationToken);
    }
}

/// <summary>
/// Customize：删除兜底定时任务。
/// 按 <c>Crontab:PersonDeleteVerify</c> 周期检查"下发删除成功但人员仍在设备上"的情况，重新下发一次删除。
/// </summary>
public sealed class PersonDeleteVerifyCronJob : CronBackgroundService
{
    private readonly IsupSdkRuntime _runtime;
    private readonly PersonOperationExecutor _executor;

    public PersonDeleteVerifyCronJob(
        IsupSdkRuntime runtime,
        PersonOperationExecutor executor,
        IConfiguration configuration,
        ILogger<PersonDeleteVerifyCronJob> logger)
        : base(configuration, logger, "Crontab:PersonDeleteVerify", "人员删除兜底")
    {
        _runtime = runtime;
        _executor = executor;
    }

    protected override Task ExecuteOnceAsync(CancellationToken cancellationToken)
    {
        PersonOperationQueueStartup.EnsureInitialized(_runtime);
        return _executor.RunDeleteVerifyAsync(cancellationToken);
    }
}
