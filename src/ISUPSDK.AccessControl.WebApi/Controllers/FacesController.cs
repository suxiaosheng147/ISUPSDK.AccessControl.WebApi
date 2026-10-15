using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 人脸下发控制器。
/// 对应 ISAPI：
/// <list type="bullet">
/// <item>人脸图片下发：<c>POST /ISAPI/Intelligent/FDLib/FaceDataRecord?format=json</c>（multipart/form-data）；</item>
/// <item>人脸图片删除：<c>PUT /ISAPI/Intelligent/FDLib/FaceDataDelete?format=json</c>；</item>
/// <item>人脸检索：<c>POST /ISAPI/Intelligent/FDLib/FDSearch?format=json</c>；</item>
/// <item>人脸库维护：<c>/ISAPI/Intelligent/FDLib</c>。</item>
/// </list>
///
/// <b>下发/修改/删除为异步任务</b>：图片先落本地磁盘、请求入库，返回 <c>202</c> 与任务 ID。
/// 查询与人脸库读取接口仍为同步。
/// </summary>
[Route("api/access-control/faces")]
[Tags("3. 人脸下发")]
public sealed class FacesController : AccessControlControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public FacesController(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        AcsTaskService tasks,
        IHttpClientFactory httpClientFactory,
        ILogger<FacesController> logger)
        : base(cms, accessControl, tasks, logger)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>下发人脸图片（multipart/form-data 文件上传，异步任务）。</summary>
    /// <remarks>
    /// 表单字段：
    /// <list type="bullet">
    /// <item><c>image</c>：人脸图片文件（必填，JPEG，建议 ≥ 480×480、≤ 200KB）；</item>
    /// <item><c>fpId</c>：人脸图片 ID，通常等于人员工号（必填）；</item>
    /// <item><c>fdId</c>：人脸库 ID，默认 1；</item>
    /// <item><c>name</c>：人员姓名（可选）；</item>
    /// <item><c>employeeNo</c>：人员工号（可选）；</item>
    /// <item><c>overWrite</c>：是否覆盖已有图片，默认 true。</item>
    /// </list>
    /// 图片会先保存到本服务磁盘（<c>ISUP:TaskQueue:BinaryDirectory</c>），
    /// 任务执行时再下发。
    /// </remarks>
    [HttpPost]
    [RequestSizeLimit(50 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadAsync(
        [FromForm] FaceUploadForm form,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        if (form.Image is null || form.Image.Length == 0)
        {
            throw new IsupApiException("请通过表单字段 image 上传人脸图片文件。");
        }

        if (string.IsNullOrWhiteSpace(form.FpId))
        {
            throw new IsupApiException("参数 fpId（人脸图片 ID）不能为空。");
        }

        var target = ResolveTaskDeviceId(deviceId, userId);

        using var memory = new MemoryStream();
        await form.Image.CopyToAsync(memory, ct);
        var binaryPath = await Tasks.SaveBinaryAsync(memory.ToArray(), ".jpg", ct);

        var request = new FaceDownloadRequest
        {
            FpId = form.FpId,
            FdId = form.FdId,
            FaceLibType = form.FaceLibType,
            Name = form.Name,
            EmployeeNo = form.EmployeeNo,
            OverWrite = form.OverWrite,
            ContentType = string.IsNullOrWhiteSpace(form.Image.ContentType) ? "image/jpeg" : form.Image.ContentType,
            FileName = string.IsNullOrWhiteSpace(form.Image.FileName) ? $"{form.FpId}.jpg" : form.Image.FileName,
            ImagePartName = form.ImagePartName,
            JsonPartName = form.JsonPartName,
            TimeoutMs = form.TimeoutMs,
        };

        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Face,
            AcsTaskOperation.FaceUpload,
            request,
            form.TimeoutMs,
            binaryPath,
            $"下发人脸 {form.FpId}",
            ct);

        return AcceptedTask(task);
    }

    /// <summary>通过 Base64 或 URL 下发人脸图片（异步任务）。</summary>
    [HttpPost("by-image")]
    public async Task<IActionResult> UploadByImageAsync(
        [FromBody] FaceUploadByImageRequest request,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.FpId))
        {
            throw new IsupApiException("参数 fpId（人脸图片 ID）不能为空。");
        }

        var target = ResolveTaskDeviceId(deviceId, userId);
        var bytes = await ResolveImageBytesAsync(request, ct);
        var binaryPath = await Tasks.SaveBinaryAsync(bytes, ".jpg", ct);

        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Face,
            AcsTaskOperation.FaceUploadByImage,
            request.ToDownloadRequest(),
            request.TimeoutMs,
            binaryPath,
            $"下发人脸 {request.FpId}",
            ct);

        return AcceptedTask(task);
    }

    /// <summary>批量下发人脸图片（异步任务，每项通过 Base64 或 URL 提供图片）。</summary>
    [HttpPost("batch")]
    public async Task<IActionResult> UploadBatchAsync(
        [FromBody] List<FaceUploadByImageRequest> requests,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        if (requests is null || requests.Count == 0)
        {
            throw new IsupApiException("请求列表为空。");
        }

        var target = ResolveTaskDeviceId(deviceId, userId);
        var items = new List<FaceUploadFaceItem>();
        var paths = new List<string>();
        var timeoutMs = 15000u;

        foreach (var request in requests)
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(request.FpId))
            {
                throw new IsupApiException("每一项都必须提供 fpId（人脸图片 ID）。");
            }

            var bytes = await ResolveImageBytesAsync(request, ct);
            paths.Add(await Tasks.SaveBinaryAsync(bytes, ".jpg", ct));
            timeoutMs = request.TimeoutMs;

            items.Add(new FaceUploadFaceItem
            {
                FpId = request.FpId,
                FdId = request.FdId,
                FaceLibType = request.FaceLibType,
                Name = request.Name,
                EmployeeNo = request.EmployeeNo,
                OverWrite = request.OverWrite ?? true,
                TimeoutMs = request.TimeoutMs,
            });
        }

        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Face,
            AcsTaskOperation.FaceBatchUpload,
            items,
            timeoutMs,
            JsonSerializer.Serialize(paths, IsupJson.Options),
            $"批量下发人脸 {items.Count} 条",
            ct);

        return AcceptedTask(task);
    }

    /// <summary>删除人脸图片（异步任务）。</summary>
    [HttpDelete("{fpId}")]
    public async Task<IActionResult> DeleteAsync(
        string fpId,
        [FromQuery] string fdId = "1",
        [FromQuery] string faceLibType = "blackFD",
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Face,
            AcsTaskOperation.FaceDelete,
            new FaceDeletePayload { FpId = fpId, FdId = fdId, FaceLibType = faceLibType },
            timeoutMs ?? 15000,
            description: $"删除人脸 {fpId}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>查询人脸图片信息。</summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchAsync(
        [FromBody] FaceSearchRequest? request = null,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        CancellationToken ct = default)
    {
        request ??= new FaceSearchRequest();
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.SearchFacesAsync(device, request.ToCondition(), request.TimeoutMs, ct);
        return FromQuery(result);
    }

    // ==========================================================================================
    // 人脸库维护
    // ==========================================================================================

    /// <summary>创建人脸库（异步任务）。</summary>
    [HttpPost("libraries")]
    public async Task<IActionResult> CreateLibraryAsync(
        [FromBody] IsapiFdLib fdLib,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.FaceLibrary,
            AcsTaskOperation.FaceLibraryCreate,
            fdLib,
            timeoutMs ?? 15000,
            description: $"创建人脸库 {fdLib.FdId}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>修改人脸库（异步任务）。</summary>
    [HttpPut("libraries")]
    public async Task<IActionResult> UpdateLibraryAsync(
        [FromBody] IsapiFdLib fdLib,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.FaceLibrary,
            AcsTaskOperation.FaceLibraryUpdate,
            fdLib,
            timeoutMs ?? 15000,
            description: $"修改人脸库 {fdLib.FdId}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>删除人脸库（异步任务）。</summary>
    [HttpDelete("libraries/{fdId}")]
    public async Task<IActionResult> DeleteLibraryAsync(
        string fdId,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.FaceLibrary,
            AcsTaskOperation.FaceLibraryDelete,
            new FaceLibDeletePayload { FdId = fdId },
            timeoutMs ?? 15000,
            description: $"删除人脸库 {fdId}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>
    /// 通过 faceURL 下发人脸图片（异步任务）。设备会自行访问该 URL 拉取图片，
    /// 因此 URL 必须能被设备访问到。
    /// </summary>
    /// <remarks>
    /// 请求体：
    /// <code>
    /// {
    ///   "faceLibType": "blackFD",
    ///   "FDID": "1",
    ///   "FPID": "1001",
    ///   "faceURL": "http://192.168.1.100:8080/faces/1001.jpg",
    ///   "name": "张三"
    /// }
    /// </code>
    /// </remarks>
    [HttpPost("by-url")]
    public async Task<IActionResult> UploadByUrlAsync(
        [FromBody] IsapiFaceDataRecord metadata,
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metadata.FpId))
        {
            throw new IsupApiException("FPID（人脸图片 ID，通常等于人员工号）不能为空。");
        }

        if (string.IsNullOrWhiteSpace(metadata.FaceUrl))
        {
            throw new IsupApiException("faceURL 不能为空。");
        }

        var target = ResolveTaskDeviceId(deviceId, userId);
        var task = await Tasks.EnqueueAsync(
            target,
            AcsTaskEntityType.Face,
            AcsTaskOperation.FaceUploadByUrl,
            metadata,
            timeoutMs ?? 15000,
            description: $"下发人脸（URL）{metadata.FpId}",
            ct: ct);

        return AcceptedTask(task);
    }

    /// <summary>查询设备上的人脸库列表（GET /ISAPI/Intelligent/FDLib）。</summary>
    [HttpGet("libraries")]
    public async Task<IActionResult> GetLibrariesAsync(
        [FromQuery] string? deviceId = null,
        [FromQuery] int? userId = null,
        [FromQuery] uint? timeoutMs = null,
        CancellationToken ct = default)
    {
        var device = ResolveDevice(deviceId, userId);
        var result = await AccessControl.GetFaceLibrariesAsync(device, timeoutMs ?? 10000, ct);
        return FromQuery(result);
    }

    private async Task<byte[]> ResolveImageBytesAsync(FaceUploadByImageRequest request, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(request.ImageBase64))
        {
            var base64 = request.ImageBase64!;
            var commaIndex = base64.IndexOf(',');
            if (base64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIndex > 0)
            {
                base64 = base64[(commaIndex + 1)..];
            }

            try
            {
                return Convert.FromBase64String(base64.Trim());
            }
            catch (FormatException ex)
            {
                throw new IsupApiException($"imageBase64 解析失败：{ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
        {
            try
            {
                var client = _httpClientFactory.CreateClient("face-image");
                using var response = await client.GetAsync(request.ImageUrl, ct);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                throw new IsupApiException($"下载人脸图片失败：{ex.Message}", StatusCodes.Status502BadGateway);
            }
        }

        throw new IsupApiException("请提供 imageBase64 或 imageUrl 之一。");
    }
}
