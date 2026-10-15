using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.SdkService
{
    public static class ISUPSDK
    {
        public const int CP_UTF8 = 65001;
        public const int CP_ACP = 0;

        [StructLayout(LayoutKind.Sequential)]
        public struct PREVIEW_IFNO
        {
            public int iDeviceIndex;  	//device index
            public int iChanIndex;  	//channel index
            public byte PanelNo;
            public int lRealHandle;
            public IntPtr hPlayWnd;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_IPADDRESS
        {
            public byte[] szIP;
            public System.Int16 lPort;     //port
            public byte[] byRes;
            public void Init()
            {
                szIP = new byte[128];
                byRes = new byte[2];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_ALARM_MSG
        {
            private System.Int32 dwAlarmType;      //device type, EN_ALARM_TYPE
            private IntPtr pAlarmInfo;              //The content of the alarm(structure)
            private System.Int32 dwAlarmInfoLen;   //the length of alarm content(structure)
            private IntPtr pXmlBuf;                 //the content of the alarm XML
            private System.Int32 dwXmlBufLen;      //the length of alarm content(XML)
            private String sSerialNumber;
            private byte byDataType;        //alarm data type,0-XML, 1-structure
            private byte[] byRes;
            public void Init()
            {
                byRes = new byte[3];
            }
        }

        public delegate bool MSGCallBack(long iHandle, ref NET_EHOME_ALARM_MSG pAlarmMsg, IntPtr pUser);


        public struct NET_EHOME_ALARM_LISTEN_PARAM
        {
            private NET_EHOME_IPADDRESS struAddress;  //Local listening information, if the IP address is *.0.0.0, it is considered as the local address. 
            private MSGCallBack fnMsgCb; // Alarm listening callback function 
            private IntPtr pUserData;   //User data 
            private byte byProtocolType;  //Connection mode, 0- TCP, 1- UDP 
            private byte byUseCmsPort; //Use CMS listen port,0-not use,!0-use，if use CMS port, byProtoclType is invalid , struAddress is loopback address
            private byte byUseThreadPool;  //0-use thread pool when callback alarm，1-dose not use thread pool when callback alarm. default: use thread pool
            private byte[] byRes;
        }

        //本地配置
        public enum NET_EHOME_LOCAL_CFG_TYPE
        {
            UNDEFINE = -1,   //暂时没有具体的定义
            ACTIVE_ACCESS_SECURITY = 0, //设备主动接入的安全性
            AMS_ADDRESS,                //报警服务器本地回环地址
            SEND_PARAM,                 //发送参数配置
        }

        [DllImport("kernel32.dll")]
        public static extern int MultiByteToWideChar(int CodePage, int dwFlags, string lpMultiByteStr, int cchMultiByte, [MarshalAs(UnmanagedType.LPWStr)] string lpWideCharStr, int cchWideChar);

        [DllImport("Kernel32.dll")]
        public static extern int WideCharToMultiByte(uint CodePage, uint dwFlags, [In, MarshalAs(UnmanagedType.LPWStr)] string lpWideCharStr, int cchWideChar,
        [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder lpMultiByteStr, int cbMultiByte, IntPtr lpDefaultChar, // Defined as IntPtr because in most cases is better to pass
        IntPtr lpUsedDefaultChar);
    }
}
