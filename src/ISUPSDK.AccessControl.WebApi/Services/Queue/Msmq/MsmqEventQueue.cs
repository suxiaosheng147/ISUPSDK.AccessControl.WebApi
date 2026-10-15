using System.Text;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue.Msmq;

/// <summary>
/// Windows 自带消息队列（MSMQ）后端：<c>EventQueue:Provider=Msmq</c>。
///
/// 实现要点：
/// <list type="bullet">
/// <item>队列路径默认 <c>.\Private$\&lt;QueueName&gt;</c>，不存在时由服务自动创建（<c>MQCreateQueue</c>）；</item>
/// <item>消息体是 UTF-8 JSON，属性用 <c>PROPID_M_LABEL</c> 打标签，便于在计算机管理里辨认；</item>
/// <item>接收使用 <c>MQ_ACTION_RECEIVE</c> + 固定超时轮询，处理失败由基类重投（投递次数 +1），超限进死信文件；</item>
/// <item>单条消息超过 MSMQ 上限（4MB）时改为进程内直通，保证事件仍能落库。</item>
/// </list>
/// </summary>
public sealed class MsmqEventQueue : EventQueueBase
{
    private readonly string _path;
    private readonly bool _transactional;
    private readonly int _maxBodyBytes;
    private readonly int _reconnectDelaySeconds;
    private readonly int _queueCheckSeconds;

    private MsmqQueue? _queue;

    public MsmqEventQueue(IOptions<AcsEventQueueOptions> options, ILogger<MsmqEventQueue> logger)
        : base(options.Value, logger)
    {
        _path = options.Value.ResolveMsmqQueuePath();
        _transactional = options.Value.Msmq.Transactional;
        _maxBodyBytes = Math.Clamp(options.Value.Msmq.MaxMessageBytes, 64 * 1024, 4 * 1024 * 1024);
        _reconnectDelaySeconds = Math.Clamp(options.Value.Msmq.ReconnectDelaySeconds, 1, 600);
        _queueCheckSeconds = Math.Clamp(options.Value.Msmq.QueueCheckSeconds, 5, 3600);
        QueueName = _path;
    }

    /// <inheritdoc />
    public override string ProviderName => nameof(AcsEventQueueProvider.Msmq);

    /// <inheritdoc />
    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        Reconnect();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override Task SendCoreAsync(AcsQueueEnvelope envelope, CancellationToken ct)
    {
        var body = AcsQueueSerialization.Serialize(envelope);

        if (body.Length > _maxBodyBytes)
        {
            Logger.LogWarning(
                "事件 {DedupKey} 的队列消息 {Bytes} 字节超过 MSMQ 单消息上限 {Limit} 字节，改为进程内直通消费。",
                envelope.DedupKey,
                body.Length,
                _maxBodyBytes);

            return BufferMessageAsync(new AcsQueueMessage { Envelope = envelope, NativeId = "oversized" }, ct);
        }

        var queue = Volatile.Read(ref _queue) ?? throw new MsmqException(
            MsmqNative.MQ_ERROR_INVALID_PARAMETER,
            "MSMQ 队列尚未打开，请先调用 StartAsync。");

        try
        {
            queue.Send(body, $"{Options.Msmq.LabelPrefix}{envelope.MessageId}");
        }
        catch (MsmqException ex)
        {
            // 发送失败通常意味着队列已不可用（队列被删除 / MSMQ 服务重启）：
            // 标记失联并丢弃句柄，由收件循环重建队列。
            Logger.LogWarning(ex, "MSMQ 发送失败，将重建队列连接：{Path}", _path);
            InvalidateQueue(queue);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(Options.Msmq.ReceiveTimeoutMs, 100, 30000));
        var reconnectDelay = TimeSpan.FromSeconds(_reconnectDelaySeconds);
        var nextQueueCheckAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(_queueCheckSeconds, 10));

        while (!ct.IsCancellationRequested)
        {
            var queue = Volatile.Read(ref _queue);

            if (queue is null || !queue.IsOpen)
            {
                try
                {
                    Reconnect();
                }
                catch (Exception ex)
                {
                    MarkNotReady();
                    SetError(ex, "MSMQ 重连失败");
                    await SafeDelayAsync(reconnectDelay, ct).ConfigureAwait(false);
                }

                continue;
            }

            // 队列存活巡检：队列被删除后 MSMQ 句柄不会立刻失效（接收只是持续超时），
            // 只靠发送失败触发重建的话，长期无事件期间连接会一直"假就绪"。
            if (DateTimeOffset.UtcNow >= nextQueueCheckAt)
            {
                nextQueueCheckAt = DateTimeOffset.UtcNow.AddSeconds(_queueCheckSeconds);

                if (!MsmqQueue.Exists(_path))
                {
                    Logger.LogWarning("MSMQ 队列已不存在（{Path}），立即重建。", _path);
                    InvalidateQueue(queue);
                    continue;
                }
            }

            // MQReceiveMessage 是同步阻塞调用：放到线程池执行，避免阻塞本循环（取消靠超时轮询生效）。
            bool received;
            byte[] body;
            ulong nativeId;

            try
            {
                (received, body, nativeId) = await Task.Run(() =>
                {
                    var ok = queue.TryReceive(timeout, _maxBodyBytes, out var buffer, out var lookupId);
                    return (ok, buffer, lookupId);
                }, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 句柄失效/队列被删：丢弃句柄，下一轮重建。
                SetError(ex, "MSMQ 收取消息失败");
                InvalidateQueue(queue);
                await SafeDelayAsync(reconnectDelay, ct).ConfigureAwait(false);
                continue;
            }

            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (!received)
            {
                continue;
            }

            var message = BuildMessage(body, nativeId);

            try
            {
                await BufferMessageAsync(message, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                SetError(ex, "MSMQ 消息入缓冲失败");
            }
        }

        Logger.LogInformation("MSMQ 事件队列收件循环已退出。");
    }

    /// <inheritdoc />
    protected override Task CompleteCoreAsync(AcsQueueMessage message, CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    protected override ValueTask DisposeCoreAsync()
    {
        var queue = Interlocked.Exchange(ref _queue, null);
        queue?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>把当前队列标记为失联并释放句柄（收件循环下一轮会重建队列）。</summary>
    private void InvalidateQueue(MsmqQueue queue)
    {
        MarkNotReady();
        Interlocked.CompareExchange(ref _queue, null, queue);

        try
        {
            queue.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "释放 MSMQ 队列句柄时出现异常（忽略）。");
        }
    }

    private void Reconnect()
    {
        var queue = new MsmqQueue(_path, _transactional);
        queue.EnsureQueue(Options.Msmq.CreateIfMissing);
        queue.Open();

        var previous = Interlocked.Exchange(ref _queue, queue);
        previous?.Dispose();

        MarkReady();

        Logger.LogInformation(
            "MSMQ 事件队列已就绪：路径={Path}，事务性={Transactional}，接收超时={Timeout}ms",
            queue.FormatName,
            _transactional,
            Options.Msmq.ReceiveTimeoutMs);
    }

    private AcsQueueMessage BuildMessage(byte[] body, ulong lookupId)
    {
        var nativeId = lookupId.ToString();

        var envelope = AcsQueueSerialization.Deserialize(body);
        if (envelope is not null)
        {
            return new AcsQueueMessage { Envelope = envelope, NativeId = nativeId };
        }

        // 无法反序列化：构造占位消息（保留报文片段）交给死信处理，避免坏消息卡住队列。
        Logger.LogWarning("MSMQ 消息（LookupId={LookupId}）无法反序列化，将进入死信。", lookupId);

        var preview = Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 4096));

        return new AcsQueueMessage
        {
            NativeId = nativeId,
            Envelope = new AcsQueueEnvelope
            {
                DedupKey = $"invalid:{nativeId}",
                Producer = "msmq-invalid",
                Event = new IsupAlarmEvent
                {
                    Source = "Msmq",
                    PayloadFormat = "json",
                    Payload = preview,
                    PayloadBytes = body.Length,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    DeviceId = string.Empty,
                },
            },
        };
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 关闭中
        }
    }
}
