using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 设备 ISAPI 直连客户端（HTTP Digest 认证）。
/// <para>
/// 不依赖 SDK 设备注册：直接用配置里的 IP + 账号密码访问设备 ISAPI，
/// 用于拉取门禁事件（<c>POST /ISAPI/AccessControl/AcsEvent?format=json</c>）与下载其
/// <c>pictureURL</c> 抓拍图。认证流程与海康示例一致：先发一次不带认证的请求拿 401 的
/// <c>WWW-Authenticate: Digest</c> 挑战，算出 response 后再带 <c>Authorization</c> 重发。
/// </para>
/// </summary>
public sealed class IsapiDigestClient : IDisposable
{
    private readonly IsapiDigestOptions _options;
    private readonly ILogger<IsapiDigestClient> _logger;
    private readonly HttpClient _http;

    public IsapiDigestClient(IOptions<IsapiDigestOptions> options, ILogger<IsapiDigestClient> logger)
    {
        _options = options.Value;
        _logger = logger;

        var handler = new HttpClientHandler();

        // 设备多为自签名证书，默认忽略证书错误（可用 ISUP:IsapiDigest:IgnoreSslErrors 关闭）。
        if (_options.IgnoreSslErrors)
        {
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }

        _http = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(_options.TimeoutSeconds, 5, 300)),
        };
    }

    /// <summary>是否可用（开关打开且至少有一台配置完整、可解析的设备）。</summary>
    public bool IsEnabled => _options.Enabled && _options.Devices.Any(d => d.IsUsable());

    /// <summary>是否下载 pictureURL 抓拍图。</summary>
    public bool DownloadPictures => _options.Enabled && _options.DownloadPictures;

    /// <summary>
    /// 抓拍图落盘目录：配置了 <c>ISUP:IsapiDigest:PictureDirectory</c> 用它，
    /// 留空时回退 <paramref name="fallbackDirectory"/>（调用方传 <c>Settings:Photo</c>）。
    /// </summary>
    public string ResolvePictureDirectory(string? fallbackDirectory)
        => string.IsNullOrWhiteSpace(_options.PictureDirectory)
            ? fallbackDirectory ?? string.Empty
            : _options.PictureDirectory.Trim();

    /// <summary>配置的抓拍图对外 URL 前缀（去掉结尾 <c>/</c>）；未配置返回 null（由调用方自动推导）。</summary>
    public string? ConfiguredPictureUrlPrefix
        => string.IsNullOrWhiteSpace(_options.PictureUrlPrefix)
            ? null
            : _options.PictureUrlPrefix.Trim().TrimEnd('/');

    /// <summary>按设备编号取可用的直连配置。</summary>
    public bool TryResolveDevice(string? deviceId, out IsapiDigestDevice device)
    {
        device = null!;

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return false;
        }

        var match = _options.Devices.FirstOrDefault(d =>
            string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase) && d.IsUsable());

        if (match is null)
        {
            return false;
        }

        device = match;
        return true;
    }

    /// <summary>配置里可用的设备（用于对账目标与高频轮询）。</summary>
    public IReadOnlyList<IsapiDigestDevice> ConfiguredDevices
        => _options.Devices.Where(d => d.IsUsable()).ToArray();

    /// <summary>配置里可用的设备编号（用于把直连设备也纳入对账目标，即使未注册到 SDK）。</summary>
    public IReadOnlyList<string> ConfiguredDeviceIds
        => _options.Devices.Where(d => d.IsUsable()).Select(d => d.DeviceId).ToArray();

    /// <summary>Digest 失败时是否回退 SDK 对账（<c>ISUP:IsapiDigest:FallbackToSdk</c>）。</summary>
    public bool FallbackToSdk => _options.FallbackToSdk;

    /// <summary>
    /// 向设备发一次 ISAPI 请求（<paramref name="pathAndQuery"/> 形如
    /// <c>/ISAPI/AccessControl/AcsEvent?format=json</c>）；401 时自动完成 Digest 认证后重发。
    /// </summary>
    public async Task<IsapiDigestResponse> SendAsync(
        IsapiDigestDevice device,
        HttpMethod method,
        string pathAndQuery,
        string? body = null,
        string? contentType = null,
        CancellationToken ct = default)
    {
        if (!device.IsUsable())
        {
            return IsapiDigestResponse.Fail(-1, "设备直连配置不完整（编号/地址/账号/密码）");
        }

        var url = device.ResolveBaseUrl() + pathAndQuery;
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            // ① 先不带认证发一次：设备若要求认证会回 401 并给出 Digest 挑战；
            //    若设备未启用认证，这一步就是最终结果。
            using (var probe = CreateRequest(method, url, body, contentType))
            using (var probeResponse = await _http
                       .SendAsync(probe, HttpCompletionOption.ResponseHeadersRead, ct)
                       .ConfigureAwait(false))
            {
                if (probeResponse.StatusCode != HttpStatusCode.Unauthorized)
                {
                    return await ReadResponseAsync(probeResponse, startedAt, ct).ConfigureAwait(false);
                }

                var challenge = ParseDigestChallenge(probeResponse.Headers.WwwAuthenticate.ToString());
                if (challenge is null || !challenge.ContainsKey("nonce"))
                {
                    return IsapiDigestResponse.Fail(
                        (int)probeResponse.StatusCode,
                        "设备未返回可用的 Digest 认证头（WWW-Authenticate）");
                }

                // ② 计算 Digest 响应并重发（uri 用目标路径，与设备校验一致）。
                var authorization = BuildDigestAuthorization(
                    method.Method,
                    new Uri(url).PathAndQuery,
                    device.UserName,
                    device.Password,
                    challenge,
                    cnonce: Md5Hex(Guid.NewGuid().ToString("N")),
                    nonceCount: "00000001");

                using var authorized = CreateRequest(method, url, body, contentType);
                authorized.Headers.TryAddWithoutValidation("Authorization", authorization);

                using var response = await _http
                    .SendAsync(authorized, HttpCompletionOption.ResponseHeadersRead, ct)
                    .ConfigureAwait(false);

                return await ReadResponseAsync(response, startedAt, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "ISAPI Digest 请求失败：{Url}", url);

            return IsapiDigestResponse.Fail(-1, ex.GetBaseException().Message, (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
    }

    /// <summary>
    /// 下载某设备 <c>pictureURL</c> 指向的抓拍图；任何失败都返回 null（调用方只保留 URL）。
    /// </summary>
    public async Task<byte[]?> TryDownloadPictureAsync(string deviceId, string url, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.DownloadPictures)
        {
            return null;
        }

        if (!TryResolveDevice(deviceId, out var device))
        {
            _logger.LogDebug("设备 {DeviceId} 未配置 ISAPI Digest 直连，跳过抓拍图下载。", deviceId);
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var pictureUri))
        {
            return null;
        }

        // 以图片 URL 自身的 host/端口/scheme 为准（设备可能给的是另一个地址）。
        var pictureDevice = new IsapiDigestDevice
        {
            DeviceId = device.DeviceId,
            Host = pictureUri.Host,
            Port = pictureUri.Port,
            UseHttps = string.Equals(pictureUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase),
            UserName = device.UserName,
            Password = device.Password,
        };

        try
        {
            var response = await SendAsync(pictureDevice, HttpMethod.Get, pictureUri.PathAndQuery, ct: ct).ConfigureAwait(false);
            if (!response.Success || response.Body is null)
            {
                _logger.LogDebug("下载设备 {DeviceId} 的抓拍图失败：{Error}", deviceId, response.Error);
                return null;
            }

            if (_options.MaxPictureBytes > 0 && response.Body.Length > _options.MaxPictureBytes)
            {
                _logger.LogWarning(
                    "设备 {DeviceId} 的抓拍图过大（{Size} 字节 > {Max}），只保留 URL。",
                    deviceId,
                    response.Body.Length,
                    _options.MaxPictureBytes);

                return null;
            }

            return response.Body;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "下载设备 {DeviceId} 的抓拍图异常：{Url}", deviceId, url);
            return null;
        }
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body, string? contentType)
    {
        var request = new HttpRequestMessage(method, url);

        if (!string.IsNullOrEmpty(body) && method != HttpMethod.Get)
        {
            request.Content = new StringContent(body, Encoding.UTF8, contentType ?? "application/json");
        }

        return request;
    }

    private static async Task<IsapiDigestResponse> ReadResponseAsync(
        HttpResponseMessage response,
        long startedAt,
        CancellationToken ct)
    {
        byte[] bytes;
        try
        {
            bytes = await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return IsapiDigestResponse.Fail((int)response.StatusCode, $"读取响应失败：{ex.GetBaseException().Message}");
        }

        var contentType = response.Content.Headers.ContentType?.ToString();
        var elapsedMs = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

        if (!response.IsSuccessStatusCode)
        {
            return new IsapiDigestResponse
            {
                Success = false,
                StatusCode = (int)response.StatusCode,
                ContentType = contentType,
                Body = bytes,
                Text = DecodeText(contentType, bytes),
                Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}".Trim(),
                ElapsedMs = elapsedMs,
            };
        }

        return new IsapiDigestResponse
        {
            Success = true,
            StatusCode = (int)response.StatusCode,
            ContentType = contentType,
            Body = bytes,
            Text = DecodeText(contentType, bytes),
            ElapsedMs = elapsedMs,
        };
    }

    /// <summary>文本响应解码为字符串；图片等二进制返回 null。</summary>
    private static string? DecodeText(string? contentType, byte[] bytes)
        => contentType is not null && contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? null
            : Encoding.UTF8.GetString(bytes);

    /// <summary>
    /// 解析 <c>WWW-Authenticate: Digest ...</c> 的认证参数（realm / nonce / qop / opaque / algorithm）。
    /// </summary>
    internal static Dictionary<string, string>? ParseDigestChallenge(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return null;
        }

        var index = header.IndexOf("Digest", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return null;
        }

        var payload = header[(index + "Digest".Length)..];
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in ChallengePattern.Matches(payload))
        {
            var value = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            result[match.Groups[1].Value.Trim()] = value;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// 按 RFC 2617 计算 <c>Authorization: Digest ...</c> 头
    /// （支持 <c>qop=auth</c> 与不带 qop 的 RFC 2069 形式、<c>algorithm=MD5 / MD5-sess</c>）。
    /// </summary>
    internal static string BuildDigestAuthorization(
        string method,
        string uri,
        string userName,
        string password,
        IReadOnlyDictionary<string, string> challenge,
        string cnonce,
        string nonceCount)
    {
        var realm = challenge.TryGetValue("realm", out var r) ? r : string.Empty;
        var nonce = challenge.TryGetValue("nonce", out var n) ? n : string.Empty;
        var algorithm = challenge.TryGetValue("algorithm", out var a) && !string.IsNullOrWhiteSpace(a) ? a : "MD5";
        var qop = ResolveQop(challenge);

        var ha1 = Md5Hex($"{userName}:{realm}:{password}");
        if (string.Equals(algorithm, "MD5-sess", StringComparison.OrdinalIgnoreCase))
        {
            ha1 = Md5Hex($"{ha1}:{nonce}:{cnonce}");
        }

        var ha2 = Md5Hex($"{method}:{uri}");

        var response = qop is null
            ? Md5Hex($"{ha1}:{nonce}:{ha2}")
            : Md5Hex($"{ha1}:{nonce}:{nonceCount}:{cnonce}:{qop}:{ha2}");

        var builder = new StringBuilder();
        builder.Append("Digest ")
               .Append($"username=\"{userName}\",")
               .Append($"realm=\"{realm}\",")
               .Append($"nonce=\"{nonce}\",")
               .Append($"uri=\"{uri}\",")
               .Append($"algorithm={algorithm},")
               .Append($"response=\"{response}\"");

        if (qop is not null)
        {
            builder.Append($",qop={qop},nc={nonceCount},cnonce=\"{cnonce}\"");
        }

        if (challenge.TryGetValue("opaque", out var opaque) && !string.IsNullOrWhiteSpace(opaque))
        {
            builder.Append($",opaque=\"{opaque}\"");
        }

        return builder.ToString();
    }

    /// <summary>取 qop：优先 <c>auth</c>（不支持 <c>auth-int</c>，无可用值时退回 RFC 2069 形式）。</summary>
    private static string? ResolveQop(IReadOnlyDictionary<string, string> challenge)
    {
        if (!challenge.TryGetValue("qop", out var qop) || string.IsNullOrWhiteSpace(qop))
        {
            return null;
        }

        var options = qop.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return options.FirstOrDefault(o => string.Equals(o, "auth", StringComparison.OrdinalIgnoreCase));
    }

    internal static string Md5Hex(string text)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    /// <summary>挑战参数：<c>key=value</c> 或 <c>key="value"</c>。</summary>
    private static readonly Regex ChallengePattern = new(
        @"([A-Za-z0-9_\-]+)\s*=\s*(?:""([^""]*)""|([^,\s]+))",
        RegexOptions.Compiled);

    public void Dispose() => _http.Dispose();
}

/// <summary>一次 ISAPI Digest 请求的结果。</summary>
public sealed class IsapiDigestResponse
{
    /// <summary>HTTP 2xx。</summary>
    public bool Success { get; init; }

    /// <summary>HTTP 状态码；-1 表示请求未发出/连接失败。</summary>
    public int StatusCode { get; init; }

    /// <summary>响应 Content-Type。</summary>
    public string? ContentType { get; init; }

    /// <summary>响应原始字节。</summary>
    public byte[]? Body { get; init; }

    /// <summary>文本形式的响应（图片等二进制为 null）。</summary>
    public string? Text { get; init; }

    /// <summary>失败原因。</summary>
    public string? Error { get; init; }

    /// <summary>耗时（毫秒）。</summary>
    public long ElapsedMs { get; init; }

    public static IsapiDigestResponse Fail(int statusCode, string error, long elapsedMs = 0) => new()
    {
        Success = false,
        StatusCode = statusCode,
        Error = error,
        ElapsedMs = elapsedMs,
    };
}
