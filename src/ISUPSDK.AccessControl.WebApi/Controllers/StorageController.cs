using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 图片/文件存储控制器（SS 组件）。
/// <para>
/// 设备把抓拍图片推到平台后由 SDK 存储回调落盘在 <c>Settings:PicturePath</c>，
/// 本控制器提供存储状态与文件的查询/下载/删除。
/// </para>
/// </summary>
[ApiController]
[Route("api/storage")]
[Produces("application/json")]
[Tags("8. 图片/文件存储（SS）")]
public sealed class StorageController : ControllerBase
{
    private readonly IsupSsService _ss;
    private readonly ILogger<StorageController> _logger;

    public StorageController(IsupSsService ss, ILogger<StorageController> logger)
    {
        _ss = ss;
        _logger = logger;
    }

    /// <summary>SS 存储状态：监听句柄、图片根目录、文件数与占用空间。</summary>
    [HttpGet("status")]
    public IActionResult Status() => Ok(ApiResponse<SsStatus>.Ok(_ss.GetStatus()));

    /// <summary>列出已落盘的图片/文件（按写入时间倒序）。</summary>
    [HttpGet("files")]
    public IActionResult ListFiles([FromQuery] int limit = 100)
        => Ok(ApiResponse<object>.Ok(new
        {
            root = _ss.PictureRoot,
            total = _ss.ListFiles(limit).Count,
            files = _ss.ListFiles(limit),
        }));

    /// <summary>下载根目录内的图片/文件（<paramref name="path"/> 为相对图片根目录的路径）。</summary>
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string path)
    {
        var full = _ss.ResolveFile(path);
        if (full is null)
        {
            throw new IsupApiException($"文件不存在或路径非法：{path}", StatusCodes.Status404NotFound);
        }

        return PhysicalFile(full, "application/octet-stream", Path.GetFileName(full));
    }

    /// <summary>删除根目录内的图片/文件。</summary>
    [HttpDelete]
    public IActionResult Delete([FromQuery] string path)
    {
        if (!_ss.DeleteFile(path))
        {
            throw new IsupApiException($"文件不存在或路径非法：{path}", StatusCodes.Status404NotFound);
        }

        return Ok(ApiResponse<object>.Ok(new { path, Deleted = true }));
    }
}
