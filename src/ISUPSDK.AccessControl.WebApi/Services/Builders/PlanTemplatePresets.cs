using ISUPSDK.AccessControl.WebApi.Models.Isapi;

namespace ISUPSDK.AccessControl.WebApi.Services.Builders;

/// <summary>
/// 人员权限计划模板预设，依据《人员权限计划模板配置》文档生成可直接下发的报文。
///
/// 组织结构（三层）：
/// <list type="number">
/// <item>假日计划 <see cref="IsapiHolidayPlanCfg"/>（一个具体日期区间的时段）</item>
/// <item>假日组 <see cref="IsapiHolidayGroupCfg"/>（用 "1,3,5" 引用若干假日计划）</item>
/// <item>周计划 <see cref="IsapiWeekPlanCfg"/>（周一~周日 × 每天最多 8 段）</item>
/// <item>计划模板 <see cref="IsapiPlanTemplate"/>（引用一个周计划 + 一个假日组）</item>
/// </list>
/// 人员的 <c>UserInfo.RightPlan</c> 最终引用计划模板编号。
/// </summary>
public static class PlanTemplatePresets
{
    private const string FullDayBegin = "00:00:00";
    private const string FullDayEnd = "23:59:59";

    /// <summary>周一到周日（与文档中的 week 枚举一致）。</summary>
    public static readonly string[] WeekDays =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday",
    };

    /// <summary>构造一个周计划：指定的星期集合全天使能，其余星期不使能。</summary>
    /// <param name="enabledDays">需要全天放行的星期（例如周一~周五）。</param>
    /// <param name="beginTime">开始时间 HH:mm:ss。</param>
    /// <param name="endTime">结束时间 HH:mm:ss。</param>
    /// <param name="planEnable">周计划整体是否使能。</param>
    public static IsapiWeekPlanCfg BuildWeekPlan(
        IEnumerable<string> enabledDays,
        string beginTime = FullDayBegin,
        string endTime = FullDayEnd,
        bool planEnable = true)
    {
        var enabled = new HashSet<string>(enabledDays, StringComparer.OrdinalIgnoreCase);
        var items = new List<IsapiWeekPlanItem>();

        foreach (var day in WeekDays)
        {
            var on = enabled.Contains(day);

            // 每天固定下发第 1 段；未启用的星期把 enable 置 false。
            items.Add(new IsapiWeekPlanItem
            {
                Week = day,
                Id = 1,
                Enable = on,
                TimeSegment = new IsapiTimeSegment
                {
                    BeginTime = on ? beginTime : string.Empty,
                    EndTime = on ? endTime : string.Empty,
                },
            });
        }

        return new IsapiWeekPlanCfg
        {
            Enable = planEnable,
            WeekPlanCfg = items,
        };
    }

    /// <summary>构造一个假日计划（单日或区间，内部时段为全天）。</summary>
    public static IsapiHolidayPlanCfg BuildHolidayPlan(
        string name,
        DateTime beginDate,
        DateTime endDate,
        bool cycleByYear = false,
        bool planEnable = true)
        => new()
        {
            Enable = planEnable,
            HolidayPlanName = name,
            BeginDate = beginDate.ToString("yyyy-MM-dd"),
            EndDate = endDate.ToString("yyyy-MM-dd"),
            CycleByYear = cycleByYear,
            HolidayPlanCfg = new List<IsapiHolidayPlanItem>
            {
                new()
                {
                    Id = 1,
                    Enable = true,
                    TimeSegment = new IsapiTimeSegment { BeginTime = FullDayBegin, EndTime = FullDayEnd },
                },
            },
        };

    /// <summary>构造一个假日组，用逗号分隔的编号字符串引用假日计划。</summary>
    public static IsapiHolidayGroupCfg BuildHolidayGroup(
        string name,
        IEnumerable<int> holidayPlanIds,
        bool groupEnable = true)
        => new()
        {
            Enable = groupEnable,
            GroupName = name,
            HolidayPlanNo = string.Join(',', holidayPlanIds),
        };

    /// <summary>构造一个计划模板，引用周计划编号与假日组编号。</summary>
    public static IsapiPlanTemplate BuildPlanTemplate(
        string templateName,
        int weekPlanNo,
        string? holidayGroupNo = null,
        bool enable = true)
        => new()
        {
            Enable = enable,
            TemplateName = templateName,
            WeekPlanNo = weekPlanNo,
            HolidayGroupNo = holidayGroupNo ?? string.Empty,
        };

    /// <summary>
    /// 生成"工作日全天"的完整预设链：周计划 + 计划模板。
    /// 适合最常见的"上班族周一至周五全天通行"场景；
    /// 需要假日例外时再单独下发假日计划与假日组。
    /// </summary>
    /// <param name="weekPlanNo">周计划编号（须未被占用）。</param>
    /// <param name="planTemplateNo">计划模板编号（须未被占用）。</param>
    /// <param name="templateName">模板名称。</param>
    /// <param name="holidayGroupNo">假日组编号（逗号分隔可多个），null 表示不关联假日组。</param>
    public static (IsapiWeekPlanCfg WeekPlan, IsapiPlanTemplate Template) BuildWorkdayPreset(
        int weekPlanNo,
        int planTemplateNo,
        string templateName = "工作日全天",
        string? holidayGroupNo = null)
    {
        var weekPlan = BuildWeekPlan(new[] { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday" });
        var template = BuildPlanTemplate(templateName, weekPlanNo, holidayGroupNo);
        return (weekPlan, template);
    }
}
