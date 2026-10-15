using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue.Msmq;

/// <summary>
/// MSMQ 队列的轻量同步封装：探测/创建队列、打开收发句柄、发送与接收消息。
///
/// 生命周期：<see cref="EnsureQueue"/>（可自动建队列）→ <see cref="Open"/>（取到收发句柄）→
/// <see cref="Send"/>/<see cref="TryReceive"/> → <see cref="Dispose"/>。
/// </summary>
internal sealed class MsmqQueue : IDisposable
{
    /// <summary>format name 输出缓冲（MSMQ 上限 256 个宽字符）。</summary>
    private const int FormatNameBufferChars = 512;

    /// <summary>生命周期（打开/关闭句柄）互斥。</summary>
    private readonly object _lifecycleSync = new();

    /// <summary>发送互斥。与接收分开，避免发送被阻塞式的 MQReceiveMessage 拖住。</summary>
    private readonly object _sendSync = new();

    /// <summary>接收互斥（MQReceiveMessage 是阻塞调用，可能持有较久）。</summary>
    private readonly object _receiveSync = new();

    private readonly bool _transactional;

    private IntPtr _sendHandle = IntPtr.Zero;
    private IntPtr _receiveHandle = IntPtr.Zero;

    public MsmqQueue(string path, bool transactional)
    {
        Path = path;
        FormatName = path;
        _transactional = transactional;
    }

    /// <summary>队列路径（如 <c>.\Private$\acs-events</c>）。</summary>
    public string Path { get; }

    /// <summary>队列的 format name（创建队列后由 MSMQ 返回）。</summary>
    public string FormatName { get; private set; }

    /// <summary>是否已打开收发句柄。</summary>
    public bool IsOpen => _sendHandle != IntPtr.Zero && _receiveHandle != IntPtr.Zero;

    /// <summary>
    /// 探测队列是否存在：尝试打开队列，成功即存在（句柄立即关闭）。
    ///
    /// 说明：<c>MQOpenQueue</c> 对私有队列只可靠地接受 **format name**
    /// （<c>DIRECT=OS:机器名\Private$\队列名</c>）；<c>.\Private$\name</c> 这类路径形式会失败，
    /// 但 <c>MQCreateQueue</c> 又需要它。因此这里按候选名逐个尝试。
    /// </summary>
    public static bool Exists(string path)
    {
        foreach (var candidate in CandidateNames(path))
        {
            var pathPtr = Marshal.StringToHGlobalUni(candidate);
            try
            {
                var hr = MsmqNative.MQOpenQueue(pathPtr, MsmqNative.MQ_RECEIVE_ACCESS, MsmqNative.MQ_DENY_NONE, out var handle);
                if (!MsmqNative.Failed(hr))
                {
                    MsmqNative.MQCloseQueue(handle);
                    return true;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }

        return false;
    }

    /// <summary>把 <c>.\Private$\name</c> 规范化为 <c>机器名\Private$\name</c>（其余形式原样返回）。</summary>
    public static string NormalizePath(string path)
        => path.StartsWith(@".\", StringComparison.Ordinal)
            ? Environment.MachineName + path[1..]
            : path;

    /// <summary>由队列路径推导可用的候选标识：原样 → 带机器名的路径 → <c>DIRECT=OS</c> format name。</summary>
    public static IEnumerable<string> CandidateNames(string path)
    {
        yield return path;

        var normalized = NormalizePath(path);
        if (!string.Equals(normalized, path, StringComparison.OrdinalIgnoreCase))
        {
            yield return normalized;
        }

        const string marker = @"\Private$\";
        var index = normalized.IndexOf(marker, StringComparison.OrdinalIgnoreCase);

        if (index > 0)
        {
            var machine = normalized[..index];
            var queueName = normalized[(index + marker.Length)..];
            yield return $@"DIRECT=OS:{machine}\Private$\{queueName}";
        }
        else if (normalized.StartsWith(@"Private$\", StringComparison.OrdinalIgnoreCase))
        {
            yield return $@"DIRECT=OS:{Environment.MachineName}\{normalized}";
        }
    }

    /// <summary>
    /// 删除队列（测试或运维清理用）。
    ///
    /// 同 <see cref="Exists"/>：按候选标识逐个尝试，私有队列通常需要 format name 形式。
    /// </summary>
    public static void Delete(string path)
    {
        var lastError = MsmqNative.MQ_ERROR_QUEUE_NOT_FOUND;

        foreach (var candidate in CandidateNames(path))
        {
            var pathPtr = Marshal.StringToHGlobalUni(candidate);
            try
            {
                var hr = MsmqNative.MQDeleteQueue(pathPtr);
                if (hr == 0 || hr == MsmqNative.MQ_ERROR_QUEUE_NOT_FOUND)
                {
                    return;
                }

                lastError = hr;
            }
            finally
            {
                Marshal.FreeHGlobal(pathPtr);
            }
        }

        throw new MsmqException(lastError, $"删除队列失败：{path}");
    }

    /// <summary>确保队列可用（按需创建），并把 <see cref="FormatName"/> 填好。</summary>
    public void EnsureQueue(bool createIfMissing)
    {
        if (createIfMissing)
        {
            FormatName = CreateOrGetFormatName();
            return;
        }

        if (!Exists(Path))
        {
            throw new MsmqException(
                MsmqNative.MQ_ERROR_QUEUE_NOT_FOUND,
                $"队列不存在且未允许自动创建：{Path}（请在\"启用或关闭 Windows 功能\"中启用 MSMQ，或先在计算机管理中创建该私有队列）");
        }

        FormatName = NormalizePath(Path);
    }

    /// <summary>创建队列并返回其 format name；队列已存在时直接复用。</summary>
    private string CreateOrGetFormatName()
    {
        using var props = new MsmqPropsBuilder();
        props.AddString(MsmqNative.PROPID_Q_PATHNAME, Path);
        props.AddUI1(MsmqNative.PROPID_Q_TRANSACTION, (byte)(_transactional ? 1 : 0));

        var nameBuffer = Marshal.AllocHGlobal(FormatNameBufferChars * sizeof(char));
        try
        {
            var length = (uint)FormatNameBufferChars;
            var hr = MsmqNative.MQCreateQueue(IntPtr.Zero, props.Build(includeStatus: false), nameBuffer, ref length);

            if (hr == 0)
            {
                return Marshal.PtrToStringUni(nameBuffer) ?? Path;
            }

            // 已存在（先前创建过或并发创建）：直接复用现有队列。
            if (hr == MsmqNative.MQ_ERROR_QUEUE_EXISTS)
            {
                return Path;
            }

            throw new MsmqException(hr, $"创建队列失败：{Path}");
        }
        finally
        {
            Marshal.FreeHGlobal(nameBuffer);
        }
    }

    /// <summary>
    /// 打开收发句柄（可重复调用，旧的句柄会被关闭）。
    ///
    /// <c>MQOpenQueue</c> 对私有队列只可靠地接受 format name 形式，因此这里按
    /// <see cref="FormatName"/> → 路径候选 → format name 候选的顺序逐个尝试，成功即记录可用形式。
    /// </summary>
    public void Open()
    {
        lock (_lifecycleSync)
        {
            CloseCore();

            var candidates = new List<string> { FormatName };
            candidates.AddRange(CandidateNames(Path));
            candidates.AddRange(CandidateNames(FormatName));

            var lastError = MsmqNative.MQ_ERROR_INVALID_PARAMETER;

            foreach (var name in candidates
                         .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var namePtr = Marshal.StringToHGlobalUni(name);
                try
                {
                    var hr = MsmqNative.MQOpenQueue(namePtr, MsmqNative.MQ_SEND_ACCESS, MsmqNative.MQ_DENY_NONE, out var sendHandle);
                    if (MsmqNative.Failed(hr))
                    {
                        lastError = hr;
                        continue;
                    }

                    hr = MsmqNative.MQOpenQueue(namePtr, MsmqNative.MQ_RECEIVE_ACCESS, MsmqNative.MQ_DENY_NONE, out var receiveHandle);
                    if (MsmqNative.Failed(hr))
                    {
                        MsmqNative.MQCloseQueue(sendHandle);
                        lastError = hr;
                        continue;
                    }

                    _sendHandle = sendHandle;
                    _receiveHandle = receiveHandle;
                    FormatName = name;
                    return;
                }
                finally
                {
                    Marshal.FreeHGlobal(namePtr);
                }
            }

            throw new MsmqException(lastError, $"打开队列失败：{Path}");
        }
    }

    /// <summary>发送一条消息（失败抛 <see cref="MsmqException"/>）。</summary>
    public void Send(byte[] body, string? label)
    {
        lock (_sendSync)
        {
            var handle = _sendHandle;
            if (handle == IntPtr.Zero)
            {
                throw new MsmqException(MsmqNative.MQ_ERROR_INVALID_PARAMETER, "队列尚未打开（发送句柄为空）。");
            }

            using var props = new MsmqPropsBuilder();
            if (!string.IsNullOrEmpty(label))
            {
                props.AddString(MsmqNative.PROPID_M_LABEL, label);
                props.AddUI4(MsmqNative.PROPID_M_LABEL_LEN, (uint)(label.Length + 1));
            }

            props.AddBytes(MsmqNative.PROPID_M_BODY, body);
            props.AddUI4(MsmqNative.PROPID_M_BODY_SIZE, (uint)body.Length);
            props.AddUI4(MsmqNative.PROPID_M_BODY_TYPE, MsmqNative.VT_UI1);

            var transaction = _transactional ? MsmqNative.MQ_SINGLE_MESSAGE : MsmqNative.MQ_NO_TRANSACTION;
            var hr = MsmqNative.MQSendMessage(handle, props.Build(), transaction);
            if (MsmqNative.Failed(hr))
            {
                throw new MsmqException(hr, "MQSendMessage 失败");
            }

            var bodyStatus = props.ReadStatus(MsmqNative.PROPID_M_BODY);
            if (MsmqNative.Failed(bodyStatus))
            {
                throw new MsmqException(bodyStatus, "MQSendMessage：消息体属性被拒绝");
            }
        }
    }

    /// <summary>
    /// 接收一条消息（同步阻塞，最长 <paramref name="timeout"/>）。
    /// 返回 false 表示超时（队列为空）；缓冲区不足时自动按实际长度重试。
    /// </summary>
    public bool TryReceive(TimeSpan timeout, int maxBodyBytes, out byte[] body, out ulong lookupId)
    {
        body = Array.Empty<byte>();
        lookupId = 0;

        var bufferSize = Math.Max(maxBodyBytes, 1024);
        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            var timeoutMs = timeout <= TimeSpan.Zero ? 0u : (uint)Math.Min(timeout.TotalMilliseconds, uint.MaxValue);

            lock (_receiveSync)
            {
                var handle = _receiveHandle;
                if (handle == IntPtr.Zero)
                {
                    throw new MsmqException(MsmqNative.MQ_ERROR_INVALID_PARAMETER, "队列尚未打开（接收句柄为空）。");
                }

                while (true)
                {
                    using var props = new MsmqPropsBuilder();
                    props.AddUI4(MsmqNative.PROPID_M_BODY_SIZE);
                    props.AddBuffer(MsmqNative.PROPID_M_BODY, buffer, bufferSize);
                    props.AddUI8(MsmqNative.PROPID_M_LOOKUPID);

                    var hr = MsmqNative.MQReceiveMessage(
                        handle,
                        timeoutMs,
                        MsmqNative.MQ_ACTION_RECEIVE,
                        props.Build(),
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        MsmqNative.MQ_NO_TRANSACTION);

                    if (hr == MsmqNative.MQ_ERROR_IO_TIMEOUT)
                    {
                        return false;
                    }

                    if (hr == MsmqNative.MQ_ERROR_BUFFER_OVERFLOW)
                    {
                        // 缓冲区不足：消息未被移除，按返回的实际长度扩容后重试。
                        var required = (int)props.ReadUInt32(MsmqNative.PROPID_M_BODY_SIZE);
                        if (required <= bufferSize)
                        {
                            throw new MsmqException(hr, "MQReceiveMessage：消息体缓冲区不足，但未返回有效长度");
                        }

                        buffer = Marshal.ReAllocHGlobal(buffer, required);
                        bufferSize = required;
                        continue;
                    }

                    if (MsmqNative.Failed(hr))
                    {
                        throw new MsmqException(hr, "MQReceiveMessage 失败");
                    }

                    var size = (int)props.ReadUInt32(MsmqNative.PROPID_M_BODY_SIZE);
                    lookupId = props.ReadUInt64(MsmqNative.PROPID_M_LOOKUPID);

                    var length = Math.Clamp(size, 0, bufferSize);
                    body = new byte[length];
                    if (length > 0)
                    {
                        Marshal.Copy(buffer, body, 0, length);
                    }

                    return true;
                }
            }
        }
        finally
        {
            if (buffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }

    public void Dispose()
    {
        lock (_lifecycleSync)
        {
            CloseCore();
        }
    }

    private void CloseCore()
    {
        if (_receiveHandle != IntPtr.Zero)
        {
            MsmqNative.MQCloseQueue(_receiveHandle);
            _receiveHandle = IntPtr.Zero;
        }

        if (_sendHandle != IntPtr.Zero)
        {
            MsmqNative.MQCloseQueue(_sendHandle);
            _sendHandle = IntPtr.Zero;
        }
    }
}
