using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Options;
using Microsoft.EntityFrameworkCore;

namespace ISUPSDK.AccessControl.WebApi.Services.Tasks;

/// <summary>
/// 异步下发任务库的依赖注入注册（EF Core，可切换 SQLite / MySQL / SQL Server）。
///
/// 配置节：<c>ISUP:TaskQueue</c>（<c>Provider</c> / <c>ConnectionString</c> / <c>DatabasePath</c>）。
/// 任务库与事件库（<c>ISUP:EventStore</c>，见 <see cref="Events.AcsEventPipelineRegistration"/>）
/// 是两套独立的数据库配置，可以各自选择数据库类型。
/// </summary>
public static class AcsTaskStoreRegistration
{
    /// <summary>注册任务库上下文工厂（数据库类型由 <c>ISUP:TaskQueue:Provider</c> 决定）。</summary>
    public static IServiceCollection AddAcsTaskStore(this IServiceCollection services, IConfiguration configuration)
    {
        var taskOptions = configuration.GetSection(AcsTaskQueueOptions.SectionName).Get<AcsTaskQueueOptions>()
                          ?? new AcsTaskQueueOptions();

        services.AddDbContextFactory<AcsTaskDbContext>(options => Configure(options, taskOptions));

        return services;
    }

    /// <summary>
    /// 按 <see cref="AcsTaskQueueOptions.ResolveProvider"/> 选择数据库
    /// （与事件库 <c>AcsEventPipelineRegistration.ConfigureStore</c> 同一套写法）。
    /// </summary>
    public static void Configure(DbContextOptionsBuilder options, AcsTaskQueueOptions taskOptions)
    {
        switch (taskOptions.ResolveProvider())
        {
            case AcsTaskStoreProvider.MySql:
            {
                var connectionString = RequireConnectionString(taskOptions, "MySql");
                var serverVersion = MySqlServerVersion.Parse(
                    string.IsNullOrWhiteSpace(taskOptions.ServerVersion) ? "8.0.36" : taskOptions.ServerVersion.Trim());

                options.UseMySql(connectionString, serverVersion, o =>
                {
                    o.CommandTimeout(Math.Max(taskOptions.CommandTimeoutSeconds, 1));
                    o.EnableRetryOnFailure(3);
                });

                break;
            }

            case AcsTaskStoreProvider.SqlServer:
            {
                var connectionString = RequireConnectionString(taskOptions, "SqlServer");

                options.UseSqlServer(connectionString, o =>
                {
                    o.CommandTimeout(Math.Max(taskOptions.CommandTimeoutSeconds, 1));
                    o.EnableRetryOnFailure(3);
                });

                break;
            }

            default:
            {
                // SQLite：库文件路径相对程序目录（AppContext.BaseDirectory）解析，并确保目录存在。
                var path = string.IsNullOrWhiteSpace(taskOptions.DatabasePath)
                    ? "./storage/acs-tasks.db"
                    : taskOptions.DatabasePath;

                var fullPath = Path.GetFullPath(path, AppContext.BaseDirectory);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                options.UseSqlite($"Data Source={fullPath}", o =>
                {
                    o.CommandTimeout(Math.Max(taskOptions.CommandTimeoutSeconds, 1));
                });

                break;
            }
        }
    }

    private static string RequireConnectionString(AcsTaskQueueOptions taskOptions, string provider)
    {
        if (string.IsNullOrWhiteSpace(taskOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                $"ISUP:TaskQueue:Provider={provider} 时必须配置 TaskQueue:ConnectionString。");
        }

        return taskOptions.ConnectionString;
    }
}
