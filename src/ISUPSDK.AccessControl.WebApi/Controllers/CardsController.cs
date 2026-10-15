using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 卡片下发控制器。
/// 对应 ISAPI：<c>/ISAPI/AccessControl/CardInfo/*</c>。
///
/// <b>新增、修改、删除为异步任务</b>：返回 <c>202</c> 与任务 ID，用
/// <c>GET /api/tasks/{id}</c> 查询结果。查询接口仍为同步。
/// </summary>
[Route("api/access-control/cards")]
[Tags("2. 卡片下发")]
public sealed class CardsController : AccessControlControllerBase
{
    public CardsController(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        ILogger<CardsController> logger)
        : base(cms, accessControl, tasks, logger)
    {
    }

    /// <summary>新增单个卡片（异步任务）。</summary>
    /// <remarks>
    /// 示例请求体：
    /// <code>
    /// {
    ///   "cardNo": "1234567890",
    ///   "employeeNo": "1001",
    ///   "cardType": "normalCard",
    ///   "Valid": { "enable": true, "beginTime": "2024-01-01T00:00:00", "endTime": "2034-01-01T00:00:00" }
    /// }
    /// </code>
    /// 注意：海康门禁要求卡片关联的人员（employeeNo）已存在于设备中，否则返回
    /// subStatusCode=<c>employeeNoNotExist</c>。
    /// </remarks>
    [HttpPost]
    public async Task<IActionResult> AddAsync(
        [FromBody] IsapiCardInfo card,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardAdd,
            card,
            timeoutMs ?? 15000,
            description: $"新增卡片 {card.CardNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>修改卡片（按卡号，异步任务）。</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateAsync(
        [FromBody] IsapiCardInfo card,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardUpdate,
            card,
            timeoutMs ?? 15000,
            description: $"修改卡片 {card.CardNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>批量新增卡片（异步任务）。</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> AddBatchAsync(
        [FromBody] CardBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var operation = request.Update ? AcsTaskOperation.CardBatchUpdate : AcsTaskOperation.CardBatchAdd;
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            operation,
            request,
            request.TimeoutMs,
            description: $"批量{(request.Update ? "修改" : "新增")}卡片 {request.Cards.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>批量修改卡片（异步任务）。</summary>
    [HttpPut("batch")]
    public async Task<IActionResult> UpdateBatchAsync(
        [FromBody] CardBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardBatchUpdate,
            request,
            request.TimeoutMs,
            description: $"批量修改卡片 {request.Cards.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>按卡号批量删除卡片（异步任务）。</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAsync(
        [FromBody] CardNoBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardDelete,
            request.CardNos,
            request.TimeoutMs,
            description: $"删除卡片 {request.CardNos.Count} 条",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>删除单个卡片（异步任务）。</summary>
    [HttpDelete("{cardNo}")]
    public async Task<IActionResult> DeleteOneAsync(
        string cardNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardDelete,
            new[] { cardNo },
            timeoutMs ?? 15000,
            description: $"删除卡片 {cardNo}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>查询卡片（支持自动翻页）。</summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(
        [FromBody] CardSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new CardSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (!request.FetchAll)
        {
            var single = await AccessControl.SearchCardsAsync(device, request.ToCondition(), request.TimeoutMs, ct);
            return FromQuery(single);
        }

        var all = new List<IsapiCardInfo>();
        var condition = request.ToCondition();
        var total = 0;
        IsapiQueryResult<IsapiCardInfoSearchResponse>? last = null;

        for (var page = 0; page < 20; page++)
        {
            condition.SearchId = Guid.NewGuid().ToString("N");
            condition.SearchResultPosition = request.SearchResultPosition + all.Count;
            condition.MaxResults = request.MaxResults;

            last = await AccessControl.SearchCardsAsync(device, condition, request.TimeoutMs, ct);
            var cards = last.Data?.CardInfoSearch?.CardInfo;
            total = last.Data?.CardInfoSearch?.TotalMatches ?? total;

            if (cards is { Count: > 0 })
            {
                all.AddRange(cards);
            }

            if (!string.Equals(last.ResponseStatusString, "MORE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        var aggregated = new IsapiQueryResult<List<IsapiCardInfo>>
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            RequestUrl = "POST /ISAPI/AccessControl/CardInfo/Search?format=json (分页聚合)",
            Success = last?.Success ?? false,
            Status = last?.Status,
            Data = all,
            ElapsedMs = last?.ElapsedMs ?? 0,
            ResponseStatusString = last?.ResponseStatusString,
            NumOfMatches = all.Count,
            TotalMatches = total,
        };

        return Ok(ApiResponse<IsapiQueryResult<List<IsapiCardInfo>>>.Ok(aggregated));
    }

    /// <summary>查询单个卡片（按卡号）。</summary>
    [HttpGet("{cardNo}")]
    public async Task<IActionResult> GetOneAsync(
        string cardNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var condition = new IsapiCardInfoSearchCond
        {
            SearchId = Guid.NewGuid().ToString("N"),
            SearchResultPosition = 0,
            MaxResults = 1,
            CardNoList = new List<IsapiCardNo> { new() { CardNo = cardNo } },
        };

        var result = await AccessControl.SearchCardsAsync(device, condition, timeoutMs ?? 15000, ct);
        return FromQuery(result);
    }

    /// <summary>按工号删除该人员名下的全部卡片（异步任务）。</summary>
    [HttpDelete("by-employee")]
    public async Task<IActionResult> DeleteByEmployeeAsync(
        [FromBody] EmployeeNoBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardDeleteByEmployee,
            request.EmployeeNos,
            request.TimeoutMs,
            description: $"按工号删除卡片（{request.EmployeeNos.Count} 人）",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>清空设备上全部卡片（异步任务）。请谨慎调用。</summary>
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
            AcsTaskEntityType.Card,
            AcsTaskOperation.CardDeleteAll,
            payload: null,
            timeoutMs ?? 30000,
            description: "清空全部卡片",
            ct: ct);

        return AcceptedTask(task);
    }
}
