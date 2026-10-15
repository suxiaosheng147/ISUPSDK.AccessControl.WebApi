using NCrontab;

namespace ISUPSDK.AccessControl.WebApi.Customize;

/// <summary>
/// NCrontab 调度器：可注册多个 cron 作业，各自按表达式周期触发。
/// <para>
/// 等待采用分段方式（每段最多 30 秒），系统时间被 NTP 调整后也能及时响应；
/// 作业回调的异常在内部兜底，个别作业出错不会影响其它作业。
/// </para>
/// </summary>
public sealed class CronJobScheduler : IDisposable
{
    /// <summary>单段等待上限。</summary>
    private static readonly TimeSpan MaxSlice = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _cts = new();
    private readonly List<Task> _loops = new();
    private bool _disposed;

    /// <summary>注册一个周期作业。</summary>
    public void AddJob(string crontabExpression, Func<CancellationToken, Task> callback, Action<Exception>? onError = null)
    {
        if (string.IsNullOrWhiteSpace(crontabExpression))
        {
            throw new ArgumentException("crontab 表达式不能为空。", nameof(crontabExpression));
        }

        ArgumentNullException.ThrowIfNull(callback);

        var schedule = CrontabSchedule.Parse(crontabExpression);
        _loops.Add(Task.Run(() => RunLoopAsync(schedule, callback, onError, _cts.Token)));
    }

    /// <summary>计算 <paramref name="now"/> 之后的第一次触发时刻。</summary>
    public static DateTime GetNextOccurrence(string crontabExpression, DateTime now)
        => CrontabSchedule.Parse(crontabExpression).GetNextOccurrence(now);

    private static async Task RunLoopAsync(
        CrontabSchedule schedule,
        Func<CancellationToken, Task> callback,
        Action<Exception>? onError,
        CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!await WaitUntilNextRunAsync(schedule, ct).ConfigureAwait(false))
            {
                return;
            }

            try
            {
                await callback(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
            }
        }
    }

    /// <summary>等到下一次触发时刻；返回 false 表示已请求取消。</summary>
    private static async Task<bool> WaitUntilNextRunAsync(CrontabSchedule schedule, CancellationToken ct)
    {
        var nextRun = schedule.GetNextOccurrence(DateTime.Now);

        while (DateTime.Now < nextRun)
        {
            if (ct.IsCancellationRequested)
            {
                return false;
            }

            var remain = nextRun - DateTime.Now;
            var slice = remain > MaxSlice ? MaxSlice : remain;

            try
            {
                await Task.Delay(slice, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            _cts.Cancel();
        }
        catch (Exception)
        {
            // 忽略：停止阶段异常不影响退出。
        }

        _cts.Dispose();
    }
}
