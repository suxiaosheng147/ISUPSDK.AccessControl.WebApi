namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 跨域配置（appsettings.json 的 ISUP:Cors 节）。
/// </summary>
public sealed class IsupCorsOptions
{
    public const string SectionName = "ISUP:Cors";
    public const string PolicyName = "IsupCors";

    public bool Enabled { get; set; } = true;

    /// <summary>允许的来源；为空或含 "*" 表示任意来源。</summary>
    public List<string> AllowedOrigins { get; set; } = new() { "*" };

    /// <summary>允许的方法；为空或含 "*" 表示任意方法。</summary>
    public List<string> AllowedMethods { get; set; } = new() { "*" };

    /// <summary>允许的请求头；为空或含 "*" 表示任意请求头。</summary>
    public List<string> AllowedHeaders { get; set; } = new() { "*" };

    /// <summary>需要暴露给浏览器的响应头。</summary>
    public List<string> ExposedHeaders { get; set; } = new();

    /// <summary>是否允许携带凭据；为 true 时 AllowedOrigins 必须是具体来源。</summary>
    public bool AllowCredentials { get; set; }

    /// <summary>预检结果缓存秒数。</summary>
    public int MaxAgeSeconds { get; set; } = 600;

    public bool AllowsAnyOrigin => AllowedOrigins.Count == 0 || AllowedOrigins.Contains("*");
    public bool AllowsAnyMethod => AllowedMethods.Count == 0 || AllowedMethods.Contains("*");
    public bool AllowsAnyHeader => AllowedHeaders.Count == 0 || AllowedHeaders.Contains("*");

    /// <summary>
    /// 解析 AllowCredentials 的最终取值；任意来源 + 携带凭据属于非法组合，这里强制关闭并给出提示。
    /// </summary>
    public bool ResolveAllowCredentials(out string? warning)
    {
        warning = null;
        if (AllowCredentials && AllowsAnyOrigin)
        {
            warning = "Cors:AllowCredentials=true 时必须指定具体的 AllowedOrigins，已自动关闭 AllowCredentials。";
            return false;
        }

        return AllowCredentials;
    }
}
