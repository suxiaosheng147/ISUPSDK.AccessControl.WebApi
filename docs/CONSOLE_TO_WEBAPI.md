# 从控制台到 Web API：迁移映射

本文说明 **ISUPSDK.NETCore（控制台）→ ISUPSDK.AccessControl.WebApi（本项目）** 的对应关系。
原控制台项目目录**保持零改动**，本项目是其源码的一份独立副本，两边可独立演进。

## 1. 启动流程

| 控制台（`ISUPSDK.NETCore/Program.cs`） | 本项目 |
|---|---|
| `Main`：`ConfigurationBuilder` 读 `appsettings.json` 的 `Settings` 节 | `IsupSdkRuntime.InitializeAsync`：使用宿主注入的 `IConfiguration`，节名由 `ISUP:SettingsSection` 指定 |
| `Main`：`InitParamFromXML()` 读 `ISUPDemo.xml` | 同逻辑，路径改为 `Path.Combine(AppContext.BaseDirectory, "ISUPDemo.xml")` |
| `Main`：`InitLib()` 初始化 SS/CMS/Stream/Alarm 并监听 | 同逻辑，SDK 日志目录改为 `ISUP:SdkLogDirectory` |
| `Main`：`CleanDayBefore()` + `AddQuantityTask(Crontab)` | 同逻辑，仍在初始化末尾执行 |
| `Main`：`Task.Delay(Timeout.Infinite)` 阻塞主线程 | 删除；改由 `IsupSdkHostedService` 在应用启动阶段调用，进程由 Web 宿主维持 |
| `Main` 里 SDK 初始化失败 → 打印并继续 | `IsupSdkHostedService` 捕获异常并记日志，服务以降级（degraded）方式继续运行 |

## 2. 静态状态 → 运行时服务

原 `Program` 是 `internal class Program` + 全静态成员。本项目把它整体搬成
`Services/IsupSdkRuntime.cs`（`public sealed class IsupSdkRuntime`，进程内单例）：

| 原来 | 现在 |
|---|---|
| `private static Settings settings` | 实例字段，并在 `CurrentSettings` 上只读暴露 |
| `private static ConcurrentDictionary<int, string> MatchDic` | 实例字段，只读暴露为 `DeviceHandles` / `DeviceIds` |
| `private static ConcurrentDictionary<string, CancellationTokenSource> AllTask` | 实例字段，暴露 `SyncingDeviceCount` / `IsSyncLoopRunning(deviceId)` |
| 全部 `static` 方法（回调、地址解析、报警处理、定时器） | 去掉 `static`，改为实例方法；SDK 回调委托改为指向实例方法 |
| — | 新增 `AlarmCount` / `LastAlarmAt` 计数，供 `/health` 使用 |
| 设备上线后内嵌的同步 `Task.Run(...)` | 逻辑保留；另抽出 `SyncDeviceAsync(deviceId)` 供手动触发 |
| 定时任务里的 `ISUPDevTime.Post` | 逻辑保留；另抽出 `SyncDeviceTimeAsync(deviceId)` 供手动触发 |

以下成员因原类为 `internal` 而随之收敛访问级别（不对外暴露）：
`rmMsgCallBack` 委托、`ptrAlarmCB` / `ptrTcpAlarm` / `ptrMqtt` 字段。

## 3. 目录对应

| 控制台项目 | 本项目 | 说明 |
|---|---|---|
| `SdkService/` | `SdkService/` | ISUP 子服务 P/Invoke 封装（CMS / Alarm / SS / Stream） |
| `Structure/` | `Structure/` | 公共结构体（`HCEHomePublic` 等） |
| `Model/` | `Model/` | 业务模型（`im_machine`、`PPVSPMessage`、`AccessControl` 等） |
| `Method/` | `Method/` | 公共方法（`Common`、`HttpHelper`、`JsonHepler`…） |
| `Distribute/` | `Distribute/` | 门禁下发（人员/卡片/人脸/计划/开门） |
| `Customize/` | `Customize/` | 权限同步（`SyncInformation` / `SyncAuthRuleInformation`） |
| `DerivedClass/` | `DerivedClass/` | 辅助类型（`SafeHGlobalHandle`） |
| `Program.cs` | `Services/IsupSdkRuntime.cs` | 见上文 |
| — | `Interop/`、`Options/`、`Hosting/`、`Middleware/`、`Controllers/`、`Models/`、`Modules/`、`Endpoints/` | 新增的 Web API 骨架 |
| `appsettings.json`、`ISUPDemo.xml`、`.editorconfig` | 同名文件 | 配置随项目复制 |

命名空间统一由 `ISUPSDK.NETCore.*` 改为 `ISUPSDK.AccessControl.WebApi.*`。

## 4. 新增的宿主骨架（模块化）

```
Modules/
├── IsupSdkModule.cs      # 设备接入模块：ISUP 配置 + IsupSdkRuntime + HostedService + 日志目录
└── IsupApiModule.cs      # Web 宿主机模块：CORS + MVC/JSON + Swagger + 异常中间件 + 端点映射
Endpoints/
└── HealthEndpoints.cs    # /health
Controllers/
├── DevicesController.cs  # 设备列表、手动同步、手动校时
└── SystemController.cs   # SDK 状态
```

`Program.cs` 只做编排：

```csharp
builder.AddIsupSdk();   // 设备接入
builder.AddIsupApi();   // Web 宿主
var app = builder.Build();
app.UseIsupApi();
app.MapIsupEndpoints();
app.Run();
```

## 5. 已知差异与后续待办

- **日志**：✅ 已统一到 `ILogger` —— `Common.WriteFile*` 与 `Console` 输出经静态门面
  `Services/Logging/IsupLog` 汇入 `ILogger`（分类 `Isup`），调用点未改动；
  `ISUP:Logging:FileFallback=true`（默认）时仍按原习惯落盘到 `ISUP:LogDirectory` 下的 `log/log_yyyyMMdd/`。
- **优雅停止**：`IsupSdkHostedService.StopAsync` 目前为空实现。原控制台程序也没有停止逻辑，
  后续可补 `NET_ECMS_StopListen` / 各组件 `StopListen` 与 `Release`。
- **业务接口化**：门禁下发（人员/卡片/人脸/计划/开门）目前仍以 `Distribute/` 内部调用为主，
  只有"权限同步"和"校时"已暴露为 REST 接口；其余按批次迁移。
- **测试**：`ISUPSDK.AccessControl.Tests` 尚未创建（csproj 已声明 `InternalsVisibleTo`）。

## 6. 门禁控制器与设备能力映射

10 个控制器按 OTAP 项目的划分一一对应，业务报文（ISAPI）相同，仅底座不同：

| 控制器 | 设备侧 ISAPI | ISUP 侧实现 |
|---|---|---|
| Persons | `/ISAPI/AccessControl/UserInfo/*` | `IsupIsapiService` → `IsupCmsService.SendIsapiAsync` |
| Cards | `/ISAPI/AccessControl/CardInfo/*` | 同上 |
| Faces | `/ISAPI/Intelligent/FDLib/*`（`FaceDataRecord` multipart 或 `FDSetUp` JSON） | 同上（推荐 faceURL 方式，与原控制台 `ISUPPostFace` 一致） |
| PlanTemplates | `/ISAPI/AccessControl/UserRight*` | 同上（复用 `Services/Builders/PlanTemplatePresets`） |
| AccessControlDoors | `/ISAPI/AccessControl/RemoteControl/door/*`、`/System/BaseWorkStatus` 等 | 同上 |
| Devices | — | `IsupCmsService`（设备表来自 CMS 注册回调）+ PTXML 透传 |
| Alarms | `/ISAPI/Event/notification/httpHosts` | `IsupAmsService` 订阅 `IsupSdkRuntime.AlarmReceived`（`NET_EALARM_StartListen` 回调） |
| Storage | — | `IsupSsService` 管理 `Settings:PicturePath` 下由 SS 存储回调落盘的图片 |
| Events | `/ISAPI/AccessControl/AcsEvent`（补漏用） | `IsupEventIngestService` 订阅报警事件 → `AcsEventStore` 幂等入库 |
| Tasks | — | `AcsTaskService` + EF Core 任务库（SQLite） |

### 与原控制台 `Distribute/` 的关系

`Distribute/` 里的方法是**面向平台同步场景**的高层封装（入参是平台 DTO，如 `SyncPersonDataPage`），
仍然保留并被 `Customize/SyncInformation`、`SyncAuthRuleInformation` 使用（设备上线后的自动权限同步）。
新增的控制器走的是**通用 ISAPI 透传**路径（入参是 ISAPI 风格 JSON），两者并存：

- 平台对接（请假同步、认证规则、按学校/人员批量下发）→ 继续走 `Customize/` + `Distribute/`；
- 平台/前端直接对设备做门禁操作 → 走本项目的 REST 控制器。

### 与 OTAP 项目的已知差异

| 项 | OTAP 项目 | 本项目 |
|---|---|---|
| 设备接入 | `OTAP_CMS_*`（HCOTAPSDK） | `NET_ECMS_*`（HCISUPSDK） |
| 报警通道 | `OTAP_AMS_*` + 自建 HTTP 推送监听 | SDK 报警回调（`NET_EALARM_StartListen`，UDP/TCP/MQTT 三路）+ 设备侧 `httpHosts` 下发 |
| 事件队列 | None / MSMQ / RabbitMQ 可切换，含落盘重放与死信 | ✅ 同样支持（`None` / `Msmq` / `RabbitMq` + spool 落盘重放 + 死信） |
| 事件补偿 | 历史对账（定时拉取 + 落盘重放） | ✅ 定时对账（`ISUP:EventReconcile`）+ 启动重放，亦可 `POST /api/events/reconcile` 手动触发 |
| 图片存储 | `OTAP_SS_*` 主动上传/下载 | SS 存储回调落盘 + REST 管理（主动上传到云存储待接入） |
| 时间口径 | 存储 UTC ticks + 展示时区转换 | `Settings` 沿用原控制台口径，`ISUP:Time` 仅供事件库/任务库使用 |