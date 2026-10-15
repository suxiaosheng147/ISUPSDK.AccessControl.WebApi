using ISUPSDK.AccessControl.WebApi.Services.Logging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPPostCard
    {


        /// <summary>
        /// 下发卡片信息
        /// </summary>
        /// <param name="machine">机器信息</param>
        /// <param name="person">下发数据</param>
        /// <param name="Prefix"></param>
        /// <param name="tempIdList"></param>
        /// <returns></returns>
        public static bool Post(im_machine machine, bool deleteCard, Detail person, string Prefix, out List<returnErrorModel> tempIdList)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new HCISUPCMS.NET_EHOME_PTXML_PARAM();
            IntPtr ptrCfg = IntPtr.Zero;
            tempIdList = new List<returnErrorModel>();
            try
            {
                var CardInfo1 = new CardInfo1()
                {
                    CardInfo = new CardInfo()
                    {
                        cardNo = person.cardId,
                        cardType = "normalCard",
                        deleteCard = deleteCard,
                        employeeNo = Prefix + person.webId.ToString()

                    }

                };


                var inputXml = JsonHepler.ToJson(CardInfo1);
                String strTemp = "PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json";
                int OutBufferLen = 2 * 1024 * 1024;
                //ConfigMethod(ref ptrCfg, strTemp, inputXml.ToString(), OutBufferLen);
                m_szUrl = Encoding.UTF8.GetBytes(strTemp);
                struPTXML.pRequestUrl = Marshal.AllocHGlobal(m_szUrl.Length);
                Marshal.Copy(m_szUrl, 0, struPTXML.pRequestUrl, m_szUrl.Length);
                struPTXML.dwRequestUrlLen = (uint)m_szUrl.Length;

                byte[] m_szInputBuffer = Encoding.UTF8.GetBytes(inputXml.ToString());
                struPTXML.pInBuffer = Marshal.AllocHGlobal(m_szInputBuffer.Length);
                Marshal.Copy(m_szInputBuffer, 0, struPTXML.pInBuffer, m_szInputBuffer.Length);
                struPTXML.dwInSize = (uint)m_szInputBuffer.Length;

                struPTXML.pOutBuffer = Marshal.AllocHGlobal(OutBufferLen);
                for (int i = 0; i < OutBufferLen; i++)
                {
                    Marshal.WriteByte(struPTXML.pOutBuffer, i, 0);
                }
                struPTXML.dwOutSize = (uint)OutBufferLen;
                struPTXML.byRes = new byte[32];

                // 创建配置结构并将其传递给 ptrCfg
                ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(struPTXML));
                Marshal.StructureToPtr(struPTXML, ptrCfg, false);
                bool lb = false;
                try
                {
                    if (!HCISUPCMS.NET_ECMS_PutPTXMLConfig(machine.lLoginID, ptrCfg))
                    {
                        int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                        ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);



                        // 记录失败信息到 tempIdList
                        AddErrorToList(tempIdList, person.dataId, $"下发卡片信息失败{iLastErr}。{ErrCodeMean}。");
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            IsupLog.Info($"下发卡片信息失败{strOutPuts}。{person.name}");
                            AddErrorToList(tempIdList, person.dataId, $"下发卡片信息失败。{errorMsg}。");
                        }

                    }
                }
                catch (Exception ex)
                {

                }

                return lb;
            }
            catch (Exception ex)
            {

                return false;
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }

        private static void AddErrorToList(List<returnErrorModel> tempIdList, int dataId, string errorMessage)
        {
            if (!tempIdList.Exists(w => w.DataId == dataId))
            {
                tempIdList.Add(new returnErrorModel { Err = errorMessage, DataId = dataId });
            }
        }
        


        /// <summary>
        /// 下发卡片信息
        /// </summary>
        /// <param name="machine">机器信息</param>
        /// <param name="deleteCard">是否删除卡片</param>
        /// <param name="person">下发数据</param>
        /// <returns>包含操作结果的对象</returns>
        public static IssuePersonCardResult PostCard(im_machine machine, bool deleteCard, SyncPersonDataPage person)
        {
            var CardInfo1 = new CardInfo1()
            {
                CardInfo = new CardInfo()
                {
                    cardNo = person.CardId,
                    cardType = "normalCard",
                    deleteCard = deleteCard,
                    employeeNo = person.PersonType + person.PersonId.ToString()
                }
            };
            string requestJson = JsonHepler.ToJson(CardInfo1);
            string urlPath = $"/ISAPI/AccessControl/CardInfo/SetUp?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(machine.lLoginID, "PUT", urlPath, requestJson);

            return new IssuePersonCardResult
            {
                Success = success,
                RequestJson = requestJson,
                ResponseJson = response,
                ErrorMessage = errorMessage
            };
        }

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
    }
    public class CardInfo
    {
        public string employeeNo { get; set; }

        public string cardNo { get; set; }

        /// <summary>
        /// /*opt, bool, 是否删除该卡, desc:true-是（只有删除该卡时，才填写该字段；新增或修改卡时，不填写该字段）*/
        /// </summary>
        public bool? deleteCard { get; set; } = null;

        /// <summary>
        /// /*req, enum, 卡类型, subType:string, [normalCard#普通卡,patrolCard#巡更卡,hijackCard#胁迫卡,superCard#超级卡,dismissingCard#解除卡,emergencyCard#应急管理卡]*/
        /// </summary>
        public string cardType { get; set; }

        /// <summary>
        /// /*opt, bool, 设备是否进行工号（人员ID）存在校验, 
        /// desc:false-不校验，true-校验（如果不配置该字段，则设备默认进行工号（人员ID）存在校验）
        /// </summary>
        public bool checkEmployeeNo { get; set; } = false;

    }
    public class CardInfo1
    {
        public CardInfo CardInfo { get; set; }
    }
}
