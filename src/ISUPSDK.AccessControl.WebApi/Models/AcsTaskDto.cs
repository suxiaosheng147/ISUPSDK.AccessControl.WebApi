using ISUPSDK.AccessControl.WebApi.Data;

namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>异步任务对外的视图（不含 Payload / 本地文件路径）。</summary>
public sealed class AcsTaskDto
{
    public string Id { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string Entity { get; set; } = string.Empty;

    public string Operation { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    /// <summary>任务来源标识（<c>null</c> = 调用方直接调控制器创建）。</summary>
    public string? Source { get; set; }

    public string? Description { get; set; }

    /// <summary>请求体原文格式（json / xml）。</summary>
    public string PayloadFormat { get; set; } = AcsPayloadFormats.Json;

    public int Attempts { get; set; }

    /// <summary>设备返回的状态码。</summary>
    public int DeviceStatusCode { get; set; }

    public string? SubStatusCode { get; set; }

    /// <summary>失败原因。</summary>
    public string? Error { get; set; }

    /// <summary>执行结果（设备原始响应摘要，仅成功/失败任务有值）。</summary>
    public string? Result { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public static AcsTaskDto From(AcsTask task, bool includeResult = true) => new()
    {
        Id = task.Id,
        DeviceId = task.DeviceId,
        Entity = task.Entity.ToString(),
        Operation = task.Operation.ToString(),
        Status = task.Status.ToString(),
        Source = task.Source,
        Description = task.Description,
        Attempts = task.Attempts,
        DeviceStatusCode = task.DeviceStatusCode,
        SubStatusCode = task.SubStatusCode,
        Error = task.Error,
        Result = includeResult ? task.ResultJson : null,
        CreatedAt = task.CreatedAt,
        StartedAt = task.StartedAt,
        CompletedAt = task.CompletedAt,
        PayloadFormat = task.PayloadFormat,
        NextAttemptAt = task.NextAttemptAt,
    };
}
