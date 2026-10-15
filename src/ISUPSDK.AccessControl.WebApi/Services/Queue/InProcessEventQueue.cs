using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue;

/// <summary>
/// 进程内直通队列（<c>EventQueue:Provider=None</c>）：
/// "发布"即写入同一个内存缓冲，"消费"照常由 <c>AcsEventQueueConsumerService</c> 批量写库。
///
/// 适用场景：单机部署、不需要跨进程/跨机解耦，但仍希望把事件落库。
/// 语义：进程崩溃会丢在途事件（靠 <c>AcsEventSpool</c> 落盘文件重放兜底）。
/// </summary>
public sealed class InProcessEventQueue : EventQueueBase
{
    public InProcessEventQueue(IOptions<AcsEventQueueOptions> options, ILogger<InProcessEventQueue> logger)
        : base(options.Value, logger)
    {
    }

    /// <inheritdoc />
    public override string ProviderName => nameof(AcsEventQueueProvider.None);

    /// <inheritdoc />
    protected override Task ConnectCoreAsync(CancellationToken ct)
    {
        MarkReady();
        Logger.LogInformation("事件队列后端为进程内直通（Provider=None）：不经过外部消息队列，事件直接由消费者写库。");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task SendCoreAsync(AcsQueueEnvelope envelope, CancellationToken ct)
        => await BufferMessageAsync(new AcsQueueMessage { Envelope = envelope, NativeId = "in-process" }, ct).ConfigureAwait(false);

    /// <inheritdoc />
    protected override Task ReceiveLoopAsync(CancellationToken ct) => Task.CompletedTask;

    /// <inheritdoc />
    protected override Task CompleteCoreAsync(AcsQueueMessage message, CancellationToken ct) => Task.CompletedTask;
}
