using System.Globalization;
using System.Text.Json;
using System.Xml.Serialization;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 入库前给事件补抓拍图。
///
/// <para>
/// 两类报文的处理方式不同：
/// <list type="bullet">
/// <item><b>ISAPI JSON</b>（Digest 直连 / 对账 / 推送）：人脸类事件带 <c>pictureURL</c>，
/// 是设备本地地址（<c>https://{设备IP}/LOCALS/pic/...jpeg@WEB...</c>），<b>取图要 Digest 认证</b> ——
/// 平台下载落盘，文件名写进 <see cref="IsupAlarmEvent.PictureFileNames"/>；</item>
/// <item><b>PPVSPMessage XML</b>（SDK 报警回调）：<c>Params.PicDataUrl</c> 指向图片服务器上的抓拍图，
/// 该地址<b>可直接访问、不需要认证</b>，因此平台<b>不下载</b>，只把展开后的完整 URL 记到
/// <see cref="IsupAlarmEvent.PictureUrl"/>（按 <c>PictureServerType</c> 展开：2=云存储拼
/// <c>http://{PictureServerIP}:{PictureServerPort}</c>，3=KMS 报文里就是完整地址）。</item>
/// </list>
/// </para>
///
/// <para>
/// 入库后 <c>GET /api/events</c> 的 <c>pictures</c> / <c>pictureUrls</c> 就有值（<c>pictureUrls</c>
/// 会把文件名拼成平台图片接口地址、并附上 XML 的外链），前端可以直接显示。
/// 已带图的事件（对账链路下载过、手工补录指定）直接跳过；同名文件已存在时复用，不重复下载。
/// </para>
///
/// <para>补图失败只记日志，绝不影响入库（图片是可选的展示信息）。</para>
/// </summary>
public sealed class AcsEventPictureEnricher
{
    /// <summary>设备上报"图片上传失败"时 <c>PicDataUrl</c> 的占位值。</summary>
    private const string PictureUploadFailed = "Failed-to-upload-picture";

    /// <summary>PPVSPMessage 序列化器（XmlSerializer 非线程安全，用锁保护）。</summary>
    private static readonly XmlSerializer PpvspSerializer = new(typeof(PPVSPMessage));

    private static readonly object PpvspLock = new();

    private readonly AcsAlarmForwardService _forwarder;
    private readonly IsupSdkRuntime _runtime;
    private readonly AcsEventQueueOptions _options;
    private readonly ILogger<AcsEventPictureEnricher> _logger;

    public AcsEventPictureEnricher(
        AcsAlarmForwardService forwarder,
        IsupSdkRuntime runtime,
        IOptions<AcsEventQueueOptions> options,
        ILogger<AcsEventPictureEnricher> logger)
    {
        _forwarder = forwarder;
        _runtime = runtime;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 给一批事件补抓拍图：JSON 报文下载落盘写 <see cref="IsupAlarmEvent.PictureFileNames"/>，
    /// XML 报文只记直链写 <see cref="IsupAlarmEvent.PictureUrl"/>；
    /// 单批下载次数受 <c>EventQueue:Consumer:MaxPictureDownloadsPerBatch</c> 限制（0 = 不限）。
    /// </summary>
    public async Task EnrichAsync(IReadOnlyList<IsupAlarmEvent> events, CancellationToken ct)
    {
        var consumer = _options.Consumer;

        if (!consumer.DownloadPictures || events.Count == 0)
        {
            return;
        }

        var budget = consumer.MaxPictureDownloadsPerBatch;
        var attempts = 0;

        foreach (var evt in events)
        {
            if (ct.IsCancellationRequested)
            {
                return;
            }

            // 已经有图了（对账链路已下载 / 手工补录指定了文件名）。
            if (evt.PictureFileNames is { Count: > 0 })
            {
                continue;
            }

            if (budget > 0 && attempts >= budget)
            {
                _logger.LogDebug("本批抓拍图下载已达上限 {Budget} 张，其余事件只入库不带图。", budget);
                return;
            }

            try
            {
                if (await TryEnrichAsync(evt, ct).ConfigureAwait(false))
                {
                    attempts++;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "补抓拍图失败（DeviceId={DeviceId}），事件照常入库。", evt.DeviceId);
            }
        }
    }

    /// <summary>单条事件补图；返回 true 表示本次确实发起了下载（供单批限流计数）。</summary>
    private async Task<bool> TryEnrichAsync(IsupAlarmEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(evt.Payload))
        {
            return false;
        }

        // SDK 报警回调：PPVSPMessage XML，图片在图片服务器上（Params.PicDataUrl），只记 URL、不下载。
        if (evt.Payload.TrimStart().StartsWith('<'))
        {
            return await EnrichFromXmlAsync(evt).ConfigureAwait(false);
        }

        if (!TryReadPictureUrl(evt.Payload, out var pictureUrl, out var eventTime, out var serial))
        {
            return false;
        }

        var fileName = BuildPictureFileName(evt.DeviceId, eventTime, serial, evt.SubEventType);
        var saved = await _forwarder
            .TryDownloadPictureFileAsync(evt.DeviceId, pictureUrl, fileName, ct)
            .ConfigureAwait(false);

        if (saved.FileName is { } name)
        {
            evt.PictureFileNames = [name];
        }

        return saved.Attempted;
    }

    /// <summary>
    /// SDK 报警回调（PPVSPMessage XML）补图：<c>Params.PicDataUrl</c> 指向图片服务器上的抓拍图，
    /// 图片服务器可以直接访问、<b>不需要</b>平台下载（与 ISAPI Digest 的 <c>pictureURL</c> 不同），
    /// 所以这里只把展开后的完整 URL 记到 <see cref="IsupAlarmEvent.PictureUrl"/>，随事件入库供前端直接显示。
    /// </summary>
    private Task<bool> EnrichFromXmlAsync(IsupAlarmEvent evt)
    {
        if (!TryReadPpvspPicture(evt.Payload!, out var picDataUrl, out _, out _))
        {
            return Task.FromResult(false);
        }

        if (ResolvePpvspPictureUrl(picDataUrl) is { } url)
        {
            evt.PictureUrl = url;
        }

        // 没有下载动作，不占用单批下载预算。
        return Task.FromResult(false);
    }

    /// <summary>
    /// 把 <c>PicDataUrl</c> 还原成可下载的完整 URL：
    /// 本身就是绝对地址（KMS <c>PictureServerType=3</c>）直接用；
    /// 云存储（<c>PictureServerType=2</c>）拼 <c>http://{PictureServerIP}:{PictureServerPort}</c>（与转发链路一致）。
    /// </summary>
    private string? ResolvePpvspPictureUrl(string picDataUrl)
    {
        if (string.IsNullOrWhiteSpace(picDataUrl) ||
            string.Equals(picDataUrl, PictureUploadFailed, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (Uri.TryCreate(picDataUrl, UriKind.Absolute, out _))
        {
            return picDataUrl;
        }

        if (_runtime.PictureServerType != 2)
        {
            return null;
        }

        var host = _runtime.PictureServerIp;
        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return $"http://{host.Trim()}:{_runtime.PictureServerPort}{picDataUrl}";
    }

    /// <summary>
    /// 解析 PPVSPMessage XML，取 <c>Params.PicDataUrl</c>、事件时间（<c>Params.Time</c>）与事件序号
    /// （<c>Params.serialNo</c>，缺失时退回次类型）。
    /// </summary>
    internal static bool TryReadPpvspPicture(
        string payload,
        out string picDataUrl,
        out DateTimeOffset? eventTime,
        out string? serial)
    {
        picDataUrl = string.Empty;
        eventTime = null;
        serial = null;

        PPVSPMessage? message;
        try
        {
            lock (PpvspLock)
            {
                using var reader = new StringReader(payload);
                message = PpvspSerializer.Deserialize(reader) as PPVSPMessage;
            }
        }
        catch (Exception)
        {
            // 报文不是 PPVSPMessage（或结构异常）：当作没有图片，不影响入库。
            return false;
        }

        var parameters = message?.Params;
        if (parameters is null || string.IsNullOrWhiteSpace(parameters.PicDataUrl))
        {
            return false;
        }

        picDataUrl = parameters.PicDataUrl.Trim();

        if (DateTimeOffset.TryParse(
                parameters.Time,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var time))
        {
            eventTime = time;
        }

        serial = parameters.serialNo > 0
            ? parameters.serialNo.ToString(CultureInfo.InvariantCulture)
            : null;

        return true;
    }

    /// <summary>
    /// 从 ISAPI JSON 报文里读 <c>pictureURL</c>（兼容顶层 / <c>AcsEvent</c> / <c>AccessControllerEvent</c> 三种层级），
    /// 同时取出事件时间与事件序号（用于稳定的落盘文件名）。
    /// </summary>
    internal static bool TryReadPictureUrl(
        string payload,
        out string pictureUrl,
        out DateTimeOffset? eventTime,
        out string? serial)
    {
        pictureUrl = string.Empty;
        eventTime = null;
        serial = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(payload);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var container = document.RootElement;
            if (container.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (container.TryGetProperty("AcsEvent", out var acsEvent) && acsEvent.ValueKind == JsonValueKind.Object)
            {
                container = acsEvent;
            }
            else if (container.TryGetProperty("AccessControllerEvent", out var accessControllerEvent) &&
                     accessControllerEvent.ValueKind == JsonValueKind.Object)
            {
                container = accessControllerEvent;
            }

            var url = ReadString(container, "pictureURL") ?? ReadString(container, "pictureUrl");
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            pictureUrl = url;
            eventTime = ReadTime(container);
            serial = ReadString(container, "serialNo") ?? ReadString(container, "eventSerialNo");

            return true;
        }
    }

    /// <summary>
    /// 抓拍图落盘文件名：<c>{设备序号}_{事件时间}_{事件序号}.jpeg</c>。
    /// 同一事件在不同链路（对账 / 轮询 / 推送）算出的名字一致，因此重复处理时命中已落盘的图，不重复下载。
    /// </summary>
    public static string BuildPictureFileName(
        string deviceId,
        DateTimeOffset? eventTime,
        string? serial,
        string? minor)
    {
        var time = (eventTime ?? DateTimeOffset.UtcNow).ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var token = !string.IsNullOrWhiteSpace(serial)
            ? serial
            : !string.IsNullOrWhiteSpace(minor) ? minor : "0";

        return $"{SanitizeFileToken(deviceId)}_{time}_{SanitizeFileToken(token)}.jpeg";
    }

    /// <summary>文件名片段清洗：只保留字母 / 数字 / <c>-</c> / <c>_</c>，其余替换成 <c>_</c>（防路径穿越）。</summary>
    internal static string SanitizeFileToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return "0";
        }

        var buffer = new System.Text.StringBuilder(token.Length);

        foreach (var c in token.Trim())
        {
            buffer.Append(char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_');
        }

        var safe = buffer.ToString().Trim('_');

        return string.IsNullOrEmpty(safe) || safe.Contains("..", StringComparison.Ordinal) ? "0" : safe;
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

    private static DateTimeOffset? ReadTime(JsonElement element)
    {
        var text = ReadString(element, "time") ?? ReadString(element, "dateTime");
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }
}
