namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>
/// 人员权限计划相关能力探测结果，来自
/// <c>GET /ISAPI/AccessControl/capabilities</c>。
///
/// 依据《人员权限计划模板配置》文档：
/// <list type="bullet">
/// <item><c>isSupportUserRightPlanTemplate</c>=true → 支持人员权限计划模板配置
/// （支持该配置则一定支持周计划配置）</item>
/// <item><c>isSupportCardRightWeekPlanCfg</c>=true → 支持人员权限周计划配置</item>
/// <item><c>isSupportUserRightHolidayGroupCfg</c>=true → 支持假日组配置
/// （支持则该设备一定支持假日计划配置）</item>
/// <item><c>isSupportCardRightHolidayPlanCfg</c>=true → 支持人员权限假日计划配置</item>
/// </list>
/// <c>null</c> 表示能力集中未出现该字段，应按"不支持"处理。
/// </summary>
public sealed class UserRightPlanCapabilities
{
    /// <summary>是否支持人员权限计划模板配置。</summary>
    public bool? PlanTemplate { get; set; }

    /// <summary>是否支持人员权限周计划配置。</summary>
    public bool? WeekPlan { get; set; }

    /// <summary>是否支持人员权限假日组配置。</summary>
    public bool? HolidayGroup { get; set; }

    /// <summary>是否支持人员权限假日计划配置。</summary>
    public bool? HolidayPlan { get; set; }

    /// <summary>能力集原文（XML 或 JSON），便于排错。</summary>
    public string? RawCapabilities { get; set; }
}
