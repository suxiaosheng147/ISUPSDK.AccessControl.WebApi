using ISUPSDK.AccessControl.WebApi.Models;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 事件入库桥接：把 ISUP 报警事件（<see cref="IsupAmsService"/> 的内存缓冲）投递到事件队列管道。
/// <para>
/// 完整链路：SDK 报警回调 → <see cref="IsupAmsService"/>.Ingest → 本服务 →
/// <see cref="AcsEventPublisher"/> → <c>AcsEventPublishWorker</c> →
/// <c>IEventQueue</c>（None / Msmq / RabbitMq）→ 消费者 → <c>AcsEventStore</c>（幂等入库）。
/// </para>
/// <para>
/// 落盘兜底（spool）与历史对账（reconcile）补漏的事件同样经 <see cref="IsupAmsService"/>.Ingest 进入此链路。
/// </para>
/// </summary>
public sealed class IsupEventIngestService : IHostedService
{
    private readonly AcsEventPublisher _publisher;
    private readonly ILogger<IsupEventIngestService> _logger;

    private long _submitted;

    public IsupEventIngestService(
        IsupAmsService ams,
        AcsEventPublisher publisher,
        ILogger<IsupEventIngestService> logger)
    {
        _publisher = publisher;
        _logger = logger;
        ams.EventCaptured += OnEventCaptured;
    }

    /// <summary>事件管道是否启用（<c>ISUP:EventQueue:Enabled</c>）。</summary>
    public bool IsEnabled => _publisher.IsEnabled;

    /// <summary>已提交到发布缓冲的事件数。</summary>
    public long Submitted => Interlocked.Read(ref _submitted);

    /// <summary>发布缓冲中待投递条数。</summary>
    public int Pending => _publisher.PendingCount;

    /// <summary>已成功投递到队列的条数。</summary>
    public long Enqueued => _publisher.Enqueued;

    /// <summary>因缓冲满等原因被丢弃的条数。</summary>
    public long Dropped => _publisher.Dropped;

    /// <summary>
    /// 宿主启动时挂接。
    /// <para>
    /// 本服务只依赖构造时建立的 <c>EventCaptured</c> 订阅，因此 Start/Stop 为空实现；
    /// 注册为 <see cref="IHostedService"/> 的目的是<b>确保 DI 容器真的实例化它</b>——
    /// 否则没有任何服务依赖它时，订阅不会建立，事件就永远不会进入队列。
    /// </para>
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "门禁事件投递桥接已挂接（事件队列：{Status}）。",
            IsEnabled ? "启用" : "未启用");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    private void OnEventCaptured(object? sender, IsupAlarmEvent e)
    {
        try
        {
            if (_publisher.TryEnqueue(e, e.SkipDedup))
            {
                Interlocked.Increment(ref _submitted);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "事件投递到队列失败（DeviceId={DeviceId}, Sequence={Sequence}）。", e.DeviceId, e.Sequence);
        }
    }
}
