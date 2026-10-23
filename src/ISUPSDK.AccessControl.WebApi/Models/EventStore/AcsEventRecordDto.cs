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

    /// <summary>
    /// 可直接显示的图片 URL 列表（前端可直接用作 &lt;img src&gt;）：
    /// <list type="bullet">
    /// <item><see cref="Pictures"/> 里的文件名按配置 <c>ISUP:IsapiDigest:PictureUrlPrefix</c> 拼成平台图片接口地址
    /// （未配置时用相对路径 <c>/api/events/pictures</c>）；</item>
    /// <item>SDK 报警回调报文自带的外链（<c>PictureUrl</c> 列，图片服务器可直接访问）直接附在最后。</item>
    /// </list>
    /// 两者都没有时为 <c>null</c>。
    /// </summary>
    public IReadOnlyList<string>? PictureUrls { get; init; }

    public bool IsAccessControl { get; init; }

    public string? HttpUrl { get; init; }

    public DateTimeOffset EventTime { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }

    public DateTimeOffset EnqueuedAt { get; init; }

    public DateTimeOffset StoredAt { get; init; }

    public string? QueueProvider { get; init; }

    public string? QueueMessageId { get; init; }

    public int DeliveryAttempts { get; init; }

    /// <summary>由实体构造对外视图（不生成 <see cref="PictureUrls"/>）。</summary>
    public static AcsEventRecordDto From(AcsEventRecord record) => From(record, null);

    /// <summary>
    /// 由实体构造对外视图。
    /// <paramref name="pictureUrlPrefix"/> 非空时把 <see cref="Pictures"/> 里的文件名拼成绝对 URL
    /// （如 <c>http://10.0.0.5:5190/api/events/pictures</c>）填入 <see cref="PictureUrls"/>。
    /// </summary>
    public static AcsEventRecordDto From(AcsEventRecord record, string? pictureUrlPrefix)
    {
        var pictures = ParsePictures(record.PictureFileNames);

        return new AcsEventRecordDto
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
            Pictures = pictures,
            PictureUrls = BuildPictureUrls(pictures, record.PictureUrl, pictureUrlPrefix),
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
    }

    /// <summary>
    /// 事件的可显示图片 URL 列表：
    /// 抓拍图文件名按配置前缀拼成平台图片接口地址；SDK 报警回调自带的外链（<see cref="AcsEventRecord.PictureUrl"/>）
    /// 直接给出（图片服务器可访问，平台不下载）。
    /// </summary>
    private static IReadOnlyList<string>? BuildPictureUrls(
        IReadOnlyList<string>? pictures,
        string? directUrl,
        string? pictureUrlPrefix)
    {
        var urls = new List<string>(2);

        if (pictures is { Count: > 0 } && !string.IsNullOrWhiteSpace(pictureUrlPrefix))
        {
            var prefix = pictureUrlPrefix.TrimEnd('/');

            foreach (var picture in pictures)
            {
                if (string.IsNullOrWhiteSpace(picture))
                {
                    continue;
                }

                urls.Add($"{prefix}/{Uri.EscapeDataString(picture.Trim())}");
            }
        }

        if (!string.IsNullOrWhiteSpace(directUrl))
        {
            urls.Add(directUrl.Trim());
        }

        return urls.Count == 0 ? null : urls;
    }

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
