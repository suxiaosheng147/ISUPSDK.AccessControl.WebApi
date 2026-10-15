# 海康 ISUP 门禁接入 Web API（.NET 8）

把原控制台项目 **ISUPSDK.NETCore**（ISUP5.0 / EHome SDK：`HCISUPCMS` / `HCISUPAlarm` /
`HCISUPSS` / `HCISUPStream`）改造成 Web API 的项目骨架。

> **原项目不改动**：`ISUPSDK.NETCore/` 保持零改动，本项目是它的一份**独立源码副本**，
> 两个项目可独立演进。迁移对应关系见 [docs/CONSOLE_TO_WEBAPI.md](docs/CONSOLE_TO_WEBAPI.md)。

当前形态为**骨架优先**：Web 宿主、模块化装配、配置、日志、Swagger、健康检查、
启停脚本已就绪，并把原控制台的全部设备接入逻辑（CMS 注册监听、报警回调、图片存储、
定时任务）搬进 Web 进程运行；对外先暴露少量核心接口，其余按批次迁移。

## 1. 目录结构

```
ISUPSDK.AccessControl/
├── ISUPSDK.AccessControl.sln
├── run.ps1
├── docs/CONSOLE_TO_WEBAPI.md
└── src/ISUPSDK.AccessControl.WebApi/
    ├── Program.cs                     # 只做编排：注册模块 → 装配管道 → 映射端点
    ├── Modules/                       # 模块化装配
    │   ├── IsupSdkModule.cs           # 设备接入模块（配置 + 运行时 + 后台初始化）
    │   ├── IsupAccessControlModule.cs # 门禁业务模块（设备访问 + ISAPI 业务 + 任务/事件库）
    │   └── IsupApiModule.cs           # Web 宿主机模块（CORS / MVC / Swagger / 中间件）
    ├── Endpoints/HealthEndpoints.cs   # /health
    ├── Controllers/                   # 10 个门禁控制器（见"API 一览"）
    ├── Hosting/IsupSdkHostedService.cs# 把原 Main 的启动流程接到应用生命周期
    ├── Middleware/                    # 统一异常响应
    ├── Options/                       # SDK / CORS / 时间 / 任务队列 / 事件库 配置
    ├── Models/                        # API DTO、ISAPI 模型、事件与队列模型
    ├── Interop/NativeLibraryResolver.cs# ISUP 原生库定位
    ├── Services/                      # 设备访问层与业务服务
    │   ├── IsupSdkRuntime.cs          #   原 Program.cs 搬迁后的运行时
    │   ├── IsupCmsService.cs          #   设备表 + ISAPI 透传（PTXML）
    │   ├── IsupIsapiService.cs        #   人员/卡片/人脸/计划/门禁 的 ISAPI 报文组装
    │   ├── IsupAmsService.cs          #   报警事件缓冲（订阅 SDK 报警回调）
    │   ├── IsupSsService.cs           #   图片存储（设备上传落盘的管理）
    │   ├── Tasks/                     #   异步下发任务队列
    │   └── Events/                    #   门禁事件入库管道
    ├── data/                          # EF Core 实体与上下文（任务库 / 事件库）
    ├── SdkService/                    # ↓ 以下为原控制台项目源码副本
    ├── Structure/
    ├── Model/
    ├── Method/
    ├── Distribute/
    ├── Customize/
    ├── DerivedClass/
    ├── wwwroot/events.html            # 内置门禁事件查询页面（http://<host>:<port>/events.html）
    ├── appsettings.json
    ├── appsettings.Development.json
    ├── ISUPDemo.xml
    └── Properties/launchSettings.json
```

## 2. 运行环境与依赖

| 项 | 要求 |
|---|---|
| 操作系统 | Windows x64（ISUP SDK 原生库为 win32/win64 构件） |
| 运行时 | .NET 8（`net8.0-windows`） |
| 进程位数 | **必须 x64** |
| ISUP SDK 原生库 | `HCISUPCMS.dll`、`HCISUPAlarm.dll`、`HCISUPSS.dll`、`HCISUPStream.dll` 及依赖（`HCNetUtils.dll`、`libeay32.dll`、`ssleay32.dll` 等） |

### 2.1 原生库目录定位

`Interop/NativeLibraryResolver` 按以下顺序查找"同时含 `HCISUPCMS.dll` 与 `HCISUPAlarm.dll`"的目录：

1. 配置 `ISUP:NativeDirectory`
2. 环境变量 `ISUP_SDK_DIR` / `HCISUPSDK_DIR`
3. 程序输出目录（沿用原控制台项目"把 dll 拷进 bin"的做法）
4. `./sdk`、`./isupsdk`
5. 从输出目录逐级向上查找 `ISUPSDK*` / `HCISUPSDK*`（含其 `lib` 子目录）

找到后会把该目录加入原生库搜索路径（`AddDllDirectory`），使依赖库同样可被加载。
**未找到时服务仍会启动**，只是 `/health` 返回 `degraded`。

## 3. 快速开始

```powershell
cd ISUPSDK.AccessControl
.\run.ps1                       # 启动（产物不存在时自动构建）
.\run.ps1 -Status               # 查看状态（端口 / 健康 / SDK 状态）
.\run.ps1 -Stop                 # 停止
.\run.ps1 -Restart              # 重启
.\run.ps1 -Build                # 启动前强制构建
.\run.ps1 -SdkDir D:\isupsdk    # 手动指定 ISUP SDK 原生库目录
```

等价的手工命令：

```powershell
$env:ISUP_SDK_DIR = "D:\isupsdk"
dotnet run --project src/ISUPSDK.AccessControl.WebApi -c Release -p:Platform=x64
```

Visual Studio：打开 `ISUPSDK.AccessControl.sln`，把 `ISUPSDK.AccessControl.WebApi`
设为启动项目，平台选 **x64**（sln 中 Any CPU / x86 也已映射到 x64），F5 调试。

启动后：

| 入口 | 地址 |
|---|---|
| **门禁事件查询页面（内置）** | **http://localhost:5190/events.html** |
| Swagger UI | http://localhost:5190/swagger |
| 健康检查 | http://localhost:5190/health |
| SDK 状态 | http://localhost:5190/api/system/sdk |
| 设备列表 | http://localhost:5190/api/devices |

## 4. 配置

`appsettings.json` 分两节：

- **`ISUP`**：Web 宿主自身配置。
  - `SettingsSection`（默认 `Settings`）：业务配置所在节名
  - `NativeDirectory`：ISUP SDK 原生库目录
  - `AutoStartListen`：是否在启动时初始化 SDK 并监听（调试接口时可设 `false`）
  - `SdkLogDirectory`：SDK 各组件日志目录（`SetLogToFile`）
  - `LogDirectory`：业务日志根目录（`Common.WriteFile*`），默认 `./logs/`
  - `Cors`：跨域配置
- **`Settings`**：业务配置，**与原控制台项目同名节保持一致**（平台地址、密钥、Crontab、休眠参数等）。

`ISUPDemo.xml` 提供 SDK 服务参数（心跳、报警服务器、NTP、图片服务器、DAS 地址等），
随生成复制到输出目录。

## 5. API 一览

统一响应包装：`{ "success": true, "code": 0, "message": null, "data": { } }`。

状态码约定：下发成功 `200`；参数无效 / 无在线设备 `400`；设备拒绝 `400`（`message` 中带 `subStatusCode`）；
ISAPI 透传失败 `502`（`code` 为 SDK 错误码）；事件库未就绪 `503`。

**设备定位**（门禁类接口通用）：请求头 `X-Device-Id`，或查询参数 `deviceId` / `userId`；
只有一台在线设备时可省略。

| 控制器 | 路由前缀 | 主要能力 |
|---|---|---|
| `PersonsController` | `/api/access-control/persons` | 人员新增 / 修改 / 覆盖 / 批量 / 删除 / 查询 / 计数 / 能力集 |
| `CardsController` | `/api/access-control/cards` | 卡片新增 / 修改 / 批量 / 删除（按卡号、按工号、全部）/ 查询 |
| `FacesController` | `/api/access-control/faces` | 人脸下发（multipart / Base64 / faceURL）、删除、查询、人脸库管理 |
| `PlanTemplatesController` | `/api/access-control/plan-templates` | 权限计划模板 / 周计划 / 假日组 / 假日计划 写入与查询 + 能力集 |
| `AccessControlDoorsController` | `/api/access-control` | 门禁控制（开 / 关 / 常开 / 常闭）、门状态、工作状态、能力集、事件查询、输出触发、设备信息 |
| `DevicesController` | `/api/devices` | 设备列表 / 详情、SDK 状态、监听启停、强制下线、原始 ISAPI 透传 |
| `AlarmsController` | `/api/alarms` | 报警监听状态与启停、门禁事件查询（内存缓冲）、设备侧 HTTP 上报下发 |
| `StorageController` | `/api/storage` | SS 存储状态、已接收图片列表 / 下载 / 删除 |
| `EventsController` | `/api/events` | 事件库状态、已入库事件查询 / 详情 / 多维统计、手工补录、从设备补漏 |
| `TasksController` | `/api/tasks` | 异步下发任务查询 / 重试 / 取消 |
| `SystemController` | `/api/system/sdk` | ISUP SDK 运行状态（初始化、监听句柄、在线设备、报警计数） |

另有：`/health`（健康检查）、`/swagger`（接口文档）、`/`（跳转 Swagger）、
**`/events.html`（内置门禁事件查询页面）**。

内置页面 `wwwroot/events.html` 提供：事件管道状态看板、多条件查询（主/次类型、事件类型、来源、
认证方式、设备、时间段、关键字）、详情报文查看、手工补录、手动对账补漏与落盘重放，
数据全部来自 `GET /api/events*` 系列接口。

> **写操作为异步任务**：人员 / 卡片 / 人脸的新增、修改、删除返回 `202` 与任务 ID，
> 用 `GET /api/tasks/{id}` 查询执行结果；查询类接口为同步。

### 5.2 门禁事件管道（队列可切换）

链路与三个队列后端由 `ISUP:EventQueue` / `ISUP:EventStore` / `ISUP:EventReconcile` 配置：

```
SDK 报警回调 ──► IsupAmsService（内存缓冲）
                      ▲
   设备 HTTP 上报 / 历史对账 ──► AcsEventSpool（落盘 + 去重）
                      │
              AcsEventPublisher ──► AcsEventPublishWorker
                      │
        IEventQueue（None / Msmq / RabbitMq）
                      │
        AcsEventQueueConsumerService（批量 + ack / 重投 / 死信）
                      │
        AcsEventStore（SQLite / MySQL / SQL Server，DedupHash 唯一索引幂等）
```

| Provider | 说明 |
|---|---|
| `None` | 进程内直通（单机部署默认） |
| `Msmq` | Windows 消息队列，走 `mqrt.dll` P/Invoke；`CreateIfMissing=true` 时自动创建 `.\Private$\<QueueName>` |
| `RabbitMq` | AMQP 0-9-1；Broker 不可达时 `ready=false`，进程与其它接口不受影响 |

- 队列不可用时：历史查询照常，`GET /api/events/status` 的 `queue.ready=false` 与 `lastError` 给出原因；
  应急可用 `POST /api/events/manual?directToStore=true` 绕过队列直接入库。
- 定时对账（默认 5 分钟）从设备拉取历史事件补齐回调丢失的缺口；`POST /api/events/reconcile` 可手动触发。
- 保留策略：`ISUP:EventStore:RetentionDays > 0` 时按 `RetentionSweepHours` 周期清理过期事件。
### 5.1 ISAPI 透传底座

所有门禁业务最终都经 `IsupCmsService.SendIsapiAsync` 透传到设备，
底层是 ISUP CMS 的 `NET_ECMS_Get/Put/Post/DeletePTXMLConfig`（`NET_EHOME_PTXML_PARAM`），
与 OTAP 侧 `OTAP_CMS_ISAPIPassThrough` 等价。透传失败会尝试放大输出缓冲区（64KB → 256KB → 1MB）后重试。

调试时的排查顺序：HTTP 响应体 `message` → `data.status.subStatusCode` → `data.rawResponse`（设备原始报文）→ 服务端日志中的 SDK 错误码。
## 6. 日志

日志统一走 .NET 的 `ILogger`：

- **业务日志**（原控制台项目的 `Common.WriteFile` / `Common.WriteFileHours`，以及散落的 `Console.WriteLine`）
  经静态门面 [`Services/Logging/IsupLog.cs`](src/ISUPSDK.AccessControl.WebApi/Services/Logging/IsupLog.cs) 汇入 `ILogger`，分类名为 `Isup`。
  之所以用门面：原控制台大量逻辑位于**静态方法**与 **SDK 回调**中，无法逐处注入 `ILogger`；
  门面由宿主在启动时接入一次，**所有调用点均未改动**（`Common.WriteFile*` 的 44 处调用、105 处 `Console` 输出）。
- **宿主日志**（控制器 / HostedService / 中间件）直接用注入的 `ILogger<T>`。
- 输出目标与级别由标准 `Logging` 节控制（默认 Console）：

  ```jsonc
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"   // 轮询取数的 SQL 不刷屏
    }
  }
  ```

- **文件回退**：`ISUP:Logging:FileFallback=true`（默认）时，业务日志在进入 `ILogger` 的同时，
  仍按原习惯写入 `<ISUP:LogDirectory>/log/log_yyyyMMdd/`（现场排查习惯）：
  `Common.WriteFile("ScheduleTimer.txt", …)` → `ScheduleTimer.txt`；仅 `IsupLog.Info(…)` 的 → `Isup.log`。
  设为 `false` 则只走 `ILogger`，不再产生 `log` 目录。文件写入沿用系统默认编码（与改造前一致）。

SDK 初始化输出的实际形态：

```
info: Isup[0]
      [Isup] NET_ECMS_StartListen Succ
info: Isup[0]
      [Isup] NET_EALARM_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Success
```

> 需要接入文件轮转 / ELK / Seq 时，只需注册相应的 `ILoggerProvider`（如 Serilog），
> 业务侧无需任何改动。
## 7. 模块化装配

`Program.cs` 保持极薄，只做三件事：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddIsupSdk();   // 模块：ISUP 设备接入（配置 + 运行时 + HostedService）
builder.AddIsupApi();   // 模块：Web 宿主（CORS + MVC/JSON + Swagger + 异常处理）
var app = builder.Build();
app.UseIsupApi();       // 管道
app.MapIsupEndpoints(); // 控制器 + /health + 根路径跳转
app.Run();
```

- **设备接入**与 **Web 宿主**互不依赖，可单独启用/替换（例如把 SDK 放进独立进程时，只移除
  `AddIsupSdk()`，接口仍能启动并在 `/health` 呈现 degraded）。
- 端点、控制器、服务各归其位；共享的 DTO 映射集中在 `Services/IsupStatusFactory`。

## 8. 后续待办

见 [docs/CONSOLE_TO_WEBAPI.md](docs/CONSOLE_TO_WEBAPI.md) 第 5 节。当前剩余：业务日志统一到
`ILogger`、补齐优雅停止（`StopListen` / `Release`）、SS 主动上传到云存储、补测试项目。
