using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ISUPSDK.AccessControl.WebApi.Data.EventStore;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 门禁事件的落库服务：把队列消息批量写入 <c>ISUP:EventStore</c> 指定的数据库。
///
/// 幂等性：入库前先按 <c>DedupHash</c>（去重键的 SHA-256）过滤已存在的行，
/// 并对并发/残留冲突走"逐条写入 + 冲突即跳过"的兜底，因此重复投递、重放、重启补数据都不会产生重复行。
/// </summary>
public sealed class AcsEventStore
{
    /// <summary>库中总行数的缓存时长：<c>/health</c> 会被监控频繁调用，避免每次全表计数。</summary>
    private static readonly TimeSpan TotalRowsCacheTtl = TimeSpan.FromSeconds(10);

    private readonly IDbContextFactory<AcsEventDbContext> _factory;
    private readonly AcsEventStoreOptions _options;
    private readonly ILogger<AcsEventStore> _logger;

    private long _inserted;
    private long _duplicates;
    private long _failed;

    private long _totalRowsCache = -1;
    private long _totalRowsCacheAtTicks;

    private int _schemaReady = -1;

    /// <summary>没有新增入库时的空键集合（避免重复分配）。</summary>
    private static readonly IReadOnlySet<string> EmptyInsertedKeys = new HashSet<string>(StringComparer.Ordinal);

    private DateTimeOffset? _lastStoredAt;
    private DateTimeOffset? _lastErrorAt;
    private string? _lastError;

    public AcsEventStore(
        IDbContextFactory<AcsEventDbContext> factory,
        IOptions<AcsEventStoreOptions> options,
        ILogger<AcsEventStore> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>是否启用事件落库。</summary>
    public bool IsEnabled => _options.Enabled;

    /// <summary>数据库类型名。</summary>
    public string ProviderName => _options.ResolveProvider().ToString();

    /// <summary>表名。</summary>
    public string TableName => string.IsNullOrWhiteSpace(_options.TableName) ? "AcsEvents" : _options.TableName.Trim();

    /// <summary>事件表是否可用（启动时或首次操作时探测；用于暴露"表不存在"这类致命配置问题）。</summary>
    public bool IsSchemaReady => Volatile.Read(ref _schemaReady) == 1;

    /// <summary>表不可用时的原因与处理建议。</summary>
    public string? SchemaError { get; private set; }

    /// <summary>启动时建库/建表（<c>AutoCreate=false</c> 时只探测表是否存在并给出明确告警）。</summary>
    public async Task<bool> EnsureCreatedAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("事件落库未启用（ISUP:EventStore:Enabled=false）。");
            return true;
        }

        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            if (_options.AutoCreate)
            {
                await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
            }

            if (await ProbeSchemaAsync(db, ct).ConfigureAwait(false))
            {
                _logger.LogInformation("事件库已就绪：provider={Provider}，表={Table}。", ProviderName, TableName);
                return true;
            }

            // 表不存在：这是"事件一条都进不去"的致命配置问题，必须显式告警（而不是只留一条日志）。
            _logger.LogError("{SchemaError}", SchemaError);
            return false;
        }
        catch (Exception ex)
        {
            SetError(ex, "创建事件表失败");
            SchemaError = BuildSchemaError(ex);
            _logger.LogError(ex, "创建事件表失败：{Message}", ex.Message);
            return false;
        }
    }

    /// <summary>探测事件表是否可用（并记录 schema 错误）。</summary>
    private async Task<bool> ProbeSchemaAsync(AcsEventDbContext db, CancellationToken ct)
    {
        try
        {
            await db.Events.AnyAsync(ct).ConfigureAwait(false);

            Volatile.Write(ref _schemaReady, 1);
            SchemaError = null;
            return true;
        }
        catch (Exception ex)
        {
            Volatile.Write(ref _schemaReady, 0);
            SchemaError = BuildSchemaError(ex);
            return false;
        }
    }

    /// <summary>判断异常是否属于"表/对象不存在"这类 schema 问题。</summary>
    private static bool IsSchemaError(Exception ex)
    {
        var message = ex.GetBaseException().Message;

        return message.Contains("no such table", StringComparison.OrdinalIgnoreCase)       // SQLite
            || message.Contains("doesn't exist", StringComparison.OrdinalIgnoreCase)       // MySQL
            || message.Contains("Unknown table", StringComparison.OrdinalIgnoreCase)       // MySQL 变体
            || message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase); // SQL Server
    }

    /// <summary>把"表不存在"这类错误翻译成带处理建议的提示。</summary>
    private string BuildSchemaError(Exception ex)
    {
        var raw = ex.GetBaseException().Message.TrimEnd('.', '。', ' ');
        var provider = _options.ResolveProvider().ToString().ToLowerInvariant();
        var script = $"docs/event-store-{(provider == "sqlite" ? "sqlite" : provider == "mysql" ? "mysql" : "sqlserver")}.sql";

        return $"事件表 {TableName} 不可用：{raw}。" +
               $"请执行建表脚本 {script}（详见 README 5.2），或把 ISUP:EventStore:AutoCreate 设为 true 让服务自动建表。";
    }

    /// <summary>批量写入一批事件，返回写入结果（重复行计入 <see cref="AcsEventWriteResult.Skipped"/>）。</summary>
    public async Task<AcsEventWriteResult> AddBatchAsync(
        IReadOnlyList<AcsQueueEnvelope> batch,
        string? queueProvider,
        CancellationToken ct = default)
    {
        if (batch.Count == 0 || !_options.Enabled)
        {
            return new AcsEventWriteResult(0, 0, true, null, EmptyInsertedKeys);
        }

        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var now = DateTimeOffset.UtcNow;
            var records = batch
                .Select(envelope => BuildRecord(envelope, queueProvider, now))
                .GroupBy(record => record.DedupHash, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();

            var duplicateInBatch = batch.Count - records.Count;

            var hashes = records.Select(record => record.DedupHash).ToList();
            var existing = await db.Events
                .Where(e => hashes.Contains(e.DedupHash))
                .Select(e => e.DedupHash)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var existingSet = new HashSet<string>(existing, StringComparer.Ordinal);
            var fresh = records.Where(record => !existingSet.Contains(record.DedupHash)).ToList();

            var skipped = duplicateInBatch + (records.Count - fresh.Count);
            if (skipped > 0)
            {
                Interlocked.Add(ref _duplicates, skipped);
            }

            if (fresh.Count == 0)
            {
                return new AcsEventWriteResult(0, skipped, true, null, EmptyInsertedKeys);
            }

            var inserted = await SaveAsync(db, fresh, ct).ConfigureAwait(false);

            Interlocked.Add(ref _inserted, inserted);
            _lastStoredAt = DateTimeOffset.UtcNow;

            // 让"库中总行数"缓存同步跟上，避免 /health 与 /api/events/status 滞后（缓存未建立时保持 -1，由下次统计填充）。
            if (inserted > 0 && Interlocked.Read(ref _totalRowsCache) >= 0)
            {
                Interlocked.Add(ref _totalRowsCache, inserted);
            }
            else if (inserted > 0)
            {
                // 尚无缓存：丢弃时间戳，使下次统计重新取数。
                Interlocked.Exchange(ref _totalRowsCacheAtTicks, 0);
            }

            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("事件入库：新增 {Inserted} 行，跳过重复 {Skipped} 行。", inserted, skipped);
            }

            return new AcsEventWriteResult(
                inserted,
                skipped,
                true,
                null,
                fresh.Select(record => record.DedupKey).ToHashSet(StringComparer.Ordinal));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Interlocked.Increment(ref _failed);

            if (IsSchemaError(ex))
            {
                Volatile.Write(ref _schemaReady, 0);
                SchemaError = BuildSchemaError(ex);
                SetError(ex, "事件批量入库失败（事件表不可用）");
                _logger.LogError("{SchemaError}", SchemaError);

                return new AcsEventWriteResult(0, 0, false, SchemaError);
            }

            SetError(ex, "事件批量入库失败");
            _logger.LogError(ex, "事件批量入库失败：{Message}", ex.Message);
            return new AcsEventWriteResult(0, 0, false, ex.Message);
        }
    }

    /// <summary>按条件查询已入库事件（事件表不可用时返回 503 与处理建议）。</summary>
    public async Task<(IReadOnlyList<AcsEventRecord> Items, long Total)> QueryAsync(
        AcsEventQuery query,
        CancellationToken ct = default)
    {
        try
        {
            var result = await QueryCoreAsync(query, ct).ConfigureAwait(false);

            Volatile.Write(ref _schemaReady, 1);
            SchemaError = null;
            return result;
        }
        catch (Exception ex) when (IsSchemaError(ex))
        {
            Volatile.Write(ref _schemaReady, 0);
            SchemaError = BuildSchemaError(ex);

            throw new IsupApiException(SchemaError, StatusCodes.Status503ServiceUnavailable);
        }
    }

    private async Task<(IReadOnlyList<AcsEventRecord> Items, long Total)> QueryCoreAsync(
        AcsEventQuery query,
        CancellationToken ct)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var source = db.Events.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.DeviceId))
        {
            source = source.Where(e => e.DeviceId.Contains(query.DeviceId));
        }

        if (query.AccessControlOnly == true)
        {
            source = source.Where(e => e.IsAccessControl);
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            source = source.Where(e => e.EventType != null && e.EventType.Contains(query.EventType));
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            source = source.Where(e => e.Source == query.Source);
        }

        if (query.Major is { } major)
        {
            source = source.Where(e => e.Major == major);
        }

        if (query.Minor is { } minor)
        {
            source = source.Where(e => e.Minor == minor);
        }

        if (!string.IsNullOrWhiteSpace(query.VerifyMode))
        {
            source = source.Where(e => e.VerifyMode == query.VerifyMode);
        }

        if (!string.IsNullOrWhiteSpace(query.EmployeeNo))
        {
            source = source.Where(e => e.EmployeeNo == query.EmployeeNo);
        }

        if (!string.IsNullOrWhiteSpace(query.CardNo))
        {
            source = source.Where(e => e.CardNo == query.CardNo);
        }

        if (query.From is { } from)
        {
            source = source.Where(e => e.EventTime >= from);
        }

        if (query.To is { } to)
        {
            source = source.Where(e => e.EventTime <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            source = source.Where(e => e.Payload != null && e.Payload.Contains(query.Keyword));
        }

        var total = await source.LongCountAsync(ct).ConfigureAwait(false);

        var limit = Math.Clamp(query.Limit, 1, 1000);
        var offset = Math.Max(query.Offset, 0);

        var ordered = query.OrderAscending
            ? source.OrderBy(e => e.EventTime).ThenBy(e => e.Id)
            : source.OrderByDescending(e => e.EventTime).ThenByDescending(e => e.Id);

        var items = await ordered
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (!query.IncludePayload)
        {
            foreach (var item in items)
            {
                item.Payload = null;
            }
        }

        return (items, total);
    }

    /// <summary>按主键取单条事件。</summary>
    public async Task<AcsEventRecord?> GetByIdAsync(long id, bool includePayload = true, CancellationToken ct = default)
    {
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var record = await db.Events
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == id, ct)
                .ConfigureAwait(false);

            if (record is not null && !includePayload)
            {
                record.Payload = null;
            }

            Volatile.Write(ref _schemaReady, 1);
            SchemaError = null;
            return record;
        }
        catch (Exception ex) when (IsSchemaError(ex))
        {
            Volatile.Write(ref _schemaReady, 0);
            SchemaError = BuildSchemaError(ex);

            throw new IsupApiException(SchemaError, StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>
    /// 取事件可选值清单（事件类型 / 来源 / 认证方式），供前端下拉使用。
    /// 只统计库里**实际出现过的值**，按出现次数倒序，因此选项永远与现场数据一致。
    /// </summary>
    public async Task<AcsEventFacets> GetFacetsAsync(int limit = 50, CancellationToken ct = default)
    {
        var max = Math.Clamp(limit, 1, 500);

        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var eventTypes = await db.Events
                .Where(e => e.EventType != null && e.EventType != string.Empty)
                .GroupBy(e => e.EventType!)
                .Select(g => new AcsEventFacetItem { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Value)
                .Take(max)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var sources = await db.Events
                .GroupBy(e => e.Source)
                .Select(g => new AcsEventFacetItem { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Value)
                .Take(max)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var verifyModes = await db.Events
                .Where(e => e.VerifyMode != null && e.VerifyMode != string.Empty)
                .GroupBy(e => e.VerifyMode!)
                .Select(g => new AcsEventFacetItem { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(item => item.Count)
                .ThenBy(item => item.Value)
                .Take(max)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // 主类型 / 次类型：SQL 里只做 int 分组，字符串化放内存（规避各 provider 对 ToString 的翻译差异）。
            var majorGroups = await db.Events
                .Where(e => e.Major != null)
                .GroupBy(e => e.Major!.Value)
                .Select(g => new { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value)
                .Take(max)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var minorGroups = await db.Events
                .Where(e => e.Minor != null)
                .GroupBy(e => e.Minor!.Value)
                .Select(g => new { Value = g.Key, Count = g.LongCount() })
                .OrderByDescending(x => x.Count)
                .ThenBy(x => x.Value)
                .Take(max)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            var total = await db.Events.LongCountAsync(ct).ConfigureAwait(false);

            Volatile.Write(ref _schemaReady, 1);
            SchemaError = null;

            return new AcsEventFacets
            {
                Majors = majorGroups
                    .Select(x => new AcsEventFacetItem { Value = x.Value.ToString(CultureInfo.InvariantCulture), Count = x.Count })
                    .ToList(),
                Minors = minorGroups
                    .Select(x => new AcsEventFacetItem { Value = x.Value.ToString(CultureInfo.InvariantCulture), Count = x.Count })
                    .ToList(),
                EventTypes = eventTypes,
                Sources = sources,
                VerifyModes = verifyModes,
                TotalRows = total,
            };
        }
        catch (Exception ex) when (IsSchemaError(ex))
        {
            Volatile.Write(ref _schemaReady, 0);
            SchemaError = BuildSchemaError(ex);

            throw new IsupApiException(SchemaError, StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>取统计快照（含库中总行数，总行数按 <see cref="TotalRowsCacheTtl"/> 缓存）。</summary>
    public async Task<AcsEventStoreStatistics> GetStatisticsAsync(CancellationToken ct = default)
    {
        long? totalRows = null;

        if (_options.Enabled)
        {
            var now = DateTimeOffset.UtcNow;
            var cached = Interlocked.Read(ref _totalRowsCache);
            var cachedAtTicks = Interlocked.Read(ref _totalRowsCacheAtTicks);

            if (cached >= 0 && now.UtcTicks - cachedAtTicks < TotalRowsCacheTtl.Ticks)
            {
                totalRows = cached;
            }
            else
            {
                try
                {
                    await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
                    var count = await db.Events.LongCountAsync(ct).ConfigureAwait(false);

                    Interlocked.Exchange(ref _totalRowsCache, count);
                    Interlocked.Exchange(ref _totalRowsCacheAtTicks, now.UtcTicks);
                    totalRows = count;
                }
                catch (Exception ex)
                {
                    if (IsSchemaError(ex))
                    {
                        Volatile.Write(ref _schemaReady, 0);
                        SchemaError = BuildSchemaError(ex);
                    }

                    SetError(ex, "统计事件行数失败");

                    // 计数失败时退回上一次的缓存值（哪怕已过期），避免接口直接报错。
                    totalRows = cached >= 0 ? cached : null;
                }
            }
        }

        return new AcsEventStoreStatistics
        {
            Enabled = _options.Enabled,
            Provider = ProviderName,
            TableName = TableName,
            Inserted = Interlocked.Read(ref _inserted),
            Duplicates = Interlocked.Read(ref _duplicates),
            Failed = Interlocked.Read(ref _failed),
            TotalRows = totalRows,
            LastStoredAt = _lastStoredAt,
            LastError = _lastError,
            LastErrorAt = _lastErrorAt,
        };
    }

    /// <summary>删除早于给定时间的记录（保留策略），返回删除行数。</summary>
    public async Task<int> DeleteOlderThanAsync(DateTimeOffset threshold, CancellationToken ct = default)
    {
        if (!_options.Enabled)
        {
            return 0;
        }

        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

            var deleted = await db.Events
                .Where(e => e.EventTime < threshold)
                .ExecuteDeleteAsync(ct)
                .ConfigureAwait(false);

            if (deleted > 0)
            {
                // 行数变了：让统计缓存立即失效。
                Interlocked.Exchange(ref _totalRowsCache, -1);
                _logger.LogInformation("事件保留策略：已删除 {Count} 条早于 {Threshold} 的事件。", deleted, threshold);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            SetError(ex, "清理历史事件失败");
            _logger.LogWarning(ex, "清理历史事件失败。");
            return 0;
        }
    }

    /// <summary>去重键 → SHA-256 十六进制（唯一索引列）。</summary>
    public static string ComputeDedupHash(string dedupKey)
    {
        var key = string.IsNullOrEmpty(dedupKey) ? "?" : dedupKey;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hash);
    }

    private async Task<int> SaveAsync(AcsEventDbContext db, List<AcsEventRecord> records, CancellationToken ct)
    {
        db.Events.AddRange(records);

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return records.Count;
        }
        catch (DbUpdateException ex)
        {
            // 唯一索引冲突（并发写入或历史残留）或其它批量写入错误：退化为逐条写入，冲突行按重复跳过。
            _logger.LogDebug(ex, "事件批量写入出现冲突，改为逐条写入。");

            db.ChangeTracker.Clear();

            var inserted = 0;
            var duplicated = 0;

            foreach (var record in records)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }

                db.Events.Add(record);
                try
                {
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    inserted++;
                }
                catch (DbUpdateException)
                {
                    duplicated++;
                    db.ChangeTracker.Clear();
                }
            }

            if (duplicated > 0)
            {
                Interlocked.Add(ref _duplicates, duplicated);
            }

            return inserted;
        }
    }

    private AcsEventRecord BuildRecord(AcsQueueEnvelope envelope, string? queueProvider, DateTimeOffset now)
    {
        var evt = envelope.Event;

        var record = new AcsEventRecord
        {
            DedupKey = Truncate(envelope.DedupKey, 200) ?? string.Empty,
            // 跳过去重时把哈希唯一化：同一条事件也允许重复落库（唯一索引不再拦截）。
            DedupHash = envelope.SkipDedup
                ? ComputeDedupHash(envelope.DedupKey + "#" + Guid.NewGuid().ToString("N"))
                : ComputeDedupHash(envelope.DedupKey),
            DeviceId = Truncate(evt.DeviceId ?? string.Empty, 128) ?? string.Empty,
            Source = Truncate(string.IsNullOrWhiteSpace(evt.Source) ? "Unknown" : evt.Source, 32) ?? "Unknown",
            SerialNumber = Truncate(evt.SerialNumber, 128),
            EventType = Truncate(evt.EventType, 128),
            SubEventType = Truncate(evt.SubEventType, 64),
            AlarmType = evt.AlarmType,
            DataType = evt.DataType,
            PayloadFormat = Truncate(string.IsNullOrWhiteSpace(evt.PayloadFormat) ? "json" : evt.PayloadFormat, 16) ?? "json",
            PayloadBytes = evt.PayloadBytes,
            IsAccessControl = evt.IsAccessControl,
            HttpUrl = Truncate(evt.HttpUrl, 512),
            EventTime = evt.ReceivedAt == default ? now : evt.ReceivedAt,
            ReceivedAt = evt.ReceivedAt == default ? now : evt.ReceivedAt,
            EnqueuedAt = envelope.EnqueuedAt,
            StoredAt = now,
            QueueProvider = queueProvider,
            QueueMessageId = Truncate(envelope.MessageId, 128),
            DeliveryAttempts = Math.Max(envelope.Attempt, 1),
        };

        var (payload, truncated) = PreparePayload(evt.Payload);
        record.Payload = payload;
        record.PayloadTruncated = truncated;

        AcsEventPayloadExtractor.Fill(record, evt);

        return record;
    }

    private (string? Payload, bool Truncated) PreparePayload(string? payload)
    {
        if (!_options.StorePayload || string.IsNullOrEmpty(payload))
        {
            return (null, false);
        }

        var max = _options.MaxPayloadBytes;
        if (max <= 0 || payload.Length <= max)
        {
            return (payload, false);
        }

        // 按字符截断（UTF-8 下极少出现半个字符问题：这里保守地只截字符，不做字节精确切分）。
        return (payload[..max], true);
    }

    private void SetError(Exception ex, string context)
    {
        _lastError = $"{context}：{ex.GetType().Name}: {ex.Message}";
        _lastErrorAt = DateTimeOffset.UtcNow;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
