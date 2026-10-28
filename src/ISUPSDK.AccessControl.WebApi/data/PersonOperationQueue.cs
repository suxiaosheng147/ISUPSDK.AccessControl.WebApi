using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.Services.Logging;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 人员下发队列：人员的增/改/删先写入数据库（<c>isup_person_queue.db</c>），
/// 再由定时任务（<see cref="PersonOperationExecutor"/>）通过本服务控制器接口下发设备。
/// </summary>
public static class PersonOperationQueue
{
    /// <summary>数据库写入串行锁，避免多设备线程并发写 SQLite 时锁表。</summary>
    private static readonly object DbLock = new();

    private static PersonOperationQueueSettings _settings = new();

    /// <summary>是否已提示过"平台下发来源Id 为空"。</summary>
    private static bool _missingSourceIdWarned;

    /// <summary>是否启用队列模式（false 时调用方走原来的实时下发逻辑）。</summary>
    public static bool Enabled { get; private set; }

    /// <summary>队列配置。</summary>
    public static PersonOperationQueueSettings Settings => _settings;

    /// <summary>关闭队列模式（回退为原来的实时下发）。</summary>
    public static void Disable(string reason)
    {
        Enabled = false;
        WriteLog($"队列模式已关闭：{reason}");
    }

    /// <summary>初始化：绑定配置并创建表结构。</summary>
    public static bool Initialize(Model.Settings settings)
    {
        try
        {
            PersonOperationDatabase.Configure(settings.Database ?? new PersonOperationDatabaseSettings());
            _settings = settings.PersonOperationQueue ?? new PersonOperationQueueSettings();
            Enabled = _settings.Enabled;

            if (Enabled && PersonOperationDatabase.Settings.EnsureCreated)
            {
                PersonOperationDatabase.EnsureCreated();
            }

            if (Enabled)
            {
                // 老库升级：补齐后加的表/列（EnsureCreated 只在库不存在时建表）
                PersonOperationDatabase.EnsureSchema();

                // SQLite 默认切回 Delete 日志模式（正常关闭后只留 .db），历史上残留的 -wal 会在切换时合并回主库
                PersonOperationDatabase.EnsureJournalMode();
            }

            IsupLog.WriteHours("PersonOperationQueue.txt", true,
                $"人员下发队列初始化完成。启用={Enabled}，提供程序={PersonOperationDatabase.Settings.Provider}，" +
                $"连接={PersonOperationDatabase.ResolveConnectionString()}");
            return true;
        }
        catch (Exception ex)
        {
            Enabled = false;
            WriteLog($"人员下发队列初始化失败，已回退为实时下发：{ex}");
            return false;
        }
    }

    #region 入队

    /// <summary>
    /// 入队（SyncInformation 流程，Detail 数据）。
    /// </summary>
    /// <param name="machineSN">设备编码。</param>
    /// <param name="prefix">人员编号前缀（01/02/03）。</param>
    /// <param name="person">人员数据。</param>
    /// <param name="operationType">操作类型。</param>
    /// <param name="returnDataType">回传数据类型（3/4/8）。</param>
    /// <param name="message">入队失败原因。</param>
    public static bool TryEnqueueDetail(string machineSN, string prefix, Detail person,
        PersonOperationType operationType, int returnDataType, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(machineSN) || person == null)
        {
            message = "入队失败：设备或人员数据为空";
            return false;
        }

        try
        {
            var task = new PersonOperationTask
            {
                MachineSN = machineSN,
                RecordKey = BuildRecordKey(machineSN, PersonOperationFlow.Detail, prefix, BuildDetailKey(person), operationType, person.AddTime),
                Prefix = prefix,
                PersonDataId = person.dataId,
                OperationType = (int)operationType,
                Flow = (int)PersonOperationFlow.Detail,
                ReturnDataType = returnDataType,
                Payload = person.ToJson(),
                State = (int)PersonOperationState.Pending,
                CreateTime = DateTime.Now
            };

            if (!SaveTask(task))
            {
                // 已有在途记录（待下发 / 正在入队 / 已入队等设备执行）：本次下发合并进那条记录，不重复入队
                WriteLog($"入队跳过：设备[{machineSN}]人员[{person.dataId}]已有在途记录（{task.RecordKey}）");
            }

            return true;
        }
        catch (Exception ex)
        {
            message = $"入队失败：{ex.Message}";
            WriteLog($"PersonOperationQueue.TryEnqueueDetail 异常：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 入队（SyncAuthRuleInformation 流程，SyncPersonDataPage 数据）。
    /// </summary>
    public static bool TryEnqueueSyncPersonData(string machineSN, SyncPersonDataPage person,
        PersonOperationType operationType, out string message)
    {
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(machineSN) || person == null)
        {
            message = "入队失败：设备或人员数据为空";
            return false;
        }

        try
        {
            // 平台按 SourceId_List / SourceId_List1 定位记录，两者都为空时回传结果无法被平台对应上
            if (string.IsNullOrWhiteSpace(person.SourceId_List) &&
                string.IsNullOrWhiteSpace(person.SourceId_List1) &&
                !_missingSourceIdWarned)
            {
                _missingSourceIdWarned = true;
                WriteLog($"警告：平台下发的人员数据 SourceId_List / SourceId_List1 都为空（Id={person.Id}，PersonType={person.PersonType}，PersonId={person.PersonId}）。" +
                         "回传结果时平台将无法用这两个字段定位记录，请确认平台接口。");
            }

            var task = new PersonOperationTask
            {
                MachineSN = machineSN,
                RecordKey = $"{machineSN}|{BuildSyncPersonKey(person, operationType)}",
                PersonDataId = person.PersonId,
                SyncDataId = person.Id,
                OperationType = (int)operationType,
                Flow = (int)PersonOperationFlow.SyncPersonDataPage,
                Payload = person.ToJson(),
                State = (int)PersonOperationState.Pending,
                CreateTime = DateTime.Now
            };

            if (!SaveTask(task))
            {
                // 已有在途记录（待下发 / 正在入队 / 已入队等设备执行）：本次下发合并进那条记录，不重复入队
                WriteLog($"入队跳过：设备[{machineSN}]人员[{person.PersonType}{person.PersonId}]已有在途记录（{task.RecordKey}）");
            }

            return true;
        }
        catch (Exception ex)
        {
            message = $"入队失败：{ex.Message}";
            WriteLog($"PersonOperationQueue.TryEnqueueSyncPersonData 异常：{ex}");
            return false;
        }
    }

    /// <summary>
    /// 同一人员同一设备同一流程只要还有"在途"记录（<see cref="PersonOperationState.Pending"/> /
    /// <see cref="PersonOperationState.Running"/> / <see cref="PersonOperationState.Issuing"/>），
    /// 就不再新增、也不覆盖已有记录 —— 在途记录可能已经被执行器取走或正等设备执行，
    /// 改它的载荷会让前后不一致；等这条走完，平台下次下发的变更会重新入队。
    /// </summary>
    /// <returns>true = 新增了一条队列记录；false = 已有在途记录，本次跳过。</returns>
    private static bool SaveTask(PersonOperationTask task)
    {
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();

            var exists = db.PersonOperationTasks.Any(w =>
                w.RecordKey == task.RecordKey &&
                ActiveStates.Contains(w.State));

            if (exists)
            {
                return false;
            }

            db.PersonOperationTasks.Add(task);
            db.SaveChanges();
            return true;
        }
    }

    /// <summary>"在途"状态：还没结束的下发记录（用于入队去重）。</summary>
    private static readonly int[] ActiveStates =
    [
        (int)PersonOperationState.Pending,
        (int)PersonOperationState.Running,
        (int)PersonOperationState.Issuing,
    ];

    /// <summary>
    /// 人员编号前缀|人员标识|流程|操作类型|添加时间 组成去重键（前缀为空时省略），
    /// 与认证规则流程的键结构保持一致。
    /// </summary>
    private static string BuildRecordKey(string machineSN, PersonOperationFlow flow, string? prefix, string key,
        PersonOperationType operationType, DateTime addTime)
    {
        var timeToken = FormatAddTime(addTime);
        return string.IsNullOrWhiteSpace(prefix)
            ? $"{machineSN}|{key}|{(int)flow}|{(int)operationType}|{timeToken}"
            : $"{machineSN}|{prefix}|{key}|{(int)flow}|{(int)operationType}|{timeToken}";
    }

    /// <summary>添加时间转成去重键里用的字符串；平台没给时间时用 "-" 占位（此时同人员同操作仍会合并）。</summary>
    private static string FormatAddTime(DateTime addTime)
    {
        return addTime == default
            ? "-"
            : addTime.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>回传用的添加时间：平台没给（默认值）时返回 null，序列化时该字段会被忽略。</summary>
    public static DateTime? ToCallbackAddTime(DateTime addTime)
    {
        return addTime == default ? null : addTime;
    }

    /// <summary>
    /// 认证规则人员的去重键：人员类型|人员ID|流程|操作类型|添加时间。
    /// PersonId 取不到时依次退回 SourceId_List / SourceId_List1（平台认的定位字段），再退回平台记录 Id，
    /// 全都没有则生成随机键单独成任务（宁可重复也不丢数据）。
    /// </summary>
    private static string BuildSyncPersonKey(SyncPersonDataPage person, PersonOperationType operationType)
    {
        string personKey;
        if (person.PersonId.HasValue && person.PersonId.Value > 0)
        {
            personKey = $"{person.PersonType}|{person.PersonId.Value}";
        }
        else if (!string.IsNullOrWhiteSpace(person.SourceId_List))
        {
            personKey = $"{person.PersonType}|src:{person.SourceId_List}";
        }
        else if (!string.IsNullOrWhiteSpace(person.SourceId_List1))
        {
            personKey = $"{person.PersonType}|src1:{person.SourceId_List1}";
        }
        else if (person.Id != Guid.Empty)
        {
            personKey = person.Id.ToString();
        }
        else
        {
            personKey = Guid.NewGuid().ToString("N");
        }

        return $"{personKey}|{(int)PersonOperationFlow.SyncPersonDataPage}|{(int)operationType}|{FormatAddTime(person.AddTime)}";
    }

    /// <summary>幼儿/家长/职工人员的去重键：优先 dataId，其次人员编号 webId / code，都取不到时生成随机键。</summary>
    private static string BuildDetailKey(Detail person)
    {
        if (person.dataId > 0)
        {
            return person.dataId.ToString();
        }

        if (person.webId > 0)
        {
            return $"web:{person.webId}";
        }

        if (!string.IsNullOrWhiteSpace(person.code))
        {
            return $"code:{person.code}";
        }

        return Guid.NewGuid().ToString("N");
    }

    #endregion

    #region 读取/更新（供定时任务使用）

    /// <summary>
    /// 取出待执行任务。
    /// </summary>
    /// <param name="batchSize">每批最多多少条。</param>
    /// <param name="minDelaySeconds">入队后至少等待多少秒才执行，保证"下发中"回传先发出。</param>
    public static List<PersonOperationTask> DequeuePending(int batchSize, int minDelaySeconds = 0)
    {
        var maxCreateTime = DateTime.Now.AddSeconds(-Math.Max(0, minDelaySeconds));
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            return db.PersonOperationTasks
                .Where(w => w.State == (int)PersonOperationState.Pending && w.CreateTime <= maxCreateTime)
                .OrderBy(w => w.Id)
                .Take(batchSize <= 0 ? 50 : batchSize)
                .ToList();
        }
    }

    /// <summary>
    /// 取出已入队（Issuing）的任务，用于轮询控制器任务（AcsTask）的执行结果。
    /// </summary>
    /// <param name="batchSize">每批最多多少条。</param>
    /// <param name="state">任务状态，默认 <see cref="PersonOperationState.Issuing"/>。</param>
    public static List<PersonOperationTask> DequeueByState(PersonOperationState state, int batchSize)
    {
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            return db.PersonOperationTasks
                .Where(w => w.State == (int)state)
                .OrderBy(w => w.Id)
                .Take(batchSize <= 0 ? 50 : batchSize)
                .ToList();
        }
    }

    /// <summary>按主键取一条任务。</summary>
    public static PersonOperationTask? Get(long id)
    {
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            return db.PersonOperationTasks.FirstOrDefault(w => w.Id == id);
        }
    }

    /// <summary>更新任务状态。</summary>
    public static void SaveTaskState(PersonOperationTask task)
    {
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            db.PersonOperationTasks.Update(task);
            db.SaveChanges();
        }
    }

    #region 删除兜底

    /// <summary>
    /// 取出需要做删除兜底的候选任务：
    /// 只看最近 windowHours 小时内下发成功的删除任务，按"设备 + 人员"分组取 CreateTime 最新的一条；
    /// 已被兜底过（PersonDeleteVerifyLog 里有记录）的不再处理。
    /// </summary>
    /// <param name="windowHours">只看最近多少小时内完成的任务。</param>
    /// <param name="batchSize">每轮最多处理多少名人员。</param>
    public static List<PersonOperationTask> GetLatestDeleteTasksForVerify(int windowHours, int batchSize)
    {
        var from = DateTime.Now.AddHours(-Math.Max(1, windowHours));
        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();

            // 窗口内下发成功的任务，按人员和设备分组后只留最新一条
            var latestList = db.PersonOperationTasks
                .Where(w => w.State == (int)PersonOperationState.Succeeded
                            && w.FinishTime != null && w.FinishTime >= from)
                .OrderByDescending(w => w.CreateTime).ThenByDescending(w => w.Id)
                .ToList()
                .GroupBy(w => new { w.MachineSN, w.Flow, w.Prefix, w.PersonDataId })
                .Select(g => g.First())
                .Where(w => w.OperationType == (int)PersonOperationType.Delete)
                .ToList();

            if (latestList.Count == 0)
            {
                return latestList;
            }

            // 已经兜底过的任务不再处理
            var verifiedTaskIds = db.PersonDeleteVerifyLogs
                .Select(w => w.TaskId)
                .ToList()
                .ToHashSet();

            return latestList
                .Where(w => !verifiedTaskIds.Contains(w.Id))
                .OrderBy(w => w.Id)
                .Take(batchSize <= 0 ? 50 : batchSize)
                .ToList();
        }
    }

    /// <summary>登记一条删除兜底记录并把任务重新放回待执行队列（同一事务）。</summary>
    public static bool EnqueueDeleteVerify(PersonOperationTask task, string remark)
    {
        if (task == null)
        {
            return false;
        }

        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            var now = DateTime.Now;

            db.PersonDeleteVerifyLogs.Add(new PersonDeleteVerifyLog
            {
                TaskId = task.Id,
                MachineSN = task.MachineSN,
                Flow = task.Flow,
                Prefix = task.Prefix,
                PersonDataId = task.PersonDataId,
                SyncDataId = task.SyncDataId,
                RecordKey = task.RecordKey,
                PersonAddTime = ReadPersonAddTime(task),
                AttemptCount = task.AttemptCount,
                CreateTime = now,
                State = (int)DeleteVerifyState.Waiting,
                Remark = remark
            });

            task.State = (int)PersonOperationState.Pending;
            task.LastError = remark;
            task.FinishTime = null;
            task.TaskIds = null;
            db.PersonOperationTasks.Update(task);

            db.SaveChanges();
            return true;
        }
    }

    /// <summary>登记一条"核对确认设备上已不存在该人员"的兜底记录（不改任务状态，也不会再被重复处理）。</summary>
    public static bool AddDeleteVerifyNotExists(PersonOperationTask task, string remark)
    {
        if (task == null)
        {
            return false;
        }

        lock (DbLock)
        {
            using var db = PersonOperationDatabase.CreateContext();
            var now = DateTime.Now;
            db.PersonDeleteVerifyLogs.Add(new PersonDeleteVerifyLog
            {
                TaskId = task.Id,
                MachineSN = task.MachineSN,
                Flow = task.Flow,
                Prefix = task.Prefix,
                PersonDataId = task.PersonDataId,
                SyncDataId = task.SyncDataId,
                RecordKey = task.RecordKey,
                PersonAddTime = ReadPersonAddTime(task),
                AttemptCount = task.AttemptCount,
                CreateTime = now,
                State = (int)DeleteVerifyState.NotExists,
                UpdateTime = now,
                Remark = remark
            });
            db.SaveChanges();
            return true;
        }
    }

    /// <summary>兜底任务下发完成后回写兜底记录的结果。</summary>
    public static void CompleteDeleteVerify(long taskId, bool success, string? error)
    {
        try
        {
            lock (DbLock)
            {
                using var db = PersonOperationDatabase.CreateContext();
                var logs = db.PersonDeleteVerifyLogs
                    .Where(w => w.TaskId == taskId && w.State == (int)DeleteVerifyState.Waiting)
                    .ToList();
                if (logs.Count == 0)
                {
                    return;
                }

                foreach (var log in logs)
                {
                    log.State = success ? (int)DeleteVerifyState.Succeeded : (int)DeleteVerifyState.Failed;
                    log.UpdateTime = DateTime.Now;
                    if (!string.IsNullOrWhiteSpace(error))
                    {
                        log.Remark = TruncateText(error!, 1000);
                    }
                }

                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            WriteLog($"回写删除兜底记录失败：{ex}");
        }
    }

    /// <summary>清理过期的删除兜底记录。</summary>
    public static int CleanVerifyLogs(int retentionDays)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        try
        {
            var expire = DateTime.Now.AddDays(-retentionDays);
            lock (DbLock)
            {
                using var db = PersonOperationDatabase.CreateContext();
                var old = db.PersonDeleteVerifyLogs
                    .Where(w => w.State != (int)DeleteVerifyState.Waiting && w.CreateTime < expire)
                    .ToList();
                if (old.Count == 0)
                {
                    return 0;
                }

                db.PersonDeleteVerifyLogs.RemoveRange(old);
                db.SaveChanges();
                return old.Count;
            }
        }
        catch (Exception ex)
        {
            WriteLog($"清理删除兜底记录失败：{ex}");
            return 0;
        }
    }

    /// <summary>从任务 payload 里取平台下发的添加时间。</summary>
    private static DateTime? ReadPersonAddTime(PersonOperationTask task)
    {
        try
        {
            if (task.Flow == (int)PersonOperationFlow.Detail)
            {
                return ToCallbackAddTime(task.Payload.FromJson<Detail>()?.AddTime ?? default);
            }

            return ToCallbackAddTime(task.Payload.FromJson<SyncPersonDataPage>()?.AddTime ?? default);
        }
        catch
        {
            return null;
        }
    }

    private static string TruncateText(string value, int length)
    {
        return value.Length <= length ? value : value[..length];
    }

    #endregion

    /// <summary>清理过期的已完成任务。</summary>
    public static int CleanFinished(int retentionDays)
    {
        if (retentionDays <= 0)
        {
            return 0;
        }

        try
        {
            var expire = DateTime.Now.AddDays(-retentionDays);
            lock (DbLock)
            {
                using var db = PersonOperationDatabase.CreateContext();
                var old = db.PersonOperationTasks
                    .Where(w => (w.State == (int)PersonOperationState.Succeeded || w.State == (int)PersonOperationState.Failed)
                                && w.FinishTime != null && w.FinishTime < expire)
                    .ToList();
                if (old.Count == 0)
                {
                    return 0;
                }

                db.PersonOperationTasks.RemoveRange(old);
                db.SaveChanges();
                return old.Count;
            }
        }
        catch (Exception ex)
        {
            WriteLog($"PersonOperationQueue.CleanFinished 异常：{ex}");
            return 0;
        }
    }

    #endregion

    /// <summary>任务 Id 列表（JSON 数组）与字符串互转。</summary>
    public static List<string> ParseTaskIds(string? taskIds)
    {
        if (string.IsNullOrWhiteSpace(taskIds))
        {
            return new List<string>();
        }

        try
        {
            return taskIds.FromJson<List<string>>() ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>把任务 Id 列表序列化成 JSON 数组。</summary>
    public static string SerializeTaskIds(IEnumerable<string> taskIds)
    {
        return taskIds.ToList().ToJson();
    }

    private static void WriteLog(string content)
    {
        try
        {
            IsupLog.WriteHours("PersonOperationQueue.txt", true, content);
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
