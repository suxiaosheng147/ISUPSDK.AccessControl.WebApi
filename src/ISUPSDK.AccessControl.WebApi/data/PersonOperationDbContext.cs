using Microsoft.EntityFrameworkCore;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 人员下发队列数据库上下文（支持 SQLite / SQL Server）。
/// <para>
/// 默认使用程序目录下的 <c>isup_person_queue.db</c>（与任务库 <c>acs-tasks.db</c> 相互独立）：
/// 队列表记录"平台下发了什么"，<see cref="PersonOperationTask.TaskIds"/> 指向 AcsTask 的
/// 任务 ID，下发情况可在 <c>/api/tasks</c>（TasksController）查询。
/// </para>
/// </summary>
public sealed class PersonOperationDbContext : DbContext
{
    /// <summary>人员操作任务表。</summary>
    public DbSet<PersonOperationTask> PersonOperationTasks => Set<PersonOperationTask>();

    /// <summary>删除兜底记录表。</summary>
    public DbSet<PersonDeleteVerifyLog> PersonDeleteVerifyLogs => Set<PersonDeleteVerifyLog>();

    /// <summary>构造函数：传入已配置好的选项。</summary>
    public PersonOperationDbContext(DbContextOptions<PersonOperationDbContext> options) : base(options)
    {
    }

    /// <summary>构造函数：使用全局配置的数据库。</summary>
    public PersonOperationDbContext() : base(PersonOperationDatabase.BuildOptions())
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var task = modelBuilder.Entity<PersonOperationTask>();
        task.ToTable("PersonOperationTask");
        task.HasKey(w => w.Id);
        task.Property(w => w.MachineSN).HasMaxLength(64).IsRequired();
        task.Property(w => w.RecordKey).HasMaxLength(200).IsRequired();
        task.Property(w => w.Prefix).HasMaxLength(16);
        task.Property(w => w.LastError).HasMaxLength(1000);
        task.Property(w => w.Payload).IsRequired();
        task.HasIndex(w => new { w.State, w.Id });
        task.HasIndex(w => new { w.MachineSN, w.State });
        task.HasIndex(w => w.RecordKey);

        var verify = modelBuilder.Entity<PersonDeleteVerifyLog>();
        verify.ToTable("PersonDeleteVerifyLog");
        verify.HasKey(w => w.Id);
        verify.Property(w => w.MachineSN).HasMaxLength(64).IsRequired();
        verify.Property(w => w.Prefix).HasMaxLength(16);
        verify.Property(w => w.RecordKey).HasMaxLength(200);
        verify.Property(w => w.Remark).HasMaxLength(1000);
        verify.HasIndex(w => w.TaskId);
        verify.HasIndex(w => new { w.MachineSN, w.State });
    }
}

/// <summary>人员下发队列数据库的配置与上下文创建。</summary>
public static class PersonOperationDatabase
{
    private static readonly object SyncRoot = new();

    private static PersonOperationDatabaseSettings _settings = new();

    /// <summary>配置提供程序（Sqlite / SqlServer）与连接字符串。</summary>
    public static void Configure(PersonOperationDatabaseSettings? settings)
    {
        lock (SyncRoot)
        {
            _settings = settings ?? new PersonOperationDatabaseSettings();
        }
    }

    /// <summary>当前配置。</summary>
    public static PersonOperationDatabaseSettings Settings
    {
        get
        {
            lock (SyncRoot)
            {
                return _settings;
            }
        }
    }

    /// <summary>是否为 SQL Server。</summary>
    public static bool IsSqlServer =>
        string.Equals(Settings.Provider, PersonOperationDatabaseSettings.SqlServer, StringComparison.OrdinalIgnoreCase);

    /// <summary>解析连接字符串，Sqlite 未配置时使用程序目录下的默认文件。</summary>
    public static string ResolveConnectionString()
    {
        var setting = Settings;
        if (!string.IsNullOrWhiteSpace(setting.ConnectionString))
        {
            return setting.ConnectionString!;
        }

        if (IsSqlServer)
        {
            throw new InvalidOperationException("人员下发队列库为 SqlServer 时必须配置连接字符串（Settings:Database:ConnectionString）。");
        }

        var file = string.IsNullOrWhiteSpace(setting.DatabasePath) ? "isup_person_queue.db" : setting.DatabasePath;
        var fullPath = Path.GetFullPath(file, AppContext.BaseDirectory);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return $"Data Source={fullPath};Default Timeout=30";
    }

    /// <summary>构建上下文选项。</summary>
    public static DbContextOptions<PersonOperationDbContext> BuildOptions()
    {
        var builder = new DbContextOptionsBuilder<PersonOperationDbContext>();
        var connectionString = ResolveConnectionString();
        if (IsSqlServer)
        {
            builder.UseSqlServer(connectionString, w => w.EnableRetryOnFailure());
        }
        else
        {
            builder.UseSqlite(connectionString);
        }

        return builder.Options;
    }

    /// <summary>创建上下文。</summary>
    public static PersonOperationDbContext CreateContext() => new(BuildOptions());

    /// <summary>创建表结构（不存在时）。</summary>
    public static void EnsureCreated()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
    }

    /// <summary>
    /// 补齐后加的表/列/索引（老库升级用：EnsureCreated 只在库不存在时建表）。
    /// 所有语句都是幂等的，可以重复执行。
    /// </summary>
    public static void EnsureSchema()
    {
        try
        {
            using var db = CreateContext();
            var scripts = IsSqlServer ? SqlServerSchemaScripts : SqliteSchemaScripts;
            foreach (var sql in scripts)
            {
                // 建表 / 建索引都是 IF NOT EXISTS 写法，重复执行不报错；
                // 只有"加列"语句会因列已存在而报错（新建的库 EnsureCreated 已经带上了列），
                // SQLite 先查表结构再决定是否执行，避免每启动一次就刷一条失败日志。
                if (!IsSqlServer && sql.Contains("ADD COLUMN", StringComparison.OrdinalIgnoreCase) &&
                    !SqliteColumnMissing(db, "PersonOperationTask", "TaskIds"))
                {
                    continue;
                }

                db.Database.ExecuteSqlRaw(sql);
            }
        }
        catch (Exception ex)
        {
            IsupLogWarn($"补齐人员下发队列表结构失败：{ex.Message}");
        }
    }

    /// <summary>SQLite：判断表里是否缺少某一列。</summary>
    private static bool SqliteColumnMissing(PersonOperationDbContext db, string table, string column)
    {
        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;
        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                // 第 2 列是列名
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static readonly string[] SqliteSchemaScripts =
    {
        """
        CREATE TABLE IF NOT EXISTS "PersonDeleteVerifyLog" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_PersonDeleteVerifyLog" PRIMARY KEY AUTOINCREMENT,
            "TaskId" INTEGER NOT NULL,
            "MachineSN" TEXT NOT NULL,
            "Flow" INTEGER NOT NULL,
            "Prefix" TEXT NULL,
            "PersonDataId" INTEGER NULL,
            "SyncDataId" TEXT NULL,
            "RecordKey" TEXT NULL,
            "PersonAddTime" TEXT NULL,
            "AttemptCount" INTEGER NOT NULL,
            "CreateTime" TEXT NOT NULL,
            "State" INTEGER NOT NULL,
            "UpdateTime" TEXT NULL,
            "Remark" TEXT NULL
        );
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_PersonDeleteVerifyLog_TaskId"
            ON "PersonDeleteVerifyLog" ("TaskId");
        """,
        """
        CREATE INDEX IF NOT EXISTS "IX_PersonDeleteVerifyLog_MachineSN_State"
            ON "PersonDeleteVerifyLog" ("MachineSN", "State");
        """,
        // 老库升级：任务 ID 列表（AcsTask.Id 的 JSON 数组）
        """
        ALTER TABLE "PersonOperationTask" ADD COLUMN "TaskIds" TEXT NULL;
        """,
    };

    private static readonly string[] SqlServerSchemaScripts =
    {
        """
        IF OBJECT_ID(N'[PersonDeleteVerifyLog]', N'U') IS NULL
        BEGIN
            CREATE TABLE [PersonDeleteVerifyLog] (
                [Id] bigint NOT NULL IDENTITY,
                [TaskId] bigint NOT NULL,
                [MachineSN] nvarchar(64) NOT NULL,
                [Flow] int NOT NULL,
                [Prefix] nvarchar(16) NULL,
                [PersonDataId] int NULL,
                [SyncDataId] uniqueidentifier NULL,
                [RecordKey] nvarchar(200) NULL,
                [PersonAddTime] datetime2 NULL,
                [AttemptCount] int NOT NULL,
                [CreateTime] datetime2 NOT NULL,
                [State] int NOT NULL,
                [UpdateTime] datetime2 NULL,
                [Remark] nvarchar(1000) NULL,
                CONSTRAINT [PK_PersonDeleteVerifyLog] PRIMARY KEY ([Id])
            );
        END
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PersonDeleteVerifyLog_TaskId'
                       AND object_id = OBJECT_ID(N'[PersonDeleteVerifyLog]'))
            CREATE INDEX [IX_PersonDeleteVerifyLog_TaskId] ON [PersonDeleteVerifyLog] ([TaskId]);
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PersonDeleteVerifyLog_MachineSN_State'
                       AND object_id = OBJECT_ID(N'[PersonDeleteVerifyLog]'))
            CREATE INDEX [IX_PersonDeleteVerifyLog_MachineSN_State] ON [PersonDeleteVerifyLog] ([MachineSN], [State]);
        """,
        // 老库升级：任务 ID 列表（AcsTask.Id 的 JSON 数组）
        """
        IF COL_LENGTH(N'PersonOperationTask', N'TaskIds') IS NULL
            ALTER TABLE [PersonOperationTask] ADD [TaskIds] nvarchar(max) NULL;
        """,
    };

    /// <summary>
    /// 设置 SQLite 日志模式（SQL Server 忽略）。
    /// 默认切回 Delete 模式：正常关闭后只剩 .db 一个文件，不会残留 -wal / -shm；
    /// 若库里残留了 -wal（上次被强制结束），切换时会自动 checkpoint 合并回主库。
    /// 配置 SqliteJournalMode=Wal 时保持 WAL，并做一次 TRUNCATE 检查点控制文件大小。
    /// </summary>
    public static void EnsureJournalMode()
    {
        if (IsSqlServer)
        {
            return;
        }

        var isWal = string.Equals(Settings.SqliteJournalMode, "Wal", StringComparison.OrdinalIgnoreCase);
        var mode = isWal ? "WAL" : "DELETE";

        using var db = CreateContext();
        var connection = db.Database.GetDbConnection();
        db.Database.OpenConnection();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA journal_mode={mode};";
            var applied = command.ExecuteScalar()?.ToString();
            if (!string.Equals(applied, mode, StringComparison.OrdinalIgnoreCase))
            {
                IsupLogWarn($"设置 SQLite 日志模式失败：期望 {mode}，实际 {applied}（可能还有其它连接在访问数据库）");
                return;
            }

            if (isWal)
            {
                // WAL 模式下主动做一次检查点，把 -wal 内容合并回主库
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                command.ExecuteNonQuery();
            }
        }
        finally
        {
            db.Database.CloseConnection();
        }
    }

    private static void IsupLogWarn(string message)
    {
        try
        {
            Services.Logging.IsupLog.WriteHours("PersonOperationQueue.txt", true, message);
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
