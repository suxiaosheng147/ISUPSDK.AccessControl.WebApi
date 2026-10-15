using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Methods;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 设备注册表 + ISAPI 透传服务（ISUP CMS 底座）。
/// <para>
/// 设备表来自 <see cref="IsupSdkRuntime"/> 的 CMS 注册监听（注册句柄 → 设备序列号）；
/// 透传通过 ISUP CMS 的 PTXML 接口（<c>NET_ECMS_Get/Put/Post/DeletePTXMLConfig</c>）完成，
/// 与 OTAP 侧的 <c>OTAP_CMS_ISAPIPassThrough</c> 等价。
/// </para>
/// </summary>
public sealed class IsupCmsService
{
    /// <summary>SDK 错误码：输出缓冲区不足，需放大后重试。</summary>
    private const int ErrorBufferTooSmall = 43;

    /// <summary>输出缓冲区尝试序列：64KB → 256KB → 1MB。</summary>
    private static readonly uint[] BufferSizes = { 64 * 1024, 256 * 1024, 1024 * 1024 };

    private static readonly JsonSerializerOptions StatusJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly IsupSdkRuntime _runtime;
    private readonly ILogger<IsupCmsService> _logger;
    private readonly Options.IsupSdkOptions _options;
    // 键 = 设备号（重连不新增条目）。
    private readonly ConcurrentDictionary<string, DeviceRecord> _records = new(StringComparer.OrdinalIgnoreCase);

    public IsupCmsService(IsupSdkRuntime runtime, IOptions<Options.IsupSdkOptions> options, ILogger<IsupCmsService> logger)
    {
        _runtime = runtime;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>SDK 是否已完成初始化。</summary>
    public bool IsInitialized => _runtime.IsInitialized;

    /// <summary>解析到的 ISUP SDK 原生库目录。</summary>
    public string? NativeDllPath => Interop.NativeLibraryResolver.ResolvedDirectory;

    /// <summary>CMS 监听句柄集合。</summary>
    public IReadOnlyCollection<int> ListenHandles
        => _runtime.ListenHandle >= 0 ? new[] { _runtime.ListenHandle } : Array.Empty<int>();

    /// <summary>设备表（含已离线的设备）。</summary>
    public IReadOnlyCollection<IsupDevice> Devices => Snapshot();

    /// <summary>CMS 组件构建版本（<c>NET_ECMS_GetBuildVersion</c>）。</summary>
    public long BuildVersion => HCISUPCMS.NET_ECMS_GetBuildVersion();

    /// <summary>
    /// 动态启动一次 CMS 设备注册监听，返回监听句柄（&lt;0 表示失败）。
    /// <paramref name="listenType"/> 为 OTAP 专有参数，ISUP 侧忽略。
    /// </summary>
    public int StartListen(string? ip, ushort? port, byte? listenType = null) => _runtime.StartListen(ip, port);

    /// <summary>停止指定 CMS 监听句柄。</summary>
    public bool StopListen(int handle) => _runtime.StopListen(handle);

    /// <summary>是否为 ISUP 协议设备（本服务接入的设备均走 ISUP，恒为 true）。</summary>
    public static bool IsIsupDevice(IsupDevice device) => true;

    /// <summary>强制指定设备下线。</summary>
    public bool ForceLogout(int userId) => HCISUPCMS.NET_ECMS_ForceLogout(userId);

    /// <summary>按注册句柄查找设备。</summary>
    public bool TryGetDevice(int userId, out IsupDevice device)
    {
        foreach (var item in Snapshot())
        {
            if (item.UserId == userId)
            {
                device = item;
                return true;
            }
        }

        device = null!;
        return false;
    }

    /// <summary>按设备 ID 查找设备。</summary>
    public bool TryGetDeviceByDeviceId(string deviceId, out IsupDevice device)
    {
        foreach (var item in Snapshot())
        {
            if (string.Equals(item.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
            {
                device = item;
                return true;
            }
        }

        device = null!;
        return false;
    }

    /// <summary>
    /// 解析目标设备：显式 deviceId → userId → 仅有一台在线设备时自动选用。
    /// </summary>
    public IsupDevice ResolveDevice(string? deviceId, int? userId)
    {
        var online = Snapshot().Where(device => device.Online).ToArray();

        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            if (TryGetDeviceByDeviceId(deviceId, out var byId))
            {
                return byId;
            }

            throw new IsupApiException($"设备 {deviceId} 未注册（或尚未上线）。");
        }

        if (userId is { } uid)
        {
            if (TryGetDevice(uid, out var byUser))
            {
                return byUser;
            }

            throw new IsupApiException($"未找到 UserId={uid} 的设备。");
        }

        if (online.Length == 1)
        {
            return online[0];
        }

        throw new IsupApiException(online.Length == 0
            ? "当前没有在线设备。"
            : $"当前有 {online.Length} 台在线设备，请通过 X-Device-Id 请求头或 deviceId / userId 查询参数指定目标设备。");
    }

    /// <summary>
    /// 向设备透传一条 ISAPI 报文。
    /// <paramref name="request"/>.RequestUrl 支持 <c>"方法 路径"</c>（如 <c>POST /ISAPI/...?format=json</c>）
    /// 或仅路径（默认 PUT）；<paramref name="parts"/> 非空时以 multipart/form-data 组织请求体。
    /// </summary>
    public async Task<IsapiResult> SendIsapiAsync(
        int userId,
        IsapiRawMessage request,
        IReadOnlyList<IsapiMultipartPart>? parts = null,
        CancellationToken ct = default)
    {
        var device = TryGetDevice(userId, out var known) ? known : new IsupDevice { UserId = userId };
        return await SendIsapiAsync(device, request, parts, ct).ConfigureAwait(false);
    }

    /// <summary>按设备对象透传（内部会采用其注册句柄）。</summary>
    public async Task<IsapiResult> SendIsapiAsync(
        IsupDevice device,
        IsapiRawMessage request,
        IReadOnlyList<IsapiMultipartPart>? parts = null,
        CancellationToken ct = default)
        => await Task.Run(() => SendIsapiCore(device.UserId, device, request, parts), ct).ConfigureAwait(false);

    /// <summary>把透传结果映射为 <see cref="IsapiOperationResult"/>（供控制器统一返回）。</summary>
    public static IsapiOperationResult ToOperationResult(IsupDevice device, IsapiRawMessage request, IsapiResult result) => new()
    {
        UserId = device.UserId,
        DeviceId = device.DeviceId,
        RequestUrl = request.RequestUrl,
        RequestBody = request.Body,
        SdkSuccess = result.SdkSuccess,
        SdkErrorCode = result.SdkErrorCode,
        SdkErrorText = result.SdkErrorText,
        DeviceSuccess = result.DeviceSuccess,
        Status = result.Status,
        RawResponse = result.ResponseText,
        ElapsedMs = result.ElapsedMs,
    };

    #region 内部实现

    private sealed class DeviceRecord
    {
        /// <summary>当前注册句柄（设备重连后会更新为最新值）。</summary>
        public int Handle;
        public string DeviceId = string.Empty;
        public int ServerHandle;
        public DateTimeOffset FirstSeenAt;
        public DateTimeOffset LastChangedAt;

        /// <summary>最近一次出现在注册表中的时间（用于清理长期离线记录）。</summary>
        public DateTimeOffset LastSeenAt;
    }

    private IsupDevice[] Snapshot()
    {
        var handles = _runtime.DeviceHandles;
        var now = DateTimeOffset.Now;

        foreach (var pair in handles)
        {
            var handle = pair.Key;
            var deviceId = pair.Value;

            // 以「设备号」为主键：设备重连会换注册句柄，这里更新为最新句柄，
            // 保证同一台设备只有一条记录、在线判定也正确（原来以句柄为键会残留重复条目）。
            _records.AddOrUpdate(
                deviceId,
                _ => new DeviceRecord
                {
                    DeviceId = deviceId,
                    Handle = handle,
                    ServerHandle = _runtime.ListenHandle,
                    FirstSeenAt = now,
                    LastChangedAt = now,
                    LastSeenAt = now,
                },
                (_, record) =>
                {
                    if (record.Handle != handle)
                    {
                        record.Handle = handle;
                        record.LastChangedAt = now;
                    }

                    record.LastSeenAt = now;
                    return record;
                });
        }

        PruneOfflineRecords(handles, now);

        return _records.Values
            .Select(record => new IsupDevice
            {
                UserId = record.Handle,
                DeviceId = record.DeviceId,
                SerialNumber = record.DeviceId,
                FullSerialNumber = record.DeviceId,
                DeviceName = record.DeviceId,
                ProtocolVersion = 5,
                ServerHandle = record.ServerHandle,
                Online = handles.ContainsKey(record.Handle),
                FirstOnlineAt = record.FirstSeenAt,
                LastChangedAt = record.LastChangedAt,
            })
            .OrderBy(device => device.DeviceId, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>上次清理历史离线设备的时间（节流用）。</summary>
    private DateTimeOffset _lastPrune = DateTimeOffset.MinValue;

    /// <summary>
    /// 清理长期离线的历史设备记录（设备表只增不减会让列表无限累积）。
    /// 保留天数由 <c>ISUP:DeviceOfflineRetentionDays</c> 控制，&lt;=0 表示不清理。
    /// </summary>
    private void PruneOfflineRecords(IReadOnlyDictionary<int, string> handles, DateTimeOffset now)
    {
        var retentionDays = _options.DeviceOfflineRetentionDays;
        if (retentionDays <= 0 || now - _lastPrune < TimeSpan.FromMinutes(30))
        {
            return;
        }

        _lastPrune = now;
        var threshold = now.AddDays(-retentionDays);

        foreach (var pair in _records)
        {
            // 句柄仍在注册表 → 在线，不清理。
            if (handles.ContainsKey(pair.Value.Handle))
            {
                continue;
            }

            if (pair.Value.LastSeenAt < threshold)
            {
                _records.TryRemove(pair.Key, out _);
            }
        }
    }

    private IsapiResult SendIsapiCore(int loginId, IsupDevice device, IsapiRawMessage request, IReadOnlyList<IsapiMultipartPart>? parts)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new IsapiResult();
        var (method, urlPath) = SplitRequestLine(request.RequestUrl);
        byte[] body;
        if (parts is { Count: > 0 })
        {
            body = BuildMultipartBody(out _, parts);
        }
        else
        {
            body = Encoding.UTF8.GetBytes(request.Body ?? string.Empty);
        }

        var succeeded = false;

        foreach (var bufferSize in BufferSizes)
        {
            var (ok, text, errorCode) = InvokePassthrough(loginId, method, urlPath, body, bufferSize, request.TimeoutMs);

            if (ok)
            {
                result.SdkSuccess = true;
                result.ResponseText = text;
                result.Status = ParseStatus(text);
                succeeded = true;
                break;
            }

            if (errorCode == ErrorBufferTooSmall)
            {
                // 输出缓冲区不足：放大后重试。
                continue;
            }

            result.SdkSuccess = false;
            result.SdkErrorCode = (uint)errorCode;
            result.SdkErrorText = DescribeError(errorCode);
            result.ElapsedMs = stopwatch.ElapsedMilliseconds;
            _logger.LogWarning("ISAPI 透传失败（{Method} {Url}）：{Error}", method, urlPath, result.SdkErrorText);
            return result;
        }

        if (!succeeded)
        {
            result.SdkSuccess = false;
            result.SdkErrorCode = ErrorBufferTooSmall;
            result.SdkErrorText = "ISAPI 响应超过最大缓冲区（1MB）。";
            _logger.LogWarning("ISAPI 透传失败（{Method} {Url}）：{Error}", method, urlPath, result.SdkErrorText);
        }

        result.ElapsedMs = stopwatch.ElapsedMilliseconds;
        return result;
    }

    private static (string method, string urlPath) SplitRequestLine(string? requestUrl)
    {
        // 清洗控制字符（设备 ID 拼接时可能带入 \0）。
        var value = new string((requestUrl ?? string.Empty).Where(c => !char.IsControl(c)).ToArray()).Trim();
        var space = value.IndexOf(' ');

        if (space <= 0)
        {
            return ("PUT", value);
        }

        var method = value[..space].ToUpperInvariant();
        var path = value[(space + 1)..].Trim();

        return (string.IsNullOrEmpty(method) ? "PUT" : method, path);
    }

    /// <summary>按 RFC 2046 组织 multipart/form-data 请求体。</summary>
    private static byte[] BuildMultipartBody(out string boundary, IReadOnlyList<IsapiMultipartPart> parts)
    {
        boundary = "----IsupBoundary" + Guid.NewGuid().ToString("N");
        using var stream = new MemoryStream();

        foreach (var part in parts)
        {
            var header = new StringBuilder()
                .Append("--").Append(boundary).Append("\r\n")
                .Append("Content-Disposition: form-data; name=\"").Append(part.Name).Append('"');

            if (!string.IsNullOrWhiteSpace(part.FileName))
            {
                header.Append("; filename=\"").Append(part.FileName).Append('"');
            }

            header.Append("\r\n")
                  .Append("Content-Type: ")
                  .Append(string.IsNullOrWhiteSpace(part.ContentType) ? "application/octet-stream" : part.ContentType)
                  .Append("\r\n\r\n");

            var headerBytes = Encoding.UTF8.GetBytes(header.ToString());
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(part.Content, 0, part.Content.Length);
            stream.Write("\r\n"u8);
        }

        var closing = Encoding.UTF8.GetBytes($"--{boundary}--\r\n");
        stream.Write(closing, 0, closing.Length);

        return stream.ToArray();
    }

    private static (bool ok, string? text, int errorCode) InvokePassthrough(
        int loginId, string method, string urlPath, byte[] body, uint bufferSize, uint timeoutMs)
    {
        var urlBytes = Encoding.UTF8.GetBytes(urlPath);

        var pUrl = Marshal.AllocHGlobal(Math.Max(urlBytes.Length, 1));
        var pIn = Marshal.AllocHGlobal(Math.Max(body.Length, 1));
        var pOut = Marshal.AllocHGlobal((int)bufferSize);
        var pCfg = Marshal.AllocHGlobal(Marshal.SizeOf<HCISUPCMS.NET_EHOME_PTXML_PARAM>());

        try
        {
            Marshal.Copy(urlBytes, 0, pUrl, urlBytes.Length);

            if (body.Length > 0)
            {
                Marshal.Copy(body, 0, pIn, body.Length);
            }

            var param = new HCISUPCMS.NET_EHOME_PTXML_PARAM
            {
                pRequestUrl = pUrl,
                dwRequestUrlLen = (uint)urlBytes.Length,
                pCondBuffer = IntPtr.Zero,
                dwCondSize = 0,
                pInBuffer = pIn,
                dwInSize = (uint)body.Length,
                pOutBuffer = pOut,
                dwOutSize = bufferSize,
                dwRecvTimeOut = timeoutMs == 0 ? 5000u : timeoutMs,
                byRes = new byte[24],
            };

            Marshal.StructureToPtr(param, pCfg, false);

            var ok = method switch
            {
                "GET" => HCISUPCMS.NET_ECMS_GetPTXMLConfig(loginId, pCfg),
                "PUT" => HCISUPCMS.NET_ECMS_PutPTXMLConfig(loginId, pCfg),
                "POST" => HCISUPCMS.NET_ECMS_PostPTXMLConfig(loginId, pCfg),
                "DELETE" => HCISUPCMS.NET_ECMS_DeletePTXMLConfig(loginId, pCfg),
                _ => false,
            };

            if (!ok)
            {
                return (false, null, HCISUPCMS.NET_ECMS_GetLastError());
            }

            var updated = Marshal.PtrToStructure<HCISUPCMS.NET_EHOME_PTXML_PARAM>(pCfg);
            var length = updated.dwReturnedXMLLen > 0 ? (int)updated.dwReturnedXMLLen : (int)updated.dwOutSize;
            var text = length > 0 ? Marshal.PtrToStringUTF8(pOut) : string.Empty;

            return (true, text, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(pUrl);
            Marshal.FreeHGlobal(pIn);
            Marshal.FreeHGlobal(pOut);
            Marshal.FreeHGlobal(pCfg);
        }
    }

    private static IsapiResponseStatus? ParseStatus(string? responseText)
    {
        var json = IsapiResponseNormalizer.ToJson(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (document.RootElement.TryGetProperty("ResponseStatus", out var status) &&
                status.ValueKind == JsonValueKind.Object)
            {
                return status.Deserialize<IsapiResponseStatus>(StatusJsonOptions);
            }

            if (document.RootElement.TryGetProperty("statusCode", out _))
            {
                return document.RootElement.Deserialize<IsapiResponseStatus>(StatusJsonOptions);
            }
        }
        catch (JsonException)
        {
            // 响应不是 JSON（例如纯 XML 且无法转换），交由调用方按原始文本处理。
        }

        return null;
    }

    private static string DescribeError(int errorCode)
    {
        ErrCodeDictionary.ErrCode.TryGetValue(errorCode, out var text);
        return $"SDK 错误码 {errorCode}：{text ?? "未知错误"}";
    }

    #endregion
}
