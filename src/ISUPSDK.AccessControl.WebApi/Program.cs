using ISUPSDK.AccessControl.WebApi.Modules;

// ---------------------------------------------------------------------------------------------
// 海康 ISUP 门禁接入 Web API
//
// 原控制台项目 ISUPSDK.NETCore 按其原有逻辑整体搬迁到本项目的 IsupSdkRuntime（设备接入层），
// 这里只做编排：按模块注册服务 → 装配管道 → 映射端点。
// ---------------------------------------------------------------------------------------------
var builder = WebApplication.CreateBuilder(args);

// 日志模块：整个进程只走 NLog（nlog.config：控制台 + logs\log\<类名>\<yyyy-MM-dd>\<HH>.log）。
// 内部会 ClearProviders() 掉内置 Console/Debug provider，所以必须在其他 Add* 之前调用。
builder.AddIsupNLog();

// 设备接入模块：ISUP SDK 配置、运行时、后台初始化（CMS 监听 / 报警 / 图片存储 / 定时任务）。
builder.AddIsupSdk();

// 门禁业务模块：设备访问层（ISAPI 通道：http-sdk / http-IsapiDigest / http-http / http-xml）、
// ISAPI 业务服务、异步下发任务队列。
builder.AddIsupAccessControl();

// Web 宿主模块：CORS、MVC/JSON、Swagger、统一异常处理。
builder.AddIsupApi();

var app = builder.Build();

// NLog 接入请求管道（请求上下文 / 停机 flush）。
app.UseIsupNLog();

// 接入统一日志门面：此后 Common.WriteFile* 与 SDK 回调日志都进入 ILogger（出口即 NLog）。
app.UseIsupSdk();

// 任务库 / 事件库建表（轻量场景直接用 EnsureCreated）。
await app.EnsureIsupAccessControlStorageAsync();

// 宿主启动完成后初始化人员下发队列（isup_person_queue.db）：之后 Customize 的人员同步走"入库 → 控制器入队"。
app.InitPersonOperationQueue();

app.UseIsupApi();
app.MapIsupEndpoints();

app.Run();
