# IIS 部署与运行注意事项

> 适用：`src/ISUPSDK.AccessControl.WebApi` 以 **IIS + ASP.NET Core Module V2（in-process）** 方式部署。
> 本文的配置取值均为 **2026-10-23 在部署机（站点 `ISUPSDK.AccessControl.WebApi.git`）实测**，
> 不是"默认值推断"。改动机器配置前请按第 10 节的命令逐条核对当前值。

---

## 0. 一句话结论

**当前配置下，IIS 应用池的空闲超时是 20 分钟且到点"终止进程"，所以"没有请求就会休眠"——而且休眠等于设备接入停摆。**
本项目不是普通 CRUD API：ISUP SDK 的注册/报警监听、定时同步、事件管道、异步任务全部跑在 w3wp 进程里，
进程被回收的那一刻，它们一起停。

---

## 1. 现场事实（实测）

| 项 | 实测值 |
|---|---|
| 站点名 | `ISUPSDK.AccessControl.WebApi.git`（id=4，`serverAutoStart=true`） |
| 绑定 | `*:7823:` |
| 物理路径 | `E:\ISUPSDK.AccessControl.WebApi` |
| 应用池 | 同名；`managedRuntimeVersion=""`（无托管代码）、**`managedPipelineMode="Classic"`**、`startMode="OnDemand"` |
| 托管模式 | `web.config` → `hostingModel="inprocess"`（应用进程 = `w3wp.exe`） |
| 端口 | `7823`（IIS 站点，由 HTTP.sys 监听）；`7660`（`Settings:InitPort`，CMS 设备注册）、`8081`（ESS 图片存储监听）**由 w3wp 进程持有**；`8083`（`ACSHttpPush:ListenPort`，默认 `Enabled=false`） |
| 进程内后台服务 | `IsupSdkHostedService`、`SyncDevicesCronJob`（`*/1 * * * *`）、`DeviceTimeCronJob`（`*/30 * * * *`）、`IsapiDigestPollService`（默认 60s）、`AcsEventPublishWorker`、`AcsEventQueueConsumerService`、`AcsEventReconcileService`、`EventStoreRetentionService`、`AcsTaskWorker`、`IsupEventIngestService` |
| 落盘目录（相对站点目录） | `storage\acs-events.db`、`storage\acs-tasks.db`（+`-shm`/`-wal`）、`logs\acs-push\spool`、`logs\log\<yyyy-MM-dd>\app.log`（宿主）、`logs\log\<类名>\<yyyy-MM-dd>\<HH>.log`（业务）、`logs\isupsdk\*`、`IsapiDigest\PictureDirectory` |
| 权限 | 站点目录未显式授权 `IIS AppPool\<池名>`；实际可写是靠 `E:\` 根对 `NT AUTHORITY\Authenticated Users` 的 **Modify** 继承来的 |

---

## 2. 头号风险：应用池空闲即终止进程

实测的应用池生效值：

| 设置 | 当前值 | 后果 |
|---|---|---|
| `processModel.idleTimeout` | **00:20:00** | 20 分钟无 HTTP 请求即计时到点 |
| `processModel.idleTimeoutAction` | **Terminate** | 到点**直接杀掉工作进程**（不是挂起） |
| `recycling.periodicRestart.time` | **1.05:00:00**（25 小时） | 另外还会定期硬重启一次 |
| `startMode` | **OnDemand** | 被杀后不会自己起来，等下一个请求冷启动 |
| 站点 `preloadEnabled` | **False** | 没有预热 |
| `processModel.pingingEnabled` | True / 30s | **ping 不算业务请求**，不能阻止空闲回收 |

### 影响面（进程没了就没了）

- CMS 设备注册监听 `7660` 与 ESS 监听 `8081` 断开 → 设备侧显示离线、报警收不到；
- 定时同步/校时 Cron、Digest 轮询、事件去重/落库/转推 worker、异步下发任务 worker 全停；
- 下一个 HTTP 请求到来时冷启动：重新 `NativeLibraryResolver` 加载原生库 → `NET_ECMS_StartListen` →
  设备重新注册 → 恢复在线，**中间的空窗期**里的设备事件只能靠 `logs\acs-push\spool` 的落盘文件事后补录
  （`AcsEventSpool` 启动时从落盘文件恢复去重键、`AcsEventSpoolReplayer` 重放），实时性丢失。

### 必改（详见第 10 节命令）

1. `idleTimeout` 设为 `00:00:00`（禁用空闲超时）——**这条是根因**；
2. `startMode = AlwaysRunning` + 站点 `preloadEnabled = true`（`ApplicationInitializationModule` 本机已安装）；
3. `periodicRestart.time` 改为 `00:00:00`，改用凌晨固定时间回收，避免白天硬重启；
4. **`maxProcesses` 必须保持 1**：进程内独占 `7660`/`8081`，开 Web 园（>1）会导致端口抢占、SQLite 文件争用、事件重复消费。

### web.config 里已经补上的（以及它管不了什么）

`src/ISUPSDK.AccessControl.WebApi/web.config` 现在包含：

```xml
<applicationInitialization doAppInitAfterRestart="true">
  <add initializationPage="/health" />
</applicationInitialization>
<aspNetCore ... hostingModel="inprocess" startupTimeLimit="120" shutdownTimeLimit="60" />
```

- **能做的**：应用池回收/重启后，IIS 立刻对 `/health` 发一次初始化请求，把 SDK 初始化、EF 建表/补列、事件 spool 恢复这些慢启动提前跑完，第一个真实请求和设备重连不必再等冷启动。
- **管不了的**：`idleTimeout`、`startMode`、`preloadEnabled`、`recycling.*` 全部属于 `applicationHost.config`，**web.config 写不进去**（写了会 500.19）。
  所以"防休眠"只能靠应用池设置 → 用 `iis-apppool.ps1 -Apply`（见第 10 节），
  且 `applicationInitialization` 只有在 `preloadEnabled=true` 时才会被持续预热，两者要配套。

---

## 3. 应用池其它核对项

| 设置 | 要求 | 说明 |
|---|---|---|
| 管道模式 | **Integrated**（当前是 `Classic`，⚠ 需核对） | ASP.NET Core 官方只支持集成管道；`Classic` 下虽能跑通部分请求，但属于非受支持组合，遇到 405/映射类怪问题先改这里 |
| 启用 32 位应用程序 | False | ISUP 原生库为 x64，项目 `PlatformTarget=x64` |
| .NET CLR 版本 | 无托管代码（`managedRuntimeVersion=""`） | 保持现状 |
| `recycling.disallowOverlappingRotation` | **True**（当前 False） | 默认的重叠回收会先起新 w3wp 再关旧的，旧进程退出前仍占着 `7660`/`8081` → 新进程 `NET_ECMS_StartListen` 失败，而 `IsupSdkHostedService` 失败只降级不退出，结果是"进程活着但设备接入是废的且不自愈"。禁用后先停旧再起新，宁可短暂断连 |
| `recycling.periodicRestart.schedule` | 每天 `00:00:00` 一次 | 取代默认 25 小时固定间隔回收，把必然发生的中断放到低峰 |
| `web.config` 的 `shutdownTimeLimit` | 60 秒（ANCM 默认只有 **10** 秒） | IIS 回收时最多等这么久就强杀；当前 `IsupSdkHostedService.StopAsync` 还没做 SDK 清理，这个值是给以后留的余量 |
| 快速失败保护（Rapid-Fail Protection） | 关注 | 短时间多次崩溃会把**整个应用池置为禁用**，站点直接 503，需要手工「启动」应用池 |
| 身份 | `ApplicationPoolIdentity` | 保持；权限见第 6 节 |

---

## 4. 单实例约束（很容易踩）

- 同一台机器上**只能有一个进程**占用 `7660` / `8081`。
  常见冲突源：IIS 站点在跑，同时又在 VS 里 F5 或执行 `run.ps1` 起了控制台/自宿主实例 → 后启动的那份 SDK `StartListen` 失败。
- 排查端口归属：

```powershell
Get-NetTCPConnection -State Listen | Where-Object { $_.LocalPort -in 7660,8081,7823,8083 } |
  ForEach-Object { "port={0} pid={1} proc={2}" -f $_.LocalPort,$_.OwningProcess,(Get-Process -Id $_.OwningProcess).ProcessName }
```

---

## 5. 发布与更新注意事项（数据别被覆盖）

`dotnet publish` 到站点目录时，以下内容**不能被覆盖/清空**：

| 路径 | 原因 |
|---|---|
| `storage\*.db` + `-shm`/`-wal` | 事件库/任务库（`EventStore:Provider`、`TaskQueue:Provider` 默认 Sqlite，路径键 `EventStore:SqlitePath` / `TaskQueue:DatabasePath`）。覆盖即丢历史事件；覆盖前先停应用池，避免 WAL 未合并 |
| `logs\` | 运行日志、`acs-push\spool`（未转发事件）、SDK 日志 |
| `IsapiDigest\PictureDirectory` | 抓拍图 |
| `appsettings.json` | 现场地址/端口/设备清单都改在这里；**发布前先备份**，发布后比对差异 |
| `ISUPDemo.xml` | 报警/图片/NTP/DAS 参数来源，csproj 已设 `CopyToOutputDirectory=Always`，注意别用旧版覆盖现场已改的版本 |

其它：

- SDK 原生库（`HCISUPCMS.dll` / `HCISUPAlarm.dll` / `HCISUPSS.dll` / `HCISUPStream.dll` 及 `HCNetUtils.dll`、`libeay32.dll`、`ssleay32.dll`）不参与编译引用，靠 `Interop/NativeLibraryResolver` 在运行时定位；
  `appsettings.json` 的 `ISUP:NativeDirectory` 当前为空 → 走"自动查找含 `HCISUPCMS.dll`+`HCISUPAlarm.dll` 的目录"，当前站点目录内已随发布带上，**换机器/换目录时要保证这堆 dll 与 exe 同目录或在 `NativeDirectory` 指明**。
- `web.config` 会随发布覆盖站点上的同名文件，**注意两份文件的差异**：
  源码里 `stdoutLogEnabled="false"`（**不再生成** `logs\stdout_<UTC时间>_<pid>.log`：进程日志统一由 NLog 写 `logs\log\<yyyy-MM-dd>\app.log`；
  确需抓 ANCM 级输出时才改回 `true` 并补 `stdoutLogFile=".\logs\stdout"`）；
  源码已含 `applicationInitialization`（预热）与 `shutdownTimeLimit="60"`，覆盖过去是期望行为——但**别用旧备份把这两段覆盖回去**。
- 覆盖 dll 会触发应用池回收 → SDK 断连重建，尽量安排在低峰。
- 站点目录是 git 仓库同名目录（`*.git`），注意别让发布产物和源码仓库混在一起。

---

## 6. 文件系统权限

- 现状：`E:\` 根对 `Authenticated Users` 给了 **Modify**，应用池身份借道继承拿到写权限 → 能跑，但**权限过宽**。
- 建议改为显式最小授权（把 `<池名>` 换成实际应用池名）：

```powershell
$site = 'E:\ISUPSDK.AccessControl.WebApi'
$id   = "IIS AppPool\ISUPSDK.AccessControl.WebApi.git"
foreach ($p in @($site, "$site\logs", "$site\storage", "$site\IsapiDigest")) {
  icacls $p /grant "${id}:(OI)(CI)M" /T | Out-Null
}
```

- `appsettings.json` 里若仍指向旧控制台的绝对路径（如 `E:\StorageServer\`、`E:\Photo\`），应用池身份同样需要这些目录的写权限，否则图片/人员照片相关功能会静默失败。
- 原生库目录需要「读取和执行」，不需要写。

---

## 7. 配置项红线（`appsettings.json`）

| 配置 | 当前值 | 注意 |
|---|---|---|
| `EventStore:Provider` / `TaskQueue:Provider` | `Sqlite` | 生产建议 SQL Server/MySQL；SQLite 与站点目录同盘、单写入者，发布/备份都要成对处理 |
| 启动补列（`EnsureCreated` 后补 `AcsTasks.PayloadFormat`、`AcsEvents.PictureUrl`） | 无条件执行、异常被吞 | 列已存在时会打出 `Failed executing DbCommand ... ALTER TABLE` 的 fail 日志（**属已知噪音**，服务会继续跑）；不要把其它 fail 也当成噪音忽略。`EventStore:AutoCreate=false` 时需先人工执行 `docs/event-store-*.sql` |
| `Cors:AllowedOrigins` | `["*"]` | 生产收紧到具体来源 |
| `ACSHttpPush:Enabled` | `false` | 开启后会多监听 `8083`，同样受第 4 节单实例约束 |
| `IsapiDigest:PollIntervalSeconds` | 60 | 设备 HTTP 直连轮询，进程停就停；`PictureUrlPrefix` 是写死的主机地址，换机器要改 |
| `IsapiDigest:Devices[].Password` | 明文 | 至少限制 `appsettings.json` 的读权限 |
| `Settings:InitPort` | 7660 | 改端口要同步改设备/server 配置与防火墙 |

Swagger 无环境开关（`IsupApiModule` 里无条件 `UseSwagger`，根路径 `/` 还会 302 到 `/swagger`），**对外网暴露时用 IIS 层做限制或加反向代理鉴权**。

---

## 8. 防火墙

入站需放通：`7823`（API）、`7660`（设备注册，设备侧必须能访问）、`8081`（ESS 图片存储，按 ISUP 部署要求放通）。

---

## 9. 日志与排障入口

日志框架是 **NLog**（站点目录下的 `nlog.config`；改完需重启进程/回收应用池生效，NLog 6 不再内置配置文件监视）：

| 日志 | 位置 | 用途 |
|---|---|---|
| 宿主日志 | `logs\log\<yyyy-MM-dd>\app.log` | 启动/请求/EF Core/SDK 回调，按天一个文件（NLog 汇总） |
| 业务日志 | `logs\log\<类名>\<yyyy-MM-dd>\<HH>.log` | 原 `Common.WriteFile*`：一个类一个目录、每小时一个文件 |
| SDK 原生日志 | `logs\isupsdk\*.log` | 排查 SDK 层失败（`ISUP:SdkLogDirectory`，不受 NLog 管辖） |
| 未转发事件 | `logs\acs-push\spool\acs-events-YYYYMMDD.jsonl` | 转发失败/进程中断时的兜底落盘 |
| IIS 访问日志 | `C:\inetpub\logs\LogFiles\W3SVC4`（站点 id=4） | 判断"多久没有请求" |

`logs\stdout_*.log` 已不再产生（`web.config` 的 `stdoutLogEnabled=false`）；排查冷启动/回收改看 `app.log`
里的 `Application started` 与 w3wp 的创建时间：

```powershell
# 每次进程启动都会在 app.log 里留下一条 "Application started"
Get-ChildItem 'E:\ISUPSDK.AccessControl.WebApi\logs\log' -Recurse -Filter app.log |
  Select-String -Pattern 'Application started' |
  Sort-Object Line | Select-Object -Last 5 Line
(Get-CimInstance Win32_Process -Filter "Name='w3wp.exe'").CreationDate
```

---

## 10. 配置落地（管理员 PowerShell）

### 10.1 用脚本（推荐，可反复执行）

```powershell
cd 'C:\Users\Admin\Desktop\.NET Core Hik\ISUPSDK.AccessControl'

# 只读核对：逐项打印 PASS/FAIL，并输出 w3wp PID / 7660/8081 归属 / /health / web.config 现状
.\iis-apppool.ps1

# 应用修改（会触发一次回收，SDK 短暂断连）
.\iis-apppool.ps1 -Apply

# 应用修改并立刻回收，回收后打印 /health
.\iis-apppool.ps1 -Apply -Restart

# 改每日回收时间（默认 00:00:00）
.\iis-apppool.ps1 -Apply -RecycleTime 03:30:00
```

脚本会设置：`idleTimeout=00:00:00`、`startMode=AlwaysRunning`、站点 `preloadEnabled=true`、
`periodicRestart.time=00:00:00`、`schedule=每日 00:00:00`、`disallowOverlappingRotation=true`；
并核对 `maxProcesses` / `managedPipelineMode` / `enable32BitAppOnWin64` / web.config 预热 / 端口归属 / `/health`。
读写分离：**默认只读**，只有 `-Apply` 才动配置。

### 10.2 等价的手工命令

```powershell
Import-Module WebAdministration
$pool = 'ISUPSDK.AccessControl.WebApi.git'
$site = 'ISUPSDK.AccessControl.WebApi.git'

# 1) 关闭空闲超时（根因）
Set-ItemProperty "IIS:\AppPools\$pool" -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

# 2) 常驻 + 预热
Set-ItemProperty "IIS:\AppPools\$pool" -Name startMode -Value 'AlwaysRunning'
Set-ItemProperty "IIS:\Sites\$site"   -Name applicationDefaults.preloadEnabled -Value $True

# 3) 去掉「固定间隔」回收，改成每天 00:00 回收一次
Set-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Clear-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.schedule -ErrorAction SilentlyContinue
Set-ItemProperty "IIS:\AppPools\$pool" -Name recycling.periodicRestart.schedule -Value @{value='00:00:00'}

# 4) 禁用重叠回收（避免新进程抢不到 7660/8081）
Set-ItemProperty "IIS:\AppPools\$pool" -Name recycling.disallowOverlappingRotation -Value $true

# 5) 生效
Restart-WebAppPool -Name $pool
```

核对结果：

```powershell
$p = Get-Item "IIS:\AppPools\$pool"
$p.processModel | Select-Object idleTimeout,idleTimeoutAction,maxProcesses
$p.recycling.periodicRestart | Select-Object time,@{n='schedule';e={$_.schedule}}
$p.startMode
(Get-Item "IIS:\Sites\$site").applicationDefaults.preloadEnabled
```

验收（改完之后）：

1. `Invoke-RestMethod http://127.0.0.1:7823/health` 正常；
2. `netstat -ano | findstr :7660` 的 PID 指向 w3wp，且**保持同一 PID 超过 40 分钟**（跨越原 20 分钟门槛）；
3. 期间 `logs\log\<yyyy-MM-dd>\app.log` 里不新增 `Application started`（进程没被回收），也不产生 `logs\stdout_*.log`；
4. 设备侧在线状态不掉线。

### 10.3 操作风险（2026-10-23 本机实测，务必看）

- **任何对 `applicationHost.config` 的写入都会让本机应用池回收**，哪怕只是新建/删除一个无关的测试站点。
  实测：16:48 创建/删除临时站点时，本服务 w3wp 被 WAS 回收（System 日志 `WAS` 事件 **5078**「ISAPI reported an unhealthy condition」），
  PID `16060 → 8668`；同一时段站点还被置为 `Stopped`、`serverAutoStart` 变成 `false`（已手工改回 `true`）。
  影响：设备掉线、SDK 重新注册，约 3~4 分钟才重新在线。
- 所以：**改 IIS 配置（含本脚本 `-Apply`）安排在业务低峰**，改完立刻核对：
  `site=Started` / `pool=Started` / `serverAutoStart=true` / `7660`+`8081` 由 w3wp 持有 / `/health=healthy` 且设备重新上线。
- `iis-apppool.ps1` **不加 `-Apply` 就是只读**，随时可以跑；
  查"是不是又被回收了"看 System 日志里的 `WAS 5078 / 5009` 事件 + `logs\log\<yyyy-MM-dd>\app.log` 是否新增 `Application started`。

---

## 11. 脚本（仓库根 `ISUPSDK.AccessControl\`）

| 脚本 | 状态 | 用途 |
|---|---|---|
| `iis-apppool.ps1` | **已提供** | 应用池配置/核对：空闲超时、AlwaysRunning、预热、每日 00:00 回收、禁用重叠回收；只读/`-Apply` 两种模式，附带 w3wp、端口、/health、web.config 现状 |
| `run.ps1` | 已提供 | 本地/自宿主启停（控制台方式），**不要和 IIS 站点同时跑**（抢 7660/8081） |
| `deploy-iis.ps1` | 待写 | 发布到 `E:\...` + 备份 `appsettings.json`/`storage` + 复制 web.config + 应用池回收 + 健康检查 |
| `apppool-alive.ps1` | 待写 | 应用池/健康探活，必要时自动启动应用池（第二道保险，不替代第 10 节配置） |
