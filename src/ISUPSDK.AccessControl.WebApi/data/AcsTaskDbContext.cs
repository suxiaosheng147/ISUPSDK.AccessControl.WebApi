using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 异步下发任务的 EF Core 上下文（Sqlite / MySql / SqlServer 共用同一份实体定义；
/// provider 由 <c>ISUP:TaskQueue:Provider</c> 选择，见 <c>AcsTaskStoreRegistration</c>）。
/// </summary>
public sealed class AcsTaskDbContext : DbContext
{
    /// <summary>
    /// SQLite / MySQL / SQL Server 对 DateTimeOffset 的支持不一致，统一转成 UTC ticks（INTEGER/BIGINT）存储
    /// （绝对时刻），读出时按 <c>ISUP:Time:TimeZone</c>（默认北京时间）还原偏移。
    /// </summary>
    private readonly ValueConverter<DateTimeOffset, long> _dateTimeOffsetConverter;

    private readonly ValueConverter<DateTimeOffset?, long?> _nullableDateTimeOffsetConverter;

    public AcsTaskDbContext(DbContextOptions<AcsTaskDbContext> options, IOptions<IsupTimeOptions> timeOptions)
        : base(options)
    {
        var offset = timeOptions.Value.ResolveOffset();

        _dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero).ToOffset(offset));

        _nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero).ToOffset(offset) : null);
    }

    public DbSet<AcsTask> Tasks => Set<AcsTask>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<AcsTask>();

        task.ToTable("AcsTasks");
        task.HasKey(t => t.Id);

        // 主键为 UUID（应用生成，非自增）。
        task.Property(t => t.Id).HasMaxLength(36).ValueGeneratedNever();

        task.Property(t => t.DeviceId).HasMaxLength(128).IsRequired();
        task.Property(t => t.Entity).HasConversion<int>();
        task.Property(t => t.Operation).HasConversion<int>();
        task.Property(t => t.Status).HasConversion<int>();
        task.Property(t => t.SubStatusCode).HasMaxLength(128);
        task.Property(t => t.PayloadFormat).HasMaxLength(16);
        task.Property(t => t.Source).HasMaxLength(64);
        task.Property(t => t.Error).HasMaxLength(2048);
        task.Property(t => t.Description).HasMaxLength(512);

        task.Property(t => t.CreatedAt).HasConversion(_dateTimeOffsetConverter).IsRequired();
        task.Property(t => t.NextAttemptAt).HasConversion(_dateTimeOffsetConverter).IsRequired();
        task.Property(t => t.StartedAt).HasConversion(_nullableDateTimeOffsetConverter);
        task.Property(t => t.CompletedAt).HasConversion(_nullableDateTimeOffsetConverter);

        task.HasIndex(t => t.Status);
        task.HasIndex(t => new { t.Status, t.NextAttemptAt });
        task.HasIndex(t => t.DeviceId);
        task.HasIndex(t => t.CreatedAt);
    }
}
