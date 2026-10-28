using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Services;
using ISUPSDK.AccessControl.WebApi.Services.Logging;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 人员下发队列执行器（由 <c>Customize/PersonOperationCronJob</c> 的两个定时任务分别触发）。
///
/// 两个阶段<b>各自独立运行</b>（任务要等 AcsTaskWorker 真正下发到设备，不能和入队挤在同一轮）：
/// <list type="number">
/// <item><b>入队</b>（<see cref="PublishAsync"/>）：取 <see cref="PersonOperationState.Pending"/> 的任务，
/// 把平台数据转成"人员 → 卡 → 人脸"的报文，<b>进程内</b>入队为一条
/// <see cref="AcsTaskOperation.PersonIssue"/>（删除则是 <see cref="AcsTaskOperation.PersonIssueDelete"/>）任务，
/// 拿到的任务 ID 存回 <see cref="PersonOperationTask.TaskIds"/>；</item>
/// <item><b>收结果</b>（<see cref="CollectAsync"/>）：取 <see cref="PersonOperationState.Issuing"/> 的任务，
/// 直接读任务库（不经控制器）拿到最终状态，回传平台（ReturnDataUrl / ReturnErrorUrl 或 PutSyncPersonData）。</item>
/// </list>
/// 设备要求"先有人员，才能下发卡片和人脸"，所以三步是同一条任务内顺序执行的，
/// 任一步失败即整条人员记录判为失败（与原来的同步下发口径一致）。
/// </summary>
public sealed class PersonOperationExecutor
{
    private readonly PersonOperationTaskGateway _gateway;
    private readonly IsupCmsService _cms;
    private readonly IsupIsapiService _accessControl;
    private readonly IsupSdkRuntime _runtime;
    private readonly ILogger<PersonOperationExecutor> _logger;

    /// <summary>入队阶段的防重入锁（入队与删除兜底互斥：两者都会改队列任务状态）。</summary>
    private readonly SemaphoreSlim _publishGate = new(1, 1);

    /// <summary>收结果阶段的防重入锁（与入队阶段互不影响，可同时运行）。</summary>
    private readonly SemaphoreSlim _collectGate = new(1, 1);

    public PersonOperationExecutor(
        PersonOperationTaskGateway gateway,
        IsupCmsService cms,
        IsupIsapiService accessControl,
        IsupSdkRuntime runtime,
        ILogger<PersonOperationExecutor> logger)
    {
        _gateway = gateway;
        _cms = cms;
        _accessControl = accessControl;
        _runtime = runtime;
        _logger = logger;
    }

    private Settings AppSettings => _runtime.CurrentSettings;

    /// <summary>
    /// 定时任务入口（阶段一）：把待下发的人员记录入队为一条 <c>AcsTask</c>（进程内直接入队，
    /// 不经过控制器），拿到任务 ID 就返回，不等设备执行——结果由另一路定时任务 <see cref="CollectAsync"/> 收。
    /// </summary>
    public async Task PublishAsync(CancellationToken ct = default)
    {
        if (!PersonOperationQueue.Enabled)
        {
            return;
        }

        if (!await _publishGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            WriteLog("上一轮人员入队任务尚未结束，本轮跳过");
            return;
        }

        try
        {
            await PublishPendingAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "人员入队任务执行异常。");
            WriteLog($"人员入队任务执行异常：{ex}");
        }
        finally
        {
            _publishGate.Release();
        }
    }

    /// <summary>
    /// 定时任务入口（阶段二）：读任务库拿最终状态，结束后回传平台并清理过期任务。
    /// 独立于入队阶段运行，任务下发到设备要多久都不会挡住新的入队。
    /// </summary>
    public async Task CollectAsync(CancellationToken ct = default)
    {
        if (!PersonOperationQueue.Enabled)
        {
            return;
        }

        if (!await _collectGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            WriteLog("上一轮人员下发结果收集尚未结束，本轮跳过");
            return;
        }

        try
        {
            await CollectIssuingAsync(ct).ConfigureAwait(false);
            PersonOperationQueue.CleanFinished(PersonOperationQueue.Settings.RetentionDays);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "人员下发结果收集异常。");
            WriteLog($"人员下发结果收集异常：{ex}");
        }
        finally
        {
            _collectGate.Release();
        }
    }

    #region 阶段一：进程内入队（不经过控制器）

    private async Task PublishPendingAsync(CancellationToken ct)
    {
        var settings = PersonOperationQueue.Settings;
        var tasks = PersonOperationQueue.DequeuePending(settings.BatchSize, settings.MinDelaySeconds);
        if (tasks.Count == 0)
        {
            return;
        }

        WriteLog($"本轮取出待下发人员任务 {tasks.Count} 条");
        var interval = Math.Max(settings.ExecuteIntervalMs, 0);

        foreach (var task in tasks)
        {
            ct.ThrowIfCancellationRequested();

            if (!IsDeviceOnline(task.MachineSN))
            {
                // 设备不在线：任务保留在队列中，等设备上线后的下一轮继续
                task.LastError = "设备未在线，等待设备上线后继续下发";
                PersonOperationQueue.SaveTaskState(task);
                WriteLog($"设备[{task.MachineSN}]未在线，任务 Id={task.Id} 保留待执行");
                continue;
            }

            task.State = (int)PersonOperationState.Running;
            task.AttemptCount += 1;
            task.ExecuteTime = DateTime.Now;
            PersonOperationQueue.SaveTaskState(task);

            var (success, error, taskIds) = await PublishTaskAsync(task, ct).ConfigureAwait(false);

            if (success)
            {
                task.TaskIds = PersonOperationQueue.SerializeTaskIds(taskIds);
                task.State = (int)PersonOperationState.Issuing;
                task.LastError = null;
                PersonOperationQueue.SaveTaskState(task);
                WriteLog($"任务 Id={task.Id}（设备[{task.MachineSN}]）已入队下发任务 {taskIds[0]}");
            }
            else
            {
                await FailAsync(task, error, ct).ConfigureAwait(false);
            }

            if (interval > 0)
            {
                await Task.Delay(interval, ct).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// 入队：把一条队列记录转成一条 <c>AcsTask</c>（PersonIssue / PersonIssueDelete），
    /// 进程内直接调任务服务，不经过控制器。
    /// </summary>
    private async Task<(bool Success, string Error, List<string> TaskIds)> PublishTaskAsync(
        PersonOperationTask task,
        CancellationToken ct)
    {
        var taskIds = new List<string>();
        PersonIssueBuildResult? build;

        try
        {
            build = await PersonOperationIssueBuilder.BuildAsync(task, AppSettings, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WriteLog($"任务 Id={task.Id} 组装下发载荷异常：{ex}");
            return (false, $"组装下发载荷失败：{ex.Message}", taskIds);
        }

        if (build is null)
        {
            return (false, "人员数据反序列化失败", taskIds);
        }

        var result = await _gateway
            .EnqueueIssueAsync(task.MachineSN, build.Operation, build.Payload, TimeoutMs, build.Description, ct)
            .ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.TaskId))
        {
            WriteLog($"任务 Id={task.Id} 入队失败：{result.Error}");
            return (false, $"入队失败：{result.Error}", taskIds);
        }

        taskIds.Add(result.TaskId!);
        return (true, string.Empty, taskIds);
    }

    /// <summary>单步透传超时（毫秒）。</summary>
    private static uint TimeoutMs => 15000;

    /// <summary>任务判失败：写状态、回传平台、回写删除兜底记录。</summary>
    private async Task FailAsync(PersonOperationTask task, string error, CancellationToken ct)
    {
        task.State = (int)PersonOperationState.Failed;
        task.LastError = Truncate(error, 1000);
        task.FinishTime = DateTime.Now;
        PersonOperationQueue.SaveTaskState(task);

        if (task.OperationType == (int)PersonOperationType.Delete)
        {
            PersonOperationQueue.CompleteDeleteVerify(task.Id, false, error);
        }

        if (PersonOperationQueue.Settings.CallbackEnabled)
        {
            try
            {
                await CallbackDetailAsync(task, false, error, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                WriteLog($"任务 Id={task.Id} 回传平台异常：{ex}");
            }
        }
    }

    #endregion

    #region 阶段二：收任务结果

    private async Task CollectIssuingAsync(CancellationToken ct)
    {
        var settings = PersonOperationQueue.Settings;
        var tasks = PersonOperationQueue.DequeueByState(PersonOperationState.Issuing, settings.BatchSize);
        if (tasks.Count == 0)
        {
            return;
        }

        // 认证规则流程（SyncPersonData）：本批次结果收集起来，一次请求带多条回传
        var syncResultList = new List<PutSyncPersonData>();

        try
        {
            foreach (var task in tasks)
            {
                ct.ThrowIfCancellationRequested();

                var taskIds = PersonOperationQueue.ParseTaskIds(task.TaskIds);
                if (taskIds.Count == 0)
                {
                    await FailAsync(task, "没有可查询的下发任务 ID", ct).ConfigureAwait(false);
                    continue;
                }

                var completed = true;
                var failed = false;
                var error = string.Empty;
                var waitingStatus = string.Empty;

                foreach (var taskId in taskIds)
                {
                    var status = await _gateway.GetTaskAsync(taskId, ct).ConfigureAwait(false);

                    // 来源校验：AcsTasks 里还混着调用方直接调控制器创建的任务，
                    // 队列表只应指向本队列（Source=person-operation）创建的任务。
                    if (status is not null && !status.BelongsToPersonOperation)
                    {
                        failed = true;
                        error = $"下发任务 {taskId} 的来源是 {status.Source}，不是人员下发队列创建的任务";
                        WriteLog($"任务 Id={task.Id}：{error}");
                        break;
                    }

                    // 只有 Succeeded / Failed（终态）才回传平台：
                    // Pending / Running 说明设备还没执行完；Canceled 表示任务被取消、不会再产生结果，
                    // 这两类都先不回传，超过 TaskTimeoutMinutes 后按失败回传。
                    if (status is null || !status.IsCompleted)
                    {
                        completed = false;
                        waitingStatus = status is null ? "查不到该任务（可能已被清理）" : status.Status;
                        break;
                    }

                    if (!status.IsSucceeded)
                    {
                        failed = true;
                        error = string.IsNullOrWhiteSpace(status.Error)
                            ? $"下发任务 {taskId} 状态 {status.Status}"
                            : status.Error!;
                    }
                }

                if (!completed)
                {
                    // 设备离线时 AcsTask 会一直待执行；超过配置的等待时长按失败处理，避免平台永远收不到结果
                    var waitFrom = task.ExecuteTime ?? task.CreateTime;
                    var timeout = Math.Max(settings.TaskTimeoutMinutes, 1);
                    if (DateTime.Now - waitFrom <= TimeSpan.FromMinutes(timeout))
                    {
                        continue;
                    }

                    await FailAsync(task, $"下发任务超过 {timeout} 分钟仍未给出结果（当前状态：{waitingStatus}）", ct).ConfigureAwait(false);
                    continue;
                }

                task.State = failed
                    ? (int)PersonOperationState.Failed
                    : (int)PersonOperationState.Succeeded;
                task.LastError = failed ? Truncate(error, 1000) : null;
                task.FinishTime = DateTime.Now;
                PersonOperationQueue.SaveTaskState(task);

                if (task.OperationType == (int)PersonOperationType.Delete)
                {
                    PersonOperationQueue.CompleteDeleteVerify(task.Id, !failed, failed ? error : null);
                }

                if (!settings.CallbackEnabled)
                {
                    continue;
                }

                try
                {
                    if (task.Flow == (int)PersonOperationFlow.Detail)
                    {
                        await CallbackDetailAsync(task, !failed, error, ct).ConfigureAwait(false);
                    }
                    else
                    {
                        syncResultList.Add(BuildSyncPersonData(task, !failed, error));
                    }
                }
                catch (Exception ex)
                {
                    WriteLog($"任务 Id={task.Id} 回传平台异常：{ex}");
                }
            }
        }
        finally
        {
            // 即使中途异常，也要把已执行的结果一次回传平台
            if (settings.CallbackEnabled && syncResultList.Count > 0)
            {
                try
                {
                    await CallbackSyncPersonDataAsync(syncResultList).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    WriteLog($"认证规则流程回传平台异常（{syncResultList.Count} 条）：{ex}");
                }
            }
        }
    }

    #endregion

    #region 删除兜底

    /// <summary>
    /// 删除兜底定时任务入口：按"设备 + 人员"取最新一条任务，若为删除操作则重新下发一次。
    /// 设备返回成功但人员仍留在机器上的情况由这里兜底，每条任务只兜底一次（记录在 PersonDeleteVerifyLog）。
    /// </summary>
    public async Task RunDeleteVerifyAsync(CancellationToken ct = default)
    {
        if (!PersonOperationQueue.Enabled)
        {
            return;
        }

        var taskSettings = PersonOperationQueue.Settings;
        var verifySettings = taskSettings.DeleteVerify ?? new DeleteVerifySettings();
        if (!verifySettings.Enabled)
        {
            return;
        }

        // 与入队阶段互斥（兜底会把任务重新置为 Pending，和入队抢同一批任务）；
        // 收结果阶段不受影响，可以并行跑
        if (!await _publishGate.WaitAsync(0, ct).ConfigureAwait(false))
        {
            WriteLog("上一轮人员入队任务尚未结束，删除兜底本轮跳过");
            return;
        }

        try
        {
            var candidates = PersonOperationQueue.GetLatestDeleteTasksForVerify(verifySettings.WindowHours, verifySettings.BatchSize);
            if (candidates.Count == 0)
            {
                return;
            }

            var requeued = 0;
            var notExists = 0;
            var checkFailed = 0;

            foreach (var machineGroup in candidates.GroupBy(w => w.MachineSN))
            {
                ct.ThrowIfCancellationRequested();

                if (!IsDeviceOnline(machineGroup.Key))
                {
                    WriteLog($"删除兜底：设备[{machineGroup.Key}]未在线，本轮跳过（下轮再试）");
                    continue;
                }

                var items = new List<(PersonOperationTask Task, string EmployeeNo)>();
                foreach (var task in machineGroup)
                {
                    var employeeNo = BuildEmployeeNo(task);
                    if (string.IsNullOrWhiteSpace(employeeNo))
                    {
                        WriteLog($"删除兜底：任务 Id={task.Id} 组装人员编号失败，本轮跳过");
                        continue;
                    }

                    items.Add((task, employeeNo));
                }

                if (items.Count == 0)
                {
                    continue;
                }

                // 关闭设备校验：直接重新下发删除
                if (!verifySettings.CheckOnDevice)
                {
                    foreach (var (task, employeeNo) in items)
                    {
                        if (RequeueDelete(task, machineGroup.Key, employeeNo))
                        {
                            requeued++;
                        }
                    }

                    continue;
                }

                // 设备单次最多只能查 3 个人员，按设备分组分批查询
                var batchSize = verifySettings.CheckBatchSize;
                if (batchSize <= 0 || batchSize > 3)
                {
                    batchSize = 3;
                }

                foreach (var batch in items.Chunk(batchSize))
                {
                    var employeeNos = batch.Select(w => w.EmployeeNo).ToList();
                    var (queryOk, existing, queryError) = await SearchOnDeviceAsync(machineGroup.Key, employeeNos, ct)
                        .ConfigureAwait(false);
                    if (!queryOk)
                    {
                        // 查不到结果就不下结论，下轮再试
                        checkFailed += batch.Length;
                        WriteLog($"删除兜底：设备[{machineGroup.Key}]一次查询 {batch.Length} 个人员失败，本轮跳过：{queryError}");
                        continue;
                    }

                    foreach (var (task, employeeNo) in batch)
                    {
                        var personText = $"设备[{machineGroup.Key}]人员[{employeeNo}]";
                        if (!existing.Contains(employeeNo, StringComparer.OrdinalIgnoreCase))
                        {
                            // 设备上已经没有这个人员，说明之前的删除确实生效了
                            notExists++;
                            PersonOperationQueue.AddDeleteVerifyNotExists(task,
                                $"删除兜底：{personText} 核对后设备上已不存在，无需重新下发");
                            WriteLog($"删除兜底：{personText} 核对后设备上已不存在，跳过");
                            continue;
                        }

                        if (RequeueDelete(task, machineGroup.Key, employeeNo))
                        {
                            requeued++;
                        }
                    }
                }
            }

            WriteLog($"删除兜底：候选 {candidates.Count} 条，设备上仍存在并重新入队 {requeued} 条，" +
                     $"核对已不存在 {notExists} 条，查询失败 {checkFailed} 条");

            PersonOperationQueue.CleanVerifyLogs(taskSettings.RetentionDays);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            WriteLog($"删除兜底执行异常：{ex}");
        }
        finally
        {
            _publishGate.Release();
        }
    }

    private bool RequeueDelete(PersonOperationTask task, string machineSN, string employeeNo)
    {
        var remark = $"删除兜底：设备[{machineSN}]人员[{employeeNo}] 下发删除成功但设备上仍存在，重新下发一次删除";
        if (!PersonOperationQueue.EnqueueDeleteVerify(task, remark))
        {
            return false;
        }

        WriteLog($"删除兜底：任务 Id={task.Id} 已重新入队（设备[{machineSN}]人员[{employeeNo}]）");
        return true;
    }

    /// <summary>组装设备上的人员编号：幼儿/家长/职工 = 前缀 + webId；认证规则 = 人员类型 + 人员ID。</summary>
    private static string BuildEmployeeNo(PersonOperationTask task)
    {
        try
        {
            if (task.Flow == (int)PersonOperationFlow.Detail)
            {
                var detail = task.Payload.FromJson<Detail>();
                return detail == null ? string.Empty : $"{task.Prefix}{detail.webId}";
            }

            var person = task.Payload.FromJson<SyncPersonDataPage>();
            return person == null ? string.Empty : $"{person.PersonType}{person.PersonId}";
        }
        catch (Exception ex)
        {
            WriteLog($"任务 Id={task.Id} 组装人员编号异常：{ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// 查设备上是否还存在这些人员（进程内直调 ISAPI 的 <c>UserInfo/Search</c>，不经控制器）。
    /// 查询失败返回 <c>Success=false</c>，调用方应下轮再试，不能据此判断"人员已不存在"。
    /// </summary>
    private async Task<(bool Success, List<string> Existing, string Error)> SearchOnDeviceAsync(
        string machineSN,
        IReadOnlyCollection<string> employeeNos,
        CancellationToken ct)
    {
        var existing = new List<string>();
        if (!_cms.TryGetDeviceByDeviceId(machineSN, out var device) || !device.Online)
        {
            return (false, existing, $"设备 {machineSN} 未在线");
        }

        try
        {
            var condition = new IsapiUserInfoSearchCond
            {
                EmployeeNoList = employeeNos.Select(w => new IsapiEmployeeNo { EmployeeNo = w }).ToList(),
                MaxResults = Math.Max(employeeNos.Count, 30),
            };

            var result = await _accessControl
                .SearchUsersAsync(device, condition, TimeoutMs, ct)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return (false, existing, result.Status?.SubStatusCode ?? result.ResponseStatusString ?? "设备查询失败");
            }

            var userInfos = result.Data?.UserInfoSearch?.UserInfo;
            if (userInfos is not null)
            {
                foreach (var item in userInfos)
                {
                    if (!string.IsNullOrWhiteSpace(item.EmployeeNo))
                    {
                        existing.Add(item.EmployeeNo!);
                    }
                }
            }

            return (true, existing, string.Empty);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "查询设备 {DeviceId} 的人员失败。", machineSN);
            return (false, existing, ex.Message);
        }
    }

    #endregion

    #region 回传平台

    /// <summary>SyncInformation 流程回传（ReturnDataUrl / ReturnErrorUrl）。</summary>
    private async Task CallbackDetailAsync(PersonOperationTask task, bool success, string error, CancellationToken ct)
    {
        if (task.Flow != (int)PersonOperationFlow.Detail)
        {
            return;
        }

        var settings = AppSettings;
        var authorizationToken = Common.SetToken(settings.WebCheckCode1 ?? string.Empty);
        if (success)
        {
            var url = Common.GetReturnUrl(settings.ReturnDataUrl, task.MachineSN, task.PersonDataId?.ToString(), task.ReturnDataType);
            await HttpHelper.PutUrlAsync(url, null, authorizationToken).ConfigureAwait(false);
            return;
        }

        var detail = task.Payload.FromJson<Detail>();
        var failList = new List<returnErrorModel>
        {
            new()
            {
                DataId = task.PersonDataId ?? 0,
                Err = string.IsNullOrWhiteSpace(error) ? "下发失败" : error,
                ErrCode = 0,
                AddTime = PersonOperationQueue.ToCallbackAddTime(detail?.AddTime ?? default),
            },
        };
        var failUrl = Common.GetReturnUrl(settings.ReturnErrorUrl, task.MachineSN, null, task.ReturnDataType);
        await HttpHelper.PutUrlAsync(failUrl, failList, authorizationToken).ConfigureAwait(false);
    }

    /// <summary>组装 SyncAuthRuleInformation 流程的回传数据（PutSyncPersonData）。</summary>
    private static PutSyncPersonData BuildSyncPersonData(PersonOperationTask task, bool success, string error)
    {
        var person = task.Payload.FromJson<SyncPersonDataPage>();
        return new PutSyncPersonData
        {
            Id = task.SyncDataId,
            State = success ? SyncPersonDataState.Succeeded : SyncPersonDataState.Failed,
            ErrorNumber = success ? 0 : 1,
            ErrorMessage = success ? string.Empty : Truncate(error, 255),
            // 平台按这两个字段定位记录，原样透传（为空时序列化会自动忽略，不发空串）
            SourceId_List = person?.SourceId_List!,
            SourceId_List1 = person?.SourceId_List1!,
            AddTime = PersonOperationQueue.ToCallbackAddTime(person?.AddTime ?? default),
        };
    }

    /// <summary>SyncAuthRuleInformation 流程回传（Put SyncPersonData，一次请求带多条结果）。</summary>
    private async Task CallbackSyncPersonDataAsync(List<PutSyncPersonData> resultList)
    {
        if (resultList.Count == 0)
        {
            return;
        }

        var settings = AppSettings;
        var authorizationToken = Common.SetToken(settings.WebCheckCode1 ?? string.Empty);
        await HttpHelper.PutUrlAsync(settings.SyncPersonData, resultList, authorizationToken).ConfigureAwait(false);
    }

    #endregion

    private bool IsDeviceOnline(string machineSN)
        => !string.IsNullOrWhiteSpace(machineSN) &&
           _cms.TryGetDeviceByDeviceId(machineSN, out var device) &&
           device.Online;

    private static string Truncate(string? value, int length)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value[..length];
    }

    private static void WriteLog(string content)
    {
        try
        {
            IsupLog.WriteHours("PersonOperationExecutor.txt", true, content);
        }
        catch
        {
            // 日志失败不影响主流程
        }
    }
}
