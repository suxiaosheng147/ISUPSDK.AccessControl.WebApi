using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPAcsEvent
    {


        /// <summary>
        /// 获取设备事件
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="startTime">2026-01-01T00:00:00+08:00</param>
        /// <param name="endTime">2026-05-23T23:00:00+08:00</param>
        /// <returns>dwMajor= 5 (Event),dwMinor=1 (全部)</returns>
        public static List<InfoListItem> GetAcsEvent(im_machine machine, GetAcsEventModel syncAcsEvent)
        {
            // ToString("yyyy-MM-ddTHH:mm:sszzz")
            string startTime= syncAcsEvent.StartTime.ToString("yyyy-MM-ddTHH:mm:sszzz");
            string endTime= syncAcsEvent.EndTime.ToString("yyyy-MM-ddTHH:mm:sszzz");
            string szUrl = "/ISAPI/AccessControl/AcsEvent?format=json";
            //事件Event
            uint dwMajor =(uint) syncAcsEvent.Major.Value;

            //事件Minor
            uint dwMinor = (uint)syncAcsEvent.Minor.Value;

            List<InfoListItem> allEvents = [];
            int pos = 0;
            int totalFetched = 0;
            while (true)
            {
                int i = 1;
                var cond = new
                {
                    searchID = "" + i,
                    searchResultPosition = pos,
                    maxResults = 30,
                    major = dwMajor,
                    picEnable = true,
                    minor = dwMinor,
                    startTime ,
                    endTime

                };
                var requestObj = new { AcsEventCond = cond };
                string szRequest = JsonConvert.SerializeObject(requestObj);

                var (success, response, errorMsg) = ExecuteHttpRequest(machine.lLoginID, "POST", szUrl, szRequest);
                if (!success || !string.IsNullOrEmpty(errorMsg))
                {
                    break;
                }
                AcsEventObject? rb = JsonConvert.DeserializeObject<AcsEventObject>(response);
                if (rb?.AcsEvent == null)
                {
                    
                    break;
                }
                // 判断是否还有匹配记录
                if (rb.AcsEvent.responseStatusStrg == "NO MATCH")
                    break;

                // 收集当前页数据
                if (rb.AcsEvent.InfoList != null && rb.AcsEvent.InfoList.Count > 0)
                {
                    allEvents.AddRange(rb.AcsEvent.InfoList);
                    totalFetched += rb.AcsEvent.InfoList.Count;
                }
                int posTmp = pos;
                posTmp += rb.AcsEvent.InfoList?.Count ?? 0;
                pos = posTmp;

                // 判断是否已获取全部结果
                if (rb.AcsEvent.responseStatusStrg == "OK" || (rb.AcsEvent.InfoList?.Count ?? 0) < 30)
                    break;

                // 避免死循环（例如超过 6 万条记录时退出）
                if (posTmp > 60000) break;

                i++;

            }
            return allEvents;



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
            byte[]? urlBytes;
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

    }
}
