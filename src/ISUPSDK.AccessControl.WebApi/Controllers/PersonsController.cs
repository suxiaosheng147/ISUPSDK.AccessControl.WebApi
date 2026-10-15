using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 人员下发控制器。
/// 对应 ISAPI：<c>/ISAPI/AccessControl/UserInfo/*</c>。
///
/// <b>新增、修改、删除为异步任务</b>：请求先入库并立即返回 <c>202</c> 与任务 ID，
/// 由后台队列执行；用 <c>GET /api/tasks/{id}</c> 查询执行结果。查询接口仍为同步。
/// </summary>
[Route("api/access-control/persons")]
[Tags("1. 人员下发")]
public sealed class PersonsController : AccessControlControllerBase
{
    public PersonsController(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        ILogger<PersonsController> logger)
        : base(cms, accessControl, tasks, logger)
    {
    }

    /// <summary>新增单个人员（异步任务）。</summary>
    /// <remarks>
    /// 立即返回 <c>202</c> 与任务 ID；执行结果用 <c>GET /api/tasks/{taskId}</c> 查询。
    /// 示例请求体：
    /// <code>
    /// {
    ///   "employeeNo": "1001",
    ///   "name": "张三",
    ///   "userType": "normal",
    ///   "gender": "male",
    ///   "doorRight": "1",
    ///   "Valid": { "enable": true, "beginTime": "2024-01-01T00:00:00", "endTime": "2034-01-01T00:00:00" },
    ///   "RightPlan": [ { "planTemplateNo": "1" } ]
    /// }
    /// </code>
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> AddAsync(
        [FromBody] IsapiUserInfo user,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonAdd,
            user,
            timeoutMs ?? 15000,
            description: $"新增人员 {user.EmployeeNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>修改人员（按工号，异步任务）。</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] IsapiUserInfo user,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonUpdate,
            user,
            timeoutMs ?? 15000,
            description: $"修改人员 {user.EmployeeNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>按工号整体覆盖人员（异步任务，PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json）。</summary>
    [HttpPut("setup")]
    public async Task<IActionResult> SetUpAsync(
        [FromBody] IsapiUserInfo user,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonSetUp,
            user,
            timeoutMs ?? 15000,
            description: $"整体覆盖人员 {user.EmployeeNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>批量新增人员（异步任务）。</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> AddBatchAsync(
        [FromBody] UserBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var operation = request.Update ? AcsTaskOperation.PersonBatchUpdate : AcsTaskOperation.PersonBatchAdd;
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            operation,
            request,
            request.TimeoutMs,
            description: $"批量{(request.Update ? "修改" : "新增")}人员 {request.Users.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>批量修改人员（异步任务）。</summary>
    [HttpPut("batch")]
    public async Task<IActionResult> UpdateBatchAsync(
        [FromBody] UserBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        request.Update = true;
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonBatchUpdate,
            request,
            request.TimeoutMs,
            description: $"批量修改人员 {request.Users.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>按工号批量删除人员（异步任务）。</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(
        [FromBody] EmployeeNoBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonDelete,
            request.EmployeeNos,
            request.TimeoutMs,
            description: $"删除人员 {request.EmployeeNos.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>删除单个人员（异步任务）。</summary>
    [HttpDelete("{employeeNo}")]
    public async Task<IActionResult> DeleteOneAsync(
        string employeeNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonDelete,
            new[] { employeeNo },
            timeoutMs ?? 15000,
            description: $"删除人员 {employeeNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>查询人员（支持自动翻页）。</summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(
        [FromBody] UserSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new UserSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (!request.FetchAll)
        {
            var single = await AccessControl.SearchUsersAsync(device, request.ToCondition(), request.TimeoutMs, ct);
            return FromQuery(single);
        }

        var all = new List<IsapiUserInfo>();
        var condition = request.ToCondition();
        var total = 0;
        IsapiQueryResult<IsapiUserInfoSearchResponse>? last = null;

        for (var page = 0; page < 20; page++)
        {
            condition.SearchId = Guid.NewGuid().ToString("N");
            condition.SearchResultPosition = request.SearchResultPosition + all.Count;
            condition.MaxResults = request.MaxResults;

            last = await AccessControl.SearchUsersAsync(device, condition, request.TimeoutMs, ct);
            var users = last.Data?.UserInfoSearch?.UserInfo;
            total = last.Data?.UserInfoSearch?.TotalMatches ?? total;

            if (users is { Count: > 0 })
            {
                all.AddRange(users);
            }

            if (!string.Equals(last.ResponseStatusString, "MORE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        var aggregated = new IsapiQueryResult<List<IsapiUserInfo>>
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            RequestUrl = "POST /ISAPI/AccessControl/UserInfo/Search?format=json (分页聚合)",
            Success = last?.Success ?? false,
            Status = last?.Status,
            Data = all,
            ElapsedMs = last?.ElapsedMs ?? 0,
            ResponseStatusString = last?.ResponseStatusString,
            NumOfMatches = all.Count,
            TotalMatches = total,
        };

        return Ok(ApiResponse<IsapiQueryResult<List<IsapiUserInfo>>>.Ok(aggregated));
    }

    /// <summary>查询单个人员（按工号，最多返回 1 条）。</summary>
    [HttpGet("{employeeNo}")]
    public async Task<IActionResult> GetOneAsync(
        string employeeNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var condition = new IsapiUserInfoSearchCond
        {
            SearchId = Guid.NewGuid().ToString("N"),
            SearchResultPosition = 0,
            MaxResults = 1,
            EmployeeNoList = new List<IsapiEmployeeNo> { new() { EmployeeNo = employeeNo } },
        };

        var result = await AccessControl.SearchUsersAsync(device, condition, timeoutMs ?? 15000, ct);
        return FromQuery(result);
    }

    /// <summary>
    /// 统计人员数量。
    /// 底层为 <c>GET /ISAPI/AccessControl/UserInfo/Count?format=json</c>（注意是 GET），
    /// 返回体形如 <c>{ "UserInfoCount": { "userNumber": 100 } }</c>。
    /// </summary>
    [HttpGet("count")]
    public async Task<IActionResult> CountAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.CountUsersAsync(device, timeoutMs ?? 15000, ct);
        return FromQuery(result);
    }

    /// <summary>查询人员能力集（GET /ISAPI/AccessControl/UserInfo/capabilities，返回 XML，用于确认固件支持的字段）。</summary>
    [HttpGet("capabilities")]
    public async Task<IActionResult> CapabilitiesAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetUserCapabilitiesAsync(device, timeoutMs ?? 10000, ct);
        return Ok(ApiResponse<IsapiOperationResult>.Ok(result));
    }

    /// <summary>清空设备上全部人员（异步任务）。请谨慎调用。</summary>
    [HttpDelete("all")]
    public async Task<IActionResult> DeleteAllAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Person,
            AcsTaskOperation.PersonDeleteAll,
            payload: null,
            timeoutMs ?? 30000,
            description: "清空全部人员",
            ct: ct);

        return AcceptedTask(task);
    }
}
