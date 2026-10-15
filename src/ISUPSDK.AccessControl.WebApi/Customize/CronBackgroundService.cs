namespace ISUPSDK.AccessControl.WebApi.Customize;

/// <summary>
/// Customize 定时任务基类：用 <see cref="CronJobScheduler"/> + NCrontab 按周期触发，
/// 由 .NET 托管服务宿主驱动，子类只需实现"一轮要做什么"（通常是遍历所有在线设备）。
/// <para>
/// 约定：
/// <list type="bullet">
/// <item>crontab 从配置文件读取，键名由子类给出（如 <c>Crontab:SyncDevices</c>）；未配置则不启动；</item>
/// <item>同一任务<b>上一轮未跑完时跳过本轮</b>，不会叠加执行；</item>
/// <item>异常统一在此捕获记录（个别轮次失败不影响后续轮次）。</item>
/// </list>
/// </para>
/// </summary>
public abstract class CronBackgroundService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    private readonly string _cronConfigKey;
    private readonly string _jobName;

    /// <summary>防重入标记：1 表示本轮仍在执行。</summary>
    private int _running;

    protected CronBackgroundService(
        IConfiguration configuration,
        ILogger logger,
        string cronConfigKey,
        string jobName)
    {
        _configuration = configuration;
        _logger = logger;
        _cronConfigKey = cronConfigKey;
        _jobName = jobName;
    }

    /// <summary>任务名（日志用）。</summary>
    protected string JobName => _jobName;

    /// <summary>一轮任务体（子类实现）。</summary>
    protected abstract Task ExecuteOnceAsync(CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cron = _configuration[_cronConfigKey];
        if (string.IsNullOrWhiteSpace(cron))
        {
            _logger.LogWarning("{Job} 未配置 Cron 表达式（{Key}），定时任务未启动。", _jobName, _cronConfigKey);
            return;
        }

        try
        {
            using var scheduler = new CronJobScheduler();
            scheduler.AddJob(cron, SafeExecuteAsync, ex => _logger.LogError(ex, "{Job} 执行异常。", _jobName));

            _logger.LogInformation(
                "{Job} 定时任务已启动，Cron：{Cron}（{Key}），下次执行 {Next}",
                _jobName,
                cron,
                _cronConfigKey,
                CronJobScheduler.GetNextOccurrence(cron, DateTime.Now));

            // 作业由调度器内部循环驱动，这里只需挂住直至应用停止。
            await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 应用正常停止
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Job} 定时任务异常。", _jobName);
        }
    }

    /// <summary>带防重入与异常兜底的一轮执行。</summary>
    private async Task SafeExecuteAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            _logger.LogWarning("{Job} 上一轮尚未结束，跳过本轮。", _jobName);
            return;
        }

        try
        {
            await ExecuteOnceAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // 停止中，忽略
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Job} 执行异常。", _jobName);
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }
}
