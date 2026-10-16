using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Acs;

/// <summary>
/// ACS 事件的统一"接收 → 去重 → 落盘 → 入缓冲"存储。
///
/// 供三条来源共用，保证按同一 <c>(deviceId, serialNo, 事件时间)</c> 键去重：
/// <list type="bullet">
/// <item>HTTP 推送通道 <see cref="AcsHttpPushListener"/>；</item>
/// <item>历史事件对账补漏 <see cref="AcsEventReconcileService"/>；</item>
/// <item>SDK 报警回调（<see cref="IsupAmsService"/>）——它不入落盘表，但入库时用同一个
/// <see cref="BuildDedupKey"/> 键做幂等，因此"回调 + 对账"或设备重发不会产生重复行。</item>
/// </list>
/// <c>PersistBeforeAck=true</c> 时"落盘成功才算接收"（抛异常表示未接收，调用方据此回 500 让设备重传）。
/// </summary>
public sealed class AcsEventSpool
{
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private readonly AcsHttpPushOptions _options;
    private readonly IsupAmsService _ams;
    private readonly ILogger<AcsEventSpool> _logger;

    private readonly object _sync = new();
    private readonly Dictionary<string, byte> _dedupKeys = new(StringComparer.Ordinal);
    private readonly Queue<string> _dedupOrder = new();

    private long _accepted;
    private long _duplicates;

    public AcsEventSpool(
        IOptions<AcsHttpPushOptions> options,
        IsupAmsService ams,
        ILogger<AcsEventSpool> logger)
    {
        _options = options.Value;
        _ams = ams;
        _logger = logger;

        Initialize();
    }

    /// <summary>已接收（落盘 + 入缓冲）的事件数，不含重复。</summary>
    public long AcceptedEvents => Interlocked.Read(ref _accepted);

    /// <summary>因重复而跳过的事件数（设备重传 / HTTP 与对账重复）。</summary>
    public long DuplicateEvents => Interlocked.Read(ref _duplicates);

    /// <summary>当前去重键数量。</summary>
    public int DedupKeyCount
    {
        get
        {
            lock (_sync)
            {
                return _dedupKeys.Count;
            }
        }
    }

    /// <summary>落盘目录。</summary>
    public string SpoolDirectory => _options.SpoolDirectory;

    /// <summary>建目录并从落盘文件恢复去重键。</summary>
    private void Initialize()
    {
        try
        {
            Directory.CreateDirectory(_options.SpoolDirectory);
            LoadDedupIndex();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ACS 事件落盘初始化失败（{Directory}）。", _options.SpoolDirectory);
        }
    }

    /// <summary>
    /// 尝试接收一条事件：去重 → 落盘 → 入缓冲。
    /// 返回 false 表示已存在（重复，跳过）；落盘失败会抛异常。
    /// </summary>
    public bool TryAccept(IsupAlarmEvent evt, bool force = false)
    {
        var key = BuildDedupKey(evt);

        // force=true：不做重复判断、不落盘去重表，但**仍然送入下游**（否则事件会丢）。
        if (!force)
        {
            lock (_sync)
            {
                if (_dedupKeys.ContainsKey(key))
                {
                    Interlocked.Increment(ref _duplicates);
                    return false;
                }

                if (_options.PersistBeforeAck)
                {
                    PersistRecord(key, evt); // 抛异常 → 未接收
                }

                AddDedupKey(key);
            }
        }

        Interlocked.Increment(ref _accepted);
        _ams.Ingest(evt);
        return true;
    }

    /// <summary>判断某事件是否已接收过（不落盘、不入库）。</summary>
    public bool IsKnown(IsupAlarmEvent evt)
    {
        var key = BuildDedupKey(evt);
        lock (_sync)
        {
            return _dedupKeys.ContainsKey(key);
        }
    }

    /// <summary>
    /// 去重键：<c>deviceId:serialNo:事件时间</c>（时间取不到时省略，二者都取不到时用报文哈希）。
    /// <para>
    /// 三条通道（报警回调 / HTTP 推送 / 历史对账）必须得到同一个键，跨来源的重复事件才能被幂等拦下；
    /// 把事件时间一并入键，是为了避免设备重启后 <c>serialNo</c> 从头计数时把新事件误判成旧事件的重复。
    /// </para>
    /// </summary>
    public static string BuildDedupKey(IsupAlarmEvent evt)
    {
        var device = string.IsNullOrWhiteSpace(evt.DeviceId) ? "?" : evt.DeviceId;
        var serial = ExtractSerialNo(evt.Payload);
        var time = ExtractEventTimeKey(evt.Payload);

        if (!string.IsNullOrEmpty(serial))
        {
            return string.IsNullOrEmpty(time) ? $"{device}:{serial}" : $"{device}:{serial}:{time}";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(evt.Payload ?? string.Empty));
        return $"{device}:{Convert.ToHexString(hash)}";
    }

    /// <summary>
    /// 提取并归一化"事件时间"，与 serialNo 一起组成去重键。
    /// <para>
    /// JSON 取 <c>dateTime</c> / <c>time</c>（或 <c>AccessControllerEvent</c> 下的同名属性）；
    /// XML（PPVSPMessage）取 <c>&lt;Time&gt;</c>。统一按<b>报文自带的墙上时间</b>格式化为
    /// <c>yyyyMMddHHmmss</c>：不做时区换算，因此不受部署机时区影响，同一条事件在
    /// JSON（<c>2026-10-16T09:29:59+08:00</c>）与 XML（<c>2026-10-16 09:29:59</c>）两种表示下取到同一个值。
    /// </para>
    /// </summary>
    public static string? ExtractEventTimeKey(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        var raw = payload.TrimStart().StartsWith('<')
            ? ReadXmlText(payload, "Time")
            : ReadJsonText(payload, "dateTime", "time");

        if (string.IsNullOrWhiteSpace(raw) ||
            !DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return null;
        }

        return parsed.DateTime.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// 从报文里提取设备事件序号，供 <see cref="BuildDedupKey"/> 组成 <c>deviceId:serialNo</c> 去重键。
    /// <list type="bullet">
    /// <item>JSON：顶层 <c>serialNo</c> 或 <c>AccessControllerEvent.serialNo</c>；</item>
    /// <item>XML（PPVSPMessage 报警报文）：<c>&lt;serialNo&gt;2021&lt;/serialNo&gt;</c>
    /// （与 <c>AcsEventPayloadExtractor</c> 的取值口径一致）。</item>
    /// </list>
    /// </summary>
    public static string? ExtractSerialNo(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
        }

        // SDK 报警回调的 PPVSPMessage 报文（XML）：此前只解析 JSON，导致这类事件的去重键
        // 退化成"报文哈希"，与历史对账用的 deviceId:serialNo 对不上 → 同一条事件入库两行。
        if (payload.TrimStart().StartsWith('<'))
        {
            return ReadXmlText(payload, "serialNo");
        }

        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (TryReadSerial(root, out var top))
            {
                return top;
            }

            if (root.TryGetProperty("AccessControllerEvent", out var ace) &&
                ace.ValueKind == JsonValueKind.Object &&
                TryReadSerial(ace, out var nested))
            {
                return nested;
            }
        }
        catch (JsonException)
        {
            // 非 JSON，忽略
        }

        return null;
    }

    private static bool TryReadSerial(JsonElement element, out string? value)
    {
        value = null;
        if (!element.TryGetProperty("serialNo", out var property))
        {
            return false;
        }

        value = property.ValueKind == JsonValueKind.Number ? property.ToString() : property.GetString();
        return !string.IsNullOrEmpty(value);
    }

    /// <summary>取 XML 元素的文本值（标签名不区分大小写，只匹配独占标签，不会命中 <c>frontSerialNo</c> 这类前缀）。</summary>
    private static string? ReadXmlText(string xml, string tag)
    {
        var match = Regex.Match(xml, "<" + tag + ">([^<]+)</" + tag + ">", RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value.Trim() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>按顺序取 JSON 顶层或 <c>AccessControllerEvent</c> 下第一个存在的字符串字段（只解析一次报文）。</summary>
    private static string? ReadJsonText(string payload, params string[] names)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var name in names)
            {
                if (TryReadString(root, name, out var top))
                {
                    return top;
                }
            }

            if (root.TryGetProperty("AccessControllerEvent", out var ace) && ace.ValueKind == JsonValueKind.Object)
            {
                foreach (var name in names)
                {
                    if (TryReadString(ace, name, out var nested))
                    {
                        return nested;
                    }
                }
            }
        }
        catch (JsonException)
        {
            // 非 JSON，忽略
        }

        return null;
    }

    private static bool TryReadString(JsonElement element, string name, out string? value)
    {
        value = null;
        if (!element.TryGetProperty(name, out var property))
        {
            return false;
        }

        value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };

        return !string.IsNullOrWhiteSpace(value);
    }

    private void PersistRecord(string key, IsupAlarmEvent evt)
    {
        var file = Path.Combine(_options.SpoolDirectory, $"acs-events-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        var record = new AcsSpoolRecord(key, DateTimeOffset.UtcNow, evt);
        var line = JsonSerializer.Serialize(record, IsupJson.Options) + Environment.NewLine;
        File.AppendAllText(file, line, Utf8NoBom);
    }

    private void LoadDedupIndex()
    {
        if (!Directory.Exists(_options.SpoolDirectory))
        {
            return;
        }

        try
        {
            var files = Directory.EnumerateFiles(_options.SpoolDirectory, "*.jsonl")
                .OrderByDescending(f => f, StringComparer.Ordinal)
                .Take(2);

            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var record = JsonSerializer.Deserialize<AcsSpoolRecord>(line, IsupJson.Options);
                        if (record is not null)
                        {
                            AddDedupKey(record.Key);
                        }
                    }
                    catch (JsonException)
                    {
                        // 忽略损坏行
                    }
                }
            }

            _logger.LogInformation("ACS 事件存储：已从落盘文件恢复 {Count} 条去重键。", _dedupKeys.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ACS 事件存储：恢复去重键失败。");
        }
    }

    private void AddDedupKey(string key)
    {
        if (_dedupKeys.ContainsKey(key))
        {
            return;
        }

        _dedupKeys[key] = 0;
        _dedupOrder.Enqueue(key);
        TrimDedup();
    }

    private void TrimDedup()
    {
        var limit = Math.Max(_options.DedupCacheSize, 1000);
        while (_dedupKeys.Count > limit && _dedupOrder.Count > 0)
        {
            _dedupKeys.Remove(_dedupOrder.Dequeue());
        }
    }
}

/// <summary>ACS 事件的落盘记录（含去重键，便于重启后恢复）。</summary>
public sealed record AcsSpoolRecord(string Key, DateTimeOffset PersistedAt, IsupAlarmEvent Event);
