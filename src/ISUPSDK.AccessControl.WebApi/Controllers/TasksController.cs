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
        var entityFilter = ParseEntityFilter(entity);
        var statusFilter = ParseStatusFilter(status);

        var list = await _tasks.ListAsync(deviceId, entityFilter, statusFilter, limit, ct);
        return Ok(ApiResponse<object>.Ok(new
        {
            total = list.Count,
            tasks = list.Select(t => AcsTaskDto.From(t, includeResult: false)).ToList(),
        }));
    }

    /// <summary>
    /// 分页查询任务（按创建时间倒序，最新在前）。
    /// <para>
    /// 与 <c>GET /api/tasks</c> 的区别：返回总条数与页码信息，适合在页面上翻页；
    /// 过滤条件在数据库侧执行，不会把整表读进内存。
    /// </para>
    /// </summary>
    /// <param name="pageIndex">页码（从 1 开始，默认 1）。</param>
    /// <param name="pageSize">每页条数（1~200，默认 20）。</param>
    /// <param name="deviceId">按设备过滤（可选）。</param>
    /// <param name="entity">按对象类型过滤：Person / Card / Face / FaceLibrary（可选）。</param>
    /// <param name="status">按状态过滤：Pending / Running / Succeeded / Failed / Canceled（可选）。</param>
    /// <param name="source">按来源过滤，例如 <c>person-operation</c>（人员下发队列创建的任务）；传 <c>direct</c> 表示只看直接调用创建的任务（可选）。</param>
    /// <param name="createdFrom">创建时间下限（含，可选）。</param>
    /// <param name="createdTo">创建时间上限（含，可选）。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpGet("page")]
    public async Task<IActionResult> PageAsync(
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? deviceId = null,
        [FromQuery] string? entity = null,
        [FromQuery] string? status = null,
        [FromQuery] string? source = null,
        [FromQuery] DateTimeOffset? createdFrom = null,
        [FromQuery] DateTimeOffset? createdTo = null,
        CancellationToken ct = default)
    {
        var entityFilter = ParseEntityFilter(entity);
        var statusFilter = ParseStatusFilter(status);

        pageIndex = Math.Max(pageIndex, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // "direct" 是约定的"来源为空"写法，便于筛选调用方直接调控制器创建的任务
        var sourceFilter = string.IsNullOrWhiteSpace(source)
            ? null
            : string.Equals(source, DirectSourceFilter, StringComparison.OrdinalIgnoreCase) ? string.Empty : source.Trim();

        if (createdFrom is { } from && createdTo is { } to && from > to)
        {
            throw new IsupApiException("createdFrom 不能晚于 createdTo。");
        }

        var (items, total) = await _tasks
            .ListPagedAsync(pageIndex, pageSize, deviceId, entityFilter, statusFilter, sourceFilter, createdFrom, createdTo, ct);

        return Ok(ApiResponse<object>.Ok(new
        {
            total,
            pageIndex,
            pageSize,
            totalPages = (int)Math.Ceiling(total / (double)pageSize),
            tasks = items.Select(t => AcsTaskDto.From(t, includeResult: false)).ToList(),
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

    /// <summary>分页接口里"只看来源为空（调用方直接调控制器）"的过滤写法。</summary>
    private const string DirectSourceFilter = "direct";

    /// <summary>解析 entity 过滤参数（无效值直接报 400）。</summary>
    private static AcsTaskEntityType? ParseEntityFilter(string? entity)
    {
        if (string.IsNullOrWhiteSpace(entity))
        {
            return null;
        }

        if (!Enum.TryParse<AcsTaskEntityType>(entity, ignoreCase: true, out var parsedEntity))
        {
            throw new IsupApiException($"无效的 entity：{entity}（应为 Person / Card / Face / FaceLibrary）。");
        }

        return parsedEntity;
    }

    /// <summary>解析 status 过滤参数（无效值直接报 400）。</summary>
    private static AcsTaskStatus? ParseStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (!Enum.TryParse<AcsTaskStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            throw new IsupApiException($"无效的 status：{status}（应为 Pending / Running / Succeeded / Failed / Canceled）。");
        }

        return parsedStatus;
    }
}
