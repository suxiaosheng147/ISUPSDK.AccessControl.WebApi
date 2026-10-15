namespace ISUPSDK.AccessControl.WebApi.Models.EventStore;

/// <summary>
/// 库中实际出现过的可选值（供前端下拉使用）：值 + 出现次数。
/// </summary>
public sealed record AcsEventFacetItem
{
    /// <summary>字段原始值（如 <c>AccessControllerEvent</c> / <c>face</c>）。</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>该值在库中出现的条数。</summary>
    public long Count { get; init; }
}

/// <summary>
/// 事件可选值清单（<c>GET /api/events/facets</c>）。
///
/// 由"库里真实出现过的值"聚合而来，因此前端下拉里永远是现场数据里实际存在的选项，
/// 不需要人工维护枚举；未出现在库里的常见值由前端用内置映射兜底。
/// </summary>
public sealed record AcsEventFacets
{
    /// <summary>事件主类型（<c>major</c>，值以字符串给出，如 "5"）。</summary>
    public IReadOnlyList<AcsEventFacetItem> Majors { get; init; } = Array.Empty<AcsEventFacetItem>();

    /// <summary>事件次类型（<c>minor</c>，值以字符串给出，如 "75"）。</summary>
    public IReadOnlyList<AcsEventFacetItem> Minors { get; init; } = Array.Empty<AcsEventFacetItem>();

    /// <summary>事件类型（<c>eventType</c>）。</summary>
    public IReadOnlyList<AcsEventFacetItem> EventTypes { get; init; } = Array.Empty<AcsEventFacetItem>();

    /// <summary>事件来源（<c>source</c>）。</summary>
    public IReadOnlyList<AcsEventFacetItem> Sources { get; init; } = Array.Empty<AcsEventFacetItem>();

    /// <summary>认证方式（<c>verifyMode</c>）。</summary>
    public IReadOnlyList<AcsEventFacetItem> VerifyModes { get; init; } = Array.Empty<AcsEventFacetItem>();

    /// <summary>库中总行数（用于判断"还没数据"）。</summary>
    public long TotalRows { get; init; }
}
