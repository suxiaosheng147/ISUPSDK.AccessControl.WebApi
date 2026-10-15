namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>图片存储（SS）组件状态。</summary>
public sealed class SsStatus
{
    /// <summary>SS 组件是否已启动监听（接收设备上传的图片/文件）。</summary>
    public bool Initialized { get; set; }

    /// <summary>SS 监听句柄，-1 表示未启动。</summary>
    public long ListenHandle { get; set; }

    /// <summary>图片落盘根目录（业务配置 <c>Settings:PicturePath</c>）。</summary>
    public string PictureRoot { get; set; } = string.Empty;

    /// <summary>根目录是否存在。</summary>
    public bool PictureRootExists { get; set; }

    /// <summary>已接收的文件总数（递归统计）。</summary>
    public int FileCount { get; set; }

    /// <summary>占用空间（字节）。</summary>
    public long TotalBytes { get; set; }

    /// <summary>最近一次写入时间。</summary>
    public DateTimeOffset? LastWriteAt { get; set; }

    public DateTimeOffset ServerTime { get; set; }
}

/// <summary>SS 已落盘的文件。</summary>
public sealed class SsFileInfo
{
    /// <summary>相对图片根目录的路径（同时作为下载/删除的标识）。</summary>
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>文件名。</summary>
    public string FileName { get; set; } = string.Empty;

    public long Length { get; set; }

    public DateTimeOffset LastWriteTime { get; set; }
}
