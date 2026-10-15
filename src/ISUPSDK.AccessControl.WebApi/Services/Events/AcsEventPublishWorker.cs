using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 事件发布 worker：把 <see cref="AcsEventPublisher"/> 缓冲里的事件投递到消息队列。
///
/// 单条消息投递失败会按 <c>EventQueue:PublishMaxAttempts</c> 重试（间隔 <c>PublishRetryDelaySeconds</c>）；
/// 仍失败则放弃并告警——事件本身已在 <c>AcsEventSpool</c> 的落盘文件中，
/// 由 <c>AcsEventSpoolReplayer</c>（消费者启动时/手动触发）按去重键幂等补齐，因此不会丢事件。
/// </summary>
public sealed class AcsEventPublishWorker : BackgroundService
{
    private readonly AcsEventQueueOptions _options;
    private readonly AcsEventPublisher _publisher;
    private readonly IEventQueue _queue;
    private readonly ILogger<AcsEventPublishWorker> _logger;

    private long _published;
    private long _failed;
    private DateTimeOffset? _lastPublishedAt;
    private string? _lastError;
    private DateTimeOffset? _lastErrorAt;

    public AcsEventPublishWorker(
        IOptions<AcsEventQueueOptions> options,
        AcsEventPublisher publisher,
        IEventQueue queue,
        ILogger<AcsEventPublishWorker> logger)
    {
        _options = options.Value;
        _publisher = publisher;
        _queue = queue;
        _logger = logger;
    }

    /// <summary>已成功投递的消息数。</summary>
    public long Published => Interlocked.Read(ref _published);

    /// <summary>最终投递失败（已交给落盘兜底）的消息数。</summary>
    public long Failed => Interlocked.Read(ref _failed);

    /// <summary>最近一次成功投递时间。</summary>
    public DateTimeOffset? LastPublishedAt => _lastPublishedAt;

    /// <summary>最近一次错误。</summary>
    public string? LastError => _lastError;

    /// <summary>最近一次错误时间。</summary>
    public DateTimeOffset? LastErrorAt => _lastErrorAt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_publisher.IsEnabled)
        {
            _logger.LogInformation("事件队列未启用（ISUP:EventQueue:Enabled=false），发布 worker 不启动。");
            return;
        }

        _logger.LogInformation(
            "事件发布 worker 已启动：后端={Provider}，队列={Queue}，重试上限={Attempts}。",
            _queue.ProviderName,
            _queue.QueueName,
            Math.Max(_options.PublishMaxAttempts, 1));

        await _queue.StartAsync(stoppingToken).ConfigureAwait(false);

        var retryDelay = TimeSpan.FromSeconds(Math.Clamp(_options.PublishRetryDelaySeconds, 1, 300));

        try
        {
            await foreach (var envelope in _publisher.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                if (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                if (await PublishWithRetryAsync(envelope, retryDelay, stoppingToken).ConfigureAwait(false))
                {
                    Interlocked.Increment(ref _published);
                    _lastPublishedAt = DateTimeOffset.UtcNow;
                }
                else
                {
                    Interlocked.Increment(ref _failed);
                    _lastError = $"事件 {envelope.DedupKey} 投递失败（已重试 {Math.Max(_options.PublishMaxAttempts, 1)} 次）";
                    _lastErrorAt = DateTimeOffset.UtcNow;

                    _logger.LogWarning(
                        "事件 {DedupKey}（设备 {DeviceId}）投递到 {Provider} 队列失败，已放弃；" +
                        "事件仍在落盘文件中，将由 spool 重放补齐。",
                        envelope.DedupKey,
                        envelope.Event.DeviceId,
                        _queue.ProviderName);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // 正常关闭
        }
        finally
        {
            _publisher.Complete();
        }

        _logger.LogInformation("事件发布 worker 已退出：已投递 {Published} 条，失败 {Failed} 条。", Published, Failed);
    }

    private async Task<bool> PublishWithRetryAsync(AcsQueueEnvelope envelope, TimeSpan retryDelay, CancellationToken ct)
    {
        var attempts = Math.Max(_options.PublishMaxAttempts, 1);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                if (!_queue.IsReady)
                {
                    await _queue.StartAsync(ct).ConfigureAwait(false);
                }

                if (_queue.IsReady && await _queue.PublishAsync(envelope, ct).ConfigureAwait(false))
                {
                    return true;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"投递事件 {envelope.DedupKey} 异常：{ex.Message}";
                _lastErrorAt = DateTimeOffset.UtcNow;
            }

            if (attempt < attempts)
            {
                try
                {
                    await Task.Delay(retryDelay, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }
}
