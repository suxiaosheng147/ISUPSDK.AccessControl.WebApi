using ISUPSDK.AccessControl.WebApi.Models;

namespace ISUPSDK.AccessControl.WebApi.Models.EventQueue;

/// <summary>
/// 投递到消息队列的一条门禁事件（队列消息体的强类型表示）。
///
/// 自带 <see cref="DedupKey"/>（与内存去重/落盘文件同一算法），因此消费者即使收到重复投递，
/// 也能靠数据库唯一索引做到"最终只落一行"。
/// </summary>
public sealed record AcsQueueEnvelope
{
    /// <summary>消息 ID（每次发布生成，重投时保留原值便于追踪）。</summary>
    public string MessageId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>去重键（<c>deviceId:serialNo</c>，无序号时为报文哈希）。</summary>
    public string DedupKey { get; init; } = string.Empty;

    /// <summary>首次入队时间（UTC）。</summary>
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>投递次数（首次为 1，每次重投 +1）。</summary>
    public int Attempt { get; init; } = 1;

    /// <summary>生产者标识（用于区分同一队列的多个来源进程）。</summary>
    public string Producer { get; init; } = "acs";

    /// <summary>事件本体。</summary>
    public IsupAlarmEvent Event { get; init; } = new();

    /// <summary>是否跳过去重写入（true 时去重哈希唯一化，同一条事件也允许重复落库）。</summary>
    public bool SkipDedup { get; init; }

    /// <summary>返回一个投递次数 +1 的副本（重投用）。</summary>
    public AcsQueueEnvelope NextAttempt() => this with { Attempt = Attempt + 1 };
}

/// <summary>
/// 队列中取出的一条消息（含队列原始标识，供完成/重投/死信时回指）。
/// </summary>
public sealed class AcsQueueMessage
{
    /// <summary>消息内容。</summary>
    public required AcsQueueEnvelope Envelope { get; init; }

    /// <summary>消息 ID。</summary>
    public string MessageId => Envelope.MessageId;

    /// <summary>队列原生标识（MSMQ 的 LookupId 或 RabbitMQ 的 delivery tag），仅用于日志。</summary>
    public string? NativeId { get; init; }

    /// <summary>取出时间（UTC）。</summary>
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>队列运行统计（<c>/health</c> 与 <c>/api/events/queue</c> 使用）。</summary>
public sealed record AcsQueueStatistics
{
    /// <summary>队列后端：None / Msmq / RabbitMq。</summary>
    public string Provider { get; init; } = string.Empty;

    /// <summary>队列名（或 MSMQ 路径）。</summary>
    public string QueueName { get; init; } = string.Empty;

    /// <summary>是否已连接/已就绪。</summary>
    public bool Ready { get; init; }

    /// <summary>已成功投递的消息数。</summary>
    public long Published { get; init; }

    /// <summary>投递失败次数。</summary>
    public long PublishFailures { get; init; }

    /// <summary>已取出（交给消费者）的消息数。</summary>
    public long Received { get; init; }

    /// <summary>已确认处理完成的消息数。</summary>
    public long Completed { get; init; }

    /// <summary>重投的条数。</summary>
    public long Retried { get; init; }

    /// <summary>进入死信/被丢弃的条数。</summary>
    public long DeadLettered { get; init; }

    /// <summary>已取出但尚未确认的消息数（在途）。</summary>
    public int InFlight { get; init; }

    /// <summary>等待发布的本地缓冲条数。</summary>
    public int Backlog { get; init; }

    /// <summary>最近一次错误。</summary>
    public string? LastError { get; init; }

    /// <summary>最近一次错误时间。</summary>
    public DateTimeOffset? LastErrorAt { get; init; }

    /// <summary>最近一次成功投递时间。</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>最近一次取出消息时间。</summary>
    public DateTimeOffset? LastReceivedAt { get; init; }
}
