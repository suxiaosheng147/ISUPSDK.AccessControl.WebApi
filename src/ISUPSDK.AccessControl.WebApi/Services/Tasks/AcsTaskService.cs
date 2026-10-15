using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Tasks;

/// <summary>
/// 异步下发任务的持久化与查询（EF Core + SQLite）。
/// 入队、列表、查询、重试、取消都走这里；后台执行由 <see cref="AcsTaskWorker"/> 驱动。
/// </summary>
public sealed class AcsTaskService
{
    private readonly IDbContextFactory<AcsTaskDbContext> _factory;
    private readonly AcsTaskQueueOptions _options;
    private readonly ILogger<AcsTaskService> _logger;

    /// <summary>串行化"取任务 + 标记 Running"，避免多轮并发重复执行。</summary>
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

    /// <summary>取出到期可执行的任务并标记为 Running（供后台执行器使用）。</summary>
    public async Task<List<AcsTask>> TakeDueAsync(int batchSize, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = await _factory.CreateDbContextAsync(ct).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;

            var due = await db.Tasks
                .Where(t => t.Status == AcsTaskStatus.Pending && t.NextAttemptAt <= now)
                .OrderBy(t => t.CreatedAt)
                .Take(Math.Clamp(batchSize, 1, 500))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (due.Count == 0)
            {
                return due;
            }

            foreach (var task in due)
            {
                task.Status = AcsTaskStatus.Running;
                task.StartedAt = now;
            }

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return due;
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
