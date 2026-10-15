using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// SDK 运行状态与基础信息。
/// </summary>
[ApiController]
[Route("api/system")]
[Tags("2. 系统")]
public sealed class SystemController : ControllerBase
{
    private readonly IsupSdkRuntime _runtime;
    private readonly IsupSdkOptions _options;

    public SystemController(IsupSdkRuntime runtime, IOptions<IsupSdkOptions> options)
    {
        _runtime = runtime;
        _options = options.Value;
    }

    /// <summary>
    /// ISUP SDK 当前状态：初始化情况、CMS 监听句柄、在线设备数、报警计数等。
    /// </summary>
    [HttpGet("sdk")]
    public ActionResult<ApiResponse<IsupStatus>> GetSdkStatus()
        => Ok(ApiResponse<IsupStatus>.Ok(IsupStatusFactory.Create(_runtime, _options)));
}
