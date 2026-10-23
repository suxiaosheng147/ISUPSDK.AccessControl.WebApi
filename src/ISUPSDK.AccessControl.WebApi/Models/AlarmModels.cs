using System.Text.Json.Serialization;

namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>
/// 一条已接收并解析的门禁/报警事件。
/// </summary>
public sealed class IsupAlarmEvent
{
    /// <summary>自增序号（进程内唯一，按接收顺序）。</summary>
    public long Sequence { get; set; }

    /// <summary>接收时间（UTC）。</summary>
    public DateTimeOffset ReceivedAt { get; set; }

    /// <summary>报警监听句柄。</summary>
    public int ListenHandle { get; set; }

    /// <summary>设备 ID（来自 OTAP_AMS_ALARM_MSG.szDeviceID）。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>报警来源类型：IsupJson / Isapi / EHome / Exception。</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>原始 byAlarmType 值。</summary>
    public byte AlarmType { get; set; }

    /// <summary>原始 byDataType 值。</summary>
    public byte DataType { get; set; }

    /// <summary>OTAP 报警的 Topic（仅 OTAP JSON 报警有值）。</summary>
    public string? AlarmTopic { get; set; }

    /// <summary>
    /// 业务事件类型（尽力解析）：
    /// 门禁事件为 <c>AcsEvent</c>；ISAPI 报警取 <c>eventType</c>；
    /// OTAP 报警取 Topic 末段。
    /// </summary>
    public string? EventType { get; set; }

    /// <summary>
    /// 门禁子事件类型（ISAPI 的 <c>subEventType</c>，majorEventType=5）。
    /// 常见值：1-刷卡开锁、6-未分配权限、7-无效时段、8-卡号过期、9-无此卡号、21-门锁打开、
    /// 22-门锁关闭、75-人脸开锁、76-人脸认证失败、1024-中心平台开锁
    /// （海康云开放平台《设备本地事件查询》对照表；个别型号语义可能不同，以现场实测为准）。
    /// </summary>
    public string? SubEventType { get; set; }

    /// <summary>设备序列号（EHome/ISUP 报警有值）。</summary>
    public string? SerialNumber { get; set; }

    /// <summary>HTTP 报警 URL（若设备以 HTTP 方式上报）。</summary>
    public string? HttpUrl { get; set; }

    /// <summary>是否属于门禁（AccessControl）相关事件。</summary>
    public bool IsAccessControl { get; set; }

    /// <summary>报文原始文本（JSON 或 XML）。</summary>
    public string? Payload { get; set; }

    /// <summary>报文格式：json / xml / struct / binary。</summary>
    public string PayloadFormat { get; set; } = "json";

    /// <summary>报警附带图片的文件名列表（平台落盘的抓拍图，配合 <c>GET /api/events/pictures/{文件名}</c>）。</summary>
    public List<string>? PictureFileNames { get; set; }

    /// <summary>
    /// 报警报文自带的外部图片直链（SDK 报警回调的 <c>Params.PicDataUrl</c> 展开后的完整 URL）。
    /// 图片服务器可直接访问，<b>不需要</b>平台下载（与 ISAPI Digest 的 <c>pictureURL</c> 不同）；
    /// 入库后随事件返回，前端可直接用作 <c>&lt;img src&gt;</c>。
    /// </summary>
    public string? PictureUrl { get; set; }

    /// <summary>异常回调时的错误码。</summary>
    public uint? ErrorCode { get; set; }

    /// <summary>
    /// 入库时是否跳过去重：手工补录 <c>force=true</c> 时不去重（允许写入重复行）；
    /// 设备报警回调、HTTP 推送、历史对账补漏都按 <c>deviceId:serialNo:事件时间</c> 幂等去重
    /// （见 <c>AcsEventSpool.BuildDedupKey</c>）。
    /// </summary>
    public bool SkipDedup { get; set; }

    /// <summary>报文长度（字节）。</summary>
    public int PayloadBytes { get; set; }
}

/// <summary>事件查询条件。</summary>
public sealed class AlarmEventQuery
{
    /// <summary>按设备 ID 过滤（模糊匹配 szDeviceID / 序列号）。</summary>
    public string? DeviceId { get; set; }

    /// <summary>只返回门禁相关事件。</summary>
    public bool? AccessControlOnly { get; set; }

    /// <summary>按来源过滤：IsupJson / Isapi / EHome / Exception。</summary>
    public string? Source { get; set; }

    /// <summary>按业务事件类型模糊匹配。</summary>
    public string? EventType { get; set; }

    /// <summary>只返回该序号之后的事件（增量拉取）。</summary>
    public long? AfterSequence { get; set; }

    /// <summary>起始时间（含）。</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>结束时间（含）。</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>是否在 Payload 中做关键字匹配。</summary>
    public string? Keyword { get; set; }

    /// <summary>最多返回条数（1~1000），默认 100。</summary>
    public int Limit { get; set; } = 100;

    /// <summary>是否包含 Payload 原文（关闭可显著减小响应体）。</summary>
    public bool IncludePayload { get; set; } = true;
}

/// <summary>AMS 运行状态。</summary>
public sealed class AmsStatus
{
    public bool Enabled { get; set; }

    public bool Initialized { get; set; }

    public string? NativeDll { get; set; }

    public string BuildVersion { get; set; } = string.Empty;

    public int[] ListenHandles { get; set; } = Array.Empty<int>();

    public string ListenAddress { get; set; } = string.Empty;

    /// <summary>已接收事件总数（含被淘汰的）。</summary>
    public long TotalReceived { get; set; }

    /// <summary>当前缓冲区内事件条数。</summary>
    public int Buffered { get; set; }

    /// <summary>其中门禁事件条数。</summary>
    public int AccessControlEvents { get; set; }

    /// <summary>已成功下发"开启报警上报"的设备数。</summary>
    public int DevicesPushEnabled { get; set; }

    public DateTimeOffset? LastEventAt { get; set; }
}

/// <summary>单台设备的报警上报配置请求。</summary>
public sealed class StartAlarmRequest
{
    /// <summary>报警服务器地址。留空则使用配置中的 AMS:AlarmServerAddress，再回退到监听地址。</summary>
    public string? ServerAddress { get; set; }

    /// <summary>报警服务器 TCP 端口，0 表示使用 AMS 监听端口。</summary>
    public ushort ServerPort { get; set; }

    /// <summary>报警心跳间隔（秒），有效值 [5-3600]。0 表示使用配置值。</summary>
    public uint KeepAliveSeconds { get; set; }

    /// <summary>报警订阅信息（JSON 字符串），可空。</summary>
    [JsonIgnore]
    public string? SubscribeInfoJson { get; set; }
}
