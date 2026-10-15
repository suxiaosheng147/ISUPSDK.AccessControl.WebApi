using System.Globalization;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Services.Logging;

/// <summary>
/// 业务日志门面：把原控制台项目里"写文件"的日志（<c>Common.WriteFile</c> / <c>WriteFileHours</c>）
/// 与散落的 <c>Console.WriteLine</c> 统一到 <see cref="ILogger"/>。
/// <para>
/// 之所以用静态门面：原控制台的业务代码大量位于<b>静态方法</b>与 <b>SDK 回调</b>中
/// （同步逻辑、报警回调、透传封装），逐一注入 <c>ILogger</c> 会大幅改动既有逻辑。
/// 这里由宿主在启动时 <see cref="Attach"/> 一次，之后所有调用点无需改动即可进入统一日志管道。
/// </para>
/// <para>
/// <c>ISUP:Logging:FileFallback=true</c>（默认）时仍按原习惯落盘到
/// <c>&lt;LogRootDirectory&gt;/log/log_yyyyMMdd/</c>，便于现场排查；设为 false 则只走 ILogger。
/// </para>
/// </summary>
public static class IsupLog
{
    /// <summary>ILogger 的分类名。</summary>
    private const string DefaultCategory = "Isup";

    private static readonly object Sync = new();

    private static ILogger? _logger;
    private static bool _fileFallback = true;

    /// <summary>日志根目录（仅文件回退使用），默认程序目录。</summary>
    public static string LogRootDirectory { get; set; } = AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>是否已接入 ILogger。</summary>
    public static bool IsAttached
    {
        get
        {
            lock (Sync)
            {
                return _logger is not null;
            }
        }
    }

    /// <summary>是否保留文件落盘。</summary>
    public static bool FileFallback
    {
        get
        {
            lock (Sync)
            {
                return _fileFallback;
            }
        }
    }

    /// <summary>由宿主启动时调用一次，接入 ILogger 工厂。</summary>
    public static void Attach(ILoggerFactory factory, bool fileFallback = true)
    {
        ArgumentNullException.ThrowIfNull(factory);

        lock (Sync)
        {
            _logger = factory.CreateLogger(DefaultCategory);
            _fileFallback = fileFallback;
        }
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
        ILogger? logger;
        bool fileFallback;

        lock (Sync)
        {
            logger = _logger;
            fileFallback = _fileFallback;
        }

        var category = NormalizeCategory(fileName);

        // 未接入时只落盘，保持与原控制台一致的行为。
        logger?.Log(level, exception, "[{Category}] {Message}", category, message);

        if (fileFallback || logger is null)
        {
            AppendToFile(fileName, category, message);
        }
    }

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

    /// <summary>把文件名归一化为日志分类：去目录、去扩展名、去"15时"这类小时前缀。</summary>
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

        return string.IsNullOrWhiteSpace(name) ? DefaultCategory : name;
    }

    private static string PrefixHour(string fileName)
        => DateTime.Now.Hour.ToString(CultureInfo.InvariantCulture) + "时" + fileName;

    /// <summary>按原控制台的落盘方式写入（目录 + 时间戳 + 内容）。</summary>
    private static void AppendToFile(string? fileName, string category, string message)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? category + ".log" : fileName;

        var directory = Path.Combine(LogRootDirectory, "log", "log_" + DateTime.Today.ToString("yyyyMMdd", CultureInfo.InvariantCulture));

        try
        {
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var line = DateTime.Now.ToString(CultureInfo.InvariantCulture) + " " + message + Environment.NewLine;

            lock (Sync)
            {
                File.AppendAllText(Path.Combine(directory, name), line, Encoding.Default);
            }
        }
        catch (Exception)
        {
            // 文件回退失败不应影响业务；日志已进入 ILogger。
        }
    }

    #endregion
}
