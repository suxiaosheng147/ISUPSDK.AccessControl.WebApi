# 门禁对接前端（KrAdmin.API · /api/acs/*）

按 `KrAdmin.API.Host\Controllers\AcsManage` 的 REST 接口写的静态页面，风格参考
`ISUPSDK.AccessControl.WebApi\wwwroot\events.html`（单页 + 原生 JS + 中文浅色卡片，无框架、无构建）。

## 目录

```
E:\web\
  index.html            导航首页 + 连通性检查 + 接口总览
  devices.html          设备（9 个接口）
  system.html           服务状态（1）
  persons.html          人员（12）
  cards.html            卡片（10）
  faces.html            人脸（6）+ 人脸库（4）
  doors.html            门禁控制（5）
  access-plans.html     权限计划（15）
  events.html           事件（9）
  alarms.html           报警（6）
  tasks.html            下发任务（4）
  storage.html          图片存储（4）
  assets\app.css        统一样式
  assets\app.js         配置 / 请求封装 / 表格 / 分页 / 下载
```

## 打开方式

任意静态托管即可，例如：

- 双击 `index.html`（`file://`）——**多半会被 CORS 拦**，见下节
- 用 IIS / nginx 把 `E:\web` 发布成站点（推荐，例如 `http://localhost:99`）
- 或直接把整个目录拷到 KrAdmin 的 `wwwroot` 下，用同源访问（最省事，不用改 CORS）

## 必填三项（页面右上角）

| 项 | 说明 |
|---|---|
| API 基址 | KrAdmin.API 地址，如 `https://localhost:5001`（不带结尾斜杠） |
| Token | 登录 KrAdmin 得到的 `access_token`，请求会带 `Authorization: bearer <token>` |
| 环境 | 对应 `RemoteServiceConfig.Environment`（PROD/UAT/TEST/DEV）；留空由服务端按宿主环境判定 |

配置保存在浏览器 `localStorage`（键 `acs.ui.config`），换浏览器要重填。

## ⚠️ 跨域（CORS）

KrAdmin 的 `Startup.ConfigureServices` 里读 `Cors:Origins` 白名单，只有列表里的来源能调：

```json
"Origins": [ "http://localhost", "http://192.168.1.124:99", ... ]
```

所以：

1. 页面从 `http://localhost:99` 打开 → 把 `http://localhost:99` 加进 `Cors:Origins`；
2. 直接双击（`file://`）→ 浏览器发的 `Origin` 是 `null`，白名单里没有就必然失败；
3. 页面报 `无法连接后端 … 跨域是否放通` 时，先看浏览器控制台的 CORS 报错。

## 接口映射（页面按钮 → REST 接口）

所有路径只用名词，动作交给 HTTP 方法：

| 页面 | 主要调用 |
|---|---|
| devices | `GET /api/acs/devices`、`GET /{deviceId}`、`GET /{deviceId}/info`、`DELETE /{deviceId}/session`、`POST /{deviceId}/http-push`、`POST /listeners`、`DELETE /listeners/{handle}`、`POST /{deviceId}/isapi` |
| system | `GET /api/acs/system`、`GET /api/acs/devices/sdk` |
| persons | `GET/POST /api/acs/persons`、`PATCH/PUT/DELETE /{employeeNo}`、`DELETE /api/acs/persons?employeeNos=`、`DELETE /all`、`POST/PATCH /batch`、`GET /count`、`GET /capabilities` |
| cards | `GET/POST /api/acs/cards`、`PATCH/DELETE /{cardNo}`、`DELETE /api/acs/cards?cardNos=`、`DELETE /employees/{employeeNo}`、`DELETE /all`、`POST/PATCH /batch` |
| faces | `GET/POST /api/acs/faces`、`POST /encoded`、`/remote`、`/batch`、`DELETE /{fpId}`；人脸库 `GET/POST/PUT/DELETE /api/acs/face-libraries[/{fdId}]` |
| doors | `GET /api/acs/doors/capabilities`、`/work-status`、`GET /{doorNo}`、`PUT /{doorNo}/state`、`PUT /outputs/{outputId}` |
| access-plans | `GET /api/acs/access-plans/capabilities`、`GET/PUT /{templates|week-plans|holiday-groups|holiday-plans}[/{id}]`、`POST /presets/workday` |
| events | `GET/POST /api/acs/events`、`GET /{id}`、`GET /facets`、`GET /pipeline`、`GET /pictures/{fileName}`、`POST /batch`、`POST /replays`、`POST /reconciliations` |
| alarms | `GET /api/acs/alarms/status`、`GET/DELETE /events`、`GET /events/{sequence}`、`POST /listeners`、`DELETE /listeners/{handle}` |
| tasks | `GET /api/acs/tasks`、`GET /{id}`、`POST /{id}/retries`、`DELETE /{id}` |
| storage | `GET /api/acs/storage`、`GET /files`、`GET /files/content?path=`、`DELETE /files?path=` |

## 分页约定

- 参数：`pageSize` + `pageIndex`（`pageIndex` **从 1 开始**）
- 返回：`PageModel<T>` = `{ RecordCount, PageCount, PageData }`

哪些接口有真翻页、哪些只有 `pageSize`：

| 接口 | 分页 |
|---|---|
| `/api/acs/events` | ✅ 真分页（门禁服务支持 limit + offset，页面按 pageIndex 换算 offset） |
| `/api/acs/persons`、`/cards`、`/faces`、`/access-plans/*` | 换算成设备的 `searchResultPosition + maxResults`（persons/cards/faces 从 0 起算，access-plans 从 1 起算，页面已处理） |
| `/api/acs/tasks`、`/alarms/events`、`/storage/files` | 只有 `pageSize`（门禁服务仅支持 limit），`PageCount` 恒为 1 |

## 已知限制（来自门禁服务/设备，非页面问题）

1. **异步下发**：人员/卡片/人脸的新增、修改、删除返回 `202` 与任务信息，结果要到「下发任务」页查，或按 ID 查。
2. **设备能力差异**：例如当前测试机 `FZ7955795` 不支持 `UserRightPlanTemplate/Search`，权限计划页会返回
   `设备查询失败：Invalid Operation（subStatusCode=notSupport）`——这是设备固件限制。
3. **请求体是 ISAPI 原始 JSON**：人员/卡片/人脸/权限计划的写入结构由设备固件定义，字段随型号变化，
   页面因此用 JSON 文本框（带示例）+ 强类型 DTO 校验在服务端，不逐个字段做表单。
4. **抓拍图 / 存储文件下载**：接口返回二进制且需要 `Authorization` 头，页面用 `fetch` 取 blob 再展示/下载
   （`<img src>` 无法带请求头）。
