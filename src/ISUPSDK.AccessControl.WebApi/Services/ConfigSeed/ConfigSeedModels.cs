namespace ISUPSDK.AccessControl.WebApi.Services.ConfigSeed;

/// <summary>
/// <c>dbo.RemoteServiceConfig</c> 的一条种子数据：**远程主机级**配置。
/// 一个 baseUrl（Environment + Host + Port + Protocol）只对应一条记录；
/// 业务分组（ServiceCode）与基础路径（BasePath）已下移到 <c>ApiUrlConfig</c>。
/// </summary>
public sealed record RemoteServiceSeed(
    string ServiceName,
    string ServiceType,
    string Environment,
    string Host,
    int Port,
    string Protocol,
    bool IsSsl,
    string AuthType,
    int ConnectTimeoutMs,
    int ReadTimeoutMs,
    int RetryTimes,
    bool IsPrimary,
    int Priority,
    bool IsEnabled,
    string Tags,
    string Remark);

/// <summary>
/// <c>dbo.ApiUrlConfig</c> 的一条种子数据：一个控制器动作。
/// 携带所属业务分组 <see cref="ServiceCode"/> 与分组基础路径 <see cref="BasePath"/>。
/// </summary>
public sealed record ApiUrlSeed(
    string ServiceCode,
    string BasePath,
    string UrlCode,
    string UrlName,
    string Environment,
    string HttpMethod,
    string UrlPath,
    string FullUrl,
    bool IsFullUrl,
    string ContentType,
    string Remark,
    bool IsEnabled);

/// <summary>种子数据快照。</summary>
public sealed record ConfigSeedSnapshot(
    IReadOnlyList<RemoteServiceSeed> RemoteServices,
    IReadOnlyList<ApiUrlSeed> ApiUrls,
    string Environment);
