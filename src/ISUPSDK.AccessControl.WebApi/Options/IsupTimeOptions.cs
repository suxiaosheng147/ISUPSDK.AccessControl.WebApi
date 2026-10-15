using System.Globalization;

namespace ISUPSDK.AccessControl.WebApi.Options;

/// <summary>
/// 时间与时区配置（对应 appsettings.json 的 <c>ISUP:Time</c>）。
///
/// 约定：服务内部与数据库一律保存**绝对时间**（UTC ticks，跨库/跨时区安全），
/// 而"给人看"的地方统一按本配置的时区呈现：接口输出、事件库/任务库读出、
/// 按日分目录（抓拍图片/日志）、手工补录的默认时间、下发给设备的时间字符串。
/// 现场固定北京时间时保持默认即可。
/// </summary>
public sealed class IsupTimeOptions
{
    public const string SectionName = "ISUP:Time";

    /// <summary>默认时区：北京时间（UTC+08:00）。</summary>
    public const string DefaultTimeZoneId = "China Standard Time";

    /// <summary>北京时间固定偏移（解析失败时的兜底）。</summary>
    public static readonly TimeSpan DefaultOffset = TimeSpan.FromHours(8);

    private readonly object _sync = new();
    private TimeSpan? _offset;
    private string? _description;

    /// <summary>
    /// 时区：支持固定偏移写法（<c>"+08:00"</c>、<c>"-05:30"</c>）或时区 ID
    /// （Windows <c>"China Standard Time"</c> / IANA <c>"Asia/Shanghai"</c>）。
    /// </summary>
    public string TimeZone { get; set; } = DefaultTimeZoneId;

    /// <summary>解析出的固定偏移。</summary>
    public TimeSpan ResolveOffset()
    {
        EnsureResolved();
        return _offset!.Value;
    }

    /// <summary>可读的时区描述（用于 <c>/health</c> 与启动日志）。</summary>
    public string Describe()
    {
        EnsureResolved();
        return _description!;
    }

    /// <summary>把任意时刻转换为配置时区的本地时间。</summary>
    public DateTimeOffset ToLocal(DateTimeOffset value) => value.ToOffset(ResolveOffset());

    /// <summary>配置时区的"现在"（绝对时刻不变，仅偏移不同）。</summary>
    public DateTimeOffset Now() => DateTimeOffset.UtcNow.ToOffset(ResolveOffset());

    private void EnsureResolved()
    {
        lock (_sync)
        {
            if (_offset is not null)
            {
                return;
            }

            var configured = TimeZone?.Trim();

            if (string.IsNullOrEmpty(configured))
            {
                _offset = DefaultOffset;
                _description = $"未配置，按默认北京时间 (+08:00)";
                return;
            }

            // 1) 固定偏移写法："+08:00" / "-05:30" / "08:00"
            if (TimeSpan.TryParse(configured, CultureInfo.InvariantCulture, out var parsed)
                && parsed > TimeSpan.FromHours(-14)
                && parsed < TimeSpan.FromHours(14))
            {
                _offset = parsed;
                _description = $"固定偏移 {Format(parsed)}";
                return;
            }

            // 2) 时区 ID（Windows 或 IANA）
            try
            {
                var zone = TimeZoneInfo.FindSystemTimeZoneById(configured);

                // 现场是固定时区，取基础偏移（不跟随夏令时），保证同一部署内时间口径稳定。
                _offset = zone.BaseUtcOffset;
                _description = $"{zone.Id} {Format(zone.BaseUtcOffset)}";
                return;
            }
            catch (TimeZoneNotFoundException)
            {
                // 落到兜底
            }
            catch (InvalidTimeZoneException)
            {
                // 落到兜底
            }

            _offset = DefaultOffset;
            _description = $"无法识别的时区 \"{configured}\"，已回退默认北京时间 (+08:00)";
        }
    }

    private static string Format(TimeSpan offset)
        => (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);
}
