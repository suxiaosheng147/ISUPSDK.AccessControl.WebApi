namespace ISUPSDK.AccessControl.WebApi.Models.EventStore;

/// <summary>
/// 手工补录一条门禁事件的请求（<c>POST /api/events/manual</c>）。
///
/// 用于两类场景：
/// <list type="bullet">
/// <item>设备/网络故障期间丢报，事后按台帐补录；</item>
/// <item>从其它系统（旧平台、Excel 导出的通行记录）导入历史事件。</item>
/// </list>
///
/// 未提供 <see cref="Payload"/> 时，服务会按下面的字段生成一份与设备上报同构的 ISAPI 报文，
/// 因此入库后的解析列（工号/卡号/门号/主次类型等）与真实事件一致。
/// </summary>
public sealed class AcsManualEventRequest
{
    /// <summary>设备 ID / 序列号（必填）。</summary>
    public string? DeviceId { get; set; }

    /// <summary>设备侧事件序号：参与去重键（<c>deviceId:serialNo</c>），重复提交同一条会只落一行。</summary>
    public string? SerialNo { get; set; }

    /// <summary>事件时间（留空用当前时间）。</summary>
    public DateTimeOffset? EventTime { get; set; }

    /// <summary>事件主类型（默认 5 = 门禁事件）。</summary>
    public int? Major { get; set; }

    /// <summary>事件次类型（默认 1 = 刷卡开锁；75-人脸开锁、76-人脸认证失败、21/22-门锁开/关 等）。</summary>
    public int? Minor { get; set; }

    /// <summary>工号。</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>人员姓名。</summary>
    public string? PersonName { get; set; }

    /// <summary>卡号。</summary>
    public string? CardNo { get; set; }

    /// <summary>门号。</summary>
    public int? DoorNo { get; set; }

    /// <summary>认证方式（face / card / fingerprint …）。</summary>
    public string? VerifyMode { get; set; }

    /// <summary>业务事件类型（默认 <c>AccessControllerEvent</c>）。</summary>
    public string? EventType { get; set; }

    /// <summary>事件来源（默认 <c>Manual</c>，便于在库里与真实上报区分）。</summary>
    public string? Source { get; set; }

    /// <summary>是否门禁事件（默认 true）。</summary>
    public bool? IsAccessControl { get; set; }

    /// <summary>直接提供原始报文（给了就不再按字段生成），格式由 <see cref="PayloadFormat"/> 指定。</summary>
    public string? Payload { get; set; }

    /// <summary>原始报文格式：json（默认）/ xml。</summary>
    public string? PayloadFormat { get; set; }

    /// <summary>关联的抓拍图片文件名（仅登记文件名，不上传文件）。</summary>
    public List<string>? PictureFileNames { get; set; }

    /// <summary>外部图片直链（图片服务器可访问的完整 URL，平台不下载，仅随事件保存供前端显示）。</summary>
    public string? PictureUrl { get; set; }
}

/// <summary>手工补录的处理结果。</summary>
public sealed record AcsManualEventResult
{
    /// <summary>请求条数。</summary>
    public int Requested { get; init; }

    /// <summary>受理（去重通过）并已落盘/入队的条数。</summary>
    public int Accepted { get; init; }

    /// <summary>因已存在（去重键重复）而跳过的条数。</summary>
    public int Duplicates { get; init; }

    /// <summary>直接写入数据库的行数（仅 <c>directToStore=true</c> 时有值）。</summary>
    public int Stored { get; init; }

    /// <summary>参数不合法等原因被拒绝的条数。</summary>
    public int Rejected { get; init; }

    /// <summary>是否绕过消息队列直接把事件写入数据库。</summary>
    public bool DirectToStore { get; init; }

    /// <summary>逐条结果的摘要。</summary>
    public IReadOnlyList<AcsManualEventItemResult> Items { get; init; } = Array.Empty<AcsManualEventItemResult>();
}

/// <summary>手工补录中单条事件的结果。</summary>
public sealed record AcsManualEventItemResult
{
    /// <summary>序号（从 0 开始，对应请求体中的位置）。</summary>
    public int Index { get; init; }

    /// <summary>设备 ID。</summary>
    public string? DeviceId { get; init; }

    /// <summary>去重键（受理成功时给出，便于核对）。</summary>
    public string? DedupKey { get; init; }

    /// <summary>结果：Accepted / Duplicate / Stored / Rejected。</summary>
    public string Status { get; init; } = "Rejected";

    /// <summary>失败原因（Status=Rejected 时给出）。</summary>
    public string? Error { get; init; }
}
