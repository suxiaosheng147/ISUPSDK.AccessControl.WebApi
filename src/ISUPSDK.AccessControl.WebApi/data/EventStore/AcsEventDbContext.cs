using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.Options;
namespace ISUPSDK.AccessControl.WebApi.Data.EventStore;

/// <summary>
/// 门禁事件落库的 EF Core 上下文。
///
/// provider（Sqlite / MySql / SqlServer）在 <c>Program.cs</c> 按配置选择，
/// 本上下文只负责模型映射，因此三库共用同一份实体定义。
/// </summary>
public sealed class AcsEventDbContext : DbContext
{
    /// <summary>
    /// SQLite / MySQL / SQL Server 对 DateTimeOffset 的支持不一致（SQLite 无原生类型，
    /// MySQL 又受时区影响），统一转成 UTC ticks（INTEGER/BIGINT）存储，保证三库结果一致，
    /// 且记录的是**绝对时刻**（不受部署时区影响）。
    /// 读出时按 <c>ISUP:Time:TimeZone</c>（默认北京时间）还原偏移，便于现场直接阅读。
    /// </summary>
    private readonly ValueConverter<DateTimeOffset, long> _dateTimeOffsetConverter;

    private readonly ValueConverter<DateTimeOffset?, long?> _nullableDateTimeOffsetConverter;

    private readonly AcsEventStoreOptions _options;

    public AcsEventDbContext(
        DbContextOptions<AcsEventDbContext> options,
        IOptions<AcsEventStoreOptions> storeOptions,
        IOptions<IsupTimeOptions> timeOptions)
        : base(options)
    {
        _options = storeOptions.Value;

        var offset = timeOptions.Value.ResolveOffset();

        _dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, long>(
            v => v.UtcTicks,
            v => new DateTimeOffset(v, TimeSpan.Zero).ToOffset(offset));

        _nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.UtcTicks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero).ToOffset(offset) : null);
    }

    public DbSet<AcsEventRecord> Events => Set<AcsEventRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var tableName = string.IsNullOrWhiteSpace(_options.TableName) ? "AcsEvents" : _options.TableName.Trim();

        var evt = modelBuilder.Entity<AcsEventRecord>();

        evt.ToTable(tableName);
        evt.HasKey(e => e.Id);
        evt.Property(e => e.Id).ValueGeneratedOnAdd();

        evt.Property(e => e.DedupKey).HasMaxLength(200).IsRequired();
        evt.Property(e => e.DedupHash).HasMaxLength(64).IsRequired();
        evt.Property(e => e.DeviceId).HasMaxLength(128).IsRequired();
        evt.Property(e => e.Source).HasMaxLength(32).IsRequired();
        evt.Property(e => e.SerialNumber).HasMaxLength(128);
        evt.Property(e => e.EventSerialNo).HasMaxLength(64);
        evt.Property(e => e.EventType).HasMaxLength(128);
        evt.Property(e => e.SubEventType).HasMaxLength(64);
        evt.Property(e => e.EmployeeNo).HasMaxLength(64);
        evt.Property(e => e.CardNo).HasMaxLength(64);
        evt.Property(e => e.PersonName).HasMaxLength(128);
        evt.Property(e => e.VerifyMode).HasMaxLength(64);
        evt.Property(e => e.AlarmTopic).HasMaxLength(256);
        evt.Property(e => e.PayloadFormat).HasMaxLength(16).IsRequired();
        evt.Property(e => e.HttpUrl).HasMaxLength(512);
        evt.Property(e => e.QueueProvider).HasMaxLength(32);
        evt.Property(e => e.QueueMessageId).HasMaxLength(128);

        // 报文原文按需映射为大字段（Sqlite TEXT / MySql LONGTEXT / SqlServer nvarchar(max)）。
        evt.Property(e => e.Payload);
        evt.Property(e => e.PictureFileNames);

        // 外部图片直链（SDK 报警回调的 PicDataUrl；图片服务器可直接访问，平台不下载）。
        evt.Property(e => e.PictureUrl).HasMaxLength(512);

        evt.Property(e => e.EventTime).HasConversion(_dateTimeOffsetConverter).IsRequired();
        evt.Property(e => e.ReceivedAt).HasConversion(_dateTimeOffsetConverter).IsRequired();
        evt.Property(e => e.EnqueuedAt).HasConversion(_dateTimeOffsetConverter).IsRequired();
        evt.Property(e => e.StoredAt).HasConversion(_dateTimeOffsetConverter).IsRequired();

        // 幂等关键：去重键哈希唯一。
        evt.HasIndex(e => e.DedupHash).IsUnique().HasDatabaseName($"UX_{tableName}_DedupHash");

        evt.HasIndex(e => e.ReceivedAt).HasDatabaseName($"IX_{tableName}_ReceivedAt");
        evt.HasIndex(e => e.EventTime).HasDatabaseName($"IX_{tableName}_EventTime");
        evt.HasIndex(e => new { e.DeviceId, e.EventTime }).HasDatabaseName($"IX_{tableName}_Device_EventTime");
        evt.HasIndex(e => new { e.IsAccessControl, e.EventTime }).HasDatabaseName($"IX_{tableName}_Acs_EventTime");
        evt.HasIndex(e => e.EmployeeNo).HasDatabaseName($"IX_{tableName}_EmployeeNo");
    }
}
