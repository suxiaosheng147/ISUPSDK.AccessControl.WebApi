using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data.EventStore;

namespace ISUPSDK.AccessControl.WebApi.Models.EventStore;

/// <summary>
/// 已入库事件的对外视图（<c>GET /api/events</c>）。
///
/// 与实体一一对应，仅把 <c>PictureFileNames</c> 从 JSON 文本还原成字符串数组，
/// 并省略仅用于索引/内部观测的字段。
/// </summary>
public sealed record AcsEventRecordDto
{
    public long Id { get; init; }

    public string DedupKey { get; init; } = string.Empty;

    public string DeviceId { get; init; } = string.Empty;

    public string Source { get; init; } = string.Empty;

    public string? SerialNumber { get; init; }

    public string? EventSerialNo { get; init; }

    public string? EventType { get; init; }

    public string? SubEventType { get; init; }

    public int? Major { get; init; }

    public int? Minor { get; init; }

    public string? EmployeeNo { get; init; }

    public string? CardNo { get; init; }

    public string? PersonName { get; init; }

    public int? DoorNo { get; init; }

    public string? VerifyMode { get; init; }

    public string? AlarmTopic { get; init; }

    public int AlarmType { get; init; }

    public int DataType { get; init; }

    public string PayloadFormat { get; init; } = "json";

    public string? Payload { get; init; }

    public int PayloadBytes { get; init; }

    public bool PayloadTruncated { get; init; }

    public IReadOnlyList<string>? Pictures { get; init; }

    public bool IsAccessControl { get; init; }

    public string? HttpUrl { get; init; }

    public DateTimeOffset EventTime { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; }

    public DateTimeOffset StoredAt { get; init; }

    public string? QueueProvider { get; init; }

    public string? QueueMessageId { get; init; }

    public int DeliveryAttempts { get; init; }

    /// <summary>由实体构造对外视图。</summary>
    public static AcsEventRecordDto From(AcsEventRecord record) => new()
    {
        Id = record.Id,
        DedupKey = record.DedupKey,
        DeviceId = record.DeviceId,
        Source = record.Source,
        SerialNumber = record.SerialNumber,
        EventSerialNo = record.EventSerialNo,
        EventType = record.EventType,
        SubEventType = record.SubEventType,
        Major = record.Major,
        Minor = record.Minor,
        EmployeeNo = record.EmployeeNo,
        CardNo = record.CardNo,
        PersonName = record.PersonName,
        DoorNo = record.DoorNo,
        VerifyMode = record.VerifyMode,
        AlarmTopic = record.AlarmTopic,
        AlarmType = record.AlarmType,
        DataType = record.DataType,
        PayloadFormat = record.PayloadFormat,
        Payload = record.Payload,
        PayloadBytes = record.PayloadBytes,
        PayloadTruncated = record.PayloadTruncated,
        Pictures = ParsePictures(record.PictureFileNames),
        IsAccessControl = record.IsAccessControl,
        HttpUrl = record.HttpUrl,
        EventTime = record.EventTime,
        ReceivedAt = record.ReceivedAt,
        EnqueuedAt = record.EnqueuedAt,
        StoredAt = record.StoredAt,
        QueueProvider = record.QueueProvider,
        QueueMessageId = record.QueueMessageId,
        DeliveryAttempts = record.DeliveryAttempts,
    };

    private static IReadOnlyList<string>? ParsePictures(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
