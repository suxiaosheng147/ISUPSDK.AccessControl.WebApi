using ISUPSDK.AccessControl.WebApi.Endpoints;
using ISUPSDK.AccessControl.WebApi.Middleware;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ISUPSDK.AccessControl.WebApi.Modules;

/// <summary>
/// Web API 宿主模块：CORS、MVC/JSON、Swagger、统一异常中间件与内置端点。
/// </summary>
public static class IsupApiModule
{
    /// <summary>注册 Web API 宿主所需的服务（CORS / MVC / JSON / Swagger）。</summary>
    public static WebApplicationBuilder AddIsupApi(this WebApplicationBuilder builder)
    {
        AddCors(builder);

        builder.Services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
            });

        // 最小 API（/health 等）不走 AddControllers 的 JSON 配置，这里单独配一份保持一致。
        builder.Services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            options.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        });

        AddSwagger(builder);

        return builder;
    }

    /// <summary>装配请求管道（异常处理 → CORS → Swagger）。</summary>
    public static WebApplication UseIsupApi(this WebApplication app)
    {
        app.UseMiddleware<ApiExceptionMiddleware>();

        var corsOptions = app.Services.GetRequiredService<IOptions<IsupCorsOptions>>().Value;
        if (corsOptions.Enabled)
        {
            app.UseCors(IsupCorsOptions.PolicyName);
        }

        // 内置静态页面（wwwroot）：事件查询页面 http://<host>:<port>/events.html
        app.UseStaticFiles();

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "ISUP 门禁接入 API v1");
            options.DocumentTitle = "海康 ISUP 门禁接入 Web API";
            options.RoutePrefix = "swagger";
        });

        return app;
    }

    /// <summary>映射控制器与内置端点（根路径跳转、健康检查）。</summary>
    public static WebApplication MapIsupEndpoints(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/", () => Results.Redirect("/swagger"))
           .ExcludeFromDescription();

        app.MapHealthEndpoints();

        return app;
    }

    private static void AddCors(WebApplicationBuilder builder)
    {
        builder.Services.Configure<IsupCorsOptions>(builder.Configuration.GetSection(IsupCorsOptions.SectionName));

        var corsOptions = builder.Configuration.GetSection(IsupCorsOptions.SectionName).Get<IsupCorsOptions>()
                          ?? new IsupCorsOptions();

        if (!corsOptions.Enabled)
        {
            return;
        }

        var allowCredentials = corsOptions.ResolveAllowCredentials(out var corsWarning);
        if (corsWarning is not null)
        {
            Console.WriteLine($"[CORS] {corsWarning}");
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(IsupCorsOptions.PolicyName, policy =>
            {
                if (corsOptions.AllowsAnyOrigin)
                {
                    policy.AllowAnyOrigin();
                }
                else
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins.ToArray());
                }

                if (corsOptions.AllowsAnyMethod)
                {
                    policy.AllowAnyMethod();
                }
                else
                {
                    policy.WithMethods(corsOptions.AllowedMethods.ToArray());
                }

                if (corsOptions.AllowsAnyHeader)
                {
                    policy.AllowAnyHeader();
                }
                else
                {
                    policy.WithHeaders(corsOptions.AllowedHeaders.ToArray());
                }

                if (corsOptions.ExposedHeaders.Count > 0)
                {
                    policy.WithExposedHeaders(corsOptions.ExposedHeaders.ToArray());
                }

                if (allowCredentials)
                {
                    policy.AllowCredentials();
                }

                if (corsOptions.MaxAgeSeconds > 0)
                {
                    policy.SetPreflightMaxAge(TimeSpan.FromSeconds(corsOptions.MaxAgeSeconds));
                }
            });
        });
    }

    private static void AddSwagger(WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "海康 ISUP 门禁接入 Web API",
                Version = "v1.0.0",
                Description =
                    "基于原控制台项目 ISUPSDK.NETCore（ISUP5.0 / EHome SDK：HCISUPCMS / HCISUPAlarm / HCISUPSS / HCISUPStream）" +
                    "封装的 .NET 8 Web API。\n\n" +
                    "服务启动后自动完成 SDK 初始化与设备注册监听（NET_ECMS_StartListen），" +
                    "接收设备注册（DEV_ON）、报警（ACS 刷卡/人脸事件）与图片上传（SS 存储回调），" +
                    "并按 Crontab 定时下发 NTP 校时；对外提供设备列表、SDK 状态、手动同步与校时接口。",
            });

            options.AddServer(new OpenApiServer { Url = "/", Description = "当前服务" });
            options.OrderActionsBy(description => description.GroupName);

            var xmlPath = Path.Combine(AppContext.BaseDirectory, "ISUPSDK.AccessControl.WebApi.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
        });
    }
}
