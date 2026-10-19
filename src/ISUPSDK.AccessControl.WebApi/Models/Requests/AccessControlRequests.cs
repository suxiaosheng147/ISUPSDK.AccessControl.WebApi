using ISUPSDK.AccessControl.WebApi.Models.Isapi;

namespace ISUPSDK.AccessControl.WebApi.Models.Requests;

/// <summary>人员查询请求（POST /api/access-control/persons/search）。</summary>
public sealed class UserSearchRequest
{
    /// <summary>按工号精确筛选（可多个）。</summary>
    public List<string>? EmployeeNos { get; set; }

    /// <summary>按姓名前缀筛选。</summary>
    public string? Name { get; set; }

    /// <summary>按人员类型筛选：normal / visitor / blackList。</summary>
    public string? UserType { get; set; }

    /// <summary>起始位置（分页）。翻页时填上一次响应里的 <c>nextPosition</c>。</summary>
    public int SearchResultPosition { get; set; }

    /// <summary>单次返回条数（建议 ≤30）。</summary>
    public int MaxResults { get; set; } = 30;

    /// <summary>
    /// 查询会话 ID（<c>searchID</c>）。翻页时必须复用上一次响应里的 <c>searchId</c>，
    /// 否则设备会当成新查询、从头发结果；留空则自动生成。
    /// </summary>
    public string? SearchId { get; set; }

    /// <summary>透传接收超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 15000;

    /// <summary>是否自动翻页拉取全部结果（每页 <see cref="MaxResults"/> 条，最多 20 页）。</summary>
    public bool FetchAll { get; set; }

    public IsapiUserInfoSearchCond ToCondition() => new()
    {
        SearchId = string.IsNullOrWhiteSpace(SearchId) ? Guid.NewGuid().ToString("N") : SearchId!,
        SearchResultPosition = SearchResultPosition,
        MaxResults = MaxResults,
        Name = Name,
        UserType = UserType,
        EmployeeNoList = EmployeeNos is { Count: > 0 }
            ? EmployeeNos.Select(no => new IsapiEmployeeNo { EmployeeNo = no }).ToList()
            : null,
    };
}

/// <summary>卡片查询请求（POST /api/access-control/cards/search）。</summary>
public sealed class CardSearchRequest
{
    public List<string>? CardNos { get; set; }

    public List<string>? EmployeeNos { get; set; }

    /// <summary>起始位置（分页）。翻页时填上一次响应里的 <c>nextPosition</c>。</summary>
    public int SearchResultPosition { get; set; }

    public int MaxResults { get; set; } = 30;

    /// <summary>
    /// 查询会话 ID（<c>searchID</c>）。翻页时必须复用上一次响应里的 <c>searchId</c>，
    /// 否则设备会当成新查询、从头发结果；留空则自动生成。
    /// </summary>
    public string? SearchId { get; set; }

    public uint TimeoutMs { get; set; } = 15000;

    public bool FetchAll { get; set; }

    public IsapiCardInfoSearchCond ToCondition() => new()
    {
        SearchId = string.IsNullOrWhiteSpace(SearchId) ? Guid.NewGuid().ToString("N") : SearchId!,
        SearchResultPosition = SearchResultPosition,
        MaxResults = MaxResults,
        CardNoList = CardNos is { Count: > 0 }
            ? CardNos.Select(no => new IsapiCardNo { CardNo = no }).ToList()
            : null,
        EmployeeNoList = EmployeeNos is { Count: > 0 }
            ? EmployeeNos.Select(no => new IsapiEmployeeNo { EmployeeNo = no }).ToList()
            : null,
    };
}

/// <summary>人脸查询请求（POST /api/access-control/faces/search）。</summary>
public sealed class FaceSearchRequest
{
    public string FaceLibType { get; set; } = "blackFD";

    public string FdId { get; set; } = "1";

    /// <summary>人脸图片 ID（可选）。</summary>
    public string? FpId { get; set; }

    /// <summary>姓名（可选）。</summary>
    public string? Name { get; set; }

    /// <summary>
    /// 查询起始位置（<c>searchResultPosition</c>，设备侧从 0 开始）。
    /// 翻页时填上一次响应里的 <c>nextPosition</c>。
    /// </summary>
    public int SearchResultPosition { get; set; }

    /// <summary>本次最多返回多少条（<c>maxResults</c>）。</summary>
    public int MaxResults { get; set; } = 30;

    /// <summary>
    /// 查询会话 ID（<c>searchID</c>，1~32 位）。
    /// 翻页时必须复用上一次响应里的 <c>searchId</c>，否则设备会当成新查询、从头发结果；留空则自动生成。
    /// </summary>
    public string? SearchId { get; set; }

    public uint TimeoutMs { get; set; } = 15000;

    public IsapiFaceSearchCond ToCondition() => new()
    {
        SearchId = string.IsNullOrWhiteSpace(SearchId) ? Guid.NewGuid().ToString("N") : SearchId!,
        SearchResultPosition = SearchResultPosition,
        MaxResults = MaxResults,
        FaceLibType = FaceLibType,
        FdId = FdId,
        FpId = FpId,
        Name = Name,
    };
}

/// <summary>人员权限配置查询请求（计划模板 / 周计划 / 假日组 / 假日计划的 Search 接口通用）。</summary>
public sealed class UserRightSearchRequest
{
    /// <summary>查询标识（1~32 位字母数字，查询条件不变时必须复用同值）。留空则自动生成。</summary>
    public string? SearchId { get; set; }

    /// <summary>查询起始位置，<b>从 1 开始</b>（文档规定）。</summary>
    public int SearchResultPosition { get; set; } = 1;

    /// <summary>本次最大结果数，范围 [1,100]，建议不超过 32。</summary>
    public int MaxResults { get; set; } = 32;

    /// <summary>是否只查询使能的对象。</summary>
    public bool? Enable { get; set; }

    /// <summary>按指定假日计划 ID 列表查询（仅假日计划查询有效）。</summary>
    public List<int>? HolidayPlanIdList { get; set; }

    /// <summary>透传接收超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 15000;

    /// <summary>是否自动翻页拉取全部结果（最多 20 页）。</summary>
    public bool FetchAll { get; set; }

    public IsapiUserRightSearchCond ToCondition() => new()
    {
        SearchId = string.IsNullOrWhiteSpace(SearchId) ? Guid.NewGuid().ToString("N")[..16] : SearchId!,
        SearchResultPosition = SearchResultPosition < 1 ? 1 : SearchResultPosition,
        MaxResults = MaxResults is < 1 or > 100 ? 32 : MaxResults,
        Enable = Enable,
        HolidayPlanIdList = HolidayPlanIdList,
    };
}

/// <summary>单个计划模板下发请求。</summary>
public sealed class PlanTemplateWriteRequest
{
    /// <summary>计划模板编号，从 1 开始，上限由能力集给出。</summary>
    public int PlanTemplateId { get; set; }

    public IsapiPlanTemplate Template { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>计划模板批量下发请求。</summary>
public sealed class PlanTemplateBatchRequest
{
    public List<PlanTemplateWriteRequest> Templates { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>周计划下发请求。</summary>
public sealed class WeekPlanWriteRequest
{
    /// <summary>周计划编号，从 1 开始。</summary>
    public int WeekPlanId { get; set; }

    public IsapiWeekPlanCfg WeekPlan { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>假日组下发请求。</summary>
public sealed class HolidayGroupWriteRequest
{
    /// <summary>假日组编号，从 1 开始。</summary>
    public int HolidayGroupId { get; set; }

    public IsapiHolidayGroupCfg HolidayGroup { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>假日计划下发请求。</summary>
public sealed class HolidayPlanWriteRequest
{
    /// <summary>假日计划编号，从 1 开始。</summary>
    public int HolidayPlanId { get; set; }

    public IsapiHolidayPlanCfg HolidayPlan { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>
/// 一键建立"工作日全天"权限计划：同时下发周计划 + 计划模板。
/// 适合不需要假日例外的常见场景。人员的 RightPlan 引用其中的 planTemplateId。
/// </summary>
public sealed class WorkdayPresetRequest
{
    /// <summary>周计划编号（从 1 开始）。</summary>
    public int WeekPlanId { get; set; } = 1;

    /// <summary>计划模板编号（从 1 开始）。</summary>
    public int PlanTemplateId { get; set; } = 1;

    /// <summary>模板名称。</summary>
    public string TemplateName { get; set; } = "工作日全天";

    /// <summary>假日组编号（逗号分隔可多个），留空表示不关联假日组。</summary>
    public string? HolidayGroupNo { get; set; }

    /// <summary>开始时间 HH:mm:ss。</summary>
    public string BeginTime { get; set; } = "00:00:00";

    /// <summary>结束时间 HH:mm:ss。</summary>
    public string EndTime { get; set; } = "23:59:59";

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>工号批量请求（删除人员）。</summary>
public sealed class EmployeeNoBatchRequest
{
    public List<string> EmployeeNos { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>卡号批量请求（删除卡片）。</summary>
public sealed class CardNoBatchRequest
{
    public List<string> CardNos { get; set; } = new();

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>门控制请求。</summary>
public sealed class DoorControlRequest
{
    /// <summary>命令：open / close / alwaysOpen / alwaysClose。</summary>
    public IsapiDoorCommand Cmd { get; set; } = IsapiDoorCommand.Open;

    public uint TimeoutMs { get; set; } = 10000;
}

/// <summary>门禁事件查询请求。</summary>
public sealed class AcsEventSearchRequest
{
    public string? StartTime { get; set; }

    public string? EndTime { get; set; }

    /// <summary>事件主类型筛选，例如 1（报警）、2（异常）、3（操作）、5（门事件）。</summary>
    public List<int>? Major { get; set; }

    public List<int>? Minor { get; set; }

    public string? EmployeeNoString { get; set; }

    public string? CardNo { get; set; }

    public string? Name { get; set; }

    public int SearchResultPosition { get; set; }

    public int MaxResults { get; set; } = 30;

    /// <summary>
    /// 查询会话 ID（<c>searchID</c>）。翻页时必须复用上一次响应里的 <c>searchId</c>，
    /// 否则设备会当成新查询、从头发结果；留空则自动生成。
    /// </summary>
    public string? SearchId { get; set; }

    public uint TimeoutMs { get; set; } = 30000;

    /// <summary>取本次查询使用的 <c>searchID</c>（未指定则生成并记住，便于翻页复用）。</summary>
    public string ResolveSearchId()
        => string.IsNullOrWhiteSpace(SearchId) ? SearchId = Guid.NewGuid().ToString("N") : SearchId!;

    // 设备要求 major/minor 为标量（传数组会返回 badParameters）；0 表示全部。
    public object ToCondition() => new
    {
        searchID = ResolveSearchId(),
        searchResultPosition = SearchResultPosition,
        maxResults = MaxResults,
        major = Major is { Count: > 0 } ? Major[0] : 0,
        minor = Minor is { Count: > 0 } ? Minor[0] : 0,
        startTime = StartTime,
        endTime = EndTime,
        employeeNoString = EmployeeNoString,
        cardNo = CardNo,
        name = Name,
    };
}

/// <summary>原始 ISAPI 透传请求（调试用）。</summary>
public sealed class RawIsapiRequest
{
    /// <summary>完整请求行，例如 <c>POST /ISAPI/AccessControl/UserInfo/Search?format=json</c>。
    /// 若省略方法前缀，默认使用 POST。</summary>
    public string RequestUrl { get; set; } = string.Empty;

    /// <summary>请求体（JSON 或 XML 文本）。</summary>
    public string? Body { get; set; }

    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>人脸图片下发（URL/Base64 形式，非 multipart）。</summary>
public sealed class FaceUploadByImageRequest
{
    public string FpId { get; set; } = string.Empty;

    public string FdId { get; set; } = "1";

    public string FaceLibType { get; set; } = "blackFD";

    public string? Name { get; set; }

    public string? EmployeeNo { get; set; }

    public bool? OverWrite { get; set; }

    /// <summary>人脸图片 Base64（可带 data:image/jpeg;base64, 前缀）。</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>人脸图片 URL（http/https）。</summary>
    public string? ImageUrl { get; set; }

    public string? ContentType { get; set; }

    public string? FileName { get; set; }

    public string ImagePartName { get; set; } = "img";

    public string JsonPartName { get; set; } = "FaceDataRecord";

    public uint TimeoutMs { get; set; } = 15000;

    public FaceDownloadRequest ToDownloadRequest() => new()
    {
        FaceLibType = FaceLibType,
        FdId = FdId,
        FpId = FpId,
        Name = Name,
        EmployeeNo = EmployeeNo,
        OverWrite = OverWrite,
        ImageBase64 = ImageBase64,
        ImageUrl = ImageUrl,
        ContentType = ContentType,
        FileName = FileName,
        ImagePartName = ImagePartName,
        JsonPartName = JsonPartName,
        TimeoutMs = TimeoutMs,
    };
}
