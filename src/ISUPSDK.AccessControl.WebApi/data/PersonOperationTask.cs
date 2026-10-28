namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>人员操作类型。</summary>
public enum PersonOperationType
{
    /// <summary>新增。</summary>
    Add = 1,

    /// <summary>修改。</summary>
    Update = 2,

    /// <summary>删除。</summary>
    Delete = 3,
}

/// <summary>任务来源流程。</summary>
public enum PersonOperationFlow
{
    /// <summary>SyncInformation（Detail 数据，回传 ReturnDataUrl / ReturnErrorUrl）。</summary>
    Detail = 1,

    /// <summary>SyncAuthRuleInformation（SyncPersonDataPage 数据，回传 SyncPersonData）。</summary>
    SyncPersonDataPage = 2,
}

/// <summary>人员下发状态（回传平台 PutSyncPersonData.State 的取值）。</summary>
public static class SyncPersonDataState
{
    /// <summary>待下发。</summary>
    public const int Pending = 1;

    /// <summary>下发失败。</summary>
    public const int Failed = 2;

    /// <summary>下发成功。</summary>
    public const int Succeeded = 3;

    /// <summary>下发中（已入队，等待定时任务下发设备）。</summary>
    public const int Issuing = 4;
}

/// <summary>
/// 任务状态。
/// <para>
/// <see cref="Pending"/> → <see cref="Running"/>（正在调用控制器入队）→ <see cref="Issuing"/>
/// （控制器已入队，等 <c>AcsTask</c> 执行结束）→ <see cref="Succeeded"/> / <see cref="Failed"/>。
/// </para>
/// </summary>
public enum PersonOperationState
{
    /// <summary>待执行。</summary>
    Pending = 0,

    /// <summary>执行中（正在调用控制器入队）。</summary>
    Running = 1,

    /// <summary>执行成功（设备已接受）。</summary>
    Succeeded = 2,

    /// <summary>执行失败。</summary>
    Failed = 3,

    /// <summary>已入队（控制器返回了 AcsTask 任务 ID，等待任务执行结束）。</summary>
    Issuing = 4,
}

/// <summary>
/// 人员操作任务（人员增删改先入库，由定时任务通过控制器接口下发设备）。
/// <para>
/// 一条人员操作会拆成多条控制器任务（人员 / 卡 / 人脸各一条），任务 ID 以 JSON 数组存在
/// <see cref="TaskIds"/> 里，可在 <c>/api/tasks</c>（TasksController）查询下发情况。
/// </para>
/// </summary>
public class PersonOperationTask
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>设备编码（= 设备 DeviceId / 序列号）。</summary>
    public string MachineSN { get; set; } = string.Empty;

    /// <summary>去重键（同一人员同一设备同一流程未执行任务会被合并）。</summary>
    public string RecordKey { get; set; } = string.Empty;

    /// <summary>人员编号前缀（SyncInformation 流程使用：01=幼儿，02=家长，03=职工）。</summary>
    public string? Prefix { get; set; }

    /// <summary>人员数据ID（Detail.dataId / SyncPersonDataPage.PersonId）。</summary>
    public int? PersonDataId { get; set; }

    /// <summary>认证规则人员数据ID（SyncPersonDataPage.Id）。</summary>
    public Guid? SyncDataId { get; set; }

    /// <summary>操作类型（<see cref="PersonOperationType"/>）。</summary>
    public int OperationType { get; set; }

    /// <summary>任务流程（<see cref="PersonOperationFlow"/>）。</summary>
    public int Flow { get; set; }

    /// <summary>回传数据类型（SyncInformation 流程：3=幼儿，4=家长，8=职工）。</summary>
    public int ReturnDataType { get; set; }

    /// <summary>下发数据（Detail 或 SyncPersonDataPage 的 JSON）。</summary>
    public string Payload { get; set; } = string.Empty;

    /// <summary>任务状态（<see cref="PersonOperationState"/>）。</summary>
    public int State { get; set; }

    /// <summary>
    /// 下发任务 ID（AcsTask.Id 的 JSON 数组）。
    /// <para>
    /// 一条人员操作只入队**一条**任务（人员 → 卡 → 人脸在同一任务里顺序执行），所以数组里通常只有一个元素；
    /// 保留数组形态是为了兼容历史数据。可用这些 ID 在 <c>/api/tasks</c> 查询下发情况。
    /// </para>
    /// </summary>
    public string? TaskIds { get; set; }

    /// <summary>已执行次数。</summary>
    public int AttemptCount { get; set; }

    /// <summary>最后一次错误信息。</summary>
    public string? LastError { get; set; }

    /// <summary>创建时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>最后执行时间（调用控制器入队的时间）。</summary>
    public DateTime? ExecuteTime { get; set; }

    /// <summary>完成时间。</summary>
    public DateTime? FinishTime { get; set; }
}
