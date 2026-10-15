namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISAPI 端点常量与请求行拼装工具。
///
/// 说明：OTAP/ISUP 平台通过 <c>OTAP_CMS_ISAPIPassThrough</c> 把 ISAPI 请求透传到设备，
/// 请求行格式与海康设备直连 ISAPI 完全一致：
/// <c>&lt;METHOD&gt; &lt;PATH&gt;[?format=json&amp;...]</c>。
/// </summary>
public static class IsapiEndpoints
{
    // ------------------------------------------------------------------ 人员
    public const string UserInfoRecord = "/ISAPI/AccessControl/UserInfo/Record";
    public const string UserInfoModify = "/ISAPI/AccessControl/UserInfo/Modify";
    public const string UserInfoSetUp = "/ISAPI/AccessControl/UserInfo/SetUp";
    public const string UserInfoDelete = "/ISAPI/AccessControl/UserInfo/Delete";
    public const string UserInfoSearch = "/ISAPI/AccessControl/UserInfo/Search";
    public const string UserInfoCount = "/ISAPI/AccessControl/UserInfo/Count";
    public const string UserInfoCapabilities = "/ISAPI/AccessControl/UserInfo/capabilities";

    // ------------------------------------------------------------------ 卡片
    public const string CardInfoRecord = "/ISAPI/AccessControl/CardInfo/Record";
    public const string CardInfoModify = "/ISAPI/AccessControl/CardInfo/Modify";
    public const string CardInfoDelete = "/ISAPI/AccessControl/CardInfo/Delete";
    public const string CardInfoSearch = "/ISAPI/AccessControl/CardInfo/Search";
    public const string CardInfoCount = "/ISAPI/AccessControl/CardInfo/Count";
    public const string CardInfoCapabilities = "/ISAPI/AccessControl/CardInfo/capabilities";

    // ------------------------------------------------------------------ 人脸 / 人脸库
    public const string FaceDataRecord = "/ISAPI/Intelligent/FDLib/FaceDataRecord";
    /// <summary>faceURL 方式下发人脸（ISUP 门禁终端使用；PUT）。</summary>
    public const string FaceDataSetUp = "/ISAPI/Intelligent/FDLib/FDSetUp";
    public const string FaceDataDelete = "/ISAPI/Intelligent/FDLib/FDSearch/Delete";
    public const string FaceSearch = "/ISAPI/Intelligent/FDLib/FDSearch";
    public const string FaceLib = "/ISAPI/Intelligent/FDLib";
    public const string FaceLibCount = "/ISAPI/Intelligent/FDLib/Count";

    // ------------------------------------------------------------------ 人员权限计划（《人员权限计划模板配置》）
    public const string PlanTemplate = "/ISAPI/AccessControl/UserRightPlanTemplate";
    public const string PlanTemplateSearch = PlanTemplate + "/Search";
    public const string WeekPlanCfg = "/ISAPI/AccessControl/UserRightWeekPlanCfg";
    public const string WeekPlanCfgSearch = WeekPlanCfg + "/Search";
    public const string HolidayGroupCfg = "/ISAPI/AccessControl/UserRightHolidayGroupCfg";
    public const string HolidayGroupCfgSearch = HolidayGroupCfg + "/Search";
    public const string HolidayPlanCfg = "/ISAPI/AccessControl/UserRightHolidayPlanCfg";
    public const string HolidayPlanCfgSearch = HolidayPlanCfg + "/Search";

    // ------------------------------------------------------------------ 门禁控制
    public const string DoorRemoteControl = "/ISAPI/AccessControl/RemoteControl/door";
    public const string AcsEvent = "/ISAPI/AccessControl/AcsEvent";
    public const string AcsEventTotalNum = "/ISAPI/AccessControl/AcsEventTotalNum";
    public const string AcsWorkStatus = "/ISAPI/AccessControl/AcsWorkStatus";
    public const string AccessControlCapabilities = "/ISAPI/AccessControl/capabilities";
    public const string DoorStatus = "/ISAPI/AccessControl/DoorStatus";
    public const string AlarmOutputTrigger = "/ISAPI/System/IO/outputs";

    // ------------------------------------------------------------------ 系统
    public const string DeviceInfo = "/ISAPI/System/deviceInfo";
    public const string DeviceCapabilities = "/ISAPI/System/capabilities";

    /// <summary>事件上报（HTTP 监听）目标配置：<c>PUT/POST /ISAPI/Event/notification/httpHosts</c>。</summary>
    public const string HttpHostNotification = "/ISAPI/Event/notification/httpHosts";

    /// <summary>拼接 GET 请求行。</summary>
    public static string Get(string path, string? query = null) => Build("GET", path, query);

    /// <summary>拼接 POST 请求行。</summary>
    public static string Post(string path, string? query = null) => Build("POST", path, query);

    /// <summary>拼接 PUT 请求行。</summary>
    public static string Put(string path, string? query = null) => Build("PUT", path, query);

    /// <summary>拼接 DELETE 请求行。</summary>
    public static string Delete(string path, string? query = null) => Build("DELETE", path, query);

    public static string Build(string method, string path, string? query = null)
    {
        var normalizedMethod = method.ToUpperInvariant();
        var normalizedPath = path.StartsWith('/') ? path : "/" + path;
        return string.IsNullOrWhiteSpace(query)
            ? $"{normalizedMethod} {normalizedPath}"
            : $"{normalizedMethod} {normalizedPath}?{query.TrimStart('?')}";
    }

    /// <summary>门远程控制路径：/ISAPI/AccessControl/RemoteControl/door/{doorNo}。</summary>
    public static string DoorControl(int doorNo) => $"{DoorRemoteControl}/{doorNo}";

    /// <summary>报警输出触发路径：/ISAPI/System/IO/outputs/{id}/trigger。</summary>
    public static string OutputTrigger(int outputId) => $"{AlarmOutputTrigger}/{outputId}/trigger";
}
