using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 事件保留策略：按 <c>EventStore:RetentionDays</c> 定时删除过旧的事件行（0 表示不清理）。
/// </summary>
public sealed class EventStoreRetentionService : BackgroundService
{
    private readonly AcsEventStoreOptions _options;
    private readonly AcsEventStore _store;
    private readonly ILogger<EventStoreRetentionService> _logger;

    private long _runs;
    private long _deleted;
    private DateTimeOffset? _lastRunAt;

    public EventStoreRetentionService(
        IOptions<AcsEventStoreOptions> options,
        AcsEventStore store,
        ILogger<EventStoreRetentionService> logger)
    {
        _options = options.Value;
        _store = store;
        _logger = logger;
    }

    /// <summary>未启用保留策略时为 false。</summary>
    public bool IsEnabled => _options.Enabled && _options.RetentionDays > 0;

    /// <summary>已执行次数。</summary>
    public long Runs => Interlocked.Read(ref _runs);

    /// <summary>累计删除行数。</summary>
    public long Deleted => Interlocked.Read(ref _deleted);

    /// <summary>最近一次执行时间。</summary>
    public DateTimeOffset? LastRunAt => _lastRunAt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!IsEnabled)
        {
            _logger.LogDebug("事件保留策略未启用（EventStore:RetentionDays={Days}）。", _options.RetentionDays);
            return;
        }

        var interval = TimeSpan.FromHours(Math.Clamp(_options.RetentionSweepHours, 1, 720));

        _logger.LogInformation(
            "事件保留策略已启用：保留 {Days} 天，每 {Hours} 小时清理一次。",
            _options.RetentionDays,
            interval.TotalHours);

        // 启动后先等一会，避免与应用启动期的建表/重放争抢资源。
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Interlocked.Increment(ref _runs);
                _lastRunAt = DateTimeOffset.UtcNow;

                var threshold = DateTimeOffset.UtcNow.AddDays(-_options.RetentionDays);
                var deleted = await _store.DeleteOlderThanAsync(threshold, stoppingToken).ConfigureAwait(false);
                Interlocked.Add(ref _deleted, deleted);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "事件保留策略执行失败。");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
