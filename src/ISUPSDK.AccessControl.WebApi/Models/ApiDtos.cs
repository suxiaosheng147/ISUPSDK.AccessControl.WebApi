using ISUPSDK.AccessControl.WebApi.Models.Isapi;

namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>设备注册信息 DTO（GET /api/devices）。</summary>
public sealed class DeviceInfoDto
{
    public int UserId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string SerialNumber { get; set; } = string.Empty;

    public string FullSerialNumber { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public string? DeviceType { get; set; }

    public string? DeviceTypeDisplay { get; set; }

    public string FirmwareVersion { get; set; } = string.Empty;

    public string ProtocolVersion { get; set; } = string.Empty;

    public byte ProtocolVersionCode { get; set; }

    public byte[]? Protocols { get; set; }

    public string RemoteAddress { get; set; } = string.Empty;

    public string RegisterAddress { get; set; } = string.Empty;

    public int ServerHandle { get; set; }

    public bool Online { get; set; }

    public DateTimeOffset FirstOnlineAt { get; set; }

    public DateTimeOffset LastChangedAt { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public static DeviceInfoDto From(IsupDevice device) => new()
    {
        UserId = device.UserId,
        DeviceId = device.DeviceId,
        SerialNumber = device.SerialNumber,
        FullSerialNumber = device.FullSerialNumber,
        DeviceName = device.DeviceName,
        DeviceType = device.DeviceType,
        DeviceTypeDisplay = device.DeviceTypeDisplay,
        FirmwareVersion = device.FirmwareVersion,
        ProtocolVersion = device.ProtocolVersionText,
        ProtocolVersionCode = device.DeviceProtocolVersion,
        Protocols = device.Protocols,
        RemoteAddress = device.RemoteAddress,
        RegisterAddress = device.RegisterAddress,
        ServerHandle = device.ServerHandle,
        Online = device.Online,
        FirstOnlineAt = device.FirstOnlineAt,
        LastChangedAt = device.LastChangedAt,
        LastHeartbeatAt = device.LastHeartbeatAt,
    };
}

/// <summary>
/// 下发类接口的统一返回：包含设备 UserID、请求行、设备原始响应与解析出的 ResponseStatus。
/// </summary>
public sealed class IsapiOperationResult
{
    /// <summary>目标设备 UserID。</summary>
    public int UserId { get; set; }

    /// <summary>目标设备 ID。</summary>
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>实际发送给设备的请求行。</summary>
    public string RequestUrl { get; set; } = string.Empty;

    /// <summary>实际发送给设备的请求体。</summary>
    public string? RequestBody { get; set; }

    /// <summary>SDK 调用是否成功。</summary>
    public bool SdkSuccess { get; set; }

    /// <summary>SDK 错误码（0 表示成功）。</summary>
    public uint SdkErrorCode { get; set; }

    /// <summary>SDK 错误描述。</summary>
    public string? SdkErrorText { get; set; }

    /// <summary>设备是否接受了本次操作。</summary>
    public bool DeviceSuccess { get; set; }

    /// <summary>设备返回的状态（statusCode/subStatusCode 等）。</summary>
    public IsapiResponseStatus? Status { get; set; }

    /// <summary>设备返回的原始报文。</summary>
    public string? RawResponse { get; set; }

    /// <summary>透传耗时（毫秒）。</summary>
    public long ElapsedMs { get; set; }

    public static IsapiOperationResult From(IsupDevice device, IsapiRawMessage request, IsapiResult result) => new()
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
}

/// <summary>人员/卡片查询结果返回。</summary>
public sealed class IsapiQueryResult<T>
{
    public int UserId { get; set; }

    public string DeviceId { get; set; } = string.Empty;

    public string RequestUrl { get; set; } = string.Empty;

    public bool Success { get; set; }

    public IsapiResponseStatus? Status { get; set; }

    /// <summary>解析后的业务数据。</summary>
    public T? Data { get; set; }

    /// <summary>设备原始响应（便于排查固件字段差异）。</summary>
    public string? RawResponse { get; set; }

    public long ElapsedMs { get; set; }

    /// <summary>查询状态字符串：OK / MORE / NO MATCH。</summary>
    public string? ResponseStatusString { get; set; }

    public int NumOfMatches { get; set; }

    public int TotalMatches { get; set; }
}

/// <summary>人脸图片下发请求（multipart）。</summary>
public sealed class FaceDownloadRequest
{
    /// <summary>人脸库类型，默认 FDLib。</summary>
    public string FaceLibType { get; set; } = "blackFD";

    /// <summary>人脸库 ID，默认 1。</summary>
    public string FdId { get; set; } = "1";

    /// <summary>人脸图片 ID（必填，通常与人员工号一致）。</summary>
    public string FpId { get; set; } = string.Empty;

    /// <summary>人员姓名（可选）。</summary>
    public string? Name { get; set; }

    /// <summary>关联人员工号（可选）。</summary>
    public string? EmployeeNo { get; set; }

    /// <summary>是否覆盖已有人脸图片。</summary>
    public bool? OverWrite { get; set; }

    /// <summary>人脸图片二进制内容（当未提供 <see cref="ImageUrl"/> / <see cref="ImageBase64"/> 时使用）。</summary>
    public byte[]? Image { get; set; }

    /// <summary>人脸图片 Base64 内容。</summary>
    public string? ImageBase64 { get; set; }

    /// <summary>人脸图片 URL（由本服务下载后下发）。</summary>
    public string? ImageUrl { get; set; }

    /// <summary>图片 MIME 类型。</summary>
    public string? ContentType { get; set; }

    /// <summary>上传的文件名。</summary>
    public string? FileName { get; set; }

    /// <summary>图片分段的表单单元名称，海康门禁 FDLib/FaceDataRecord 要求固定为 img。</summary>
    public string ImagePartName { get; set; } = "img";

    /// <summary>JSON 分段的表单单元名称，海康门禁要求固定为 FaceDataRecord。</summary>
    public string JsonPartName { get; set; } = "FaceDataRecord";

    /// <summary>接收超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>人员批量下发请求。</summary>
public sealed class UserBatchRequest
{
    public List<IsapiUserInfo> Users { get; set; } = new();

    /// <summary>为 true 时使用 PUT + mode=modify 按工号修改；否则 POST 新增（已存在则报错）。</summary>
    public bool Update { get; set; }

    /// <summary>接收超时（毫秒）。</summary>
    public uint TimeoutMs { get; set; } = 15000;
}

/// <summary>卡片批量下发请求。</summary>
public sealed class CardBatchRequest
{
    public List<IsapiCardInfo> Cards { get; set; } = new();

    public bool Update { get; set; }

    public uint TimeoutMs { get; set; } = 15000;
}
