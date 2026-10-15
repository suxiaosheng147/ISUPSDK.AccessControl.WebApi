using ISUPSDK.AccessControl.WebApi.Modules;

// ---------------------------------------------------------------------------------------------
// 海康 ISUP 门禁接入 Web API
//
// 原控制台项目 ISUPSDK.NETCore 按其原有逻辑整体搬迁到本项目的 IsupSdkRuntime（设备接入层），
// 这里只做编排：按模块注册服务 → 装配管道 → 映射端点。
// ---------------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// 设备接入模块：ISUP SDK 配置、运行时、后台初始化（CMS 监听 / 报警 / 图片存储 / 定时任务）。
builder.AddIsupSdk();

// 门禁业务模块：设备访问层（PTXML 透传）、ISAPI 业务服务、异步下发任务队列。
builder.AddIsupAccessControl();

// Web 宿主模块：CORS、MVC/JSON、Swagger、统一异常处理。
builder.AddIsupApi();

var app = builder.Build();

// 接入统一日志门面：此后 Common.WriteFile* 与 SDK 回调日志都进入 ILogger。
app.UseIsupSdk();

// 任务库建表（SQLite 轻量场景）。
await app.EnsureIsupAccessControlStorageAsync();

app.UseIsupApi();
app.MapIsupEndpoints();

app.Run();
