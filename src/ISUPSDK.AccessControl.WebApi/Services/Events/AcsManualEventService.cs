using System.Globalization;
using System.Text;
using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.EventQueue;
using ISUPSDK.AccessControl.WebApi.Models.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 手工补录门禁事件（<c>POST /api/events/manual</c> 的后端）。
///
/// 默认走与真实上报**完全相同**的链路：<c>AcsEventSpool</c>（去重 + 落盘 outbox）→ 消息队列 →
/// 消费者写库，因此补录事件同样具备"重复提交只落一行""队列不可用不丢""可重放补齐"。
/// 也可以选择 <c>directToStore=true</c> 绕过队列直接入库（队列长时间不可用时的应急通道）。
/// </summary>
public sealed class AcsManualEventService
{
    /// <summary>单次手工补录的条数上限。</summary>
    public const int MaxBatchSize = 1000;

    private readonly AcsEventSpool _spool;
    private readonly AcsEventStore _store;
    private readonly IsupTimeOptions _time;
    private readonly ILogger<AcsManualEventService> _logger;

    public AcsManualEventService(
        AcsEventSpool spool,
        AcsEventStore store,
        IOptions<IsupTimeOptions> timeOptions,
        ILogger<AcsManualEventService> logger)
    {
        _spool = spool;
        _store = store;
        _time = timeOptions.Value;
        _logger = logger;
    }

    /// <summary>递交一批手工事件。</summary>
    public async Task<AcsManualEventResult> SubmitAsync(
        IReadOnlyList<AcsManualEventRequest> requests,
        bool directToStore = false,
        bool force = false,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
        {
            return new AcsManualEventResult { DirectToStore = directToStore };
        }

        var items = new List<AcsManualEventItemResult>(requests.Count);
        var accepted = 0;
        var duplicates = 0;
        var rejected = 0;
        var stored = 0;

        if (directToStore)
        {
            await _store.EnsureCreatedAsync(ct).ConfigureAwait(false);
        }

        for (var index = 0; index < requests.Count; index++)
        {
            if (ct.IsCancellationRequested)
            {
                break;
            }

            var request = requests[index];
            var error = Validate(request);
            if (error is not null)
            {
                rejected++;
                items.Add(new AcsManualEventItemResult
                {
                    Index = index,
                    DeviceId = request.DeviceId,
                    Status = "Rejected",
                    Error = error,
                });

                continue;
            }

            var evt = BuildEvent(request, _time.ResolveOffset());
            var dedupKey = AcsEventSpool.BuildDedupKey(evt);

            if (directToStore)
            {
                var envelope = new AcsQueueEnvelope
                {
                    DedupKey = dedupKey,
                    Event = evt,
                    EnqueuedAt = DateTimeOffset.UtcNow,
                    Producer = "manual",
                    SkipDedup = force,
                };

                var write = await _store.AddBatchAsync(new[] { envelope }, "Manual", ct).ConfigureAwait(false);
                if (!write.Success)
                {
                    rejected++;
                    items.Add(new AcsManualEventItemResult
                    {
                        Index = index,
                        DeviceId = evt.DeviceId,
                        DedupKey = dedupKey,
                        Status = "Rejected",
                        Error = $"写库失败：{write.Error}",
                    });

                    continue;
                }

                if (write.Inserted > 0)
                {
                    accepted++;
                    stored += write.Inserted;
                    items.Add(new AcsManualEventItemResult
                    {
                        Index = index,
                        DeviceId = evt.DeviceId,
                        DedupKey = dedupKey,
                        Status = "Stored",
                    });
                }
                else
                {
                    duplicates++;
                    items.Add(new AcsManualEventItemResult
                    {
                        Index = index,
                        DeviceId = evt.DeviceId,
                        DedupKey = dedupKey,
                        Status = "Duplicate",
                    });
                }

                continue;
            }

            bool isFresh;
            try
            {
                // 与设备上报共用同一条入口：去重 → 落盘 JSONL（outbox）→ 入消息队列。
                // force=true：不做重复判断，直接按新事件受理，并让后续入队/入库同样跳过去重。
                evt.SkipDedup = force;
                isFresh = _spool.TryAccept(evt, force);
            }
            catch (Exception ex)
            {
                rejected++;
                _logger.LogWarning(ex, "手工补录事件落盘失败：设备={DeviceId}，去重键={DedupKey}", evt.DeviceId, dedupKey);
                items.Add(new AcsManualEventItemResult
                {
                    Index = index,
                    DeviceId = evt.DeviceId,
                    DedupKey = dedupKey,
                    Status = "Rejected",
                    Error = $"落盘失败：{ex.Message}",
                });

                continue;
            }

            if (isFresh)
            {
                accepted++;
            }
            else
            {
                duplicates++;
            }

            items.Add(new AcsManualEventItemResult
            {
                Index = index,
                DeviceId = evt.DeviceId,
                DedupKey = dedupKey,
                Status = isFresh ? "Accepted" : "Duplicate",
            });
        }

        var result = new AcsManualEventResult
        {
            Requested = requests.Count,
            Accepted = accepted,
            Duplicates = duplicates,
            Rejected = rejected,
            Stored = stored,
            DirectToStore = directToStore,
            Items = items,
        };

        _logger.LogInformation(
            "手工补录事件：请求 {Requested} 条，受理 {Accepted} 条，重复 {Duplicates} 条，拒绝 {Rejected} 条，直接入库 {Stored} 行（direct={Direct}）。",
            result.Requested,
            result.Accepted,
            result.Duplicates,
            result.Rejected,
            result.Stored,
            directToStore);

        return result;
    }

    /// <summary>校验收到的请求（返回 null 表示合法）。</summary>
    internal static string? Validate(AcsManualEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return "deviceId 必填";
        }

        if (!string.IsNullOrWhiteSpace(request.PayloadFormat))
        {
            var format = request.PayloadFormat.Trim().ToLowerInvariant();
            if (format is not ("json" or "xml"))
            {
                return "payloadFormat 只支持 json / xml";
            }
        }

        if (request.DoorNo is < 0)
        {
            return "doorNo 不能为负数";
        }

        return null;
    }

    /// <summary>
    /// 把请求转成事件模型（未提供原文时按字段生成与设备上报同构的 ISAPI 报文）。
    /// 未指定 <see cref="AcsManualEventRequest.EventTime"/> 时按业务时区取"现在"。
    /// </summary>
    internal static IsupAlarmEvent BuildEvent(AcsManualEventRequest request, TimeSpan defaultOffset)
    {
        var eventTime = request.EventTime ?? DateTimeOffset.UtcNow.ToOffset(defaultOffset);
        var format = string.IsNullOrWhiteSpace(request.PayloadFormat)
            ? "json"
            : request.PayloadFormat.Trim().ToLowerInvariant();

        var payload = request.Payload;
        if (string.IsNullOrWhiteSpace(payload))
        {
            payload = BuildPayload(request, eventTime);
            format = "json";
        }

        return new IsupAlarmEvent
        {
            DeviceId = request.DeviceId!.Trim(),
            Source = string.IsNullOrWhiteSpace(request.Source) ? "Manual" : request.Source.Trim(),
            EventType = string.IsNullOrWhiteSpace(request.EventType) ? "AccessControllerEvent" : request.EventType.Trim(),
            SubEventType = (request.Minor ?? 1).ToString(CultureInfo.InvariantCulture),
            AlarmType = (byte)Math.Clamp(request.Major ?? 5, 0, 255),
            DataType = 1,
            Payload = payload,
            PayloadFormat = format,
            PayloadBytes = Encoding.UTF8.GetByteCount(payload),
            IsAccessControl = request.IsAccessControl ?? true,
            PictureFileNames = request.PictureFileNames is { Count: > 0 } ? request.PictureFileNames : null,
            ReceivedAt = eventTime,
        };
    }

    /// <summary>
    /// 生成与设备上报同构的 ISAPI 报文，使入库后的解析列（工号/卡号/门号/主次类型…）与真实事件一致。
    /// </summary>
    private static string BuildPayload(AcsManualEventRequest request, DateTimeOffset eventTime)
    {
        var serialNo = 0;
        if (!string.IsNullOrWhiteSpace(request.SerialNo))
        {
            _ = int.TryParse(request.SerialNo.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out serialNo);
        }

        var accessControllerEvent = new Dictionary<string, object?>
        {
            ["deviceName"] = string.Empty,
            ["majorEventType"] = request.Major ?? 5,
            ["subEventType"] = request.Minor ?? 1,
            ["employeeNoString"] = request.EmployeeNo ?? string.Empty,
            ["name"] = request.PersonName ?? string.Empty,
            ["cardNo"] = request.CardNo ?? string.Empty,
            ["doorNo"] = request.DoorNo ?? 0,
            ["currentVerifyMode"] = request.VerifyMode ?? string.Empty,
        };

        if (serialNo > 0)
        {
            accessControllerEvent["serialNo"] = serialNo;
        }

        var payload = new Dictionary<string, object?>
        {
            ["ipAddress"] = string.Empty,
            ["dateTime"] = eventTime.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
            ["eventType"] = string.IsNullOrWhiteSpace(request.EventType) ? "AccessControllerEvent" : request.EventType.Trim(),
            ["eventState"] = "active",
            ["deviceID"] = request.DeviceId!.Trim(),
            ["manual"] = true,
            ["AccessControllerEvent"] = accessControllerEvent,
        };

        return JsonSerializer.Serialize(payload, AcsQueueSerialization.Options);
    }
}
