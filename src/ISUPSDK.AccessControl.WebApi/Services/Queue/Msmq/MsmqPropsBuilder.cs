using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.Services.Queue.Msmq;

/// <summary>
/// 构造 MSMQ 的 <c>MQMSGPROPS</c> / <c>MQQUEUEPROPS</c>（两者结构一致：cProp + aPropID + aPropVar + aStatus）
/// 与其中的 <c>MQPROPVARIANT</c> 数组。
///
/// 全部使用非托管内存手工布局（而不是托管结构体数组），以便精确控制
/// <c>PROPVARIANT</c> 的联合体对齐；所有分配的内存由 <see cref="Dispose"/> 统一释放。
/// </summary>
internal sealed class MsmqPropsBuilder : IDisposable
{
    /// <summary>PROPVARIANT：<c>VARTYPE vt</c> + 3 个保留 WORD（8 字节）+ 联合体指针大小。</summary>
    private static readonly int VariantHeaderSize = 8;

    private static readonly int VariantSize = 8 + (IntPtr.Size == 8 ? 16 : 8);

    /// <summary>CAUB.pElems 在 PROPVARIANT 内的偏移（x64 需按 8 字节对齐）。</summary>
    private static readonly int VectorPointerOffset = 8 + (IntPtr.Size == 8 ? 8 : 4);

    /// <summary>MQMSGPROPS.aPropID 的偏移。</summary>
    private static readonly int PropsPropIdOffset = IntPtr.Size == 8 ? 8 : 4;

    private readonly List<uint> _ids = new();
    private readonly List<IntPtr> _variants = new();
    private readonly List<IntPtr> _owned = new();

    private IntPtr _props = IntPtr.Zero;
    private IntPtr _idsPtr = IntPtr.Zero;
    private IntPtr _variantsPtr = IntPtr.Zero;
    private IntPtr _statusPtr = IntPtr.Zero;

    /// <summary>属性个数。</summary>
    public int Count => _ids.Count;

    /// <summary>查找属性在集合中的序号（未添加返回 -1）。</summary>
    public int IndexOf(uint propId)
    {
        for (var i = 0; i < _ids.Count; i++)
        {
            if (_ids[i] == propId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>添加 <c>VT_UI1</c> 属性（如 PROPID_Q_TRANSACTION）。</summary>
    public void AddUI1(uint propId, byte value)
    {
        var variant = NewVariant(MsmqNative.VT_UI1);
        Marshal.WriteByte(variant, VariantHeaderSize, value);
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_UI4</c> 属性（输入/输出均可，输出时初值为 0）。</summary>
    public void AddUI4(uint propId, uint value = 0)
    {
        var variant = NewVariant(MsmqNative.VT_UI4);
        Marshal.WriteInt32(variant, VariantHeaderSize, unchecked((int)value));
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_UI8</c> 输出属性（如 PROPID_M_LOOKUPID）。</summary>
    public void AddUI8(uint propId)
    {
        NewVariant(MsmqNative.VT_UI8);
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_LPWSTR</c> 属性（如队列路径、消息标签）。</summary>
    public void AddString(uint propId, string value)
    {
        var variant = NewVariant(MsmqNative.VT_LPWSTR);
        var buffer = Marshal.StringToHGlobalUni(value);
        _owned.Add(buffer);
        Marshal.WriteIntPtr(variant, VariantHeaderSize, buffer);
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_VECTOR|VT_UI1</c> 输入属性（发送消息体等）。</summary>
    public void AddBytes(uint propId, byte[] data)
    {
        var variant = NewVariant(MsmqNative.VT_VECTOR_UI1);
        var size = Math.Max(data.Length, 1);
        var buffer = Marshal.AllocHGlobal(size);
        _owned.Add(buffer);
        if (data.Length > 0)
        {
            Marshal.Copy(data, 0, buffer, data.Length);
        }
        else
        {
            Marshal.WriteByte(buffer, 0, 0);
        }

        Marshal.WriteInt32(variant, VariantHeaderSize, data.Length);
        Marshal.WriteIntPtr(variant, VectorPointerOffset, buffer);
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_VECTOR|VT_UI1</c> 输出属性（接收消息体，缓冲区由调用方提供/释放）。</summary>
    public void AddBuffer(uint propId, IntPtr buffer, int size)
    {
        var variant = NewVariant(MsmqNative.VT_VECTOR_UI1);
        Marshal.WriteInt32(variant, VariantHeaderSize, size);
        Marshal.WriteIntPtr(variant, VectorPointerOffset, buffer);
        _ids.Add(propId);
    }

    /// <summary>添加 <c>VT_NULL</c> 占位属性。</summary>
    public void AddNull(uint propId)
    {
        NewVariant(MsmqNative.VT_NULL);
        _ids.Add(propId);
    }

    /// <summary>
    /// 构建 MQMSGPROPS/MQQUEUEPROPS 结构，返回其非托管地址（重复调用返回同一地址）。
    /// </summary>
    /// <param name="includeStatus">
    /// 是否分配 aStatus（逐属性 HRESULT 数组）。消息属性需要它来读取 body 的校验结果；
    /// <c>MQCreateQueue</c>/<c>MQGetQueueProperties</c> 用不到，可传 false（等价于 C 里的 NULL）。
    /// </param>
    public IntPtr Build(bool includeStatus = true)
    {
        if (_props != IntPtr.Zero)
        {
            return _props;
        }

        var count = _ids.Count;
        var capacity = Math.Max(count, 1);

        _idsPtr = Marshal.AllocHGlobal(capacity * sizeof(uint));
        _owned.Add(_idsPtr);
        Zero(_idsPtr, capacity * sizeof(uint));
        for (var i = 0; i < count; i++)
        {
            Marshal.WriteInt32(_idsPtr, i * sizeof(uint), unchecked((int)_ids[i]));
        }

        // 关键：MQMSGPROPS.aPropVar 指向的是 MQPROPVARIANT 的“结构体数组”（连续布局），
        // 不是指针数组。这里把逐个构造的变体拷贝进一块连续内存，并把索引重定向到块内地址。
        _variantsPtr = Marshal.AllocHGlobal(capacity * VariantSize);
        _owned.Add(_variantsPtr);
        Zero(_variantsPtr, capacity * VariantSize);
        for (var i = 0; i < count; i++)
        {
            var target = _variantsPtr + (i * VariantSize);
            CopyVariant(_variants[i], target);
            _variants[i] = target;
        }

        if (includeStatus)
        {
            _statusPtr = Marshal.AllocHGlobal(capacity * sizeof(int));
            _owned.Add(_statusPtr);
            Zero(_statusPtr, capacity * sizeof(int));
        }
        else
        {
            _statusPtr = IntPtr.Zero;
        }

        // MQMSGPROPS 的四个成员：cProp / aPropID / aPropVar / aStatus（后三个均按指针大小对齐）。
        var propsSize = PropsPropIdOffset + (3 * IntPtr.Size);
        _props = Marshal.AllocHGlobal(propsSize);
        _owned.Add(_props);
        Zero(_props, propsSize);
        Marshal.WriteInt32(_props, 0, count);
        Marshal.WriteIntPtr(_props, PropsPropIdOffset, _idsPtr);
        Marshal.WriteIntPtr(_props, PropsPropIdOffset + IntPtr.Size, _variantsPtr);
        Marshal.WriteIntPtr(_props, PropsPropIdOffset + (2 * IntPtr.Size), includeStatus ? _statusPtr : IntPtr.Zero);

        return _props;
    }

    /// <summary>按 8 字节为单位拷贝一个 MQPROPVARIANT（VariantSize 恒为 8 的倍数）。</summary>
    private static void CopyVariant(IntPtr source, IntPtr destination)
    {
        for (var offset = 0; offset < VariantSize; offset += 8)
        {
            Marshal.WriteInt64(destination, offset, Marshal.ReadInt64(source, offset));
        }
    }

    /// <summary>读取第 <paramref name="index"/> 个属性的 HRESULT 状态。</summary>
    public int ReadStatus(int index)
        => index >= 0 && _statusPtr != IntPtr.Zero ? Marshal.ReadInt32(_statusPtr, index * sizeof(int)) : 0;

    /// <summary>按属性 ID 读取 HRESULT 状态。</summary>
    public int ReadStatus(uint propId) => ReadStatus(IndexOf(propId));

    /// <summary>读取 <c>VT_UI4</c> 属性的值。</summary>
    public uint ReadUInt32(uint propId)
    {
        var index = IndexOf(propId);
        return index < 0 ? 0u : unchecked((uint)Marshal.ReadInt32(_variants[index], VariantHeaderSize));
    }

    /// <summary>读取 <c>VT_UI8</c> 属性的值。</summary>
    public ulong ReadUInt64(uint propId)
    {
        var index = IndexOf(propId);
        return index < 0 ? 0ul : unchecked((ulong)Marshal.ReadInt64(_variants[index], VariantHeaderSize));
    }

    /// <summary>
    /// 读取输出属性的指针值（<c>VT_LPWSTR</c> / 以 <c>VT_NULL</c> 请求后由 MSMQ 填充的字符串）。
    /// 这类内存由 MSMQ 分配，读完后需 <c>MQFreeMemory</c> 释放。
    /// </summary>
    public IntPtr ReadPointer(uint propId)
    {
        var index = IndexOf(propId);
        return index < 0 ? IntPtr.Zero : Marshal.ReadIntPtr(_variants[index], VariantHeaderSize);
    }

    public void Dispose()
    {
        foreach (var pointer in _owned)
        {
            if (pointer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        _owned.Clear();
        _variants.Clear();
        _ids.Clear();
        _props = _idsPtr = _variantsPtr = _statusPtr = IntPtr.Zero;
    }

    private IntPtr NewVariant(ushort vt)
    {
        var variant = Marshal.AllocHGlobal(VariantSize);
        _owned.Add(variant);
        Zero(variant, VariantSize);
        Marshal.WriteInt16(variant, 0, unchecked((short)vt));
        _variants.Add(variant);
        return variant;
    }

    private static void Zero(IntPtr pointer, int size)
        => Marshal.Copy(new byte[size], 0, pointer, size);
}
