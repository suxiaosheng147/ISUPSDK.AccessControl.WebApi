using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace ISUPSDK.AccessControl.WebApi.Services.ConfigSeed;

/// <summary>
/// 依据本项目**控制器**生成 <c>dbo.RemoteServiceConfig</c> / <c>dbo.ApiUrlConfig</c> 两张表的种子数据，
/// 并输出 SQL Server 写法的添加数据脚本。
///
/// <para>表职责划分（ServiceCode 已下移到接口表）：</para>
/// <list type="bullet">
/// <item><b>RemoteServiceConfig</b>：**远程主机级**配置，一个 baseUrl
/// （Environment + Host + Port + Protocol）一条，唯一索引 <c>UX_RSC_Unique</c> 也按这四列；</item>
/// <item><b>ApiUrlConfig</b>：每个控制器动作一条，携带业务分组编码 <c>ServiceCode</c>
/// （如 <c>DEVICES</c>、<c>ACCESS-CONTROL.CARDS</c>）与分组基础路径 <c>BasePath</c>，
/// 通过 <c>ServiceConfigId</c> 指向上面那条主机记录。</item>
/// </list>
/// </summary>
public sealed class ConfigSeedSqlGenerator
{
    /// <summary>写入 <c>dbo.RemoteServiceConfig</c> 的列（<c>Id</c> 由 <c>DEFAULT NEWSEQUENTIALID()</c> 生成；<c>RowVer</c> 为 ROWVERSION，均不插）。</summary>
    public static readonly string[] RemoteServiceColumns =
    {
        "ServiceName", "ServiceType", "Environment", "Host", "Port", "Protocol",
        "IsSsl", "AuthType", "ConnectTimeoutMs", "ReadTimeoutMs", "RetryTimes",
        "IsPrimary", "Priority", "IsEnabled", "IsDeleted", "Tags", "Remark", "CreatedAt",
    };

    /// <summary>写入 <c>dbo.ApiUrlConfig</c> 的列（<c>Id</c> 为 IDENTITY，<c>RowVer</c> 为 ROWVERSION，均不插）。</summary>
    public static readonly string[] ApiUrlColumns =
    {
        "ServiceCode", "BasePath", "UrlCode", "UrlName", "Environment", "ServiceConfigId",
        "HttpMethod", "UrlPath", "IsFullUrl", "FullUrl", "ContentType", "TimeoutMs",
        "IsEnabled", "IsDeleted", "Remark", "CreatedAt",
    };

    /// <summary>远程主机级配置的服务名（一个 baseUrl 一条，因此不再按控制器分组命名）。</summary>
    private const string HostServiceName = "ISUP 门禁接入服务";

    // 列宽（与建表语句一致，超长会报 String or binary data would be truncated）
    private const int ServiceNameLength = 128;
    private const int BasePathLength = 255;
    private const int UrlCodeLength = 128;
    private const int UrlNameLength = 128;
    private const int UrlPathLength = 500;
    private const int FullUrlLength = 1000;
    private const int HostLength = 255;
    private const int TagsLength = 200;
    private const int RemarkLength = 500;
    private const int ServiceCodeLength = 64;

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> EnvironmentAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DEV"] = "DEV",
        ["DEVELOPMENT"] = "DEV",
        ["LOCAL"] = "DEV",
        ["TEST"] = "TEST",
        ["TESTING"] = "TEST",
        ["STAGING"] = "TEST",
        ["QA"] = "TEST",
        ["SIT"] = "TEST",
        ["UAT"] = "UAT",
        ["PRE"] = "UAT",
        ["PREPROD"] = "UAT",
        ["PROD"] = "PROD",
        ["PRODUCTION"] = "PROD",
        ["RELEASE"] = "PROD",
        ["LIVE"] = "PROD",
    };

    private readonly IApiDescriptionGroupCollectionProvider _apiExplorer;
    private readonly IWebHostEnvironment _environment;

    public ConfigSeedSqlGenerator(
        IApiDescriptionGroupCollectionProvider apiExplorer,
        IWebHostEnvironment environment)
    {
        _apiExplorer = apiExplorer;
        _environment = environment;
    }

    /// <summary><c>Environment</c> 列允许的取值。</summary>
    public static IReadOnlyCollection<string> AllowedEnvironments { get; } = new[] { "DEV", "TEST", "UAT", "PROD" };

    /// <summary>把宿主环境名或用户传值归一化成 <c>DEV/TEST/UAT/PROD</c>（不合法时退回 PROD）。</summary>
    public static string NormalizeEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "PROD";
        }

        return EnvironmentAliases.TryGetValue(value.Trim(), out var normalized) ? normalized : "PROD";
    }

    /// <summary>是否为合法的 <c>Environment</c> 取值（大小写不敏感）。</summary>
    public static bool IsAllowedEnvironment(string? value)
        => !string.IsNullOrWhiteSpace(value) && EnvironmentAliases.ContainsKey(value.Trim());

    /// <summary>
    /// 收集种子数据。
    /// </summary>
    /// <param name="serviceBaseUrl">本服务对外基址，如 <c>http://192.168.1.12:7823</c>（不含结尾斜杠）。</param>
    /// <param name="serviceCodes">只保留这些业务分组；为 <c>null</c> / 空集合时导出全部。</param>
    /// <param name="environment">目标环境；留空取宿主环境名归一化后的值。</param>
    public ConfigSeedSnapshot Build(
        string serviceBaseUrl,
        IEnumerable<string>? serviceCodes = null,
        string? environment = null)
    {
        var baseUrl = (serviceBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        var env = NormalizeEnvironment(string.IsNullOrWhiteSpace(environment) ? _environment.EnvironmentName : environment);
        var (host, port, protocol) = ParseEndpoint(baseUrl);
        var isSsl = string.Equals(protocol, "HTTPS", StringComparison.OrdinalIgnoreCase);

        var filter = serviceCodes?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var summaries = XmlSummary.Load();
        var apis = new List<ApiUrlSeed>();
        var usedUrlCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var description in EnumerateActions())
        {
            var urlPath = NormalizeRelativePath(description.RelativePath);
            if (urlPath.Length == 0)
            {
                continue;
            }

            var action = (ControllerActionDescriptor)description.ActionDescriptor;
            var serviceCode = Clamp(ResolveServiceCode(action, urlPath), ServiceCodeLength);

            if (filter is { Count: > 0 } && !filter.Contains(serviceCode))
            {
                continue;
            }

            var httpMethod = (description.HttpMethod ?? "GET").ToUpperInvariant();
            var segments = urlPath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            apis.Add(new ApiUrlSeed(
                ServiceCode: serviceCode,
                BasePath: Clamp(BuildBasePath(serviceCode), BasePathLength),
                UrlCode: BuildUrlCode(httpMethod, segments, usedUrlCodes),
                UrlName: Clamp(summaries.GetSummary(action.MethodInfo) ?? $"{action.ControllerName}.{action.ActionName}", UrlNameLength),
                Environment: env,
                HttpMethod: httpMethod,
                UrlPath: Clamp(urlPath, UrlPathLength),
                FullUrl: Clamp(baseUrl.Length == 0 ? urlPath : baseUrl + urlPath, FullUrlLength),
                IsFullUrl: false,
                ContentType: "application/json",
                Remark: Clamp($"{action.ControllerName}Controller.{action.ActionName}", RemarkLength),
                IsEnabled: true));
        }

        var orderedApis = apis
            .OrderBy(api => api.ServiceCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(api => api.UrlPath, StringComparer.OrdinalIgnoreCase)
            .ThenBy(api => api.HttpMethod, StringComparer.Ordinal)
            .ToArray();

        var groups = orderedApis
            .Select(api => api.ServiceCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // 主机级记录：一个 baseUrl 一条，业务分组信息写进 Remark 便于排查
        var services = new[]
        {
            new RemoteServiceSeed(
                ServiceName: Clamp(HostServiceName, ServiceNameLength),
                ServiceType: "API",
                Environment: env,
                Host: Clamp(host, HostLength),
                Port: port,
                Protocol: protocol,
                IsSsl: isSsl,
                AuthType: "NONE",
                ConnectTimeoutMs: 30000,
                ReadTimeoutMs: 30000,
                RetryTimes: 0,
                IsPrimary: true,
                Priority: 100,
                IsEnabled: true,
                Tags: Clamp("AUTO-SEED,ISUP-ACS", TagsLength),
                Remark: Clamp(
                    $"远程主机级配置：共 {groups.Length} 个接口分组、{orderedApis.Length} 个接口。" +
                    $"分组：{string.Join("、", groups)}。",
                    RemarkLength)),
        };

        return new ConfigSeedSnapshot(services, orderedApis, env);
    }

    /// <summary>
    /// 生成 SQL Server 写法的添加数据脚本（**不使用事务**，语句逐条独立提交）。
    /// </summary>
    /// <param name="snapshot">种子数据。</param>
    /// <param name="generatedFrom">生成来源描述，写进脚本文档头。</param>
    /// <param name="generatedAt">生成时间。</param>
    public string BuildSqlServerScript(
        ConfigSeedSnapshot snapshot,
        string generatedFrom,
        DateTimeOffset generatedAt)
    {
        var sql = new StringBuilder();
        var endpoint = snapshot.RemoteServices.Count > 0
            ? $"{snapshot.RemoteServices[0].Host}:{snapshot.RemoteServices[0].Port}"
            : "-";

        var groups = snapshot.ApiUrls
            .Select(api => api.ServiceCode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        sql.AppendLine("/* ===========================================================================");
        sql.AppendLine("   RemoteServiceConfig / ApiUrlConfig 初始化数据（SQL Server 写法）");
        sql.AppendLine();
        sql.AppendLine("   生成来源：" + generatedFrom);
        sql.AppendLine("   生成时间：" + generatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        sql.AppendLine("   目标环境：" + snapshot.Environment + "（Environment 列只接受 DEV / TEST / UAT / PROD）");
        sql.AppendLine("   服务入口：" + endpoint);
        sql.AppendLine($"   记录条数：RemoteServiceConfig {snapshot.RemoteServices.Count} 条（主机级）/ " +
                       $"ApiUrlConfig {snapshot.ApiUrls.Count} 条（{groups} 个业务分组）");
        sql.AppendLine();
        sql.AppendLine("   表职责：RemoteServiceConfig = 远程主机配置，按 (Environment, Host, Port, Protocol) 唯一；");
        sql.AppendLine("           ApiUrlConfig       = 接口地址 + 业务分组编码 ServiceCode + 分组基础路径 BasePath。");
        sql.AppendLine("   幂等规则：RemoteServiceConfig 按 (Environment, Host, Port, Protocol) 判重；");
        sql.AppendLine("             ApiUrlConfig      按 (UrlCode, Environment, ServiceConfigId) 判重。");
        sql.AppendLine("   执行方式：不使用事务，语句逐条独立提交；中途失败直接整份重跑即可补全，不会产生重复数据。");
        sql.AppendLine("   主键 Id：UNIQUEIDENTIFIER，由列的 DEFAULT NEWSEQUENTIALID() 生成，本脚本不显式写入。");
        sql.AppendLine("=========================================================================== */");
        sql.AppendLine("SET NOCOUNT ON;");
        sql.AppendLine("SET XACT_ABORT ON;");
        sql.AppendLine("GO");
        sql.AppendLine();
        sql.AppendLine($"DECLARE @env NVARCHAR(16) = {Literal(snapshot.Environment)};");
        sql.AppendLine("DECLARE @now DATETIME2(3) = SYSDATETIME();");
        sql.AppendLine();

        AppendRemoteServices(sql, snapshot.RemoteServices);
        sql.AppendLine();
        AppendApiUrls(sql, snapshot.RemoteServices, snapshot.ApiUrls);

        sql.AppendLine($"PRINT N'配置种子数据已写入：RemoteServiceConfig {snapshot.RemoteServices.Count} 条 / " +
                       $"ApiUrlConfig {snapshot.ApiUrls.Count} 条，环境 ' + @env + N'。';");
        sql.AppendLine("GO");

        return sql.ToString();
    }

    private static void AppendRemoteServices(StringBuilder sql, IReadOnlyList<RemoteServiceSeed> services)
    {
        sql.AppendLine("/* ---------------------------------------------------------------------------");
        sql.AppendLine("   1) RemoteServiceConfig：远程主机配置（一个 baseUrl 一条）");
        sql.AppendLine("--------------------------------------------------------------------------- */");
        sql.AppendLine();

        if (services.Count == 0)
        {
            sql.AppendLine("-- （没有主机配置，未生成 INSERT）");
            return;
        }

        foreach (var service in services)
        {
            sql.AppendLine($"-- {service.ServiceName}（{service.Environment} · {service.Host}:{service.Port}/{service.Protocol}）");
            sql.AppendLine("IF NOT EXISTS (SELECT 1 FROM dbo.RemoteServiceConfig");
            sql.AppendLine("               WHERE Environment = @env");
            sql.AppendLine($"                 AND Host = {Literal(service.Host)}");
            sql.AppendLine($"                 AND Port = {service.Port}");
            sql.AppendLine($"                 AND Protocol = {Literal(service.Protocol)}");
            sql.AppendLine("                 AND IsDeleted = 0)");
            sql.AppendLine("BEGIN");
            sql.AppendLine("    INSERT INTO dbo.RemoteServiceConfig");
            sql.AppendLine("        (" + string.Join(", ", RemoteServiceColumns) + ")");
            sql.AppendLine("    VALUES");
            sql.AppendLine("        (");
            sql.AppendLine($"            {Literal(service.ServiceName)}, {Literal(service.ServiceType)}, @env, " +
                           $"{Literal(service.Host)}, {service.Port}, {Literal(service.Protocol)},");
            sql.AppendLine($"            {Bit(service.IsSsl)}, {Literal(service.AuthType)}, " +
                           $"{service.ConnectTimeoutMs}, {service.ReadTimeoutMs}, {service.RetryTimes},");
            sql.AppendLine($"            {Bit(service.IsPrimary)}, {service.Priority}, {Bit(service.IsEnabled)}, 0, " +
                           $"{Literal(service.Tags)}, {Literal(service.Remark)}, @now");
            sql.AppendLine("        );");
            sql.AppendLine("END");
            sql.AppendLine();
        }
    }

    private static void AppendApiUrls(
        StringBuilder sql,
        IReadOnlyList<RemoteServiceSeed> services,
        IReadOnlyList<ApiUrlSeed> apis)
    {
        sql.AppendLine("/* ---------------------------------------------------------------------------");
        sql.AppendLine("   2) ApiUrlConfig：控制器动作对应的接口地址（ServiceCode / BasePath 在接口表上）");
        sql.AppendLine("--------------------------------------------------------------------------- */");
        sql.AppendLine();

        if (apis.Count == 0)
        {
            sql.AppendLine("-- （没有匹配的控制器动作，未生成 INSERT）");
            return;
        }

        var service = services.Count > 0 ? services[0] : null;

        // 先取回主机记录主键；主表按 (Environment, Host, Port, Protocol) 唯一
        sql.AppendLine("DECLARE @svc UNIQUEIDENTIFIER =");
        sql.AppendLine("    (SELECT TOP (1) Id FROM dbo.RemoteServiceConfig");
        sql.AppendLine("      WHERE Environment = @env");
        if (service != null)
        {
            sql.AppendLine($"        AND Host = {Literal(service.Host)}");
            sql.AppendLine($"        AND Port = {service.Port}");
            sql.AppendLine($"        AND Protocol = {Literal(service.Protocol)}");
        }

        sql.AppendLine("        AND IsDeleted = 0");
        sql.AppendLine("      ORDER BY Id DESC);");
        sql.AppendLine();
        sql.AppendLine("IF @svc IS NULL");
        sql.AppendLine("BEGIN");
        sql.AppendLine("    PRINT N'跳过接口配置：dbo.RemoteServiceConfig 中没有匹配的远程主机记录。';");
        sql.AppendLine("END");
        sql.AppendLine("ELSE");
        sql.AppendLine("BEGIN");

        var currentGroup = string.Empty;

        foreach (var api in apis)
        {
            if (!string.Equals(currentGroup, api.ServiceCode, StringComparison.OrdinalIgnoreCase))
            {
                if (currentGroup.Length > 0)
                {
                    sql.AppendLine();
                }

                currentGroup = api.ServiceCode;
                sql.AppendLine($"    /* ===== 分组：{api.ServiceCode}（{api.BasePath}）===== */");
            }

            sql.AppendLine($"    -- {api.HttpMethod} {api.UrlPath}（{api.Remark}）");
            sql.AppendLine("    IF NOT EXISTS (SELECT 1 FROM dbo.ApiUrlConfig");
            sql.AppendLine($"                   WHERE UrlCode = {Literal(api.UrlCode)}");
            sql.AppendLine("                     AND Environment = @env");
            sql.AppendLine("                     AND ServiceConfigId = @svc");
            sql.AppendLine("                     AND IsDeleted = 0)");
            sql.AppendLine("    BEGIN");
            sql.AppendLine("        INSERT INTO dbo.ApiUrlConfig");
            sql.AppendLine("            (" + string.Join(", ", ApiUrlColumns) + ")");
            sql.AppendLine("        VALUES");
            sql.AppendLine("            (");
            sql.AppendLine($"                {Literal(api.ServiceCode)}, {Literal(api.BasePath)}, " +
                           $"{Literal(api.UrlCode)}, {Literal(api.UrlName)}, @env, @svc,");
            sql.AppendLine($"                {Literal(api.HttpMethod)}, {Literal(api.UrlPath)}, " +
                           $"{Bit(api.IsFullUrl)}, {Literal(api.FullUrl)},");
            sql.AppendLine($"                {Literal(api.ContentType)}, NULL, {Bit(api.IsEnabled)}, 0,");
            sql.AppendLine($"                {Literal(api.Remark)}, @now");
            sql.AppendLine("            );");
            sql.AppendLine("    END");
            sql.AppendLine();
        }

        sql.AppendLine("END");
        sql.AppendLine();
    }

    private static (string Host, int Port, string Protocol) ParseEndpoint(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
        {
            return (uri.Host, uri.Port, uri.Scheme.ToUpperInvariant());
        }

        return ("127.0.0.1", 80, "HTTP");
    }

    private IEnumerable<ApiDescription> EnumerateActions()
        => _apiExplorer.ApiDescriptionGroups.Items
            .SelectMany(group => group.Items)
            .Where(item => item.ActionDescriptor is ControllerActionDescriptor && !string.IsNullOrWhiteSpace(item.RelativePath));

    /// <summary>
    /// 业务分组编码以<b>控制器级 <c>[Route]</c> 模板</b>为准（如 <c>[Route("api/access-control/cards")]</c>），
    /// 一个控制器一个分组；没有特性路由时退回动作的相对路径。
    /// </summary>
    private static string ResolveServiceCode(ControllerActionDescriptor action, string relativePath)
    {
        var template = action.ControllerTypeInfo.GetCustomAttribute<RouteAttribute>()?.Template;
        var source = string.IsNullOrWhiteSpace(template)
            ? relativePath
            : "/" + template.TrimStart('/');

        return ResolveServiceCode(source.Split('/', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return string.Empty;
        }

        var path = relativePath.Trim();
        return path.StartsWith('/') ? path : "/" + path;
    }

    /// <summary>
    /// 由路由模板归纳分组编码：跳过 <c>api</c> 段，最多取两段非参数段，转成大写。
    /// 例：<c>api/access-control/persons</c> → <c>ACCESS-CONTROL.PERSONS</c>；<c>api/devices</c> → <c>DEVICES</c>。
    /// </summary>
    private static string ResolveServiceCode(string[] segments)
    {
        var parts = new List<string>(2);

        foreach (var segment in segments)
        {
            if (parts.Count == 0 && string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (parts.Count >= 2 || IsParameterSegment(segment))
            {
                break;
            }

            parts.Add(segment);
        }

        return parts.Count == 0
            ? "DEFAULT"
            : string.Join('.', parts).ToUpperInvariant();
    }

    /// <summary>分组基础路径，如 <c>ACCESS-CONTROL.CARDS</c> → <c>/api/access-control/cards</c>。</summary>
    private static string BuildBasePath(string serviceCode)
        => "/api/" + serviceCode.Replace('.', '/').ToLowerInvariant();

    private static bool IsParameterSegment(string segment)
        => segment.StartsWith('{') || segment.StartsWith(':');

    /// <summary>
    /// 由 HTTP 方法与路径生成 <c>UrlCode</c>，例如
    /// <c>POST /api/access-control/cards/search</c> → <c>ACCESS_CONTROL_CARDS_SEARCH_POST</c>。
    /// </summary>
    private static string BuildUrlCode(string httpMethod, string[] segments, HashSet<string> used)
    {
        var builder = new StringBuilder();

        foreach (var segment in segments)
        {
            if (builder.Length == 0 && string.Equals(segment, "api", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var normalized = NormalizeSegment(segment);
            if (normalized.Length == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('_');
            }

            builder.Append(normalized);
        }

        if (builder.Length == 0)
        {
            builder.Append("ROOT");
        }

        var urlCode = builder.Append('_').Append(httpMethod).ToString().ToUpperInvariant();

        if (!used.Add(urlCode))
        {
            var index = 2;
            var candidate = $"{urlCode}_{index}";
            while (!used.Add(candidate))
            {
                index++;
                candidate = $"{urlCode}_{index}";
            }

            urlCode = candidate;
        }

        return Clamp(urlCode, UrlCodeLength);
    }

    private static string NormalizeSegment(string segment)
    {
        var value = segment.Trim('{', '}');

        // 去掉路由约束：{doorNo:int} → doorNo
        var colon = value.IndexOf(':');
        if (colon >= 0)
        {
            value = value[..colon];
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        }

        return builder.ToString().Trim('_');
    }

    private static string Clamp(string? value, int maxLength)
    {
        var text = value ?? string.Empty;
        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private static string Bit(bool value) => value ? "1" : "0";

    /// <summary>SQL Server 字符串字面量（Unicode，单引号转义）。</summary>
    private static string Literal(string? value)
        => "N'" + (value ?? string.Empty).Replace("'", "''") + "'";

    /// <summary>
    /// 控制器动作的 XML 注释摘要（项目开启 <c>GenerateDocumentationFile</c>），
    /// 用于填充 <c>ApiUrlConfig.UrlName</c>。
    /// </summary>
    private sealed class XmlSummary
    {
        private readonly XDocument? _document;

        private XmlSummary(XDocument? document) => _document = document;

        public static XmlSummary Load()
        {
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "ISUPSDK.AccessControl.WebApi.xml");
                return new XmlSummary(File.Exists(path) ? XDocument.Load(path) : null);
            }
            catch (Exception)
            {
                return new XmlSummary(null);
            }
        }

        public string? GetSummary(MethodInfo? method)
        {
            if (_document?.Root is null || method?.DeclaringType is null)
            {
                return null;
            }

            var key = $"M:{method.DeclaringType.FullName}.{method.Name}";
            var member = _document.Root
                .Element("members")?
                .Elements("member")
                .FirstOrDefault(element =>
                {
                    var name = (string?)element.Attribute("name");
                    if (name is null || !name.StartsWith(key, StringComparison.Ordinal))
                    {
                        return false;
                    }

                    return name.Length == key.Length || name[key.Length] == '(';
                });

            var summary = member?.Element("summary");
            if (summary is null)
            {
                return null;
            }

            var text = WhitespaceRegex
                .Replace(string.Concat(summary.Nodes().Select(RenderNode)), " ")
                .Trim();

            if (text.Length == 0)
            {
                return null;
            }

            // 只取第一句，避免把整段 remarks 塞进列里。
            var stop = text.IndexOf('。');
            if (stop > 0 && stop + 1 < text.Length)
            {
                text = text[..(stop + 1)];
            }

            return text;
        }

        private static string RenderNode(XNode node) => node switch
        {
            XElement element when element.Name.LocalName is "see" or "seealso" =>
                (string?)element.Attribute("cref") is { } cref
                    ? cref[(cref.IndexOf(':') + 1)..]
                    : (string?)element.Attribute("langword") ?? element.Value,
            XElement element => element.Value,
            XText text => text.Value,
            _ => string.Empty,
        };
    }
}
