using System.Reflection;

namespace ISUPSDK.AccessControl.WebApi.Services.Builders;

/// <summary>
/// 标记某个模型在 ISAPI 请求体中的根节点名称。
///
/// 海康 ISAPI 的"配置类"接口要求把业务对象包在固定根节点下，例如：
/// <code>
/// POST /ISAPI/AccessControl/UserInfo/Record?format=json
/// { "UserInfo": { "employeeNo": "1001", ... } }
/// </code>
/// 由于同一个业务对象在 WR（写入）与 Search（查询）场景下根节点名不同
/// （新增用 <c>UserInfo</c>、查询用 <c>UserInfoSearchCond</c>），因此这里由端点决定根节点名，
/// 模型侧只声明自己的"WR 根节点名"。
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class IsapiRootNodeAttribute : Attribute
{
    public IsapiRootNodeAttribute(string rootName) => RootName = rootName;

    public string RootName { get; }
}

/// <summary>
/// 把业务模型包装为 ISAPI 根节点下的请求体。
/// </summary>
public static class IsapiBodyBuilder
{
    /// <summary>端点路径 → 请求体根节点名映射（WR/Delete 场景）。</summary>
    private static readonly Dictionary<string, string> EndpointRootNames = new(StringComparer.OrdinalIgnoreCase)
    {
        // 人员
        [IsapiEndpoints.UserInfoRecord] = "UserInfo",
        [IsapiEndpoints.UserInfoModify] = "UserInfo",
        [IsapiEndpoints.UserInfoSetUp] = "UserInfo",
        [IsapiEndpoints.UserInfoDelete] = "UserInfoDelCond",

        // 卡片
        [IsapiEndpoints.CardInfoRecord] = "CardInfo",
        [IsapiEndpoints.CardInfoModify] = "CardInfo",
        [IsapiEndpoints.CardInfoDelete] = "CardInfoDelCond",

        // 人脸库
        [IsapiEndpoints.FaceLib] = "FDLib",

        // 人员权限计划（ID 寻址资源）
        [IsapiEndpoints.PlanTemplate] = "UserRightPlanTemplate",
        [IsapiEndpoints.WeekPlanCfg] = "UserRightWeekPlanCfg",
        [IsapiEndpoints.HolidayGroupCfg] = "UserRightHolidayGroupCfg",
        [IsapiEndpoints.HolidayPlanCfg] = "UserRightHolidayPlanCfg",

        // 门禁控制
        [IsapiEndpoints.DoorRemoteControl] = "RemoteControlDoor",
    };

    /// <summary>
    /// 依据端点路径决定根节点名，若模型中已带根节点（例如调用方直接给出
    /// <c>IsapiUserInfoRecord</c>）则原样返回。
    /// </summary>
    public static object Wrap(string endpointPath, object body)
    {
        var rootName = ResolveRootName(endpointPath, body);
        return rootName is null ? body : new Dictionary<string, object?> { [rootName] = body };
    }

    /// <summary>构造带指定根节点的请求体。</summary>
    public static object WrapWith(string rootName, object body)
        => new Dictionary<string, object?> { [rootName] = body };

    /// <summary>查询场景：使用显式的条件根节点名包装。</summary>
    public static object WrapCondition(string conditionRootName, object condition)
        => new Dictionary<string, object?> { [conditionRootName] = condition };

    private static string? ResolveRootName(string endpointPath, object body)
    {
        var normalized = endpointPath.Split('?')[0].TrimEnd('/');

        // 1) 先做"端点 + /"的前缀匹配，避免 /FDLib 抢占 /FDLib/Count 这类更具体的子路径。
        foreach (var pair in EndpointRootNames)
        {
            if (normalized.StartsWith(pair.Key + "/", StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        // 2) 精确匹配
        if (EndpointRootNames.TryGetValue(normalized, out var fromEndpoint))
        {
            return fromEndpoint;
        }

        // 3) 模型自身的根节点声明
        var attribute = body.GetType().GetCustomAttribute<IsapiRootNodeAttribute>();
        if (attribute is not null)
        {
            return attribute.RootName;
        }

        // 4) 调用方已提供根节点包装（模型名以 Record / Delete / Search 结尾时视为已包装）
        var typeName = body.GetType().Name;
        if (typeName.EndsWith("Record", StringComparison.Ordinal) ||
            typeName.EndsWith("Delete", StringComparison.Ordinal) ||
            typeName.EndsWith("Search", StringComparison.Ordinal) ||
            typeName.EndsWith("Count", StringComparison.Ordinal))
        {
            return null;
        }

        return null;
    }
}
