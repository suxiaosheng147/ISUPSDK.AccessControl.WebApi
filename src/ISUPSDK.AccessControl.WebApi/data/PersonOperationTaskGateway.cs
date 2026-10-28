using ISUPSDK.AccessControl.WebApi.Services.Tasks;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>一次"入队人员下发任务"的结果。</summary>
public sealed record PersonTaskEnqueueResult(bool Success, string? TaskId, string? Error);

/// <summary>AcsTask 的当前状态（从任务库直接读取，不经控制器）。</summary>
public sealed record PersonTaskStatus(string Id, string Status, string? Error, string? Result, string? Source = null)
{
    /// <summary>
    /// 任务是否已给出最终结果 —— <b>只有 <c>Succeeded</c> / <c>Failed</c> 两个状态才会回传平台</b>。
    /// <para>
    /// <c>Pending</c> / <c>Running</c> 表示设备还没执行完，继续等；
    /// <c>Canceled</c> 表示任务被人为取消、不会再产生结果，同样不回传，
    /// 由 <c>TaskTimeoutMinutes</c> 超时后按失败回传（避免平台永远收不到结果）。
    /// </para>
    /// </summary>
    public bool IsCompleted => Status is "Succeeded" or "Failed";

    /// <summary>任务是否成功。</summary>
    public bool IsSucceeded => Status == "Succeeded";

    /// <summary>
    /// 该任务是否由人员下发队列创建：<c>Source</c> 为空（历史任务 / 直接调用）时视为通过，
    /// 仅当来源明确是其它值时才判为"不是本队列的任务"。
    /// </summary>
    public bool BelongsToPersonOperation =>
        string.IsNullOrWhiteSpace(Source) ||
        string.Equals(Source, AcsTaskSources.PersonOperation, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// 人员下发队列与任务库之间的桥（<b>进程内直接调用</b>，不经过控制器 / HTTP）。
/// <para>
/// 队列只负责"入队 + 查结果"：真正的下发由 <c>AcsTaskWorker</c> → <c>AcsTaskExecutor</c> 完成，
/// 一条人员操作就是一条 <see cref="AcsTaskOperation.PersonIssue"/>（或
/// <see cref="AcsTaskOperation.PersonIssueDelete"/>）任务，人员 → 卡 → 人脸 在同一任务里顺序执行。
/// </para>
/// </summary>
public sealed class PersonOperationTaskGateway
{
    private readonly AcsTaskService _tasks;
    private readonly ILogger<PersonOperationTaskGateway> _logger;

    public PersonOperationTaskGateway(AcsTaskService tasks, ILogger<PersonOperationTaskGateway> logger)
    {
        _tasks = tasks;
        _logger = logger;
    }

    /// <summary>入队一条人员下发任务，返回任务 ID（可用 <c>/api/tasks/{id}</c> 查询）。</summary>
    /// <param name="deviceId">目标设备 ID（序列号）。</param>
    /// <param name="operation">操作类型（PersonIssue / PersonIssueDelete）。</param>
    /// <param name="payload">下发载荷（每一步的 ISAPI 报文）。</param>
    /// <param name="timeoutMs">单步透传超时（毫秒）。</param>
    /// <param name="description">可读描述。</param>
    /// <param name="ct">取消令牌。</param>
    public async Task<PersonTaskEnqueueResult> EnqueueIssueAsync(
        string deviceId,
        AcsTaskOperation operation,
        PersonIssuePayload payload,
        uint timeoutMs,
        string description,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return new PersonTaskEnqueueResult(false, null, "入队失败：设备编码为空");
        }

        try
        {
            var task = await _tasks
                .EnqueueAsync(
                    deviceId,
                    AcsTaskEntityType.Person,
                    operation,
                    payload,
                    timeoutMs,
                    description: description,
                    source: AcsTaskSources.PersonOperation,
                    ct: ct)
                .ConfigureAwait(false);

            return new PersonTaskEnqueueResult(true, task.Id, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "人员下发任务入队失败：设备={DeviceId}，操作={Operation}", deviceId, operation);
            return new PersonTaskEnqueueResult(false, null, $"入队失败：{ex.Message}");
        }
    }

    /// <summary>读取任务状态；返回 null 表示查不到（任务被清理 / 不存在）。</summary>
    public async Task<PersonTaskStatus?> GetTaskAsync(string taskId, CancellationToken ct = default)
    {
        var task = await _tasks.GetAsync(taskId, ct).ConfigureAwait(false);
        return task is null
            ? null
            : new PersonTaskStatus(task.Id, task.Status.ToString(), task.Error, task.ResultJson, task.Source);
    }
}
