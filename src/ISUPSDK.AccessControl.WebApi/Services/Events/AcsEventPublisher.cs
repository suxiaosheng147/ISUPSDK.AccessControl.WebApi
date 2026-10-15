using System.Threading.Channels;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 事件生产者门面：把收到的门禁事件装进队列信封并放入内存待发缓冲（非阻塞）。
///
/// 调用点在事件接收路径上（HTTP 上报线程 / AMS 回调线程），因此这里<b>绝不阻塞</b>：
/// 缓冲满时丢弃最旧的信封并计数告警；事件本身已由 <c>AcsEventSpool</c> 落盘，
/// 可由 <c>AcsEventSpoolReplayer</c> 重放补齐。
/// </summary>
public sealed class AcsEventPublisher
{
    private readonly AcsEventQueueOptions _options;
    private readonly ILogger<AcsEventPublisher> _logger;
    private readonly Channel<AcsQueueEnvelope> _pending;

    private long _enqueued;
    private long _skipped;
    private long _dropped;
    private int _pendingCount;

    public AcsEventPublisher(IOptions<AcsEventQueueOptions> options, ILogger<AcsEventPublisher> logger)
    {
        _options = options.Value;
        _logger = logger;

        _pending = Channel.CreateBounded<AcsQueueEnvelope>(new BoundedChannelOptions(Math.Max(_options.PendingBufferSize, 100))
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>是否启用事件队列链路。</summary>
    public bool IsEnabled => _options.Enabled;

    /// <summary>待发布条数（近似值）。</summary>
    public int PendingCount => Math.Max(Volatile.Read(ref _pendingCount), 0);

    /// <summary>已入待发缓冲的事件数。</summary>
    public long Enqueued => Interlocked.Read(ref _enqueued);

    /// <summary>按配置过滤掉（如非门禁事件）的数量。</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>因待发缓冲满而被丢弃的信封数（事件仍在落盘文件中）。</summary>
    public long Dropped => Interlocked.Read(ref _dropped);

    /// <summary>
    /// 尝试把一条事件放入待发缓冲。
    /// 返回 false 表示未入队（功能未启用、被过滤、或缓冲已满）。
    /// </summary>
    public bool TryEnqueue(IsupAlarmEvent evt, bool skipDedup = false)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (_options.AccessControlOnly && !evt.IsAccessControl)
        {
            Interlocked.Increment(ref _skipped);
            return false;
        }

        // AMS 回调通道（Source 为 IsupJson / Isapi / EHome / Exception 等）可用开关单独关闭。
        if (!_options.IncludeAmsEvents && IsAmsChannel(evt))
        {
            Interlocked.Increment(ref _skipped);
            return false;
        }

        var envelope = new AcsQueueEnvelope
        {
            DedupKey = AcsEventSpool.BuildDedupKey(evt),
            Event = evt,
            EnqueuedAt = DateTimeOffset.UtcNow,
            Producer = "acs",
            SkipDedup = skipDedup,
        };

        var limit = Math.Max(_options.PendingBufferSize, 100);
        var queued = Volatile.Read(ref _pendingCount);
        if (queued >= limit)
        {
            Interlocked.Increment(ref _dropped);
            if (Interlocked.Read(ref _dropped) % 100 == 1)
            {
                _logger.LogWarning(
                    "事件待发缓冲已满（{Limit}），最旧的事件信封被丢弃；事件仍在落盘文件中，可由 spool 重放补齐。",
                    limit);
            }
        }

        if (!_pending.Writer.TryWrite(envelope))
        {
            Interlocked.Increment(ref _dropped);
            return false;
        }

        Interlocked.Increment(ref _pendingCount);
        Interlocked.Increment(ref _enqueued);
        return true;
    }

    /// <summary>读取待发信封（仅供发布 worker 使用）。</summary>
    public IAsyncEnumerable<AcsQueueEnvelope> ReadAllAsync(CancellationToken ct)
    {
        return ReadCoreAsync(ct);

        async IAsyncEnumerable<AcsQueueEnvelope> ReadCoreAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        {
            await foreach (var envelope in _pending.Reader.ReadAllAsync(token).ConfigureAwait(false))
            {
                Interlocked.Decrement(ref _pendingCount);
                yield return envelope;
            }
        }
    }

    /// <summary>完成写入（停止接收新事件）。</summary>
    public void Complete() => _pending.Writer.TryComplete();

    /// <summary>
    /// 是否属于 AMS 回调通道：HTTP 推送（HttpPush）与历史对账（Reconcile）之外的事件
    /// 都来自 <c>OTAP_AMS_StartListen</c> 的报警回调。
    /// </summary>
    private static bool IsAmsChannel(IsupAlarmEvent evt)
        => !string.Equals(evt.Source, "HttpPush", StringComparison.OrdinalIgnoreCase)
           && !string.Equals(evt.Source, "Reconcile", StringComparison.OrdinalIgnoreCase);
}
