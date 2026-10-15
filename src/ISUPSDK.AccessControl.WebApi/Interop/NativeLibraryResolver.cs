using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.Interop;

/// <summary>
/// ISUP SDK 原生库（HCISUPCMS.dll / HCISUPAlarm.dll / HCISUPSS.dll / HCISUPStream.dll 等）定位。
/// <para>
/// 解析顺序：配置 ISUP:NativeDirectory → 环境变量 ISUP_SDK_DIR / HCISUPSDK_DIR →
/// 程序输出目录 → ./sdk → 从输出目录逐级向上查找 ISUPSDK* / HCISUPSDK* 目录。
/// </para>
/// <para>
/// 找到目录后会把它加入原生库搜索路径（<c>AddDllDirectory</c>），
/// 这样 HCISUPCMS.dll 的依赖（libeay32.dll、ssleay32.dll、HCNetUtils.dll 等）也能被加载。
/// </para>
/// </summary>
public static class NativeLibraryResolver
{
    /// <summary>判定"目录就是 SDK 目录"所需要的文件。</summary>
    private static readonly string[] RequiredAssemblies = { "HCISUPCMS.dll", "HCISUPAlarm.dll" };

    private const int LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;

    private static readonly object Sync = new();
    private static bool _resolved;
    private static string? _directory;

    /// <summary>已解析出的原生库目录，未找到时为 null。</summary>
    public static string? ResolvedDirectory => _directory;

    /// <summary>原生库搜索目录列表（已解析目录 + 程序目录），用于状态展示与排查。</summary>
    public static IReadOnlyList<string> Directories
    {
        get
        {
            var directories = new List<string>();
            if (!string.IsNullOrWhiteSpace(_directory))
            {
                directories.Add(_directory!);
            }

            directories.Add(AppContext.BaseDirectory);
            return directories;
        }
    }

    /// <summary>解析并注册原生库搜索路径；多次调用只生效一次。</summary>
    public static string? Resolve(string? configuredDirectory)
    {
        lock (Sync)
        {
            if (_resolved)
            {
                return _directory;
            }

            _resolved = true;

            foreach (var candidate in EnumerateCandidates(configuredDirectory))
            {
                if (IsSdkDirectory(candidate))
                {
                    _directory = Path.GetFullPath(candidate!);
                    RegisterSearchPath(_directory);
                    return _directory;
                }
            }

            return _directory;
        }
    }

    private static IEnumerable<string?> EnumerateCandidates(string? configuredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(configuredDirectory))
        {
            yield return configuredDirectory;
        }

        foreach (var variable in new[] { "ISUP_SDK_DIR", "HCISUPSDK_DIR" })
        {
            var value = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(value))
            {
                yield return value;
            }
        }

        var baseDirectory = AppContext.BaseDirectory;
        yield return baseDirectory;
        yield return Path.Combine(baseDirectory, "sdk");
        yield return Path.Combine(baseDirectory, "isupsdk");

        // 开发期便利：从输出目录逐级向上查找 SDK 目录。
        var current = new DirectoryInfo(baseDirectory);
        for (var depth = 0; depth < 6 && current is not null; depth++)
        {
            DirectoryInfo[] children;
            try
            {
                children = current.GetDirectories();
            }
            catch (Exception)
            {
                break;
            }

            foreach (var child in children)
            {
                if (child.Name.StartsWith("ISUPSDK", StringComparison.OrdinalIgnoreCase)
                    || child.Name.StartsWith("HCISUPSDK", StringComparison.OrdinalIgnoreCase))
                {
                    yield return child.FullName;

                    // 常见布局是 <SDK 根>/lib，再补一个候选。
                    yield return Path.Combine(child.FullName, "lib");
                }
            }

            current = current.Parent;
        }
    }

    private static bool IsSdkDirectory(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return false;
        }

        return RequiredAssemblies.All(name => File.Exists(Path.Combine(directory, name)));
    }

    private static void RegisterSearchPath(string directory)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
            AddDllDirectory(directory);
        }
        catch (Exception)
        {
            // 注册失败时退回到默认搜索路径（依赖库与可执行文件同目录即可）。
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetDefaultDllDirectories(int directoryFlags);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr AddDllDirectory(string newDirectory);
}
