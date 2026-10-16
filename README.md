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
    │   └── Events/                    #   门禁事件入库管道 + 报警转发（判定与发送在消费者侧）
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
SDK 报警回调 ──► IsupAmsService（内存缓冲；只广播原始报文，不做判定）
                      ▲
   设备 HTTP 上报 / 历史对账 ──► AcsEventSpool（落盘 + 去重）
                      │
              AcsEventPublisher ──► AcsEventPublishWorker
                      │
        IEventQueue（None / Msmq / RabbitMq）
                      │
        AcsEventQueueConsumerService（批量 + ack / 重投 / 死信）
                      │            │
                      │            └─ 报警转发（AcsAlarmForwardService）：按 MinorType 判定
                      │               → 下载图片 + 组 Events JSON
                      │               → Settings:MessageQueueAddress（默认 .\Private$\haikangevent）
                      │
        AcsEventStore（SQLite / MySQL / SQL Server，DedupHash 唯一索引幂等）
```

**报警转发：判定与发送在消费者侧**。生产端（SDK 报警回调）只把报文原样投进队列，
不做业务判定、也不直接对外发送；消费者在事件落库成功后判定并转发到下游队列
（`Settings:MessageQueueAddress`，由 `Settings:IsMessageQueue` 总开关控制）：

| 载荷来源 | 转发规则 |
|---|---|
| PPVSPMessage XML（SDK 报警回调） | `0x4b` / `0x3c`（人脸）必须带有效图片（`PicDataUrl` 非空且不是 `Failed-to-upload-picture`）才转发；`0x5` / `0x1`（刷卡）无条件转发；其它 MinorType 跳过 |
| ISAPI JSON（历史对账补漏 / HTTP 推送） | 报文没有图片信息，采用**无图降级**：白名单内的 `minor` 一律转发（`1` / `5` / `75` / `60`），转发报文的图片字段留空，人脸类仍按人脸事件码 196893 上报（不降级成刷卡） |

转发内容为 `Events` JSON（`eventType`、图片 URL 与本地图片路径，链路与原控制台一致）。
只有"本次真正新增入库"或"重投"的消息才转发：同一事件被重复投递（设备重发、对账补录同一条）
时不再二次发往下游（计入 `consumer.forwardDuplicates`），因此"回调丢失、由对账补进库"的刷卡/人脸事件
会补发给下游，而"回调已发过、对账又补录"的事件不会重复发。
转发失败按与落库失败相同的策略重投（`EventQueue:Consumer:MaxAttempts` / `RetryDelaySeconds`），
超限进死信；入库按 `DedupHash` 幂等，因此重投不会重复落行。
`GET /api/events/status` 的 `consumer.forwarded` / `forwardSkipped` / `forwardDuplicates` / `forwardFailed` 给出转发统计。

**跨来源去重（同一设备事件只入库一行）**：去重键统一为 `deviceId:serialNo:事件时间`
（`AcsEventSpool.ExtractSerialNo` / `ExtractEventTimeKey` 同时解析 JSON 与 PPVSPMessage XML 里的
`serialNo` 与事件时间；时间按报文自带的"墙上时间"归一化，不做时区换算，因此不受部署机时区影响）。
报警回调 / HTTP 推送 / 历史对账三条通道共用这一个键，入库按 `DedupHash` 幂等：
同一条事件被"回调 + 对账"各收到一次、或设备重发时，都只落一行；设备重启后 `serialNo` 从头计数
也不会把新事件误判成旧事件的重复。报文里取不到 `serialNo` 时才退化为报文哈希；
手工补录 `force=true` 仍可写入重复行。

| Provider | 说明 |
|---|---|
| `None` | 进程内直通（单机部署默认） |
| `Msmq` | Windows 消息队列，走 `mqrt.dll` P/Invoke；`CreateIfMissing=true` 时自动创建 `.\Private$\<QueueName>` |
| `RabbitMq` | AMQP 0-9-1；Broker 不可达时 `ready=false`，进程与其它接口不受影响 |

- 队列不可用时：历史查询照常，`GET /api/events/status` 的 `queue.ready=false` 与 `lastError` 给出原因；
  应急可用 `POST /api/events/manual?directToStore=true` 绕过队列直接入库。
- **设备 ISAPI 直连（Digest）——对账的优先通道**：`ISUP:IsapiDigest` 里配置设备 IP / 编号 / 账号 / 密码（可配多台），
  不依赖 SDK 设备注册即可 `POST /ISAPI/AccessControl/AcsEvent?format=json` 拉取事件。
  对账目标 = **在线 SDK 设备 ∪ 已配置的直连设备**，通道按配置决定：
  - **配置里有该设备 → 走 Digest 直连**（跳过 SDK 对账）；
  - 配置里没有但设备在线 → 走 SDK 透传对账；
  - 直连失败时是否回退 SDK 由 `FallbackToSdk` 控制（默认 `true` = 回退兜底，`false` = 该设备只走直连）。

  这类报文带 `pictureURL`（人脸事件抓拍图，需 Digest 认证才能取）。落盘开关与目录由本节点控制：
  `DownloadPictures`（是否下载）与 `PictureDirectory`（目录，留空沿用 `Settings:Photo`）。
  转发报文里的图片字段用**平台自己的地址**（不用设备地址，下游无需访问设备）：
  - `ExtEventPictureURL` = `{PictureUrlPrefix}/{文件名}`，默认前缀按 `Settings:ServerAddress` + Kestrel 端口推导为
    `http://{ServerAddress}:{port}/api/events/pictures`，可用 `PictureUrlPrefix` 覆盖（反代/域名场景）；
  - `ExtEventPicture` = 落盘文件的本地绝对路径；
  - 平台侧按 `GET /api/events/pictures/{文件名}` 提供图片（只接受纯文件名，防目录穿越）；
  - 下载失败时两个字段都留空（不回落成设备地址）。
  逐设备用的通道见响应的 `items[].channel`（`IsapiDigest` / `Sdk` / `None`）。

- **Digest 高频轮询（抓拍图时效）**：`PollIntervalSeconds`（默认 60，`&lt;=0` 关闭）让服务按短间隔直连每台已配置设备拉增量事件
  （首轮回看 `PollLookbackSeconds`，每轮与上次时间重叠 `PollOverlapSeconds`，单轮最多 `PollMaxPages` 页）。
  原因：**人脸抓拍图只存在设备本地且会被循环覆盖**，越早拉越能拿到；定时对账（默认 5 分钟）粒度太粗。
  轮询只处理已配置 Digest 的设备，与对账 / 回调共用去重键（`deviceId:serialNo:事件时间`），
  不会重复入库或重发下游；关闭时行为与之前一致（只在对账环节用直连）。

- 定时对账（默认 5 分钟）从设备拉取历史事件补齐回调丢失的缺口，增量窗口为
  "上次对账时间 − `EventReconcile:OverlapSeconds` → 现在"；`POST /api/events/reconcile` 可手动触发，并支持**按区间补历史**：
  - 指定区间：`?from=2026-10-15T00:00:00&to=2026-10-16T00:00:00`
    （不带时区偏移按 `ISUP:Time:TimeZone` 解释；只给 `from` 时补到"现在"，只给 `to` 时按回看窗口倒推）；
  - 指定回看：`?lookbackSeconds=3600`（从"现在"回看 N 秒）；
  - 都不给：用配置的增量窗口与设备白名单。
  补历史区间**不推进**定时对账的进度。内页 `events.html` 的"手动对账补漏"已提供开始/结束时间、
  每轮最大页数与"仅诊断"开关。
- **分页取完整个区间**：按设备返回的 `responseStatusStrg=MORE` 逐页取（`EventReconcile:PageSize` 是请求页大小，
  但设备侧常有自己的单页上限，例如 30 条 —— 所以**只信任 `MORE`**，不能拿"本页条数 < 请求页大小"当结束条件）。
  页数上限：补历史（显式 `from`/`to`）默认 **500 页**（一年区间可能上万条），定时/增量模式用
  `EventReconcile:MaxPagesPerRun`（默认 20 页 ≈ 600 条）；两者都可用 `?maxPages=N` 覆盖。
  响应里的 `truncated=true` 表示到页数上限时设备还有剩余（同时给出 `maxPages`）：
  调大 `maxPages`，或用该设备的 `items[].nextFrom` 作为新的 `from` 继续补
  （`nextFrom` 是本轮最后一条事件的时间，设备按时间升序返回）。
- **补齐 0 条怎么排查**：加 `dryRun=true` 只查询不写库（`POST /api/events/reconcile?dryRun=true&from=…&to=…`），
  - `queried`：设备实际返回的事件条数（0 表示设备侧就没有/没查到）；
  - `items[].error`：查询失败原因，含 `subStatusCode` 与设备原始响应摘要（截断 1000 字符）；
  - `items[].detail`：查询成功但设备未返回事件时的说明（`responseStatusStrg` / `numOfMatches` / 页码 / 页大小）。
  也可以用 `POST /api/devices/{deviceId}/isapi` 手工透传 `POST /ISAPI/AccessControl/AcsEvent?format=json` 看设备原始响应。
  服务端对设备响应做了多层清理：原生输出缓冲先清零、按 SDK 返回长度并在首个 NUL 处截断、
  再截到第一个完整 JSON 值（设备常在正文后跟 `0x03` 结束符或零填充/残留字节）；
  日志里出现"响应无法解析为 JSON"时，看该告警附带的响应片段。
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
