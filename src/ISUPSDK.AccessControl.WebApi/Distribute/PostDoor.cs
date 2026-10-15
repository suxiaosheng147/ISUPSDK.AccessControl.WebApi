using ISUPSDK.AccessControl.WebApi.Services.Logging;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi
{
    public class PostDoor
    {

        private static string ReadOutBuffer(IntPtr pOutBuf, uint dwOutSize)
        {
            if (pOutBuf == IntPtr.Zero || dwOutSize == 0)
                return string.Empty;

            try
            {
                byte[] buffer = new byte[dwOutSize];
                Marshal.Copy(pOutBuf, buffer, 0, (int)dwOutSize);
                return Encoding.UTF8.GetString(buffer).TrimEnd('\0');
            }
            catch
            {
                return string.Empty;
            }
        }


        /// <summary>
        /// 设置计划模板
        /// </summary>
        /// <param name="lUserID"></param>
        /// <param name="planNo"></param>
        /// <param name="weekPlanNo">0-不对应任何计划，即24小时通行，或等于planNo</param>
        /// <param name="HolidayGroupNo">假日组编号</param>
        /// <returns></returns>
        public static bool SetTemplate(int lUserID, int planNo, int weekPlanNo, string HolidayGroupNo = "0")
        {
            var inputXml = $@"<Params>
<ConfigCmd>SetCardTemplate</ConfigCmd>
<ConfigParam1>{planNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<Template>
<enabled>1</enabled>
<name>{planNo}</name>
<weekPlanNo>{weekPlanNo}</weekPlanNo>
<HGNo>{(HolidayGroupNo == "0" ? "" : HolidayGroupNo.ToString())}</HGNo>
</Template>
</ConfigXML>
</Params>";
            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new();
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

            if (!HCISUPCMS.NET_ECMS_XMLConfig(lUserID, ptrCfg, nInSize))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string ErrCodeMean);
                IsupLog.Info($"设置计划模板失败{iLastErr}。{ErrCodeMean}。" + inputXml);
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }

            //byte[] byOutBuf = new byte[pInputCfgXml.dwOutSize];
            //Marshal.Copy(pInputCfgXml.pOutBuf, byOutBuf, 0, (int)pInputCfgXml.dwOutSize);
            //var textBoxOutput = System.Text.Encoding.UTF8.GetString(byOutBuf);

            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }


        /// <summary>
        /// 设置计划模板
        /// </summary>
        /// <param name="lUserID"></param>
        /// <param name="planNo"></param>
        /// <param name="weekPlanNo">0-不对应任何计划，即24小时通行，或等于planNo</param>
        /// <param name="HolidayGroupNo">假日组编号</param>
        /// <returns></returns>
        public static IssueCardDoorResult SetTemplateResult(int lUserID, int planNo, int weekPlanNo, string HolidayGroupNo = "0")
        {
            var result = new IssueCardDoorResult();

            // 构建请求 XML
            var inputXml = $@"<Params>
<ConfigCmd>SetCardTemplate</ConfigCmd>
<ConfigParam1>{planNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<Template>
<enabled>1</enabled>
<name>{planNo}</name>
<weekPlanNo>{weekPlanNo}</weekPlanNo>
<HGNo>{(HolidayGroupNo == "0" ? "" : HolidayGroupNo)}</HGNo>
</Template>
</ConfigXML>
</Params>";
            result.RequestXML = inputXml;

            HCISUPCMS.NET_EHOME_XML_CFG pInputCfgXml = new();
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
                if (!HCISUPCMS.NET_ECMS_XMLConfig(lUserID, ptrCfg, nInSize))
                {
                    int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                    result.Success = false;
                    result.ErrorMessage = ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? errMsg) ? errMsg : $"未知错误码 {iLastErr}";
                }
                else
                {
                    result.Success = true;
                }

                // 读取响应 XML
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
        /// 启用周计划
        /// </summary>
        public static bool Start(int lUserID, int planNo)
        {
            var inputXml = $@"<Params>
<ConfigCmd>SetCardWeekPlan</ConfigCmd>
<ConfigParam1>{planNo}</ConfigParam1>
<ConfigParam2>0</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<WeekPlanCfg>
<enabled>1</enabled>
</WeekPlanCfg>
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

            if (!HCISUPCMS.NET_ECMS_XMLConfig(lUserID, ptrCfg, nInSize))
            {
                //uint iLastErr = HCISUPSDK.NET_ECMS_GetLastError();
                //ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string ErrCodeMean);
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }

            //byte[] byOutBuf = new byte[pInputCfgXml.dwOutSize];
            //Marshal.Copy(pInputCfgXml.pOutBuf, byOutBuf, 0, (int)pInputCfgXml.dwOutSize);
            //var textBoxOutput = System.Text.Encoding.UTF8.GetString(byOutBuf);

            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }


        /// <summary>
        /// 设置计划时间
        /// </summary>
        /// <param name="lUserID"></param>
        /// <param name="planNo">计划编号</param>
        /// <param name="week">0-周一，1-周二，2-周三，3-周四，4-周五，5-周六，6-周日</param>
        /// <returns></returns>
        public static bool SetTime(int lUserID, DoorData doorData)
        {
            if (doorData.TimePeriods != null && doorData.TimePeriods.Count > 0)
            {
                return SetTimePeriod(lUserID, doorData);
            }
            var WeekPlanTime = $"{BuildWeekPlanTime(doorData.fromTime1, doorData.toTime1)}{BuildWeekPlanTime(doorData.fromTime2, doorData.toTime2)}{BuildWeekPlanTime(doorData.fromTime3, doorData.toTime3)}{BuildWeekPlanTime(doorData.fromTime4, doorData.toTime4)}";
            for (var i = 0; i <= 6; i++)
            {
                if (!SetTime(lUserID, doorData.groupID, i, WeekPlanTime))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 设置周计划时间
        /// </summary>
        /// <param name="lUserID"></param>
        /// <param name="doorData">门禁数据</param>
        /// <returns></returns>
        public static bool SetTimePeriod(int lUserID, DoorData doorData)
        {

            if (doorData.TimePeriods != null && doorData.TimePeriods.Count > 0)
            {
                int i = 0;
                foreach (var item in doorData.TimePeriods)
                {
                    var WeekPlanTime = 
                        $"{BuildWeekPlanTime(item.FromTime1, item.ToTime1)}" +
                        $"{BuildWeekPlanTime(item.FromTime2, item.ToTime2)}" +
                        $"{BuildWeekPlanTime(item.FromTime3, item.ToTime3)}" +
                        $"{BuildWeekPlanTime(item.FromTime4, item.ToTime4)}" +
                        $"{BuildWeekPlanTime(item.FromTime5, item.ToTime5)}" +
                        $"{BuildWeekPlanTime(item.FromTime6, item.ToTime6)}" +
                        $"{BuildWeekPlanTime(item.FromTime7, item.ToTime7)}" +
                        $"{BuildWeekPlanTime(item.FromTime8, item.ToTime8)}";
                    if (!SetTime(lUserID, doorData.groupID, item.Week, WeekPlanTime))
                        return false;
                    i++;
                }
            }
            return true;

        }



        private static string BuildWeekPlanTime(string fromTime, string toTime)
        {
            if (!string.IsNullOrWhiteSpace(fromTime) && !string.IsNullOrWhiteSpace(toTime))
                return $@"<WeekPlanTime>
<startTime>{fromTime}</startTime>
<endTime>{toTime}</endTime>
</WeekPlanTime>";
            else
                return string.Empty;
        }

        /// <summary>
        /// 设置计划时间
        /// </summary>
        /// <param name="lUserID"></param>
        /// <param name="planNo">计划编号</param>
        /// <param name="week">0-周一，1-周二，2-周三，3-周四，4-周五，5-周六，6-周日</param>
        /// <returns></returns>
        private static bool SetTime(int lUserID, int planNo, int week, string WeekPlanTime)
        {
            var inputXml = $@"<Params>
<ConfigCmd>SetCardWeekPlanTime</ConfigCmd>
<ConfigParam1>{planNo}</ConfigParam1>
<ConfigParam2>{week}</ConfigParam2>
<ConfigParam3>0</ConfigParam3>
<ConfigParam4>0</ConfigParam4>
<ConfigXML>
<WeekPlanTimeList>
{WeekPlanTime}
</WeekPlanTimeList>
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

            if (!HCISUPCMS.NET_ECMS_XMLConfig(lUserID, ptrCfg, nInSize))
            {
                //uint iLastErr = HCISUPSDK.NET_ECMS_GetLastError();
                //ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string ErrCodeMean);
                Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
                Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
                return false;
            }

            //byte[] byOutBuf = new byte[pInputCfgXml.dwOutSize];
            //Marshal.Copy(pInputCfgXml.pOutBuf, byOutBuf, 0, (int)pInputCfgXml.dwOutSize);
            //var textBoxOutput = System.Text.Encoding.UTF8.GetString(byOutBuf);

            Marshal.FreeHGlobal(pInputCfgXml.pCmdBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pInBuf);
            Marshal.FreeHGlobal(pInputCfgXml.pOutBuf);
            return true;
        }
    }
}
