using ISUPSDK.AccessControl.WebApi.Services.Logging;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using Newtonsoft.Json;
using System;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    internal class ISUPDoor
    {
        private static void CleanupResources(HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML, IntPtr ptrCfg)
        {
            // 清理分配的内存
            if (struPTXML.pRequestUrl != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pRequestUrl);
            if (struPTXML.pInBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pInBuffer);
            if (struPTXML.pOutBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pOutBuffer);
            if (struPTXML.pCondBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pCondBuffer);
            if (ptrCfg != IntPtr.Zero) Marshal.FreeHGlobal(ptrCfg);
        }

      
        /// <summary>
        /// 执行HTTP请求
        /// </summary>
        /// <param name="iUserID">设备句柄</param>
        /// <param name="httpMethod">请求方法</param>
        /// <param name="urlPath">请求地址</param>
        /// <param name="requestBody">请求体</param>
        /// <returns>元组（success，response，errorMessage）</returns>
        private static (bool success, string response, string errorMessage) ExecuteHttpRequest(int iUserID,
            string httpMethod,
            string urlPath, 
            string requestBody = "")
        {
            const int InitialBufferSize = 64 * 1024; // 初始 64KB
            int outBufferLen = InitialBufferSize;
            byte[]? urlBytes ;
            byte[]? inputBytes;
            IntPtr pRequestUrl = IntPtr.Zero, pInBuffer = IntPtr.Zero, pOutBuffer = IntPtr.Zero, ptrCfg = IntPtr.Zero;
            bool success = false;
            string response = string.Empty;
            string errorMessage = string.Empty;

            try
            {
                urlBytes = Encoding.UTF8.GetBytes(urlPath);
                inputBytes = Encoding.UTF8.GetBytes(requestBody);

                // 动态分配输出缓冲区（若 SDK 支持重新调用，此处简化）
                pOutBuffer = Marshal.AllocHGlobal(outBufferLen);
                pRequestUrl = Marshal.AllocHGlobal(urlBytes.Length);
                pInBuffer = Marshal.AllocHGlobal(inputBytes.Length);

                Marshal.Copy(urlBytes, 0, pRequestUrl, urlBytes.Length);
                Marshal.Copy(inputBytes, 0, pInBuffer, inputBytes.Length);

                var struPTXML = new HCISUPCMS.NET_EHOME_PTXML_PARAM
                {
                    pRequestUrl = pRequestUrl,
                    dwRequestUrlLen = (uint)urlBytes.Length,
                    pInBuffer = pInBuffer,
                    dwInSize = (uint)inputBytes.Length,
                    pOutBuffer = pOutBuffer,
                    dwOutSize = (uint)outBufferLen,
                    byRes = new byte[32]
                };

                ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(struPTXML));
                Marshal.StructureToPtr(struPTXML, ptrCfg, false);

                bool apiResult = false;
                if (string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                    apiResult = HCISUPCMS.NET_ECMS_GetPTXMLConfig(iUserID, ptrCfg);
                else if (string.Equals(httpMethod, "PUT", StringComparison.OrdinalIgnoreCase))
                    apiResult = HCISUPCMS.NET_ECMS_PutPTXMLConfig(iUserID, ptrCfg);
                else if (string.Equals(httpMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    apiResult = HCISUPCMS.NET_ECMS_PostPTXMLConfig(iUserID, ptrCfg);
                else if (string.Equals(httpMethod, "DELETE", StringComparison.OrdinalIgnoreCase))
                    apiResult = HCISUPCMS.NET_ECMS_DeletePTXMLConfig(iUserID, ptrCfg);
                else
                    return (false, "", $"不支持的HTTP方法: {httpMethod}");

                if (!apiResult)
                {
                    int lastErr = HCISUPCMS.NET_ECMS_GetLastError();
                    ErrCodeDictionary.ErrCode.TryGetValue(lastErr, out string? errMsg);
                    errorMessage = $"SDK调用失败，错误码：{lastErr}，说明：{errMsg ?? "未知"}";
                    return (false, "", errorMessage);
                }

                // 重新获取结构体中的实际输出长度（SDK 可能修改了 dwOutSize）
                var updatedParam = Marshal.PtrToStructure<HCISUPCMS.NET_EHOME_PTXML_PARAM>(ptrCfg);
                int actualLen = (int)updatedParam.dwOutSize;
                if (actualLen > outBufferLen)
                {
                    // 若实际数据大于缓冲区，可重新分配并重试（具体取决于 SDK 是否支持）
                    errorMessage = "响应数据超过缓冲区大小";
                    return (false, "", errorMessage);
                }

                // 使用 UTF-8 解码响应
                byte[] responseBytes = new byte[actualLen];
                Marshal.Copy(updatedParam.pOutBuffer, responseBytes, 0, actualLen);
                response = Encoding.UTF8.GetString(responseBytes).Trim();

                
                if (string.Equals(httpMethod, "GET", StringComparison.OrdinalIgnoreCase))
                {
                    success = true;
                }
                else
                {
                   
                    if (!ISUPResultMethod.ISUPResult(response, out int statusCode, out string statusError))
                    {
                        errorMessage = $"HTTP响应状态异常：{statusCode}，错误信息：{statusError}";
                        return (false, "", errorMessage);
                    }
                    success = true;
                }

                return (success, response, "");
            }
            catch (Exception ex)
            {
                return (false, "", $"执行HTTP请求时发生异常：{ex.Message}");
            }
            finally
            {
                // 释放所有非托管内存
                if (pRequestUrl != IntPtr.Zero) Marshal.FreeHGlobal(pRequestUrl);
                if (pInBuffer != IntPtr.Zero) Marshal.FreeHGlobal(pInBuffer);
                if (pOutBuffer != IntPtr.Zero) Marshal.FreeHGlobal(pOutBuffer);
                if (ptrCfg != IntPtr.Zero) Marshal.FreeHGlobal(ptrCfg);
            }
        }
        /// <summary>
        /// 获取设备能力集（AccessControl）
        /// </summary>
        /// <param name="iUserID"></param>
        /// <returns>XML</returns>
        public static string AccessControlCapabilities(int iUserID)
        {
            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "GET", "/ISAPI/AccessControl/capabilities?format=json");
            if (!string.IsNullOrWhiteSpace(errorMessage)&&!success)
                IsupLog.Info($"获取能力集失败：{errorMessage}");
            return success ? response : "";
        }

        #region 假日计划
        /// <summary>
        /// 设置访问权限假日计划。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayPlanData"></param>
        /// <param name="isDELETE">是否删除</param>
        /// <returns></returns>
        public static IssueCardDoorResult SetCardHolidayPlanResult(int iUserID, HolidayPeriodData holidayPlanData,bool isDELETE=false)
        {

            string requestJson = GenerateHolidayPlanJson(holidayPlanData, isDELETE);
            string urlPath = $"/ISAPI/AccessControl/UserRightHolidayPlanCfg/{holidayPlanData.HolidayPlanNo}?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "PUT", urlPath, requestJson);

            return new IssueCardDoorResult
            {
                Success = success,
                RequestXML = requestJson,
                ResponseXML = response,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayPlanNo"></param>
        /// <returns></returns>
        public static string GetHolidayPlan(int iUserID,string holidayPlanNo)
        {
           
            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "GET", $"/ISAPI/AccessControl/UserRightHolidayPlanCfg/{holidayPlanNo}?format=json");
            if (!string.IsNullOrWhiteSpace(errorMessage) && !success)
                IsupLog.Info($"获取HolidayPlan失败：{errorMessage}");
            else
            {
                IsupLog.Info($"获取holidayPlanNo：{holidayPlanNo},  HolidayPlan成功：{response}");
            }
            return success ? response : "";
        }



        
        /// <summary>
        /// 生成假日计划配置的 JSON 字符串
        /// </summary>
        /// <param name="data">假日计划数据</param>
        /// <returns>符合目标格式的 JSON 字符串</returns>
        public static string GenerateHolidayPlanJson(HolidayPeriodData data,bool isDELETE=false)
        {
            // 辅助函数：将 "HH:mm" 格式转换为 "HH:mm:ss"
            string FormatTime(string time)
            {
                if (string.IsNullOrWhiteSpace(time))
                    return "00:00:00";
                return time.Contains(":") && time.Split(':').Length == 2 ? time + ":00" : time;
            }

            // 辅助函数：获取某个时间段的时间对
            (string from, string to) GetTimePair(int index)
            {
                return index switch
                {
                    1 => (data.FromTime1, data.ToTime1),
                    2 => (data.FromTime2, data.ToTime2),
                    3 => (data.FromTime3, data.ToTime3),
                    4 => (data.FromTime4, data.ToTime4),
                    5 => (data.FromTime5, data.ToTime5),
                    6 => (data.FromTime6, data.ToTime6),
                    7 => (data.FromTime7, data.ToTime7),
                    8 => (data.FromTime8, data.ToTime8),
                    _ => ("", "")
                };
            }

            // 提取日期（取前10个字符，若无效则使用默认值）
            string ExtractDate(string dateTimeStr)
            {
                if (DateTime.TryParse(dateTimeStr, out DateTime dt))
                    return dt.ToString("yyyy-MM-dd");
                return "2000-01-01";
            }

            var holidayPlanCfg = new List<object>();
            for (int i = 1; i <= 8; i++)
            {
                var (fromTime, toTime) = GetTimePair(i);
                bool isEnabled = !string.IsNullOrWhiteSpace(fromTime) && !string.IsNullOrWhiteSpace(toTime);

                holidayPlanCfg.Add(new
                {
                    id = i,
                    enable = isEnabled,
                    TimeSegment = new
                    {
                        beginTime = FormatTime(fromTime),
                        endTime = FormatTime(toTime)
                    },
                    authenticationTimesEnabled = false,
                    authenticationTimes = 0
                });

            }

            var root = new
            {
                UserRightHolidayPlanCfg = new
                {
                    enable = isDELETE==true?false:true,   // 整体启用，可根据业务逻辑调整
                    deleteHolidayPlan=isDELETE,
                    holidayPlanName=data.HolidayPlanNo,
                    beginDate = ExtractDate(data.StartDate),
                    endDate = ExtractDate(data.EndDate),
                    cycleByYear=false,
                    HolidayPlanCfg = holidayPlanCfg
                }
            };

            return JsonConvert.SerializeObject(root, Formatting.Indented);
        }

        #endregion

        #region 假日组


        /// <summary>
        /// 
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="holidayGroupNo"></param>
        /// <returns></returns>
        public static string GetHolidayGroupNo(int iUserID, string holidayGroupNo)
        {
            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "GET", $"/ISAPI/AccessControl/UserRightHolidayGroupCfg/{holidayGroupNo}?format=json");
            if (!string.IsNullOrWhiteSpace(errorMessage) && !success)
                IsupLog.Info($"获取HolidayGroupNo失败：{errorMessage}");
            return success ? response : "";

        }
        /// <summary>
        /// 设置访问权限假日组。
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="HolidayGroupNo">假日分组编号</param>
        /// <param name="groupName">组名称</param>
        /// <param name="holidayNo">假日计划编号，最多 16 个 示例：1,2,3<!--必选项，xs:integer--></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetCardHolidayGroupResult(int iUserID, string HolidayGroupNo, string groupName, string holidayNo,bool enable=true)
        {
            var root = new
            {
                UserRightHolidayGroupCfg = new
                {
                    enable = enable,
                    groupName = HolidayGroupNo,
                    holidayPlanNo = holidayNo
                }
            };
            string requestJson = JsonConvert.SerializeObject(root, Formatting.Indented);
            string urlPath = $"/ISAPI/AccessControl/UserRightHolidayGroupCfg/{HolidayGroupNo}?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "PUT", urlPath, requestJson);

            return new IssueCardDoorResult
            {
                Success = success,
                RequestXML = requestJson,
                ResponseXML = response,
                ErrorMessage = errorMessage
            };


        }
        #endregion


        #region 周计划
        /// <summary>
        /// 获取周计划
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="weekPlanNo"></param>
        /// <returns></returns>
        public static string GetWeekPlan(int iUserID,string weekPlanNo)
        {
            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "GET", $"/ISAPI/AccessControl/UserRightWeekPlanCfg/<{weekPlanNo}>?format=json");
            if (!string.IsNullOrWhiteSpace(errorMessage) && !success)
                IsupLog.Info($"获取WeekPlan失败：{errorMessage}");
            return success ? response : "";

        }

        /// <summary>
        /// 设置用户权限周计划
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="doorData"></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetUserRightWeekPlanCfgResult(int iUserID, DoorData doorData)
        {
            string requestJson = string.Empty;
            if (doorData.TimePeriods != null && doorData.TimePeriods.Count > 0)
            {
                requestJson = WeekPlanSerializer.ToJsonWithDefaults(doorData.TimePeriods, true);
            }
            else
            {
                requestJson = WeekPlanSerializer.ConvertDoorDataToWeekPlanJson(doorData);
                Common.WriteFileHours("SetUserRightWeekPlanCfgResult.txt", true, requestJson);
            }
            string urlPath = $"/ISAPI/AccessControl/UserRightWeekPlanCfg/{doorData.groupID}?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "PUT", urlPath, requestJson);

            return new IssueCardDoorResult
            {
                Success = success,
                RequestXML = requestJson,
                ResponseXML = response,
                ErrorMessage = errorMessage
            };
        }
        #endregion

        #region 计划模板

        /// <summary>
        /// 设置计划模板
        /// </summary>
        /// <param name="iUserID"></param>
        /// <param name="planNo"></param>
        /// <param name="weekPlanNo"></param>
        /// <param name="HolidayGroupNo"></param>
        /// <returns></returns>
        public static IssueCardDoorResult SetUserRightPlanTemplateResult(int iUserID, int planNo, int weekPlanNo,string name, string HolidayGroupNo = "0")
        {
            string requestJson =string.Empty;
            var UserRightPlanTemplate = new
            {
                enable=true,
                templateName = ""+ planNo.ToString(),
                weekPlanNo= weekPlanNo,
                holidayGroupNo= HolidayGroupNo=="0" ?"": HolidayGroupNo,
                operateType= "byTerminal",

            };

            var re = new
            {
                UserRightPlanTemplate= UserRightPlanTemplate
            };
            requestJson=JsonConvert.SerializeObject(re, Formatting.Indented);
            string urlPath = $"/ISAPI/AccessControl/UserRightPlanTemplate/{planNo}?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "PUT", urlPath, requestJson);

            return new IssueCardDoorResult
            {
                Success = success,
                RequestXML = requestJson,
                ResponseXML = response,
                ErrorMessage = errorMessage
            };
        }


        #endregion
    }

}
