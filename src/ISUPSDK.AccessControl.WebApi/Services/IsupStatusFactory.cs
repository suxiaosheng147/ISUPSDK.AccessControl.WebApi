using ISUPSDK.AccessControl.WebApi.Interop;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 把 <see cref="IsupSdkRuntime"/> 的运行时状态映射成对外 DTO，供 /health 与 /api/system/sdk 复用。
/// </summary>
public static class IsupStatusFactory
{
    public static IsupStatus Create(IsupSdkRuntime runtime, IsupSdkOptions options)
    {
        var current = runtime.CurrentSettings;

        return new IsupStatus
        {
            SdkInitialized = runtime.IsInitialized,
            AutoStartListen = options.AutoStartListen,
            ListenHandle = runtime.ListenHandle,
            NativeDirectory = NativeLibraryResolver.ResolvedDirectory,
            SettingsServerAddress = current.ServerAddress ?? string.Empty,
            SettingsInitPort = current.InitPort,
            OnlineDeviceCount = runtime.DeviceIds.Count,
            SyncingDeviceCount = runtime.SyncingDeviceCount,
            AlarmCount = runtime.AlarmCount,
            LastAlarmAt = runtime.LastAlarmAt,
            ServerTime = DateTimeOffset.Now,
        };
    }
}
