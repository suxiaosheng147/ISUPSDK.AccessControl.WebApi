using ISUPSDK.AccessControl.WebApi.Interop;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 设备与平台管理控制器：设备注册表查询、监听管理、原始 ISAPI 透传。
/// </summary>
[ApiController]
[Route("api/devices")]
[Produces("application/json")]
[Tags("6. 设备与平台管理")]
public sealed class DevicesController : ControllerBase
{
    private readonly IsupCmsService _cms;
    private readonly IsupIsapiService _accessControl;
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(IsupCmsService cms, IsupIsapiService accessControl, ILogger<DevicesController> logger)
    {
        _cms = cms;
        _accessControl = accessControl;
        _logger = logger;
    }

    /// <summary>查询已注册设备（含在线状态）。</summary>
    [HttpGet]
    public IActionResult List([FromQuery] bool onlineOnly = false)
    {
        var devices = _cms.Devices
            .Where(d => !onlineOnly || d.Online)
            .OrderBy(d => d.DeviceId, StringComparer.OrdinalIgnoreCase)
            .Select(DeviceInfoDto.From)
            .ToList();

        return Ok(ApiResponse<List<DeviceInfoDto>>.Ok(devices));
    }

    /// <summary>查询单个设备。</summary>
    [HttpGet("{deviceId}")]
    public IActionResult Get(string deviceId)
    {
        if (!_cms.TryGetDeviceByDeviceId(deviceId, out var device))
        {
            throw new IsupApiException($"设备 '{deviceId}' 未注册。", StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<DeviceInfoDto>.Ok(DeviceInfoDto.From(device)));
    }

    /// <summary>查询 SDK 运行状态（初始化状态、原生库路径、监听句柄、构建版本）。</summary>
    [HttpGet("sdk")]
    public IActionResult SdkStatus()
    {
        var snapshot = new
        {
            Initialized = _cms.IsInitialized,
            NativeDll = _cms.NativeDllPath,
            BuildVersion = $"0x{_cms.BuildVersion:X8}",
            ListenHandles = _cms.ListenHandles.ToArray(),
            SearchDirectories = NativeLibraryResolver.Directories.ToArray(),
            OnlineDevices = _cms.Devices.Count(d => d.Online),
            TotalDevices = _cms.Devices.Count,
            Process = new
            {
                Is64Bit = Environment.Is64BitProcess,
                MachineName = Environment.MachineName,
                Framework = Environment.Version.ToString(),
            },
        };

        return Ok(ApiResponse<object>.Ok(snapshot));
    }

    /// <summary>开启设备注册监听（应用启动已自动开启，此处用于手动补充监听地址/端口）。</summary>
    [HttpPost("listen")]
    public IActionResult StartListen([FromQuery] string? ip = null, [FromQuery] ushort? port = null, [FromQuery] byte? listenType = null)
    {
        var handle = _cms.StartListen(ip, port, listenType);
        return Ok(ApiResponse<object>.Ok(new { ListenHandle = handle }));
    }

    /// <summary>关闭指定监听。</summary>
    [HttpDelete("listen/{handle:int}")]
    public IActionResult StopListen(int handle)
    {
        var result = _cms.StopListen(handle);
        return result
            ? Ok(ApiResponse<object>.Ok(new { ListenHandle = handle, Stopped = true }))
            : throw new IsupApiException($"停用监听句柄 {handle} 失败。", StatusCodes.Status500InternalServerError);
    }

    /// <summary>强制设备下线并释放 SDK 资源。</summary>
    [HttpDelete("{deviceId}/session")]
    public IActionResult ForceLogout(string deviceId)
    {
        if (!_cms.TryGetDeviceByDeviceId(deviceId, out var device))
        {
            throw new IsupApiException($"设备 '{deviceId}' 未注册。", StatusCodes.Status404NotFound);
        }

        var result = _cms.ForceLogout(device.UserId);
        return Ok(ApiResponse<object>.Ok(new { device.DeviceId, device.UserId, Success = result }));
    }

    /// <summary>
    /// 原始 ISAPI 透传（调试与特殊机型适配使用）。
    /// <c>requestUrl</c> 必须包含方法前缀，例如
    /// <c>POST /ISAPI/AccessControl/UserInfo/Search?format=json</c>。
    /// </summary>
    [HttpPost("{deviceId}/isapi")]
    public async Task<IActionResult> PassthroughAsync(
        string deviceId,
        [FromBody] RawIsapiRequest request,
        CancellationToken ct = default)
    {
        if (!_cms.TryGetDeviceByDeviceId(deviceId, out var device))
        {
            throw new IsupApiException($"设备 '{deviceId}' 未注册。", StatusCodes.Status404NotFound);
        }

        var raw = new IsapiRawMessage
        {
            RequestUrl = request.RequestUrl,
            Body = request.Body,
            TimeoutMs = request.TimeoutMs,
        };

        var result = await _accessControl.SendRawAsync(device, raw, ct);
        return Ok(ApiResponse<IsapiOperationResult>.Ok(result));
    }
}
