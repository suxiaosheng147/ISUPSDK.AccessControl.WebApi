namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 异步下发任务队列配置（对应 appsettings.json 的 <c>ISUP:TaskQueue</c>）。
///
/// 人员/卡片/人脸的新增、修改、删除端点会把请求入库为任务并立即返回任务 ID，
/// 由后台定时任务执行；调用方凭任务 ID 查询结果。
/// </summary>
public sealed class AcsTaskQueueOptions
{
    public const string SectionName = "ISUP:TaskQueue";

    /// <summary>是否启用任务队列（关闭后写端点会因无执行者而一直待执行，故默认开启）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>SQLite 数据库文件路径。</summary>
    public string DatabasePath { get; set; } = "./storage/acs-tasks.db";

    /// <summary>后台执行轮询间隔（秒）。</summary>
    public int PollSeconds { get; set; } = 5;

    /// <summary>设备离线时，任务重新排期的间隔（秒）。</summary>
    public int OfflineRetrySeconds { get; set; } = 15;

    /// <summary>临时性错误（网络/SDK 超时）的最大重试次数。</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>已完成任务的保留天数（0 表示不自动清理）。</summary>
    public int CompletedRetentionDays { get; set; } = 7;

    /// <summary>人脸图片等二进制附件存放目录。</summary>
    public string BinaryDirectory { get; set; } = "./storage/task-files/";

    /// <summary>单轮最多处理的任务数。</summary>
    public int BatchSize { get; set; } = 20;
}
