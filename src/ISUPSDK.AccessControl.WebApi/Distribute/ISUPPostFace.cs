using ISUPSDK.AccessControl.WebApi.Services.Logging;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.SdkService.CmsService;
using SkiaSharp;
using System.Runtime.InteropServices;
using System.Text;
namespace ISUPSDK.AccessControl.WebApi.Distribute
{
    public class ISUPPostFace
    {

        


        /// <summary>
        /// 下发人面信息
        /// </summary>
        public static async Task<(bool lb, List<returnErrorModel> tempIdList)> PUT(im_machine machine, Detail person, string Prefix, Settings settings)
        {
            string FPID = Prefix + person.webId.ToString();
            var tempIdList=new List<returnErrorModel>();
            


            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            try
            {
                
                string bornTime = "2004-05-03";
                string FaceUrl = person.photoPath;
                string ReplaceDomainName= settings.ReplaceDomainName;
                switch (settings.FacePicType)
                {
                    case 1: // 替换域名（） 不需要下载到本地通过iis来访问
                        FaceUrl= ReplaceDomain(FaceUrl, settings.ReplaceDomainName);
                        IsupLog.Info($"{FaceUrl}");
                        break;
                    case 2: // 替换域名（） 需要下载到本地通过iis来访问
                        await DownloadFileAndSaveLocallyAsync(person.photoPath, settings.Photo1 + FPID+".jpg",200, settings.ForceUpdatePic);
                        
                        FaceUrl = ReplaceDomainName + FPID + ".jpg";
                        IsupLog.Info($"{FaceUrl}");
                        break;
                   
                }
                bool l = await IsImageAccessibleAsync(FaceUrl);
                if (!l)
                {
                    tempIdList.Add(new returnErrorModel() { DataId = person.dataId, ErrCode = -1, Err = "下发人员照片访问不了 "+ FaceUrl });
                    return (false, tempIdList);
                }
                // 使用 StringBuilder 来构造 XML 字符串，性能更优
                var inputXml = new StringBuilder();
                inputXml.Append("{\"faceURL\":\"").Append(FaceUrl)
                    .Append("\",\"faceLibType\":\"blackFD\",\"FDID\":\"1\",\"name\":\"")
                    .Append(person.name).Append("\",\"FPID\":\"").Append(FPID)
                    .Append("\",\"bornTime\":\"").Append(bornTime).Append("\"}");

                String strTemp = "/ISAPI/Intelligent/FDLib/FDSetUp?format=json";
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


                        IsupLog.Info($"下发人面信息失败{iLastErr}。{ErrCodeMean}。" + FaceUrl);
                        // 记录失败信息到 tempIdList
                        AddErrorToList(tempIdList, person.dataId, $"下发人面信息失败{iLastErr}。{ErrCodeMean}。" + FaceUrl);
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            IsupLog.Info($"下发人面信息失败。{errorMsg}。 " + FaceUrl);
                            AddErrorToList(tempIdList, person.dataId, $"下发人面信息失败。{errorMsg}。 " + FaceUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    
                }

                return (lb, tempIdList);
            }
            catch (Exception ex)
            {
                // 记录异常
                
                return (false, tempIdList);
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }
        public static string ReplaceDomain(string url, string newDomain)
        {
            Uri uri = new(url);
            string host = uri.Host;
            if (host== "admin.krbb.cn")
            {
                //PathAndQuery

                return $"{newDomain}{uri.PathAndQuery.Replace("yeyfiles/upfiles/", "/yeyupfiles/")}";

                //yeyfiles/upfiles/  去掉


            }


            // 构建新的URL：新域名和原始的路径、查询字符串、端口等
            string newUrl = $"{newDomain}{uri.PathAndQuery}";

            return newUrl;
        }
        public static async Task<bool> IsImageAccessibleAsync(string imageUrl)
        {
            // 使用 HttpClient 发送请求
            using var client = new HttpClient();
            try
            {
                // 设置超时时间，避免请求时间过长
                client.Timeout = TimeSpan.FromSeconds(10);

                // 发送 GET 请求并获取响应
                var response = await client.GetAsync(imageUrl);

                // 判断状态码是否是 2xx (成功) 的范围
                if (response.IsSuccessStatusCode)
                {
                    // 判断返回内容类型是否是图片（可以检查 Content-Type 头部）
                    string? contentType = response.Content.Headers.ContentType.MediaType;
                    if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;  // 图片可以访问
                    }
                }
            }
            catch (Exception)
            {
                // 捕获异常，返回 false
                return false;
            }

            return false;  // 如果不是成功状态码或不是图片格式，返回 false
        }


        // 分辨率限制常量
        private const long MaxPixelCount = 4_000_000; // 400 万分辨率上限
        private const int SuggestedWidth = 640;        // 建议宽度
        private const int SuggestedHeight = 480;       // 建议高度

        public static async Task<bool> DownloadFileAndSaveLocallyAsync(string url, string localFilePath, long maxFileSizeKB = 200,bool ForceUpdatePic=true)
        {
            try
            {
                if (File.Exists(localFilePath))
                {
                    if (ForceUpdatePic)
                    {
                        try
                        {
                            File.Delete(localFilePath);
                        }
                        catch (Exception)
                        {

                            return false;
                        }

                    }
                    else
                    {
                        return true;
                    }
                }

                // 下载文件
                using HttpClient client = new();
                byte[] fileBytes = await client.GetByteArrayAsync(url);

                // 使用SkiaSharp加载图像
                using SKBitmap decoded = SKBitmap.Decode(fileBytes);
                if (decoded == null)
                {
                    IsupLog.Info("无法解码图像");
                    return false;
                }

                // 计算目标尺寸：不超过 400 万分辨率，并参考 640*480 建议值
                (int targetWidth, int targetHeight) = CalculateTargetSize(decoded.Width, decoded.Height);

                SKBitmap resized = null;
                SKBitmap bitmap = decoded;
                if (targetWidth != decoded.Width || targetHeight != decoded.Height)
                {
                    resized = decoded.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
                    if (resized == null)
                    {
                        IsupLog.Info("图像缩放失败");
                        return false;
                    }
                    bitmap = resized;
                }

                try
                {
                    // 使用一个内存流来保存JPEG图像，逐步压缩
                    using MemoryStream jpegStream = new();
                    using SKImage img = SKImage.FromBitmap(bitmap);

                    // 按指定质量编码为 JPEG 字节
                    byte[] Encode(int q)
                    {
                        jpegStream.SetLength(0);
                        using SKData data = img.Encode(SKEncodedImageFormat.Jpeg, q);
                        data.SaveTo(jpegStream);
                        return jpegStream.ToArray();
                    }

                    // 设置初始质量（从100开始逐步降低）
                    int quality = 100;
                    byte[] jpegBytes = Encode(quality);

                    // 如果文件大小大于 maxFileSizeKB，则逐步降低质量，直到小于 maxFileSizeKB
                    while (jpegBytes.Length > maxFileSizeKB * 1024 && quality > 10)
                    {
                        quality -= 10; // 降低10%的质量
                        jpegBytes = Encode(quality);
                    }

                    // 检查压缩后文件大小是否仍然超标
                    if (jpegBytes.Length > maxFileSizeKB * 1024)
                    {
                        IsupLog.Info("无法将图像压缩到指定大小。");
                        return false;
                    }

                    // 将压缩后的图像字节数组保存到本地文件
                    await File.WriteAllBytesAsync(localFilePath, jpegBytes);

                    //IsupLog.Info($"文件已保存到: {localFilePath}");
                    return true;
                }
                finally
                {
                    resized?.Dispose();
                }
            }
            catch (Exception ex)
            {
                // 处理异常，例如记录日志
                IsupLog.Info($"下载文件时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 计算符合分辨率要求的等比缩放后尺寸。
        /// 1) 总像素不超过 400 万（硬性限制）；
        /// 2) 参考 640*480 建议值（仅缩小，不放大）。
        /// </summary>
        private static (int Width, int Height) CalculateTargetSize(int width, int height)
        {
            double scale = 1.0;

            // 1) 硬性限制：总像素不超过 400 万
            double pixels = (double)width * height;
            if (pixels > MaxPixelCount)
            {
                scale = Math.Sqrt(MaxPixelCount / pixels);
            }

            // 2) 建议值：等比缩放到 640*480 以内（仅缩小，不放大）
            if (width > SuggestedWidth || height > SuggestedHeight)
            {
                double suggestedScale = Math.Min((double)SuggestedWidth / width, (double)SuggestedHeight / height);
                scale = Math.Min(scale, suggestedScale);
            }

            if (scale >= 1.0)
            {
                return (width, height);
            }

            int newWidth = Math.Max(1, (int)Math.Round(width * scale));
            int newHeight = Math.Max(1, (int)Math.Round(height * scale));
            return (newWidth, newHeight);
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


        /// <summary>
        /// 下发人面信息
        /// </summary>
        public static async Task<(bool lb, string Error)> PUT(im_machine machine,  Settings settings, SyncPersonDataPage person)
        {
            string FPID = person.PersonType+ person.PersonId;
            string Error=string.Empty;
            byte[] m_szUrl = new byte[1024];
            HCISUPCMS.NET_EHOME_PTXML_PARAM struPTXML = new();
            IntPtr ptrCfg = IntPtr.Zero;
            try
            {

                string bornTime = "2004-05-03";
                string FaceUrl = person.Pic;
                string ReplaceDomainName = settings.ReplaceDomainName;
                switch (settings.FacePicType)
                {
                    case 1: // 替换域名（） 不需要下载到本地通过iis来访问
                        FaceUrl = ReplaceDomain(FaceUrl, settings.ReplaceDomainName);
                        IsupLog.Info($"{FaceUrl}");
                        break;
                    case 2: // 替换域名（） 需要下载到本地通过iis来访问
                        await DownloadFileAndSaveLocallyAsync(person.Pic, settings.Photo1 + FPID + ".jpg", 200, settings.ForceUpdatePic);

                        FaceUrl = ReplaceDomainName + FPID + ".jpg";
                        IsupLog.Info($"{FaceUrl}");
                        break;

                }
                bool l = await IsImageAccessibleAsync(FaceUrl);
                if (!l)
                {
                    
                    return (false, "下发人员照片访问不了 " + FaceUrl);
                }
                // 使用 StringBuilder 来构造 XML 字符串，性能更优
                var inputXml = new StringBuilder();
                inputXml.Append("{\"faceURL\":\"").Append(FaceUrl)
                    .Append("\",\"faceLibType\":\"blackFD\",\"FDID\":\"1\",\"name\":\"")
                    .Append(person.PersonName).Append("\",\"FPID\":\"").Append(FPID)
                    .Append("\",\"bornTime\":\"").Append(bornTime).Append("\"}");

                String strTemp = "/ISAPI/Intelligent/FDLib/FDSetUp?format=json";
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

                        Error = $"下发人面信息失败{iLastErr}。{ErrCodeMean}。" + FaceUrl;
                        IsupLog.Info(Error);
                        // 记录失败信息到 tempIdList
                        
                    }
                    else
                    {
                        string strOutPuts = Marshal.PtrToStringAnsi(struPTXML.pOutBuffer, (int)struPTXML.dwOutSize);
                        lb = ISUPResultMethod.Method(strOutPuts, out int statusCode, out string errorMsg);
                        if (!lb)
                        {
                            Error = $"下发人面信息失败。{errorMsg}。 " + FaceUrl;
                            IsupLog.Info(Error);

                        }
                        else
                            Error = strOutPuts;
                    }
                }
                catch (Exception ex)
                {

                }

                return (lb, Error);
            }
            catch (Exception ex)
            {
                // 记录异常

                return (false, "");
            }
            finally
            {
                CleanupResources(struPTXML, ptrCfg);
            }
        }


        /// <summary>
        /// 下发人面信息
        /// </summary>
        public static async Task<IssuePersonFaceResult> PUTFace(im_machine machine, Settings settings, SyncPersonDataPage person)
        {
            string FPID = person.PersonType + person.PersonId;
            string requestJson = string.Empty;
            string bornTime = "2004-05-03";
            string FaceUrl = person.Pic;
            string ReplaceDomainName = settings.ReplaceDomainName;
            switch (settings.FacePicType)
            {
                case 1: // 替换域名（） 不需要下载到本地通过iis来访问
                    FaceUrl = ReplaceDomain(FaceUrl, settings.ReplaceDomainName);
                    IsupLog.Info($"{FaceUrl}");
                    break;
                case 2: // 替换域名（） 需要下载到本地通过iis来访问
                    await DownloadFileAndSaveLocallyAsync(person.Pic, settings.Photo1 + FPID + ".jpg", 200, settings.ForceUpdatePic);
                    FaceUrl = ReplaceDomainName + FPID + ".jpg";
                    IsupLog.Info($"{FaceUrl}");
                    break;
            }

            bool l = await IsImageAccessibleAsync(FaceUrl);
            if (!l)
            {
                return new IssuePersonFaceResult
                {
                    Success = false,
                    RequestJson = requestJson,
                    ResponseJson = "",
                    ErrorMessage = "下发人员照片访问不了 " + FaceUrl
                };
            }

            // 使用 StringBuilder 来构造 XML 字符串，性能更优
            var inputXml = new StringBuilder();
            inputXml.Append("{\"faceURL\":\"").Append(FaceUrl)
                .Append("\",\"faceLibType\":\"blackFD\",\"FDID\":\"1\",\"name\":\"")
                .Append(person.PersonName).Append("\",\"FPID\":\"").Append(FPID)
                .Append("\",\"bornTime\":\"").Append(bornTime).Append("\"}");

            requestJson = inputXml.ToString(); // 捕获请求 JSON
            string urlPath = $"/ISAPI/Intelligent/FDLib/FDSetUp?format=json";

            var (success, response, errorMessage) = ExecuteHttpRequest(machine.lLoginID, "PUT", urlPath, requestJson);

            return new IssuePersonFaceResult
            {
                Success = success,
                RequestJson = requestJson,
                ResponseJson = response,
                ErrorMessage = errorMessage
            };

        }
        public static IssuePersonFaceResult DelFace(im_machine machine,string FPID)
        {
            string requestJson = "";
            string urlPath = "";

            requestJson = "{\r\n    \"FPID\": [\r\n        {\r\n            \"value\": \"" + FPID + "\"\r\n        }\r\n    ]\r\n}";
            urlPath = "/ISAPI/Intelligent/FDLib/FDSearch/Delete?format=json&FDID=1&faceLibType=blackFD";
            var (success, response, errorMessage) = ExecuteHttpRequest(machine.lLoginID, "PUT", urlPath, requestJson);

            return new IssuePersonFaceResult
            {
                Success = success,
                RequestJson = requestJson,
                ResponseJson = response,
                ErrorMessage = errorMessage
            };
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
    
}
