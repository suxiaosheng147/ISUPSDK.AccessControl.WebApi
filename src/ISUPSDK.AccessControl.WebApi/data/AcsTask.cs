namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>任务归属于哪类对象。</summary>
public enum AcsTaskEntityType
{
    Person = 1,
    Card = 2,
    Face = 3,
    FaceLibrary = 4,
}

/// <summary>任务对应的具体操作（与 Persons/Cards/Faces 的写端点一一对应）。</summary>
public enum AcsTaskOperation
{
    // ---- 人员 ----
    PersonAdd = 101,
    PersonUpdate = 102,
    PersonSetUp = 103,
    PersonBatchAdd = 104,
    PersonBatchUpdate = 105,
    PersonDelete = 106,
    PersonDeleteAll = 107,

    // ---- 卡片 ----
    CardAdd = 201,
    CardUpdate = 202,
    CardBatchAdd = 203,
    CardBatchUpdate = 204,
    CardDelete = 205,
    CardDeleteByEmployee = 206,
    CardDeleteAll = 207,

    // ---- 人脸 / 人脸库 ----
    FaceUpload = 301,
    FaceUploadByImage = 302,
    FaceBatchUpload = 303,
    FaceUploadByUrl = 304,
    FaceDelete = 305,
    FaceLibraryCreate = 306,
    FaceLibraryUpdate = 307,
    FaceLibraryDelete = 308,
}

/// <summary>任务状态。</summary>
public enum AcsTaskStatus
{
    /// <summary>待执行（含"等待设备上线"）。</summary>
    Pending = 0,

    /// <summary>执行中。</summary>
    Running = 1,

    /// <summary>成功（设备已接受）。</summary>
    Succeeded = 2,

    /// <summary>失败（设备拒绝，或临时错误超过重试上限）。</summary>
    Failed = 3,

    /// <summary>已取消。</summary>
    Canceled = 4,
}

/// <summary>
/// 一条下发的异步任务（EF Core 实体，落 <c>ISUP:TaskQueue</c> 指定的数据库：Sqlite / MySql / SqlServer）。
///
/// 写操作（人员/卡片/人脸的新增、修改、删除）先入库为 Pending，由
/// <c>AcsTaskWorker</c> 定时取出执行；调用方凭 <see cref="Id"/> 查询执行结果。
/// </summary>
public sealed class AcsTask
{
    /// <summary>任务 ID（UUID，非自增）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>目标设备 ID（序列号）。</summary>
    public string DeviceId { get; set; } = string.Empty;

    public AcsTaskEntityType Entity { get; set; }

    public AcsTaskOperation Operation { get; set; }

    public AcsTaskStatus Status { get; set; } = AcsTaskStatus.Pending;

    /// <summary>请求体原文（JSON）；人脸图片上传时为人脸元数据 JSON。</summary>
    public string? Payload { get; set; }

    /// <summary>
    /// 请求体原文格式：<c>json</c> 或 <c>xml</c>（执行时按此选择解析方式）。
    /// 入队时由 <see cref="AcsPayloadFormats.Detect"/> 按内容自动判定。
    /// </summary>
    public string PayloadFormat { get; set; } = AcsPayloadFormats.Json;

    /// <summary>人脸图片在本地磁盘的路径（仅人脸图片上传类任务）。</summary>
    public string? BinaryPath { get; set; }

    /// <summary>透传超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 15000;

    /// <summary>已执行次数。</summary>
    public int Attempts { get; set; }

    /// <summary>临时性错误（网络/SDK 超时等）的最大重试次数。</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>设备是否接受了本次操作。</summary>
    public bool DeviceSuccess { get; set; }

    /// <summary>设备返回的状态码（1=OK、6=Invalid Content 等，0 表示未知）。</summary>
    public int DeviceStatusCode { get; set; }

    /// <summary>设备返回的 subStatusCode。</summary>
    public string? SubStatusCode { get; set; }

    /// <summary>执行结果（设备原始响应的 JSON 摘要）。</summary>
    public string? ResultJson { get; set; }

    /// <summary>失败原因。</summary>
    public string? Error { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>下次可执行时间（用于设备离线时的等待退避）。</summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>可读描述（便于列表展示）。</summary>
    public string? Description { get; set; }
}


/// <summary>请求体原文格式常量。</summary>
public static class AcsPayloadFormats
{
    public const string Json = "json";
    public const string Xml = "xml";

    /// <summary>
    /// 按内容判断请求体格式：以 <c>&lt;</c> 开头视为 XML，其余（含空）视为 JSON。
    /// </summary>
    public static string Detect(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return Json;
        }

        return payload.TrimStart().StartsWith('<') ? Xml : Json;
    }
}
