using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Services.ConfigSeed;
using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Controllers;

/// <summary>
/// 配置种子数据：把本项目控制器暴露的 API 清单导出成 <c>dbo.RemoteServiceConfig</c> /
/// <c>dbo.ApiUrlConfig</c> 两张表的 SQL Server 添加数据脚本（INSERT）。
///
/// <para>
/// 脚本只依赖控制器元数据（路由 + HTTP 方法 + XML 注释摘要），不读取 <c>appsettings.json</c>
/// 里的业务配置节，因此换环境后只需把 <c>baseUrl</c> / <c>environment</c> 换掉重新生成即可。
/// </para>
/// </summary>
[ApiController]
[Route("api/config-seed")]
[Produces("application/json")]
[Tags("11. 配置种子数据（RemoteServiceConfig / ApiUrlConfig）")]
public sealed class ConfigSeedController : ControllerBase
{
    private readonly ConfigSeedSqlGenerator _generator;

    public ConfigSeedController(ConfigSeedSqlGenerator generator) => _generator = generator;

    /// <summary>
    /// 生成 <c>RemoteServiceConfig</c> / <c>ApiUrlConfig</c> 的 SQL Server INSERT 脚本（text/plain）。
    ///
    /// <para>
    /// <c>RemoteServiceConfig</c> 按控制器路由分组（<c>DEVICES</c>、<c>ACCESS-CONTROL.CARDS</c> …），
    /// <c>ApiUrlConfig</c> 每个控制器动作一条并通过 <c>ServiceConfigId</c> 外键挂到对应服务上；
    /// 脚本按两张表的唯一索引判重，可重复执行。
    /// </para>
    /// </summary>
    /// <param name="baseUrl">
    /// 本服务对外基址，用于拆出 <c>RemoteServiceConfig</c> 的 <c>Host/Port/Protocol/IsSsl</c>，
    /// 同时拼进 <c>ApiUrlConfig.FullUrl</c>；留空用当前请求的 <c>scheme://host</c>。
    /// </param>
    /// <param name="serviceCode">
    /// 只导出指定服务分组，可重复传入（<c>?serviceCode=DEVICES&amp;serviceCode=EVENTS</c>）；留空导出全部。
    /// </param>
    /// <param name="environment">
    /// 目标环境，只能是 <c>DEV</c> / <c>TEST</c> / <c>UAT</c> / <c>PROD</c>（对应约束 <c>CK_RSC_Environment</c>）；
    /// 留空按宿主环境名（<c>ASPNETCORE_ENVIRONMENT</c>）自动归一化。
    /// </param>
    /// <returns>可直接在 SSMS 中执行的 SQL 脚本。</returns>
    [HttpGet("sql")]
    [Produces("text/plain")]
    public IActionResult GetSqlServerInsertScript(
        [FromQuery] string? baseUrl = null,
        [FromQuery] string[]? serviceCode = null,
        [FromQuery] string? environment = null)
    {
        if (!string.IsNullOrWhiteSpace(environment) && !ConfigSeedSqlGenerator.IsAllowedEnvironment(environment))
        {
            throw new IsupApiException(
                $"environment 只能是 DEV / TEST / UAT / PROD（收到 '{environment}'）。",
                StatusCodes.Status400BadRequest);
        }

        var origin = ResolveBaseUrl(baseUrl);
        var snapshot = _generator.Build(origin, serviceCode, environment);
        var script = _generator.BuildSqlServerScript(
            snapshot,
            $"GET {Request.Path}{Request.QueryString}",
            DateTimeOffset.Now);

        return Content(script, "text/plain; charset=utf-8");
    }

    /// <summary>本服务基址：未显式指定时用当前请求的 scheme://host。</summary>
    private string ResolveBaseUrl(string? baseUrl)
        => string.IsNullOrWhiteSpace(baseUrl)
            ? $"{Request.Scheme}://{Request.Host}"
            : baseUrl.Trim().TrimEnd('/');
}
