using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 门禁控制控制器（开门/关门/门状态/门禁事件/报警输出/设备信息）。
/// </summary>
[Route("api/access-control")]
[Tags("5. 门禁控制")]
public sealed class AccessControlDoorsController : AccessControlControllerBase
{
    public AccessControlDoorsController(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        ILogger<AccessControlDoorsController> logger)
        : base(cms, accessControl, tasks, logger)
    {
    }

    /// <summary>门远程控制（开门/关门/常开/常闭）。</summary>
    /// <remarks>
    /// 对应 ISAPI：<c>PUT /ISAPI/AccessControl/RemoteControl/door/&lt;doorNo&gt;?format=json</c>。
    /// 请求体：
    /// <code>{ "doorControl": { "cmd": "open" } }</code>
    /// cmd 取值：open / close / alwaysOpen / alwaysClose。
    /// </remarks>
    [HttpPut("doors/{doorNo:int}/control")]
    public async Task<IActionResult> ControlDoorAsync(
        int doorNo,
        [FromBody] DoorControlRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.ControlDoorAsync(device, doorNo, request.Cmd, request.TimeoutMs, ct);
        return FromResult(result);
    }

    /// <summary>开门（快捷方式）：PUT /api/access-control/doors/1/open。</summary>
    [HttpPut("doors/{doorNo:int}/open")]
    public Task<IActionResult> OpenDoorAsync(
        int doorNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
        => ControlDoorAsync(doorNo, new DoorControlRequest { Cmd = IsapiDoorCommand.Open, TimeoutMs = timeoutMs ?? 10000 }, deviceId, userId, ct);

    /// <summary>关门（快捷方式）。</summary>
    [HttpPut("doors/{doorNo:int}/close")]
    public Task<IActionResult> CloseDoorAsync(
        int doorNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
        => ControlDoorAsync(doorNo, new DoorControlRequest { Cmd = IsapiDoorCommand.Close, TimeoutMs = timeoutMs ?? 10000 }, deviceId, userId, ct);

    /// <summary>常开（快捷方式）。</summary>
    [HttpPut("doors/{doorNo:int}/always-open")]
    public Task<IActionResult> AlwaysOpenDoorAsync(
        int doorNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
        => ControlDoorAsync(doorNo, new DoorControlRequest { Cmd = IsapiDoorCommand.AlwaysOpen, TimeoutMs = timeoutMs ?? 10000 }, deviceId, userId, ct);

    /// <summary>常闭（快捷方式）。</summary>
    [HttpPut("doors/{doorNo:int}/always-close")]
    public Task<IActionResult> AlwaysCloseDoorAsync(
        int doorNo,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
        => ControlDoorAsync(doorNo, new DoorControlRequest { Cmd = IsapiDoorCommand.AlwaysClose, TimeoutMs = timeoutMs ?? 10000 }, deviceId, userId, ct);

    /// <summary>查询门状态（GET /ISAPI/AccessControl/DoorStatus/&lt;doorNo&gt;）。</summary>
    [HttpGet("doors/{doorNo:int}/status")]
    public async Task<IActionResult> GetDoorStatusAsync(
        int doorNo = 1,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetDoorStatusAsync(device, doorNo, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询门禁工作状态（GET /ISAPI/AccessControl/AcsWorkStatus）。</summary>
    [HttpGet("work-status")]
    public async Task<IActionResult> GetWorkStatusAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetWorkStatusAsync(device, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    /// <summary>查询门禁能力集（GET /ISAPI/AccessControl/capabilities，返回 XML）。</summary>
    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilitiesAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetAccessControlCapabilitiesAsync(device, timeoutMs ?? 10000, ct);
        return FromResult(result);
    }

    /// <summary>查询门禁事件（POST /ISAPI/AccessControl/AcsEvent）。</summary>
    /// <remarks>
    /// 示例请求体：
    /// <code>
    /// {
    ///   "startTime": "2024-12-01T00:00:00+08:00",
    ///   "endTime": "2024-12-02T00:00:00+08:00",
    ///   "maxResults": 30,
    ///   "major": [5],
    ///   "minor": [1, 75]
    /// }
    /// </code>
    /// major：1-报警、2-异常、3-操作、5-门事件。minor 取值随设备型号变化。
    /// </remarks>
    [HttpPost("events/search")]
    public async Task<IActionResult> SearchEventsAsync(
        [FromBody] AcsEventSearchRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.SearchEventsAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    /// <summary>触发报警输出（PUT /ISAPI/System/IO/outputs/{id}/trigger，仅接受 XML 报文）。</summary>
    /// <param name="outputId">报警输出口编号，从 1 开始。</param>
    /// <param name="command">触发命令：high（闭合） / low（断开） / pulse（脉冲）。</param>
    /// <param name="deviceId">目标设备 ID（也可用请求头 X-Device-Id）。</param>
    /// <param name="userId">目标设备 UserID。</param>
    /// <param name="timeoutMs">透传接收超时（毫秒）。</param>
    /// <param name="ct">取消令牌。</param>
    [HttpPut("outputs/{outputId:int}/trigger")]
    public async Task<IActionResult> TriggerOutputAsync(
        int outputId,
        [FromQuery] string command = "high",
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.TriggerOutputAsync(device, outputId, command, timeoutMs ?? 10000, ct);
        return FromResult(result);
    }

    /// <summary>查询设备信息（GET /ISAPI/System/deviceInfo）—用于验证透传链路是否可用。</summary>
    [HttpGet("device-info")]
    public async Task<IActionResult> GetDeviceInfoAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetDeviceInfoAsync(device, timeoutMs ?? 10000, ct);
        return Ok(ApiResponse<IsapiOperationResult>.Ok(result));
    }
}
