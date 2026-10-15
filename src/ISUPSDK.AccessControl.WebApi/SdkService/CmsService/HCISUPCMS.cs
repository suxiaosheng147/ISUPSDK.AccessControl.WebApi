using ISUPSDK.AccessControl.WebApi.Structure;
using System;
using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.SdkService.CmsService
{
    public class HCISUPCMS
    {
        //开启关闭监听
        public const int ENUM_UNKNOWN = -1;
        public const int ENUM_DEV_ON = 0;             //设备上线回调
        public const int ENUM_DEV_OFF = 1;               //设备下线回调
        public const int ENUM_DEV_ADDRESS_CHANGED = 2;     //设备地址发生变化
        public const int ENUM_DEV_AUTH = 3;       //Ehome5.0设备认证回调
        public const int ENUM_DEV_SESSIONKEY = 4;    //Ehome5.0设备Sessionkey回调
        public const int ENUM_DEV_DAS_REQ = 5;      //Ehome5.0设备重定向请求回调
        public const int ENUM_DEV_SESSIONKEY_REQ = 6;//EHome5.0设备sessionkey请求回调
        public const int ENUM_DEV_DAS_REREGISTER = 7;//设备重注册回调
        public const int MAX_DEVNAME_LEN = 32;

        //OpenSSL路径
        public const int NET_EHOME_CMS_INIT_CFG_LIBEAY_PATH = 0;   //设置OpenSSL的libeay32.dll/libcrypto.so所在路径
        public const int NET_EHOME_CMS_INIT_CFG_SSLEAY_PATH = 1;   //设置OpenSSL的ssleay32.dll/libssl.so所在

        //通道类型
        public const int DEMO_CHANNEL_TYPE_INVALID = -1;
        public const int DEMO_CHANNEL_TYPE_ANALOG = 0;
        public const int DEMO_CHANNEL_TYPE_IP = 1;
        public const int DEMO_CHANNEL_TYPE_ZERO = 2; //零通道

        public const int MAX_SERIALNO_LEN = 128;    //序列号最大长度
        public const int MAX_PHOMENUM_LEN = 32;     //手机号码最大长度
        public const int MAX_DEVICE_NAME_LEN = 32;  //设备名称长度
        public const int NET_EHOME_GET_GPS_CFG = 20; //获取GPS参数
        public const int NET_EHOME_SET_GPS_CFG = 21; //设置GPS参数
        public const int NET_EHOME_GET_PIC_CFG = 22; //获取OSD显示参数
        public const int NET_EHOME_SET_PIC_CFG = 23; //设置OSD显示参数
        public const int MAX_EHOME_PROTOCOL_LEN = 1500;
        public const int IPADDRESS_LENGTH = 128;//IP地址数组的长度

        public const String CONFIG_GET_PARAM_XML = "<Params>\r\n<ConfigCmd>{0}</ConfigCmd>\r\n<ConfigParam1>{1}</ConfigParam1>\r\n<ConfigParam2>{2}</ConfigParam2>\r\n<ConfigParam3>{3}</ConfigParam3>\r\n<ConfigParam4>{4}</ConfigParam4>\r\n</Params>\r\n";
        public const String CONFIG_SET_PARAM_XML = "<Params>\r\n<ConfigCmd>{0}</ConfigCmd>\r\n<ConfigParam1>{1}</ConfigParam1>\r\n<ConfigParam2>{2}</ConfigParam2>\r\n<ConfigParam3>{3}</ConfigParam3>\r\n<ConfigParam4>{4}</ConfigParam4>\r\n<ConfigXML>{5}</ConfigXML>\r\n</Params>\r\n";

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_CONFIG
        {
            public IntPtr pCondBuf;    //[in]，条件数据指针，如表示通道号等
            public uint dwCondSize; //[in]，pCondBuf指向的数据大小
            public IntPtr pInBuf;        //[in]，设置时需要用到，指向结构体的指针
            public uint dwInSize;    //[in], pInBuf指向的数据大小
            public IntPtr pOutBuf;        //[out]，获取时需要用到，指向结构体的指针，内存由上层分配
            public uint dwOutSize;    //[in]，获取时需要用到，表示pOutBuf指向的内存大小， 
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 40, ArraySubType = UnmanagedType.U1)]
            public byte[] byRes;    //保留
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_PTXML_PARAM
        {
            public IntPtr pRequestUrl;        // 请求URL
            public uint dwRequestUrlLen;       // 请求URL长度
            public IntPtr pCondBuffer;        // 条件缓冲区（XML格式数据）
            public uint dwCondSize;            // 条件缓冲区大小
            public IntPtr pInBuffer;          // 输入缓冲区（XML格式数据）
            public uint dwInSize;              // 输入缓冲区大小
            public IntPtr pOutBuffer;         // 输出缓冲区（XML格式数据）
            public uint dwOutSize;             // 输出缓冲区大小
            public uint dwReturnedXMLLen;      // 实际从设备接收到的XML数据的长度
            public uint dwRecvTimeOut;         // 默认5000ms
            //public uint dwHandle;              // （输出参数）设置了回放异步回调之后，该值为消息句柄，回调中用于标识（新增）
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] byRes;              // 保留
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_XML_CFG
        {
            public IntPtr pCmdBuf;    //字符串格式命令
            public uint dwCmdLen;   //pCmdBuf长度
            public IntPtr pInBuf;     //输入数据
            public uint dwInSize;   //输入数据长度
            public IntPtr pOutBuf;    //输出缓冲
            public uint dwOutSize;  //输出缓冲区长度
            public uint dwSendTimeOut;  //数据发送超时时间,单位ms，默认5s
            public uint dwRecvTimeOut;  //数据接收超时时间,单位ms，默认5s
            public IntPtr pStatusBuf;     //返回的状态参数(XML格式),如果不需要,可以置NULL
            public uint dwStatusSize;   //状态缓冲区大小(内存大小)
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] byRes;
        }
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_DEV_REG_INFO
        {
            public int dwSize;
            public int dwNetUnitType;            //根据EHomeSDK协议预留，目前没有意义
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.MAX_DEVICE_ID_LEN)]
            public byte[] byDeviceID; //设备ID
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] byFirmwareVersion;    //固件版本
            public HCEHomePublic.NET_EHOME_IPADDRESS struDevAdd;         //设备注册上来是，设备的本地地址
            public int dwDevType;                  //设备类型
            public int dwManufacture;              //设备厂家代码
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] byPassWord;             //设备登陆CMS的密码，由用户自行根据需求进行验证
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.NET_EHOME_SERIAL_LEN/*12*/)]
            public byte[] sDeviceSerial;    //设备序列号，数字序列号
            public byte byReliableTransmission;
            public byte byWebSocketTransmission;
            public byte bySupportRedirect;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] byDevProtocolVersion;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.MAX_MASTER_KEY_LEN)]
            public byte[] bySessionKey;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 27)]
            public byte[] byRes;
            public void Init()
            {
                byDeviceID = new byte[HCEHomePublic.MAX_DEVICE_ID_LEN];
                byFirmwareVersion = new byte[24];
                byPassWord = new byte[32];
                sDeviceSerial = new byte[HCEHomePublic.NET_EHOME_SERIAL_LEN];
                byDevProtocolVersion = new byte[6];
                bySessionKey = new byte[HCEHomePublic.MAX_MASTER_KEY_LEN];
                byRes = new byte[27];
            }
        }
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_XML_REMOTE_CTRL_PARAM
        {
            public uint dwSize;
            public IntPtr lpInbuffer;          //input param buffer
            public uint dwInBufferSize;      //size of input param buffer
            public uint dwSendTimeOut;  //send time out,unit ms，default 5s
            public uint dwRecvTimeOut;  //receive time out,unit ms，default 5s
            public IntPtr lpOutBuffer;     //output buffer
            public uint dwOutBufferSize;  //size of output buffer
            public IntPtr lpStatusBuffer;   //status buffer,if not user can set NULL
            public uint dwStatusBufferSize;  //status buffer size
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] byRes;
        }
        public struct NET_EHOME_IPADDRESS
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 128, ArraySubType = UnmanagedType.U1)]
            public char[] szIP;
            public Int16 wPort;     //端口
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] byRes;
            public void Init()
            {
                wPort = 0;
                szIP = new char[128];
                byRes = new byte[2];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_DEV_REG_INFO_V12
        {
            public NET_EHOME_DEV_REG_INFO struRegInfo;
            public NET_EHOME_IPADDRESS struRegAddr;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = MAX_DEVNAME_LEN)]
            public byte[] sDevName;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 224)]
            public byte[] byRes;
            public void Init()
            {
                struRegInfo.Init();
                struRegAddr.Init();
                sDevName = new byte[MAX_DEVNAME_LEN];
                byRes = new byte[224];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_BLACKLIST_SEVER
        {
            public HCEHomePublic.NET_EHOME_IPADDRESS struAdd;         //服务器地址
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.NAME_LEN/*32*/)]
            public byte[] byServerName;                            //服务器名称
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.NAME_LEN/*32*/)]
            public byte[] byUserName;                              //用户名
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = HCEHomePublic.NAME_LEN/*32*/)]
            public byte[] byPassWord;                              //密码
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64/*64*/)]
            public byte[] byRes;
            public void Init()
            {
                struAdd.Init();
                byServerName = new byte[HCEHomePublic.NAME_LEN];
                byUserName = new byte[HCEHomePublic.NAME_LEN];
                byPassWord = new byte[HCEHomePublic.NAME_LEN];
                byRes = new byte[64];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SERVER_INFO
        {
            public int dwSize;
            public int dwKeepAliveSec;            //心跳间隔（单位：秒,0:默认为15S）
            public int dwTimeOutCount;         //心跳超时次数（0：默认为6）
            public HCEHomePublic.NET_EHOME_IPADDRESS struTCPAlarmSever;      //报警服务器地址（TCP协议）
            public HCEHomePublic.NET_EHOME_IPADDRESS struUDPAlarmSever;        //报警服务器地址（UDP协议）
            public int dwAlarmServerType;        //报警服务器类型0-只支持UDP协议上报，1-支持UDP、TCP两种协议上报
            public HCEHomePublic.NET_EHOME_IPADDRESS struNTPSever;            //NTP服务器地址
            public int dwNTPInterval;            //NTP校时间隔（单位：秒）
            public HCEHomePublic.NET_EHOME_IPADDRESS struPictureSever;       //图片服务器地址
            public int dwPicServerType;        //图片服务器类型图片服务器类型，1-VRB图片服务器，0-Tomcat图片服务
            public NET_EHOME_BLACKLIST_SEVER struBlackListServer;    //黑名单服务器
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 128/*128*/)]
            public byte[] byRes;
            public void Init()
            {
                struTCPAlarmSever.Init();
                struUDPAlarmSever.Init();
                struNTPSever.Init();
                struPictureSever.Init();
                struBlackListServer.Init();
                byRes = new byte[128];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SERVER_INFO_V50
        {
            public int dwSize;
            public int dwKeepAliveSec;            //心跳间隔（单位：秒,0:默认为15S）
            public int dwTimeOutCount;         //心跳超时次数（0：默认为6）
            public HCEHomePublic.NET_EHOME_IPADDRESS struTCPAlarmSever;      //报警服务器地址（TCP协议）
            public HCEHomePublic.NET_EHOME_IPADDRESS struUDPAlarmSever;        //报警服务器地址（UDP协议）
            public int dwAlarmServerType;        //报警服务器类型0-只支持UDP协议上报，1-支持UDP、TCP两种协议上报
            public HCEHomePublic.NET_EHOME_IPADDRESS struNTPSever;            //NTP服务器地址
            public int dwNTPInterval;            //NTP校时间隔（单位：秒）
            public HCEHomePublic.NET_EHOME_IPADDRESS struPictureSever;       //图片服务器地址
            public int dwPicServerType;        //图片服务器类型图片服务器类型，1-VRB图片服务器，0-Tomcat图片服务,2-云存储3,3-KMS
            public NET_EHOME_BLACKLIST_SEVER struBlackListServer;    //黑名单服务器
            public HCEHomePublic.NET_EHOME_IPADDRESS struRedirectSever;       //Redirect Server
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] byClouldAccessKey; //云存储AK
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] byClouldSecretKey; //云存储SK
            public byte byClouldHttps;//云存储HTTPS使能 1-HTTPS 0-HTTP
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 383)]
            public byte[] byRes;

            public void Init()
            {
                struTCPAlarmSever.Init();
                struUDPAlarmSever.Init();
                struNTPSever.Init();
                struPictureSever.Init();
                struBlackListServer.Init();
                struRedirectSever.Init();
                byClouldAccessKey = new byte[64];
                byClouldSecretKey = new byte[64];
                byRes = new byte[383];
            }
        }


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_CMS_LISTEN_PARAM
        {
            public HCEHomePublic.NET_EHOME_IPADDRESS struAddress; //本地监听信息，IP为0.0.0.0的情况下，默认为本地地址，多个网卡的情况下，默认为从操作系统获取到的第一个
            public DEVICE_REGISTER_CB fnCB; //报警信息回调函数
            public IntPtr pUserData;   //用户数据
            public int dwKeepAliveSec; //心跳间隔（单位：秒,0:默认为30S）
            public int dwTimeOutCount; //心跳超时次数（0：默认为3）
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 24)]
            public byte[] byRes;
        }
















        #region
        //SDK初始化
        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_Init();
        //SDK反初始化
        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_Fini();
        //开启关闭监听
        [DllImport("HCISUPCMS.dll")]
        public static extern int NET_ECMS_StartListen(ref NET_EHOME_CMS_LISTEN_PARAM lpCMSListenPara);
        [DllImport("HCISUPCMS.dll")]
        public static extern bool NET_ECMS_StopListen(Int32 iHandle);
        //注销设备
        [DllImport("HCISUPCMS.dll")]
        public static extern bool NET_ECMS_ForceLogout(Int32 lUserID);
        //日志
        [DllImport(@"HCISUPCMS.dll")]//, CallingConvention = CallingConvention.Cdecl)
        public static extern bool NET_ECMS_SetLogToFile(Int32 iLogLevel, string szLogDir, bool bAutoDel);
        [DllImport(@"HCISUPCMS.dll")]//, CallingConvention = CallingConvention.Cdecl)
        public static extern bool NET_ECMS_SetLogToFile(Int32 iLogLevel, char[] szLogDir, bool bAutoDel);
        //注册回调函数
        public delegate bool DEVICE_REGISTER_CB(int lUserID, int dwDataType, IntPtr pOutBuffer, uint dwOutLen,
                                                IntPtr pInBuffer, uint dwInLen, IntPtr pUser);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_SetDeviceSessionKey(ref HCEHomePublic.NET_EHOME_DEV_SESSIONKEY pDeviceKey);

        [DllImport("HCISUPCMS.dll")]
        public static extern long NET_ECMS_GetBuildVersion();
        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE enumType, IntPtr pInBuff);

        [DllImport("HCISUPCMS.dll")]
        public static extern bool NET_ECMS_SetSDKInitCfg(Int32 enumType, IntPtr lpInBuff);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern int NET_ECMS_GetLastError();

        //ISAPI 
        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_GetPTXMLConfig(Int32 iUserID, IntPtr lpPTXMLParam);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_PutPTXMLConfig(Int32 iUserID, IntPtr lpPTXMLParam);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_PostPTXMLConfig(Int32 iUserID, IntPtr lpPTXMLParam);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_DeletePTXMLConfig(Int32 lUserID, IntPtr lpPTXMLParam);
        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_ISAPIPassThrough(Int32 lUserID, IntPtr lpParam);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_XMLConfig(Int32 lUserID, IntPtr pXmlCfg, Int32 dwConfigSize);

        [DllImport(@"HCISUPCMS.dll")]
        public static extern bool NET_ECMS_XMLRemoteControl(Int32 lUserID, IntPtr lpCtrlParam, Int32 dwCtrlSize);
        #endregion


    }
}
