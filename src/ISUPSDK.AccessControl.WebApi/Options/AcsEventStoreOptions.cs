namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>门禁事件落库使用的数据库类型。</summary>
public enum AcsEventStoreProvider
{
    /// <summary>SQLite 单文件库（默认，零外部依赖）。</summary>
    Sqlite = 0,

    /// <summary>MySQL / MariaDB（Pomelo.EntityFrameworkCore.MySql）。</summary>
    MySql = 1,

    /// <summary>SQL Server（Microsoft.EntityFrameworkCore.SqlServer）。</summary>
    SqlServer = 2,
}

/// <summary>
/// 门禁事件持久化配置（对应 appsettings.json 的 <c>ISUP:EventStore</c>）。
///
/// 消费者（<c>AcsEventQueueConsumerService</c>）把队列消息批量写入本配置指定的数据库，
/// 并按 <c>DedupHash</c>（= 去重键的 SHA-256）建立唯一索引，保证重复投递/重放不会产生重复行。
/// </summary>
public sealed class AcsEventStoreOptions
{
    public const string SectionName = "ISUP:EventStore";

    /// <summary>是否启用事件落库。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>数据库类型：Sqlite / MySql / SqlServer（不区分大小写，无法识别时回退 Sqlite 并告警）。</summary>
    public string Provider { get; set; } = nameof(AcsEventStoreProvider.Sqlite);

    /// <summary>连接串（MySql / SqlServer 必填；Sqlite 留空时用 <see cref="SqlitePath"/> 生成）。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>MySQL 服务端版本（Pomelo 必需；不做自动探测，避免数据库未就绪时启动失败）。</summary>
    public string ServerVersion { get; set; } = "8.0.36";

    /// <summary>SQLite 数据库文件路径（仅 <c>Provider=Sqlite</c> 使用）。</summary>
    public string SqlitePath { get; set; } = "./storage/acs-events.db";

    /// <summary>是否在启动时自动建库/建表（EnsureCreated）。生产环境可关闭改为人工执行脚本。</summary>
    public bool AutoCreate { get; set; } = true;

    /// <summary>事件表名。</summary>
    public string TableName { get; set; } = "AcsEvents";

    /// <summary>单条命令超时（秒）。</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>是否保存报文原文（Payload）。关闭可显著减小库体积，但会丢失原始报文。</summary>
    public bool StorePayload { get; set; } = true;

    /// <summary>单条事件的报文原文入库上限（字节），超出部分截断（0 表示不截断）。</summary>
    public int MaxPayloadBytes { get; set; } = 256 * 1024;

    /// <summary>事件保留天数（0 表示不清理）。需要配合 <c>RetentionSweepHours</c> 定时清理。</summary>
    public int RetentionDays { get; set; }

    /// <summary>清理任务的执行间隔（小时）。</summary>
    public int RetentionSweepHours { get; set; } = 24;

    /// <summary>解析 <see cref="Provider"/>（无法识别时回退 <see cref="AcsEventStoreProvider.Sqlite"/>）。</summary>
    public AcsEventStoreProvider ResolveProvider()
    {
        if (Enum.TryParse<AcsEventStoreProvider>(Provider, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AcsEventStoreProvider.Sqlite;
    }
}
