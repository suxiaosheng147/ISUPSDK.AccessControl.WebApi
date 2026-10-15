using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Data.EventStore;
using ISUPSDK.AccessControl.WebApi.Services.Acs;
using ISUPSDK.AccessControl.WebApi.Services.Events;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ISUPSDK.AccessControl.WebApi.Modules;

/// <summary>
/// 门禁业务模块：设备访问层（ISUP CMS + ISAPI 透传）、ISAPI 业务服务与异步下发任务队列。
/// <para>
/// 该模块建立在设备接入模块（<see cref="IsupSdkModule"/>）之上：
/// <c>IsupSdkRuntime</c> 负责把设备接进来，本模块负责把门禁业务接口化。
/// </para>
/// </summary>
public static class IsupAccessControlModule
{
    /// <summary>注册设备访问层、ISAPI 业务服务与异步下发任务队列。</summary>
    public static WebApplicationBuilder AddIsupAccessControl(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<IsupTimeOptions>(builder.Configuration.GetSection(IsupTimeOptions.SectionName));
        builder.Services.Configure<AcsTaskQueueOptions>(builder.Configuration.GetSection(AcsTaskQueueOptions.SectionName));

        // ---- 设备访问层：设备表 + PTXML 透传 ----
        builder.Services.AddSingleton<IsupCmsService>();
        // ---- ISAPI 业务服务：人员/卡片/人脸/计划模板/门禁控制的报文组装 ----
        builder.Services.AddSingleton<IsupIsapiService>();
        // ---- 报警事件服务：订阅 ISUP 报警回调并做内存缓冲 ----
        builder.Services.AddSingleton<IsupAmsService>();
        // ---- 图片存储服务：设备上传图片的落盘管理与查询 ----
        builder.Services.AddSingleton<IsupSsService>();

        // ---- HTTP 客户端：人脸下发时按 faceURL 下载图片（FacesController 使用命名客户端 face-image）----
        builder.Services.AddHttpClient("face-image", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ISUPSDK.AccessControl/1.0");
        });
        // 默认客户端，便于其它服务直接注入 IHttpClientFactory。
        builder.Services.AddHttpClient();

        // ---- 门禁事件管道：事件 → 队列（None / Msmq / RabbitMq）→ 消费者 → 事件库 ----
        // 配置节：ISUP:EventQueue（队列后端与生产者/消费者）、ISUP:EventStore（落库与保留策略）。
        builder.Services.AddAcsEventPipeline(builder.Configuration);
        builder.Services.AddSingleton<AcsEventSpool>();
        builder.Services.AddSingleton<IsupEventIngestService>();
        // 注册为 HostedService：确保 DI 真正实例化它（否则订阅不会建立，事件进不了队列）。
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<IsupEventIngestService>());

        // ---- 历史事件对账补漏：定时从设备拉取，补齐回调丢失的缺口 ----
        builder.Services.Configure<AcsEventReconcileOptions>(builder.Configuration.GetSection(AcsEventReconcileOptions.SectionName));
        builder.Services.AddSingleton<AcsEventReconcileService>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AcsEventReconcileService>());

        // ---- 异步下发任务库（SQLite + EF Core）----
        builder.Services.AddDbContextFactory<AcsTaskDbContext>(options =>
        {
            var path = builder.Configuration["ISUP:TaskQueue:DatabasePath"];
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "./storage/acs-tasks.db";
            }

            var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            options.UseSqlite($"Data Source={fullPath}");
        });

        builder.Services.AddSingleton<AcsTaskService>();
        builder.Services.AddSingleton<AcsTaskExecutor>();
        builder.Services.AddSingleton<AcsTaskWorker>();
        builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AcsTaskWorker>());

        return builder;
    }

    /// <summary>确保任务库与事件库的表已创建（轻量场景直接用 EnsureCreated）。</summary>
    public static async Task<WebApplication> EnsureIsupAccessControlStorageAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();

        var taskFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AcsTaskDbContext>>();
        using (var taskDb = taskFactory.CreateDbContext())
        {
            taskDb.Database.EnsureCreated();

            // EnsureCreated 只建库、不补列：老库升级后需要补一次（SQLite 支持 ADD COLUMN），
            // 否则查询 AcsTask 会因缺少 PayloadFormat 列而报错。
            try
            {
                taskDb.Database.ExecuteSqlRaw(
                    "ALTER TABLE AcsTasks ADD COLUMN PayloadFormat TEXT NOT NULL DEFAULT 'json'");
            }
            catch (Exception)
            {
                // 列已存在（含刚建好的新库）—— 忽略。
            }
        }

        var eventStore = scope.ServiceProvider.GetRequiredService<AcsEventStore>();
        await eventStore.EnsureCreatedAsync().ConfigureAwait(false);

        return app;
    }
}
