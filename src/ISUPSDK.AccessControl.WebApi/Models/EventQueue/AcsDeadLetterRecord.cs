namespace ISUPSDK.AccessControl.WebApi.Models.EventQueue;

/// <summary>
/// 一条死信记录（无法处理的消息）：写入 <c>EventQueue:DeadLetterFile</c>，便于事后排查。
/// </summary>
public sealed record AcsDeadLetterRecord(
    string MessageId,
    string? NativeId,
    string Reason,
    DateTimeOffset RecordedAt,
    AcsQueueEnvelope Envelope,
    int Attempts);
