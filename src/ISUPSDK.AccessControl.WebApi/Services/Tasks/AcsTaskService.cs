using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Tasks;

/// <summary>
/// 异步下发任务的持久化与查询（EF Core，库由 <c>ISUP:TaskQueue:Provider</c> 选择）。
/// 入队、列表、查询、重试、取消都走这里；后台执行由 <see cref="AcsTaskWorker"/> 驱动。
/// </summary>
public sealed class AcsTaskService
{
    private readonly IDbContextFactory<AcsTaskDbContext> _factory;
    private readonly AcsTaskQueueOptions _options;
    private readonly ILogger<AcsTaskService> _logger;

    /// <summary>
    /// 单实例内串行化"取任务"（减少同一实例并发调用时的无谓争抢）；
    /// 跨实例 / 跨进程的互斥由数据库层的条件更新保证，见 <see cref="TakeDueAsync"/>。
    /// </summary>
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AcsTaskService(
        IDbContextFactory<AcsTaskDbContext> factory,
        IOptions<AcsTaskQueueOptions> options,
        ILogger<AcsTaskService> logger)
    {
        _factory = factory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    /// <summary>把一次写操作入队，返回任务（含自增 ID）。</summary>
    public async Task<AcsTask> EnqueueAsync(
        string deviceId,
        AcsTaskEntityType entity,
        AcsTaskOperation operation,
        object? payload,
        uint timeoutMs,
        string? binaryPath = null,
        string? description = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var payloadJson = payload is null ? null : JsonSerializer.Serialize(payload, IsupJson.Options);
        var task = new AcsTask
        {
            Id = Guid.NewGuid().ToString(),
            DeviceId = deviceId,
            Entity = entity,
            Operation = operation,
            Status = AcsTaskStatus.Pending,
            Payload = payloadJson,
            PayloadFormat = AcsPayloadFormats.Detect(payloadJson),
            BinaryPath = binaryPath,
            TimeoutMs = timeoutMs,
            MaxAttempts = Math.Max(_options.MaxAttempts, 1),
            CreatedAt = now,
            NextAttemptAt = now,
            Description = description,
        };

        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation(
            "任务已入队：Id={Id}，设备={DeviceId}，操作={Operation}",
            task.Id,
            task.DeviceId,
            task.Operation);

        return task;
    }

    /// <summary>把人脸图片等二进制附件保存到本地磁盘，返回文件路径（入队时调用）。</summary>
    public async Task<string> SaveBinaryAsync(byte[] bytes, string extension = ".bin", CancellationToken ct = default)
    {
        Directory.CreateDirectory(_options.BinaryDirectory);
        var name = $"task-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var path = Path.Combine(_options.BinaryDirectory, name);
        await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);
        return path;
    }

    /// <summary>各状态任务数量（供 /health 展示）。</summary>
    public async Task<(int Pending, int Running, int Succeeded, int Failed)> GetCountsAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var pending = await db.Tasks.CountAsync(t => t.Status == AcsTaskStatus.Pending, ct).ConfigureAwait(false);
        var running = await db.Tasks.CountAsync(t => t.Status == AcsTaskStatus.Running, ct).ConfigureAwait(false);
        var succeeded = await db.Tasks.CountAsync(t => t.Status == AcsTaskStatus.Succeeded, ct).ConfigureAwait(false);
        var failed = await db.Tasks.CountAsync(t => t.Status == AcsTaskStatus.Failed, ct).ConfigureAwait(false);
        return (pending, running, succeeded, failed);
    }

    public async Task<AcsTask?> GetAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        return await db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<List<AcsTask>> ListAsync(
        string? deviceId,
        AcsTaskEntityType? entity,
        AcsTaskStatus? status,
        int limit,
        CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var query = db.Tasks.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            query = query.Where(t => t.DeviceId == deviceId);
        }

        if (entity is { } e)
        {
            query = query.Where(t => t.Entity == e);
        }

        if (status is { } s)
        {
            query = query.Where(t => t.Status == s);
        }

        return await query
            .OrderByDescending(t => t.Id)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>把失败/取消的任务重新排队执行。</summary>
    public async Task<bool> RetryAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (task is null)
        {
            return false;
        }

        task.Status = AcsTaskStatus.Pending;
        task.NextAttemptAt = DateTimeOffset.UtcNow;
        task.CompletedAt = null;
        task.Error = null;
        task.Attempts = 0;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>取消一个尚未执行（Pending）的任务。</summary>
    public async Task<bool> CancelAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (task is null || task.Status != AcsTaskStatus.Pending)
        {
            return false;
        }

        task.Status = AcsTaskStatus.Canceled;
        task.CompletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// 取出到期可执行的任务并原子地标记为 Running（供后台执行器使用）。
    ///
    /// "取"分两步：先按到期条件取候选 ID，再逐条做 <c>Status = Pending</c> 的条件更新。
    /// 只有条件更新影响 1 行的实例才真正拿到该任务，因此多个进程 / 多台机器共享同一个任务库时，
    /// 同一条任务只会被其中一个实例执行一次（原先的"查出实体 → 改状态 → SaveChanges"
    /// 在数据库层面不是原子的，多实例会重复下发）。
    ///
    /// 之所以逐条更新、而不是一条批量更新：批量更新只返回总影响行数，无法判断"哪些行归我"
    /// （用时间戳之类的标记不可靠 —— 同进程内的两个实例可能取到相同的时间戳），
    /// 而逐条更新的返回值为 1 就是明确的归属。单轮最多 <c>batchSize</c>（默认 20）次主键小更新，
    /// 代价可接受。
    ///
    /// 已知限制：实例在执行过程中崩溃时任务会停在 Running，需要人工重试
    /// （<c>POST /api/tasks/{id}/retry</c>）。
    /// </summary>
    public async Task<List<AcsTask>> TakeDueAsync(int batchSize, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            var candidates = await db.Tasks
                .AsNoTracking()
                .Where(t => t.Status == AcsTaskStatus.Pending && t.NextAttemptAt <= now)
                .OrderBy(t => t.CreatedAt)
                .Take(Math.Clamp(batchSize, 1, 500))
                .Select(t => t.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (candidates.Count == 0)
            {
                return new List<AcsTask>();
            }

            var claimed = new List<string>(candidates.Count);

            foreach (var id in candidates)
            {
                // 条件更新：只有把该行从 Pending 改成 Running 成功的实例才拿到它（影响行数为 0 = 被抢先）。
                var affected = await db.Tasks
                    .Where(t => t.Id == id && t.Status == AcsTaskStatus.Pending)
                    .ExecuteUpdateAsync(
                        setters => setters
                            .SetProperty(t => t.Status, AcsTaskStatus.Running)
                            .SetProperty(t => t.StartedAt, now),
                        ct)
                    .ConfigureAwait(false);

                if (affected == 1)
                {
                    claimed.Add(id);
                }
            }

            if (claimed.Count == 0)
            {
                return new List<AcsTask>();
            }

            return await db.Tasks
                .AsNoTracking()
                .Where(t => claimed.Contains(t.Id))
                .ToListAsync(ct)
                .ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>保存任务的最新状态。</summary>
    public async Task SaveAsync(AcsTask task, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
        db.Tasks.Update(task);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>清理已结束且超过保留期的任务，返回删除条数。</summary>
    public async Task<int> CleanupAsync(int retentionDays, CancellationToken ct = default)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var old = await db.Tasks
            .Where(t => t.CompletedAt != null && t.CompletedAt < cutoff)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (old.Count == 0)
        {
            return 0;
        }

        db.Tasks.RemoveRange(old);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        return old.Count;
    }
}
