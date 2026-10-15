namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>门禁事件消息队列后端类型。</summary>
public enum AcsEventQueueProvider
{
    /// <summary>不使用外部消息队列：事件在进程内经内存通道直通给消费者（仍然写库，便于单机部署）。</summary>
    None = 0,

    /// <summary>Windows 自带消息队列（Microsoft Message Queuing，mqrt.dll）。</summary>
    Msmq = 1,

    /// <summary>RabbitMQ（AMQP 0-9-1，RabbitMQ.Client）。</summary>
    RabbitMq = 2,
}

/// <summary>
/// 门禁事件的消息队列中转配置（对应 appsettings.json 的 <c>ISUP:EventQueue</c>）。
///
/// 数据流：事件接收（HTTP 推送 / AMS 回调）→ 落盘 JSONL（<c>AcsEventSpool</c>）→
/// 发布到本配置指定的队列 → 同进程消费者批量写入数据库（<c>ISUP:EventStore</c>）。
/// <c>Provider=None</c> 时跳过队列，直接由进程内消费者写库。
/// </summary>
public sealed class AcsEventQueueOptions
{
    public const string SectionName = "ISUP:EventQueue";

    /// <summary>是否启用事件队列链路（false 时事件只进内存缓冲与落盘文件，不写数据库）。</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>队列后端：None / Msmq / RabbitMq（不区分大小写，无法识别时回退 None 并告警）。</summary>
    public string Provider { get; set; } = nameof(AcsEventQueueProvider.None);

    /// <summary>队列名（MSMQ 用它拼私有队列路径；RabbitMQ 用它作 queue 名）。</summary>
    public string QueueName { get; set; } = "acs-events";

    /// <summary>是否把 AMS 通道（<c>OTAP_AMS_StartListen</c> 回调）的报警事件也投递到队列。</summary>
    public bool IncludeAmsEvents { get; set; } = true;

    /// <summary>是否只投递门禁（AccessControl）相关事件。false 表示报警/异常/操作事件一并入库。</summary>
    public bool AccessControlOnly { get; set; }

    /// <summary>等待发布的内存缓冲上限（超过时丢弃最旧事件，仅记警告；事件仍留在落盘文件中）。</summary>
    public int PendingBufferSize { get; set; } = 50000;

    /// <summary>发布失败的重试间隔（秒）。</summary>
    public int PublishRetryDelaySeconds { get; set; } = 3;

    /// <summary>单条消息的发布重试上限（超过后交给落盘文件兜底重放）。</summary>
    public int PublishMaxAttempts { get; set; } = 5;

    /// <summary>死信记录文件（重试超限或无法反序列化的消息写到这里，便于事后排查）。</summary>
    public string DeadLetterFile { get; set; } = "./logs/acs-queue/deadletter.jsonl";

    /// <summary>MSMQ 后端配置。</summary>
    public MsmqOptions Msmq { get; set; } = new();

    /// <summary>RabbitMQ 后端配置。</summary>
    public RabbitMqOptions RabbitMq { get; set; } = new();

    /// <summary>消费端配置。</summary>
    public ConsumerOptions Consumer { get; set; } = new();

    /// <summary>解析 <see cref="Provider"/>（无法识别时回退 <see cref="AcsEventQueueProvider.None"/>）。</summary>
    public AcsEventQueueProvider ResolveProvider()
    {
        if (Enum.TryParse<AcsEventQueueProvider>(Provider, ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        return AcsEventQueueProvider.None;
    }

    /// <summary>MSMQ 队列路径（<c>.\Private$\acs-events</c> 或 <c>FormatName:DIRECT=...</c>）。</summary>
    public string ResolveMsmqQueuePath()
        => string.IsNullOrWhiteSpace(Msmq.QueuePath)
            ? $@".\Private$\{(string.IsNullOrWhiteSpace(QueueName) ? "acs-events" : QueueName)}"
            : Msmq.QueuePath.Trim();

    /// <summary>MSMQ 队列配置。</summary>
    public sealed class MsmqOptions
    {
        /// <summary>队列路径；留空时用 <c>.\Private$\{QueueName}</c>。</summary>
        public string QueuePath { get; set; } = string.Empty;

        /// <summary>队列不存在时是否自动创建（MQCreateQueue）。</summary>
        public bool CreateIfMissing { get; set; } = true;

        /// <summary>
        /// 是否创建事务性队列。默认 false：本服务的消费语义是"接收后处理失败则重投"，
        /// 而事务性接收需要 DTC（分布式事务），会带来额外部署依赖。
        /// </summary>
        public bool Transactional { get; set; }

        /// <summary>单次接收等待超时（毫秒）。</summary>
        public int ReceiveTimeoutMs { get; set; } = 2000;

        /// <summary>单条消息体上限（字节）。MSMQ 单消息上限 4MB。</summary>
        public int MaxMessageBytes { get; set; } = 4 * 1024 * 1024;

        /// <summary>消息标签前缀（便于在"计算机管理 → 消息队列"里辨认）。</summary>
        public string LabelPrefix { get; set; } = "acs-event:";

        /// <summary>断线/队列被删后的重连间隔（秒）。</summary>
        public int ReconnectDelaySeconds { get; set; } = 5;

        /// <summary>
        /// 队列存活巡检间隔（秒）：MSMQ 的句柄在队列被删除后不会立刻失效（接收只是超时），
        /// 因此收件循环每隔该间隔确认一次队列是否还在，不在则重建。
        /// </summary>
        public int QueueCheckSeconds { get; set; } = 30;
    }

    /// <summary>RabbitMQ 队列配置。</summary>
    public sealed class RabbitMqOptions
    {
        public string HostName { get; set; } = "127.0.0.1";

        public int Port { get; set; } = 5672;

        public string VirtualHost { get; set; } = "/";

        public string UserName { get; set; } = "guest";

        public string Password { get; set; } = "guest";

        /// <summary>是否使用 TLS（amqps）。</summary>
        public bool UseTls { get; set; }

        /// <summary>队列是否持久化（durable）。false 时 RabbitMQ 重启会丢失队列。</summary>
        public bool Durable { get; set; } = true;

        /// <summary>消费预取条数（basic.qos），控制单消费者在途消息数。</summary>
        public ushort PrefetchCount { get; set; } = 50;

        /// <summary>单条消息体上限（字节）。</summary>
        public int MaxMessageBytes { get; set; } = 4 * 1024 * 1024;

        /// <summary>断线重连间隔（秒）。</summary>
        public int ReconnectDelaySeconds { get; set; } = 5;

        /// <summary>是否声明死信交换机/队列并把重试超限的消息投递到 <c>{QueueName}.dead</c>。</summary>
        public bool DeadLetterEnabled { get; set; } = true;
    }

    /// <summary>消费端配置。</summary>
    public sealed class ConsumerOptions
    {
        /// <summary>是否启动消费者（false 时只发布不消费，适合把入库放到独立进程）。</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>批量写库的最大条数。</summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>批量写库的最长等待（毫秒），凑不满 <see cref="BatchSize"/> 也落库。</summary>
        public int FlushIntervalMs { get; set; } = 1000;

        /// <summary>单条消息的处理重试上限（超过后进入死信/丢弃，并记警告）。</summary>
        public int MaxAttempts { get; set; } = 5;

        /// <summary>重投前的等待（秒），避免失败消息热循环。</summary>
        public int RetryDelaySeconds { get; set; } = 5;

        /// <summary>
        /// 是否在启动时从落盘 JSONL 重放尚未入库的事件。
        /// 用于兜底"队列不可用期间发布失败"或"消息在途丢失"的情况（入库按 DedupHash 幂等）。
        /// </summary>
        public bool ReplaySpoolOnStartup { get; set; } = true;

        /// <summary>重放时最多回看的落盘文件天数（0 表示全部）。</summary>
        public int ReplaySpoolDays { get; set; } = 2;
    }
}
