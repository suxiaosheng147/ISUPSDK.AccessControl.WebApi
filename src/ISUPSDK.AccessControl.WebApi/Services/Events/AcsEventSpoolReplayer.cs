using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 落盘文件（outbox）重放：读取 <c>AcsEventSpool</c> 写的 JSONL 事件文件，
/// 把尚未入库的事件补写进数据库。
///
/// 用途：队列不可用期间投递失败、进程崩溃丢在途消息、或数据库曾短暂下线时，
/// 由本服务兜住"至少一次"语义（入库按 <c>DedupHash</c> 幂等，不会重复）。
/// </summary>
public sealed class AcsEventSpoolReplayer
{
    /// <summary>单批写库条数。</summary>
    private const int BatchSize = 500;

    private readonly AcsHttpPushOptions _pushOptions;
    private readonly AcsEventStore _store;
    private readonly ILogger<AcsEventSpoolReplayer> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private long _runs;
    private long _inserted;
    private long _skipped;
    private long _errors;
    private DateTimeOffset? _lastRunAt;

    public AcsEventSpoolReplayer(
        IOptions<AcsHttpPushOptions> pushOptions,
        AcsEventStore store,
        ILogger<AcsEventSpoolReplayer> logger)
    {
        _pushOptions = pushOptions.Value;
        _store = store;
        _logger = logger;
    }

    /// <summary>落盘目录。</summary>
    public string SpoolDirectory => _pushOptions.SpoolDirectory;

    /// <summary>已执行次数。</summary>
    public long Runs => Interlocked.Read(ref _runs);

    /// <summary>累计补写入行数。</summary>
    public long Inserted => Interlocked.Read(ref _inserted);

    /// <summary>累计跳过（重复）行数。</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>累计出错记录数。</summary>
    public long Errors => Interlocked.Read(ref _errors);

    /// <summary>最近一次重放时间。</summary>
    public DateTimeOffset? LastRunAt => _lastRunAt;

    /// <summary>
    /// 重放落盘文件。<paramref name="days"/> 表示只看最近 N 天的文件（0 表示全部）。
    /// </summary>
    public async Task<AcsEventReplayResult> ReplayAsync(int days, string? queueProvider = null, CancellationToken ct = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var directory = Path.GetFullPath(_pushOptions.SpoolDirectory);

        if (!Directory.Exists(directory))
        {
            _logger.LogDebug("事件落盘目录不存在，跳过重放：{Directory}", directory);
            return new AcsEventReplayResult();
        }

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Interlocked.Increment(ref _runs);
            _lastRunAt = DateTimeOffset.UtcNow;

            var earliest = days > 0 ? DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-days + 1)) : (DateOnly?)null;

            var files = Directory
                .EnumerateFiles(directory, "acs-events-*.jsonl")
                .Where(file => IsWithinRange(file, earliest))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToList();

            var records = 0L;
            var inserted = 0L;
            var skipped = 0L;
            var errors = 0L;

            var batch = new List<AcsQueueEnvelope>(BatchSize);

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    foreach (var line in File.ReadLines(file))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        AcsSpoolRecord? record;
                        try
                        {
                            record = JsonSerializer.Deserialize<AcsSpoolRecord>(line, AcsQueueSerialization.Options);
                        }
                        catch (JsonException)
                        {
                            errors++;
                            continue;
                        }

                        if (record is null || string.IsNullOrWhiteSpace(record.Key))
                        {
                            errors++;
                            continue;
                        }

                        records++;
                        batch.Add(new AcsQueueEnvelope
                        {
                            DedupKey = record.Key,
                            Event = record.Event,
                            EnqueuedAt = record.PersistedAt,
                            Producer = "spool-replay",
                        });

                        if (batch.Count >= BatchSize)
                        {
                            var result = await FlushAsync(batch, queueProvider, ct).ConfigureAwait(false);
                            inserted += result.Inserted;
                            skipped += result.Skipped;
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    _logger.LogWarning(ex, "重放落盘文件失败：{File}", file);
                }
            }

            if (batch.Count > 0)
            {
                var result = await FlushAsync(batch, queueProvider, ct).ConfigureAwait(false);
                inserted += result.Inserted;
                skipped += result.Skipped;
            }

            Interlocked.Add(ref _inserted, inserted);
            Interlocked.Add(ref _skipped, skipped);
            Interlocked.Add(ref _errors, errors);

            var elapsed = (long)(DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            if (records > 0)
            {
                _logger.LogInformation(
                    "事件落盘重放完成：文件 {Files} 个，记录 {Records} 条，新入库 {Inserted} 行，重复跳过 {Skipped} 行，出错 {Errors} 条，耗时 {Elapsed}ms。",
                    files.Count,
                    records,
                    inserted,
                    skipped,
                    errors,
                    elapsed);
            }
            else
            {
                _logger.LogDebug("事件落盘重放：文件 {Files} 个，无记录。", files.Count);
            }

            return new AcsEventReplayResult
            {
                Files = files.Count,
                Records = records,
                Inserted = inserted,
                Skipped = skipped,
                Errors = errors,
                ElapsedMs = elapsed,
            };
        }
        catch (OperationCanceledException)
        {
            return new AcsEventReplayResult();
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<AcsEventWriteResult> FlushAsync(
        List<AcsQueueEnvelope> batch,
        string? queueProvider,
        CancellationToken ct)
    {
        var snapshot = batch.ToArray();
        batch.Clear();

        var result = await _store
            .AddBatchAsync(snapshot, queueProvider ?? "SpoolReplay", ct)
            .ConfigureAwait(false);

        if (!result.Success)
        {
            Interlocked.Add(ref _errors, snapshot.Length);
        }

        return result;
    }

    /// <summary>文件名形如 <c>acs-events-20261013.jsonl</c>；按文件名里的日期过滤。</summary>
    private static bool IsWithinRange(string file, DateOnly? earliest)
    {
        if (earliest is null)
        {
            return true;
        }

        var name = Path.GetFileNameWithoutExtension(file);
        var parts = name.Split('-');
        if (parts.Length < 3 || !DateOnly.TryParseExact(parts[^1], "yyyyMMdd", out var date))
        {
            // 文件名不符合约定：保守地纳入重放（入库幂等，不会重复）。
            return true;
        }

        return date >= earliest.Value;
    }
}
