using ISUPSDK.AccessControl.WebApi.Services.Logging;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPPostPerson
    {

        /// <summary>
        /// 删除人员信息
        /// </summary>
        public static bool Del(im_machine machine, Detail person, string Prefix, ref List<returnErrorModel> tempIdList)
        {

            DelPerson delPerson = new DelPerson
            {
                UserInfoDetail = new UserInfoDetail()
                {
                    EmployeeNoList = new List<EmployeeNoList>()
                       {
                            new EmployeeNoList()
                            {
                                employeeNo= Prefix+ person.webId.ToString()
                            }
                       }
                }
            };

            var inputXml = JsonHepler.ToJson(delPerson);
            HCISUPCMS.NET_EHOME_PTXML_PARAM pInputPTXml = new HCISUPCMS.NET_EHOME_PTXML_PARAM();
            Int32 nInSize = Marshal.SizeOf(pInputPTXml);

            string strRequestUrl = "PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json";
            uint dwRequestUrlLen = (uint)strRequestUrl.Length;
            pInputPTXml.pRequestUrl = Marshal.AllocHGlobal((int)dwRequestUrlLen);
            pInputPTXml.pRequestUrl = Marshal.StringToHGlobalAnsi(strRequestUrl);
            pInputPTXml.dwRequestUrlLen = dwRequestUrlLen;

            string strCondXParam = "";//条件XML
            uint dwCondXParamLen = (uint)strCondXParam.Length;
            pInputPTXml.pCondBuffer = Marshal.AllocHGlobal((int)dwCondXParamLen);
            pInputPTXml.pCondBuffer = Marshal.StringToHGlobalAnsi(strCondXParam);
            pInputPTXml.dwCondSize = dwCondXParamLen;

            string strInputXParam = inputXml;
            uint dwInputXParamLen = (uint)strInputXParam.Length;
            pInputPTXml.pInBuffer = Marshal.AllocHGlobal((int)dwInputXParamLen);
            pInputPTXml.pInBuffer = Marshal.StringToHGlobalAnsi(strInputXParam);
            pInputPTXml.dwInSize = dwInputXParamLen;


            pInputPTXml.pOutBuffer = Marshal.AllocHGlobal(1 * 1024);
            pInputPTXml.dwOutSize = 1 * 1024;
            IntPtr ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(pInputPTXml));
            Marshal.StructureToPtr(pInputPTXml, ptrCfg, false);

            bool lb = false;
            if (!HCISUPCMS.NET_ECMS_ISAPIPassThrough(machine.lLoginID, ptrCfg))
            {
                int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                if (!tempIdList.Exists(w => w.DataId == person.dataId))
                {
                    tempIdList.Add(new returnErrorModel { Err = $"删除人员失败{iLastErr}。{ErrCodeMean}。"/*'{System.Web.HttpUtility.UrlEncode(facePicUrl)}'"*/, ErrCode = iLastErr, DataId = person.dataId });
                }
            }
            else
            {
                string? strOutPuts = Marshal.PtrToStringAnsi(pInputPTXml.pOutBuffer);
                var Output = strOutPuts;

                lb = ISUPResultMethod.Method(Output, out int statusCode, out string errorMsg);
                if (!lb)
                {
                    if (!tempIdList.Exists(w => w.DataId == person.dataId))
                    {

                        tempIdList.Add(new returnErrorModel { Err = $"删除人员失败。{errorMsg}。", ErrCode = statusCode, DataId = person.dataId });

                    }
                }

            }
            Marshal.FreeHGlobal(pInputPTXml.pRequestUrl);
            Marshal.FreeHGlobal(pInputPTXml.pInBuffer);
            Marshal.FreeHGlobal(pInputPTXml.pOutBuffer);
            Marshal.FreeHGlobal(pInputPTXml.pCondBuffer);
            return lb;
        }


        /// <summary>
        /// 删除人员信息
        /// </summary>
        public static bool Del1(im_machine machine, Detail person, string Prefix, ref List<returnErrorModel> tempIdList)
        {

            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            tempIdList = [];
            try
            {
                PostPerson postPerson = new()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = Prefix + person.webId,
                        Name = person.name,
                        DeleteUser=true,
                        Valid = new Valid()
                        {
                            Enable = false,
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            TimeType = "local"
                        },
                        DoorRight = "1",
                        RightPlan =
                        [
                              new RightPlan()
                              {
                                   DoorNo=1,
                                    PlanTemplateNo= person.door.ToString()
                              }
                        ]
                    }
                };

                var inputXml = JsonHepler.ToJson(postPerson);
               
                String strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
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
                        AddErrorToList(tempIdList, person.dataId, $"删除人员信息失败{iLastErr}。{ErrCodeMean}。");
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            
                            AddErrorToList(tempIdList, person.dataId, $"删除人员信息失败。{errorMsg}。");
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



        /// <summary>
        /// 下发人员信息
        /// </summary>
        public static bool Post(im_machine machine, Detail person, string Prefix, out List<returnErrorModel> tempIdList)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            tempIdList = [];
            try
            {
                PostPerson postPerson = new()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = Prefix + person.webId,
                        Name = person.name,
                        Valid = new Valid()
                        {
                            Enable = false,
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            TimeType = "local"
                        },
                        DoorRight = "1",
                        RightPlan =
                        [
                              new RightPlan()
                              {
                                   DoorNo=1,
                                    PlanTemplateNo= person.door.ToString()
                              }
                        ]
                    }
                };

                var inputXml = JsonHepler.ToJson(postPerson);
                //       var inputXml = "{\"UserInfo\":{\"employeeNo\":\"" + Prefix + person.webId +
                //"\",\"name\":\"" + person.name +
                //"\",\"userType\":\"normal\",\"Valid\":{\"enable\":true,\"beginTime\":\"2017-08-01T17:30:08\",\"endTime\":\"2030-08-01T17:30:08\"},\"doorRight\": \"1\",\"RightPlan\":[{\"doorNo\":1,\"planTemplateNo\":\"" + person.door.ToString() +
                //"\"}]}}";
                String strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
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

                        
                        AddErrorToList(tempIdList, person.dataId, $"下发人员信息失败{iLastErr}。{ErrCodeMean}。");
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            
                            AddErrorToList(tempIdList, person.dataId, $"下发人员信息失败。{errorMsg}。");
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


        private static void CleanupResources(HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML, IntPtr ptrCfg)
        {
            // 清理分配的内存
            if (struPTXML.pRequestUrl != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pRequestUrl);
            if (struPTXML.pInBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pInBuffer);
            if (struPTXML.pOutBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pOutBuffer);
            if (struPTXML.pCondBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pCondBuffer);
            if (ptrCfg != IntPtr.Zero) Marshal.FreeHGlobal(ptrCfg);
        }


        public static void GetUserInfoSearch(im_machine machine, List<string> employeeNos,int RecordNumber, out List<UserInfo> UserInfo)
        {
            UserInfo=[];
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            try
            {
                List<EmployeeNoList> EmployeeNoList=new List<EmployeeNoList>();
                string searchID= Guid.NewGuid().ToString();
                foreach (var item in employeeNos)
                {
                    EmployeeNoList.Add(new Distribute.EmployeeNoList() { employeeNo = item });
                    //searchID += item ;
                }
                UserInfoSearchCond userInfoSearchCond = new()
                {
                    searchID = machine.MachineSN,
                    EmployeeNoList = EmployeeNoList,
                    maxResults = RecordNumber,
                    searchResultPosition = 0
                };
                var getData = new { userInfoSearchCond };
                var inputXml = JsonHepler.ToJson(getData);
                String strTemp = "POST /ISAPI/AccessControl/UserInfo/Search?format=json";
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
                
                try
                {
                    if (!HCISUPCMS.NET_ECMS_PostPTXMLConfig(machine.lLoginID, ptrCfg))
                    {
                        int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                        ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);

                       
                    }
                    else
                    {
                        string strOutPuts = PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);

                        if (!string.IsNullOrWhiteSpace(strOutPuts))
                        {
                            try
                            {
                                
                                var da = JsonHepler.FromJson<UserInfoSearch1>(strOutPuts);
                                if (da != null&& da.UserInfoSearch.responseStatusStrg=="OK")
                                {
                                    UserInfo = da.UserInfoSearch.UserInfo;
                                }
                            }
                            catch (Exception)
                            {

                                throw;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    IsupLog.Info($"异常: {ex.Message}");
                }
            }
            catch (Exception ex)
            {

                IsupLog.Info($"异常: {ex.Message}");
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
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
        /// 设置
        /// </summary>
        /// <param name="machine"></param>
        /// <param name="person"></param>
        /// <returns></returns>
        public static bool SetPost(im_machine machine,Person person)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;

            try
            {
               
                PostPerson postPerson = new ()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = person.PersonType+ person.PersonId,
                        Valid = new Valid()
                        {
                            Enable = person.ValidEnable,
                            BeginTime = person.BeginTime,
                            EndTime = person.EndTime,
                            TimeType = "local"
                        }
                    }

                };

                if (!string.IsNullOrWhiteSpace(person.PlanTemplateNo))
                {
                    postPerson.UserInfo.RightPlan =
                    [
                          new RightPlan()
                          {
                                DoorNo=1,
                                PlanTemplateNo=person.PlanTemplateNo,
                          }
                    ];
                }


                var inputXml = JsonHepler.ToJson(postPerson);
                IsupLog.Info(inputXml);
                String strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
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
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            IsupLog.Info(strOutPuts);
                        }
                        
                    }
                }
                catch (Exception ex)
                {
                    IsupLog.Info($"异常: {ex.Message}");
                }

                return lb;
            }
            catch (Exception ex)
            {
                // 记录异常
                IsupLog.Info($"异常: {ex.Message}");
                return false;
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }



        /// <summary>
        /// 下发人员信息
        /// </summary>
        public static (bool,string) Post(im_machine machine, SyncPersonDataPage person)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            string Error = string.Empty;
            try
            {
                PostPerson postPerson = new()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = person.PersonType + person.PersonId,
                        Name = person.PersonName,
                        Valid = new Valid()
                        {
                            Enable = false,
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            TimeType = "local"
                        },
                        DoorRight = "1",
                        RightPlan =
                        [
                              new RightPlan()
                              {
                                   DoorNo=1,
                                    PlanTemplateNo= person.DoorCode.ToString()
                              }
                        ]
                    }
                };
                
                var inputXml = JsonHepler.ToJson(postPerson);
                IsupLog.Info(inputXml);
                //       var inputXml = "{\"UserInfo\":{\"employeeNo\":\"" + Prefix + person.webId +
                //"\",\"name\":\"" + person.name +
                //"\",\"userType\":\"normal\",\"Valid\":{\"enable\":true,\"beginTime\":\"2017-08-01T17:30:08\",\"endTime\":\"2030-08-01T17:30:08\"},\"doorRight\": \"1\",\"RightPlan\":[{\"doorNo\":1,\"planTemplateNo\":\"" + person.door.ToString() +
                //"\"}]}}";
                String strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
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

                        Error = $"下发人员信息失败{iLastErr}。{ErrCodeMean}。";
                        
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            Error = $"下发人员信息失败。{errorMsg}。";
                            
                        }

                    }
                }
                catch (Exception ex)
                {

                }

                return (lb, Error);
            }
            catch (Exception ex)
            {

                return (false, "");
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }

        /// <summary>
        /// 下发人员信息
        /// </summary>
        public static IssuePersonInfoResult PostPerson(im_machine machine, SyncPersonDataPage person)
        {


            //if (person.DoorCode.EndsWith(","))
            //{
            //    person.DoorCode = person.DoorCode.TrimEnd(','); // 去掉末尾所有逗号
            //                                                    // 或者只删一个：str = str.Substring(0, str.Length - 1);


            //    IsupLog.Info(person.DoorCode);
            //}
            // 构建请求对象
            PostPerson postPerson = new()
            {
                UserInfo = new UserInfo()
                {
                    UserType = "normal",
                    EmployeeNo = person.PersonType + person.PersonId,
                    Name = person.PersonName,
                    Valid = new Valid()
                    {
                        Enable = false,
                        BeginTime = "2017-08-01T17:30:08",
                        EndTime = "2030-08-01T17:30:08",
                        TimeType = "local"
                    },
                    DoorRight = "1",
                    RightPlan =
                    [
                        new RightPlan()
                    {
                        DoorNo = 1,
                        PlanTemplateNo = person.DoorCode.ToString()
                    }
                    ]
                }
            };

            
            string requestJson= JsonHepler.ToJson(postPerson);

            string urlPath = $"/ISAPI/AccessControl/UserInfo/SetUp?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(machine.lLoginID, "PUT", urlPath, requestJson);

            return new IssuePersonInfoResult
            {
                Success = success,
                RequestJson = requestJson,
                ResponseJson = response,
                ErrorMessage = errorMessage
            };
        }

        /// <summary>
        /// 删除人员信息
        /// </summary>
        public static (bool,string) Del1(im_machine machine, SyncPersonDataPage person)
        {

            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            string Error = string.Empty;
            try
            {
                PostPerson postPerson = new()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = person.PersonType + person.PersonId,
                        Name = person.PersonName,
                        DeleteUser = true,
                        Valid = new Valid()
                        {
                            Enable = false,
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            TimeType = "local"
                        },
                        DoorRight = "1"
                        
                    }
                };

                var inputXml = JsonHepler.ToJson(postPerson);

                String strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
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


                        Error=$"删除人员信息失败{iLastErr}。{ErrCodeMean}。";
                       
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        IsupLog.Info(strOutPuts);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            Error = $"删除人员信息失败。{errorMsg}。";
                            
                        }

                    }
                }
                catch (Exception ex)
                {

                }

                return (lb, Error);
            }
            catch (Exception ex)
            {

                return (false,"");
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }


        /// <summary>
        /// 删除人员信息
        /// </summary>
        public static IssuePersonInfoResult DelPerson(im_machine machine, SyncPersonDataPage person)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            string error = string.Empty;
            string requestJson = string.Empty;
            string responseJson = string.Empty;
            bool success = false;

            try
            {
                // 构建删除人员请求对象
                PostPerson postPerson = new()
                {
                    UserInfo = new UserInfo()
                    {
                        UserType = "normal",
                        EmployeeNo = person.PersonType + person.PersonId,
                        Name = person.PersonName,
                        DeleteUser = true, // 删除标志
                        Valid = new Valid()
                        {
                            Enable = false,
                            BeginTime = "2017-08-01T17:30:08",
                            EndTime = "2030-08-01T17:30:08",
                            TimeType = "local"
                        },
                        DoorRight = "1",
                        RightPlan =
                        [
                            new RightPlan()
                    {
                        DoorNo = 1,
                        PlanTemplateNo = person.DoorCode.ToString()
                    }
                        ]
                    }
                };

                requestJson = JsonHepler.ToJson(postPerson);
                IsupLog.Info(requestJson);

                string strTemp = "PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json";
                int OutBufferLen = 2 * 1024 * 1024;

                m_szUrl = Encoding.UTF8.GetBytes(strTemp);
                struPTXML.pRequestUrl = Marshal.AllocHGlobal(m_szUrl.Length);
                Marshal.Copy(m_szUrl, 0, struPTXML.pRequestUrl, m_szUrl.Length);
                struPTXML.dwRequestUrlLen = (uint)m_szUrl.Length;

                byte[] m_szInputBuffer = Encoding.UTF8.GetBytes(requestJson);
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

                ptrCfg = Marshal.AllocHGlobal(Marshal.SizeOf(struPTXML));
                Marshal.StructureToPtr(struPTXML, ptrCfg, false);

                try
                {
                    if (!HCISUPCMS.NET_ECMS_PutPTXMLConfig(machine.lLoginID, ptrCfg))
                    {
                        int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                        ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                        error = $"删除人员信息失败{iLastErr}。{ErrCodeMean}。";
                        success = false;
                    }
                    else
                    {
                        responseJson = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        success = ISUPResultMethod.Method(responseJson, out int statusCode, out string errorMsg);
                        if (!success)
                        {
                            error = $"删除人员信息失败。{errorMsg}。";
                        }
                    }
                }
                catch (Exception ex)
                {
                    error = $"执行配置时发生异常：{ex.Message}";
                    success = false;
                }
            }
            catch (Exception ex)
            {
                error = $"构建请求时发生异常：{ex.Message}";
                success = false;
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }

            return new IssuePersonInfoResult
            {
                Success = success,
                RequestJson = requestJson,
                ResponseJson = responseJson,
                ErrorMessage = error
            };
        }


        /// <summary>
        /// 获取人员信息能力集
        /// </summary>
        /// <param name="machine"></param>
        /// <returns></returns>
        public static string GetUserInfoCapabilities(int iUserID)
        {


            var (success, response, errorMessage) = ExecuteHttpRequest(iUserID, "GET", "/ISAPI/AccessControl/UserInfo/capabilities?format=json");
            if (!string.IsNullOrWhiteSpace(errorMessage) && !success)
                IsupLog.Info($"获取能力集失败：{errorMessage}");
            else
            {
                IsupLog.Info("GetUserInfoCapabilities :" +   response);
            }
            return success ? response : "";
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
            // 清洗 URL，去除控制字符
            if (!string.IsNullOrEmpty(urlPath))
                urlPath = new string(urlPath.Where(c => !char.IsControl(c)).ToArray());
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
                //输出缓冲区，如果接口调用失败提示错误码43，需要增大输出缓冲区
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
                    // 若实际数据大于缓冲区，可重新分配并重试
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
        /// 发起人员删除任务
        /// URL：PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json
        /// </summary>
        public static (bool success, string respJson, string errorMsg) StartDeletePerson(im_machine machine, string PersonType,int PersonId)
        {
            string url = "PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json";

            string empNo = PersonType + PersonId;
            var reqBodyObj = new
            {
                UserInfoDetail = new
                {
                    mode = "byEmployeeNo",
                    EmployeeNoList = new[]
                    {
                new { employeeNo = empNo }
            }
                }
            };
            string body = JsonHepler.ToJson(reqBodyObj);
            // 调试：打印发送报文
            IsupLog.Info("删除人员请求报文：" + body);

            return ExecuteHttpRequest(machine.lLoginID, "PUT", url, body);
        }

        /// <summary>
        /// 发起人员删除任务
        /// URL：PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json
        /// </summary>
        public static (bool success, string respJson, string errorMsg) StartDeletePerson(im_machine machine, SyncPersonDataPage person)
        {
            string url = "PUT /ISAPI/AccessControl/UserInfoDetail/Delete?format=json";

            string empNo = person.PersonType + person.PersonId;
            var reqBodyObj = new
            {
                UserInfoDetail = new
                {
                    mode = "byEmployeeNo",
                    EmployeeNoList = new[]
                    {
                new { employeeNo = empNo }
            }
                }
            };
            string body = JsonHepler.ToJson(reqBodyObj);
            // 调试：打印发送报文
            IsupLog.Info("删除人员请求报文：" + body);

            return ExecuteHttpRequest(machine.lLoginID, "PUT", url, body);
        }
        /// <summary>
        /// 查询人员删除任务进度
        /// URL：GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess?format=json
        /// </summary>
        private static (bool success, string respJson, string errorMsg) QueryDeleteProcess(im_machine machine)
        {
            string url = "GET /ISAPI/AccessControl/UserInfoDetail/DeleteProcess?format=json";
            return ExecuteHttpRequest(machine.lLoginID, "GET", url, "");
        }


        /// <summary>
        /// 完整删除人员流程：发起删除任务 + 轮询进度
        /// </summary>
        /// <param name="machine">设备</param>
        /// <param name="person">待删除人员</param>
        /// <param name="timeoutMs">总超时毫秒，默认30秒</param>
        /// <param name="pollIntervalMs">轮询间隔，默认500ms</param>
        /// <returns>(是否成功, 描述信息)</returns>
        public static (bool, string) DelPerson(im_machine machine, string PersonType, int PersonId, int timeoutMs = 30000, int pollIntervalMs = 500)
        {
            // 1.发起删除任务
            var (startOk, startResp, startErr) = StartDeletePerson(machine, PersonType, PersonId);
            if (!startOk)
            {
                return (false, $"发起删除任务失败：{startErr}");
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(pollIntervalMs);

                var (queryOk, queryResp, queryErr) = QueryDeleteProcess(machine);
                if (!queryOk)
                {
                    // 查询接口异常，继续轮询
                    continue;
                }

                // 反序列化进度结果
                UserInfoDetailDeleteProcessRoot processResult;
                try
                {
                    processResult = JsonHepler.FromJson<UserInfoDetailDeleteProcessRoot>(queryResp);
                }
                catch (Exception ex)
                {
                    // JSON解析异常，继续等待下一轮
                    IsupLog.Info($"删除进度JSON解析异常：{ex.Message},原始报文:{queryResp}");
                    continue;
                }

                if (processResult?.UserInfoDetailDeleteProcess == null)
                    continue;

                string status = processResult.UserInfoDetailDeleteProcess.status?.ToLower() ?? "";
                switch (status)
                {
                    case "success":
                        return (true, "人员删除成功");
                    case "failed":
                        return (false, "设备执行删除任务失败");
                    case "processing":
                        // 继续循环
                        break;
                    default:
                        return (false, $"未知任务状态：{status}");
                }
            }
            watch.Stop();
            return (false, $"删除任务执行超时（{timeoutMs}ms）");
        }


        /// <summary>
        /// 完整删除人员流程：发起删除任务 + 轮询进度
        /// </summary>
        /// <param name="machine">设备</param>
        /// <param name="person">待删除人员</param>
        /// <param name="timeoutMs">总超时毫秒，默认30秒</param>
        /// <param name="pollIntervalMs">轮询间隔，默认500ms</param>
        /// <returns>(是否成功, 描述信息)</returns>
        public static (bool, string) DelPerson(im_machine machine, SyncPersonDataPage person, int timeoutMs = 30000, int pollIntervalMs = 500)
        {
            // 1.发起删除任务
            var (startOk, startResp, startErr) = StartDeletePerson(machine, person);
            if (!startOk)
            {
                return (false, $"发起删除任务失败：{startErr}");
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(pollIntervalMs);

                var (queryOk, queryResp, queryErr) = QueryDeleteProcess(machine);
                if (!queryOk)
                {
                    // 查询接口异常，继续轮询
                    continue;
                }

                // 反序列化进度结果
                UserInfoDetailDeleteProcessRoot processResult;
                try
                {
                    processResult = JsonHepler.FromJson<UserInfoDetailDeleteProcessRoot>(queryResp);
                }
                catch (Exception ex)
                {
                    // JSON解析异常，继续等待下一轮
                    IsupLog.Info($"删除进度JSON解析异常：{ex.Message},原始报文:{queryResp}");
                    continue;
                }

                if (processResult?.UserInfoDetailDeleteProcess == null)
                    continue;

                string status = processResult.UserInfoDetailDeleteProcess.status?.ToLower() ?? "";
                switch (status)
                {
                    case "success":
                        return (true, "人员删除成功");
                    case "failed":
                        return (false, "设备执行删除任务失败");
                    case "processing":
                        // 继续循环
                        break;
                    default:
                        return (false, $"未知任务状态：{status}");
                }
            }
            watch.Stop();
            return (false, $"删除任务执行超时（{timeoutMs}ms）");
        }

        /// <summary>
        /// 完整删除人员流程：发起删除任务 + 轮询进度
        /// </summary>
        /// <param name="machine">设备</param>
        /// <param name="person">待删除人员</param>
        /// <param name="timeoutMs">总超时毫秒，默认30秒</param>
        /// <param name="pollIntervalMs">轮询间隔，默认500ms</param>
        /// <returns>下发结果模型</returns>
        public static IssuePersonInfoResult DelPersonInfo(im_machine machine, SyncPersonDataPage person, int timeoutMs = 30000, int pollIntervalMs = 500)
        {
            var result = new IssuePersonInfoResult();

            // 1.发起删除任务
            var (startOk, startResp, startErr) = StartDeletePerson(machine, person);
            result.RequestJson = JsonHepler.ToJson(new
            {
                UserInfoDetail = new
                {
                    mode = "byEmployeeNo",
                    EmployeeNoList = new[]
                    {
                new { employeeNo = person.PersonType + person.PersonId }
            }
                }
            });
            result.ResponseJson = startResp;

            if (!startOk)
            {
                result.Success = false;
                result.ErrorMessage = $"发起删除任务失败：{startErr}";
                return result;
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < timeoutMs)
            {
                Thread.Sleep(pollIntervalMs);

                var (queryOk, queryResp, queryErr) = QueryDeleteProcess(machine);
                if (!queryOk)
                {
                    // 查询进度异常，继续轮询，不中断
                    continue;
                }

                // 更新为最新进度响应
                result.ResponseJson = queryResp;

                UserInfoDetailDeleteProcessRoot processResult;
                try
                {
                    processResult = JsonHepler.FromJson<UserInfoDetailDeleteProcessRoot>(queryResp);
                }
                catch (Exception ex)
                {
                    result.ErrorMessage = $"删除进度JSON解析异常：{ex.Message}";
                    continue;
                }

                if (processResult?.UserInfoDetailDeleteProcess == null)
                    continue;

                string status = processResult.UserInfoDetailDeleteProcess.status?.ToLower() ?? "";
                switch (status)
                {
                    case "success":
                        result.Success = true;
                        result.ErrorMessage = "";
                        return result;
                    case "failed":
                        result.Success = false;
                        result.ErrorMessage = "设备执行删除任务失败";
                        return result;
                    case "processing":
                        break;
                    default:
                        result.Success = false;
                        result.ErrorMessage = $"未知任务状态：{status}";
                        return result;
                }
            }
            watch.Stop();

            // 循环超时退出
            result.Success = false;
            result.ErrorMessage = $"删除任务执行超时（{timeoutMs}ms）";
            return result;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public class PostPerson
    {
        public UserInfo UserInfo { get; set; }
    }

    public class UserInfo
    {
        [JsonProperty("employeeNo")]
        public string EmployeeNo { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("userType")]
        public string UserType { get; set; }
        public Valid Valid { get; set; }
        [JsonProperty("belongGroup")]
        public string BelongGroup { get; set; } // 注意：这里不应有空格
        [JsonProperty("doorRight")]
        public string DoorRight { get; set; }
        public List<RightPlan> RightPlan { get; set; }

        [JsonProperty("deleteUser")]
        public bool? DeleteUser { get; set; }
    }

    public class Valid
    {
        [JsonProperty("enable")]
        public bool Enable { get; set; }
        [JsonProperty("beginTime")]
        public string BeginTime { get; set; }
        [JsonProperty("endTime")]
        public string EndTime { get; set; }
        [JsonProperty("timeType")]
        public string TimeType { get; set; }
    }

    public class RightPlan
    {
        [JsonProperty("doorNo")]
        public int DoorNo { get; set; }
        [JsonProperty("planTemplateNo")]
        public string PlanTemplateNo { get; set; }
    }


    public class DelPerson
    {
        /// <summary>
        /// /*opt, object, 用户信息*/
        /// </summary>
        public UserInfoDetail UserInfoDetail { get; set; }
    }
    /// <summary>
    /// /*opt, object, 用户信息*/
    /// </summary>
    public class UserInfoDetail
    {
        /// <summary>
        ///  /*req, enum, 删除模式, subType:string, [all#删除所有,byEmployeeNo#按工号]*/
        /// </summary>
        public string mode { get; set; } = "byEmployeeNo";

        /// <summary>
        /// 
        /// </summary>
        public List<EmployeeNoList> EmployeeNoList { get; set; }
    }
    public class EmployeeNoList
    {
        /// <summary>
        /// /*opt, string, 工号, range:[,]*/
        /// </summary>
        public string employeeNo { get; set; }
    }


    public class UserInfoSearchCond
    {
        /// <summary>
        /// /*req, string,搜索记录唯一标识, range:[,], 
        /// desc:搜索记录唯一标识,用来确认上层客户端是否为同一个(倘若是同一个,设备记录内存,下次搜索加快速度),string类型*/
        /// </summary>
        public string searchID { get; set; }

        /// <summary>
        /// /*opt, int, 查询结果在结果列表中的起始位置, range:[,], step:, unit:, unitType:,
        /// desc:当记录条数很多时,一次查询不能获取所有的记录,
        /// 下一次查询时指定位置可以查询后面的记录
        /// （若设备支持的最大totalMatches为M个，但是当前设备已存储的totalMatches为N个（N<=M），则该字段的合法范围为0~N-1）*/
        /// </summary>
        public int searchResultPosition { get; set; }


        /// <summary>
        /// /*opt, int, 本次协议调用可获取的最大记录数, range:[,], step:, unit:, unitType:, 
        /// desc:如maxResults值大于设备能力集返回的范围，则设备按照能力集最大值返回，设备不进行报错*/
        /// </summary>
        public int maxResults { get; set; }


        /// <summary>
        /// /*opt, array, 人员ID列表, subType:object, range:[,]*/
        /// </summary>
        public List<EmployeeNoList> EmployeeNoList { get; set; }
    }

    public class UserInfoSearch
    {
        /// <summary>
        /// /*ro, req, string, 搜索记录唯一标识, range:[,]*/
        /// </summary>
        public string searchID { get; set; }

        /// <summary>
        /// /*ro, req, enum, 查询状态字符串描述, subType:string, 
        /// [OK#查询结束,MORE#还有数据等待查询,NO MATCH#没有匹配数据],
        /// desc:OK-查询结束,MORE-还有数据等待查询,NO MATCH-没有匹配数据*/
        /// </summary>
        public string responseStatusStrg { get; set; }

        /// <summary>
        ///  /*ro, req, int, 本次返回的记录条数, range:[,], step:, unit:, unitType:*/
        /// </summary>
        public string numOfMatches { get; set; }

        /// <summary>
        ///    /*ro, req, int, 符合条件的记录总条数, range:[,], step:, unit:, unitType:*/
        /// </summary>
        public string totalMatches { get; set; }

        /// <summary>
        /// /*ro, opt, array, 人员信息, subType:object, range:[,]*/
        /// </summary>
        public List<UserInfo> UserInfo { get; set; }
    }


    public class UserInfoSearch1
    {
        /// <summary>
        /// 
        /// </summary>
        public UserInfoSearch UserInfoSearch { get; set; }
    }


    /// <summary>
    /// 删除人员进度返回实体
    /// </summary>
    public class UserInfoDetailDeleteProcessRoot
    {
        public UserInfoDetailDeleteProcessItem UserInfoDetailDeleteProcess { get; set; }
    }

    public class UserInfoDetailDeleteProcessItem
    {
        /// <summary>
        /// processing 处理中 / success 成功 / failed 失败
        /// </summary>
        public string status { get; set; }
    }


}
