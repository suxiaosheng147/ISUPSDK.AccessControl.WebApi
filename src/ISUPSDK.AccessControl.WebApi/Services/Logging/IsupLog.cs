using System.Collections.Concurrent;
using System.Globalization;

namespace ISUPSDK.AccessControl.WebApi.Services.Logging;

/// <summary>
/// 业务日志门面：把原控制台项目里"写文件"的日志（<c>Common.WriteFile</c> / <c>WriteFileHours</c>）
/// 与散落的 <c>Console.WriteLine</c> 统一到 <see cref="ILogger"/>（其唯一 provider 是 NLog）。
/// <para>
/// 之所以用静态门面：原控制台的业务代码大量位于<b>静态方法</b>与 <b>SDK 回调</b>中
/// （同步逻辑、报警回调、透传封装），逐一注入 <c>ILogger</c> 会大幅改动既有逻辑。
/// 这里由宿主在启动时 <see cref="Attach"/> 一次，之后所有调用点无需改动即可进入统一日志管道。
/// </para>
/// <para>
/// 落盘不再由本类完成（改造前会自己追加写 <c>&lt;LogDirectory&gt;/log/log_yyyyMMdd/</c>），
/// 而是交给 <c>nlog.config</c> 的 <c>bizFile</c> 目标，目录布局为
/// <c>&lt;LogDirectory&gt;/log/&lt;类名&gt;/&lt;yyyy-MM-dd&gt;/&lt;HH&gt;.log</c>：
/// <c>Common.WriteFile("ScheduleTimer.txt", …)</c> → <c>logs/log/ScheduleTimer/2026-05-06/14.log</c>；
/// 未指定文件名的（<c>IsupLog.Info(…)</c>）→ <c>logs/log/IsupLog/&lt;yyyy-MM-dd&gt;/&lt;HH&gt;.log</c>。
/// </para>
/// </summary>
public static class IsupLog
{
    /// <summary>NLog 里业务日志的 logger 名前缀（nlog.config 用 <c>IsupLog*</c> 规则命中）。</summary>
    private const string LoggerPrefix = "IsupLog";

    /// <summary>未指定文件名时使用的分类名。</summary>
    private const string DefaultCategory = "Isup";

    /// <summary>按分类缓存的 logger，避免每条日志都走工厂的锁。</summary>
    private static readonly ConcurrentDictionary<string, ILogger> Loggers = new(StringComparer.Ordinal);

    private static ILoggerFactory? _factory;

    /// <summary>是否已接入日志管道。</summary>
    public static bool IsAttached => Volatile.Read(ref _factory) is not null;

    /// <summary>由宿主启动时调用一次，接入 <see cref="ILoggerFactory"/>（其 provider 只有 NLog）。</summary>
    public static void Attach(ILoggerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // 先清缓存再换工厂：重复 Attach（如测试宿主）不会留下旧工厂创建的 logger。
        Loggers.Clear();
        Volatile.Write(ref _factory, factory);
    }

    #region 与 Common 原有签名保持一致（调用点无需改动）

    /// <summary>对应 <c>Common.WriteFile</c>：写入一条业务日志。</summary>
    public static void Write(string fileName, bool isAppend, string fileContent)
        => Emit(LogLevel.Information, fileName, fileContent);

    /// <summary>对应 <c>Common.WriteFileHours</c>：按小时分文件写入业务日志。</summary>
    public static void WriteHours(string fileName, bool isAppend, string fileContent)
        => Emit(LogLevel.Information, PrefixHour(fileName), fileContent);

    #endregion

    #region 通用入口（用于替换 Console.WriteLine）

    public static void Debug(string message, params object?[] args)
        => Emit(LogLevel.Debug, null, Format(message, args));

    public static void Info(string message, params object?[] args)
        => Emit(LogLevel.Information, null, Format(message, args));

    public static void Warn(string message, params object?[] args)
        => Emit(LogLevel.Warning, null, Format(message, args));

    public static void Error(string message, params object?[] args)
        => Emit(LogLevel.Error, null, Format(message, args));

    public static void Error(Exception exception, string message, params object?[] args)
        => Emit(LogLevel.Error, null, Format(message, args), exception);

    #endregion

    #region 内部实现

    private static void Emit(LogLevel level, string? fileName, string message, Exception? exception = null)
    {
        var category = NormalizeCategory(fileName);
        var factory = Volatile.Read(ref _factory);

        if (factory is null)
        {
            // 尚未接入（宿主启动最早期 / 单测直接调用）：退回标准输出，至少不把日志丢掉。
            var suffix = exception is null ? string.Empty : " " + exception;
            Console.WriteLine("[" + category + "] " + message + suffix);
            return;
        }

        var logger = Loggers.GetOrAdd(category, static (key, f) => f.CreateLogger(BuildLoggerName(key)), factory);
        logger.Log(level, exception, "[{Category}] {Message}", category, message);
    }

    /// <summary>logger 名同时决定 NLog 的文件夹：<c>IsupLog</c> / <c>IsupLog.&lt;分类&gt;</c>（nlog.config 取最后一个点之后作目录名）。</summary>
    private static string BuildLoggerName(string category)
        => string.Equals(category, DefaultCategory, StringComparison.OrdinalIgnoreCase)
            ? LoggerPrefix
            : LoggerPrefix + "." + category;

    /// <summary>与原生 <c>Console.WriteLine(format, args)</c> 语义一致：无参时原样返回。</summary>
    private static string Format(string message, object?[] args)
    {
        if (args is not { Length: > 0 })
        {
            return message;
        }

        try
        {
            return string.Format(CultureInfo.InvariantCulture, message, args);
        }
        catch (FormatException)
        {
            // 模板不合法（命名占位符 {Name}、未转义花括号等）：原样拼接，日志绝不因格式化失败而抛异常。
            return message + " " + string.Join(' ', args.Select(a => a?.ToString() ?? string.Empty));
        }
    }

    /// <summary>把文件名归一化为日志分类：去目录、去扩展名、去"15时"这类小时前缀、清掉文件名非法字符。</summary>
    private static string NormalizeCategory(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return DefaultCategory;
        }

        var name = Path.GetFileNameWithoutExtension(fileName.Trim());

        // 去掉 WriteFileHours 产生的 "<小时>时" 前缀。
        var index = name.IndexOf('时');
        if (index >= 0 && index < name.Length - 1)
        {
            name = name[(index + 1)..];
        }

        // 分类会拼进 NLog 的 logger 名（nlog.config 取最后一个点之后作目录名），这里挡掉非法字符与点。
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        // 点会被 ${logger:shortName=true} 当成分隔符，替换掉才能保证"一个类一个文件夹"。
        name = name.Replace('.', '_');

        return string.IsNullOrWhiteSpace(name) ? DefaultCategory : name;
    }

    private static string PrefixHour(string fileName)
        => DateTime.Now.Hour.ToString(CultureInfo.InvariantCulture) + "时" + fileName;

    #endregion
}
