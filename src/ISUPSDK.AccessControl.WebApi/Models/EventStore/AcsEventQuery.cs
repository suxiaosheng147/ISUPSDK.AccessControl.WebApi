namespace ISUPSDK.AccessControl.WebApi.Models.EventStore;

/// <summary>已入库事件的查询条件（<c>GET /api/events</c>）。</summary>
public sealed class AcsEventQuery
{
    /// <summary>按设备 ID 模糊匹配。</summary>
    public string? DeviceId { get; set; }

    /// <summary>只返回门禁相关事件。</summary>
    public bool? AccessControlOnly { get; set; }

    /// <summary>按业务事件类型模糊匹配。</summary>
    public string? EventType { get; set; }

    /// <summary>按事件主类型精确匹配（1-报警、2-异常、3-操作、5-门禁事件）。</summary>
    public int? Major { get; set; }

    /// <summary>按事件次类型精确匹配（1-刷卡开锁、75-人脸开锁、76-人脸认证失败、21/22-门锁开/关 …）。</summary>
    public int? Minor { get; set; }

    /// <summary>按来源精确匹配（HttpPush / IsupJson / Isapi / EHome / Reconcile / Manual / Exception）。</summary>
    public string? Source { get; set; }

    /// <summary>按认证方式精确匹配（card / face / fingerprint …）。</summary>
    public string? VerifyMode { get; set; }

    /// <summary>按工号精确匹配。</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>按卡号精确匹配。</summary>
    public string? CardNo { get; set; }

    /// <summary>事件时间下界（含）。</summary>
    public DateTimeOffset? From { get; set; }

    /// <summary>事件时间上界（含）。</summary>
    public DateTimeOffset? To { get; set; }

    /// <summary>报文原文关键字模糊匹配（数据量大时较慢）。</summary>
    public string? Keyword { get; set; }

    /// <summary>最多返回条数（1~1000），默认 100。</summary>
    public int Limit { get; set; } = 100;

    /// <summary>跳过的条数（分页）。</summary>
    public int Offset { get; set; }

    /// <summary>是否返回报文原文（关闭可显著减小响应体）。</summary>
    public bool IncludePayload { get; set; } = true;

    /// <summary>是否按事件时间升序（默认降序，最新在前）。</summary>
    public bool OrderAscending { get; set; }
}

/// <summary>
/// 一次批量写入的结果。
/// <para>
/// <see cref="InsertedKeys"/> 给出本次<b>真正新增入库</b>的 <c>DedupKey</c>（去重键）集合，
/// 消费端据此判断"这条消息是否需要转发到下游队列"——因重复被跳过的消息不再二次转发。
/// </para>
/// </summary>
public sealed record AcsEventWriteResult(
    int Inserted,
    int Skipped,
    bool Success,
    string? Error,
    IReadOnlySet<string>? InsertedKeys = null);

/// <summary>一次"落盘文件重放"的结果。</summary>
public sealed record AcsEventReplayResult
{
    /// <summary>扫描的文件数。</summary>
    public int Files { get; init; }

    /// <summary>读到的记录数。</summary>
    public long Records { get; init; }

    /// <summary>新写入的行数。</summary>
    public long Inserted { get; init; }

    /// <summary>因重复跳过的行数。</summary>
    public long Skipped { get; init; }

    /// <summary>解析/写入出错的记录数。</summary>
    public long Errors { get; init; }

    /// <summary>耗时（毫秒）。</summary>
    public long ElapsedMs { get; init; }
}

/// <summary>事件入库统计（<c>/health</c> 与 <c>GET /api/events/status</c> 使用）。</summary>
public sealed record AcsEventStoreStatistics
{
    /// <summary>是否启用落库。</summary>
    public bool Enabled { get; init; }

    /// <summary>数据库类型：Sqlite / MySql / SqlServer。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>表名。</summary>
    public string TableName { get; init; } = string.Empty;

    /// <summary>本次进程启动后写入的行数。</summary>
    public long Inserted { get; init; }

    /// <summary>因重复（DedupHash 已存在）跳过的行数。</summary>
    public long Duplicates { get; init; }

    /// <summary>写入失败的行数。</summary>
    public long Failed { get; init; }

    /// <summary>库中总行数（查询失败时为 null）。</summary>
    public long? TotalRows { get; init; }

    /// <summary>最近一次成功写入时间。</summary>
    public DateTimeOffset? LastStoredAt { get; init; }

    /// <summary>最近一次错误。</summary>
    public string? LastError { get; init; }

    /// <summary>最近一次错误时间。</summary>
    public DateTimeOffset? LastErrorAt { get; init; }
}
