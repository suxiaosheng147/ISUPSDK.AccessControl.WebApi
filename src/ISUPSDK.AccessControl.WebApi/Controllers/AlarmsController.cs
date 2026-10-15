using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 报警与门禁事件控制器（ISUP 报警组件）。
/// <para>
/// 设备上报的门禁事件（刷卡 / 人脸 / 开门 / 异常）由 SDK 的报警监听回调接收，
/// 经 <see cref="IsupAmsService"/> 缓冲后即可查询——实时且无需轮询。
/// 另提供 <c>httpHosts</c> 下发，让设备以 HTTP 方式把事件 POST 到平台。
/// </para>
/// </summary>
[ApiController]
[Route("api/alarms")]
[Produces("application/json")]
[Tags("7. 报警与门禁事件")]
public sealed class AlarmsController : ControllerBase
{
    private readonly IsupAmsService _ams;
    private readonly IsupCmsService _cms;
    private readonly IsupIsapiService _accessControl;
    private readonly AcsHttpPushOptions _acsHttpPush;
    private readonly ILogger<AlarmsController> _logger;

    public AlarmsController(
        IsupAmsService ams,
        IsupCmsService cms,
        IsupIsapiService accessControl,
        IOptions<AcsHttpPushOptions> acsHttpPush,
        ILogger<AlarmsController> logger)
    {
        _ams = ams;
        _cms = cms;
        _accessControl = accessControl;
        _acsHttpPush = acsHttpPush.Value;
        _logger = logger;
    }

    /// <summary>报警监听运行状态（是否初始化、监听句柄、已接收事件数等）。</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(ApiResponse<AmsStatus>.Ok(_ams.GetStatus()));

    /// <summary>开启一路报警监听（应用启动已自动开启 UDP/TCP/MQTT 三路，此处用于补充监听地址）。</summary>
    [HttpPost("listen")]
    public IActionResult StartListen([FromQuery] string? ip = null, [FromQuery] ushort? port = null)
    {
        var handle = _ams.StartListen(ip, port);
        if (handle < 0)
        {
            throw new IsupApiException("报警监听启动失败，请查看服务端日志中的 SDK 错误码。", StatusCodes.Status502BadGateway);
        }

        return Ok(ApiResponse<object>.Ok(new { ListenHandle = handle, Address = _ams.ListenAddress }));
    }

    /// <summary>停止指定报警监听。</summary>
    [HttpDelete("listen/{handle:int}")]
    public IActionResult StopListen(int handle)
    {
        if (!_ams.StopListen(handle))
        {
            throw new IsupApiException($"停用报警监听句柄 {handle} 失败（句柄不存在或 SDK 报错）。");
        }

        return Ok(ApiResponse<object>.Ok(new { ListenHandle = handle, Stopped = true }));
    }

    /// <summary>
    /// 查询已接收的报警/门禁事件（内存缓冲，最新在前）。
    /// </summary>
    /// <remarks>
    /// 典型用法：
    /// <list type="bullet">
    /// <item>增量拉取：记下上次最大的 <c>sequence</c>，下次带 <c>afterSequence</c>；</item>
    /// <item>只看门禁：<c>?accessControlOnly=true</c>；</item>
    /// <item>按设备：<c>?deviceId=FZ7955795</c>；</item>
    /// <item>按时间：<c>?from=2026-10-15T00:00:00%2B08:00</c>。</item>
    /// </list>
    /// </remarks>
    [HttpGet("events")]
    public IActionResult QueryEvents([FromQuery] AlarmEventQuery query)
    {
        var limit = Math.Clamp(query.Limit, 1, 1000);
        var all = _ams.Snapshot();

        IEnumerable<IsupAlarmEvent> filtered = all;

        if (!string.IsNullOrWhiteSpace(query.DeviceId))
        {
            filtered = filtered.Where(e =>
                e.DeviceId.Contains(query.DeviceId, StringComparison.OrdinalIgnoreCase) ||
                (e.SerialNumber?.Contains(query.DeviceId, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (query.AccessControlOnly == true)
        {
            filtered = filtered.Where(e => e.IsAccessControl);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            filtered = filtered.Where(e => string.Equals(e.Source, query.Source, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            filtered = filtered.Where(e =>
                e.EventType?.Contains(query.EventType, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        if (query.AfterSequence is { } after)
        {
            filtered = filtered.Where(e => e.Sequence > after);
        }

        if (query.From is { } from)
        {
            filtered = filtered.Where(e => e.ReceivedAt >= from);
        }

        if (query.To is { } to)
        {
            filtered = filtered.Where(e => e.ReceivedAt <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            filtered = filtered.Where(e =>
                e.Payload?.Contains(query.Keyword, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        var list = filtered
            .OrderByDescending(e => e.Sequence)
            .Take(limit)
            .ToList();

        if (!query.IncludePayload)
        {
            foreach (var item in list)
            {
                item.Payload = null;
            }
        }

        return Ok(ApiResponse<object>.Ok(new
        {
            total = list.Count,
            limit,
            latestSequence = all.Count > 0 ? all.Max(e => e.Sequence) : 0,
            events = list,
        }));
    }

    /// <summary>获取单条事件的完整报文（配合 <c>?includePayload=false</c> 的两段式拉取）。</summary>
    [HttpGet("events/{sequence:long}")]
    public IActionResult GetEvent(long sequence)
    {
        var found = _ams.Snapshot().FirstOrDefault(e => e.Sequence == sequence);
        if (found is null)
        {
            throw new IsupApiException(
                $"未找到序号为 {sequence} 的事件（可能已被缓冲区淘汰）。",
                StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<IsupAlarmEvent>.Ok(found));
    }

    /// <summary>清空内存中的事件缓冲。</summary>
    [HttpDelete("events")]
    public IActionResult ClearEvents()
    {
        var count = _ams.Snapshot().Count;
        _ams.ClearEvents();
        _logger.LogInformation("已清空报警事件缓冲，共 {Count} 条。", count);
        return Ok(ApiResponse<object>.Ok(new { Cleared = count }));
    }

    /// <summary>
    /// 为指定设备下发 HTTP 上报配置（<c>PUT /ISAPI/Event/notification/httpHosts</c>），
    /// 让设备把门禁事件以 <c>multipart/form-data</c> POST 到平台的上报地址。
    /// </summary>
    /// <remarks>
    /// 目标 IP 解析顺序：查询参数 <c>address</c> → <c>ISUP:ACSHttpPush:AdvertiseAddress</c> →
    /// 报警服务器地址。会覆盖设备侧第 1 条上报配置。
    /// </remarks>
    [HttpPost("devices/{deviceId}/enable-http-push")]
    public async Task<IActionResult> EnableHttpPush(
        string deviceId,
        [FromQuery] string? address = null,
        [FromQuery] ushort? port = null,
        [FromQuery] string? url = null,
        CancellationToken ct = default)
    {
        var device = _cms.ResolveDevice(deviceId, null);

        var targetAddress = address
                            ?? _acsHttpPush.AdvertiseAddress
                            ?? (_ams.ListenAddress is { Length: > 0 } listen ? listen : null)
                            ?? throw new IsupApiException(
                                "未能确定 HTTP 上报目标 IP：请传入 address，或配置 ISUP:ACSHttpPush:AdvertiseAddress。");

        var targetPort = port ?? (_acsHttpPush.ListenPort != 0 ? _acsHttpPush.ListenPort : (ushort)8083);
        var targetUrl = string.IsNullOrWhiteSpace(url) ? _acsHttpPush.UrlPath ?? "/" : url!;

        var result = await _accessControl
            .SetHttpHostNotificationAsync(device, targetUrl, targetAddress, targetPort, ct: ct)
            .ConfigureAwait(false);

        if (!result.DeviceSuccess)
        {
            throw new IsupApiException(
                $"下发 HTTP 上报配置失败：SDK={result.SdkSuccess}（{result.SdkErrorText}），设备状态={result.Status?.SubStatusCode}。",
                StatusCodes.Status502BadGateway);
        }

        _logger.LogInformation(
            "已向设备 {DeviceId} 下发 HTTP 上报配置：{Address}:{Port}{Url}",
            device.DeviceId,
            targetAddress,
            targetPort,
            targetUrl);

        return Ok(ApiResponse<IsapiOperationResult>.Ok(result, "已下发 HTTP 上报配置"));
    }
}
