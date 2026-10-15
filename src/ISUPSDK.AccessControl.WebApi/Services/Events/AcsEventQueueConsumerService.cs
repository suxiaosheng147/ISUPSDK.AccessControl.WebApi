using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 事件消费者：从消息队列批量取件并写入数据库（<c>ISUP:EventStore</c>）。
///
/// 处理规则：
/// <list type="bullet">
/// <item>整批写库成功 → 逐条确认（<c>ack</c>）；</item>
/// <item>写库失败 → 未超上限的消息延迟重投（投递次数 +1），超上限的进死信并告警；</item>
/// <item>启动时可选重放落盘文件（<c>Consumer:ReplaySpoolOnStartup</c>），兜住队列/进程异常期间的缺口。</item>
/// </list>
/// </summary>
public sealed class AcsEventQueueConsumerService : BackgroundService
{
    private readonly AcsEventQueueOptions _options;
    private readonly AcsEventStore _store;
    private readonly IEventQueue _queue;
    private readonly AcsEventSpoolReplayer _replayer;
    private readonly ILogger<AcsEventQueueConsumerService> _logger;

    private long _consumed;
    private long _stored;
    private long _skipped;
    private long _retried;
    private long _deadLettered;
    private long _failed;

    private DateTimeOffset? _lastConsumedAt;
    private string? _lastError;
    private DateTimeOffset? _lastErrorAt;

    public AcsEventQueueConsumerService(
        IOptions<AcsEventQueueOptions> options,
        AcsEventStore store,
        IEventQueue queue,
        AcsEventSpoolReplayer replayer,
        ILogger<AcsEventQueueConsumerService> logger)
    {
        _options = options.Value;
        _store = store;
        _queue = queue;
        _replayer = replayer;
        _logger = logger;
    }

    /// <summary>消费者是否处于工作状态（队列 + 落库 + 消费者开关都打开）。</summary>
    public bool IsEnabled => _options.Enabled && _options.Consumer.Enabled && _store.IsEnabled;

    /// <summary>已从队列取出的消息数。</summary>
    public long Consumed => Interlocked.Read(ref _consumed);

    /// <summary>已写入数据库的行数。</summary>
    public long Stored => Interlocked.Read(ref _stored);

    /// <summary>因重复跳过的行数。</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>重投次数。</summary>
    public long Retried => Interlocked.Read(ref _retried);

    /// <summary>进入死信的消息数。</summary>
    public long DeadLettered => Interlocked.Read(ref _deadLettered);

    /// <summary>写库失败的批次数。</summary>
    public long Failed => Interlocked.Read(ref _failed);

    /// <summary>最近一次取件时间。</summary>
    public DateTimeOffset? LastConsumedAt => _lastConsumedAt;

    /// <summary>最近一次错误。</summary>
    public string? LastError => _lastError;

    /// <summary>最近一次错误时间。</summary>
    public DateTimeOffset? LastErrorAt => _lastErrorAt;

    /// <summary>手动触发落盘文件重放。<paramref name="days"/> 为 null 时用配置值。</summary>
    public Task<AcsEventReplayResult> ReplaySpoolAsync(int? days = null, CancellationToken ct = default)
        => _replayer.ReplayAsync(days ?? _options.Consumer.ReplaySpoolDays, _queue.ProviderName, ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("事件队列未启用（ISUP:EventQueue:Enabled=false），消费者不启动。");
            return;
        }

        if (!_store.IsEnabled)
        {
            _logger.LogInformation("事件落库未启用（ISUP:EventStore:Enabled=false），消费者不启动。");
            return;
        }

        if (!_options.Consumer.Enabled)
        {
            _logger.LogInformation("事件消费者已关闭（ISUP:EventQueue:Consumer:Enabled=false）：只发布不消费。");
            return;
        }

        await _store.EnsureCreatedAsync(stoppingToken).ConfigureAwait(false);
        await EnsureQueueStartedAsync(stoppingToken).ConfigureAwait(false);

        if (_options.Consumer.ReplaySpoolOnStartup)
        {
            try
            {
                await ReplaySpoolAsync(ct: stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "启动时重放事件落盘文件失败。");
            }
        }

        var batchSize = Math.Clamp(_options.Consumer.BatchSize, 1, 1000);
        var wait = TimeSpan.FromMilliseconds(Math.Clamp(_options.Consumer.FlushIntervalMs, 50, 60000));

        _logger.LogInformation(
            "事件消费者已启动：后端={Provider}，队列={Queue}，批大小={BatchSize}，等待={Wait}ms，落库={StoreProvider}/{Table}。",
            _queue.ProviderName,
            _queue.QueueName,
            batchSize,
            wait.TotalMilliseconds,
            _store.ProviderName,
            _store.TableName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var messages = await _queue.ReceiveBatchAsync(batchSize, wait, stoppingToken).ConfigureAwait(false);
                if (messages.Count == 0)
                {
                    continue;
                }

                await ProcessBatchAsync(messages, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _lastError = $"消费循环异常：{ex.Message}";
                _lastErrorAt = DateTimeOffset.UtcNow;
                _logger.LogWarning(ex, "事件消费循环异常，1 秒后继续。");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation(
            "事件消费者已退出：取出 {Consumed} 条，入库 {Stored} 行，重复 {Skipped} 行，重投 {Retried} 次，死信 {DeadLettered} 条。",
            Consumed,
            Stored,
            Skipped,
            Retried,
            DeadLettered);
    }

    private async Task EnsureQueueStartedAsync(CancellationToken ct)
    {
        var delay = TimeSpan.FromSeconds(5);

        while (!ct.IsCancellationRequested)
        {
            if (await _queue.StartAsync(ct).ConfigureAwait(false))
            {
                return;
            }

            _logger.LogWarning("事件队列（{Provider}）启动失败，{Delay} 秒后重试。", _queue.ProviderName, delay.TotalSeconds);

            try
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task ProcessBatchAsync(IReadOnlyList<AcsQueueMessage> messages, CancellationToken ct)
    {
        Interlocked.Add(ref _consumed, messages.Count);
        _lastConsumedAt = DateTimeOffset.UtcNow;

        var valid = new List<AcsQueueMessage>(messages.Count);

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Envelope?.DedupKey))
            {
                Interlocked.Increment(ref _deadLettered);
                await _queue.DeadLetterAsync(message, "消息缺少去重键（无法幂等入库）", ct).ConfigureAwait(false);
                continue;
            }

            valid.Add(message);
        }

        if (valid.Count == 0)
        {
            return;
        }

        var result = await _store
            .AddBatchAsync(valid.Select(message => message.Envelope).ToList(), _queue.ProviderName, ct)
            .ConfigureAwait(false);

        if (result.Success)
        {
            Interlocked.Add(ref _stored, result.Inserted);
            Interlocked.Add(ref _skipped, result.Skipped);

            foreach (var message in valid)
            {
                await _queue.CompleteAsync(message, ct).ConfigureAwait(false);
            }

            return;
        }

        Interlocked.Increment(ref _failed);
        _lastError = result.Error;
        _lastErrorAt = DateTimeOffset.UtcNow;

        var maxAttempts = Math.Max(_options.Consumer.MaxAttempts, 1);
        var delayMs = Math.Max(_options.Consumer.RetryDelaySeconds, 1) * 1000;

        foreach (var message in valid)
        {
            if (message.Envelope.Attempt >= maxAttempts)
            {
                Interlocked.Increment(ref _deadLettered);
                await _queue
                    .DeadLetterAsync(message, $"写库连续失败 {message.Envelope.Attempt} 次：{result.Error}", ct)
                    .ConfigureAwait(false);
            }
            else
            {
                Interlocked.Increment(ref _retried);
                await _queue.RetryAsync(message, delayMs, ct).ConfigureAwait(false);
            }
        }
    }
}
