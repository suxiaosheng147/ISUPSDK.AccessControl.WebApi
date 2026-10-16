using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.SdkService.AlarmService;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISUP 报警事件服务（对应 OTAP 项目的 AMS 组件）。
/// <para>
/// 订阅 <see cref="IsupSdkRuntime"/> 的报警回调（<c>NET_EALARM_StartListen</c> 上报的门禁事件），
/// 把事件放入内存环形缓冲供 REST 查询；实时性优于轮询 ISAPI <c>AcsEvent</c>。
/// </para>
/// </summary>
public sealed class IsupAmsService
{
    /// <summary>内存缓冲上限，超出后淘汰最旧的。</summary>
    private const int BufferCapacity = 5000;

    /// <summary>门禁事件的 MinorType 白名单（与原控制台 AddReadCard 的判定一致）。</summary>
    private static readonly HashSet<string> AccessControlMinorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "0x4b", "0x3c", "0x5", "0x1",
    };

    private readonly IsupSdkRuntime _runtime;
    private readonly ILogger<IsupAmsService> _logger;
    private readonly ConcurrentQueue<IsupAlarmEvent> _events = new();

    private long _sequence;
    private long _totalReceived;
    private int _accessControlEvents;
    private DateTimeOffset? _lastEventAt;

    public IsupAmsService(IsupSdkRuntime runtime, ILogger<IsupAmsService> logger)
    {
        _runtime = runtime;
        _logger = logger;
        runtime.AlarmReceived += OnAlarmReceived;
    }

    /// <summary>每接收并缓存一条事件时触发（供事件入库管道订阅）。</summary>
    public event EventHandler<IsupAlarmEvent>? EventCaptured;

    /// <summary>报警服务器地址（取业务配置的本机地址，端口以 SDK 服务参数中的报警端口为准）。</summary>
    public string ListenAddress => _runtime.CurrentSettings.ServerAddress ?? string.Empty;

    /// <summary>运行状态。</summary>
    public AmsStatus GetStatus() => new()
    {
        Enabled = true,
        Initialized = _runtime.IsInitialized,
        NativeDll = Interop.NativeLibraryResolver.ResolvedDirectory,
        BuildVersion = BuildVersionText(),
        ListenHandles = _runtime.AlarmListenHandles.ToArray(),
        ListenAddress = ListenAddress,
        TotalReceived = Interlocked.Read(ref _totalReceived),
        Buffered = _events.Count,
        AccessControlEvents = Volatile.Read(ref _accessControlEvents),
        DevicesPushEnabled = 0,
        LastEventAt = _lastEventAt,
    };

    /// <summary>事件快照（最新在前）。</summary>
    public IReadOnlyList<IsupAlarmEvent> Snapshot() => _events.Reverse().ToArray();

    /// <summary>清空内存缓冲（计数保留）。</summary>
    public void ClearEvents()
    {
        while (_events.TryDequeue(out _))
        {
        }
    }

    /// <summary>动态启动一次报警监听（1-UDP）。</summary>
    public int StartListen(string? ip, ushort? port) => _runtime.StartAlarmListen(ip, port, 1);

    /// <summary>停止报警监听。</summary>
    public bool StopListen(int handle) => _runtime.StopAlarmListen(handle);

    private void OnAlarmReceived(object? sender, IsupAlarmEventArgs e)
    {
        // 报警回调：解析后统一走 Ingest（与重放 / 对账补漏同一条路径）。
        var (eventType, subEventType) = ExtractTypes(e.Payload);
        var deviceId = ExtractDeviceId(e.Payload) ?? e.DeviceId;

        Ingest(new IsupAlarmEvent
        {
            ReceivedAt = e.ReceivedAt,
            ListenHandle = e.ListenHandle,
            DeviceId = deviceId,
            SerialNumber = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId,
            Source = "Isup",
            AlarmType = (byte)HCISUPAlarm.EHOME_ALARM_ACS,
            EventType = eventType,
            SubEventType = subEventType,
            IsAccessControl = IsAccessControlEvent(eventType, subEventType),
            Payload = e.Payload,
            PayloadFormat = "xml",
            // 去重键为 deviceId:serialNo:事件时间（XML 报文的 <serialNo> / <Time> 已在
            // AcsEventSpool.ExtractSerialNo / ExtractEventTimeKey 中解析），与历史对账 / HTTP 推送共用同一键：
            // 因此同一条设备事件无论走"回调 + 对账"还是被设备重发，入库时都按 DedupHash 幂等，只会留下一行。
            SkipDedup = false,
            PayloadBytes = e.Payload?.Length ?? 0,
        });
    }

    /// <summary>
    /// 把一条事件灌入缓冲并广播：报警回调、落盘重放与历史对账补漏共用。
    /// 已带序号（重放 / 补漏）时保留原序号，否则分配新序号。
    /// </summary>
    public void Ingest(IsupAlarmEvent evt)
    {
        try
        {
            if (evt.ReceivedAt == default)
            {
                evt.ReceivedAt = DateTimeOffset.UtcNow;
            }

            if (evt.Sequence <= 0)
            {
                evt.Sequence = Interlocked.Increment(ref _sequence);
            }

            if (!evt.IsAccessControl)
            {
                evt.IsAccessControl = IsAccessControlEvent(evt.EventType, evt.SubEventType)
                                      || (evt.SubEventType is { Length: > 0 } minor && AccessControlMinorTypes.Contains(minor));
            }
            Interlocked.Increment(ref _totalReceived);
            _lastEventAt = evt.ReceivedAt;

            if (evt.IsAccessControl)
            {
                Interlocked.Increment(ref _accessControlEvents);
            }

            _events.Enqueue(evt);

            // 广播给订阅者（事件入库管道）。
            EventCaptured?.Invoke(this, evt);

            while (_events.Count > BufferCapacity && _events.TryDequeue(out _))
            {
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "处理报警事件失败。");
        }
    }

    /// <summary>
    /// 判定是否门禁事件。
    /// <para>
    /// ISUP / EHome 5.0 的 PPVSPMessage 里 <c>MajorType=0x5</c> 即门禁事件
    /// （0x1-报警、0x2-异常、0x3-操作、0x5-门禁）；
    /// 部分固件的门禁事件 MinorType 与刷卡/人脸白名单一致，作为兜底。
    /// </para>
    /// </summary>
    private static bool IsAccessControlEvent(string? majorType, string? minorType)
        => string.Equals(majorType, "0x5", StringComparison.OrdinalIgnoreCase)
           || (minorType is { Length: > 0 } minor && AccessControlMinorTypes.Contains(minor));

    /// <summary>
    /// 从 PPVSPMessage XML 中取设备 ID。
    /// <para>
    /// 报警回调给出的 <c>iHandle</c> 是<b>报警监听句柄</b>（0/1/2），不是设备注册句柄，
    /// 无法用它反查设备表，因此设备 ID 一律以报文里的 <c>&lt;DeviceID&gt;</c> 为准。
    /// </para>
    /// </summary>
    private static string? ExtractDeviceId(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var match = Regex.Match(payload, "<DeviceID>([^<]+)</DeviceID>", RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value?.Trim() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>从 PPVSPMessage XML 中提取 MajorType / MinorType。</summary>
    private static (string? EventType, string? SubEventType) ExtractTypes(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return (null, null);
        }

        var major = Regex.Match(payload, "<MajorType>([^<]+)</MajorType>", RegexOptions.IgnoreCase);
        var minor = Regex.Match(payload, "<MinorType>([^<]+)</MinorType>", RegexOptions.IgnoreCase);

        return (
            major.Success ? major.Groups[1].Value.Trim() : "AcsEvent",
            minor.Success ? minor.Groups[1].Value.Trim() : null);
    }

    private static string BuildVersionText()
    {
        try
        {
            return $"0x{HCISUPAlarm.NET_EALARM_GetBuildVersion():X8}";
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }
}
