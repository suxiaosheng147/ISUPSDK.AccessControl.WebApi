using ISUPSDK.AccessControl.WebApi.Models.Isapi;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>
/// 人员下发任务（<see cref="AcsTaskOperation.PersonIssue"/> / <see cref="AcsTaskOperation.PersonIssueDelete"/>）的载荷。
/// <para>
/// 队列侧（Customize）按平台下发的人员数据，把每一步要发的 ISAPI 报文先算好，
/// 执行器只按固定顺序发送：<b>人员 →（删旧卡）→ 卡 → 人脸</b>，
/// 人员那一步没成功就不会继续下发卡和人脸（设备要求"先有人员"）。
/// </para>
/// </summary>
public sealed class PersonIssuePayload
{
    /// <summary>人员工号（= 人员编号前缀 + webId，或 人员类型 + 人员ID）。</summary>
    public string EmployeeNo { get; set; } = string.Empty;

    /// <summary>
    /// 是否为删除人员：为 <c>true</c> 时只执行「删人员 → 删人脸」，
    /// <see cref="Person"/> / <see cref="Card"/> / <see cref="Face"/> 一律忽略。
    /// </summary>
    public bool DeletePerson { get; set; }

    /// <summary>人员信息（第一步，新增/修改时必填）。</summary>
    public IsapiUserInfo? Person { get; set; }

    /// <summary>需要先删除的旧卡号（修改人员时换卡用，可为空）。</summary>
    public string? OldCardNo { get; set; }

    /// <summary>需要下发的卡片（可为空：人员没有卡就不下发）。</summary>
    public IsapiCardInfo? Card { get; set; }

    /// <summary>需要下发的人脸（faceURL 方式，可为空）。</summary>
    public IsapiFaceDataRecord? Face { get; set; }

    /// <summary>需要删除的人脸 ID（没有照片时把设备上的旧人脸清掉，可为空）。</summary>
    public string? DeleteFaceId { get; set; }

    /// <summary>本次要下发的步骤描述（"人员 / 卡 / 人脸"之类，日志与任务描述用）。</summary>
    public string? Steps { get; set; }
}
