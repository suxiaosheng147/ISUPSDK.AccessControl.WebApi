using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 门禁控制器基类。
///
/// 目标设备解析规则（按优先级）：
/// <list type="number">
/// <item>请求头 <c>X-Device-Id</c>（设备 ID / 序列号）；</item>
/// <item>查询参数 <c>deviceId</c>；</item>
/// <item>查询参数 <c>userId</c>（SDK 分配的 UserID）；</item>
/// <item>当且仅当只有一台在线设备时，自动选用该设备。</item>
/// </list>
/// </summary>
[ApiController]
[Produces("application/json")]
public abstract class AccessControlControllerBase : ControllerBase
{
    protected AccessControlControllerBase(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        ILogger logger)
    {
        Cms = cms;
        AccessControl = accessControl;
        Tasks = tasks;
        Logger = logger;
    }

    protected IsupCmsService Cms { get; }

    protected IsupIsapiService AccessControl { get; }

    /// <summary>异步下发任务队列（写操作入队用）。</summary>
    protected AcsTaskService Tasks { get; }

    protected ILogger Logger { get; }

    /// <summary>解析目标设备。</summary>
    protected IsupDevice ResolveDevice([FromQuery] string? deviceId = null, [FromQuery] int? userId = null)
    {
        var headerDeviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
        var effectiveDeviceId = !string.IsNullOrWhiteSpace(headerDeviceId) ? headerDeviceId : deviceId;
        var effectiveUserId = userId;

        if (effectiveUserId is null &&
            int.TryParse(Request.Headers["X-Device-UserId"].FirstOrDefault(), out var headerUserId))
        {
            effectiveUserId = headerUserId;
        }

        return Cms.ResolveDevice(effectiveDeviceId, effectiveUserId);
    }

    /// <summary>
    /// 解析异步任务的目标设备 ID（<b>允许设备离线</b>）。
    ///
    /// 规则同 <see cref="ResolveDevice"/>：显式 <c>deviceId</c> 直接采用（即使离线）；
    /// 否则按 <c>userId</c> 或"仅一台在线设备"推断；都确定不了才报错。
    /// </summary>
    protected string ResolveTaskDeviceId([FromQuery] string? deviceId = null, [FromQuery] int? userId = null)
    {
        var headerDeviceId = Request.Headers["X-Device-Id"].FirstOrDefault();
        var effectiveDeviceId = !string.IsNullOrWhiteSpace(headerDeviceId) ? headerDeviceId : deviceId;

        if (!string.IsNullOrWhiteSpace(effectiveDeviceId))
        {
            return effectiveDeviceId!;
        }

        var effectiveUserId = userId;
        if (effectiveUserId is null &&
            int.TryParse(Request.Headers["X-Device-UserId"].FirstOrDefault(), out var headerUserId))
        {
            effectiveUserId = headerUserId;
        }

        if (effectiveUserId is { } uid && Cms.TryGetDevice(uid, out var byUser))
        {
            return byUser.DeviceId;
        }

        var online = Cms.Devices.Where(d => d.Online).ToArray();
        if (online.Length == 1)
        {
            return online[0].DeviceId;
        }

        throw new IsupApiException(
            "无法确定目标设备：请通过 X-Device-Id 请求头或 deviceId 查询参数指定（离线设备也可入队）。");
    }

    /// <summary>把入队的任务转换为 <c>202 Accepted</c> 响应。</summary>
    protected IActionResult AcceptedTask(AcsTask task, string message = "任务已入队，可用任务 ID 查询执行结果")
        => Accepted(ApiResponse<AcsTaskDto>.Ok(AcsTaskDto.From(task), message));

    /// <summary>
    /// 把下发结果转换为 HTTP 响应：
    /// SDK 调用成功且设备接受 → 200；SDK 失败 → 502；设备拒绝 → 400。
    /// </summary>
    protected IActionResult FromResult(IsapiOperationResult result)
    {
        if (!result.SdkSuccess)
        {
            return StatusCode(StatusCodes.Status502BadGateway, ApiResponse<IsapiOperationResult>.Fail(
                (int)result.SdkErrorCode,
                $"ISAPI 透传失败（SDK 错误码 {result.SdkErrorCode}：{result.SdkErrorText}）",
                result));
        }

        if (!result.DeviceSuccess)
        {
            var subStatus = result.Status?.SubStatusCode ?? "未知";
            return BadRequest(ApiResponse<IsapiOperationResult>.Fail(
                result.Status?.StatusCode ?? -1,
                $"设备拒绝本次操作：{result.Status?.StatusString}（subStatusCode={subStatus}）",
                result));
        }

        return Ok(ApiResponse<IsapiOperationResult>.Ok(result));
    }

    /// <summary>查询结果转换为 HTTP 响应。</summary>
    protected IActionResult FromQuery<T>(IsapiQueryResult<T> result, bool treatNoMatchAsSuccess = true)
    {
        if (!result.Success)
        {
            if (treatNoMatchAsSuccess &&
                string.Equals(result.ResponseStatusString, "NO MATCH", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(ApiResponse<IsapiQueryResult<T>>.Ok(result));
            }

            return BadRequest(ApiResponse<IsapiQueryResult<T>>.Fail(
                result.Status?.StatusCode ?? -1,
                $"设备查询失败：{result.Status?.StatusString}（subStatusCode={result.Status?.SubStatusCode}）",
                result));
        }

        return Ok(ApiResponse<IsapiQueryResult<T>>.Ok(result));
    }
}
