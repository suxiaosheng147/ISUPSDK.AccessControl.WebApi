using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue;

/// <summary>
/// 队列实现的公共骨架：统一的统计、取件缓冲与生命周期。
///
/// 子类只需关注"怎么连、怎么发、怎么收"（<see cref="ConnectCoreAsync"/>、
/// <see cref="SendCoreAsync"/>、<see cref="ReceiveLoopAsync"/>）以及
/// 完成/重投/死信三个原生动作。
/// </summary>
public abstract class EventQueueBase : IEventQueue
{
    private readonly Channel<AcsQueueMessage> _buffer;
    private readonly Dictionary<string, AcsQueueMessage> _inFlight = new(StringComparer.Ordinal);
    private readonly object _inFlightSync = new();
    private readonly SemaphoreSlim _deadLetterLock = new(1, 1);
    private readonly ILogger _logger;

    private readonly CancellationTokenSource _cts = new();
    private Task? _receiveLoop;
    private int _ready;
    private int _buffered;

    private long _published;
    private long _publishFailures;
    private long _received;
    private long _completed;
    private long _retried;
    private long _deadLettered;

    private string? _lastError;
    private DateTimeOffset? _lastErrorAt;
    private DateTimeOffset? _lastPublishedAt;
    private DateTimeOffset? _lastReceivedAt;

    protected EventQueueBase(AcsEventQueueOptions options, ILogger logger)
    {
        Options = options;
        QueueName = options.QueueName;
        _logger = logger;

        // 取件缓冲：接收线程/回调往里写，消费者批量拉取。有界 + 等待写入 => 对队列形成背压。
        _buffer = Channel.CreateBounded<AcsQueueMessage>(new BoundedChannelOptions(Math.Max(options.PendingBufferSize, 100))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>队列配置。</summary>
    protected AcsEventQueueOptions Options { get; }

    /// <summary>日志。</summary>
    protected ILogger Logger => _logger;

    /// <summary>关闭令牌（收件回调等长时间等待场景使用）。</summary>
    protected CancellationToken ShutdownToken => _cts.Token;

    /// <summary>累加"已发布"计数（覆写重投逻辑时使用）。</summary>
    protected void CountPublished() => Interlocked.Increment(ref _published);

    /// <inheritdoc />
    public abstract string ProviderName { get; }

    /// <inheritdoc />
    public string QueueName { get; protected set; }

    /// <inheritdoc />
    public bool IsReady => Volatile.Read(ref _ready) == 1;

    /// <summary>取件缓冲上限。</summary>
    public int PendingBufferSize => Math.Max(Options.PendingBufferSize, 100);

    /// <summary>缓冲中待消费的消息数（自行计数，避免依赖 Channel 的 Count 实现）。</summary>
    protected int BufferedCount => Math.Max(Volatile.Read(ref _buffered), 0);

    /// <summary>已取出但未确认的消息数。</summary>
    protected int InFlightCount
    {
        get
        {
            lock (_inFlightSync)
            {
                return _inFlight.Count;
            }
        }
    }

    /// <inheritdoc />
    public AcsQueueStatistics GetStatistics() => new()
    {
        Provider = ProviderName,
        QueueName = QueueName,
        Ready = IsReady,
        Published = Interlocked.Read(ref _published),
        PublishFailures = Interlocked.Read(ref _publishFailures),
        Received = Interlocked.Read(ref _received),
        Completed = Interlocked.Read(ref _completed),
        Retried = Interlocked.Read(ref _retried),
        DeadLettered = Interlocked.Read(ref _deadLettered),
        InFlight = InFlightCount,
        Backlog = BufferedCount,
        LastError = _lastError,
        LastErrorAt = _lastErrorAt,
        LastPublishedAt = _lastPublishedAt,
        LastReceivedAt = _lastReceivedAt,
    };

    /// <inheritdoc />
    public async Task<bool> StartAsync(CancellationToken ct = default)
    {
        if (Interlocked.CompareExchange(ref _ready, 1, 0) == 1)
        {
            return true;
        }

        try
        {
            await ConnectCoreAsync(ct).ConfigureAwait(false);
            _receiveLoop = Task.Run(() => ReceiveLoopAsync(_cts.Token), CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            SetError(ex, "连接队列失败");
            Volatile.Write(ref _ready, 0);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> PublishAsync(AcsQueueEnvelope envelope, CancellationToken ct = default)
    {
        if (!IsReady)
        {
            Interlocked.Increment(ref _publishFailures);
            return false;
        }

        try
        {
            await SendCoreAsync(envelope, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _published);
            _lastPublishedAt = DateTimeOffset.UtcNow;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _publishFailures);
            SetError(ex, "发布消息失败");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AcsQueueMessage>> ReceiveBatchAsync(int maxCount, TimeSpan wait, CancellationToken ct = default)
    {
        var list = new List<AcsQueueMessage>(Math.Clamp(maxCount, 1, 1000));

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(wait <= TimeSpan.Zero ? TimeSpan.FromMilliseconds(200) : wait);

        try
        {
            while (list.Count < maxCount)
            {
                if (!await _buffer.Reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
                {
                    break;
                }

                while (list.Count < maxCount && _buffer.Reader.TryRead(out var message))
                {
                    Interlocked.Decrement(ref _buffered);
                    list.Add(message);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 等待超时（或调用方取消）：返回已取到的消息。
        }

        return list;
    }

    /// <inheritdoc />
    public async Task CompleteAsync(AcsQueueMessage message, CancellationToken ct = default)
    {
        RemoveInFlight(message);
        try
        {
            await CompleteCoreAsync(message, ct).ConfigureAwait(false);
            Interlocked.Increment(ref _completed);
        }
        catch (Exception ex)
        {
            SetError(ex, "确认消息完成失败");
        }
    }

    /// <inheritdoc />
    public async Task RetryAsync(AcsQueueMessage message, int delayMilliseconds, CancellationToken ct = default)
    {
        RemoveInFlight(message);
        Interlocked.Increment(ref _retried);

        var next = message.Envelope.NextAttempt();
        var delay = Math.Max(delayMilliseconds, 0);

        try
        {
            await RetryCoreAsync(message, next, delay, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetError(ex, "重投消息失败");
        }
    }

    /// <inheritdoc />
    public async Task DeadLetterAsync(AcsQueueMessage message, string reason, CancellationToken ct = default)
    {
        RemoveInFlight(message);
        Interlocked.Increment(ref _deadLettered);

        try
        {
            await DeadLetterCoreAsync(message, reason, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            SetError(ex, "标记死信失败");
        }
    }

    /// <summary>关闭连接（<c>DisposeAsync</c> 或取消时调用）。</summary>
    protected virtual ValueTask DisposeCoreAsync()
        => ValueTask.CompletedTask;

    /// <summary>建立连接/创建队列（失败抛异常由 <see cref="StartAsync"/> 捕获）。</summary>
    protected abstract Task ConnectCoreAsync(CancellationToken ct);

    /// <summary>发送一条消息（失败抛异常）。</summary>
    protected abstract Task SendCoreAsync(AcsQueueEnvelope envelope, CancellationToken ct);

    /// <summary>
    /// 收件循环：把队列里的消息搬进内存缓冲（<see cref="BufferMessageAsync"/>）。
    /// 该循环需要自己处理断线重连，直到 <paramref name="ct"/> 取消。
    /// </summary>
    protected abstract Task ReceiveLoopAsync(CancellationToken ct);

    /// <summary>原生"确认完成"动作。</summary>
    protected virtual Task CompleteCoreAsync(AcsQueueMessage message, CancellationToken ct) => Task.CompletedTask;

    /// <summary>
    /// 原生"重投"动作，默认实现为重新发送。
    ///
    /// 有延迟时把"等待 + 重发"放到后台执行，避免写库故障时逐条等待把消费循环拖死
    /// （进程退出时未重投的消息由落盘文件兜底）。
    /// </summary>
    protected virtual async Task RetryCoreAsync(AcsQueueMessage message, AcsQueueEnvelope next, int delayMilliseconds, CancellationToken ct)
    {
        if (delayMilliseconds > 0)
        {
            ScheduleResend(next, delayMilliseconds, ct);
            return;
        }

        await SendCoreAsync(next, ct).ConfigureAwait(false);
        CountPublished();
    }

    /// <summary>安排一次延迟重投（后台执行，立即返回）。</summary>
    protected void ScheduleResend(AcsQueueEnvelope envelope, int delayMilliseconds, CancellationToken ct)
        => _ = ResendAfterDelayAsync(envelope, delayMilliseconds, ct);

    private async Task ResendAfterDelayAsync(AcsQueueEnvelope envelope, int delayMilliseconds, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMilliseconds, ct).ConfigureAwait(false);
            await SendCoreAsync(envelope, ct).ConfigureAwait(false);
            CountPublished();
        }
        catch (OperationCanceledException)
        {
            // 关闭中：消息仍可经落盘文件重放补齐。
        }
        catch (Exception ex)
        {
            SetError(ex, "延迟重投失败");
        }
    }

    /// <summary>
    /// 原生"死信"动作，默认实现为写死信文件（<c>EventQueue:DeadLetterFile</c>）并记警告。
    /// 三种队列后端共用该语义：消息不会在队列里反复重投。
    /// </summary>
    protected virtual async Task DeadLetterCoreAsync(AcsQueueMessage message, string reason, CancellationToken ct)
    {
        _logger.LogWarning(
            "队列消息 {MessageId} 进入死信：{Reason}（投递次数={Attempt}，设备={DeviceId}，来源={Source}）",
            message.MessageId,
            reason,
            message.Envelope.Attempt,
            message.Envelope.Event.DeviceId,
            message.Envelope.Event.Source);

        SetErrorText($"消息 {message.MessageId} 进入死信：{reason}");

        try
        {
            var record = new AcsDeadLetterRecord(
                message.MessageId,
                message.NativeId,
                reason,
                DateTimeOffset.UtcNow,
                message.Envelope,
                message.Envelope.Attempt);

            var path = Path.GetFullPath(Options.DeadLetterFile);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = JsonSerializer.Serialize(record, AcsQueueSerialization.Options) + Environment.NewLine;

            await _deadLetterLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await File.AppendAllTextAsync(path, line, new UTF8Encoding(false), ct).ConfigureAwait(false);
            }
            finally
            {
                _deadLetterLock.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // 关闭中，忽略
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "写入队列死信文件失败（{File}）。", Options.DeadLetterFile);
        }
    }

    /// <summary>把消息放入内存缓冲（背压：缓冲满时等待）。</summary>
    protected async Task BufferMessageAsync(AcsQueueMessage message, CancellationToken ct)
    {
        AddInFlight(message);
        Interlocked.Increment(ref _received);
        _lastReceivedAt = DateTimeOffset.UtcNow;
        Interlocked.Increment(ref _buffered);

        try
        {
            await _buffer.Writer.WriteAsync(message, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Interlocked.Decrement(ref _buffered);
            RemoveInFlight(message);
            throw;
        }
    }

    /// <summary>记录一条错误（线程安全，保留最近一条）。</summary>
    protected void SetError(Exception ex, string context)
        => SetErrorText($"{context}：{ex.GetType().Name}: {ex.Message}");

    /// <summary>记录一条错误文本。</summary>
    protected void SetErrorText(string message)
    {
        _lastError = message;
        _lastErrorAt = DateTimeOffset.UtcNow;
    }

    /// <summary>清除就绪标记（断线时调用，<see cref="ReceiveLoopAsync"/> 重连成功后再置位）。</summary>
    protected void MarkNotReady() => Volatile.Write(ref _ready, 0);

    /// <summary>置位就绪标记。</summary>
    protected void MarkReady() => Volatile.Write(ref _ready, 1);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // 已释放
        }

        var receiveLoopStopped = true;

        if (_receiveLoop is not null)
        {
            try
            {
                await _receiveLoop.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                // 收件循环未在 5 秒内退出（阻塞在原生调用上）：此时不释放 CTS，避免它对已释放对象取 Token。
                receiveLoopStopped = false;
                _logger.LogWarning("队列 {Provider} 的收件循环未在 5 秒内退出，跳过资源释放。", ProviderName);
            }
            catch (Exception)
            {
                // 忽略关闭期异常
            }
        }

        try
        {
            await DisposeCoreAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // 忽略关闭期异常
        }

        if (receiveLoopStopped)
        {
            _cts.Dispose();
        }

        GC.SuppressFinalize(this);
    }

    private void AddInFlight(AcsQueueMessage message)
    {
        lock (_inFlightSync)
        {
            _inFlight[message.MessageId] = message;
        }
    }

    private void RemoveInFlight(AcsQueueMessage message)
    {
        lock (_inFlightSync)
        {
            _inFlight.Remove(message.MessageId);
        }
    }
}
