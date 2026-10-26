using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Experimental.System.Messaging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.Models;
using SkiaSharp;
using AlarmEvent = global::ISUPSDK.AccessControl.WebApi.Events;
using AlarmExtData = global::ISUPSDK.AccessControl.WebApi.ExtData;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 报警转发（消费者侧）：从队列消息里取出<b>原始事件报文</b>，按 MinorType 判定后组装
/// Events JSON 并投递到下游消息队列 <c>Settings:MessageQueueAddress</c>
/// （默认 <c>.\Private$\haikangevent</c>）。
/// <para>
/// 生产端（SDK 报警回调 <c>AlarmMsgCallBack</c>）只把报文原样入队，不做任何判定；
/// 判定与"下载图片 + 组装报文 + 发送"都在这里完成，即"队列 → 消费者判断发送"。
/// </para>
/// <para>支持两类载荷：</para>
/// <list type="bullet">
/// <item><b>PPVSPMessage XML</b>（SDK 报警回调）：判定规则沿用原生产端 <c>AddReadCard</c> 的语义 ——
/// <c>0x4b</c>/<c>0x3c</c> 必须带有效图片（<c>PicDataUrl</c> 非空且不是
/// <c>Failed-to-upload-picture</c>）才转发，<c>0x5</c>/<c>0x1</c> 无条件转发，
/// 其余 MinorType 跳过（照常入库，不外发）；</item>
/// <item><b>ISAPI JSON</b>（历史对账补漏 / Digest 直连 / HTTP 推送）：白名单内的 <c>minor</c> 一律转发
/// （<c>1</c>→<c>0x1</c>、<c>5</c>→<c>0x5</c>、<c>75</c>→<c>0x4b</c>、<c>60</c>→<c>0x3c</c>），
/// 图片<b>优先复用</b>入库链路（对账 / 高频轮询 / 消费者补图）已落盘的抓拍图
/// （<see cref="IsupAlarmEvent.PictureFileNames"/>），平台还没有图时才按报文里的 <c>pictureURL</c>
/// 用 Digest 下载；两者都拿不到时图片字段留空，人脸类仍按人脸事件码（196893）上报。
/// 这样"回调丢失、由对账补进库"的刷卡/人脸事件也能补发给下游，且不会为了同一张图重复打设备。</item>
/// </list>
/// </summary>
public sealed class AcsAlarmForwardService
{
    /// <summary>需要带图才转发的人脸类事件。</summary>
    private static readonly HashSet<string> PictureMinorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "0x4b", "0x3c",
    };

    /// <summary>无条件转发的刷卡类事件。</summary>
    private static readonly HashSet<string> DirectMinorTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "0x5", "0x1",
    };

    /// <summary>设备上报"图片上传失败"时 PicDataUrl 的占位值。</summary>
    private const string PictureUploadFailed = "Failed-to-upload-picture";

    /// <summary>未配置 <c>ISUP:IsapiDigest:PictureUrlPrefix</c> 时使用的图片接口相对路径。</summary>
    private const string PictureUrlPath = "/api/events/pictures";

    /// <summary>PPVSPMessage 解析器（线程安全，可复用）。</summary>
    private static readonly XmlSerializer PayloadSerializer = new(typeof(PPVSPMessage));

    private readonly IsupSdkRuntime _runtime;
    private readonly IsapiDigestClient _digest;
    private readonly ILogger<AcsAlarmForwardService> _logger;

    private long _forwarded;
    private long _skipped;
    private long _failed;
    private DateTimeOffset? _lastForwardedAt;

    public AcsAlarmForwardService(
        IsupSdkRuntime runtime,
        IsapiDigestClient digest,
        ILogger<AcsAlarmForwardService> logger)
    {
        _runtime = runtime;
        _digest = digest;
        _logger = logger;
    }

    /// <summary>抓拍图落盘目录（配置优先，留空回退 <c>Settings:Photo</c>）。</summary>
    public string ResolvePictureDirectory()
        => _digest.ResolvePictureDirectory(_runtime.CurrentSettings.Photo);

    /// <summary>
    /// 抓拍图对外 URL 前缀，**只**来自配置 <c>ISUP:IsapiDigest:PictureUrlPrefix</c>
    /// （如 <c>http://10.0.0.5:5190/api/events/pictures</c>，反代 / 域名场景写
    /// <c>https://acs.example.com/acs-pictures</c>）；留空时用相对路径
    /// <c>/api/events/pictures</c>（浏览器按页面同源解析，不依赖任何 IP 配置）。
    /// </summary>
    public string ResolvePictureUrlPrefix()
        => _digest.ConfiguredPictureUrlPrefix ?? PictureUrlPath;

    /// <summary>
    /// 把请求里的文件名解析成落盘目录下的绝对路径；文件名非法（含路径分隔符 / 上级目录）返回 null。
    /// </summary>
    public string? ResolvePictureFile(string? fileName)
    {
        if (!IsSafePictureFileName(fileName))
        {
            return null;
        }

        var directory = ResolvePictureDirectory();
        return string.IsNullOrWhiteSpace(directory)
            ? null
            : Path.GetFullPath(Path.Combine(directory, fileName!));
    }

    /// <summary>文件名是否安全（纯文件名，不含路径分隔符 / 上级目录）。</summary>
    internal static bool IsSafePictureFileName(string? fileName)
        => !string.IsNullOrWhiteSpace(fileName)
           && !fileName.Contains("..", StringComparison.Ordinal)
           && fileName.IndexOfAny(['/', '\\']) < 0
           && fileName == Path.GetFileName(fileName);

    /// <summary>已成功转发到下游队列的条数。</summary>
    public long Forwarded => Interlocked.Read(ref _forwarded);

    /// <summary>判定为"无需转发"而跳过的条数。</summary>
    public long Skipped => Interlocked.Read(ref _skipped);

    /// <summary>转发失败的条数（由消费者重投，超上限进死信）。</summary>
    public long Failed => Interlocked.Read(ref _failed);

    /// <summary>最近一次成功转发时间。</summary>
    public DateTimeOffset? LastForwardedAt => _lastForwardedAt;

    /// <summary>
    /// 判定并转发一条报警事件。
    /// <para>
    /// 返回 <see cref="AcsAlarmForwardStatus.Failed"/> 时调用方应重投该消息
    /// （与落库失败同样策略：投递次数 +1，超上限进死信）。
    /// </para>
    /// </summary>
    public async Task<AcsAlarmForwardResult> TryForwardAsync(IsupAlarmEvent evt, CancellationToken ct = default)
    {
        var settings = _runtime.CurrentSettings;

        if (!settings.IsMessageQueue)
        {
            return Record(AcsAlarmForwardResult.Skipped("未开启消息队列（Settings:IsMessageQueue=false）"));
        }

        if (string.IsNullOrWhiteSpace(evt.Payload))
        {
            return Record(AcsAlarmForwardResult.Skipped("事件没有报文"));
        }

        try
        {
            var outcome = evt.Payload.TrimStart().StartsWith('<')
                ? ForwardAlarmXml(evt.Payload)
                : await ForwardIsapiJsonAsync(evt, ct).ConfigureAwait(false);

            if (outcome.Status == AcsAlarmForwardStatus.Forwarded)
            {
                Interlocked.Increment(ref _forwarded);
                _lastForwardedAt = DateTimeOffset.UtcNow;
            }

            return Record(outcome);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "事件转发失败（DeviceID={DeviceId}，Source={Source}），将由消费者重投。",
                evt.DeviceId,
                evt.Source);

            return Record(AcsAlarmForwardResult.Failed(ex.GetBaseException().Message));
        }
    }

    /// <summary>转发 SDK 报警回调的 PPVSPMessage XML 报文（含图片下载）。</summary>
    private AcsAlarmForwardResult ForwardAlarmXml(string payload)
    {
        if (!TryParse(payload, out var message, out var parseError))
        {
            return AcsAlarmForwardResult.Failed(parseError!);
        }

        if (message!.Params is null)
        {
            return AcsAlarmForwardResult.Skipped("报警报文缺少 Params 节点");
        }

        if (!ShouldForward(message.Params.MinorType, message.Params.PicDataUrl, out var skipReason))
        {
            return AcsAlarmForwardResult.Skipped(skipReason!);
        }

        SendSetMessage(message);

        return AcsAlarmForwardResult.Forwarded(
            $"XML 报警：MinorType={message.Params.MinorType}，DeviceID={message.Params.DeviceID}");
    }

    /// <summary>
    /// 转发 ISAPI JSON 事件报文（历史对账补漏 / Digest 直连 / HTTP 推送）。
    /// <para>
    /// 这类报文没有图片字节，采用"<b>无图降级</b>"：白名单内的 MinorType 一律转发
    /// （复用 <see cref="IsForwardableMinor"/> 时把十进制 minor 还原成 XML 的十六进制写法：
    /// 1→<c>0x1</c>、5→<c>0x5</c>、75→<c>0x4b</c>、60→<c>0x3c</c>）。
    /// 若报文带 <c>pictureURL</c>（Digest 直连的人脸事件），则用 Digest 下载抓拍图：
    /// 报文里同时给出原始 URL 与本地路径；下载失败只保留 URL。
    /// 人脸类的事件类型仍按人脸认证通过（196893）上报，不因无图降级成刷卡。
    /// </para>
    /// </summary>
    private async Task<AcsAlarmForwardResult> ForwardIsapiJsonAsync(IsupAlarmEvent evt, CancellationToken ct)
    {
        if (!TryParseIsapiEvent(evt.Payload!, out var isapi, out var parseError))
        {
            return AcsAlarmForwardResult.Failed(parseError!);
        }

        var minorHex = isapi.Minor is null ? null : $"0x{isapi.Minor.Value:x}";
        if (!IsForwardableMinor(minorHex, out var skipReason))
        {
            return AcsAlarmForwardResult.Skipped($"ISAPI JSON 事件：{skipReason}");
        }

        await SendIsapiEventAsync(evt, isapi, minorHex!, ct).ConfigureAwait(false);

        return AcsAlarmForwardResult.Forwarded(
            $"ISAPI JSON：minor={isapi.Minor}，DeviceID={evt.DeviceId}");
    }

    /// <summary>统计并返回结果（<see cref="AcsAlarmForwardStatus.Failed"/> 由调用方另行处理）。</summary>
    private AcsAlarmForwardResult Record(AcsAlarmForwardResult result)
    {
        if (result.Status == AcsAlarmForwardStatus.Skipped)
        {
            Interlocked.Increment(ref _skipped);
        }
        else if (result.Status == AcsAlarmForwardStatus.Failed)
        {
            Interlocked.Increment(ref _failed);
        }

        return result;
    }

    /// <summary>把队列里的原始报文解析成 PPVSPMessage（只接受 XML 报警报文）。</summary>
    private static bool TryParse(string payload, out PPVSPMessage? message, out string? error)
    {
        message = null;
        error = null;

        try
        {
            using var reader = new StringReader(payload);
            message = PayloadSerializer.Deserialize(reader) as PPVSPMessage;
        }
        catch (Exception ex)
        {
            error = $"报警报文解析失败：{ex.GetBaseException().Message}";
            return false;
        }

        if (message is null)
        {
            error = "报警报文反序列化结果为空";
            return false;
        }

        return true;
    }

    /// <summary>是否属于可转发的 MinorType（<c>0x4b</c> / <c>0x3c</c> 人脸，<c>0x5</c> / <c>0x1</c> 刷卡）。</summary>
    private static bool IsForwardableMinor(string? minorType, out string? skipReason)
    {
        skipReason = null;

        if (string.IsNullOrWhiteSpace(minorType))
        {
            skipReason = "报警报文没有 MinorType";
            return false;
        }

        if (DirectMinorTypes.Contains(minorType) || PictureMinorTypes.Contains(minorType))
        {
            return true;
        }

        skipReason = $"MinorType={minorType} 不在转发范围（0x4b/0x3c/0x5/0x1）";
        return false;
    }

    /// <summary>
    /// 是否转发 PPVSPMessage XML 报警报文（等价于原生产端 <c>AlarmMsgCallBack</c> 的 switch + <c>AddReadCard</c> 判定）：
    /// 白名单内的人脸类（<c>0x4b</c> / <c>0x3c</c>）还要求带有效图片。
    /// </summary>
    private static bool ShouldForward(string? minorType, string? picDataUrl, out string? skipReason)
    {
        if (!IsForwardableMinor(minorType, out skipReason))
        {
            return false;
        }

        if (!PictureMinorTypes.Contains(minorType!))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(picDataUrl))
        {
            skipReason = $"MinorType={minorType} 无图片（PicDataUrl 为空）";
            return false;
        }

        if (string.Equals(picDataUrl, PictureUploadFailed, StringComparison.OrdinalIgnoreCase))
        {
            skipReason = $"MinorType={minorType} 图片上传失败（PicDataUrl={PictureUploadFailed}）";
            return false;
        }

        return true;
    }

    /// <summary>
    /// 组装 Events 报文并发送到下游消息队列（原生产端 <c>SendSetMessage</c> 的逻辑）。
    /// </summary>
    private void SendSetMessage(PPVSPMessage message)
    {
        var settings = _runtime.CurrentSettings;

        var shuaTime = Convert.ToDateTime(message.Params.Time);
        var cardNo = message.Params.cardNo;

        // 根据员工编号解析用户类型（01-学生 / 02-家长 / 03-教职工）
        var (userid, userType) = ResolvePerson(message.Params.employeeNoString);

        string picSave = string.Empty;
        string picUrl = string.Empty;

        if (!string.IsNullOrEmpty(message.Params.PicDataUrl))
        {
            var picServerType = _runtime.PictureServerType;
            if (picServerType == 2) // 云存储
            {
                picUrl = $"http://{_runtime.PictureServerIp}:{_runtime.PictureServerPort}" + message.Params.PicDataUrl;
            }
            else if (picServerType == 3) // KMS
            {
                picUrl = message.Params.PicDataUrl;
            }

            var fileName = new StringBuilder(128)
               .Append(message.Params.DeviceID)
               .Append('_')
               .Append(shuaTime.ToString("yyyyMMddHHmmss"))
               .Append('_').Append(userType).Append('_')
               .Append(userid).Append(".jpeg").ToString();

            picSave = DownloadPic(settings.IsDownloadPic, picUrl, fileName);
        }

        var eventType = !string.IsNullOrWhiteSpace(picUrl) ? "196893" : "198914";

        AlarmEvent events = new()
        {
            eventId = message.Params.DeviceID + eventType + userType + userid.ToString() + message.Params.Time,
            eventType = eventType,
            happenTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            srcName = message.Params.DeviceID,
            srcParentIndex = "",
            srcType = "人脸机",
            data = new AlarmExtData()
            {
                ExtEventCardNo = cardNo,
                ExtEventInOut = 0,
                ExtEventPictureURL = picUrl,
                ExtEventPersonNo = userid.ToString(),
                UserType = userType,
                Time = shuaTime,
                ExtEventPicture = picSave
            }
        };

        var inputXml = JsonHepler.ToJson(events);

        using MessageQueue mq = MessageQueueHepler.Create(settings.MessageQueueAddress);
        mq.Send(inputXml);
    }

    /// <summary>
    /// 组装并发送一条来自 ISAPI JSON 事件报文的下游消息。
    /// 抓拍图<b>优先复用</b>入库链路（对账 / 高频轮询 / 消费者补图）已经落盘的文件
    /// （<see cref="IsupAlarmEvent.PictureFileNames"/>），只有平台还没有图时才用 Digest 下载；
    /// 两条路径都拿不到时图片字段留空（无图降级）。
    /// </summary>
    private async Task SendIsapiEventAsync(IsupAlarmEvent evt, IsapiEventInfo info, string minorHex, CancellationToken ct)
    {
        var settings = _runtime.CurrentSettings;
        var deviceId = evt.DeviceId;
        var shuaTime = ParseEventTime(info.Time);
        var (userId, userType) = ResolvePerson(info.EmployeeNo);

        // 人脸类 → 人脸认证通过（196893），刷卡类 → 合法卡比对通过（198914）：
        // 事件类型由 minor 决定，不因"无图"把人脸事件降级成刷卡事件。
        var eventType = PictureMinorTypes.Contains(minorHex) ? "196893" : "198914";

        // 抓拍图：Digest 报文里的 pictureURL 是设备本地地址（下游取不了，还得带 Digest 认证），
        // 报文里给出的是平台自己的图片接口地址（复用入库已落盘的图，必要时才下载）。
        var picture = await ResolveAlarmPictureAsync(evt, info, ct).ConfigureAwait(false);

        AlarmEvent events = new()
        {
            eventId = deviceId + eventType + userType + userId.ToString() + (info.Time ?? string.Empty),
            eventType = eventType,
            happenTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            srcName = deviceId,
            srcParentIndex = "",
            srcType = "人脸机",
            data = new AlarmExtData
            {
                ExtEventCardNo = info.CardNo ?? string.Empty,
                ExtEventInOut = 0,
                ExtEventPictureURL = picture?.Url ?? string.Empty,
                ExtEventPersonNo = userId.ToString(),
                UserType = userType,
                Time = shuaTime,
                ExtEventPicture = picture?.FilePath ?? string.Empty
            }
        };

        var json = JsonHepler.ToJson(events);

        using MessageQueue mq = MessageQueueHepler.Create(settings.MessageQueueAddress);
        mq.Send(json);
    }

    /// <summary>
    /// 取转发报文要用的抓拍图：<b>先复用</b>入库链路落盘的文件，没有再按 Digest 下载。
    /// 两条路径的文件名都由 <see cref="AcsEventPictureEnricher.BuildPictureFileName"/> 生成，
    /// 因此重复处理同一事件时必然命中已落盘文件，不会向设备重复取图
    /// （对账补历史时设备上的图往往已被覆盖，重下只会失败 → 报文里空 URL）。
    /// </summary>
    private async Task<DownloadedPicture?> ResolveAlarmPictureAsync(
        IsupAlarmEvent evt,
        IsapiEventInfo info,
        CancellationToken ct)
    {
        if (evt.PictureFileNames is { Count: > 0 } names && IsSafePictureFileName(names[0]))
        {
            var fileName = names[0];
            var path = ResolvePictureFile(fileName);

            // 图片被人工清理 / 换机导致文件不在时，继续走下面的下载兜底。
            if (path is not null && File.Exists(path))
            {
                return new DownloadedPicture(path, $"{ResolvePictureUrlPrefix()}/{fileName}");
            }
        }

        if (string.IsNullOrWhiteSpace(info.PictureUrl) || !_digest.DownloadPictures)
        {
            return null;
        }

        return await TryDownloadAlarmPictureAsync(evt, info, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 用 Digest 下载抓拍图并按给定文件名落盘，返回落盘结果（<b>对账 / 高频轮询复用</b>，
    /// 让入库事件的 <c>pictures</c> / <c>pictureUrls</c> 也有值）。
    /// <para>
    /// 开关为 <c>ISUP:IsapiDigest:DownloadPictures</c>；文件已存在时不再重复下载。
    /// </para>
    /// </summary>
    public async Task<PictureSaveResult> TryDownloadPictureFileAsync(
        string deviceId,
        string? pictureUrl,
        string fileName,
        CancellationToken ct = default)
    {
        if (!_digest.DownloadPictures ||
            string.IsNullOrWhiteSpace(pictureUrl) ||
            !IsSafePictureFileName(fileName))
        {
            return PictureSaveResult.Skipped;
        }

        var directory = ResolvePictureDirectory();
        var path = Path.Combine(directory ?? string.Empty, fileName);

        // 同一事件的抓拍图已落盘（对账 / 轮询重复扫描同一区间）：直接复用，不再发请求。
        if (File.Exists(path))
        {
            return PictureSaveResult.Reused(fileName);
        }

        try
        {
            var bytes = await _digest.TryDownloadPictureAsync(deviceId, pictureUrl, ct).ConfigureAwait(false);
            if (bytes is null || bytes.Length == 0)
            {
                return PictureSaveResult.Failed;
            }

            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllBytesAsync(path, bytes, ct).ConfigureAwait(false);

            return PictureSaveResult.Saved(fileName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "保存抓拍图失败：{File}", fileName);
            return PictureSaveResult.Failed;
        }
    }

    /// <summary>
    /// 用 Digest 下载抓拍图并落盘；成功返回"本地绝对路径 + 平台对外 URL"，
    /// 失败返回 null（调用方留空，不发设备地址）。
    /// <para>
    /// 文件名与入库链路共用 <see cref="AcsEventPictureEnricher.BuildPictureFileName"/>
    /// （<c>{设备}_{事件时间}_{序号|人员编号|报文指纹}.jpeg</c>），不再拼 <c>userType_userId</c>：
    /// 后者在报文缺 <c>employeeNoString</c> 时会退化成 <c>_0_0</c>，
    /// 同设备同一秒的多条事件会撞名并复用别人的照片。
    /// </para>
    /// </summary>
    private async Task<DownloadedPicture?> TryDownloadAlarmPictureAsync(
        IsupAlarmEvent evt,
        IsapiEventInfo info,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(info.PictureUrl))
        {
            return null;
        }

        var fileName = AcsEventPictureEnricher.BuildPictureFileName(
            evt.DeviceId,
            ParseEventTimeOffset(info.Time),
            info.Serial,
            info.Minor?.ToString(CultureInfo.InvariantCulture),
            info.EmployeeNo,
            evt.Payload);

        var saved = await TryDownloadPictureFileAsync(evt.DeviceId, info.PictureUrl, fileName, ct).ConfigureAwait(false);
        if (saved.FileName is not { } pictureFile)
        {
            return null;
        }

        var path = Path.GetFullPath(Path.Combine(ResolvePictureDirectory() ?? string.Empty, pictureFile));

        return new DownloadedPicture(path, $"{ResolvePictureUrlPrefix()}/{pictureFile}");
    }

    /// <summary>下载成功的抓拍图：本地绝对路径 + 平台对外 URL（指向 <c>GET /api/events/pictures/{fileName}</c>）。</summary>
    private readonly record struct DownloadedPicture(string FilePath, string Url);

    /// <summary>解析 ISAPI 事件报文（顶层字段，或 <c>AcsEvent</c> / <c>AccessControllerEvent</c> 容器下）。</summary>
    private static bool TryParseIsapiEvent(string payload, out IsapiEventInfo info, out string? error)
    {
        info = default!;
        error = null;

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            error = $"事件报文解析失败：{ex.GetBaseException().Message}";
            return false;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            error = "事件报文不是 JSON 对象";
            return false;
        }

        var container = root;
        if (root.TryGetProperty("AcsEvent", out var acsEvent) && acsEvent.ValueKind == JsonValueKind.Object)
        {
            container = acsEvent;
        }
        else if (root.TryGetProperty("AccessControllerEvent", out var accessControllerEvent) &&
                 accessControllerEvent.ValueKind == JsonValueKind.Object)
        {
            container = accessControllerEvent;
        }

        info = new IsapiEventInfo(
            Minor: ReadInt(container, "minor"),
            Time: ReadString(container, "time") ?? ReadString(container, "dateTime"),
            CardNo: ReadString(container, "cardNo"),
            EmployeeNo: ReadString(container, "employeeNoString"),
            // 海康 Digest 直连返回的事件里带抓拍图 URL（人脸事件才有）
            PictureUrl: ReadString(container, "pictureURL") ?? ReadString(container, "pictureUrl"),
            // 事件序号：抓拍图文件名与入库链路（对账 / 消费者补图）共用同一 token，才能复用已落盘的图
            Serial: ReadString(container, "serialNo") ?? ReadString(container, "eventSerialNo"));

        return true;
    }

    /// <summary>员工编号 → 用户类型（01-学生 / 02-家长 / 03-教职工）与用户 ID（编号第 3 位起）。</summary>
    private static (int UserId, int UserType) ResolvePerson(string? employeeNoString)
    {
        if (string.IsNullOrEmpty(employeeNoString) || employeeNoString.Length <= 2)
        {
            return (0, 0);
        }

        var userType = employeeNoString.StartsWith("01") ? 1
            : employeeNoString.StartsWith("02") ? 2
            : employeeNoString.StartsWith("03") ? 3
            : 0;

        if (userType == 0)
        {
            return (0, 0);
        }

        // 脏数据（后缀非数字）不抛异常：用户 ID 记 0，用户类型照前缀判定。
        return (int.TryParse(employeeNoString.AsSpan(2), out var userId) ? userId : 0, userType);
    }

    private static DateTime ParseEventTime(string? raw)
        => ParseEventTimeOffset(raw)?.DateTime ?? DateTime.Now;

    /// <summary>
    /// 事件时间（保留报文里的时区偏移）；解析失败返回 null。
    /// 抓拍图文件名用它做时间 token，必须与入库链路（<see cref="AcsEventPictureEnricher.BuildPictureFileName"/>）一致。
    /// </summary>
    private static DateTimeOffset? ParseEventTimeOffset(string? raw)
        => DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// 下载报警图片到本地（原生产端 <c>DownloadPic</c> 的逻辑，含解码后统一转存 JPEG）。
    /// 下载失败只记警告并返回目标路径，不影响转发（下游可按 URL 自行取图）。
    /// </summary>
    private string DownloadPic(bool isDownloadPic, string picUrl, string fileName)
    {
        if (!isDownloadPic || string.IsNullOrWhiteSpace(picUrl))
        {
            return string.Empty;
        }

        var settings = _runtime.CurrentSettings;
        var picSave = settings.Photo + "\\" + fileName;

        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(picUrl);
        req.Timeout = 5000; // 设置超时值5秒
        req.Method = "GET";
        req.ServicePoint.Expect100Continue = false; // 加了这一行代码就OK了

        try
        {
            using var response = (HttpWebResponse)req.GetResponse();
            using var stream = response.GetResponseStream();
            using var bitmap = SKBitmap.Decode(stream);

            if (bitmap != null)
            {
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
                using var fileStream = File.OpenWrite(picSave);
                data.SaveTo(fileStream);
            }

            return picSave;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载报警图片失败：{PicUrl}", picUrl);
            return picSave;
        }
    }

    /// <summary>ISAPI JSON 事件报文里转发需要的字段（Digest 直连的报文可能带 <c>pictureURL</c>）。</summary>
    /// <remarks>
    /// <c>Serial</c> = <c>serialNo</c> / <c>eventSerialNo</c>：抓拍图文件名与入库链路共用同一 token。
    /// </remarks>
    private sealed record IsapiEventInfo(
        int? Minor,
        string? Time,
        string? CardNo,
        string? EmployeeNo,
        string? PictureUrl,
        string? Serial);
}

/// <summary>转发判定结果。</summary>
public enum AcsAlarmForwardStatus
{
    /// <summary>已转发到下游队列。</summary>
    Forwarded,

    /// <summary>判定为无需转发（消息正常确认）。</summary>
    Skipped,

    /// <summary>转发失败（消息应重投，超上限进死信）。</summary>
    Failed,
}

/// <summary>一次转发判定的结果与说明。</summary>
/// <param name="Status">判定结果。</param>
/// <param name="Detail">说明（成功时是报文摘要，跳过/失败时是原因）。</param>
public readonly record struct AcsAlarmForwardResult(AcsAlarmForwardStatus Status, string? Detail)
{
    public static AcsAlarmForwardResult Forwarded(string detail) => new(AcsAlarmForwardStatus.Forwarded, detail);

    public static AcsAlarmForwardResult Skipped(string detail) => new(AcsAlarmForwardStatus.Skipped, detail);

    public static AcsAlarmForwardResult Failed(string detail) => new(AcsAlarmForwardStatus.Failed, detail);
}

/// <summary>
/// 抓拍图落盘结果。
/// <para>
/// <see cref="FileName"/> 是落盘的纯文件名（未落盘为 null，可直接拼
/// <c>GET /api/events/pictures/{fileName}</c>）；<see cref="Attempted"/> 表示本次是否真的
/// 向设备发起了下载 —— 已存在的文件为 false，对账/轮询据此做"单轮下载次数"限流。
/// </para>
/// </summary>
public readonly record struct PictureSaveResult(string? FileName, bool Attempted)
{
    /// <summary>未发起下载：开关关闭 / 无 pictureURL / 文件名非法。</summary>
    public static PictureSaveResult Skipped => new(null, false);

    /// <summary>发起了下载但失败（非 2xx、设备上图片已被覆盖、落盘异常）。</summary>
    public static PictureSaveResult Failed => new(null, true);

    /// <summary>下载并落盘成功。</summary>
    public static PictureSaveResult Saved(string fileName) => new(fileName, true);

    /// <summary>文件已存在，直接复用（未发请求）。</summary>
    public static PictureSaveResult Reused(string fileName) => new(fileName, false);
}

