using ISUPSDK.AccessControl.WebApi.Models;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 图片存储（SS）服务：管理设备经 <c>NET_ESS_StartListen</c> 上传并落盘的图片/文件。
/// <para>
/// ISUP 的 SS 是"设备 → 平台"方向：设备把抓拍图片推到平台，由 SDK 的存储回调写入
/// <c>Settings:PicturePath</c>。本服务提供状态与文件管理能力。
/// </para>
/// </summary>
public sealed class IsupSsService
{
    private readonly IsupSdkRuntime _runtime;
    private readonly ILogger<IsupSsService> _logger;

    public IsupSsService(IsupSdkRuntime runtime, ILogger<IsupSsService> logger)
    {
        _runtime = runtime;
        _logger = logger;
    }

    /// <summary>图片落盘根目录（绝对路径）。</summary>
    public string PictureRoot
    {
        get
        {
            var configured = _runtime.CurrentSettings.PicturePath;
            if (string.IsNullOrWhiteSpace(configured))
            {
                configured = "./storage/pictures";
            }

            return Path.GetFullPath(configured, AppContext.BaseDirectory);
        }
    }

    /// <summary>存储状态：目录、文件数、占用空间。</summary>
    public SsStatus GetStatus()
    {
        var root = PictureRoot;
        var exists = Directory.Exists(root);
        var count = 0;
        long bytes = 0;
        DateTimeOffset? lastWrite = null;

        if (exists)
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    count++;
                    bytes += info.Length;

                    var write = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime();
                    if (lastWrite is null || write > lastWrite)
                    {
                        lastWrite = write;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "统计图片目录 {Root} 失败。", root);
            }
        }

        return new SsStatus
        {
            Initialized = _runtime.SsListenHandle >= 0,
            ListenHandle = _runtime.SsListenHandle,
            PictureRoot = root,
            PictureRootExists = exists,
            FileCount = count,
            TotalBytes = bytes,
            LastWriteAt = lastWrite,
            ServerTime = DateTimeOffset.Now,
        };
    }

    /// <summary>列出已落盘文件（按写入时间倒序）。</summary>
    public IReadOnlyList<SsFileInfo> ListFiles(int limit)
    {
        var root = PictureRoot;
        if (!Directory.Exists(root))
        {
            return Array.Empty<SsFileInfo>();
        }

        try
        {
            return Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(file => new FileInfo(file))
                .OrderByDescending(info => info.LastWriteTimeUtc)
                .Take(Math.Clamp(limit, 1, 2000))
                .Select(info => new SsFileInfo
                {
                    RelativePath = Path.GetRelativePath(root, info.FullName),
                    FileName = info.Name,
                    Length = info.Length,
                    LastWriteTime = new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero).ToLocalTime(),
                })
                .ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "枚举图片目录 {Root} 失败。", root);
            return Array.Empty<SsFileInfo>();
        }
    }

    /// <summary>把相对路径解析为根目录内的安全绝对路径；越权或不存在时返回 null。</summary>
    public string? ResolveFile(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        var root = PictureRoot;
        var full = Path.GetFullPath(Path.Combine(root, relativePath));

        // 目录穿越防护：必须落在根目录内。
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("拒绝越权的图片路径访问：{Path}", relativePath);
            return null;
        }

        return File.Exists(full) ? full : null;
    }

    /// <summary>删除根目录内的一个文件。</summary>
    public bool DeleteFile(string relativePath)
    {
        var full = ResolveFile(relativePath);
        if (full is null)
        {
            return false;
        }

        File.Delete(full);
        _logger.LogInformation("已删除图片文件 {Path}", relativePath);
        return true;
    }
}
