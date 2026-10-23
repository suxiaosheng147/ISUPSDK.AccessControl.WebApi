using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Tasks;

/// <summary>
/// 异步下发任务的后台执行器（定时轮询）。
///
/// 规则：
/// <list type="bullet">
/// <item>只处理 <see cref="AcsTaskStatus.Pending"/> 且到期的任务；</item>
/// <item><b>设备离线则保持待执行</b>，按 <c>OfflineRetrySeconds</c> 重新排期（不计失败）；</item>
/// <item>设备明确拒绝 → 直接记为失败（可用 <c>POST /api/tasks/{id}/retry</c> 重试）；</item>
/// <item>临时性错误（网络/SDK 超时）→ 自动重试至 <c>MaxAttempts</c> 次。</item>
/// </list>
/// </summary>
public sealed class AcsTaskWorker : BackgroundService
{
    private readonly AcsTaskService _service;
    private readonly AcsTaskExecutor _executor;
    private readonly AcsTaskQueueOptions _options;
    private readonly ILogger<AcsTaskWorker> _logger;

    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;

    public AcsTaskWorker(
        AcsTaskService service,
        AcsTaskExecutor executor,
        IOptions<AcsTaskQueueOptions> options,
        ILogger<AcsTaskWorker> logger)
    {
        _service = service;
        _executor = executor;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("下发任务队列已禁用（ISUP:TaskQueue:Enabled=false）。");
            return;
        }

        _logger.LogInformation(
            "下发任务队列已启动：库={Provider}，轮询={Poll}s，批量={Batch}，离线重试={Offline}s，临时错误重试上限={Max}，已结束任务保留={RetentionDays}天（每 {SweepHours} 小时清理）。",
            _options.Provider,
            _options.PollSeconds,
            _options.BatchSize,
            _options.OfflineRetrySeconds,
            _options.MaxAttempts,
            _options.CompletedRetentionDays,
            Math.Clamp(_options.RetentionSweepHours, 1, 720));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken).ConfigureAwait(false);
                await MaybeCleanupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "下发任务队列处理异常。");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(_options.PollSeconds, 1)), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var tasks = await _service.TakeDueAsync(_options.BatchSize, ct).ConfigureAwait(false);
        foreach (var task in tasks)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var result = await _executor.ExecuteAsync(task, ct).ConfigureAwait(false);
            await ApplyAsync(task, result, ct).ConfigureAwait(false);
        }
    }

    private async Task ApplyAsync(AcsTask task, AcsTaskExecutionResult result, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        if (result.Outcome != AcsTaskExecutionOutcome.DeviceUnavailable)
        {
            task.Attempts++;

            if (result.Result is not null)
            {
                task.DeviceSuccess = result.Result.DeviceSuccess;
                task.DeviceStatusCode = result.Result.Status?.StatusCode ?? 0;
                task.SubStatusCode = result.Result.Status?.SubStatusCode;
                task.ResultJson = SerializeResult(result.Result);
            }
        }

        switch (result.Outcome)
        {
            case AcsTaskExecutionOutcome.Success:
                task.Status = AcsTaskStatus.Succeeded;
                task.CompletedAt = now;
                task.Error = null;
                break;

            case AcsTaskExecutionOutcome.DeviceFailed:
                task.Status = AcsTaskStatus.Failed;
                task.CompletedAt = now;
                task.Error = result.Error;
                break;

            case AcsTaskExecutionOutcome.TemporaryError:
                if (task.Attempts >= Math.Max(task.MaxAttempts, 1))
                {
                    task.Status = AcsTaskStatus.Failed;
                    task.CompletedAt = now;
                    task.Error = $"重试 {task.Attempts} 次仍失败：{result.Error}";
                }
                else
                {
                    task.Status = AcsTaskStatus.Pending;
                    task.NextAttemptAt = now.AddSeconds(Math.Max(_options.OfflineRetrySeconds, 1));
                    task.Error = result.Error;
                }

                break;

            case AcsTaskExecutionOutcome.DeviceUnavailable:
            default:
                // 等待设备上线：保持待执行，不计失败。
                task.Status = AcsTaskStatus.Pending;
                task.NextAttemptAt = now.AddSeconds(Math.Max(_options.OfflineRetrySeconds, 1));
                task.Error = result.Error;
                break;
        }

        await _service.SaveAsync(task, ct).ConfigureAwait(false);

        if (task.Status is AcsTaskStatus.Succeeded or AcsTaskStatus.Failed)
        {
            _logger.LogInformation(
                "任务结束：Id={Id}，设备={DeviceId}，操作={Operation}，状态={Status}，原因={Error}",
                task.Id,
                task.DeviceId,
                task.Operation,
                task.Status,
                task.Error);
        }
    }

    private async Task MaybeCleanupAsync(CancellationToken ct)
    {
        if (_options.CompletedRetentionDays <= 0)
        {
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(_options.RetentionSweepHours, 1, 720));
        if (DateTimeOffset.UtcNow - _lastCleanup < interval)
        {
            return;
        }

        _lastCleanup = DateTimeOffset.UtcNow;
        var removed = await _service.CleanupAsync(_options.CompletedRetentionDays, ct).ConfigureAwait(false);
        if (removed > 0)
        {
            _logger.LogInformation("任务清理：已删除 {Count} 条超过 {Days} 天的已结束任务。", removed, _options.CompletedRetentionDays);
        }
    }

    private static string SerializeResult(Models.IsapiOperationResult result)
    {
        var json = JsonSerializer.Serialize(result, IsupJson.Options);
        return json.Length <= 8192 ? json : json[..8192] + "...(truncated)";
    }
}
