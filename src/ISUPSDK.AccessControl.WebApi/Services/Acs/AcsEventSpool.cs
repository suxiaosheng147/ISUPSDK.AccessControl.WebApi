using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Acs;

/// <summary>
/// ACS 事件的统一"接收 → 去重 → 落盘 → 入缓冲"存储。
///
/// 供两条来源共用，保证按同一 <c>(deviceId, serialNo)</c> 键去重：
/// <list type="bullet">
/// <item>HTTP 推送通道 <see cref="AcsHttpPushListener"/>；</item>
/// <item>历史事件对账补漏 <see cref="AcsEventReconcileService"/>。</item>
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

    /// <summary>去重键：优先 <c>deviceId:serialNo</c>，无 serialNo 时用报文哈希。</summary>
    public static string BuildDedupKey(IsupAlarmEvent evt)
    {
        var device = string.IsNullOrWhiteSpace(evt.DeviceId) ? "?" : evt.DeviceId;
        var serial = ExtractSerialNo(evt.Payload);
        if (!string.IsNullOrEmpty(serial))
        {
            return $"{device}:{serial}";
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(evt.Payload ?? string.Empty));
        return $"{device}:{Convert.ToHexString(hash)}";
    }

    /// <summary>从报文里提取设备事件序号（顶层 <c>serialNo</c> 或 <c>AccessControllerEvent.serialNo</c>）。</summary>
    public static string? ExtractSerialNo(string? payload)
    {
        if (string.IsNullOrEmpty(payload))
        {
            return null;
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
