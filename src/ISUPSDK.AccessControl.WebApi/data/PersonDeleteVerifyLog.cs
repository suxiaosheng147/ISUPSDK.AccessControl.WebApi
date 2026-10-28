namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>删除兜底记录状态。</summary>
public enum DeleteVerifyState
{
    /// <summary>已登记并重新入队，等待下发结果。</summary>
    Waiting = 0,

    /// <summary>兜底重新下发成功。</summary>
    Succeeded = 1,

    /// <summary>兜底重新下发失败。</summary>
    Failed = 2,

    /// <summary>核对确认设备上已不存在该人员，无需重新下发。</summary>
    NotExists = 3,
}

/// <summary>
/// 删除兜底记录：设备返回删除成功但人员仍在机器上时，登记一条并重新下发一次删除。
/// 同一条任务（TaskId）只兜底一次，结果回写到本表。
/// </summary>
public class PersonDeleteVerifyLog
{
    /// <summary>自增主键。</summary>
    public long Id { get; set; }

    /// <summary>对应的下发任务 Id（PersonOperationTask.Id）。</summary>
    public long TaskId { get; set; }

    /// <summary>设备编码。</summary>
    public string MachineSN { get; set; } = string.Empty;

    /// <summary>任务流程。</summary>
    public int Flow { get; set; }

    /// <summary>人员编号前缀（幼儿/家长/职工流程使用）。</summary>
    public string? Prefix { get; set; }

    /// <summary>人员数据ID。</summary>
    public int? PersonDataId { get; set; }

    /// <summary>认证规则人员记录ID。</summary>
    public Guid? SyncDataId { get; set; }

    /// <summary>原任务去重键（便于追溯）。</summary>
    public string? RecordKey { get; set; }

    /// <summary>平台下发的添加时间。</summary>
    public DateTime? PersonAddTime { get; set; }

    /// <summary>兜底时原任务的执行次数。</summary>
    public int AttemptCount { get; set; }

    /// <summary>兜底登记时间。</summary>
    public DateTime CreateTime { get; set; }

    /// <summary>兜底状态（<see cref="DeleteVerifyState"/>）。</summary>
    public int State { get; set; }

    /// <summary>结果回写时间。</summary>
    public DateTime? UpdateTime { get; set; }

    /// <summary>备注/错误信息。</summary>
    public string? Remark { get; set; }
}
