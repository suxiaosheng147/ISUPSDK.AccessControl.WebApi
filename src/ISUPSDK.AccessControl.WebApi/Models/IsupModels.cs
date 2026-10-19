namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>
/// 设备注册表中的一条设备记录（由 ISUP CMS 注册回调维护）。
/// </summary>
public sealed class IsupDevice
{
    /// <summary>SDK 分配的登录句柄（<c>lLoginID</c> / UserId），调用透传与配置接口时必须携带。</summary>
    public int UserId { get; set; }

    /// <summary>设备 ID（序列号）。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>注册报文中的设备序列号。</summary>
    public string SerialNumber { get; set; } = string.Empty;

    /// <summary>完整序列号。</summary>
    public string FullSerialNumber { get; set; } = string.Empty;

    /// <summary>设备名称。</summary>
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>设备真实型号。</summary>
    public string? DeviceType { get; set; }

    /// <summary>设备显示型号。</summary>
    public string? DeviceTypeDisplay { get; set; }

    /// <summary>固件版本。</summary>
    public string FirmwareVersion { get; set; } = string.Empty;

    /// <summary>注册时协商的协议版本（2 / 4 / 5）。</summary>
    public byte ProtocolVersion { get; set; }

    /// <summary>设备当前协议版本（与注册报文一致）。</summary>
    public byte DeviceProtocolVersion { get; set; }

    /// <summary>设备上报的协议组（设备注册报文中的能力字节）。</summary>
    public byte[]? Protocols { get; set; }

    /// <summary>最近一次心跳时间。</summary>
    public DateTimeOffset? LastHeartbeatAt { get; set; }

    /// <summary>设备远端地址（IP:Port）。</summary>
    public string RemoteAddress { get; set; } = string.Empty;

    /// <summary>设备注册的服务器地址。</summary>
    public string RegisterAddress { get; set; } = string.Empty;

    /// <summary>CMS 监听服务句柄。</summary>
    public int ServerHandle { get; set; }

    /// <summary>是否在线。</summary>
    public bool Online { get; set; }

    /// <summary>首次上线时间。</summary>
    public DateTimeOffset FirstOnlineAt { get; set; }

    /// <summary>最近一次状态变化时间。</summary>
    public DateTimeOffset LastChangedAt { get; set; }

    /// <summary>协议版本的可读描述。</summary>
    public string ProtocolVersionText => ProtocolVersion switch
    {
        2 => "ISUP2.0",
        4 => "ISUP4.0",
        5 => "ISUP5.0",
        _ => $"未知({ProtocolVersion})",
    };
}

/// <summary>一次 ISAPI 透传的原始报文（请求）。</summary>
public sealed record IsapiRawMessage
{
    /// <summary>请求行，例如 <c>POST /ISAPI/AccessControl/UserInfo/Record?format=json</c>。</summary>
    public string RequestUrl { get; set; } = string.Empty;

    /// <summary>请求体（JSON/XML 文本）。</summary>
    public string? Body { get; set; }

    /// <summary>透传使用的接收超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 10000;
}

/// <summary>ISAPI 透传结果。</summary>
public sealed class IsapiResult
{
    /// <summary>
    /// 本次实际使用的通道：<c>Http</c>（<c>NET_ECMS_ISAPIPassThrough</c>）/
    /// <c>Ptxml</c>（<c>NET_ECMS_Get/Put/Post/DeletePTXMLConfig</c>），按设备编号由
    /// <c>ISUP:IsapiChannel</c> 决定。
    /// </summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>SDK 调用是否成功（<c>NET_ECMS_*PTXMLConfig</c> 返回值）。</summary>
    public bool SdkSuccess { get; set; }

    /// <summary>SDK 错误码（<c>NET_ECMS_GetLastError()</c>）。</summary>
    public uint SdkErrorCode { get; set; }

    /// <summary>SDK 错误码的可读描述。</summary>
    public string? SdkErrorText { get; set; }

    /// <summary>设备返回的原始文本（UTF-8 解码）。</summary>
    public string? ResponseText { get; set; }

    /// <summary>从响应中解析出的 ResponseStatus（若响应体为 JSON 且包含）。</summary>
    public Isapi.IsapiResponseStatus? Status { get; set; }

    /// <summary>透传耗时（毫秒）。</summary>
    public long ElapsedMs { get; set; }

    /// <summary>设备是否报告成功（statusCode == 1 或 200；无 ResponseStatus 时以有响应体为准）。</summary>
    public bool DeviceSuccess => Status is null
        ? !string.IsNullOrWhiteSpace(ResponseText)
        : Status.StatusCode == 1 || Status.StatusCode == 200;
}

/// <summary>统一 API 响应包装：{ "success": true, "code": 0, "message": null, "data": { } }。</summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; set; }

    public int Code { get; set; }

    public string? Message { get; set; }

    public T? Data { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null)
        => new() { Success = true, Code = 0, Message = message, Data = data };

    public static ApiResponse<T> Fail(int code, string message, T? data = default)
        => new() { Success = false, Code = code, Message = message, Data = data };
}

/// <summary>业务层异常，携带 HTTP 状态码与业务码。</summary>
public sealed class IsupApiException : Exception
{
    public IsupApiException(string message, int statusCode = StatusCodes.Status400BadRequest, int code = -1)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }

    public int StatusCode { get; }

    public int Code { get; }
}

/// <summary>ISAPI 报文使用的 HTTP 方法（透传请求行用）。</summary>
public enum IsapiHttpMethod
{
    Get,
    Post,
    Put,
    Delete,
}

/// <summary>ISUP 报警回调广播的事件参数。</summary>
public sealed class IsupAlarmEventArgs : EventArgs
{
    /// <summary>报警监听句柄。</summary>
    public int ListenHandle { get; init; }

    /// <summary>设备 ID（由注册表按句柄反查）。</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>接收时间。</summary>
    public DateTimeOffset ReceivedAt { get; init; }

    /// <summary>报警报文原文（XML / JSON）。</summary>
    public string? Payload { get; init; }
}