namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 人员下发队列数据库配置（支持 SQLite / SQL Server）。
/// </summary>
public sealed class PersonOperationDatabaseSettings
{
    /// <summary>SQLite。</summary>
    public const string Sqlite = "Sqlite";

    /// <summary>SQL Server。</summary>
    public const string SqlServer = "SqlServer";

    /// <summary>提供程序：Sqlite（默认） 或 SqlServer。</summary>
    public string Provider { get; set; } = Sqlite;

    /// <summary>连接字符串；Sqlite 留空时用 <see cref="DatabasePath"/>（相对程序目录解析）。</summary>
    public string? ConnectionString { get; set; }

    /// <summary>SQLite 数据库文件（仅 Sqlite 生效，默认 <c>isup_person_queue.db</c>）。</summary>
    public string DatabasePath { get; set; } = "isup_person_queue.db";

    /// <summary>启动时自动创建表结构。</summary>
    public bool EnsureCreated { get; set; } = true;

    /// <summary>SQLite 日志模式：Delete（默认，正常关闭后只留 .db 一个文件）或 Wal。</summary>
    public string SqliteJournalMode { get; set; } = "Delete";
}

/// <summary>
/// 人员下发队列配置（人员增删改先入库，再由定时任务通过控制器接口下发设备）。
/// </summary>
public sealed class PersonOperationQueueSettings
{
    /// <summary>是否启用队列模式；false 时调用方走原来的实时下发逻辑。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>每轮最多处理多少条任务。</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>同一条任务执行前的间隔（毫秒），避免瞬间压满设备。</summary>
    public int ExecuteIntervalMs { get; set; } = 200;

    /// <summary>任务入队后至少等待多少秒才执行，保证"下发中"回传先发出。</summary>
    public int MinDelaySeconds { get; set; } = 3;

    /// <summary>执行完成后是否回传结果到平台。</summary>
    public bool CallbackEnabled { get; set; } = true;

    /// <summary>已完成任务保留天数，小于等于 0 表示不清理。</summary>
    public int RetentionDays { get; set; } = 7;

    /// <summary>
    /// 任务入队后在 AcsTask 里最长等多久（分钟）视为超时按失败处理，
    /// 避免 AcsTask 一直待执行导致平台侧永远收不到结果。
    /// </summary>
    public int TaskTimeoutMinutes { get; set; } = 60;

    /// <summary>删除兜底配置。</summary>
    public DeleteVerifySettings DeleteVerify { get; set; } = new();
}

/// <summary>
/// 删除兜底配置：按"设备 + 人员"取最新一条任务，若为删除操作则重新下发一次。
/// </summary>
public sealed class DeleteVerifySettings
{
    /// <summary>是否启用删除兜底。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>只处理最近多少小时内完成的任务。</summary>
    public int WindowHours { get; set; } = 24;

    /// <summary>每轮最多兜底多少名人员。</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>
    /// 兜底前先在设备上查询该人员是否真的还存在（true：只在时才重删；false：直接重删）。
    /// 查询失败时本轮跳过、下轮再试，不会误判。
    /// </summary>
    public bool CheckOnDevice { get; set; } = true;

    /// <summary>单次查询设备的人员数（按设备能力限制为 3 个）。</summary>
    public int CheckBatchSize { get; set; } = 3;
}
