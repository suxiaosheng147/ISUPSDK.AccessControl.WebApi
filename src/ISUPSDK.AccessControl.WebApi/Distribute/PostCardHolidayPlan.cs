using ISUPSDK.AccessControl.WebApi.Services.Logging;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using System;
using System.Runtime.InteropServices;
using System.Text;


namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    /// <summary>
    /// 卡假日计划
    /// </summary>
    public class PostCardHolidayPlan
    {
        

        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayPlanData"></param>
        /// <returns></returns>
        public static bool SetCardHolidayPlan(int iUserID, HolidayPeriodData holidayPlanData)
        {
            var PlanTimes = $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime1, holidayPlanData.ToTime1)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime2, holidayPlanData.ToTime2)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime3, holidayPlanData.ToTime3)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime4, holidayPlanData.ToTime4)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime5, holidayPlanData.ToTime5)}"+
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime6, holidayPlanData.ToTime6)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime7, holidayPlanData.ToTime7)}"+
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime8, holidayPlanData.ToTime8)}";
            return SetCardHolidayPlan(iUserID, 1, holidayPlanData.HolidayPlanNo, holidayPlanData.StartDate, holidayPlanData.EndDate, PlanTimes);
        }
        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayPlanData"></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetCardHolidayPlanResult(int iUserID, HolidayPeriodData holidayPlanData)
        {
            var PlanTimes = $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime1, holidayPlanData.ToTime1)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime2, holidayPlanData.ToTime2)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime3, holidayPlanData.ToTime3)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime4, holidayPlanData.ToTime4)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime5, holidayPlanData.ToTime5)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime6, holidayPlanData.ToTime6)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime7, holidayPlanData.ToTime7)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime8, holidayPlanData.ToTime8)}";
            return SetCardHolidayPlanResult(iUserID, 1, holidayPlanData.HolidayPlanNo, holidayPlanData.StartDate, holidayPlanData.EndDate, PlanTimes);
        }


        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="Enabled">是否启用</param>
        /// <param name="StartDate">假日计划开始时间 年月日   2017-01-01T 00:00:00</param>
        /// <param name="EndDate">假日计划结束时间 年月日   2017-01-01T 00:00:00</param>
        /// <param name="HolidayPlanNo">假日计划编号</param>
        /// <param name="PlanTimes">必选项，时间段，最多 8 个   xs: time，ISO8601 time</param>
        /// <returns></returns>
        public static bool SetCardHolidayPlan(int iUserID, int Enabled, string HolidayPlanNo, string StartDate, string EndDate, string PlanTimes)
        {
            
            var inputXml = $@"
                           <Params><ConfigCmd>SetCardHolidayPlan</ConfigCmd>
                           <ConfigParam1>{HolidayPlanNo}</ConfigParam1>
                           <ConfigParam2>0</ConfigParam2>
                           <ConfigParam3>0</ConfigParam3>
                           <ConfigParam4>0</ConfigParam4>
                           <ConfigXML>
                          <HolidayPlan>
                          <enabled>{Enabled}</enabled>
                          <startDate>{StartDate}</startDate>
                          <endDate>{EndDate}</endDate>
                         <TimeList>{PlanTimes}</TimeList>
                         </HolidayPlan>
                         </ConfigXML>
                         </Params>";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(1 * 1024);
            pInputCfgXml.dwOutSize = 1 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                IsupLog.Info(inputXml);
                IsupLog.Info($"设置访问权限假日计划失败{iLastErr}。{ErrCodeMean}。");

                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }
            //string strOutPuts = PtrToStringAnsi(pInputCfgXml.pOutBuf, (int)pInputCfgXml.dwOutSize);
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }

        /// <summary>
        /// 设置卡假日计划结果
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="Enabled"></param>
        /// <param name="HolidayPlanNo"></param>
        /// <param name="StartDate"></param>
        /// <param name="EndDate"></param>
        /// <param name="PlanTimes"></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetCardHolidayPlanResult(int iUserID, 
            int Enabled, string HolidayPlanNo
            , string StartDate, 
            string EndDate, 
            string PlanTimes)
        {

            //判断这个假日计划编号是否设置了和时间上是否要下发的一致，如果不一致，重新下发，否则就返回成功

            string getxml = GetCardHolidayPlan(iUserID, HolidayPlanNo);



            var result = new IssueCardDoorResult();

            // 构建请求 XML
            var inputXml = $@"
                   <Params><ConfigCmd>SetCardHolidayPlan</ConfigCmd>
                   <ConfigParam1>{HolidayPlanNo}</ConfigParam1>
                   <ConfigParam2>0</ConfigParam2>
                   <ConfigParam3>0</ConfigParam3>
                   <ConfigParam4>0</ConfigParam4>
                   <ConfigXML>
                  <HolidayPlan>
                  <enabled>{Enabled}</enabled>
                  <startDate>{StartDate}</startDate>
                  <endDate>{EndDate}</endDate>
                 <TimeList>{PlanTimes}</TimeList>
                 </HolidayPlan>
                 </ConfigXML>
                 </Params>";
            result.RequestXML = inputXml;

            string xml = GetCardHolidayPlan(iUserID, HolidayPlanNo);

            



            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            byte[] byInputParam = Encoding.UTF8.GetBytes(inputXml);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(2 * 1024);
            pInputCfgXml.dwOutSize = 2 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            try
            {
                if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
                {
                    int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                    result.Success = false;
                    result.ErrorMessage = ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? errMsg) ? errMsg : $"未知错误码 {iLastErr}";

                    // 尝试读取响应 XML（即使失败也可能有响应）
                    //result.ResponseXML = ReadOutBuffer(pInputCfgXml.pOutBuf, pInputCfgXml.dwOutSize);
                }
                else
                {
                    result.Success = true;
                    //result.ResponseXML = ReadOutBuffer(pInputCfgXml.pOutBuf, pInputCfgXml.dwOutSize);
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                // 异常时，无法读取响应，留空
            }
            finally
            {
                // 释放资源
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                Marshal.FreeHGlobal(ptrCfg);
            }

            return result;
        }

        // 辅助方法：从输出缓冲区读取字符串
        private static string ReadOutBuffer(IntPtr pOutBuf, uint dwOutSize)
        {
            if (pOutBuf == IntPtr.Zero || dwOutSize == 0)
                return string.Empty;

            try
            {
                byte[] buffer = new byte[dwOutSize];
                Marshal.Copy(pOutBuf, buffer, 0, (int)dwOutSize);
                // 使用 UTF-8 解码，并去除末尾的 null 字符
                string response = Encoding.UTF8.GetString(buffer).TrimEnd('\0');
                return response;
            }
            catch
            {
                return string.Empty;
            }
        }



        /// <summary>
        /// 查看访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayPlanNo">假日计划编号</param>
        /// <returns></returns>
        private static string GetCardHolidayPlan(int iUserID, string HolidayPlanNo)
        {

            var inputXml = $@"
                           <Params>
<ConfigCmd>GetCardHolidayPlan</ConfigCmd>
<ConfigParam1>{HolidayPlanNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
</Params>
";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(2 * 1024);
            pInputCfgXml.dwOutSize = 2 * 1024;

            string strCmd = "GETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                
                IsupLog.Info($"设置访问权限假日计划失败{iLastErr}。{ErrCodeMean}。");

                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return "";
            }
            else
            {

            }
            string strOutPuts = PtrToStringAnsi(pInputCfgXml.pOutBuf, (int)pInputCfgXml.dwOutSize);
            IsupLog.Info(strOutPuts.ToLower());
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return strOutPuts;
        }


        public unsafe static string PtrToStringAnsi(IntPtr ptr, int len)
        {
            // 如果指针为空或者长度为0，返回空字符串
            if (ptr == IntPtr.Zero || len <= 0)
                return string.Empty;

            // 创建一个字节数组来存储字符串的内容
            byte[] bytes = new byte[len];

            // 从非托管内存复制数据到字节数组
            for (int i = 0; i < len; i++)
            {
                bytes[i] = *((byte*)ptr + i);
            }

            // 使用 ANSI 编码将字节数组转换为字符串
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        /// <summary>
        /// 构建卡假日计划时间点
        /// </summary>
        /// <param name="fromTime">Time  开始时间点</param>
        /// <param name="toTime">Time  结束时间点</param>
        /// <returns></returns>
        public static string BuildCardHolidayPlanTime(string fromTime, string toTime)
        {
            if (!string.IsNullOrWhiteSpace(fromTime) && !string.IsNullOrWhiteSpace(toTime))
                return $@"<Time><startTime>{fromTime}</startTime><endTime>{toTime}</endTime></Time>";
            else
                return string.Empty;
        }


        /// <summary>
        /// 设置访问权限假日组。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayGroupNo">假日分组编号</param>
        /// <param name="groupName">组名称</param>
        /// <param name="holidayNo">假日计划编号，最多 16 个 示例：1,2,3<!--必选项，xs:integer--></param>
        /// <returns></returns>
        public static bool SetCardHolidayGroup(int iUserID, string HolidayGroupNo, string groupName, string holidayNo)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = "JustTest";
            }

            var inputXml = $@"<Params>
<ConfigCmd>SetCardHolidayGroup</ConfigCmd>
<ConfigParam1>{HolidayGroupNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<HolidayGroup>
<enabled>1</enabled>
<groupName>{groupName}</groupName>
<holidayNo>{holidayNo}</holidayNo>
</HolidayGroup>
</ConfigXML>
</Params>
";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(1 * 1024);
            pInputCfgXml.dwOutSize = 1 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
               
                IsupLog.Info($"设置访问权限假日组{iLastErr}。{ErrCodeMean}。");
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }



        /// <summary>
        /// 查看访问权限假日组。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayPlanNo">假日计划编号</param>
        /// <returns></returns>
        private static string GetCardHolidayPlanGroup(int iUserID, string HolidayPlanNo)
        {

            var inputXml = $@"
                           <Params>
<ConfigCmd>GetCardHolidayGroup</ConfigCmd>
<ConfigParam1>{HolidayPlanNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
</Params>
";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(2 * 1024);
            pInputCfgXml.dwOutSize = 2 * 1024;

            string strCmd = "GETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                IsupLog.Info(inputXml);
                IsupLog.Info($"设置访问权限假日计划失败{iLastErr}。{ErrCodeMean}。");

                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return "";
            }
            else
            {

            }
            string strOutPuts = PtrToStringAnsi(pInputCfgXml.pOutBuf, (int)pInputCfgXml.dwOutSize);
            IsupLog.Info(strOutPuts.ToLower());
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return strOutPuts;
        }

        /// <summary>
        /// 设置访问权限假日组。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayGroupNo">假日分组编号</param>
        /// <param name="groupName">组名称</param>
        /// <param name="holidayNo">假日计划编号，最多 16 个 示例：1,2,3<!--必选项，xs:integer--></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetCardHolidayGroupResult(int iUserID, string HolidayGroupNo, string groupName, string holidayNo)
        {
            var result = new IssueCardDoorResult();

            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = "JustTest";
            }

            // 构建请求 XML
            var inputXml = $@"<Params>
<ConfigCmd>SetCardHolidayGroup</ConfigCmd>
<ConfigParam1>{HolidayGroupNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<HolidayGroup>
<enabled>1</enabled>
<groupName>{groupName}</groupName>
<holidayNo>{holidayNo}</holidayNo>
</HolidayGroup>
</ConfigXML>
</Params>";
            result.RequestXML = inputXml;


            string xml = GetCardHolidayPlanGroup(iUserID, HolidayGroupNo);



            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            byte[] byInputParam = Encoding.UTF8.GetBytes(inputXml);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(1 * 1024);
            pInputCfgXml.dwOutSize = 1 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            try
            {
                if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
                {
                    int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                    result.Success = false;
                    result.ErrorMessage = ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? errMsg) ? errMsg : $"未知错误码 {iLastErr}";
                }
                else
                {
                    result.Success = true;
                }

                // 无论成功与否，都尝试读取响应 XML
                //result.ResponseXML = ReadOutBuffer(pInputCfgXml.pOutBuf, pInputCfgXml.dwOutSize);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.ResponseXML = string.Empty;
            }
            finally
            {
                // 释放所有非托管资源
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                Marshal.FreeHGlobal(ptrCfg);
            }

            return result;
        }


        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayPlanData"></param>
        /// <returns></returns>
        public static bool SetCardHolidayPlan(int iUserID, HolidayPeriodData holidayPlanData,out string strOutPuts)
        {
            var PlanTimes = $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime1, holidayPlanData.ToTime1)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime2, holidayPlanData.ToTime2)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime3, holidayPlanData.ToTime3)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime4, holidayPlanData.ToTime4)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime5, holidayPlanData.ToTime5)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime6, holidayPlanData.ToTime6)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime7, holidayPlanData.ToTime7)}" +
                $"{BuildCardHolidayPlanTime(holidayPlanData.FromTime8, holidayPlanData.ToTime8)}";
            return SetCardHolidayPlan(iUserID, 1, holidayPlanData.HolidayPlanNo, holidayPlanData.StartDate, holidayPlanData.EndDate, PlanTimes,out strOutPuts);
        }


        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="Enabled">是否启用</param>
        /// <param name="StartDate">假日计划开始时间 年月日   2017-01-01T 00:00:00</param>
        /// <param name="EndDate">假日计划结束时间 年月日   2017-01-01T 00:00:00</param>
        /// <param name="HolidayPlanNo">假日计划编号</param>
        /// <param name="PlanTimes">必选项，时间段，最多 8 个   xs: time，ISO8601 time</param>
        /// <returns></returns>
        public static bool SetCardHolidayPlan(int iUserID, int Enabled, string HolidayPlanNo, string StartDate, string EndDate, string PlanTimes,out string strOutPuts)
        {
            strOutPuts = "";
            var inputXml = $@"
                           <Params><ConfigCmd>SetCardHolidayPlan</ConfigCmd>
                           <ConfigParam1>{HolidayPlanNo}</ConfigParam1>
                           <ConfigParam2>0</ConfigParam2>
                           <ConfigParam3>0</ConfigParam3>
                           <ConfigParam4>0</ConfigParam4>
                           <ConfigXML>
                          <HolidayPlan>
                          <enabled>{Enabled}</enabled>
                          <startDate>{StartDate}</startDate>
                          <endDate>{EndDate}</endDate>
                         <TimeList>{PlanTimes}</TimeList>
                         </HolidayPlan>
                         </ConfigXML>
                         </Params>";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(1 * 1024);
            pInputCfgXml.dwOutSize = 1 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                IsupLog.Info(inputXml);
                IsupLog.Info($"设置访问权限假日计划失败{iLastErr}。{ErrCodeMean}。");
                strOutPuts= $"设置访问权限假日计划失败{iLastErr}。{ErrCodeMean}。inputXml{inputXml}";
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }
            //string strOutPuts = PtrToStringAnsi(pInputCfgXml.pOutBuf, (int)pInputCfgXml.dwOutSize);
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }

        /// <summary>
        /// 设置访问权限假日组。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayGroupNo">假日分组编号</param>
        /// <param name="groupName">组名称</param>
        /// <param name="holidayNo">假日计划编号，最多 16 个 示例：1,2,3<!--必选项，xs:integer--></param>
        /// <returns></returns>
        public static bool SetCardHolidayGroup(int iUserID, string HolidayGroupNo, string groupName, string holidayNo,out string strOutPuts)
        {
            strOutPuts="";
            if (string.IsNullOrWhiteSpace(groupName))
            {
                groupName = "JustTest";
            }

            var inputXml = $@"<Params>
<ConfigCmd>SetCardHolidayGroup</ConfigCmd>
<ConfigParam1>{HolidayGroupNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<HolidayGroup>
<enabled>1</enabled>
<groupName>{groupName}</groupName>
<holidayNo>{holidayNo}</holidayNo>
</HolidayGroup>
</ConfigXML>
</Params>
";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new HCISUPCMS.NET_EHOME_XML_CFG();
            Int32 nInSize = Marshal.SizeOf(pInputCfgXml);
            pInputCfgXml.dwSendTimeOut = 5000;
            pInputCfgXml.dwRecvTimeOut = 5000;

            string strInputParam = inputXml;
            byte[] byInputParam = Encoding.UTF8.GetBytes(strInputParam);
            int byInputLen = byInputParam.Length;

            pInputCfgXml.pInBuf = Marshal.AllocHGlobal(byInputLen);
            Marshal.Copy(byInputParam, 0, pInputCfgXml.pInBuf, byInputLen);
            pInputCfgXml.dwInSize = (uint)byInputLen;

            pInputCfgXml.pOutBuf = Marshal.AllocHGlobal(1 * 1024);
            pInputCfgXml.dwOutSize = 1 * 1024;

            string strCmd = "SETDEVICECONFIG";
            uint dwstrCmdLen = (uint)strCmd.Length;
            pInputCfgXml.pCmdBuf = Marshal.StringToHGlobalAnsi(strCmd);
            pInputCfgXml.dwCmdLen = dwstrCmdLen;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputCfgXml));
            Marshal.StructureToPtr(pInputCfgXml, ptrCfg, false);

            if (!HCISUPCMS.NET_ECMS_XMLConfig(iUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                IsupLog.Info(inputXml);
                IsupLog.Info($"设置访问权限假日组{iLastErr}。{ErrCodeMean}。");
                strOutPuts = $"设置访问权限假日组{iLastErr}。{ErrCodeMean}。inputXml{inputXml}";
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }
            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }
    }
}
