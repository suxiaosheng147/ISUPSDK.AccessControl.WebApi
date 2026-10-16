using ISUPSDK.AccessControl.WebApi.Services.Logging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Customize;
using ISUPSDK.AccessControl.WebApi.Distribute;
using ISUPSDK.AccessControl.WebApi.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.SdkService.AlarmService;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using ISUPSDK.AccessControl.WebApi.SdkService.SSService;
using ISUPSDK.AccessControl.WebApi.SdkService.StreamService;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Structure;
using Microsoft.Extensions.Configuration;
using NCrontab;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
namespace ISUPSDK.AccessControl.WebApi.Services
{
    public sealed class IsupSdkRuntime
    {
        #region 定义全局变量

        private Settings settings = new();
        /// <summary>
        /// 程序集地址
        /// </summary>
        private string sCurPath = AppDomain.CurrentDomain.BaseDirectory;

        private XmlDocument doc = new();

        private ConcurrentDictionary<int, string> MatchDic = new();
        private ConcurrentDictionary<string, im_machine> MainDic = new();
        // 在线设备及其同步状态（键=设备号；重连会更新为最新注册句柄）。
private ConcurrentDictionary<string, DeviceSyncState> AllTask = new();
        
        private const string PublicKey = "krbb202012345678";

        /// <summary>
        /// MQTT端口
        /// </summary>
        private ushort m_wAlarmServerMqttPort = 0;

        /// <summary>
        /// 图片服务器地址
        /// </summary>
        private string PicServerip = string.Empty;


        private int PicServerPort = 0;


        private string ip=string.Empty;

        /// <summary>
        /// 
        /// </summary>
        private int m_byUseCmsPort;

        private byte m_byCmsSecureAccessType;

        private byte m_byAlarmSecureAccessType;

        private byte m_byStreamSecureAccessType;

        public const int StreamAdd = 1;
        public const int AudioAdd = 2;
        public const int DASAdd = 3;
        public const int PSSAdd = 4;

        /// <summary>
        /// 
        /// </summary>
        private string StrTcpAlarmIp = "";
        private string StrUdpAlarmIp = "";
        private string StrPicServIp = "";
        #endregion

        #region  SDK变量
        private HCISUPCMS.NET_EHOME_CMS_LISTEN_PARAM m_struCMSListenPara = new();
        /// <summary>
        /// 服务器信息结构体
        /// </summary>
        public HCISUPCMS.NET_EHOME_SERVER_INFO_V50 m_struServInfo = new(); //服务
        private int CmsListenHandle = -1;     //注册监听句柄
        private int lLoginID = -1;

        private delegate bool rmMsgCallBack(long lHandle, ref HCISUPAlarm.NET_EHOME_ALARM_MSG pAlarmMsg, IntPtr pUser);
        public HCEHomePublic.NET_EHOME_AMS_ADDRESS m_struAmsAddr = new();
        public HCEHomePublic.NET_EHOME_LOCAL_ACCESS_SECURITY m_struAccessSecure = new();
        public HCEHomePublic.NET_EHOME_SEND_PARAM m_struSendParam = new();


        public HCISUPCMS.NET_EHOME_SERVER_INFO_V50 m_struServUIConfig = new();

        // public static ACCESS_DEVICE_INFO[] m_stAccessDeviceList = new ACCESS_DEVICE_INFO[64];
        //private static HCISUPStream.PREVIEW_NEWLINK_CB fnPREVIEW_NEWLINK_CB_Func = null;
        private HCISUPAlarm.EHomeMsgCallBack? AlarmMsgCallBack_Func = null;
        private HCISUPCMS.DEVICE_REGISTER_CB? EHOME_REGISTER_Func = null;

        private HCISUPSS.EHomeSSMsgCallBack? SSMsgCallBack = null;
        private HCISUPSS.EHomeSSStorageCallBack? StroageCallBack = null;

        private HCISUPSS.EHomeSSMsgCallBack ptrSSMsgCallBack;
        private HCISUPSS.EHomeSSStorageCallBack ptrStroageCallBack;

        private HCISUPAlarm.EHomeMsgCallBack? ptrAlarmCB = null;
        private HCISUPAlarm.EHomeMsgCallBack? ptrTcpAlarm = null;
        private HCISUPAlarm.EHomeMsgCallBack? ptrMqtt = null;


        #endregion

        #region SDK回调函数

        /// <summary>
        /// 消息回调函数
        /// </summary>
        /// <param name="iHandle"></param>
        /// <param name="enumType"></param>
        /// <param name="pOutBuffer"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pInBuffer"></param>
        /// <param name="dwInLen"></param>
        /// <param name="pUser"></param>
        /// <returns></returns>
        private bool SS_Message_Callback(int iHandle, HCISUPSS.NET_EHOME_SS_MSG_TYPE enumType, IntPtr pOutBuffer, int dwOutLen, IntPtr pInBuffer, int dwInLen, IntPtr pUser)
        {

            if (enumType == HCISUPSS.NET_EHOME_SS_MSG_TYPE.NET_EHOME_SS_MSG_KMS_USER_PWD)
            {
                Marshal.WriteByte(pInBuffer, 1);
            }
            else if (enumType == HCISUPSS.NET_EHOME_SS_MSG_TYPE.NET_EHOME_SS_MSG_CLOUD_AK)
            {
                byte[] temp = new byte[128];
                Marshal.Copy(pOutBuffer, temp, 0, dwOutLen);
                //for (int i = 0; i < MAX_DEVICES; ++i)
                //{
                //    if (DeviceTree.g_struDeviceInfo[i].iDeviceIndex != -1)
                //    {
                //        if (DeviceTree.isByteEqual(ref DeviceTree.g_struDeviceInfo[i].byDeviceID, ref temp, 64))
                //        {
                //            Marshal.Copy(DeviceTree.g_struDeviceInfo[i].byClouldSecretKey, 0, pInBuffer, 64);
                //            break;
                //        }
                //    }
                //}

                // 将目标缓冲区转换为字符串
                string ak = settings.LogOnSecretKey;
                byte[] akBytes = System.Text.Encoding.UTF8.GetBytes(ak);


                Marshal.Copy(akBytes, 0, pInBuffer, akBytes.Length); // 将密钥复制到 pInBuffer
            }
            return true;
        }

        /// <summary>
        /// 存储回调函数
        /// </summary>
        /// <param name="iHandle"></param>
        /// <param name="pFileName"></param>
        /// <param name="pFileBuf"></param>
        /// <param name="dwFileLen"></param>
        /// <param name="pFilepath"></param>
        /// <param name="pUser"></param>
        /// <returns></returns>
        private bool SS_Storage_Callback(int iHandle, IntPtr pFileName, IntPtr pFileBuf, int dwFileLen, IntPtr pFilepath, IntPtr pUser)
        {
            string? strFilename = Marshal.PtrToStringUTF8(pFileName);
            if (string.IsNullOrWhiteSpace(strFilename) || IntPtr.Zero == pFileBuf || 0 == dwFileLen)
            {
                return false;
            }

            if (false == System.IO.Directory.Exists(settings.PicturePath))
            {
                System.IO.Directory.CreateDirectory(settings.PicturePath);
            }
            strFilename.Replace(':', '_');
            string KMSTemp = "/home/config/pic/enrlFace";
            if (strFilename.Contains("/home/config/pic/enrlFace"))
            {
                int KMSIndex = strFilename.IndexOf(KMSTemp);
                strFilename = strFilename.Substring(0, KMSIndex);
            }

            string Filepath = settings.PicturePath + "\\" + strFilename;
            try
            {
                using FileStream fs = new FileStream(Filepath, FileMode.OpenOrCreate, FileAccess.Read | FileAccess.Write);
                byte[] DataStream = new byte[dwFileLen];
                Marshal.Copy(pFileBuf, DataStream, 0, dwFileLen);
                fs.Write(DataStream, 0, dwFileLen);
                fs.Flush();
                fs.Close();
            }
            catch (Exception)
            {

            }
            HCISUPSS.PicPath pPath = new();
            pPath.Init();

            pPath.Path = System.Text.Encoding.Default.GetBytes(Filepath.PadRight(100, '\0'));
            IntPtr DemoPath = Marshal.AllocHGlobal(Marshal.SizeOf(pPath));
            Marshal.StructureToPtr(pPath, pFilepath, false);
            return true;
        }

        /// <summary>
        /// 回调注册
        /// </summary>
        /// <param name="iUserID">句柄</param>
        /// <param name="dwDataType"></param>
        /// <param name="pOutBuffer"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pInBuffer"></param>
        /// <param name="dwInLen"></param>
        /// <param name="pUser"></param>
        /// <returns></returns>
        public bool FRegisterCallBack(int iUserID, int dwDataType, IntPtr pOutBuffer, uint dwOutLen, IntPtr pInBuffer, uint dwInLen, IntPtr pUser)
        {

            var struDevInfo = GetStructure<HCISUPCMS.NET_EHOME_DEV_REG_INFO_V12>(pOutBuffer);
            var deviceIdSpan = struDevInfo.struRegInfo.byDeviceID.AsSpan();
           
            var strDeviceID = GetCleanDeviceId(deviceIdSpan);
            switch (dwDataType)
            {
                case HCISUPCMS.ENUM_DEV_ON://枚举类型为0, 设备上线回调 
                    if (pInBuffer == IntPtr.Zero)
                    {
                        return false;
                    }
                    if (string.IsNullOrWhiteSpace(ip))  //这个为空，证明没有配置  DasInfo 
                    {
                        ip = new string(struDevInfo.struRegAddr.szIP).Replace("\0", "");
                        if (string.IsNullOrWhiteSpace(ip))
                        {
                            ip = settings.ServerAddressOut;  // app.config 里面配置的 外网ip 
                        }
                    }
                    //var token = Security.AESEncrypt(strDeviceID, PublicKey);
                    //string geturl = Common.SplicingUrl(settings.GetMachineIdUrl, strDeviceID, ip, struDevInfo.struRegAddr.wPort.ToString(), token??"", 0, 0, null, null, 0, 0);
                    //string geturlDB = Common.PostUrlAsync(geturl).Result;
                    //if (string.IsNullOrWhiteSpace(geturlDB))
                    //{
                    //    Common.WriteFile("log.txt", true, strDeviceID + "获取机器其他信息失败，登录失败");
                    //    return false;
                    //}
                    //var setting = JsonHepler.FromJson<UpdateData<im_machine>>(geturlDB);
                    //if (setting != null && setting.Status)
                    //    setting.Data.Password = Security.AESDecrypt(setting.Data.Password, PublicKey);
                    //else
                    //{
                    //    Common.WriteFile("log.txt", true, strDeviceID + "设备在管理云平台资料不匹配，登录失败");
                    //    return false;
                    //}
                    //setting.Data.lLoginID = iUserID;
                    //lLoginID = iUserID;




                    MatchDic.AddOrUpdate(iUserID, strDeviceID, (k, v) => strDeviceID);


                    //im_machine machine = new()
                    //{
                    //    MachineSN = strDeviceID,
                    //    HKLoginId = iUserID,
                    //    lLoginID = iUserID,
                    //};
                    //string xmlString = AccessControlCapabilities.GetAccessControlCapabilities(iUserID);
                    //if (!string.IsNullOrWhiteSpace(xmlString))
                    //{
                    //    var serializer = new XmlSerializer(typeof(AccessControl));
                    //    using var reader = new StringReader(xmlString);
                    //    var acs = (AccessControl)serializer?.Deserialize(reader);
                    //    machine.accessControl = acs;
                    //}
                    //MainDic.AddOrUpdate(strDeviceID, machine, (k, v) => machine);

                    byte[] szDeviceSerial = new byte[HCEHomePublic.NET_EHOME_SERIAL_LEN + 1];
                    struDevInfo.struRegInfo.sDeviceSerial.CopyTo(szDeviceSerial, 0);


                    

                    Span<byte> sessionKeySpan = struDevInfo.struRegInfo.bySessionKey.AsSpan();
                    byte[] szSessionKey = new byte[HCEHomePublic.MAX_MASTER_KEY_LEN + 1];
                    sessionKeySpan.Slice(0, Math.Min(sessionKeySpan.Length, szSessionKey.Length))
                                  .CopyTo(szSessionKey);

                    int dwVersion;

                    if (struDevInfo.struRegInfo.byDevProtocolVersion[0] == '2')
                    {
                        dwVersion = 2;
                    }
                    else if ('4' == struDevInfo.struRegInfo.byDevProtocolVersion[0])
                    {
                        dwVersion = 4;
                    }
                    else
                    {
                        dwVersion = 5;
                    }
                    HCISUPCMS.NET_EHOME_SERVER_INFO_V50 struServInfo = GetStructure<HCISUPCMS.NET_EHOME_SERVER_INFO_V50>(pInBuffer);
                    

                    struServInfo = m_struServInfo;   //use ISUPDemo.xml
                    if (5 == dwVersion)
                    {
                        struServInfo.struTCPAlarmSever.wPort = (short)m_wAlarmServerMqttPort;
                    }

                    int TdwSize = Marshal.SizeOf(struServInfo);
                    IntPtr ptrStruS = Marshal.AllocHGlobal(TdwSize);
                    Marshal.StructureToPtr(struServInfo, ptrStruS, false);


                    int value = 0;
                    GetAddressByType(0, 0, ref ptrStruS, TdwSize, ref value, 0);
                    struServInfo = GetStructure<HCISUPCMS.NET_EHOME_SERVER_INFO_V50>(ptrStruS);
                    struServInfo.byClouldHttps = 0;
                    Array.Copy(struDevInfo.struRegInfo.byDeviceID, 0, struServInfo.byClouldAccessKey, 0, 64);
                    

                    if (5 == dwVersion)
                    {
                        struServInfo.struTCPAlarmSever.wPort = (short)m_wAlarmServerMqttPort;
                    }
                    int iLoginID = iUserID;
                    //Marshal.StructureToPtr(struServInfo, pInBuffer, false);

                    // 在操作前验证缓冲区大小
                    if (dwInLen < Marshal.SizeOf<HCISUPCMS.NET_EHOME_SERVER_INFO_V50>())
                    {
                        Common.WriteFile("error.log", true, "输入缓冲区不足！");
                        Marshal.FreeHGlobal(ptrStruS); // 释放内存
                        return false;
                    }

                    // 使用安全的复制方法
                    try
                    {
                        Marshal.StructureToPtr(struServInfo, pInBuffer, false);
                    }
                    catch (Exception ex)
                    {
                        Common.WriteFile("error.log", true, $"内存写入失败: {ex.Message}");
                        Marshal.FreeHGlobal(ptrStruS); // 释放内存
                        return false;
                    }

                if (AllTask.TryGetValue(strDeviceID, out var existingState))
                {
                    // 重复上线（多为重连）：更新为最新注册句柄，避免后续用旧句柄同步。
                    existingState.Handle = iUserID;
                    IsupLog.Info("设备 {0} 重复上线，已更新注册句柄。", strDeviceID);
                    Marshal.FreeHGlobal(ptrStruS); // 释放内存
                    return true;
                }

                // 设备在线标记：权限同步改由全局定时任务（Settings:SyncCrontab）统一驱动，
                // 不再为每台设备常驻后台循环（原 while + Task.Delay 逻辑已移除）。
                AllTask[strDeviceID] = new DeviceSyncState { Handle = iUserID };

                // 本轮注册回填所需的内存已用完，直接释放（原来由常驻任务的 finally 负责）。
                if (ptrStruS != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(ptrStruS);
                }

                break;
                case HCISUPCMS.ENUM_DEV_AUTH://枚举类型为3, Ehome5.0设备认证回调 
                    string ISUPKey = settings.LogOnSecretKey;
                    byte[] byTemp = Encoding.Default.GetBytes(ISUPKey);
                    byte[] byISUPKey = new byte[32];
                    byTemp.CopyTo(byISUPKey, 0);
                    Marshal.Copy(byISUPKey, 0, pInBuffer, 32);
                    break;
                case HCISUPCMS.ENUM_DEV_SESSIONKEY://枚举类型为4, Ehome5.0设备Sessionkey回调
                    HCEHomePublic.NET_EHOME_DEV_SESSIONKEY devSessionkey = new();
                    devSessionkey.Init();
                    struDevInfo.struRegInfo.byDeviceID.CopyTo(devSessionkey.sDeviceID, 0);
                    struDevInfo.struRegInfo.bySessionKey.CopyTo(devSessionkey.sSessionKey, 0);
                    HCISUPCMS.NET_ECMS_SetDeviceSessionKey(ref devSessionkey);
                    HCISUPAlarm.NET_EALARM_SetDeviceSessionKey(ref devSessionkey);
                    break;
                case HCISUPCMS.ENUM_DEV_DAS_REQ://枚举类型为5, Ehome5.0设备重定向请求回调
                    string szLocalIP = "";
                    IntPtr ptrLocalIP = IntPtr.Zero;
                    int dwPort = 0;
                    GetAddressByType(3, 0, ref ptrLocalIP, 128, ref dwPort, 4);
                    szLocalIP = Marshal.PtrToStringUTF8(ptrLocalIP);
                    if (0 == dwPort)
                    {
                        dwPort =settings.InitPort /*m_nPort*/;
                    }
                    string portTemp = dwPort.ToString();
                    string strInBuffer = "{\"Type\":\"DAS\",\"DasInfo\":{\"Address\":\"" + szLocalIP + "\"," +
                    "\"Domain\":\"test.ys7.com\",\"ServerID\":\"das_" + szLocalIP + "_" + portTemp + "\",\"Port\":" + portTemp + ",\"UdpPort\":" + portTemp + "}}";

                    byTemp = System.Text.Encoding.Default.GetBytes(strInBuffer);
                    Marshal.Copy(byTemp, 0, pInBuffer, byTemp.Length);
                    break;
                case HCISUPCMS.ENUM_DEV_OFF: //设备下线回调

                    //iUserID
                    //if (MatchDic.TryGetValue(iUserID, out string sn))
                    //{
                    //    AllTask.TryRemove(sn, out var cts);
                    //    cts?.Cancel();
                    //    cts?.Dispose(); // 显式释放资源
                    //    Common.WriteFile("log.txt", true, $"设备[{sn}] 下线");
                    //    MatchDic.TryRemove(iUserID, out _);
                    //}
                    if (MatchDic.TryGetValue(iUserID, out string? sn))
                    {
                        if (AllTask.TryRemove(sn, out var state))
                        {
                            state?.Cts.Cancel();
                            state?.Cts.Dispose();
                        }
                        Common.WriteFile("log.txt", true, $"设备[{sn}] 下线");
                        MatchDic.TryRemove(iUserID, out _);
                    }
                    break;
                //case HCISUPCMS.ENUM_DEV_ADDRESS_CHANGED:

                //    //表示设备上线。实际为设备在心跳超时时间内重新上线，采用的IP和端口跟之前链路不同（IP地址或端口发生了变化）。
                //    GCHandle handle = GCHandle.Alloc(pOutBuffer, GCHandleType.Pinned);
                //    try
                //    {
                //        NET_EHOME_DEV_REG_INFO_V12 devInfo = (NET_EHOME_DEV_REG_INFO_V12)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(NET_EHOME_DEV_REG_INFO_V12));
                //        IsupLog.Info($"Device ID: {Encoding.Default.GetString(devInfo.struRegInfo.byDeviceID).TrimEnd('\0')}");
                //        IsupLog.Info($"Device Type: {devInfo.struRegInfo.dwNetUnitType}");
                //    }
                //    finally
                //    {
                //        handle.Free();
                //    }
                //    break;
                //case HCISUPCMS.ENUM_DEV_DAS_REREGISTER:

                //    //表示设备上线。实际为设备在心跳超时时间内重新上线，采用的IP和端口跟之前链路相同。
                //    GCHandle handle1 = GCHandle.Alloc(pOutBuffer, GCHandleType.Pinned);
                //    try
                //    {
                //        NET_EHOME_DEV_REG_INFO_V12 devInfo = (NET_EHOME_DEV_REG_INFO_V12)Marshal.PtrToStructure(handle1.AddrOfPinnedObject(), typeof(NET_EHOME_DEV_REG_INFO_V12));
                //        IsupLog.Info($"Device ID: {Encoding.Default.GetString(devInfo.struRegInfo.byDeviceID).TrimEnd('\0')}");
                //        IsupLog.Info($"Device Type: {devInfo.struRegInfo.dwNetUnitType}");
                //    }
                //    finally
                //    {
                //        handle1.Free();
                //    }

                //    break;
            }
            return true;
        }
        // 辅助方法组
        private T GetStructure<T>(IntPtr ptr) where T : new()
        {
            var structure = new T();
            if (ptr != IntPtr.Zero)
            {
                structure = (T)Marshal.PtrToStructure(ptr, typeof(T));
            }
            return structure;
        }
        /// <summary>
        /// 获取机器编号
        /// </summary>
        /// <param name="deviceIdBytes"></param>
        /// <returns></returns>
        private string GetCleanDeviceId(Span<byte> deviceIdBytes)
        {
            if (deviceIdBytes == null || deviceIdBytes.Length == 0)
            {
                return string.Empty;
            }

            int cleanLength = deviceIdBytes.IndexOf((byte)0);
            return Encoding.ASCII.GetString(deviceIdBytes.Slice(0, cleanLength));
        }

        ///// <summary>
        ///// 报警
        ///// </summary>
        ///// <param name="iHandle"></param>
        ///// <param name="pAlarmMsg"></param>
        ///// <param name="pUser"></param>
        ///// <returns></returns>
        //private static bool AlarmMsgCallBack(int iHandle, IntPtr pAlarmMsg/*ref HCEHomeAlarm.NET_EHOME_ALARM_MSG struAlarmMsg*/, IntPtr pUser)
        //{
        //    try
        //    {
        //        bool lb = false;


        //        HCISUPAlarm.NET_EHOME_ALARM_MSG struAlarmMsg = new()
        //        {
        //            sSerialNumber = new byte[HCISUPAlarm.NET_EHOME_SERIAL_LEN],
        //            byRes = new byte[20]
        //        };
        //        struAlarmMsg = GetStructure<HCISUPAlarm.NET_EHOME_ALARM_MSG>(pAlarmMsg); /*(HCISUPAlarm.NET_EHOME_ALARM_MSG)Marshal.PtrToStructure(pAlarmMsg, typeof(HCISUPAlarm.NET_EHOME_ALARM_MSG))*/;


        //        using StringReader sr = new(Marshal.PtrToStringUTF8(struAlarmMsg.pXmlBuf));
        //        XmlSerializer xz = new(typeof(PPVSPMessage));
        //        IsupLog.Info(Marshal.PtrToStringUTF8(struAlarmMsg.pXmlBuf));

        //        if (struAlarmMsg.dwAlarmType == HCISUPAlarm.EHOME_ALARM_ACS)
        //        {

                    
        //            var temp = (PPVSPMessage)xz.Deserialize(sr);
        //            if (temp != null && temp.Params != null && (temp.Params.MinorType == "0x4b" || temp.Params.MinorType == "0x3c")) // 0x4b=  ;0x3c=
        //            {
                       

        //                if (AddReadCard(null, temp))
        //                    return true;
        //                else
        //                    return lb;
        //            }
        //            else if (temp != null && temp.Params != null && (temp.Params.MinorType == "0x5" || temp.Params.MinorType == "0x1")) //刷卡
        //            {
        //                if (AddReadCard(temp))
        //                    return true;
        //                else
        //                    return lb;
        //            }
        //        }

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        Common.WriteFileHours("AlarmMsgCallBack.txt", true, ex.InnerException == null ? ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source : ex.InnerException.Message + "\n" + ex.InnerException.StackTrace + "\n" + ex.InnerException.Source);
        //        return false;
        //    }
        //}


        /// <summary>
        /// 报警回调（<c>NET_EALARM_StartListen</c> 上报的门禁事件）。
        /// <para>
        /// 只把报文<b>原样</b>广播给事件管道（<see cref="AlarmReceived"/> → 内存缓冲 → 落盘 → 事件队列），
        /// 这里<b>不做业务判定、也不直接对外发送</b>：MinorType 白名单判定、图片下载与下游转发
        /// 统一由消费者侧完成（<c>AcsAlarmForwardService</c>）。
        /// </para>
        /// </summary>
        private bool AlarmMsgCallBack(int iHandle, IntPtr pAlarmMsg, IntPtr pUser)
        {
            try
            {
                Interlocked.Increment(ref _alarmCount);
                LastAlarmAt = DateTimeOffset.Now;

                // 解析报警消息结构体
                var alarmMsg = GetStructure<HCISUPAlarm.NET_EHOME_ALARM_MSG>(pAlarmMsg);

                // 仅处理门禁报警类型
                if (alarmMsg.dwAlarmType != HCISUPAlarm.EHOME_ALARM_ACS)
                    return true;

                // 解析XML报警数据
                string xmlData = Marshal.PtrToStringUTF8(alarmMsg.pXmlBuf)!;

                // 广播给订阅者（报警缓存 / 事件管道）。
                var alarmDeviceId = MatchDic.TryGetValue(iHandle, out var alarmSn) ? alarmSn : string.Empty;
                AlarmReceived?.Invoke(this, new IsupAlarmEventArgs
                {
                    ListenHandle = iHandle,
                    DeviceId = alarmDeviceId,
                    ReceivedAt = DateTimeOffset.Now,
                    Payload = xmlData,
                });
                IsupLog.Info(xmlData);

                return true;
            }
            catch (Exception ex)
            {
                LogException("AlarmMsgCallBack.txt", ex);
                return false;
            }
        }

        #endregion

        #region  Assembly

        /// <summary>
        /// 配置组件
        /// </summary>
        public void InitLib()
        {
            String csLogPath = _sdkLogDirectory;
            char[] szLogPath = new char[128];
            csLogPath.CopyTo(0, szLogPath, 0, csLogPath.Length);

            HCISUPSS.NET_ESS_Init();
            HCISUPSS.NET_ESS_SetLogToFile(3, szLogPath, true);

            HCEHomePublic.NET_EHOME_IPADDRESS struPublicAddress = new();
            struPublicAddress.Init();

            IntPtr PtrSzIp = Marshal.AllocHGlobal(Marshal.SizeOf(struPublicAddress));
            Marshal.StructureToPtr(struPublicAddress, PtrSzIp, false);
            int port = 0;
            int size = Marshal.SizeOf(struPublicAddress.wPort);
            GetAddressByType(4, 0, ref PtrSzIp, 128, ref port, size);

#pragma warning disable CS8605 // 取消装箱可能为 null 的值。
            struPublicAddress = (HCEHomePublic.NET_EHOME_IPADDRESS)Marshal.PtrToStructure(PtrSzIp, typeof(HCEHomePublic.NET_EHOME_IPADDRESS));
#pragma warning restore CS8605 // 取消装箱可能为 null 的值。

            struPublicAddress.wPort = short.Parse(port.ToString());
            if (0 == struPublicAddress.wPort)
            {
                struPublicAddress.wPort = m_struServInfo.struPictureSever.wPort;
            }
            Array.Copy(m_struServInfo.struPictureSever.szIP, 0, struPublicAddress.szIP, 0, 128);


            int dwsize = Marshal.SizeOf(struPublicAddress);
            IntPtr ptrstruPublicAddress = Marshal.AllocHGlobal(dwsize);
            Marshal.StructureToPtr(struPublicAddress, ptrstruPublicAddress, false);
            HCISUPSS.NET_ESS_SetSDKInitCfg(HCISUPSS.NET_EHOME_SS_INIT_CFG_TYPE.NET_EHOME_SS_INIT_CFG_PUBLIC_IP_PORT, ptrstruPublicAddress);
            Marshal.FreeHGlobal(ptrstruPublicAddress);

            HCISUPSS.NET_EHOME_SS_LISTEN_PARAM struSSListenParam = new();
            struSSListenParam.Init();
            struSSListenParam.struAddress.Init();
            //m_csLocalIP.CopyTo(0, struSSListenParam.struAddress.szIP, 0, m_csLocalIP.Length);

            settings.ServerAddress.CopyTo(0, struSSListenParam.struAddress.szIP, 0, settings.ServerAddress.Length);
            struSSListenParam.struAddress.wPort = m_struServInfo.struPictureSever.wPort;
            struSSListenParam.szKMS_UserName = "test";
            struSSListenParam.szKMS_Password = "12345";
            struSSListenParam.szAccessKey = "test";
            struSSListenParam.szSecretKey = "12345";
            //将安全模式关闭
            struSSListenParam.bySecurityMode = 1;


            SSMsgCallBack = new HCISUPSS.EHomeSSMsgCallBack(SS_Message_Callback);
            struSSListenParam.fnSSMsgCb = SSMsgCallBack;
            ptrSSMsgCallBack = SSMsgCallBack;

            //struSSListenParam.fnSSRWCbEx = null;

            StroageCallBack = new HCISUPSS.EHomeSSStorageCallBack(SS_Storage_Callback);
            struSSListenParam.fnSStorageCb = StroageCallBack;
            ptrStroageCallBack = StroageCallBack;

            struSSListenParam.pUserData = default;

            long m_lSSHandle = HCISUPSS.NET_ESS_StartListen(ref struSSListenParam);
            if (-1 == m_lSSHandle)
            {
                IsupLog.Info("NET_ESS_StartListen Failed, port：" + struSSListenParam.struAddress.wPort);
            }
            else
            {
                IsupLog.Info("NET_ESS_StartListen succ , port：" + struSSListenParam.struAddress.wPort);
                SsListenHandle = m_lSSHandle;
            }


            bool bRet = false;

            bRet = HCISUPCMS.NET_ECMS_Init();
            if (!bRet)
            {
                String str = string.Format("{0}", HCISUPCMS.NET_ECMS_GetLastError());
                IsupLog.Info("NET_ECMS_Init failed, err={0}", HCISUPCMS.NET_ECMS_GetLastError());

            }
            csLogPath.CopyTo(0, szLogPath, 0, csLogPath.Length);
            bRet = HCISUPCMS.NET_ECMS_SetLogToFile(3, szLogPath, true);

            m_struAccessSecure.dwSize = (uint)Marshal.SizeOf(m_struAccessSecure);
            m_struAccessSecure.byAccessSecurity = m_byCmsSecureAccessType;
            IntPtr ptrAccessSecure = Marshal.AllocHGlobal((int)m_struAccessSecure.dwSize);
            Marshal.StructureToPtr(m_struAccessSecure, ptrAccessSecure, false);
            if (!HCISUPCMS.NET_ECMS_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE.ACTIVE_ACCESS_SECURITY, ptrAccessSecure))
            {
                int iRet = HCISUPCMS.NET_ECMS_GetLastError();
                IsupLog.Info("NET_ECMS_Init failed, err={0}", HCISUPCMS.NET_ECMS_GetLastError());
                Marshal.FreeHGlobal(ptrAccessSecure);
                return;
            }
            else
                IsupLog.Info("NET_ECMS_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Succeed");


            m_struAmsAddr.struAddress.Init();
            settings.ServerAddress.CopyTo(0, m_struAmsAddr.struAddress.szIP, 0, settings.ServerAddress.Length);
            m_struAmsAddr.struAddress.wPort = m_struServInfo.struUDPAlarmSever.wPort;
            m_struAmsAddr.byEnable = (byte)m_byUseCmsPort;
            m_struAmsAddr.dwSize = Marshal.SizeOf(m_struAmsAddr);
            IntPtr ptrAmsAddr = Marshal.AllocHGlobal(m_struAmsAddr.dwSize);
            Marshal.StructureToPtr(m_struAmsAddr, ptrAmsAddr, false);

            if (!HCISUPCMS.NET_ECMS_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE.AMS_ADDRESS, ptrAmsAddr))
                IsupLog.Info("open cms-alarm Failed "+ m_byUseCmsPort);
            else
                IsupLog.Info("open cms-alarm  Succ "+ m_byUseCmsPort);
            Marshal.FreeHGlobal(ptrAmsAddr);

            m_struSendParam.dwRecvTimeOut = 0;
            m_struSendParam.bySendTimes = 2;
            m_struSendParam.byRes2 = new byte[127];
            m_struSendParam.dwSize = Marshal.SizeOf(m_struSendParam);
            IntPtr ptrSendParam = Marshal.AllocHGlobal(m_struSendParam.dwSize);
            Marshal.StructureToPtr(m_struSendParam, ptrSendParam, false);

            if (!HCISUPCMS.NET_ECMS_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE.SEND_PARAM, ptrSendParam))
                IsupLog.Info("NET_ECMS_SetSDKLocalCfg SEND_PARAM failed");
            else
                IsupLog.Info("NET_ECMS_SetSDKLocalCfg SEND_PARAM succ");
            Marshal.FreeHGlobal(ptrSendParam);

            m_struCMSListenPara.struAddress.Init();
            settings.ServerAddress.CopyTo(0, m_struCMSListenPara.struAddress.szIP, 0, settings.ServerAddress.Length);
            m_struCMSListenPara.struAddress.wPort = (short) settings.InitPort  /*m_nPort*/;
            EHOME_REGISTER_Func = new HCISUPCMS.DEVICE_REGISTER_CB(FRegisterCallBack);
            m_struCMSListenPara.fnCB = EHOME_REGISTER_Func;
            m_struCMSListenPara.pUserData = default;
            m_struCMSListenPara.byRes = new byte[32];
            Int32 dwSize = Marshal.SizeOf(m_struCMSListenPara);
            IntPtr ptrCMSListenPara = Marshal.AllocHGlobal(dwSize);
            Marshal.StructureToPtr(m_struCMSListenPara, ptrCMSListenPara, false);
            int iListen = -1;
            iListen = HCISUPCMS.NET_ECMS_StartListen(ref m_struCMSListenPara);
            CmsListenHandle = iListen;
            Marshal.FreeHGlobal(ptrCMSListenPara);
            if (iListen == -1)
                IsupLog.Info("NET_ECMS_StartListen Failed");
            else
                IsupLog.Info("NET_ECMS_StartListen Succ");
            HCISUPCMS.NET_ECMS_GetBuildVersion();

            if (!HCISUPCMS.NET_ECMS_SetSDKInitCfg(HCISUPCMS.NET_EHOME_CMS_INIT_CFG_LIBEAY_PATH, Marshal.StringToHGlobalAnsi(sCurPath + "\\libeay32.dll")))
                IsupLog.Info("NET_ECMS_SetSDKInitCfg, NET_EHOME_CMS_INIT_CFG_LIBEAY_PATH, Failed");
            if (!HCISUPCMS.NET_ECMS_SetSDKInitCfg(HCISUPCMS.NET_EHOME_CMS_INIT_CFG_SSLEAY_PATH, Marshal.StringToHGlobalAnsi(sCurPath + "\\ssleay32.dll")))
                IsupLog.Info("NET_ECMS_SetSDKInitCfg, NET_EHOME_CMS_INIT_CFG_SSLEAY_PATH, Failed");

            bRet = HCISUPStream.NET_ESTREAM_Init();
            bRet = HCISUPStream.NET_ESTREAM_SetLogToFile(3, _sdkLogDirectory, true);
            m_struAccessSecure.dwSize = (uint)Marshal.SizeOf(m_struAccessSecure);
            m_struAccessSecure.byAccessSecurity = m_byStreamSecureAccessType;
            IntPtr ptrStreamAccessSecure = Marshal.AllocHGlobal((int)m_struAccessSecure.dwSize);
            Marshal.StructureToPtr(m_struAccessSecure, ptrStreamAccessSecure, false);

            if (!HCISUPStream.NET_ESTREAM_SetSDKLocalCfg(ISUPSDK.AccessControl.WebApi.SdkService.ISUPSDK.NET_EHOME_LOCAL_CFG_TYPE.ACTIVE_ACCESS_SECURITY, ptrStreamAccessSecure))
                IsupLog.Info("NET_ESTREAM_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Success");
            else
                IsupLog.Info("NET_ESTREAM_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Success");
            Marshal.FreeHGlobal(ptrStreamAccessSecure);
            HCISUPStream.NET_ESTREAM_GetBuildVersion();


            bRet = HCISUPAlarm.NET_EALARM_Init();
            bRet = HCISUPAlarm.NET_EALARM_SetLogToFile(3, _sdkLogDirectory, true);
            m_struAccessSecure.dwSize = (UInt32)Marshal.SizeOf(m_struAccessSecure);
            m_struAccessSecure.byAccessSecurity = m_byAlarmSecureAccessType;
            ptrAccessSecure = Marshal.AllocHGlobal((int)m_struAccessSecure.dwSize);
            Marshal.StructureToPtr(m_struAccessSecure, ptrAccessSecure, false);

            if (!HCISUPAlarm.NET_EALARM_SetSDKLocalCfg(HCEHomePublic.NET_EHOME_LOCAL_CFG_TYPE.ACTIVE_ACCESS_SECURITY, ptrAccessSecure))
                IsupLog.Info("NET_EALARM_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Failed");
            else
                IsupLog.Info("NET_EALARM_SetSDKLocalCfg ACTIVE_ACCESS_SECURITY Success");


            HCISUPAlarm.NET_EHOME_ALARM_LISTEN_PARAM struAlarmListenParam = new();
            struAlarmListenParam.Init();
            string LocalIP = settings.ServerAddress;
            LocalIP.CopyTo(0, struAlarmListenParam.struAddress.szIP, 0, LocalIP.Length);
            struAlarmListenParam.struAddress.wPort = m_struServInfo.struUDPAlarmSever.wPort;
            AlarmMsgCallBack_Func = new HCISUPAlarm.EHomeMsgCallBack(AlarmMsgCallBack);
            struAlarmListenParam.fnMsgCb = AlarmMsgCallBack_Func;
            struAlarmListenParam.pUserData = default;
            struAlarmListenParam.byProtocolType = 1;
            struAlarmListenParam.byUseThreadPool = 0;
{
            var alarmListenHandle = HCISUPAlarm.NET_EALARM_StartListen(ref struAlarmListenParam);
            if (alarmListenHandle < 0)
            {
                IsupLog.Info("NET_EALARM_StartListen Failed Port " + struAlarmListenParam.struAddress.wPort);
            }
            else
            {
                _alarmListenHandles[alarmListenHandle] = 0;
            }
        }

            struAlarmListenParam.byProtocolType = 0;
            struAlarmListenParam.struAddress.wPort = m_struServInfo.struTCPAlarmSever.wPort;
            ptrTcpAlarm = new HCISUPAlarm.EHomeMsgCallBack(AlarmMsgCallBack);
            struAlarmListenParam.fnMsgCb = ptrTcpAlarm;
            struAlarmListenParam.pUserData = default;
{
            var alarmListenHandle = HCISUPAlarm.NET_EALARM_StartListen(ref struAlarmListenParam);
            if (alarmListenHandle < 0)
            {
                IsupLog.Info("NET_EALARM_StartListen Failed Port " + struAlarmListenParam.struAddress.wPort);
            }
            else
            {
                _alarmListenHandles[alarmListenHandle] = 0;
            }
        }

            struAlarmListenParam.struAddress.Init();
            settings.ServerAddress.CopyTo(0, struAlarmListenParam.struAddress.szIP, 0, settings.ServerAddress.Length);
            ptrMqtt = new HCISUPAlarm.EHomeMsgCallBack(AlarmMsgCallBack);
            struAlarmListenParam.fnMsgCb = ptrMqtt;
            struAlarmListenParam.pUserData = default;
            struAlarmListenParam.byUseThreadPool = 0;
            struAlarmListenParam.byUseCmsPort = 0;
            struAlarmListenParam.byProtocolType = 2;
            struAlarmListenParam.struAddress.wPort = (short)m_wAlarmServerMqttPort;
{
            var alarmListenHandle = HCISUPAlarm.NET_EALARM_StartListen(ref struAlarmListenParam);
            if (alarmListenHandle < 0)
            {
                IsupLog.Info("NET_EALARM_StartListen Failed Port " + struAlarmListenParam.struAddress.wPort);
            }
            else
            {
                _alarmListenHandles[alarmListenHandle] = 0;
            }
        }
        }

        /// <summary>
        /// 初始化服务参数XML配置
        /// </summary>
        private void InitParamFromXML()
        {

            String strTemp;
            // 控制台时代按进程工作目录读取；Web API 下统一用程序目录，避免受启动目录影响。
            doc.Load(Path.Combine(AppContext.BaseDirectory, "ISUPDemo.xml"));

            try
            {

                XmlNode root = doc.DocumentElement.FirstChild;

                m_struServInfo.dwKeepAliveSec = int.Parse(root.SelectSingleNode("//KeepAliveSeconds").InnerText); //心跳间隔，单位:s 如果设为0，表示默认心跳间隔为30s

                m_struServInfo.dwTimeOutCount = int.Parse(root.SelectSingleNode("//KeepAliveCount").InnerText);//心跳超时次数。如果设为0，表示默认次数为3次

                m_struServInfo.dwAlarmServerType = int.Parse(root.SelectSingleNode("//AlarmServerType").InnerText);
                strTemp = root.SelectSingleNode("//AlarmServerIP").InnerText;
                StrTcpAlarmIp = strTemp;
                StrUdpAlarmIp = strTemp;
                strTemp.CopyTo(0, m_struServInfo.struTCPAlarmSever.szIP, 0, strTemp.Length);
                strTemp.CopyTo(0, m_struServInfo.struUDPAlarmSever.szIP, 0, strTemp.Length);

                m_struServInfo.struUDPAlarmSever.wPort = short.Parse(root.SelectSingleNode("//AlarmServerUdpPort").InnerText);

                m_struServInfo.struTCPAlarmSever.wPort = short.Parse(root.SelectSingleNode("//AlarmServerTcpPort").InnerText);
                m_wAlarmServerMqttPort = ushort.Parse(root.SelectSingleNode("//AlarmServerMqttPort").InnerText);
                m_byUseCmsPort = int.Parse(root.SelectSingleNode("//AlarmServerPortUseCms").InnerText);

                strTemp = root.SelectSingleNode("//NTPServerIP").InnerText;
                strTemp.CopyTo(0, m_struServInfo.struNTPSever.szIP, 0, strTemp.Length);

                m_struServInfo.struNTPSever.wPort = short.Parse(root.SelectSingleNode("//NTPServerPort").InnerText);
                m_struServInfo.dwNTPInterval = int.Parse(root.SelectSingleNode("//NTPInterval").InnerText);

                m_struServInfo.dwPicServerType = int.Parse(root.SelectSingleNode("//PictureServerType").InnerText);
                strTemp = root.SelectSingleNode("//PictureServerIP").InnerText;
                PicServerip = strTemp;
                StrPicServIp = strTemp;
                strTemp.CopyTo(0, m_struServInfo.struPictureSever.szIP, 0, strTemp.Length);
                m_struServInfo.struPictureSever.wPort = short.Parse(root.SelectSingleNode("//PictureServerPort").InnerText);
                PicServerPort = int.Parse(root.SelectSingleNode("//PictureServerPort").InnerText);

                m_byCmsSecureAccessType = Convert.ToByte(root.SelectSingleNode("//CmsAccessSecurity").InnerText);
                m_byAlarmSecureAccessType = Convert.ToByte(root.SelectSingleNode("//AlarmAccessSecurity").InnerText);
                m_byStreamSecureAccessType = Convert.ToByte(root.SelectSingleNode("//StreamAccessSecurity").InnerText);


                //新增修改的内容，修复设备注册回调时，IP地址异常的bug，IP地址后面增加结束符'\0',否则传到底层dll库时会变成“地址00000000000”形式
                char[] TCPAlarmIP = StrTcpAlarmIp.ToCharArray();
                char[] UDPAlarmIP = StrUdpAlarmIp.ToCharArray();
                char[] PicServIP = StrPicServIp.ToCharArray();
                ClearCharArr(ref m_struServInfo.struPictureSever.szIP);
                ClearCharArr(ref m_struServInfo.struTCPAlarmSever.szIP);
                ClearCharArr(ref m_struServInfo.struUDPAlarmSever.szIP);
                Array.Copy(TCPAlarmIP, m_struServInfo.struTCPAlarmSever.szIP, TCPAlarmIP.Length);
                Array.Copy(UDPAlarmIP, m_struServInfo.struUDPAlarmSever.szIP, UDPAlarmIP.Length);
                Array.Copy(PicServIP, m_struServInfo.struPictureSever.szIP, PicServIP.Length);

            }

            catch (Exception)
            {
                //g_formList.AddLog(0, HCEHomePublic.OPERATION_FAIL_T, 0, ep.ToString());


            }
        }


        #endregion

        #region  地址
        private bool IsNULLAddress(IntPtr pOutValue)
        {
            string? temp = Marshal.PtrToStringUTF8(pOutValue);
            return string.IsNullOrWhiteSpace(temp) || temp == "0.0.0.0";
        }
        private void ClearCharArr(ref char[] source)
        {
            for (int i = 0; i < source.Length; ++i)
            {
                source[i] = '\0';
            }
        }

        private void GetAddressByType(int iType, int dwVersion, ref IntPtr pOutValue, int dwOutLen, ref int pSubValue, int dwSubLen)
        {
            int iMapType = -1;
            try
            {
#pragma warning disable CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
                XmlNode root = doc.SelectSingleNode("//AddressMap");
#pragma warning restore CS8600 // 将 null 字面量或可能为 null 的值转换为非 null 类型。
#pragma warning disable CS8602 // 解引用可能出现空引用。
                iMapType = int.Parse(root.SelectSingleNode("//Enable").InnerText);
#pragma warning restore CS8602 // 解引用可能出现空引用。
                if (iMapType != 1 && iMapType != -1 && iMapType != 0)
                {
                    iMapType = -1;
                }
                else if (iMapType == 1)
                {
                    iMapType = 1;
                }
                else
                {
                    iMapType = -1;
                }
            }
            catch
            {

            }

            if (iType == 0)
            {
                FormatRegAddress(ref pOutValue, dwOutLen, dwVersion, iMapType, ref doc);
            }
            else if (StreamAdd == iType)
            {
                GetStreamAddress(ref pOutValue, dwOutLen, ref pSubValue, dwSubLen, iMapType, ref doc);
            }
            else if (AudioAdd == iType)
            {
                GetAudioAddress(ref pOutValue, dwOutLen, ref pSubValue, dwSubLen, iMapType, ref doc);
            }
            else if (DASAdd == iType)
            {
                GetDasAddress(ref pOutValue, dwOutLen, ref pSubValue, dwSubLen, iMapType, ref doc);
            }
            else if (PSSAdd == iType)
            {
                GetPicServerAddress(ref pOutValue, dwOutLen, ref pSubValue, dwSubLen, iMapType, ref doc);
            }
        }
        /// <summary>
        /// 格式规则地址
        /// </summary>
        /// <param name="pOutValue"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="dwVersion"></param>
        /// <param name="iMapType"></param>
        /// <param name="doc"></param>
        private void FormatRegAddress(ref IntPtr pOutValue, int dwOutLen, int dwVersion, int iMapType, ref XmlDocument doc)
        {
            //XmlNode root = doc.SelectSingleNode("//AddressMap");
            HCISUPCMS.NET_EHOME_SERVER_INFO_V50 struServInfo = new();
            struServInfo.Init();
            //struServInfo = (HCISUPCMS.NET_EHOME_SERVER_INFO_V50)Marshal.PtrToStructure(pOutValue, typeof(HCISUPCMS.NET_EHOME_SERVER_INFO_V50));
            struServInfo = m_struServInfo;
            string strTemp;
            if (5 == dwVersion)
            {
                struServInfo.struTCPAlarmSever.wPort = (short)m_wAlarmServerMqttPort;
            }

            if (1 == iMapType)
            {
                strTemp = doc.SelectSingleNode("//AddressMap//AlarmServerIP").InnerText;
                ClearCharArr(ref struServInfo.struTCPAlarmSever.szIP);
                ClearCharArr(ref struServInfo.struUDPAlarmSever.szIP);
                strTemp.CopyTo(0, struServInfo.struTCPAlarmSever.szIP, 0, strTemp.Length);
                strTemp.CopyTo(0, struServInfo.struUDPAlarmSever.szIP, 0, strTemp.Length);
                //System.Buffer.BlockCopy(strTemp.ToCharArray(), 0, struServInfo.struTCPAlarmSever.szIP, 0, strTemp.ToCharArray().Length);
                //System.Buffer.BlockCopy(strTemp.ToCharArray(), 0, struServInfo.struUDPAlarmSever.szIP, 0, strTemp.ToCharArray().Length);

                struServInfo.struUDPAlarmSever.wPort = short.Parse(doc.SelectSingleNode("//AddressMap//AlarmServerUdpPort").InnerText);
                struServInfo.struTCPAlarmSever.wPort = short.Parse(doc.SelectSingleNode("//AddressMap//AlarmServerTcpPort").InnerText);

                if (5 == dwVersion)
                {
                    struServInfo.struTCPAlarmSever.wPort = short.Parse(doc.SelectSingleNode("//AddressMap//AlarmServerMqttPort").InnerText);
                }

                string strPicIp = doc.SelectSingleNode("//AddressMap//PictureServerIP").InnerText.ToString();

                //System.Buffer.BlockCopy(strPicIp.ToCharArray(), 0, struServInfo.struPictureSever.szIP, 0, strPicIp.ToCharArray().Length);
                ClearCharArr(ref struServInfo.struPictureSever.szIP);
                strPicIp.CopyTo(0, struServInfo.struPictureSever.szIP, 0, strPicIp.Length);

                struServInfo.struPictureSever.wPort = short.Parse(doc.SelectSingleNode("//AddressMap//PictureServerPort").InnerText);
            }

            string strptrTest = new string(struServInfo.struTCPAlarmSever.szIP, 0, 128);
            IntPtr ptrTest = Marshal.StringToHGlobalAnsi(strptrTest);
            if (IsNULLAddress(ptrTest))
            {
                string LocalIP =settings.ServerAddress /*ServerAddress*/;
                System.Buffer.BlockCopy(LocalIP.ToCharArray(), 0, struServInfo.struTCPAlarmSever.szIP, 0, LocalIP.Length);

            }
            Marshal.FreeHGlobal(ptrTest);

            strptrTest = new string(struServInfo.struUDPAlarmSever.szIP, 0, 128);
            ptrTest = Marshal.StringToHGlobalAnsi(strptrTest);
            if (IsNULLAddress(ptrTest))
            {
                string LocalIP = settings.ServerAddress /*ServerAddress*/;
                System.Buffer.BlockCopy(LocalIP.ToCharArray(), 0, struServInfo.struUDPAlarmSever.szIP, 0, LocalIP.Length);
            }
            Marshal.FreeHGlobal(ptrTest);

            Marshal.StructureToPtr(struServInfo, pOutValue, false);
        }


        /// <summary>
        /// 获取预览地址
        /// </summary>
        /// <param name="pOutValue"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pSubValue"></param>
        /// <param name="dwSubLen"></param>
        /// <param name="iMapType"></param>
        /// <param name="doc"></param>
        private void GetStreamAddress(ref IntPtr pOutValue, int dwOutLen, ref int pSubValue, int dwSubLen, int iMapType, ref XmlDocument doc)
        {
            if (-1 == iMapType || 0 == iMapType)
            {

                pOutValue = Marshal.StringToHGlobalAnsi(settings.ServerAddress  /*m_csLocalIP*/);
            }
            else
            {
                string StreamIP = doc.SelectSingleNode("//StreamServerIP").InnerText.ToString();
                char[] TempByte = new char[StreamIP.Length * sizeof(char)];
                System.Buffer.BlockCopy(StreamIP.ToCharArray(), 0, TempByte, 0, TempByte.Length);
                Marshal.Copy(TempByte, 0, pOutValue, TempByte.Length);

                int ServerPort = int.Parse(doc.SelectSingleNode("//StreamServerPort").InnerText);
                pSubValue = ServerPort;
            }
            if (IsNULLAddress(pOutValue))
            {
                string LocalIP =settings.ServerAddress /*ServerAddress*/;
                char[] IPtemp = new char[LocalIP.Length * sizeof(char)];
                System.Buffer.BlockCopy(LocalIP.ToCharArray(), 0, IPtemp, 0, LocalIP.Length);
                Marshal.Copy(IPtemp, 0, pOutValue, IPtemp.Length);
            }
        }

        /// <summary>
        /// 获取音频地址
        /// </summary>
        /// <param name="pOutValue"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pSubValue"></param>
        /// <param name="dwSubLen"></param>
        /// <param name="iMapType"></param>
        /// <param name="doc"></param>
        private void GetAudioAddress(ref IntPtr pOutValue, int dwOutLen, ref int pSubValue, int dwSubLen, int iMapType, ref XmlDocument doc)
        {
            if (-1 == iMapType || 0 == iMapType)
            {
                pOutValue = Marshal.StringToHGlobalAnsi(settings.ServerAddress /*m_csLocalIP*/);
            }
            else
            {
                string AudioIP = doc.SelectSingleNode("//AudioServerIP").InnerText.ToString();
                char[] TempByte = new char[AudioIP.Length * sizeof(char)];
                System.Buffer.BlockCopy(AudioIP.ToCharArray(), 0, TempByte, 0, TempByte.Length);
                Marshal.Copy(TempByte, 0, pOutValue, TempByte.Length);

                int ServerPort = int.Parse(doc.SelectSingleNode("//AudioServerPort").InnerText);
                pSubValue = ServerPort;
            }

            if (IsNULLAddress(pOutValue))
            {
                string LocalIP = settings.ServerAddress  /*ServerAddress*/;
                char[] IPtemp = new char[LocalIP.Length * sizeof(char)];
                System.Buffer.BlockCopy(LocalIP.ToCharArray(), 0, IPtemp, 0, LocalIP.Length);
                Marshal.Copy(IPtemp, 0, pOutValue, IPtemp.Length);
            }
        }

        /// <summary>
        /// 获取DAS地址  
        /// </summary>
        /// <param name="pOutValue"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pSubValue"></param>
        /// <param name="dwSubLen"></param>
        /// <param name="iMapType"></param>
        /// <param name="doc"></param>
        private void GetDasAddress(ref IntPtr pOutValue, int dwOutLen, ref int pSubValue, int dwSubLen, int iMapType, ref XmlDocument doc)
        {
            if (-1 == iMapType || 0 == iMapType)
            {
                pOutValue = Marshal.StringToHGlobalAnsi(settings.ServerAddress /*m_csLocalIP*/);
            }
            else
            {
                string DASIP = doc.SelectSingleNode("//DASIP").InnerText.ToString();
                ip = DASIP;
                //char[] TempByte = new char[DASIP.Length * sizeof(char)];
                //System.Buffer.BlockCopy(DASIP.ToCharArray(), 0, TempByte, 0, TempByte.Length);
                //Marshal.Copy(TempByte, 0, pOutValue, TempByte.Length);

                pOutValue = Marshal.StringToHGlobalAnsi(DASIP);
                int ServerPort = int.Parse(doc.SelectSingleNode("//DASPort").InnerText);
                pSubValue = ServerPort;
            }

            if (IsNULLAddress(pOutValue))
            {
                string LocalIP =settings.ServerAddress /*ServerAddress*/;
                char[] IPtemp = new char[LocalIP.Length * sizeof(char)];
                System.Buffer.BlockCopy(LocalIP.ToCharArray(), 0, IPtemp, 0, LocalIP.Length);
                Marshal.Copy(IPtemp, 0, pOutValue, IPtemp.Length);
            }
        }

        /// <summary>
        /// 获取图片服务地址
        /// </summary>
        /// <param name="pOutValue"></param>
        /// <param name="dwOutLen"></param>
        /// <param name="pSubValue"></param>
        /// <param name="dwSubLen"></param>
        /// <param name="iMapType"></param>
        /// <param name="doc"></param>
        private void GetPicServerAddress(ref IntPtr pOutValue, int dwOutLen, ref int pSubValue, int dwSubLen, int iMapType, ref XmlDocument doc)
        {

            if (-1 == iMapType || 0 == iMapType)
            {
                string LocalIP = settings.ServerAddress  /*ServerAddress*/;
                byte[] IPtemp = new byte[LocalIP.Length];
                StrTobyte(ref IPtemp, LocalIP);
                Marshal.Copy(IPtemp, 0, pOutValue, IPtemp.Length);
            }
            else
            {
                string strPicIp = doc.SelectSingleNode("//AddressMap//PictureServerIP").InnerText.ToString();
                PicServerip = doc.SelectSingleNode("//AddressMap//PictureServerIP").InnerText.ToString();
                //char[] TempByte = new char[strPicIp.Length * sizeof(char)];
                //System.Buffer.BlockCopy(strPicIp.ToCharArray(), 0, TempByte, 0, TempByte.Length);
                byte[] TempByte = new byte[strPicIp.Length];
                StrTobyte(ref TempByte, strPicIp);
                //pOutValue = Marshal.StringToHGlobalAnsi(strPicIp);
                Marshal.Copy(TempByte, 0, pOutValue, strPicIp.Length);
                int ServerPort = int.Parse(doc.SelectSingleNode("//AddressMap//PictureServerPort").InnerText);
                pSubValue = ServerPort;
            }
            if (IsNULLAddress(pOutValue))
            {
                string LocalIP = settings.ServerAddress  /*ServerAddress*/;
                byte[] IPtemp = new byte[LocalIP.Length];
                StrTobyte(ref IPtemp, LocalIP);
                Marshal.Copy(IPtemp, 0, pOutValue, IPtemp.Length);
            }
        }

        private void StrTobyte(ref byte[] des, string source)
        {
            if (source.Length <= 0)
                return;
            for (int i = 0; i < source.Length; ++i)
            {
                des[i] = (byte)source[i];
            }
        }
        #endregion


        #region 报警转发（已迁移到消费者侧）

        // 报警报文的 MinorType 判定、图片下载与下游队列转发已统一移到
        // Services/Events/AcsAlarmForwardService.cs（由 AcsEventQueueConsumerService 在
        // 消费队列消息时调用）：生产端只负责把原始报文入队，不做业务判定。

        /// <summary>
        /// 清理过期文件
        /// </summary>
        private void CleanDayBefore()
        {

            try
            {
                string path = settings.PicturePath;
                DirectoryInfo dir = new(path);
                FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  // 返回目录中所有文件和子目录
                                                                       //FileSystemInfo[] fileinfo = dir.GetDirectories();  // 返回获取当前目录的子目录

                string path1 = settings.Photo;
                DirectoryInfo dir1 = new(path1);
                FileSystemInfo[] fileinfo1 = dir1.GetFileSystemInfos();  // 返回目录中所有文件和子目录

                DateTime dtNow = DateTime.Now.Date.AddDays(-180);
                foreach (FileSystemInfo i in fileinfo)
                {
                    if (i is DirectoryInfo)     // 判断是否文件夹
                    {
                        if (i.Name == "STORAGE")
                        {
                            DirectoryInfo dirfile = new(i.FullName);
                            FileSystemInfo[] subfileinfo = dirfile.GetFileSystemInfos();
                            foreach (FileSystemInfo item in subfileinfo)        // 删除人脸机上传数据的过期文件
                                if (item is FileInfo && item.CreationTime < dtNow)
                                    File.Delete(item.FullName);
                        }
                        else if (i.CreationTime < dtNow)
                        {
                            DirectoryInfo subdir = new(i.FullName);
                            subdir.Delete(true);          // 删除子目录和文件
                        }
                    }
                    else
                    {
                        if (i.CreationTime < dtNow)
                        {
                            File.Delete(i.FullName); //删除文件
                        }
                    }

                }
                foreach (FileSystemInfo i in fileinfo1)
                {
                    if (i is DirectoryInfo)     // 判断是否文件夹
                    {
                        if (i.Name == "STORAGE")
                        {
                            DirectoryInfo dirfile = new(i.FullName);
                            FileSystemInfo[] subfileinfo = dirfile.GetFileSystemInfos();
                            foreach (FileSystemInfo item in subfileinfo)        // 删除人脸机上传数据的过期文件
                                if (item is FileInfo && item.CreationTime < dtNow)
                                    File.Delete(item.FullName);
                        }
                        else if (i.CreationTime < dtNow)
                        {
                            DirectoryInfo subdir = new(i.FullName);
                            subdir.Delete(true);          // 删除子目录和文件
                        }
                    }
                    else
                    {
                        if (i.CreationTime < dtNow)
                        {
                            File.Delete(i.FullName); //删除文件
                        }
                    }

                }
            }
            catch (Exception)
            {

                
            }
            //Thread.Sleep(14400000);
        }



        // 统一的异常日志记录
        private void LogException(string filename, Exception ex)
        {
            var error = ex.InnerException ?? ex;
            var logContent = $"{error.Message}\n{error.StackTrace}\n{error.Source}";
            Common.WriteFileHours(filename, true, logContent);
        }
        #endregion
        #region 计时器


        /// <summary>停止全部定时任务（宿主关闭时调用）。</summary>


        
        

        #endregion
        public async Task InitializeAsync(IConfiguration configuration, IsupSdkOptions options, CancellationToken cancellationToken)
        {
            // 配置由宿主（Web API）注入的 IConfiguration 提供，取代控制台时代的本地 ConfigurationBuilder。
            // 节名默认 "Settings"，可通过 IsupSdkOptions.SettingsSection 覆盖。
            settings = configuration.GetSection(options.SettingsSection).Get<Settings>() ?? new Settings();
            settings.WebCheckCode1 = Common.Decrypt(settings.WebCheckCode, "7ijn5tgb");

            // SDK 组件日志目录（原控制台程序硬编码为 C:/EHomeSdkLog/）。
            _sdkLogDirectory = string.IsNullOrWhiteSpace(options.SdkLogDirectory) ? "C:/EHomeSdkLog/" : options.SdkLogDirectory;
            try
            {
                Directory.CreateDirectory(_sdkLogDirectory);
            }
            catch (Exception)
            {
                // 目录创建失败时沿用 SDK 默认行为。
            }
           
            m_struServInfo.Init();

            InitParamFromXML();

            InitLib();

            CleanDayBefore();

            
            IsInitialized = true;
            await Task.CompletedTask;
        }

        #region 权限同步定时任务（全局调度）

        /// <summary>在线设备及其同步状态。</summary>
        private sealed class DeviceSyncState
        {
            /// <summary>当前注册句柄（设备重连后会更新为最新值）。</summary>
            public int Handle { get; set; }

            /// <summary>取消源（掉线时取消；当前无消费方，保留以便后续接入取消）。</summary>
            public CancellationTokenSource Cts { get; } = new();
        }

        /// <summary>
        /// 轻量探活：向设备发一次 ISAPI deviceInfo 请求。
        /// 用于识别“断网但未触发 OFF 回调”的设备，避免对已离线设备继续下发。
        /// </summary>
        private static bool ProbeDeviceOnline(int handle, uint timeoutMs = 3000)
        {
            if (handle < 0)
            {
                return false;
            }

            var urlBytes = Encoding.UTF8.GetBytes("/ISAPI/System/deviceInfo?format=json");
            var pUrl = Marshal.AllocHGlobal(Math.Max(urlBytes.Length, 1));
            var pOut = Marshal.AllocHGlobal(4096);
            var pCfg = Marshal.AllocHGlobal(Marshal.SizeOf<HCISUPCMS.NET_EHOME_PTXML_PARAM>());
            try
            {
                Marshal.Copy(urlBytes, 0, pUrl, urlBytes.Length);

                var param = new HCISUPCMS.NET_EHOME_PTXML_PARAM
                {
                    pRequestUrl = pUrl,
                    dwRequestUrlLen = (uint)urlBytes.Length,
                    pCondBuffer = IntPtr.Zero,
                    dwCondSize = 0,
                    pInBuffer = IntPtr.Zero,
                    dwInSize = 0,
                    pOutBuffer = pOut,
                    dwOutSize = 4096,
                    dwRecvTimeOut = timeoutMs,
                    byRes = new byte[24],
                };

                Marshal.StructureToPtr(param, pCfg, false);
                return HCISUPCMS.NET_ECMS_GetPTXMLConfig(handle, pCfg);
            }
            catch (Exception)
            {
                return false;
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
                Marshal.FreeHGlobal(pOut);
                Marshal.FreeHGlobal(pCfg);
            }
        }

        /// <summary>同步轮次是否正在执行（防止上一轮未结束时重入）。</summary>
        private int _syncRunning;

        /// <summary>
        /// 启动权限同步定时任务：按 <c>Settings:SyncCrontab</c> 周期性触发，
        /// 每轮遍历所有在线设备逐个同步（原先是每台设备一个常驻 while 循环）。
        /// </summary>

        /// <summary>
        /// 定时触发入口：直接顺序遍历在线设备逐个同步（不再用 Task.Run 包装）。
        /// </summary>
        /// <summary>定时任务体：遍历所有在线设备逐个同步一轮（供 SyncDevicesCronJob 调用）。</summary>
        public async Task SyncOnlineDevicesAsync(CancellationToken ct = default)
        {
            if (Interlocked.CompareExchange(ref _syncRunning, 1, 0) != 0)
            {
                IsupLog.Warn("上一轮权限同步尚未结束，跳过本轮。");
                return;
            }

            try
            {
                foreach (var deviceId in AllTask.Keys.ToArray())
                {
                    if (!AllTask.ContainsKey(deviceId))
                    {
                        continue; // 本轮开始后设备已下线
                    }

                    try
                    {
                        await SyncDeviceOnceAsync(deviceId).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
                    {
                        // 平台不可达 / 网络抖动时的常态：只记一行、不打堆栈，避免刷屏。
                        IsupLog.Warn("设备 {0} 权限同步超时或被取消：{1}", deviceId, ex.Message);
                    }
                    catch (Exception ex)
                    {
                        // 单台设备失败不影响其它设备；只记日志，不终止进程。
                        LogException("SyncSchedule.txt", ex);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _syncRunning, 0);
            }
        }

        /// <summary>定时任务体：给所有在线设备下发一次当前时间（供 DeviceTimeCronJob 调用）。</summary>
        public async Task SyncDeviceTimeAllAsync(CancellationToken ct = default)
        {
            foreach (var pair in DeviceHandles.ToArray())
            {
                if (ct.IsCancellationRequested)
                {
                    return;
                }

                var localTime = await Common.GetBeijingTimeAsync(settings.NtpServer).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(localTime))
                {
                    continue;
                }

                ISUPDevTime.Post(pair.Key, localTime);
            }
        }

        /// <summary>对一台在线设备执行一轮权限同步（原常驻循环的循环体，逐行等价）。</summary>
        private async Task SyncDeviceOnceAsync(string deviceId)
        {
            // 设备已掉线（在线标记被移除）则跳过，不再发起同步。
            if (!AllTask.TryGetValue(deviceId, out var state))
            {
                return;
            }

            // 探活：设备断网但未收到 OFF 回调时这里会失败 → 跳过本轮（设备恢复后自动继续）。
            if (!ProbeDeviceOnline(state.Handle))
            {
                IsupLog.Warn("设备 {0} 探活失败（疑似离线），本轮同步跳过。", deviceId);
                return;
            }

            // 直接用最新注册句柄（不再反查 MatchDic，避免重连后拿到旧句柄）。
            var handle = state.Handle;
            im_machine machine = new()
            {
                MachineSN = deviceId,
                HKLoginId = handle,
                lLoginID = handle,
            };

            string authorizationToken = Common.SetToken(settings.WebCheckCode1);
            var url = Common.GetUrl(settings.IsAuthRuleBindMachine, machine.MachineSN, 1);
            string urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken).ConfigureAwait(false);
            // 等待平台接口期间设备可能已掉线，这里再确认一次，避免对离线设备继续下发。
            if (!AllTask.ContainsKey(deviceId))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(urlDB))
            {
                return;
            }

            var updateSetting = JsonHepler.FromJson<UpdateData<bool>>(urlDB);
            if (updateSetting?.Data == true)
            {
                await SyncAuthRuleInformation.SyncDevicesAsync(machine, settings).ConfigureAwait(false);
            }
            else
            {
                await SyncInformation.SyncDevicesAsync(machine, settings).ConfigureAwait(false);
            }
        }

        #endregion

        #region Web API 宿主暴露的只读状态与手动入口

        /// <summary>SDK 是否已完成初始化（CMS / 报警 / 图片 / 流媒体组件均已启动）。</summary>
        public bool IsInitialized { get; private set; }

        /// <summary>当前生效的配置（appsettings.json 的 Settings 节）。</summary>
        public Settings CurrentSettings => settings;

        /// <summary>图片服务器 IP（消费者侧回取报警图片时用于拼 URL，来自 ISUPDemo.xml 的 AddressMap）。</summary>
        public string PictureServerIp => PicServerip;

        /// <summary>图片服务器端口。</summary>
        public int PictureServerPort => PicServerPort;

        /// <summary>图片服务器类型：2-云存储，3-KMS（其它值表示图片地址直接取自报文）。</summary>
        public int PictureServerType => m_struServInfo.dwPicServerType;

        /// <summary>CMS 设备注册监听句柄，-1 表示未启动。</summary>
        public int ListenHandle => CmsListenHandle;

        /// <summary>
        /// 动态启动一次 CMS 设备注册监听（Web API 的 <c>/api/devices/listen</c> 使用），
        /// 复用启动阶段的监听参数与注册回调，返回监听句柄（&lt;0 表示失败）。
        /// </summary>
        public int StartListen(string? ip, ushort? port)
        {
            var listenIp = string.IsNullOrWhiteSpace(ip) ? (settings.ServerAddress ?? "0.0.0.0") : ip!;
            var listenPort = port is > 0 ? port!.Value : (ushort)settings.InitPort;

            m_struCMSListenPara.struAddress.Init();
            listenIp.CopyTo(0, m_struCMSListenPara.struAddress.szIP, 0, listenIp.Length);
            m_struCMSListenPara.struAddress.wPort = (short)listenPort;
            EHOME_REGISTER_Func = new HCISUPCMS.DEVICE_REGISTER_CB(FRegisterCallBack);
            m_struCMSListenPara.fnCB = EHOME_REGISTER_Func;
            m_struCMSListenPara.pUserData = default;
            m_struCMSListenPara.byRes = new byte[32];

            var handle = HCISUPCMS.NET_ECMS_StartListen(ref m_struCMSListenPara);
            if (handle >= 0)
            {
                CmsListenHandle = handle;
            }

            return handle;
        }

        /// <summary>停止指定 CMS 监听句柄。</summary>
        public bool StopListen(int handle) => handle >= 0 && HCISUPCMS.NET_ECMS_StopListen(handle);
        /// <summary>收到门禁报警报文时触发，供报警缓存与事件管道订阅。</summary>
        public event EventHandler<IsupAlarmEventArgs>? AlarmReceived;

        /// <summary>已建立的报警监听句柄集合。</summary>
        public IReadOnlyCollection<int> AlarmListenHandles => _alarmListenHandles.Keys.ToArray();

        /// <summary>图片存储（SS）监听句柄，-1 表示未启动。</summary>
        public long SsListenHandle { get; private set; } = -1;

        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, byte> _alarmListenHandles = new();

        /// <summary>
        /// 动态启动一次报警监听（protocolType：0-TCP，1-UDP，2-MQTT），返回监听句柄（&lt;0 表示失败）。
        /// </summary>
        public int StartAlarmListen(string? ip, ushort? port, byte protocolType = 1)
        {
            var listenIp = string.IsNullOrWhiteSpace(ip) ? (settings.ServerAddress ?? "0.0.0.0") : ip!;
            var listenPort = port is > 0
                ? port!.Value
                : (protocolType == 2 ? m_wAlarmServerMqttPort : (ushort)m_struServInfo.struUDPAlarmSever.wPort);

            var param = new HCISUPAlarm.NET_EHOME_ALARM_LISTEN_PARAM();
            param.Init();
            listenIp.CopyTo(0, param.struAddress.szIP, 0, listenIp.Length);
            param.struAddress.wPort = (short)listenPort;
            param.fnMsgCb = new HCISUPAlarm.EHomeMsgCallBack(AlarmMsgCallBack);
            param.pUserData = default;
            param.byProtocolType = protocolType;
            param.byUseThreadPool = 0;

            var handle = HCISUPAlarm.NET_EALARM_StartListen(ref param);
            if (handle >= 0)
            {
                _alarmListenHandles[handle] = 0;
            }

            return handle;
        }

        /// <summary>停止指定报警监听句柄。</summary>
        public bool StopAlarmListen(int handle)
        {
            var ok = handle >= 0 && HCISUPAlarm.NET_EALARM_StopListen(handle);
            if (ok)
            {
                _alarmListenHandles.TryRemove(handle, out _);
            }

            return ok;
        }

        /// <summary>在线设备：注册句柄 → 设备 ID（序列号）。</summary>
        public IReadOnlyDictionary<int, string> DeviceHandles => MatchDic;

        /// <summary>在线设备 ID 列表。</summary>
        public IReadOnlyList<string> DeviceIds => MatchDic.Values.ToArray();

        /// <summary>正在执行权限同步循环的设备数量。</summary>
        public int SyncingDeviceCount => AllTask.Count;

        /// <summary>已接收并处理的报警报文数。</summary>
        public long AlarmCount => Interlocked.Read(ref _alarmCount);

        /// <summary>最近一次收到报警报文的时间。</summary>
        public DateTimeOffset? LastAlarmAt { get; private set; }

        /// <summary>报警计数器（回调线程写入，接口线程读取）。</summary>
        private long _alarmCount;

        /// <summary>SDK 各组件日志目录（SDK 的 SetLogToFile 使用）。</summary>
        private string _sdkLogDirectory = "C:/EHomeSdkLog/";

        /// <summary>指定设备的权限同步循环是否正在运行。</summary>
        public bool IsSyncLoopRunning(string deviceId) => AllTask.ContainsKey(deviceId);

        /// <summary>
        /// 手动触发一台在线设备的门禁权限同步，等价于设备上线之后自动执行的那一轮。
        /// </summary>
        public async Task<IsupSyncResult> SyncDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            var match = MatchDic.FirstOrDefault(w => w.Value == deviceId);
            if (match.Equals(default(KeyValuePair<int, string>)))
            {
                return new IsupSyncResult { DeviceId = deviceId, Success = false, Message = "设备不在线或不存在。" };
            }

            try
            {
                im_machine machine = new()
                {
                    MachineSN = deviceId,
                    HKLoginId = match.Key,
                    lLoginID = match.Key,
                };

                string authorizationToken = Common.SetToken(settings.WebCheckCode1);
                var url = Common.GetUrl(settings.IsAuthRuleBindMachine, machine.MachineSN, 1);
                string urlDB = await HttpHelper.GetUrlAsync(url, authorizationToken).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(urlDB))
                {
                    return new IsupSyncResult { DeviceId = deviceId, Success = false, Message = "查询认证规则绑定关系失败（平台无返回）。" };
                }

                var updateSetting = JsonHepler.FromJson<UpdateData<bool>>(urlDB);
                bool useAuthRule = updateSetting?.Data ?? false;
                if (useAuthRule)
                {
                    await SyncAuthRuleInformation.SyncDevicesAsync(machine, settings).ConfigureAwait(false);
                }
                else
                {
                    await SyncInformation.SyncDevicesAsync(machine, settings).ConfigureAwait(false);
                }

                return new IsupSyncResult
                {
                    DeviceId = deviceId,
                    Success = true,
                    UsedAuthRule = useAuthRule,
                    Message = "同步完成。",
                };
            }
            catch (Exception ex)
            {
                LogException("ManualSync.txt", ex);
                return new IsupSyncResult { DeviceId = deviceId, Success = false, Message = ex.Message };
            }
        }

        /// <summary>
        /// 手动触发一台在线设备的 NTP 校时，与定时任务里的逻辑一致。
        /// </summary>
        public async Task<bool> SyncDeviceTimeAsync(string deviceId)
        {
            var match = MatchDic.FirstOrDefault(w => w.Value == deviceId);
            if (match.Equals(default(KeyValuePair<int, string>)))
            {
                return false;
            }

            string localTime = await Common.GetBeijingTimeAsync(settings.NtpServer).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(localTime))
            {
                return false;
            }

            ISUPDevTime.Post(match.Key, localTime);
            return true;
        }

        #endregion
    }
}
