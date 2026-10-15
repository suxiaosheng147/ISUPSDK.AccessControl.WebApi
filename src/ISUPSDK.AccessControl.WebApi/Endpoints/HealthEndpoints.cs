using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ISUPSDK.AccessControl.WebApi.Endpoints;

/// <summary>
/// 健康检查端点：进程存活与 SDK 就绪状态。
/// </summary>
public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", (IsupSdkRuntime runtime, IOptions<IsupSdkOptions> options) =>
        {
            var status = IsupStatusFactory.Create(runtime, options.Value);

            return Results.Ok(new
            {
                // SDK 未初始化即视为降级：接口进程仍在，但设备接入不可用。
                status = runtime.IsInitialized ? "healthy" : "degraded",
                onlineDevices = status.OnlineDeviceCount,
                devices = runtime.DeviceIds.ToArray(),
                sdk = status,
                utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            });
        })
        .WithTags("9. 健康检查");

        return endpoints;
    }
}
