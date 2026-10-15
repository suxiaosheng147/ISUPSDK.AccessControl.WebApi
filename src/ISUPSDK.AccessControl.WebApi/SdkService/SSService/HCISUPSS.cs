using ISUPSDK.AccessControl.WebApi.Structure;
using System;
using System.Runtime.InteropServices;

namespace ISUPSDK.AccessControl.WebApi.SdkService.SSService
{
    public class HCISUPSS
    {
        public const int MAX_PATH = 260;
        public const int MAX_KMS_USER_LEN = 512;
        public const int MAX_KMS_PWD_LEN = 512;
        public const int MAX_CLOUD_AK_SK_LEN = 64;
        public const int MAX_URL_LEN_SS = 4096;
        public delegate bool EHomeSSRWCallBack(int iHandle, byte byAct, char[] pFileName, IntPtr pFileBuf, IntPtr dwFileLen, IntPtr pFileUrl, IntPtr pUser);
        public delegate bool EHomeSSStorageCallBack(int iHandle, IntPtr pFileName, IntPtr pFileBuf, int dwFileLen, IntPtr pFilePath, IntPtr pUser);
        public delegate bool EHomeSSMsgCallBack(int iHandle, NET_EHOME_SS_MSG_TYPE enumType, IntPtr pOutBuffer, int dwOutLen, IntPtr pInBuffer, int dwInLen, IntPtr pUser);


        public delegate bool EHomeSSRWCallBackEx(int iHandle, ref NET_EHOME_SS_RW_PARAM pRwParam, ref NET_EHOME_SS_EX_PARAM pExStruct);


        public enum NET_EHOME_SS_INIT_CFG_TYPE
        {
            NET_EHOME_SS_INIT_CFG_SDK_PATH = 1,
            NET_EHOME_SS_INIT_CFG_CLOUD_TIME_DIFF,
            NET_EHOME_SS_INIT_CFG_PUBLIC_IP_PORT
        }

        public enum NET_EHOME_SS_MSG_TYPE
        {
            NET_EHOME_SS_MSG_TOMCAT = 1,       //Tomcat回调函数
            NET_EHOME_SS_MSG_KMS_USER_PWD,     //KMS用户名密码校验
            NET_EHOME_SS_MSG_CLOUD_AK          //EHome5.0存储协议AK回调
        }

        public enum NET_EHOME_SS_CLIENT_TYPE
        {
            NET_EHOME_SS_CLIENT_TYPE_TOMCAT = 1,//Tomcat图片上传客户端
            NET_EHOME_SS_CLIENT_TYPE_VRB,   //VRB图片上传客户端
            NET_EHOME_SS_CLIENT_TYPE_KMS,   //KMS图片上传客户端
            NET_EHOME_SS_CLIENT_TYPE_CLOUD   //EHome5.0存储协议客户端
        }


        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_TOMCAT_MSG
        {
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_URL_LEN_SS)]
            public string szDevUri;
            public int dwPicNum;
            public IntPtr pPicURLs;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 64)]
            public byte[] byRes;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_LISTEN_HTTPS_PARAM
        {
            public byte byHttps;
            public byte byVerifyMode;
            public byte byCertificateFileType;
            public byte byPrivateKeyFileType;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szUserCertificateFile;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_PATH)]
            public string szUserPrivateKeyFile;
            public int dwSSLVersion;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 360)]
            public byte[] byRes3;

            public void Init()
            {
                byRes3 = new byte[360];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_CLIENT_PARAM
        {
            public NET_EHOME_SS_CLIENT_TYPE enumType; //图片上传客户端类型
            public HCEHomePublic.NET_EHOME_IPADDRESS struAddress;//图片服务器地址
            public byte byHttps;//是否启用HTTPs
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 63)]
            public byte[] byRes;
            public void Init()
            {
                struAddress.Init();
                byRes = new byte[63];
            }
        }
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_RW_PARAM
        {
            public IntPtr pFileName;
            public IntPtr pFileBuf;
            public IntPtr dwFileLen;
            public IntPtr pFileUrl;
            public IntPtr pUser;
            public byte byAct;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 63)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[63];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_CLOUD_PARAM
        {
            public IntPtr pPoolId;          //poolId 资源池id
            public byte byPoolIdLength;   //poolid 长度
            public int dwErrorCode;     //云存储错误码
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 503)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[503];
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_TOMCAT_PARAM
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[512];
            }
        }


        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_KMS_PARAM
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[512];
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_VRB_PARAM
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 512)]
            public byte[] byRes;
            public void Init()
            {
                byRes = new byte[512];
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        public struct NET_EHOME_ESS_STORE_UNION
        {
            public NET_EHOME_SS_CLOUD_PARAM struCloud;
            public NET_EHOME_SS_TOMCAT_PARAM struTomcat;
            public NET_EHOME_SS_KMS_PARAM struKms;
            public NET_EHOME_SS_VRB_PARAM struVrb;
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_EX_PARAM
        {
            public byte byProtoType;  //Storage Protocol 1-Cloud Storage，2-tomcat，3-kms，4-vrb
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 23)]
            public byte[] byRes;
            public NET_EHOME_ESS_STORE_UNION unionStoreInfo;
            public void Init()
            {
                byRes = new byte[23];
            }
        }
        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct NET_EHOME_SS_LISTEN_PARAM
        {
            public HCEHomePublic.NET_EHOME_IPADDRESS struAddress;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_KMS_USER_LEN)]
            public string szKMS_UserName;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_KMS_PWD_LEN)]
            public string szKMS_Password;
            public EHomeSSStorageCallBack fnSStorageCb;//图片服务器信息存储回调函数
            public EHomeSSMsgCallBack fnSSMsgCb;//图片服务器信息Tomcat回调函数
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_CLOUD_AK_SK_LEN)]
            public string szAccessKey;
            [MarshalAsAttribute(UnmanagedType.ByValTStr, SizeConst = MAX_CLOUD_AK_SK_LEN)]
            public string szSecretKey;
            public IntPtr pUserData;
            public byte byHttps;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 3)]
            public byte[] byRes1;
            public EHomeSSRWCallBack fnSSRWCb;
            //Security Mode
            public EHomeSSRWCallBackEx fnSSRWCbEx;
            public byte bySecurityMode;
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 51)]
            public byte[] byRes;
            public void Init()
            {
                byRes1 = new byte[3];
                byRes = new byte[51];
            }
        }

        [StructLayoutAttribute(LayoutKind.Sequential)]
        public struct PicPath
        {
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 100)]
            public byte[] Path;
            public void Init()
            {
                Path = new byte[100];
            }
        }





        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_HAMSHA256(IntPtr pSrc, IntPtr pSecretKey, byte[] pSingatureOut, int dwSingatureLen);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_Init();

        [DllImport(@"HCISUPSS.dll")]
        public static extern int NET_ESS_SetLogToFile(int iLogLevel, char[] strLogDir, bool bAutoDel);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_SetListenHttpsParam(ref NET_EHOME_SS_LISTEN_HTTPS_PARAM pSSHttpsParam);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_SetSDKInitCfg(NET_EHOME_SS_INIT_CFG_TYPE enumType, IntPtr lpInBuff);

        [DllImport(@"HCISUPSS.dll")]
        public static extern int NET_ESS_StartListen(ref NET_EHOME_SS_LISTEN_PARAM pSSListenParam);

        [DllImport(@"HCISUPSS.dll")]
        public static extern int NET_ESS_CreateClient(ref NET_EHOME_SS_CLIENT_PARAM pClientParam);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientSetTimeout(int lHandle, int dwSendTimeout, int dwRecvTimeout);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientSetParam(int lHandle, string strParamName, string strParamVal);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientDoUpload(int lHandle, IntPtr strUrl, int dwUrlLen);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientDoDownload(int lHandle, IntPtr strUrl, IntPtr pFileContent, int dwContentLen);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientDoDelete(int lHandle, IntPtr strUrl);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_ClientDoUploadBuffer(int lHandle, IntPtr strUrl, int dwUrlLen, IntPtr pFileContent, int dwContentLen);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_DestroyClient(int lHandle);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_StopListen(int lListenHandle);

        [DllImport(@"HCISUPSS.dll")]
        public static extern bool NET_ESS_Fini();
    }
}
