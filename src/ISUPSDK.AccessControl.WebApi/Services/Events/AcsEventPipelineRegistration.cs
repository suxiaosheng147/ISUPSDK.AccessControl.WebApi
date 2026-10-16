using ISUPSDK.AccessControl.WebApi.Data.EventStore;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Queue;
using ISUPSDK.AccessControl.WebApi.Services.Queue.Msmq;
using ISUPSDK.AccessControl.WebApi.Services.Queue.RabbitMq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 门禁事件"接收 → 消息队列 → 消费入库"链路的依赖注入注册。
///
/// 配置节：
/// <list type="bullet">
/// <item><c>ISUP:EventQueue</c>：队列后端（None / Msmq / RabbitMq）与生产者/消费者参数；</item>
/// <item><c>ISUP:EventStore</c>：落库数据库（Sqlite / MySql / SqlServer）与表/保留策略。</item>
/// </list>
/// </summary>
public static class AcsEventPipelineRegistration
{
    /// <summary>注册事件队列管道（队列 + 生产者 + 消费者 + 落库 + 重放）。</summary>
    public static IServiceCollection AddAcsEventPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AcsEventQueueOptions>(configuration.GetSection(AcsEventQueueOptions.SectionName));
        services.Configure<AcsEventStoreOptions>(configuration.GetSection(AcsEventStoreOptions.SectionName));

        var storeOptions = configuration.GetSection(AcsEventStoreOptions.SectionName).Get<AcsEventStoreOptions>()
                           ?? new AcsEventStoreOptions();

        services.AddDbContextFactory<AcsEventDbContext>(options => ConfigureStore(options, storeOptions));

        // 队列后端按配置选择（三者行为统一，见 IEventQueue 的说明）。
        services.AddSingleton<IEventQueue>(CreateQueue);

        services.AddSingleton<AcsEventStore>();
        services.AddSingleton<AcsEventSpoolReplayer>();
        services.AddSingleton<AcsEventPublisher>();

        // 报警转发：消费者取出队列消息后按 MinorType 判定，并投递到下游队列
        // （Settings:MessageQueueAddress）——生产端（SDK 报警回调）不做判定、不直接发送。
        services.AddSingleton<AcsAlarmForwardService>();

        // 手工补录（POST /api/events/manual）：走 spool + 队列，或直接入库。
        services.AddSingleton<AcsManualEventService>();

        // 生产者：投递到队列；消费者：批量写库。
        services.AddSingleton<AcsEventPublishWorker>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AcsEventPublishWorker>());

        services.AddSingleton<AcsEventQueueConsumerService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<AcsEventQueueConsumerService>());

        // 保留策略清理（EventStore:RetentionDays > 0 时生效）。
        services.AddSingleton<EventStoreRetentionService>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<EventStoreRetentionService>());

        return services;
    }

    private static IEventQueue CreateQueue(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptions<AcsEventQueueOptions>>().Value;
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("AcsEventQueue");

        if (!Enum.TryParse<AcsEventQueueProvider>(options.Provider, ignoreCase: true, out var provider))
        {
            logger.LogWarning(
                "ISUP:EventQueue:Provider=\"{Provider}\" 无法识别（可选：None / Msmq / RabbitMq），已回退为进程内直通。",
                options.Provider);

            provider = AcsEventQueueProvider.None;
        }

        return provider switch
        {
            AcsEventQueueProvider.Msmq => ActivatorUtilities.CreateInstance<MsmqEventQueue>(sp),
            AcsEventQueueProvider.RabbitMq => ActivatorUtilities.CreateInstance<RabbitMqEventQueue>(sp),
            _ => ActivatorUtilities.CreateInstance<InProcessEventQueue>(sp),
        };
    }

    private static void ConfigureStore(DbContextOptionsBuilder options, AcsEventStoreOptions storeOptions)
    {
        switch (storeOptions.ResolveProvider())
        {
            case AcsEventStoreProvider.MySql:
            {
                var connectionString = RequireConnectionString(storeOptions, "MySql");
                var serverVersion = MySqlServerVersion.Parse(
                    string.IsNullOrWhiteSpace(storeOptions.ServerVersion) ? "8.0.36" : storeOptions.ServerVersion.Trim());

                options.UseMySql(connectionString, serverVersion, o =>
                {
                    o.CommandTimeout(Math.Max(storeOptions.CommandTimeoutSeconds, 1));
                    o.EnableRetryOnFailure(3);
                });

                break;
            }

            case AcsEventStoreProvider.SqlServer:
            {
                var connectionString = RequireConnectionString(storeOptions, "SqlServer");

                options.UseSqlServer(connectionString, o =>
                {
                    o.CommandTimeout(Math.Max(storeOptions.CommandTimeoutSeconds, 1));
                    o.EnableRetryOnFailure(3);
                });

                break;
            }

            default:
            {
                var path = string.IsNullOrWhiteSpace(storeOptions.ConnectionString)
                    ? storeOptions.SqlitePath
                    : storeOptions.ConnectionString;

                var fullPath = Path.GetFullPath(path);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var connectionString = fullPath.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
                    ? fullPath
                    : $"Data Source={fullPath}";

                options.UseSqlite(connectionString, o =>
                {
                    o.CommandTimeout(Math.Max(storeOptions.CommandTimeoutSeconds, 1));
                });

                break;
            }
        }
    }

    private static string RequireConnectionString(AcsEventStoreOptions storeOptions, string provider)
    {
        if (string.IsNullOrWhiteSpace(storeOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                $"ISUP:EventStore:Provider={provider} 时必须配置 EventStore:ConnectionString。");
        }

        return storeOptions.ConnectionString;
    }
}
