using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ISUPSDK.AccessControl.WebApi.Models.Isapi;

// ============================================================================================
// 海康威视 ISAPI 门禁（AccessControl）请求/响应模型。
//
// 说明：不同设备型号与固件版本对字段的支持存在差异（例如部分门禁主机不支持
// floorNumber、userVerifyMode、localUIRight），SDK/设备会忽略不支持的字段或返回
// SubStatusCode。因此本项目所有模型均使用"可空 + 忽略空值"的序列化策略，
// 只把调用方显式赋值的字段下发到设备。
// ============================================================================================

/// <summary>人员有效期限。</summary>
public sealed class IsapiValidPeriod
{
    /// <summary>是否启用有效期限制。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 时间类型：local（设备本地时间）、UTC。
    /// </summary>
    [JsonPropertyName("timeType")]
    public string? TimeType { get; set; }

    /// <summary>生效开始时间 yyyy-MM-ddTHH:mm:ss。</summary>
    [JsonPropertyName("beginTime")]
    public string? BeginTime { get; set; }

    /// <summary>生效结束时间 yyyy-MM-ddTHH:mm:ss。</summary>
    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }
}

/// <summary>权限计划模板引用（人员 UserInfo.RightPlan 中引用模板编号）。</summary>
public sealed class IsapiRightPlan
{
    /// <summary>计划模板编号（1~255）。模板 1 为设备内置的全天候模板。</summary>
    [JsonPropertyName("planTemplateNo")]
    public string? PlanTemplateNo { get; set; }

    /// <summary>门编号。同一门下的多个计划模板按"或"处理。</summary>
    [JsonPropertyName("doorNo")]
    public int? DoorNo { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 人员信息（UserInfo）。对应 <c>POST /ISAPI/AccessControl/UserInfo/Record?format=json</c>
/// 以及 <c>PUT /ISAPI/AccessControl/UserInfo/Delete?format=json</c> 的 UserInfo 节点。
/// </summary>
public sealed class IsapiUserInfo
{
    /// <summary>人员工号（必填，设备内唯一，最长 32 字节）。</summary>
    [Required]
    [StringLength(32)]
    [JsonPropertyName("employeeNo")]
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>姓名（最长 32 个字符，UTF-8）。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>人员类型：normal / visitor / blackList。</summary>
    [JsonPropertyName("userType")]
    public string? UserType { get; set; }

    /// <summary>所属组织/组编号。</summary>
    [JsonPropertyName("belongGroup")]
    public string? BelongGroup { get; set; }

    /// <summary>人员密码（需设备支持密码认证）。</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>关门超时时间：1~255 秒。</summary>
    [JsonPropertyName("closeDoorTime")]
    public int? CloseDoorTime { get; set; }

    /// <summary>有效期。</summary>
    [JsonPropertyName("Valid")]
    public IsapiValidPeriod? Valid { get; set; }

    /// <summary>门权限（doorRight）：多个门用逗号分隔，例如 "1,2"。设备也支持 doorNo 数组形式。</summary>
    [JsonPropertyName("doorRight")]
    public string? DoorRight { get; set; }

    /// <summary>权限计划模板列表。</summary>
    [JsonPropertyName("RightPlan")]
    public List<IsapiRightPlan>? RightPlan { get; set; }

    /// <summary>最大开门时间：1~255 秒。</summary>
    [JsonPropertyName("maxOpenDoorTime")]
    public int? MaxOpenDoorTime { get; set; }

    /// <summary>开门时间（秒）。</summary>
    [JsonPropertyName("openDoorTime")]
    public int? OpenDoorTime { get; set; }

    /// <summary>房间号。</summary>
    [JsonPropertyName("roomNumber")]
    public int? RoomNumber { get; set; }

    /// <summary>楼层号。</summary>
    [JsonPropertyName("floorNumber")]
    public int? FloorNumber { get; set; }

    /// <summary>是否启用本地 UI 权限。</summary>
    [JsonPropertyName("localUIRight")]
    public bool? LocalUiRight { get; set; }

    /// <summary>性别：male / female / unknown。</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    /// <summary>人员序号（查询响应中返回，新增时可不填）。</summary>
    [JsonPropertyName("personId")]
    public int? PersonId { get; set; }

    /// <summary>关联卡号数量（仅查询响应）。</summary>
    [JsonPropertyName("numOfCard")]
    public int? NumOfCard { get; set; }

    /// <summary>关联人脸数量（仅查询响应）。</summary>
    [JsonPropertyName("numOfFace")]
    public int? NumOfFace { get; set; }

    /// <summary>关联指纹数量（仅查询响应）。</summary>
    [JsonPropertyName("numOfFP")]
    public int? NumOfFp { get; set; }

    /// <summary>自定义扩展字段，按原样合并到请求 JSON（用于适配特定型号的私有字段）。</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>新增/修改人员的请求体根节点（POST/PUT /ISAPI/AccessControl/UserInfo/Record）。</summary>
public sealed class IsapiUserInfoRecord
{
    [JsonPropertyName("UserInfo")]
    public IsapiUserInfo UserInfo { get; set; } = new();
}

/// <summary>删除人员的请求体（PUT /ISAPI/AccessControl/UserInfo/Delete）。</summary>
public sealed class IsapiUserInfoDelete
{
    /// <summary>删除模式：byEmployeeNo（按工号）。</summary>
    [JsonPropertyName("mode")]
    public string Mode { get; set; } = "byEmployeeNo";

    /// <summary>要删除的工号列表。</summary>
    [JsonPropertyName("EmployeeNoList")]
    public List<IsapiEmployeeNo> EmployeeNoList { get; set; } = new();
}

/// <summary>
/// 删除人员的条件（UserInfoDelCond）。
/// <c>PUT /ISAPI/AccessControl/UserInfo/Delete?format=json</c>
/// <code>{ "UserInfoDelCond": { "EmployeeNoList": [ { "employeeNo": "1001" } ] } }</code>
/// </summary>
public sealed class UserInfoDelCond
{
    /// <summary>按工号删除时的工号列表。与 <see cref="Mode"/> 互斥。</summary>
    [JsonPropertyName("EmployeeNoList")]
    public List<IsapiEmployeeNo>? EmployeeNoList { get; set; }

    /// <summary>删除模式：all（清空全部人员）。仅部分固件支持。</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

/// <summary>工号条目（删除请求使用）。</summary>
public sealed class IsapiEmployeeNo
{
    [JsonPropertyName("employeeNo")]
    public string EmployeeNo { get; set; } = string.Empty;
}

/// <summary>查询条件（UserInfo/Search）。</summary>
public sealed class IsapiUserInfoSearchCond
{
    /// <summary>
    /// 查询会话 ID。分页时必须保持同一个 searchID，否则设备会从第 1 页重新开始。
    /// </summary>
    [JsonPropertyName("searchID")]
    public string SearchId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 30;

    /// <summary>按工号过滤。</summary>
    [JsonPropertyName("EmployeeNoList")]
    public List<IsapiEmployeeNo>? EmployeeNoList { get; set; }

    /// <summary>按姓名过滤（前缀匹配）。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("userType")]
    public string? UserType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>查询请求包装。</summary>
public sealed class IsapiUserInfoSearch
{
    [JsonPropertyName("UserInfoSearchCond")]
    public IsapiUserInfoSearchCond UserInfoSearchCond { get; set; } = new();
}

/// <summary>查询响应。</summary>
public sealed class IsapiUserInfoSearchResult
{
    [JsonPropertyName("searchID")]
    public string? SearchId { get; set; }

    /// <summary>查询状态：OK / MORE / NO MATCH。</summary>
    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("UserInfo")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<IsapiUserInfo>))]
    public List<IsapiUserInfo>? UserInfo { get; set; }
}

/// <summary>查询响应包装。</summary>
public sealed class IsapiUserInfoSearchResponse
{
    [JsonPropertyName("UserInfoSearch")]
    public IsapiUserInfoSearchResult? UserInfoSearch { get; set; }

    [JsonPropertyName("ResponseStatus")]
    public IsapiResponseStatus? ResponseStatus { get; set; }
}

/// <summary>数量统计条件（UserInfo/Count）。</summary>
public sealed class IsapiUserInfoCountCond
{
    [JsonPropertyName("userType")]
    public string? UserType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

// ============================================================================================
// 卡片（CardInfo）
// ============================================================================================

/// <summary>卡片信息。对应 <c>POST /ISAPI/AccessControl/CardInfo/Record?format=json</c>。</summary>
public sealed class IsapiCardInfo
{
    /// <summary>卡号（必填）。</summary>
    [Required]
    [StringLength(32)]
    [JsonPropertyName("cardNo")]
    public string CardNo { get; set; } = string.Empty;

    /// <summary>关联人员工号（必填，需已存在）。</summary>
    [Required]
    [StringLength(32)]
    [JsonPropertyName("employeeNo")]
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>卡片类型：normalCard（普通卡）、hijackCard（胁迫卡）、patrolCard（巡更卡）、
    /// superCard（超卡）、dismissCard（解除卡）、securityCard（安全卡）等。</summary>
    [JsonPropertyName("cardType")]
    public string? CardType { get; set; }

    /// <summary>卡号所属门（doorNo）。</summary>
    [JsonPropertyName("doorNo")]
    public int? DoorNo { get; set; }

    /// <summary>有效期限。</summary>
    [JsonPropertyName("Valid")]
    public IsapiValidPeriod? Valid { get; set; }

    /// <summary>卡密码。</summary>
    [JsonPropertyName("password")]
    public string? Password { get; set; }

    /// <summary>自定义扩展字段。</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>新增/修改卡片的请求体根节点（POST/PUT /ISAPI/AccessControl/CardInfo/Record）。</summary>
public sealed class IsapiCardInfoRecord
{
    [JsonPropertyName("CardInfo")]
    public IsapiCardInfo CardInfo { get; set; } = new();
}

/// <summary>
/// 删除卡片的条件（CardInfoDelCond）。
/// <code>{ "CardInfoDelCond": { "CardNoList": [ { "cardNo": "123" } ] } }</code>
/// <code>{ "CardInfoDelCond": { "EmployeeNoList": [ { "employeeNo": "1001" } ] } }</code>
/// <code>{ "CardInfoDelCond": { "mode": "all" } }</code>
/// </summary>
public sealed class CardInfoDelCond
{
    [JsonPropertyName("CardNoList")]
    public List<IsapiCardNo>? CardNoList { get; set; }

    [JsonPropertyName("EmployeeNoList")]
    public List<IsapiEmployeeNo>? EmployeeNoList { get; set; }

    /// <summary>删除模式：all（清空全部卡片）。</summary>
    [JsonPropertyName("mode")]
    public string? Mode { get; set; }
}

public sealed class IsapiCardNo
{
    [JsonPropertyName("cardNo")]
    public string CardNo { get; set; } = string.Empty;
}

/// <summary>卡片查询条件。</summary>
public sealed class IsapiCardInfoSearchCond
{
    /// <summary>查询会话 ID（分页时必须保持不变）。</summary>
    [JsonPropertyName("searchID")]
    public string SearchId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 30;

    /// <summary>扁平卡号过滤（部分固件使用 CardNoList 数组，见 <see cref="CardNoList"/>）。</summary>
    [JsonPropertyName("cardNo")]
    public string? CardNo { get; set; }

    /// <summary>按卡号数组过滤（部分固件支持）。</summary>
    [JsonPropertyName("CardNoList")]
    public List<IsapiCardNo>? CardNoList { get; set; }

    /// <summary>扁平工号过滤。</summary>
    [JsonPropertyName("employeeNo")]
    public string? EmployeeNo { get; set; }

    /// <summary>按工号数组过滤（部分固件支持）。</summary>
    [JsonPropertyName("EmployeeNoList")]
    public List<IsapiEmployeeNo>? EmployeeNoList { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

public sealed class IsapiCardInfoSearch
{
    [JsonPropertyName("CardInfoSearchCond")]
    public IsapiCardInfoSearchCond CardInfoSearchCond { get; set; } = new();
}

public sealed class IsapiCardInfoSearchResult
{
    [JsonPropertyName("searchID")]
    public string? SearchId { get; set; }

    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    [JsonPropertyName("CardInfo")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<IsapiCardInfo>))]
    public List<IsapiCardInfo>? CardInfo { get; set; }
}

public sealed class IsapiCardInfoSearchResponse
{
    [JsonPropertyName("CardInfoSearch")]
    public IsapiCardInfoSearchResult? CardInfoSearch { get; set; }

    [JsonPropertyName("ResponseStatus")]
    public IsapiResponseStatus? ResponseStatus { get; set; }
}

// ============================================================================================
// 人脸（FDLib / FaceDataRecord）
// ============================================================================================

/// <summary>
/// 人脸下发时的 JSON 元数据。
/// 对应 <c>POST /ISAPI/Intelligent/FDLib/FaceDataRecord?format=json</c> 的 multipart JSON 分段
/// （分段名固定为 <c>FaceDataRecord</c>），或 JSON 直接下发（faceURL 方式）。
/// </summary>
public sealed class IsapiFaceDataRecord
{
    /// <summary>人脸库类型：门禁终端人员库固定使用 blackFD。</summary>
    [JsonPropertyName("faceLibType")]
    public string FaceLibType { get; set; } = "blackFD";

    /// <summary>人脸库 ID，门禁终端固定为 1。</summary>
    [Required]
    [JsonPropertyName("FDID")]
    public string FdId { get; set; } = "1";

    /// <summary>人脸图片 ID，等于人员工号（最长 63 字节）。</summary>
    [Required]
    [JsonPropertyName("FPID")]
    public string FpId { get; set; } = string.Empty;

    /// <summary>人员姓名（可选，部分固件支持）。</summary>
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>人员工号（可选）。</summary>
    [JsonPropertyName("employeeNo")]
    public string? EmployeeNo { get; set; }

    /// <summary>人脸图片 URL（JSON 下发方式，比 multipart 更稳定）。提供时无需上传图片二进制。</summary>
    [JsonPropertyName("faceURL")]
    public string? FaceUrl { get; set; }

    /// <summary>性别：male / female / unknown（可选）。</summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }

    /// <summary>是否覆盖已有图片（部分固件支持）。</summary>
    [JsonPropertyName("overWrite")]
    public bool? OverWrite { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>人脸库信息（FDLib 创建/查询）。</summary>
public sealed class IsapiFdLib
{
    [JsonPropertyName("FDID")]
    public string FdId { get; set; } = "1";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    /// <summary>人脸库类型：blackFD（门禁终端人员库）/ infraredFD / staticFD。</summary>
    [JsonPropertyName("faceLibType")]
    public string FaceLibType { get; set; } = "blackFD";

    /// <summary>人脸库容量。</summary>
    [JsonPropertyName("capacity")]
    public int? Capacity { get; set; }

    /// <summary>自定义扩展字段。</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>人脸库请求包装。</summary>
public sealed class IsapiFdLibRecord
{
    [JsonPropertyName("FDLib")]
    public IsapiFdLib FdLib { get; set; } = new();
}

/// <summary>人脸图片查询条件（FDSearch）。</summary>
public sealed class IsapiFaceSearchCond
{
    [JsonPropertyName("searchID")]
    public string SearchId { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; }

    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 30;

    [JsonPropertyName("faceLibType")]
    public string FaceLibType { get; set; } = "blackFD";

    [JsonPropertyName("FDID")]
    public string FdId { get; set; } = "1";

    [JsonPropertyName("FPID")]
    public string? FpId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

public sealed class IsapiFaceSearchResponse
{
    [JsonPropertyName("ResponseStatus")]
    public IsapiResponseStatus? ResponseStatus { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("statusString")]
    public string? StatusString { get; set; }

    [JsonPropertyName("subStatusCode")]
    public string? SubStatusCode { get; set; }

    /// <summary>匹配结果列表。设备可能只返回一条时输出"裸对象"，已通过转换器统一为列表。</summary>
    [JsonPropertyName("MatchList")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<MatchListItem>))]
    public List<MatchListItem>? MatchList { get; set; }

    [JsonPropertyName("numOfMatches")]
    public int? NumOfMatches { get; set; }

    [JsonPropertyName("totalMatches")]
    public int? TotalMatches { get; set; }

    [JsonPropertyName("responseStatusStrg")]
    public string? ResponseStatusStrg { get; set; }
}

// ============================================================================================
// 权限计划模板（UserRightPlanTemplate / PlanTemplate）
// ============================================================================================

/// <summary>
/// 人员权限计划模板（UserRightPlanTemplate）。
///
/// 权威依据：《人员权限计划模板配置》文档。
/// <code>
/// PUT /ISAPI/AccessControl/UserRightPlanTemplate/&lt;planTemplateID&gt;?format=json
/// GET /ISAPI/AccessControl/UserRightPlanTemplate/&lt;planTemplateID&gt;?format=json
/// POST /ISAPI/AccessControl/UserRightPlanTemplate/Search?format=json
/// </code>
/// 模板把"一个周计划 + 假日组"绑定在一起，再由人员的 UserInfo.RightPlan 引用；
/// <b>人员权限计划必须配置，否则下发的人员没有任何通行权限。</b>
/// 假日计划的优先级高于周计划。
/// </summary>
public sealed class IsapiPlanTemplate
{
    /// <summary>是否启用。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>模板名称（设备侧通常限长 32）。</summary>
    [JsonPropertyName("templateName")]
    public string? TemplateName { get; set; }

    /// <summary>周计划编号（从 1 开始，上限由能力集给出）。设备出厂默认返回 0。</summary>
    [JsonPropertyName("weekPlanNo")]
    public int? WeekPlanNo { get; set; }

    /// <summary>
    /// 假日组编号，逗号分隔可关联多个（例如 "1,3,5"）；
    /// 空字符串 "" 表示不关联假日组。
    /// </summary>
    [JsonPropertyName("holidayGroupNo")]
    public string? HolidayGroupNo { get; set; }

    /// <summary>操作类型：byTerminal（按终端）/ byOrg（按组织）/ byTerminalOrg（按终端组织）。</summary>
    [JsonPropertyName("operateType")]
    public string? OperateType { get; set; }

    /// <summary>终端 ID 列表。operateType 为 byTerminal / byTerminalOrg 时必填。</summary>
    [JsonPropertyName("terminalNoList")]
    public List<int>? TerminalNoList { get; set; }

    /// <summary>组织 ID 列表。operateType 为 byOrg / byTerminalOrg 时必填。</summary>
    [JsonPropertyName("orgNoList")]
    public List<int>? OrgNoList { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>一个时间段（TimeSegment）。</summary>
public sealed class IsapiTimeSegment
{
    /// <summary>开始时间点，设备本地时间，格式 HH:mm:ss（文档示例 "10:10:00"）。</summary>
    [JsonPropertyName("beginTime")]
    public string? BeginTime { get; set; }

    /// <summary>结束时间点，设备本地时间，格式 HH:mm:ss。</summary>
    [JsonPropertyName("endTime")]
    public string? EndTime { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 周计划中的一条记录（WeekPlanCfg 数组元素）。
/// 每天最多 8 个时间段，用 <see cref="Id"/> 区分（1~8）。
/// </summary>
public sealed class IsapiWeekPlanItem
{
    /// <summary>星期：Monday / Tuesday / Wednesday / Thursday / Friday / Saturday / Sunday。</summary>
    [Required]
    [JsonPropertyName("week")]
    public string Week { get; set; } = string.Empty;

    /// <summary>时间段编号，范围 1~8。</summary>
    [Required]
    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;

    /// <summary>该时间段是否使能。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>时间段起止。</summary>
    [Required]
    [JsonPropertyName("TimeSegment")]
    public IsapiTimeSegment TimeSegment { get; set; } = new();

    /// <summary>认证次数使能（可选，默认 false）。</summary>
    [JsonPropertyName("authenticationTimesEnabled")]
    public bool? AuthenticationTimesEnabled { get; set; }

    /// <summary>
    /// 认证次数，范围 0~255。处于当前有效期、认证使能且认证次数未超时计划模板验证通过；
    /// 0 表示不能认证。
    /// </summary>
    [JsonPropertyName("authenticationTimes")]
    public int? AuthenticationTimes { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 人员权限周计划（UserRightWeekPlanCfg）。
/// <code>
/// PUT /ISAPI/AccessControl/UserRightWeekPlanCfg/&lt;weekPlanID&gt;?format=json
/// POST /ISAPI/AccessControl/UserRightWeekPlanCfg/Search?format=json
/// </code>
/// </summary>
public sealed class IsapiWeekPlanCfg
{
    /// <summary>周计划是否使能。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>周计划参数（周一至周日 × 每天最多 8 段，全部平铺在同一个数组中）。</summary>
    [Required]
    [JsonPropertyName("WeekPlanCfg")]
    public List<IsapiWeekPlanItem> WeekPlanCfg { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 人员权限假日组（UserRightHolidayGroupCfg）——假日组的"容器"。
/// <code>
/// PUT /ISAPI/AccessControl/UserRightHolidayGroupCfg/&lt;holidayGroupID&gt;?format=json
/// GET /ISAPI/AccessControl/UserRightHolidayGroupCfg/&lt;holidayGroupID&gt;?format=json
/// POST /ISAPI/AccessControl/UserRightHolidayGroupCfg/Search?format=json
/// </code>
/// </summary>
public sealed class IsapiHolidayGroupCfg
{
    /// <summary>是否启用。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>假日组名称。</summary>
    [Required]
    [JsonPropertyName("groupName")]
    public string GroupName { get; set; } = string.Empty;

    /// <summary>假日组包含的假日计划编号，逗号分隔（例如 "1,3,5"）；可为空字符串。</summary>
    [Required]
    [JsonPropertyName("holidayPlanNo")]
    public string HolidayPlanNo { get; set; } = string.Empty;

    /// <summary>操作类型：byTerminal / byOrg / byTerminalOrg。</summary>
    [JsonPropertyName("operateType")]
    public string? OperateType { get; set; }

    /// <summary>终端 ID 列表。</summary>
    [JsonPropertyName("terminalNoList")]
    public List<int>? TerminalNoList { get; set; }

    /// <summary>组织 ID 列表。</summary>
    [JsonPropertyName("orgNoList")]
    public List<int>? OrgNoList { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>假日计划中的一条时间段记录（HolidayPlanCfg 数组元素）。</summary>
public sealed class IsapiHolidayPlanItem
{
    /// <summary>时间段编号，范围 1~8。</summary>
    [Required]
    [JsonPropertyName("id")]
    public int Id { get; set; } = 1;

    /// <summary>该时间段是否使能。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>时间段起止。</summary>
    [Required]
    [JsonPropertyName("TimeSegment")]
    public IsapiTimeSegment TimeSegment { get; set; } = new();

    /// <summary>认证次数使能（可选，默认 false）。</summary>
    [JsonPropertyName("authenticationTimesEnabled")]
    public bool? AuthenticationTimesEnabled { get; set; }

    /// <summary>认证次数，范围 0~255。</summary>
    [JsonPropertyName("authenticationTimes")]
    public int? AuthenticationTimes { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 人员权限假日计划（UserRightHolidayPlanCfg）——一个具体假日（日期区间）。
/// <code>
/// PUT /ISAPI/AccessControl/UserRightHolidayPlanCfg/&lt;holidayPlanID&gt;?format=json
/// GET /ISAPI/AccessControl/UserRightHolidayPlanCfg/&lt;holidayPlanID&gt;?format=json
/// POST /ISAPI/AccessControl/UserRightHolidayPlanCfg/Search?format=json
/// </code>
/// 假日计划内部的每天时间段相同（最多 8 段）。
///
/// 注意：日期与时间段在"删除假日计划"场景下允许缺省
/// （删除形态为 <c>enable=false</c> + <c>deleteHolidayPlan=true</c>），
/// 因此这里不加 <c>[Required]</c>，必填性由控制器按场景校验。
/// </summary>
public sealed class IsapiHolidayPlanCfg
{
    /// <summary>使能。</summary>
    [JsonPropertyName("enable")]
    public bool Enable { get; set; } = true;

    /// <summary>
    /// 是否删除假日计划。为 true 时设备需把该假日计划从假日组中删除。
    /// 与 <see cref="Enable"/>=false 配合使用。
    /// </summary>
    [JsonPropertyName("deleteHolidayPlan")]
    public bool? DeleteHolidayPlan { get; set; }

    /// <summary>假日计划名称（限长 64）。</summary>
    [JsonPropertyName("holidayPlanName")]
    public string? HolidayPlanName { get; set; }

    /// <summary>假日开始日期 yyyy-MM-dd（设备本地时间）。非删除场景必填。</summary>
    [JsonPropertyName("beginDate")]
    public string? BeginDate { get; set; }

    /// <summary>假日结束日期 yyyy-MM-dd（设备本地时间）。非删除场景必填。</summary>
    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    /// <summary>
    /// 假日计划按年循环，默认 false。使能后 beginDate / endDate 仍按
    /// yyyy-MM-dd 下发，但界面展示时不显示年份。
    /// </summary>
    [JsonPropertyName("cycleByYear")]
    public bool? CycleByYear { get; set; }

    /// <summary>假日时间段参数（最多 8 段）。非删除场景必填。</summary>
    [JsonPropertyName("HolidayPlanCfg")]
    public List<IsapiHolidayPlanItem> HolidayPlanCfg { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>
/// 五种配置对应的查询条件（POST .../Search）。
/// 注意：<c>searchResultPosition</c> <b>从 1 开始</b>（不是 0）。
/// </summary>
public sealed class IsapiUserRightSearchCond
{
    /// <summary>
    /// 查询标识，1~32 位字母数字。查询条件不变时该值必须不变；
    /// 若新请求的 searchID 与设备正在执行的任务相同，设备返回错误。
    /// </summary>
    [Required]
    [JsonPropertyName("searchID")]
    public string SearchId { get; set; } = Guid.NewGuid().ToString("N")[..16];

    /// <summary>查询结果起始位置，范围 [1,256]/[1,1024]，从 1 开始。</summary>
    [JsonPropertyName("searchResultPosition")]
    public int SearchResultPosition { get; set; } = 1;

    /// <summary>本次交互最大结果数，范围 [1,100]（建议不超过 32）。</summary>
    [JsonPropertyName("maxResults")]
    public int MaxResults { get; set; } = 32;

    /// <summary>是否使能过滤（周计划/假日计划/计划模板）。</summary>
    [JsonPropertyName("enable")]
    public bool? Enable { get; set; }

    /// <summary>按指定假日计划 ID 列表查询（仅假日计划查询）。</summary>
    [JsonPropertyName("holidayPlanIDList")]
    public List<int>? HolidayPlanIdList { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }

    /// <summary>创建分页副本（复用同一个 searchID）。</summary>
    public IsapiUserRightSearchCond CloneForPage(int position) => new()
    {
        SearchId = SearchId,
        SearchResultPosition = position,
        MaxResults = MaxResults,
        Enable = Enable,
        HolidayPlanIdList = HolidayPlanIdList,
        Extra = Extra,
    };
}

/// <summary>UserRightPlanTemplate 查询结果条目。</summary>
public sealed class IsapiPlanTemplateMatch
{
    [JsonPropertyName("planTemplateID")]
    public int PlanTemplateId { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("templateName")]
    public string? TemplateName { get; set; }

    [JsonPropertyName("weekPlanNo")]
    public int WeekPlanNo { get; set; }

    [JsonPropertyName("holidayGroupNo")]
    public string? HolidayGroupNo { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>UserRightWeekPlanCfg 查询结果条目。</summary>
public sealed class IsapiWeekPlanMatch
{
    [JsonPropertyName("weekPlanID")]
    public int WeekPlanId { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("WeekPlanCfg")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<IsapiWeekPlanItem>))]
    public List<IsapiWeekPlanItem>? WeekPlanCfg { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>UserRightHolidayGroupCfg 查询结果条目。</summary>
public sealed class IsapiHolidayGroupMatch
{
    [JsonPropertyName("holidayGroupID")]
    public int HolidayGroupId { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("groupName")]
    public string? GroupName { get; set; }

    [JsonPropertyName("holidayPlanNo")]
    public string? HolidayPlanNo { get; set; }

    [JsonPropertyName("holidayPlanName")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<string>))]
    public List<string>? HolidayPlanName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>UserRightHolidayPlanCfg 查询结果条目。</summary>
public sealed class IsapiHolidayPlanMatch
{
    [JsonPropertyName("holidayPlanID")]
    public int HolidayPlanId { get; set; }

    [JsonPropertyName("enable")]
    public bool Enable { get; set; }

    [JsonPropertyName("holidayPlanName")]
    public string? HolidayPlanName { get; set; }

    [JsonPropertyName("beginDate")]
    public string? BeginDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("cycleByYear")]
    public bool? CycleByYear { get; set; }

    [JsonPropertyName("HolidayPlanCfg")]
    [JsonConverter(typeof(Services.SingleOrArrayConverter<IsapiHolidayPlanItem>))]
    public List<IsapiHolidayPlanItem>? HolidayPlanCfg { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

/// <summary>四类人员权限配置的通用查询响应封装（responseStatus / numOfMatches / totalMatches / matchResults）。</summary>
public sealed class IsapiUserRightSearchResponse<TMatch>
{
    /// <summary>查询状态：OK（结束）/ MORE（还有数据）/ NO MATCH / abnormal。</summary>
    [JsonPropertyName("responseStatus")]
    public string? ResponseStatus { get; set; }

    /// <summary>本次返回记录数。</summary>
    [JsonPropertyName("numOfMatches")]
    public int NumOfMatches { get; set; }

    /// <summary>符合条件的记录总数。</summary>
    [JsonPropertyName("totalMatches")]
    public int TotalMatches { get; set; }

    /// <summary>
    /// 查询结果。设备可能返回裸对象（单条）或数组，反序列化时由
    /// <see cref="Services.SingleOrArrayConverter{TMatch}"/> 兼容两种形态
    /// （泛型实参无法写在特性里，因此在 <c>IsupJson</c> 反序列化时按需注入转换器）。
    /// </summary>
    [JsonPropertyName("matchResults")]
    public List<TMatch>? MatchResults { get; set; }

    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }

    /// <summary>是否还有下一页。</summary>
    public bool HasMore => string.Equals(ResponseStatus, "MORE", StringComparison.OrdinalIgnoreCase);
}

// ============================================================================================
// 门禁控制（RemoteControl / 事件）
// ============================================================================================

/// <summary>门的远程控制命令。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IsapiDoorCommand>))]
public enum IsapiDoorCommand
{
    /// <summary>开门（常开一次）</summary>
    Open,

    /// <summary>关门</summary>
    Close,

    /// <summary>常开</summary>
    AlwaysOpen,

    /// <summary>常闭</summary>
    AlwaysClose,
}

/// <summary>
/// 门控制请求体（PUT /ISAPI/AccessControl/RemoteControl/door/&lt;doorNo&gt;?format=json）。
/// <code>{ "RemoteControlDoor": { "cmd": "open" } }</code>
/// </summary>
public sealed class IsapiDoorControl
{
    [JsonPropertyName("RemoteControlDoor")]
    public IsapiDoorControlBody RemoteControlDoor { get; set; } = new();
}

public sealed class IsapiDoorControlBody
{
    /// <summary>命令：open / close / alwaysOpen / alwaysClose（后两者部分固件不支持）。</summary>
    [Required]
    [JsonPropertyName("cmd")]
    public string Cmd { get; set; } = "open";

    /// <summary>自定义扩展字段。</summary>
    [JsonExtensionData]
    public Dictionary<string, object?>? Extra { get; set; }
}

// ============================================================================================
// 通用响应封装（ResponseStatus）
// ============================================================================================

/// <summary>设备返回的通用响应状态。</summary>
public sealed class IsapiResponseStatus
{
    [JsonPropertyName("requestURL")]
    public string? RequestUrl { get; set; }

    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("statusString")]
    public string? StatusString { get; set; }

    /// <summary>子状态码，例如 <c>ok</c>、<c>DeviceBusy</c>、<c>RebootRequired</c> 等。</summary>
    [JsonPropertyName("subStatusCode")]
    public string? SubStatusCode { get; set; }

    [JsonPropertyName("errorCode")]
    public int? ErrorCode { get; set; }

    [JsonPropertyName("errorMsg")]
    public string? ErrorMsg { get; set; }

    /// <summary>透传响应的 HTTP 状态码（本项目补充字段，非设备返回）。</summary>
    [JsonIgnore]
    public int HttpStatusCode { get; set; }
}

/// <summary>
/// 人脸检索结果条目（MatchList 元素）。设备返回的字段随固件版本变化，
/// 因此使用开放式字典承载，避免丢失未知字段。
/// </summary>
public sealed class MatchListItem : Dictionary<string, object?>
{
    public MatchListItem()
    {
    }

    public MatchListItem(IDictionary<string, object?> source) : base(source)
    {
    }
}