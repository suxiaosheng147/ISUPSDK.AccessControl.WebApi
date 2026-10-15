using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue.Msmq;

/// <summary>
/// MSMQ 原生 API（<c>mqrt.dll</c>）的 P/Invoke 声明与常量。
///
/// 常量取值来源（Windows SDK / mingw-w64 头文件）：
/// <list type="bullet">
/// <item><c>mp.h</c>：<c>PROPID_M_BASE = 0</c>、<c>PROPID_M_BODY = 9</c>、<c>PROPID_M_BODY_SIZE = 10</c>、
/// <c>PROPID_M_LABEL = 11</c>、<c>PROPID_M_LABEL_LEN = 12</c>、<c>PROPID_M_BODY_TYPE = 42</c>、
/// <c>PROPID_M_LOOKUPID = 60</c>；<c>PROPID_Q_BASE = 100</c>、<c>PROPID_Q_PATHNAME = 103</c>、
/// <c>PROPID_Q_TRANSACTION = 113</c>；</item>
/// <item><c>mq.h</c>：访问权限、接收动作、<c>MQ_ERROR_*</c> 错误码。</item>
/// </list>
///
/// 本封装只支持 x64 进程（与项目其它部分一致），指针大小相关的布局按 <see cref="IntPtr.Size"/> 计算。
/// </summary>
internal static class MsmqNative
{
    internal const string Dll = "mqrt.dll";

    // =========================================================================================
    // PROPVARIANT 类型（wtypes.h）
    // =========================================================================================
    internal const ushort VT_NULL = 1;
    internal const ushort VT_UI1 = 17;
    internal const ushort VT_UI4 = 19;
    internal const ushort VT_UI8 = 21;
    internal const ushort VT_LPWSTR = 31;
    internal const ushort VT_VECTOR = 0x1000;

    /// <summary>消息体/二进制属性使用 <c>VT_VECTOR | VT_UI1</c>（CAUB）。</summary>
    internal const ushort VT_VECTOR_UI1 = VT_VECTOR | VT_UI1;

    // =========================================================================================
    // 队列属性（mp.h）
    // =========================================================================================
    internal const uint PROPID_Q_PATHNAME = 103;
    internal const uint PROPID_Q_TRANSACTION = 113;

    // =========================================================================================
    // 消息属性（mp.h）
    // =========================================================================================
    internal const uint PROPID_M_BODY = 9;
    internal const uint PROPID_M_BODY_SIZE = 10;
    internal const uint PROPID_M_LABEL = 11;
    internal const uint PROPID_M_LABEL_LEN = 12;
    internal const uint PROPID_M_BODY_TYPE = 42;
    internal const uint PROPID_M_LOOKUPID = 60;

    // =========================================================================================
    // 访问权限 / 共享模式 / 接收动作（mq.h）
    // =========================================================================================
    internal const uint MQ_RECEIVE_ACCESS = 0x00000001;
    internal const uint MQ_SEND_ACCESS = 0x00000002;
    internal const uint MQ_PEEK_ACCESS = 0x00000020;
    internal const uint MQ_DENY_NONE = 0x00000000;
    internal const uint MQ_DENY_RECEIVE_SHARE = 0x00000001;

    internal const uint MQ_ACTION_RECEIVE = 0x00000000;
    internal const uint MQ_ACTION_PEEK_CURRENT = 0x80000000;
    internal const uint MQ_ACTION_PEEK_NEXT = 0x80000001;

    /// <summary>MQReceiveMessage 的无限等待（0xFFFFFFFF）。</summary>
    internal const uint MQ_INFINITE = 0xFFFFFFFF;

    /// <summary>非事务发送。</summary>
    internal static readonly IntPtr MQ_NO_TRANSACTION = IntPtr.Zero;

    /// <summary>事务性队列的单消息事务（<c>(HANDLE)-3</c>）。</summary>
    internal static readonly IntPtr MQ_SINGLE_MESSAGE = new(-3);

    // =========================================================================================
    // 错误码（mq.h）
    // =========================================================================================
    internal const int MQ_ERROR_QUEUE_NOT_FOUND = unchecked((int)0xC00E0003);
    internal const int MQ_ERROR_QUEUE_EXISTS = unchecked((int)0xC00E0005);
    internal const int MQ_ERROR_INVALID_PARAMETER = unchecked((int)0xC00E0006);
    internal const int MQ_ERROR_BUFFER_OVERFLOW = unchecked((int)0xC00E001A);
    internal const int MQ_ERROR_IO_TIMEOUT = unchecked((int)0xC00E001B);
    internal const int MQ_ERROR_INSUFFICIENT_PROPERTIES = unchecked((int)0xC00E003F);
    internal const int MQ_ERROR_ILLEGAL_QUEUE_PATHNAME = unchecked((int)0xC00E001D);

    /// <summary>队列在使用过程中被删除：该句柄已失效，必须关闭并重新打开/创建。</summary>
    internal const int MQ_ERROR_QUEUE_DELETED = unchecked((int)0xC00E005A);

    /// <summary>HRESULT 是否表示失败。</summary>
    internal static bool Failed(int hr) => hr < 0;

    /// <summary>把已知 MSMQ 错误码翻译成可读名称（未知时返回十六进制）。</summary>
    internal static string Describe(int hr) => hr switch
    {
        MQ_ERROR_QUEUE_NOT_FOUND => "MQ_ERROR_QUEUE_NOT_FOUND（队列不存在）",
        MQ_ERROR_QUEUE_EXISTS => "MQ_ERROR_QUEUE_EXISTS（队列已存在）",
        MQ_ERROR_INVALID_PARAMETER => "MQ_ERROR_INVALID_PARAMETER（参数无效）",
        MQ_ERROR_BUFFER_OVERFLOW => "MQ_ERROR_BUFFER_OVERFLOW（缓冲区不足）",
        MQ_ERROR_IO_TIMEOUT => "MQ_ERROR_IO_TIMEOUT（接收超时）",
        MQ_ERROR_INSUFFICIENT_PROPERTIES => "MQ_ERROR_INSUFFICIENT_PROPERTIES（缺少必需属性，如队列路径）",
        MQ_ERROR_ILLEGAL_QUEUE_PATHNAME => "MQ_ERROR_ILLEGAL_QUEUE_PATHNAME（队列路径非法）",
        MQ_ERROR_QUEUE_DELETED => "MQ_ERROR_QUEUE_DELETED（队列已被删除，句柄失效）",
        _ => $"0x{unchecked((uint)hr):X8}",
    };

    // =========================================================================================
    // API
    // =========================================================================================
    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQCreateQueue(IntPtr pSecurityDescriptor, IntPtr pQueueProps, IntPtr lpwcsFormatName, ref uint pdwFormatNameLength);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQDeleteQueue(IntPtr lpwcsFormatName);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQGetQueueProperties(IntPtr lpwcsFormatName, IntPtr pQueueProps);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQOpenQueue(IntPtr lpwcsFormatName, uint dwAccess, uint dwShareMode, out IntPtr phQueue);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQCloseQueue(IntPtr hQueue);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQSendMessage(IntPtr hDestinationQueue, IntPtr pMessageProps, IntPtr hTransaction);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQReceiveMessage(
        IntPtr hSource,
        uint dwTimeout,
        uint dwAction,
        IntPtr pMessageProps,
        IntPtr lpOverlapped,
        IntPtr fnReceiveCallback,
        IntPtr hCursor,
        IntPtr hTransaction);

    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern int MQMarkMessageRejected(IntPtr hQueue, ulong ullLookupId);

    /// <summary>释放 MSMQ 为输出属性分配的内存（例如 <c>VT_NULL</c> 形式的 PROPID_Q_PATHNAME）。</summary>
    [DllImport(Dll, CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = false)]
    internal static extern void MQFreeMemory(IntPtr pvMemory);
}

/// <summary>MSMQ 调用失败时抛出的异常（携带原始 HRESULT）。</summary>
internal sealed class MsmqException : Exception
{
    public MsmqException(int hresult, string message)
        : base($"{message}（HRESULT=0x{unchecked((uint)hresult):X8} {MsmqNative.Describe(hresult)}）")
    {
        HResult = hresult;
    }

    public MsmqException(int hresult, string message, Exception inner)
        : base($"{message}（HRESULT=0x{unchecked((uint)hresult):X8} {MsmqNative.Describe(hresult)}）", inner)
    {
        HResult = hresult;
    }
}
