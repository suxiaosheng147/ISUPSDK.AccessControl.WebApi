using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Builders;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 人员权限计划控制器（计划模板 / 周计划 / 假日组 / 假日计划）。
///
/// 依据《人员权限计划模板配置》文档实现，四类资源都是"按编号寻址"的配置资源：
/// <list type="bullet">
/// <item><c>PUT/GET /ISAPI/AccessControl/UserRightPlanTemplate/&lt;planTemplateID&gt;</c></item>
/// <item><c>PUT/GET /ISAPI/AccessControl/UserRightWeekPlanCfg/&lt;weekPlanID&gt;</c></item>
/// <item><c>PUT/GET /ISAPI/AccessControl/UserRightHolidayGroupCfg/&lt;holidayGroupID&gt;</c></item>
/// <item><c>PUT/GET /ISAPI/AccessControl/UserRightHolidayPlanCfg/&lt;holidayPlanID&gt;</c></item>
/// </list>
/// 各自另有 <c>POST .../Search</c> 查询接口。
///
/// 结构与优先级：计划模板 = 1 个周计划 + 若干假日组；假日计划优先级高于周计划。
/// 人员的 <c>UserInfo.RightPlan</c> 引用计划模板编号。
/// <b>人员权限计划必须配置，否则下发的人员将没有任何通行权限。</b>
/// </summary>
[Route("api/access-control/plan-templates")]
[Tags("4. 人员权限计划（模板/周计划/假日组/假日计划）")]
public sealed class PlanTemplatesController : AccessControlControllerBase
{
    public PlanTemplatesController(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        ILogger<PlanTemplatesController> logger)
        : base(cms, accessControl, tasks, logger)
    {
    }

    // ==========================================================================================
    // 能力探测
    // ==========================================================================================

    /// <summary>
    /// 探测设备是否支持人员权限计划相关配置。
    /// 下发前应先调用本接口：四个开关为 false / 缺失时，对应资源不可配置。
    /// </summary>
    [HttpGet("capabilities")]
    public async Task<IActionResult> CapabilitiesAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var capabilities = await AccessControl.GetUserRightPlanCapabilitiesAsync(device, timeoutMs ?? 10000, ct);
        return Ok(ApiResponse<UserRightPlanCapabilities>.Ok(capabilities));
    }

    // ==========================================================================================
    // 计划模板
    // ==========================================================================================

    /// <summary>下发/修改单个人员权限计划模板。</summary>
    /// <remarks>
    /// <code>
    /// PUT /api/access-control/plan-templates?planTemplateId=1
    /// {
    ///   "enable": true,
    ///   "templateName": "白班",
    ///   "weekPlanNo": 1,
    ///   "holidayGroupNo": "1,3"
    /// }
    /// </code>
    /// </remarks>
    [HttpPut]
    public async Task<IActionResult> WriteAsync(
        [FromBody] IsapiPlanTemplate template,
        [FromQuery] int planTemplateId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(planTemplateId, "planTemplateId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.WritePlanTemplateAsync(device, planTemplateId, template, timeoutMs ?? 15000, ct);
        return FromResult(result);
    }

    /// <summary>批量下发计划模板。</summary>
    [HttpPut("batch")]
    public async Task<IActionResult> WriteBatchAsync(
        [FromBody] PlanTemplateBatchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        if (request.Templates.Count == 0)
        {
            throw new IsupApiException("templates 不能为空。");
        }

        foreach (var item in request.Templates)
        {
            ValidateId(item.PlanTemplateId, "planTemplateId");
        }

        var device = ResolveDevice(deviceId, userId);
        var results = await AccessControl.WritePlanTemplatesAsync(
            device,
            request.Templates.Select(t => (t.PlanTemplateId, t.Template)).ToList(),
            request.TimeoutMs,
            ct);

        return FromBatchResults(results);
    }

    /// <summary>读取单个计划模板（GET UserRightPlanTemplate/&lt;id&gt;，返回设备原始报文）。</summary>
    [HttpGet("{planTemplateId:int}")]
    public async Task<IActionResult> GetAsync(
        int planTemplateId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(planTemplateId, "planTemplateId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetPlanTemplateRawAsync(device, planTemplateId, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询人员权限计划模板（POST UserRightPlanTemplate/Search）。</summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(
        [FromBody] UserRightSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new UserRightSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (request.FetchAll)
        {
            var all = await AccessControl.ReadAllUserRightAsync<IsapiPlanTemplateMatch>(
                device, IsapiEndpoints.PlanTemplateSearch, request.ToCondition(), request.TimeoutMs, 20, ct);
            return Ok(ApiResponse<List<IsapiPlanTemplateMatch>>.Ok(all, $"共 {all.Count} 条"));
        }

        var result = await AccessControl.SearchPlanTemplatesAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    // ==========================================================================================
    // 周计划
    // ==========================================================================================

    /// <summary>下发/修改人员权限周计划（PUT UserRightWeekPlanCfg/&lt;weekPlanId&gt;）。</summary>
    /// <remarks>
    /// 周一至周日 × 每天最多 8 个时间段，全部平铺在 <c>WeekPlanCfg</c> 数组中，
    /// 用 <c>week</c> 指定星期、<c>id</c>（1~8）指定段号。
    /// <code>
    /// PUT /api/access-control/plan-templates/week-plans?weekPlanId=1
    /// {
    ///   "enable": true,
    ///   "WeekPlanCfg": [
    ///     { "week": "Monday", "id": 1, "enable": true,
    ///       "TimeSegment": { "beginTime": "08:00:00", "endTime": "18:00:00" } },
    ///     { "week": "Saturday", "id": 1, "enable": false,
    ///       "TimeSegment": { "beginTime": "", "endTime": "" } }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    [HttpPut("week-plans")]
    public async Task<IActionResult> WriteWeekPlanAsync(
        [FromBody] IsapiWeekPlanCfg weekPlan,
        [FromQuery] int weekPlanId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(weekPlanId, "weekPlanId");
        ValidateWeekPlan(weekPlan);

        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.WriteWeekPlanAsync(device, weekPlanId, weekPlan, timeoutMs ?? 15000, ct);
        return FromResult(result);
    }

    /// <summary>读取单个周计划（GET UserRightWeekPlanCfg/&lt;id&gt;）。</summary>
    [HttpGet("week-plans/{weekPlanId:int}")]
    public async Task<IActionResult> GetWeekPlanAsync(
        int weekPlanId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(weekPlanId, "weekPlanId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetWeekPlanRawAsync(device, weekPlanId, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询人员权限周计划（POST UserRightWeekPlanCfg/Search）。</summary>
    [HttpPost("week-plans/search")]
    public async Task<IActionResult> SearchWeekPlansAsync(
        [FromBody] UserRightSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new UserRightSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (request.FetchAll)
        {
            var all = await AccessControl.ReadAllUserRightAsync<IsapiWeekPlanMatch>(
                device, IsapiEndpoints.WeekPlanCfgSearch, request.ToCondition(), request.TimeoutMs, 20, ct);
            return Ok(ApiResponse<List<IsapiWeekPlanMatch>>.Ok(all, $"共 {all.Count} 条"));
        }

        var result = await AccessControl.SearchWeekPlansAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    // ==========================================================================================
    // 假日组
    // ==========================================================================================

    /// <summary>下发/修改人员权限假日组（PUT UserRightHolidayGroupCfg/&lt;holidayGroupId&gt;）。</summary>
    /// <remarks>
    /// <c>holidayPlanNo</c> 用逗号分隔引用若干假日计划编号，可为空字符串表示不关联。
    /// <code>
    /// PUT /api/access-control/plan-templates/holiday-groups?holidayGroupId=1
    /// { "enable": true, "groupName": "法定节假日", "holidayPlanNo": "1,3,5" }
    /// </code>
    /// </remarks>
    [HttpPut("holiday-groups")]
    public async Task<IActionResult> WriteHolidayGroupAsync(
        [FromBody] IsapiHolidayGroupCfg holidayGroup,
        [FromQuery] int holidayGroupId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(holidayGroupId, "holidayGroupId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.WriteHolidayGroupAsync(device, holidayGroupId, holidayGroup, timeoutMs ?? 15000, ct);
        return FromResult(result);
    }

    /// <summary>读取单个假日组（GET UserRightHolidayGroupCfg/&lt;id&gt;）。</summary>
    [HttpGet("holiday-groups/{holidayGroupId:int}")]
    public async Task<IActionResult> GetHolidayGroupAsync(
        int holidayGroupId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(holidayGroupId, "holidayGroupId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetHolidayGroupRawAsync(device, holidayGroupId, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询人员权限假日组（POST UserRightHolidayGroupCfg/Search）。</summary>
    [HttpPost("holiday-groups/search")]
    public async Task<IActionResult> SearchHolidayGroupsAsync(
        [FromBody] UserRightSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new UserRightSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (request.FetchAll)
        {
            var all = await AccessControl.ReadAllUserRightAsync<IsapiHolidayGroupMatch>(
                device, IsapiEndpoints.HolidayGroupCfgSearch, request.ToCondition(), request.TimeoutMs, 20, ct);
            return Ok(ApiResponse<List<IsapiHolidayGroupMatch>>.Ok(all, $"共 {all.Count} 条"));
        }

        var result = await AccessControl.SearchHolidayGroupsAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    // ==========================================================================================
    // 假日计划
    // ==========================================================================================

    /// <summary>下发/修改人员权限假日计划（PUT UserRightHolidayPlanCfg/&lt;holidayPlanId&gt;）。</summary>
    /// <remarks>
    /// 一个假日计划描述一个日期区间（起止日期），区间内每天的时间段相同（最多 8 段）。
    /// 删除假日计划时下发 <c>"enable": false, "deleteHolidayPlan": true</c>，
    /// 设备会同时把它从假日组中移除。
    /// <code>
    /// PUT /api/access-control/plan-templates/holiday-plans?holidayPlanId=1
    /// {
    ///   "enable": true,
    ///   "holidayPlanName": "国庆节",
    ///   "beginDate": "2025-10-01",
    ///   "endDate": "2025-10-07",
    ///   "cycleByYear": false,
    ///   "HolidayPlanCfg": [
    ///     { "id": 1, "enable": true,
    ///       "TimeSegment": { "beginTime": "00:00:00", "endTime": "23:59:59" } }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    [HttpPut("holiday-plans")]
    public async Task<IActionResult> WriteHolidayPlanAsync(
        [FromBody] IsapiHolidayPlanCfg holidayPlan,
        [FromQuery] int holidayPlanId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(holidayPlanId, "holidayPlanId");
        ValidateHolidayPlan(holidayPlan);

        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.WriteHolidayPlanAsync(device, holidayPlanId, holidayPlan, timeoutMs ?? 15000, ct);
        return FromResult(result);
    }

    /// <summary>读取单个假日计划（GET UserRightHolidayPlanCfg/&lt;id&gt;）。</summary>
    [HttpGet("holiday-plans/{holidayPlanId:int}")]
    public async Task<IActionResult> GetHolidayPlanAsync(
        int holidayPlanId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        ValidateId(holidayPlanId, "holidayPlanId");
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetHolidayPlanRawAsync(device, holidayPlanId, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询人员权限假日计划（POST UserRightHolidayPlanCfg/Search，支持 holidayPlanIDList 过滤）。</summary>
    [HttpPost("holiday-plans/search")]
    public async Task<IActionResult> SearchHolidayPlansAsync(
        [FromBody] UserRightSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new UserRightSearchRequest();
        var device = ResolveDevice(deviceId, userId);

        if (request.FetchAll)
        {
            var all = await AccessControl.ReadAllUserRightAsync<IsapiHolidayPlanMatch>(
                device, IsapiEndpoints.HolidayPlanCfgSearch, request.ToCondition(), request.TimeoutMs, 20, ct);
            return Ok(ApiResponse<List<IsapiHolidayPlanMatch>>.Ok(all, $"共 {all.Count} 条"));
        }

        var result = await AccessControl.SearchHolidayPlansAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    // ==========================================================================================
    // 便捷预设
    // ==========================================================================================

    /// <summary>
    /// 一键建立"工作日全天"权限计划：先下发周计划，再下发引用它的计划模板。
    /// 人员的 <c>RightPlan[].planTemplateNo</c> 填 <c>planTemplateId</c> 即可获得周一至周五全天通行权限。
    /// </summary>
    [HttpPost("presets/workday")]
    public async Task<IActionResult> ApplyWorkdayPresetAsync(
        [FromBody] WorkdayPresetRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new WorkdayPresetRequest();
        ValidateId(request.WeekPlanId, "weekPlanId");
        ValidateId(request.PlanTemplateId, "planTemplateId");

        var device = ResolveDevice(deviceId, userId);
        var (weekPlan, template) = PlanTemplatePresets.BuildWorkdayPreset(
            request.WeekPlanId,
            request.PlanTemplateId,
            request.TemplateName,
            request.HolidayGroupNo);

        // 覆盖自定义时间段
        foreach (var item in weekPlan.WeekPlanCfg.Where(i => i.Enable))
        {
            item.TimeSegment.BeginTime = request.BeginTime;
            item.TimeSegment.EndTime = request.EndTime;
        }

        var results = new List<IsapiOperationResult>
        {
            await AccessControl.WriteWeekPlanAsync(device, request.WeekPlanId, weekPlan, request.TimeoutMs, ct),
        };

        if (results[^1].DeviceSuccess)
        {
            results.Add(await AccessControl.WritePlanTemplateAsync(
                device, request.PlanTemplateId, template, request.TimeoutMs, ct));
        }

        return FromBatchResults(results);
    }

    // ==========================================================================================
    // 校验
    // ==========================================================================================

    private static void ValidateId(int id, string name)
    {
        if (id < 1)
        {
            throw new IsupApiException($"{name} 必须从 1 开始，当前值 {id}。");
        }
    }

    private static void ValidateWeekPlan(IsapiWeekPlanCfg weekPlan)
    {
        if (weekPlan.WeekPlanCfg.Count == 0)
        {
            throw new IsupApiException("WeekPlanCfg 不能为空，至少需要一个时间段。");
        }

        foreach (var item in weekPlan.WeekPlanCfg)
        {
            if (!PlanTemplatePresets.WeekDays.Contains(item.Week, StringComparer.OrdinalIgnoreCase))
            {
                throw new IsupApiException(
                    $"week '{item.Week}' 无效，取值范围：{string.Join('/', PlanTemplatePresets.WeekDays)}。");
            }

            if (item.Id is < 1 or > 8)
            {
                throw new IsupApiException($"id（时间段编号）取值范围为 1~8，当前值 {item.Id}。");
            }

            if (item.Enable && (string.IsNullOrWhiteSpace(item.TimeSegment.BeginTime) ||
                                string.IsNullOrWhiteSpace(item.TimeSegment.EndTime)))
            {
                throw new IsupApiException(
                    $"week={item.Week}, id={item.Id} 已使能，必须同时提供 TimeSegment.beginTime 与 endTime。");
            }
        }
    }

    private static void ValidateHolidayPlan(IsapiHolidayPlanCfg holidayPlan)
    {
        // 删除场景：enable=false 且 deleteHolidayPlan=true，允许不带日期与时间段。
        if (holidayPlan.DeleteHolidayPlan == true || !holidayPlan.Enable)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(holidayPlan.BeginDate) || string.IsNullOrWhiteSpace(holidayPlan.EndDate))
        {
            throw new IsupApiException("beginDate 与 endDate 为必填（格式 yyyy-MM-dd）。");
        }

        if (!DateTime.TryParse(holidayPlan.BeginDate, out _) || !DateTime.TryParse(holidayPlan.EndDate, out _))
        {
            throw new IsupApiException("beginDate / endDate 格式必须为 yyyy-MM-dd。");
        }

        if (holidayPlan.HolidayPlanCfg.Count == 0)
        {
            throw new IsupApiException("HolidayPlanCfg 不能为空，至少需要一个时间段。");
        }

        foreach (var item in holidayPlan.HolidayPlanCfg)
        {
            if (item.Id is < 1 or > 8)
            {
                throw new IsupApiException($"HolidayPlanCfg.id（时间段编号）取值范围为 1~8，当前值 {item.Id}。");
            }

            if (item.Enable && (string.IsNullOrWhiteSpace(item.TimeSegment.BeginTime) ||
                                string.IsNullOrWhiteSpace(item.TimeSegment.EndTime)))
            {
                throw new IsupApiException(
                    $"HolidayPlanCfg(id={item.Id}) 已使能，必须同时提供 TimeSegment.beginTime 与 endTime。");
            }
        }
    }

    private IActionResult FromBatchResults(IReadOnlyList<IsapiOperationResult> results)
    {
        var failed = results.FirstOrDefault(r => !r.DeviceSuccess || !r.SdkSuccess);
        if (failed is null)
        {
            return Ok(ApiResponse<IReadOnlyList<IsapiOperationResult>>.Ok(results, $"全部 {results.Count} 步成功"));
        }

        // 复用统一的失败判定（SDK 失败 → 502；设备拒绝 → 400）
        return FromResult(failed);
    }
}
