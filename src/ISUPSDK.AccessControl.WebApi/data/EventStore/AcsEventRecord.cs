namespace ISUPSDK.AccessControl.WebApi.Data.EventStore;

/// <summary>
/// 已入库的一条门禁/报警事件（EF Core 实体，落 <c>ISUP:EventStore</c> 指定的数据库）。
///
/// 幂等性由 <see cref="DedupHash"/> 上的唯一索引保证：同一条设备事件（<c>deviceId:serialNo</c>）
/// 无论经过多少次重投、重放，最终只落一行。
/// </summary>
public sealed class AcsEventRecord
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>去重键原文（<c>deviceId:serialNo</c>，无序号时为报文哈希），最长 200 字符。</summary>
    public string DedupKey { get; set; } = string.Empty;

    /// <summary>去重键的 SHA-256（十六进制，64 字符），唯一索引列。</summary>
    public string DedupHash { get; set; } = string.Empty;

    /// <summary>设备 ID（序列号）。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>事件来源：HttpPush（设备 HTTP 上报）/IsupJson/Isapi/EHome/Reconcile（历史对账）/Ams。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>设备序列号（EHome/ISUP 报警回调提供，HTTP 推送通常为空）。</summary>
    public string? SerialNumber { get; set; }

    /// <summary>设备侧事件序号（报文里的 <c>serialNo</c>，与去重键相关）。</summary>
    public string? EventSerialNo { get; set; }

    /// <summary>业务事件类型（AccessControllerEvent / AcsEvent / 报警 topic 末段等）。</summary>
    public string? EventType { get; set; }

    /// <summary>
    /// 原始 subEventType（majorEventType=5 门禁事件时）。
    /// 常见值：1-刷卡开锁、6-未分配权限、7-无效时段、8-卡号过期、9-无此卡号、21-门锁打开、
    /// 22-门锁关闭、75-人脸开锁、76-人脸认证失败、1024-中心平台开锁
    /// （海康云开放平台《设备本地事件查询》对照表；个别型号语义可能不同，以现场实测为准）。
    /// </summary>
    public string? SubEventType { get; set; }

    /// <summary>事件主类型（报文 majorEventType / major，尽力解析）。</summary>
    public int? Major { get; set; }

    /// <summary>事件次类型（报文 subEventType / minor，尽力解析）。</summary>
    public int? Minor { get; set; }

    /// <summary>工号（employeeNoString / employeeNo，尽力解析）。</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>卡号（cardNo，尽力解析）。</summary>
    public string? CardNo { get; set; }

    /// <summary>人员姓名（name，尽力解析）。</summary>
    public string? PersonName { get; set; }

    /// <summary>门号（doorNo，尽力解析）。</summary>
    public int? DoorNo { get; set; }

    /// <summary>认证方式（currentVerifyMode：face/card/fingerprint…）。</summary>
    public string? VerifyMode { get; set; }

    /// <summary>OTAP 报警 Topic（仅 OTAP JSON 报警有值）。</summary>
    public string? AlarmTopic { get; set; }

    /// <summary>原始 byAlarmType。</summary>
    public int AlarmType { get; set; }

    /// <summary>原始 byDataType。</summary>
    public int DataType { get; set; }

    /// <summary>报文格式：json / xml / struct / binary。</summary>
    public string PayloadFormat { get; set; } = "json";

    /// <summary>报文原文（可用 <c>EventStore:StorePayload=false</c> 关闭，或按 <c>MaxPayloadBytes</c> 截断）。</summary>
    public string? Payload { get; set; }

    /// <summary>报文长度（字节）。</summary>
    public int PayloadBytes { get; set; }

    /// <summary>报文中是否被截断（true 表示已超过 MaxPayloadBytes）。</summary>
    public bool PayloadTruncated { get; set; }

    /// <summary>抓拍图片文件名列表（JSON 数组文本）。</summary>
    public string? PictureFileNames { get; set; }

    /// <summary>是否属于门禁（AccessControl）事件。</summary>
    public bool IsAccessControl { get; set; }

    /// <summary>HTTP 报警 URL（设备以 HTTP 方式上报时的请求路径）。</summary>
    public string? HttpUrl { get; set; }

    /// <summary>AMS 监听句柄（仅 AMS 通道有值）。</summary>
    public int? ListenHandle { get; set; }

    /// <summary>事件时间（报文中的设备时间，缺失时等于 <see cref="ReceivedAt"/>）。</summary>
    public DateTimeOffset EventTime { get; set; }

    /// <summary>平台接收时间（UTC）。</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>入队时间（UTC）。</summary>
    public DateTimeOffset EnqueuedAt { get; set; }

    /// <summary>入库时间（UTC）。</summary>
    public DateTimeOffset StoredAt { get; set; }

    /// <summary>队列后端（None / Msmq / RabbitMq）。</summary>
    public string? QueueProvider { get; set; }

    /// <summary>队列消息标识（MSMQ 的 LookupId / RabbitMQ 的 delivery tag）。</summary>
    public string? QueueMessageId { get; set; }

    /// <summary>投递次数（>1 表示经过重投/重放）。</summary>
    public int DeliveryAttempts { get; set; } = 1;
}
