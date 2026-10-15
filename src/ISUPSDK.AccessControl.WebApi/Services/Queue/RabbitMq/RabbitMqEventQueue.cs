using System.Text;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue.RabbitMq;

/// <summary>
/// RabbitMQ（AMQP 0-9-1）后端：<c>EventQueue:Provider=RabbitMq</c>。
///
/// 实现要点：
/// <list type="bullet">
/// <item>消息体为 UTF-8 JSON，<c>DeliveryMode=Persistent</c> + durable 队列保证 broker 重启不丢；</item>
/// <item>消费用 <c>basic.qos</c> 预取 + 手动 ack：处理成功 ack，失败则 ack 旧消息并延迟重投新副本（避免热循环），
/// 超过上限进死信（<c>basic.nack(requeue=false)</c>，可选 DLX 队列 + 死信文件）；</item>
/// <item>连接/信道断开由本类自行重连（关闭了客户端的自动恢复，便于统计与观测）。</item>
/// </list>
/// </summary>
public sealed class RabbitMqEventQueue : EventQueueBase
{
    private readonly AcsEventQueueOptions.RabbitMqOptions _rabbit;
    private readonly int _maxBodyBytes;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private readonly SemaphoreSlim _ackLock = new(1, 1);

    private IConnection? _connection;
    private IChannel? _publishChannel;
    private IChannel? _consumeChannel;
    private TaskCompletionSource<bool>? _disconnectSignal;

    public RabbitMqEventQueue(IOptions<AcsEventQueueOptions> options, ILogger<RabbitMqEventQueue> logger)
        : base(options.Value, logger)
    {
        _rabbit = options.Value.RabbitMq;
        _maxBodyBytes = Math.Clamp(options.Value.RabbitMq.MaxMessageBytes, 64 * 1024, 64 * 1024 * 1024);
    }

    /// <inheritdoc />
    public override string ProviderName => nameof(AcsEventQueueProvider.RabbitMq);

    /// <summary>死信队列名（启用 DLX 时使用）。</summary>
    private string DeadLetterQueueName => $"{QueueName}.dead";

    /// <inheritdoc />
    protected override async Task ConnectCoreAsync(CancellationToken ct)
        => await EnsureConnectedAsync(ct).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async Task SendCoreAsync(AcsQueueEnvelope envelope, CancellationToken ct)
    {
        var body = AcsQueueSerialization.Serialize(envelope);

        if (body.Length > _maxBodyBytes)
        {
            Logger.LogWarning(
                "事件 {DedupKey} 的队列消息 {Bytes} 字节超过 RabbitMQ 配置上限 {Limit} 字节，改为进程内直通消费。",
                envelope.DedupKey,
                body.Length,
                _maxBodyBytes);

            await BufferMessageAsync(new AcsQueueMessage { Envelope = envelope, NativeId = "oversized" }, ct).ConfigureAwait(false);
            return;
        }

        var channel = _publishChannel ?? throw new InvalidOperationException("RabbitMQ 信道尚未就绪，请先调用 StartAsync。");

        var properties = new BasicProperties
        {
            ContentType = "application/json",
            ContentEncoding = "utf-8",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = envelope.MessageId,
            AppId = envelope.Producer,
            Type = "acs-event",
            Timestamp = new AmqpTimestamp(envelope.EnqueuedAt.ToUnixTimeSeconds()),
        };

        await _publishLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await channel.BasicPublishAsync(string.Empty, QueueName, mandatory: false, properties, body, ct).ConfigureAwait(false);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    /// <inheritdoc />
    protected override async Task ReceiveLoopAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(Math.Clamp(_rabbit.ReconnectDelaySeconds, 1, 600));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await EnsureConnectedAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                MarkNotReady();
                SetError(ex, "RabbitMQ 连接失败");
                await SafeDelayAsync(delay, ct).ConfigureAwait(false);
                continue;
            }

            var signal = _disconnectSignal;
            if (signal is not null)
            {
                try
                {
                    await signal.Task.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            MarkNotReady();
            Logger.LogWarning("RabbitMQ 连接断开，{Delay} 秒后重连。", delay.TotalSeconds);
            await CleanupConnectionAsync().ConfigureAwait(false);
            await SafeDelayAsync(delay, ct).ConfigureAwait(false);
        }

        Logger.LogInformation("RabbitMQ 事件队列收件循环已退出。");
    }

    /// <inheritdoc />
    protected override async Task CompleteCoreAsync(AcsQueueMessage message, CancellationToken ct)
    {
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await BasicAckAsync(deliveryTag).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async Task RetryCoreAsync(AcsQueueMessage message, AcsQueueEnvelope next, int delayMilliseconds, CancellationToken ct)
    {
        // 先 ack 旧消息（否则会一直占着 prefetch 配额），再让"延迟 + 重发"在后台执行，
        // 避免写库故障时逐条等待把消费循环拖死。
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await BasicAckAsync(deliveryTag).ConfigureAwait(false);
        }

        if (delayMilliseconds > 0)
        {
            ScheduleResend(next, delayMilliseconds, ct);
            return;
        }

        await SendCoreAsync(next, ct).ConfigureAwait(false);
        CountPublished();
    }

    /// <inheritdoc />
    protected override async Task DeadLetterCoreAsync(AcsQueueMessage message, string reason, CancellationToken ct)
    {
        // requeue=false：消息离开队列（若启用了 DLX 则进入死信队列），避免堵塞正常消息。
        if (TryGetDeliveryTag(message, out var deliveryTag))
        {
            await BasicNackAsync(deliveryTag, requeue: false).ConfigureAwait(false);
        }

        await base.DeadLetterCoreAsync(message, reason, ct).ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask DisposeCoreAsync()
        => await CleanupConnectionAsync().ConfigureAwait(false);

    private static bool TryGetDeliveryTag(AcsQueueMessage message, out ulong deliveryTag)
        => ulong.TryParse(message.NativeId, out deliveryTag);

    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true } && _consumeChannel is { IsOpen: true } && _publishChannel is { IsOpen: true })
        {
            MarkReady();
            return;
        }

        await CleanupConnectionAsync().ConfigureAwait(false);

        var factory = new ConnectionFactory
        {
            HostName = _rabbit.HostName,
            Port = _rabbit.Port,
            VirtualHost = string.IsNullOrWhiteSpace(_rabbit.VirtualHost) ? "/" : _rabbit.VirtualHost,
            UserName = _rabbit.UserName,
            Password = _rabbit.Password,
            ClientProvidedName = "hikotap-access-control",
            AutomaticRecoveryEnabled = false,
        };

        if (_rabbit.UseTls)
        {
            factory.Ssl = new SslOption { Enabled = true, ServerName = _rabbit.HostName };
        }

        var connection = await factory.CreateConnectionAsync(ct).ConfigureAwait(false);
        var publishChannel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);
        var consumeChannel = await connection.CreateChannelAsync(cancellationToken: ct).ConfigureAwait(false);

        await DeclareTopologyAsync(publishChannel, ct).ConfigureAwait(false);

        await consumeChannel
            .BasicQosAsync(prefetchSize: 0, prefetchCount: (ushort)Math.Clamp(_rabbit.PrefetchCount, (ushort)1, (ushort)1000), global: false, cancellationToken: ct)
            .ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(consumeChannel);
        consumer.ReceivedAsync += OnMessageReceivedAsync;

        await consumeChannel.BasicConsumeAsync(QueueName, autoAck: false, consumer: consumer, cancellationToken: ct).ConfigureAwait(false);

        _disconnectSignal = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
        consumeChannel.ChannelShutdownAsync += OnChannelShutdownAsync;

        _connection = connection;
        _publishChannel = publishChannel;
        _consumeChannel = consumeChannel;

        MarkReady();

        Logger.LogInformation(
            "RabbitMQ 事件队列已就绪：{Host}:{Port}{VHost}，队列={Queue}，预取={Prefetch}，死信={DeadLetter}",
            _rabbit.HostName,
            _rabbit.Port,
            string.IsNullOrWhiteSpace(_rabbit.VirtualHost) ? "/" : _rabbit.VirtualHost,
            QueueName,
            _rabbit.PrefetchCount,
            _rabbit.DeadLetterEnabled ? DeadLetterQueueName : "否");
    }

    private async Task DeclareTopologyAsync(IChannel channel, CancellationToken ct)
    {
        Dictionary<string, object?>? arguments = null;

        if (_rabbit.DeadLetterEnabled)
        {
            var deadLetterExchange = $"{QueueName}.dlx";

            await channel
                .ExchangeDeclareAsync(deadLetterExchange, ExchangeType.Fanout, durable: true, autoDelete: false, arguments: null, cancellationToken: ct)
                .ConfigureAwait(false);

            await channel
                .QueueDeclareAsync(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct)
                .ConfigureAwait(false);

            await channel
                .QueueBindAsync(DeadLetterQueueName, deadLetterExchange, routingKey: string.Empty, arguments: null, cancellationToken: ct)
                .ConfigureAwait(false);

            arguments = new Dictionary<string, object?> { ["x-dead-letter-exchange"] = deadLetterExchange };
        }

        try
        {
            await channel
                .QueueDeclareAsync(QueueName, durable: _rabbit.Durable, exclusive: false, autoDelete: false, arguments: arguments, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (_rabbit.DeadLetterEnabled)
        {
            // 队列已存在且参数不同（如先前没启用 DLX）：降级为不带死信参数再次声明，避免整个链路不可用。
            Logger.LogWarning(ex, "声明带死信的队列失败，改为按无死信参数声明：{Queue}", QueueName);

            await channel
                .QueueDeclareAsync(QueueName, durable: _rabbit.Durable, exclusive: false, autoDelete: false, arguments: null, cancellationToken: ct)
                .ConfigureAwait(false);
        }
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            var body = args.Body.ToArray();
            var envelope = AcsQueueSerialization.Deserialize(body);

            var message = envelope is not null
                ? new AcsQueueMessage { Envelope = envelope, NativeId = args.DeliveryTag.ToString() }
                : BuildInvalidMessage(body, args.DeliveryTag);

            await BufferMessageAsync(message, ShutdownToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // 关闭中
        }
        catch (Exception ex)
        {
            SetError(ex, "RabbitMQ 投递入缓冲失败");

            // 未能进入缓冲：直接 nack 重投，避免消息长期挂在 unacked 状态。
            await BasicNackAsync(args.DeliveryTag, requeue: true).ConfigureAwait(false);
        }
    }

    private AcsQueueMessage BuildInvalidMessage(byte[] body, ulong deliveryTag)
    {
        Logger.LogWarning("RabbitMQ 消息（DeliveryTag={Tag}）无法反序列化，将进入死信。", deliveryTag);

        var preview = Encoding.UTF8.GetString(body, 0, Math.Min(body.Length, 4096));

        return new AcsQueueMessage
        {
            NativeId = deliveryTag.ToString(),
            Envelope = new AcsQueueEnvelope
            {
                DedupKey = $"invalid:{deliveryTag}",
                Producer = "rabbitmq-invalid",
                Event = new IsupAlarmEvent
                {
                    Source = "RabbitMq",
                    PayloadFormat = "json",
                    Payload = preview,
                    PayloadBytes = body.Length,
                    ReceivedAt = DateTimeOffset.UtcNow,
                    DeviceId = string.Empty,
                },
            },
        };
    }

    private async Task BasicAckAsync(ulong deliveryTag)
    {
        var channel = _consumeChannel;
        if (channel is null || !channel.IsOpen)
        {
            return;
        }

        await _ackLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await channel.BasicAckAsync(deliveryTag, multiple: false).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetError(ex, "RabbitMQ basic.ack 失败");
        }
        finally
        {
            _ackLock.Release();
        }
    }

    private async Task BasicNackAsync(ulong deliveryTag, bool requeue)
    {
        var channel = _consumeChannel;
        if (channel is null || !channel.IsOpen)
        {
            return;
        }

        await _ackLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: requeue).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetError(ex, "RabbitMQ basic.nack 失败");
        }
        finally
        {
            _ackLock.Release();
        }
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs args)
    {
        Logger.LogWarning("RabbitMQ 连接已关闭：{Reply}", args.ReplyText);
        _disconnectSignal?.TrySetResult(true);
        return Task.CompletedTask;
    }

    private Task OnChannelShutdownAsync(object sender, ShutdownEventArgs args)
    {
        Logger.LogWarning("RabbitMQ 信道已关闭：{Reply}", args.ReplyText);
        _disconnectSignal?.TrySetResult(true);
        return Task.CompletedTask;
    }

    private async Task CleanupConnectionAsync()
    {
        var connection = _connection;
        var publishChannel = _publishChannel;
        var consumeChannel = _consumeChannel;

        _connection = null;
        _publishChannel = null;
        _consumeChannel = null;

        if (connection is not null)
        {
            connection.ConnectionShutdownAsync -= OnConnectionShutdownAsync;
        }

        if (consumeChannel is not null)
        {
            consumeChannel.ChannelShutdownAsync -= OnChannelShutdownAsync;
        }

        foreach (var channel in new[] { consumeChannel, publishChannel })
        {
            if (channel is null)
            {
                continue;
            }

            try
            {
                await channel.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "关闭 RabbitMQ 信道时出现异常（忽略）。");
            }
        }

        if (connection is not null)
        {
            try
            {
                await connection.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "关闭 RabbitMQ 连接时出现异常（忽略）。");
            }
        }
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
