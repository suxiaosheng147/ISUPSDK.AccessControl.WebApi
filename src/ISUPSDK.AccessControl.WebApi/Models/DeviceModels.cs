namespace ISUPSDK.AccessControl.WebApi.Models;

/// <summary>设备列表项。</summary>
public sealed class IsupDeviceInfo
{
    /// <summary>CMS 注册句柄（SDK 侧 UserId）。</summary>
    public int Handle { get; init; }

    /// <summary>设备 ID（序列号）。</summary>
    public string DeviceId { get; init; } = string.Empty;

    /// <summary>是否在线（存在于注册表）。</summary>
    public bool Online { get; init; }

    /// <summary>该设备的权限同步循环是否正在运行。</summary>
    public bool SyncLoopRunning { get; init; }
}

/// <summary>手动触发同步的结果。</summary>
public sealed class IsupSyncResult
{
    public string DeviceId { get; init; } = string.Empty;
    public bool Success { get; init; }

    /// <summary>是否走了"认证规则绑定机器"的同步分支（对应 Settings.IsAuthRuleBindMachine）。</summary>
    public bool UsedAuthRule { get; init; }

    public string? Message { get; init; }
}

/// <summary>SDK 运行状态（/health 与 /api/system/sdk 共用）。</summary>
public sealed class IsupStatus
{
    public bool SdkInitialized { get; init; }
    public bool AutoStartListen { get; init; }

    /// <summary>CMS 设备注册监听句柄，-1 表示未启动。</summary>
    public int ListenHandle { get; init; }

    /// <summary>解析到的 ISUP SDK 原生库目录。</summary>
    public string? NativeDirectory { get; init; }

    public string SettingsServerAddress { get; init; } = string.Empty;
    public int SettingsInitPort { get; init; }

    public int OnlineDeviceCount { get; init; }
    public int SyncingDeviceCount { get; init; }
    public long AlarmCount { get; init; }
    public DateTimeOffset? LastAlarmAt { get; init; }
    public DateTimeOffset ServerTime { get; init; }
}
