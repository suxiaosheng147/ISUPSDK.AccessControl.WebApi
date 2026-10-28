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
├── docs/
│   ├── CONSOLE_TO_WEBAPI.md
│   ├── event-store-{sqlite,mysql,sqlserver}.sql  # 事件表建表脚本（EventStore:AutoCreate=false 时人工执行）
│   └── task-store-{sqlite,mysql,sqlserver}.sql   # 任务表建表脚本（供 DBA 预建 / 核对表结构）
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
  - `LogDirectory`：日志根目录（**NLog** 落盘位置，业务日志按类/日期/小时分文件），默认 `./logs/`
  - `Logging`：NLog 引导配置（`ConfigFile` 默认 `nlog.config`；`Directory` 留空沿用 `LogDirectory`）
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
| `TasksController` | `/api/tasks` | 异步下发任务查询（列表 / 分页 `/api/tasks/page`）/ 重试 / 取消 |
| `SystemController` | `/api/system/sdk` | ISUP SDK 运行状态（初始化、监听句柄、在线设备、报警计数） |

另有：`/health`（健康检查）、`/swagger`（接口文档）、`/`（跳转 Swagger）、
**`/events.html`（内置门禁事件查询页面）**。

内置页面 `wwwroot/events.html` 提供：事件管道状态看板、多条件查询（主/次类型、事件类型、来源、
认证方式、设备、时间段、关键字）、详情报文查看、手工补录、手动对账补漏与落盘重放，
数据全部来自 `GET /api/events*` 系列接口。列表里的「抓拍图」列显示缩略图（多张标 `+N`），
详情弹窗里给出可点击查看原图的图集。

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
  转发报文里的图片字段用**平台自己的地址**（不用设备地址，下游无需访问设备）；
  图片**优先复用入库时已落盘的文件**（转发与入库共用同一套文件名规则
  `{设备}_{事件时间}_{序号|人员编号|报文指纹}.jpeg`），平台还没有图时才按 `pictureURL` 现下载一次
  —— 对账补历史时设备上的图多半已被覆盖，重复下载只会失败并让报文里没有 URL：
  - `ExtEventPictureURL` = `{PictureUrlPrefix}/{文件名}`：前缀**只**取配置
    `ISUP:IsapiDigest:PictureUrlPrefix`（例：`http://10.0.0.5:5190/api/events/pictures`，反代/域名写
    `https://acs.example.com/acs-pictures`，末尾不带 `/`）。不再按 `Settings:ServerAddress` 推导
    —— 那个地址通常只在设备网段可达；留空时用相对路径 `/api/events/pictures`（前端按同源解析即可显示）；
  - `ExtEventPicture` = 落盘文件的本地绝对路径；
  - 平台侧按 `GET /api/events/pictures/{文件名}` 提供图片（只接受纯文件名，防目录穿越）；
  - `GET /api/events` / `GET /api/events/{id}` 返回的事件项里，`pictures` 是文件名数组、`pictureUrls` 是用同一前缀
    拼出的 URL 数组（与 `pictures` 一一对应，无图时为 `null`），前端可直接作为 `<img src>` 显示；
  - **图片在入库前解析**（消费者统一处理，开关 `EventQueue:Consumer:DownloadPictures`，
    单批下载上限 `MaxPictureDownloadsPerBatch` 默认 50，失败只记日志、不影响入库）：
    - ISAPI JSON 报文带 `pictureURL`（Digest 直连 / 对账 / 推送）→ 设备本地地址、**要 Digest 认证**，
      平台下载落盘，文件名进 `pictures`；
    - SDK 报警回调的 PPVSPMessage XML → `Params.PicDataUrl` 指向图片服务器，该地址**可直接访问、不需要下载**，
      平台只把展开后的完整 URL 存进事件的 `PictureUrl` 列（`PictureServerType=2` 云存储拼
      `http://{PictureServerIP}:{PictureServerPort}`，`=3` KMS 报文里就是完整地址），
      查询时直接出现在 `pictureUrls` 里；
    - SDK 的 AcsEvent 查询本身不返回 `pictureURL`（只有直连设备的历史事件才有），所以 SDK 通道补的事件没图；
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
- **对账补的抓拍图**：设备返回的事件带 `pictureURL`（人脸类）时，对账/高频轮询会用 Digest 下载并落盘，
  文件名随事件入库 —— 因此 `GET /api/events` 的 `pictures` / `pictureUrls` 对补漏的事件同样有值。
  开关与限流：`EventReconcile:DownloadPictures`（默认 `true`，实际下载还要求 `ISUP:IsapiDigest:DownloadPictures=true`）、
  `EventReconcile:MaxPictureDownloadsPerRun`（单设备单轮最多发起多少次下载，默认 200，`0` = 不限；
  启动补历史默认回看 60 天，设备上的图多已被循环覆盖，限流避免拖慢对账）。
  同一事件重复对账时文件名稳定（`{设备}_{事件时间}_{事件序号}.jpeg`），已落盘的文件直接复用、不再下载。
- **补齐 0 条怎么排查**：加 `dryRun=true` 只查询不写库（`POST /api/events/reconcile?dryRun=true&from=…&to=…`），
  - `queried`：设备实际返回的事件条数（0 表示设备侧就没有/没查到）；
  - `items[].error`：查询失败原因，含 `subStatusCode` 与设备原始响应摘要（截断 1000 字符）；
  - `items[].detail`：查询成功但设备未返回事件时的说明（`responseStatusStrg` / `numOfMatches` / 页码 / 页大小）。
  也可以用 `POST /api/devices/{deviceId}/isapi` 手工透传 `POST /ISAPI/AccessControl/AcsEvent?format=json` 看设备原始响应。
  服务端对设备响应做了多层清理：原生输出缓冲先清零、按 SDK 返回长度并在首个 NUL 处截断、
  再截到第一个完整 JSON 值（设备常在正文后跟 `0x03` 结束符或零填充/残留字节）；
  日志里出现"响应无法解析为 JSON"时，看该告警附带的响应片段。
- 保留策略：`ISUP:EventStore:RetentionDays`（默认 **90**）> 0 时，每 `RetentionSweepHours`（默认 24）小时
  删除 `EventTime` 早于该天数的 `AcsEventRecord` 行；设 `0` 表示永久保留。任务库的 `AcsTask`
  同理，配置项见下节 `ISUP:TaskQueue:CompletedRetentionDays`。

#### 事件库的数据库选择

| Provider | 配置 | 说明 |
|---|---|---|
| `Sqlite`（默认） | `SqlitePath: "./storage/acs-events.db"` | 零外部依赖，单文件；与任务库 `acs-tasks.db` 分开 |
| `MySql` | `ConnectionString` + `ServerVersion`（如 `8.0.36`） | Pomelo provider；不做版本自动探测，避免库未就绪时启动失败 |
| `SqlServer` | `ConnectionString` | Microsoft provider（EF Core 8 支持 SQL Server 2012+） |

> 任务库（异步下发任务，配置节 `ISUP:TaskQueue`）与事件库是两套独立的 EF Core 上下文，
> 各自用 `Provider` 选择数据库（见下节），可以是不同的库；两者在同一进程里并存没有冲突。

**SQL Server 的连接串必须带 `TrustServerCertificate=true`（或 `Encrypt=false`）**：

```jsonc
"EventStore": {
  "Enabled": true,
  "Provider": "SqlServer",
  "ConnectionString": "Server=192.168.1.124,1433;Database=acs;User Id=sa;Password=<密码>;TrustServerCertificate=true;Encrypt=false;Connect Timeout=15",
  "AutoCreate": true
}
```

- 原因：Microsoft.Data.SqlClient 4.0 起默认 `Encrypt=true`，现场 SQL Server 多为自签证书，
  不带这一项会以"证书链是由不受信任的颁发机构颁发的"连接失败。
- `Provider` 值写错（如 `"SQL Server"` 带空格、`"MSSQL"`）会**静默回退 Sqlite**（`ResolveProvider()`
  只回退、不告警），切库后请用 `GET /api/events/status` 的 `provider` 字段核对实际生效的库。
- `Provider=MySql/SqlServer` 时 `ConnectionString` 必填。漏配不会让进程启动失败，但事件库整体不可用：
  `/health` 与 `GET /api/events/status` 的 `schemaReady=false` / `lastError` 给出原因，事件一条都进不去。
- 时间列在三种库里都存成 UTC ticks（`bigint`），读回按 `ISUP:Time:TimeZone`（默认北京时间）还原偏移；
  查询这些列时要用变量传参（内联常量写法在 SQL Server 上会报 `datetimeoffset 与 bigint 不兼容`）。

**建表两种方式（二选一）**：

1. **让服务自动建表**：`EventStore:AutoCreate=true`（默认；库不存在时 EF Core 自动建库建表，
   SQL Server 上需要建库权限）；
2. **由 DBA 手工执行 DDL**：`EventStore:AutoCreate=false`，然后执行随仓库提供的脚本：

   | 数据库 | 脚本 |
   |---|---|
   | SQLite | [docs/event-store-sqlite.sql](docs/event-store-sqlite.sql) |
   | MySQL / MariaDB | [docs/event-store-mysql.sql](docs/event-store-mysql.sql) |
   | SQL Server | [docs/event-store-sqlserver.sql](docs/event-store-sqlserver.sql) |

   脚本由 EF Core 的 `Database.GenerateCreateScript()` 生成，与实体模型严格一致
   （含唯一索引 `UX_AcsEvents_DedupHash` —— 幂等入库的关键，**不要省略**）。

   > ⚠ `AutoCreate=false` 且表不存在时，事件一条都进不去。服务会在启动时探测并明确报错：
   > `/health` 与 `GET /api/events/status` 的 `schemaReady=false` / `schemaError` 给出原因与应执行的脚本；
   > `GET /api/events` 返回 `503` 而不是裸的数据库异常。

> `EnsureCreated` 只建库/建表、不做列迁移：实体新增列后，换库或升级时要重建表（或用脚本补齐列），
> 否则相关查询会因缺列报错。

#### 任务库（异步下发）的数据库选择

人员 / 卡片 / 人脸的新增、修改、删除是**异步任务**：接口先把请求入队并返回任务 ID，
由 `AcsTaskWorker` 定时取出执行，调用方凭 `GET /api/tasks/{id}` 查询结果。
任务库的配置节是 `ISUP:TaskQueue`，与事件库完全独立：

> 翻页用 `GET /api/tasks/page?pageIndex=1&pageSize=20`（可选过滤：`deviceId` / `entity` / `status` /
> `source` / `createdFrom` / `createdTo`），返回 `total` / `pageIndex` / `pageSize` / `totalPages` / `tasks`，
> 按创建时间倒序；`source=person-operation` 只看人员下发队列创建的任务，`source=direct` 只看来源为空的任务。

| Provider | 配置 | 说明 |
|---|---|---|
| `Sqlite`（默认） | `DatabasePath: "./storage/acs-tasks.db"` | 零外部依赖，单文件；路径按程序目录（`AppContext.BaseDirectory`）解析 |
| `MySql` | `ConnectionString` + `ServerVersion`（如 `8.0.36`） | Pomelo provider；不做版本自动探测 |
| `SqlServer` | `ConnectionString` | Microsoft provider（EF Core 8 支持 SQL Server 2012+） |

```jsonc
"TaskQueue": {
  "Enabled": true,
  "Provider": "SqlServer",
  "ConnectionString": "Server=192.168.1.124,1433;Database=acs;User Id=sa;Password=<密码>;TrustServerCertificate=true;Encrypt=false;Connect Timeout=15"
}
```

- 连接串同样必须带 `TrustServerCertificate=true`（或 `Encrypt=false`），原因见上节。
- `Provider` 写错同样会静默回退 Sqlite；切库后看启动日志"下发任务队列已启动：库=…"核对实际生效的库。
- 非 Sqlite 时 `ConnectionString` 必填：**任务库漏配会让服务启动阶段直接失败**（启动就要建表）；
  事件库则是"启动不失败、只把事件库标记为不可用" —— 任务库取快速失败，避免任务静默堆积。
- 建表：服务启动固定执行 `EnsureCreated`（任务库没有 `AutoCreate` 开关），老库缺 `PayloadFormat`
  列时启动会自动补列（SQL Server 用 `ADD PayloadFormat …`，不带 `COLUMN` 关键字）。
  需要预先建表或核对结构时用脚本：

  | 数据库 | 脚本 |
  |---|---|
  | SQLite | [docs/task-store-sqlite.sql](docs/task-store-sqlite.sql) |
  | MySQL / MariaDB | [docs/task-store-mysql.sql](docs/task-store-mysql.sql) |
  | SQL Server | [docs/task-store-sqlserver.sql](docs/task-store-sqlserver.sql) |

- **记录保留**：`CompletedRetentionDays`（默认 **30**，`0` 表示不清理）天前**已结束**的任务
  （`CompletedAt` 有值：`Succeeded` / `Failed` / `Canceled`）由 `AcsTaskWorker` 顺带清理，
  执行间隔是 `RetentionSweepHours`（默认 1 小时，最小 1）。判定依据是任务的 `CompletedAt`，
  **不是**入队时间；`Pending`（含"等设备上线"）和 `Running` 的任务永远不会被清理，
  所以设备长期离线时任务会一直堆积，需要人工处理。
  删除条数见日志"任务清理：已删除 N 条超过 X 天的已结束任务"，
  生效值也可从启动日志"…已结束任务保留=…天"核对。

> **切库提醒**：换到 MySQL / SQL Server 后，原 SQLite 文件里的任务历史不会自动迁移，
> 未执行完的 `Pending` 任务建议先跑完再切。
> **多实例**：`AcsTaskService.TakeDueAsync` 用数据库层的**条件更新**抢任务
> （`update AcsTasks set Status = Running, StartedAt = ? where Id = ? and Status = Pending`，
> 只有影响 1 行的实例才真正拿到该任务），所以多进程 / 多台机器共享同一个任务库时，
> 同一条任务只会被下发一次；单实例下每轮的额外开销是至多 `BatchSize` 次主键小更新。
> 仍需注意两点：
> 1. 执行中实例崩溃时任务会停在 `Running`，需要人工 `POST /api/tasks/{id}/retry`；
> 2. `TaskQueue:BinaryDirectory`（人脸图片等附件）仍是本地磁盘，多实例需改用共享目录。
> SQLite 单文件在多实例下还有写锁竞争，多实例建议用 MySQL / SQL Server。

#### 人员下发队列（平台同步 → 进程内入队 → 顺序下发）

平台（`Customize/SyncInformation`、`SyncAuthRuleInformation`）下发的人员增/改/删**不再实时调用**
`Distribute/` 的 SDK 方法，而是先写入独立的队列库 `isup_person_queue.db`，再由**两个相互独立的定时任务**
入队到 `AcsTasks` 并回传结果：

```
平台 → Customize（入队）→ isup_person_queue.db
        ┌────────────────────────────────────────────────────────────────┐
        │ ① 入队  Crontab:PersonOperationQueue（默认每分钟）              │
        │   PersonOperationExecutor.PublishAsync：进程内直接调           │
        │   AcsTaskService（不经控制器/HTTP），一条人员操作 = 一条任务： │
        │   Operation=PersonIssue / PersonIssueDelete                    │
        │   → AcsTasks（任务 ID 记回队列表 TaskIds）→ AcsTaskWorker      │
        ├────────────────────────────────────────────────────────────────┤
        │ ② 收结果  Crontab:PersonOperationResult（默认每分钟）          │
        │   PersonOperationExecutor.CollectAsync：直接读任务库           │
        │   完成后回传平台（ReturnDataUrl / ReturnErrorUrl /             │
        │   SyncPersonData），并清理过期任务                             │
        └────────────────────────────────────────────────────────────────┘
```

**下发顺序**（设备要求"先有人员，才能下发卡片和人脸"）在 `AcsTaskExecutor` 内保证：
`PersonIssue` 按 **人员 →（删旧卡）→ 卡 → 人脸** 依次执行，任何一步设备没接受**立刻停止**，
后面的卡/人脸绝不下发；`PersonIssueDelete` 是 **删人员 → 删人脸**。
因为三步在同一条任务内顺序执行，所以**不能**把一条人员操作拆成多条任务（拆开后各自被
`AcsTaskWorker` 取走，顺序不可控）。

报文口径与原来的 `Distribute/` 一致，**四步全部是"SetUp 覆盖"语义**（新增和修改用同一套报文，设备上已有就覆盖）：

| 步骤 | 请求 | 报文 |
|---|---|---|
| 人员 | `PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json` | `{"UserInfo":{"employeeNo","name","userType":"normal","Valid":{…},"doorRight":"1","RightPlan":[…]}}` |
| 旧卡（换卡时） | `PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json` | `{"CardInfo":{"cardNo","employeeNo","cardType":"normalCard","deleteCard":true}}` |
| 卡 | `PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json` | `{"CardInfo":{"cardNo","employeeNo","cardType":"normalCard","deleteCard":false}}` |
| 人脸 | `PUT /ISAPI/Intelligent/FDLib/FDSetUp?format=json` | `{"faceLibType":"blackFD","FDID":"1","FPID","name","faceURL"}`（裸报文，不包根节点） |

> 不要用 `POST CardInfo/Record`（部分固件返回 `subStatusCode=MessageParametersLack`）；
> 删除人员 / 没有照片时清旧人脸仍走 Delete 接口（删除没有覆盖语义）。

两个定时任务**不共用一把锁、也不在同一轮里顺序执行**：任务要等 `AcsTaskWorker` 真正下发到设备
（设备离线时可能等上几分钟甚至几十分钟），如果和入队挤在同一轮，会把新的人员数据一起堵住。

- 配置节：`Settings:Database`（队列库，默认 SQLite `isup_person_queue.db`）、
  `Settings:PersonOperationQueue`（批量、间隔、回传开关、任务等待超时、删除兜底）。
- 定时任务（`Crontab` 节，任一留空即不启动）：`PersonOperationQueue`（入队）、
  `PersonOperationResult`（收结果）、`PersonDeleteVerify`（删除兜底）。
- 队列表 `PersonOperationTask` 的 `TaskIds` 列保存对应的 `AcsTask.Id`（JSON 数组，
  一条人员操作只有一个元素），可用 `/api/tasks?deviceId=…`、`/api/tasks/page` 或 `GET /api/tasks/{id}` 查。
- 入队去重：同一设备 + 人员 + 流程 + 操作，只要已有**在途**记录
  （`Pending` / `Running` / `Issuing`）就直接跳过 —— 既不新增也不覆盖那条记录的载荷
  （在途记录可能已被执行器取走，改载荷会前后不一致）；等它走完，平台下次下发的变更会重新入队。
  终态（`Succeeded` / `Failed`）不影响新入队。
- 区分来源：入队时显式把 `AcsTask.Source` 设为 `person-operation`（`/api/tasks` 的 `source` 字段）；
  调用方直接调控制器创建的任务该字段为空。收结果阶段会校验来源，
  遇到"不是本队列创建"的任务按失败处理并记日志，避免把别人的任务当成人员下发结果。
- 状态流转：`Pending`（待下发）→ `Running`（正在入队）→ `Issuing`（已入队，等设备执行）
  → `Succeeded` / `Failed`。`Issuing` 超过 `TaskTimeoutMinutes`（默认 60）仍未结束按失败回传，
  避免设备长期离线导致平台永远收不到结果。
- 收结果口径：**只有 `AcsTask` 的 `Succeeded` / `Failed` 两个终态才回传平台**；
  `Pending` / `Running`（设备还没执行完）和 `Canceled`（任务被人为取消、不会再有结果）都不回传，
  继续留在 `Issuing` 等下一轮，直到超过 `TaskTimeoutMinutes` 才按失败回传。
- 结果口径：人员 → 卡 → 人脸**任一步失败即整条人员记录判失败**（后续步骤不再下发，
  失败原因会写明是哪一步，例如"卡片下发失败（subStatusCode=…）"）；
  认证规则流程入队时会先回传 `State=4（下发中）`，最终结果再由定时任务回传 `3/2`。
- `Settings:PersonOperationQueue:Enabled=false` 可退回原来的实时下发（直接调用 `Distribute/`）。
- 队列库的建表与补列（`TaskIds`）在宿主启动后自动完成，老库无需手工迁移。

### 5.1 ISAPI 透传底座

所有门禁业务最终都经 `IsupCmsService.SendIsapiAsync` 透传到设备，
底层是 ISUP CMS 的 `NET_ECMS_Get/Put/Post/DeletePTXMLConfig`（`NET_EHOME_PTXML_PARAM`），
与 OTAP 侧 `OTAP_CMS_ISAPIPassThrough` 等价。透传失败会尝试放大输出缓冲区（64KB → 256KB → 1MB）后重试。

调试时的排查顺序：HTTP 响应体 `message` → `data.status.subStatusCode` → `data.rawResponse`（设备原始报文）→ 服务端日志中的 SDK 错误码。

#### 门远程控制的报文格式（按设备型号切换）

开门 / 关门 / 常开 / 常闭固定是 `PUT /ISAPI/AccessControl/RemoteControl/door/<doorNo>`，
但**报文体格式随固件不同**，由 `ISUP:AccessControl:DoorControlFormat` 决定：

| 取值 | 请求行 | 报文体 | 适用 |
|---|---|---|---|
| `Json`（代码默认） | `.../door/1?format=json` | `{"RemoteControlDoor":{"cmd":"open"}}` | 海康通用格式 |
| `Xml` | `.../door/1`（不带 `format=json`） | `<RemoteControlDoor><cmd>open</cmd></RemoteControlDoor>` | DS-K1T670M 等只认 XML 的固件 |
| `Auto` | 先发 JSON | 报文类错误时自动改发 XML | 型号混杂、不想逐台配 |

现场实测（DS-K1T670M）：JSON 报文被设备回
`statusCode=6 Invalid Content / subStatusCode=MessageParametersLack`（"the required node does not exist"），
同样内容改用 XML 立即成功（`statusCode=1 OK`，`doorLockStatus` 由 `0` 变 `1`，关门后回到 `0`）。
该机型对参数错误的区分可用于定位问题：XML + 非法 `cmd` 回 `badParameters`、不存在的门号回 `invalidID`。

> 排查顺序：先看响应里的 `data.status.subStatusCode`。
> `MessageParametersLack` / `badParameters` → 报文格式或节点不对；
> `invalidID` → 门号错；`notSupport` → 该机型不支持这个接口
> （例如 `GET /ISAPI/AccessControl/DoorStatus/<doorNo>` 在 DS-K1T670M 上不支持，
> 改用 `GET /api/access-control/work-status` 的 `doorStatus` / `doorLockStatus`）。
> `Auto` 只在报文类错误时重发 XML，SDK 层失败（`sdkSuccess=false`）不会重试，避免把一次真实动作重复下发。

## 6. 日志

整个项目只用一个日志框架：**NLog**（[`src/ISUPSDK.AccessControl.WebApi/nlog.config`](src/ISUPSDK.AccessControl.WebApi/nlog.config)）。

- **装配**：`Program.cs` 的 `builder.AddIsupNLog()` 先 `ClearProviders()` 掉内置 Console / Debug / EventSource，
  再 `UseNLog()`；`app.UseIsupNLog()` 负责正常停机时 flush。此后宿主 `ILogger<T>`、EF Core、Kestrel 与业务日志
  全部只由 NLog 决定去向。配置文件缺失或写坏时退回代码内置的等价默认配置
  （[`Modules/IsupLoggingModule.cs`](src/ISUPSDK.AccessControl.WebApi/Modules/IsupLoggingModule.cs)），不会静默丢日志。
- **业务日志**（原控制台项目的 `Common.WriteFile` / `Common.WriteFileHours` 以及散落的 `Console.WriteLine`）
  经静态门面 [`Services/Logging/IsupLog.cs`](src/ISUPSDK.AccessControl.WebApi/Services/Logging/IsupLog.cs) 进入 NLog，
  门面产生的 logger 名以 `IsupLog` 开头，`nlog.config` 用 `IsupLog*` 规则单独落盘、不混进 `app.log`。
  之所以用门面：原控制台大量逻辑位于**静态方法**与 **SDK 回调**中，无法逐处注入 `ILogger`；
  门面由宿主在启动时接入一次，**所有调用点均未改动**。
- **落盘布局**（根目录由 `ISUP:LogDirectory` 决定，默认 `.\logs\`）：

  | 日志 | 路径 | 例子 |
  |---|---|---|
  | 业务日志 | `<LogDirectory>\log\<类名>\<yyyy-MM-dd>\<HH>.log` | `logs\log\SyncDoorData\2026-05-06\14.log` |
  | 宿主日志 | `<LogDirectory>\log\<yyyy-MM-dd>\app.log` | `logs\log\2026-05-06\app.log` |

  「类名」= 原来的日志文件名去扩展名：`Common.WriteFile("SyncDoorData.txt", …)` → `logs\log\SyncDoorData\`；
  `IsupLog.Info(…)` 这类没传文件名的落在 `logs\log\IsupLog\`。宿主日志（控制器 / HostedService / EF Core）
  不分类，按日期汇总到 `app.log`。
- **格式**：`yyyy-MM-dd HH:mm:ss.ffff [级别] [类别] 消息`（UTF-8，异常附堆栈），例如：

  ```
  2026-05-06 14:32:07.4821 [INFO] IsupLog.SyncDoorData: [SyncDoorData] 设备FZ7955795同步数据无效：Status=1
  2026-05-06 14:32:08.1130 [INFO] ISUPSDK.AccessControl.WebApi.Services.IsupSdkRuntime: 下发任务队列已启动：库=Sqlite
  ```
- **级别**：先由标准 `Logging:LogLevel` 过滤，再由 `nlog.config` 的 `<rules>` 决定去向：

  ```jsonc
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"   // 轮询取数的 SQL 不刷屏
    }
  }
  ```

  规则：业务日志 `Trace` 以上进 `bizFile`+控制台并终止匹配；其余 `Info` 以上进 `app.log`+控制台；
  `Debug` 只进 Visual Studio 输出窗口，不落盘。改 `nlog.config` 后需重启进程（NLog 6 不再内置配置文件监视）。
- **不在 NLog 管辖内**：SDK 原生库（HCISUPCMS / HCISUPAlarm / HCISUPSS / HCISUPStream）自己写的日志，
  目录由 `ISUP:SdkLogDirectory`（`SetLogToFile`）决定，默认 `.\logs\isupsdk\`。
- **IIS 托管**：`web.config` 的 `stdoutLogEnabled` 已改为 `false`，不再生成 `logs\stdout_<UTC时间>_<pid>.log`；
  排查冷启动/回收直接看 `logs\log\<yyyy-MM-dd>\app.log`。

> 需要转发到 ELK / Seq / 数据库时，在 `nlog.config` 里加一个 target 和对应 `<logger>` 规则即可，业务侧无需改动。

## 7. 模块化装配

`Program.cs` 保持极薄，只做编排：

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddIsupNLog();  // 模块：日志（整个进程只走 NLog）
builder.AddIsupSdk();   // 模块：ISUP 设备接入（配置 + 运行时 + HostedService）
builder.AddIsupApi();   // 模块：Web 宿主（CORS + MVC/JSON + Swagger + 异常处理）
var app = builder.Build();
app.UseIsupNLog();      // 停机 flush NLog
app.UseIsupApi();       // 管道
app.MapIsupEndpoints(); // 控制器 + /health + 根路径跳转
app.Run();
```

- **设备接入**与 **Web 宿主**互不依赖，可单独启用/替换（例如把 SDK 放进独立进程时，只移除
  `AddIsupSdk()`，接口仍能启动并在 `/health` 呈现 degraded）。
- 端点、控制器、服务各归其位；共享的 DTO 映射集中在 `Services/IsupStatusFactory`。

## 8. 后续待办

见 [docs/CONSOLE_TO_WEBAPI.md](docs/CONSOLE_TO_WEBAPI.md) 第 5 节。当前剩余：补齐优雅停止（`StopListen` / `Release`）、
SS 主动上传到云存储、补测试项目。
