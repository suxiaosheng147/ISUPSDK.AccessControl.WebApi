using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using System.Runtime.InteropServices;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPDevTime
    {
        /// <summary>
        /// 设置时间
        /// </summary>
        /// <param name="lLoginID">机器SDK句柄</param>
        /// <param name="localTime">时间</param>
        /// <returns></returns>
        public static bool Post(int lLoginID, string localTime)
        {
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;

            try
            {
                var inputXml = $@"<Time><timeMode>manual</timeMode><localTime>{localTime}</localTime><timeZone>CST-8:00:00</timeZone></Time>";
                String strTemp = "PUT /ISAPI/System/time";
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
                    if (!HCISUPCMS.NET_ECMS_PutPTXMLConfig(lLoginID, ptrCfg))
                    {
                        int iLastErr = HCISUPCMS.NET_ECMS_GetLastError();
                        ErrCodeDictionary.ErrCode.TryGetValue(iLastErr, out string? ErrCodeMean);
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
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

        private static void CleanupResources(HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML, IntPtr ptrCfg)
        {
            // 清理分配的内存
            if (struPTXML.pRequestUrl != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pRequestUrl);
            if (struPTXML.pInBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pInBuffer);
            if (struPTXML.pOutBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pOutBuffer);
            if (struPTXML.pCondBuffer != IntPtr.Zero) Marshal.FreeHGlobal(struPTXML.pCondBuffer);
            if (ptrCfg != IntPtr.Zero) Marshal.FreeHGlobal(ptrCfg);
        }
    }
}
