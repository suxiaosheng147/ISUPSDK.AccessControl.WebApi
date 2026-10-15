using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 异步下发任务查询控制器。
///
/// 人员/卡片/人脸的新增、修改、删除接口会立即返回 <c>202</c> 与任务 ID，
/// 用这里的接口查询任务执行结果。
/// </summary>
[ApiController]
[Route("api/tasks")]
[Produces("application/json")]
[Tags("10. 下发任务")]
public sealed class TasksController : ControllerBase
{
    private readonly AcsTaskService _tasks;
    private readonly ILogger<TasksController> _logger;

    public TasksController(AcsTaskService tasks, ILogger<TasksController> logger)
    {
        _tasks = tasks;
        _logger = logger;
    }

    /// <summary>查询单个任务的执行结果。</summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAsync(string id, CancellationToken ct = default)
    {
        var task = await _tasks.GetAsync(id, ct);
        if (task is null)
        {
            throw new IsupApiException($"未找到任务 {id}。", StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<AcsTaskDto>.Ok(AcsTaskDto.From(task)));
    }

    /// <summary>按条件列出任务（最新在前）。</summary>
    /// <param name="deviceId">按设备过滤（可选）。</param>
    /// <param name="entity">按对象类型过滤：Person / Card / Face / FaceLibrary（可选）。</param>
    /// <param name="status">按状态过滤：Pending / Running / Succeeded / Failed / Canceled（可选）。</param>
    /// <param name="limit">返回条数（1~500，默认 50）。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpGet]
    public async Task<IActionResult> ListAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] string? entity = null,
        [FromQuery] string? status = null,
        [FromQuery] int limit = 50,
        CancellationToken ct = default)
    {
        AcsTaskEntityType? entityFilter = null;
        if (!string.IsNullOrWhiteSpace(entity))
        {
            if (!Enum.TryParse<AcsTaskEntityType>(entity, ignoreCase: true, out var parsedEntity))
            {
                throw new IsupApiException($"无效的 entity：{entity}（应为 Person / Card / Face / FaceLibrary）。");
            }

            entityFilter = parsedEntity;
        }

        AcsTaskStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<AcsTaskStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                throw new IsupApiException($"无效的 status：{status}（应为 Pending / Running / Succeeded / Failed / Canceled）。");
            }

            statusFilter = parsedStatus;
        }

        var list = await _tasks.ListAsync(deviceId, entityFilter, statusFilter, limit, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            total = list.Count,
            tasks = list.Select(t => AcsTaskDto.From(t, includeResult: false)).ToList(),
        }));
    }

    /// <summary>重新排队一个失败/取消的任务。</summary>
    [HttpPost("{id}/retry")]
    public async Task<IActionResult> RetryAsync(string id, CancellationToken ct = default)
    {
        if (!await _tasks.RetryAsync(id, ct))
        {
            throw new IsupApiException($"任务 {id} 不存在。", StatusCodes.Status404NotFound);
        }

        var task = await _tasks.GetAsync(id, ct);
        _logger.LogInformation("任务 {Id} 已重新排队。", id);
        return Ok(ApiResponse<AcsTaskDto>.Ok(AcsTaskDto.From(task!), "任务已重新排队"));
    }

    /// <summary>取消一个尚未执行的（Pending）任务。</summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelAsync(string id, CancellationToken ct = default)
    {
        if (!await _tasks.CancelAsync(id, ct))
        {
            throw new IsupApiException(
                $"任务 {id} 不存在或已不在 Pending 状态（只能取消尚未执行的任务）。",
                StatusCodes.Status409Conflict);
        }

        var task = await _tasks.GetAsync(id, ct);
        return Ok(ApiResponse<AcsTaskDto>.Ok(AcsTaskDto.From(task!), "任务已取消"));
    }
}
