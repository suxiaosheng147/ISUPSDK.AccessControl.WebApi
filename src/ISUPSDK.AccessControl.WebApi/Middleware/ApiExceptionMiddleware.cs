using ISUPSDK.AccessControl.WebApi.Models;

namespace ISUPSDK.AccessControl.WebApi.Middleware;

/// <summary>
/// 统一异常响应：把未处理异常转成与 <c>ApiResponse</c> 一致的 JSON。
/// <para>
/// 业务异常 <see cref="IsupApiException"/> 自带 HTTP 状态码与业务码，按其返回；
/// 其余异常一律 500，且不把堆栈返回给调用方。
/// </para>
/// </summary>
public sealed class ApiExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiExceptionMiddleware> _logger;

    public ApiExceptionMiddleware(RequestDelegate next, ILogger<ApiExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // 客户端主动断开，不算服务端异常。
            _logger.LogDebug("请求被取消：{Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var statusCode = ex is IsupApiException api ? api.StatusCode : StatusCodes.Status500InternalServerError;
            var code = ex is IsupApiException apiEx ? apiEx.Code : statusCode;

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(ex, "接口处理异常：{Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogWarning("接口请求被拒绝：{Method} {Path} -> {Message}", context.Request.Method, context.Request.Path, ex.Message);
            }

            context.Response.Clear();
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                code,
                message = ex.Message,
            });
        }
    }
}
