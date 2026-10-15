using ISUPSDK.AccessControl.WebApi.Models.EventQueue;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue;

/// <summary>
/// 门禁事件队列的统一抽象。
///
/// 三种实现（<c>ISUP:EventQueue:Provider</c> 切换）：
/// <list type="bullet">
/// <item><c>None</c>：<see cref="InProcessEventQueue"/>，进程内内存通道直通（单机部署无需外部 MQ）；</item>
/// <item><c>Msmq</c>：<see cref="Msmq.MsmqEventQueue"/>，Windows 自带消息队列（mqrt.dll）；</item>
/// <item><c>RabbitMq</c>：<see cref="RabbitMq.RabbitMqEventQueue"/>，AMQP broker。</item>
/// </list>
///
/// 消费语义为 <b>at-most-once + 幂等落库 + 落盘兜底</b>：消息在取出后若处理失败会重投（投递次数 +1），
/// 超过上限则进入死信并告警；进程崩溃时在途消息可能丢失，由 <c>AcsEventSpool</c> 的落盘文件重放补齐。
/// </summary>
public interface IEventQueue : IAsyncDisposable
{
    /// <summary>后端名称：None / Msmq / RabbitMq。</summary>
    string ProviderName { get; }

    /// <summary>队列名（MSMQ 为完整路径）。</summary>
    string QueueName { get; }

    /// <summary>是否已就绪（连接可用）。</summary>
    bool IsReady { get; }

    /// <summary>发布缓冲上限。</summary>
    int PendingBufferSize { get; }

    /// <summary>取当前统计快照。</summary>
    AcsQueueStatistics GetStatistics();

    /// <summary>建立连接并启动收件循环；失败返回 false（不抛异常，由调用方退避重试）。</summary>
    Task<bool> StartAsync(CancellationToken ct = default);

    /// <summary>发布一条事件；返回 false 表示本次发布失败（调用方可重试）。</summary>
    Task<bool> PublishAsync(AcsQueueEnvelope envelope, CancellationToken ct = default);

    /// <summary>批量取出消息（最多 <paramref name="maxCount"/> 条，<paramref name="wait"/> 内无消息则返回空）。</summary>
    Task<IReadOnlyList<AcsQueueMessage>> ReceiveBatchAsync(int maxCount, TimeSpan wait, CancellationToken ct = default);

    /// <summary>标记处理完成（消息从队列中移除）。</summary>
    Task CompleteAsync(AcsQueueMessage message, CancellationToken ct = default);

    /// <summary>
    /// 重投一条消息（投递次数 +1，延迟 <paramref name="delayMilliseconds"/> 后重新入队）。
    /// </summary>
    Task RetryAsync(AcsQueueMessage message, int delayMilliseconds, CancellationToken ct = default);

    /// <summary>把消息标记为死信（无法处理，超过重试上限或反序列化失败）。</summary>
    Task DeadLetterAsync(AcsQueueMessage message, string reason, CancellationToken ct = default);
}
