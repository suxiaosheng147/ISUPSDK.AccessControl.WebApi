using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.DerivedClass
{
    // 定义 SafeHandle 派生类
    public sealed class SafeHGlobalHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeHGlobalHandle(int size) : base(true)
        {
            SetHandle(Marshal.AllocHGlobal(size));
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid) Marshal.FreeHGlobal(handle);
            return true;
        }
    }
}
