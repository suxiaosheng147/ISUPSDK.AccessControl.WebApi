namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>异步下发任务库使用的数据库类型。</summary>
public enum AcsTaskStoreProvider
{
    /// <summary>SQLite 单文件库（默认，零外部依赖）。</summary>
    Sqlite = 0,

    /// <summary>MySQL / MariaDB（Pomelo.EntityFrameworkCore.MySql）。</summary>
    MySql = 1,

    /// <summary>SQL Server（Microsoft.EntityFrameworkCore.SqlServer）。</summary>
    SqlServer = 2,
}

/// <summary>
/// 异步下发任务队列配置（对应 appsettings.json 的 <c>ISUP:TaskQueue</c>）。
///
/// 人员/卡片/人脸的新增、修改、删除端点会把请求入库为任务并立即返回任务 ID，
/// 由后台定时任务执行；调用方凭任务 ID 查询结果。
///
/// 任务库与事件库（<c>ISUP:EventStore</c>）是两套独立的数据库配置，
/// 各自可用 <c>Provider</c> 选择 Sqlite / MySql / SqlServer，互不影响。
/// </summary>
public sealed class AcsTaskQueueOptions
{
    public const string SectionName = "ISUP:TaskQueue";

    /// <summary>是否启用任务队列（关闭后写端点会因无执行者而一直待执行，故默认开启）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>任务库类型：Sqlite / MySql / SqlServer（不区分大小写，无法识别时回退 Sqlite 并告警）。</summary>
    public string Provider { get; set; } = nameof(AcsTaskStoreProvider.Sqlite);

    /// <summary>连接串（MySql / SqlServer 必填；Sqlite 留空时用 <see cref="DatabasePath"/> 生成）。</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>MySQL 服务端版本（Pomelo 必需；不做自动探测，避免数据库未就绪时启动失败）。</summary>
    public string ServerVersion { get; set; } = "8.0.36";

    /// <summary>SQLite 数据库文件路径（仅 <c>Provider=Sqlite</c> 使用）。</summary>
    public string DatabasePath { get; set; } = "./storage/acs-tasks.db";

    /// <summary>单条命令超时（秒）。</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>后台执行轮询间隔（秒）。</summary>
    public int PollSeconds { get; set; } = 5;

    /// <summary>设备离线时，任务重新排期的间隔（秒）。</summary>
    public int OfflineRetrySeconds { get; set; } = 15;

    /// <summary>临时性错误（网络/SDK 超时）的最大重试次数。</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// 已结束任务（<c>CompletedAt</c> 有值：<c>Succeeded</c> / <c>Failed</c> / <c>Canceled</c>）的保留天数；
    /// 0 表示不自动清理。<c>Pending</c> / <c>Running</c> 的任务永远不清理。
    /// </summary>
    public int CompletedRetentionDays { get; set; } = 30;

    /// <summary>任务清理的执行间隔（小时）；最小按 1 小时执行，避免过于频繁地扫库。</summary>
    public int RetentionSweepHours { get; set; } = 1;

    /// <summary>人脸图片等二进制附件存放目录。</summary>
    public string BinaryDirectory { get; set; } = "./storage/task-files/";

    /// <summary>单轮最多处理的任务数。</summary>
    public int BatchSize { get; set; } = 20;

    /// <summary>解析 <see cref="Provider"/>（无法识别时回退 <see cref="AcsTaskStoreProvider.Sqlite"/>）。</summary>
    public AcsTaskStoreProvider ResolveProvider()
    {
        if (Enum.TryParse<AcsTaskStoreProvider>(Provider, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AcsTaskStoreProvider.Sqlite;
    }
}
