using System.Text.Json;
using System.Text.Json.Serialization;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue;

/// <summary>
/// 队列消息体的序列化（UTF-8 JSON 字节）。
///
/// 刻意不用 <c>BinaryFormatter</c>（.NET 9 已移除，且跨版本不安全）：
/// MSMQ 与 RabbitMQ 都只承载这段 JSON 字节，队列里用"计算机管理 → 消息队列"或
/// RabbitMQ 管理页也能直接读懂消息内容。
/// </summary>
public static class AcsQueueSerialization
{
    /// <summary>消息体序列化选项。</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>序列化为 UTF-8 字节。</summary>
    public static byte[] Serialize(AcsQueueEnvelope envelope)
        => JsonSerializer.SerializeToUtf8Bytes(envelope, Options);

    /// <summary>反序列化；消息体损坏时返回 null（由调用方按死信处理）。</summary>
    public static AcsQueueEnvelope? Deserialize(byte[] body)
    {
        if (body.Length == 0)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AcsQueueEnvelope>(body, Options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
