using ISUPSDK.AccessControl.WebApi.Services.Logging;
using HKEDUBasicDataSync.Methods;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public class HttpHelper
{
    /// <summary>平台接口请求超时（秒）。原实现未设超时（默认 100 秒），平台不可达时会长时间卡住。</summary>
    private const int PlatformTimeoutSeconds = 8;

    // 静态 HttpClient 实例，以便在整个应用程序生命周期内复用
    private static readonly HttpClient client = new();

    static HttpHelper()
    {
        // 可以在此设置默认的一些请求配置，比如超时时间、基础地址等
        // client.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// 异步Post请求指定地址
    /// </summary>
    /// <param name="url">请求的URL</param>
    /// <param name="postModel">可选的POST请求体</param>
    /// <returns>异步返回响应内容</returns>

    public static async Task<string> PostUrlAsync(string url, object? postModel = null, string authorizationToken = "")
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            using var client = new HttpClient();
            
            // 如果提供了授权令牌，添加 Authorization 请求头
            if (!string.IsNullOrWhiteSpace(authorizationToken))
            {
                var customHeaders = new Dictionary<string, string> { { "Authorization", "bearer " + authorizationToken } };

                foreach (var header in customHeaders)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer ", authorizationToken);
            }


            // 根据 postModel 是否为空来决定请求的 Content-Type
            HttpContent content;

            if (postModel != null)
            {
                // 如果有 postModel，使用 JSON 格式
                content = new StringContent(JsonConvert.SerializeObject(postModel), Encoding.UTF8, "application/json");
            }
            else
            {
                // 使用x-www-form-urlencoded格式进行POST请求
                content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");

            }

            // 发起POST请求并等待响应
            HttpResponseMessage response = await client.PostAsync(url, content);

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 获取响应内容
            string result = await response.Content.ReadAsStringAsync();

            return result;
        }
        catch (Exception ex)
        {
            // 在捕获异常时记录错误日志
            IsupLog.Info($"Error occurred: {ex.Message}");
            // 可以根据需要选择抛出异常或返回特定的错误信息
            return "";
        }
    }
    /// <summary>
    /// 异步Get请求指定地址
    /// </summary>
    /// <param name="url">请求的URL</param>
    /// <param name="authorizationToken">授权令牌</param>
    /// <returns>异步返回响应内容，若状态码非200则返回空字符串</returns>
    public static async Task<string> GetUrlAsync(string url, string authorizationToken = "")
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13 | SecurityProtocolType.Tls11;
            // 原实现未设超时（默认 100 秒）：平台不可达时每台设备要卡 100 秒才失败。
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(PlatformTimeoutSeconds) };

            if (!string.IsNullOrWhiteSpace(authorizationToken))
            {
                client.DefaultRequestHeaders.Add("Authorization", "bearer " + authorizationToken);
            }

            HttpResponseMessage response = await client.GetAsync(url);

            // 读取响应内容（无论状态码如何，先读取，因为可能包含错误描述）
            string responseBody = await response.Content.ReadAsStringAsync();

            // 检查状态码是否为 200（如果期望严格200）
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
            {
                // 状态码非200时打印状态码和响应内容
                IsupLog.Info($"非200响应 | URL: {url} | StatusCode: {(int)response.StatusCode} {response.StatusCode} | Body: {responseBody}");
                return ""; // 可根据需要改为抛出异常或返回特定错误标记
            }

            // 状态码为200，正常返回内容（可选择性打印成功日志，取消注释即可）
            // IsupLog.Info($"成功响应 | URL: {url} | StatusCode: 200");
            return responseBody;
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            // 平台不可达 / 网络抖动时的常态：只记一行（不打堆栈），避免刷屏。
            IsupLog.Warn("平台接口超时或被取消：{0}（{1}）", url, ex.Message);
            return "";
        }
        catch (Exception ex)
        {
            IsupLog.Warn("平台接口请求失败：{0}（{1}）", url, ex.Message);
            return "";
        }
    }

    /// <summary>
    /// 异步Get请求指定地址
    /// </summary>
    /// <param name="url">请求的URL</param>
    /// <param name="authorizationToken">授权令牌</param>
    /// <returns>异步返回响应内容</returns>
    public static async Task<string> GetUrlAsync1(string url, string authorizationToken = "")
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13 | SecurityProtocolType.Tls11;
            using var client = new HttpClient();

            // 如果提供了授权令牌，添加 Authorization 请求头
            if (!string.IsNullOrWhiteSpace(authorizationToken))
            {
                var customHeaders = new Dictionary<string, string> { { "Authorization", "bearer " + authorizationToken } };

                foreach (var header in customHeaders)
                {
                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
                }
                //client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer ", authorizationToken);
            }
            // 发起POST请求并等待响应
            HttpResponseMessage response = await client.GetAsync(url);

            // 确保响应成功
            response.EnsureSuccessStatusCode();

            // 获取响应内容
            string result = await response.Content.ReadAsStringAsync();

            return result;
        }
        catch (Exception ex)
        {
            // 在捕获异常时记录错误日志
            IsupLog.Info($" Data:{DateTime.Now}  Error occurred: {ex.Message} " + url);
            // 可以根据需要选择抛出异常或返回特定的错误信息
            return "";
        }
    }

    /// <summary>
    /// 异步Get请求指定地址（带重试机制）
    /// </summary>
    /// <param name = "url" > 请求的URL </ param >
    /// < param name="authorizationToken">授权令牌</param>
    /// <param name = "maxRetryAttempts" > 最大重试次数（默认3次）</param>
    /// <param name = "delayBetweenRetries" > 重试之间的基础延迟时间（毫秒，默认500ms）</param>
    /// <returns>异步返回响应内容</returns>
    //public static async Task<string> GetUrlAsync(
    //    string url,
    //    string authorizationToken = "",
    //    int maxRetryAttempts = 3,
    //    int delayBetweenRetries = 500)
    //{
    //    int retryCount = 0;

    //    while (true)
    //    {
    //        try
    //        {
    //            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13 | SecurityProtocolType.Tls11;
    //            using var client = new HttpClient();

    //            如果提供了授权令牌，添加 Authorization 请求头
    //            if (!string.IsNullOrWhiteSpace(authorizationToken))
    //            {
    //                var customHeaders = new Dictionary<string, string> { { "Authorization", "bearer " + authorizationToken } };

    //                foreach (var header in customHeaders)
    //                {
    //                    client.DefaultRequestHeaders.Add(header.Key, header.Value);
    //                }
    //            }

    //            发起GET请求并等待响应
    //           HttpResponseMessage response = await client.GetAsync(url);

    //            确保响应成功
    //            response.EnsureSuccessStatusCode();

    //            获取响应内容
    //            string result = await response.Content.ReadAsStringAsync();

    //            return result;
    //        }
    //        catch (Exception ex) when (retryCount < maxRetryAttempts)
    //        {
    //            retryCount++;

    //            计算退避延迟（指数退避策略）
    //            double delay = delayBetweenRetries * Math.Pow(2, retryCount - 1);

    //            添加随机抖动以避免多个客户端同时重试
    //           Random jitterer = new Random();
    //            delay = delay * (0.8 + 0.4 * jitterer.NextDouble());

    //            记录重试信息
    //            IsupLog.Info($"{DateTime.Now} 请求失败，正在进行第 {retryCount} 次重试，等待 {delay:F0}ms 后重试。错误: {ex.Message}; URL：{url}");

    //            等待一段时间后重试
    //           await Task.Delay(TimeSpan.FromMilliseconds(delay));
    //        }
    //        catch (Exception ex)
    //        {
    //            在捕获异常且达到最大重试次数时记录错误日志
    //            IsupLog.Info($"{DateTime.Now}请求失败，已达到最大重试次数 {maxRetryAttempts}。最终错误: {ex.Message} " + url);

    //            可以根据需要选择抛出异常或返回特定的错误信息
    //            return "";
    //        }
    //    }
    //}

    /// <summary>
    /// 异步Put请求指定地址（带重试机制）
    /// </summary>
    /// <param name="url">请求的URL</param>
    /// <param name="postModel">可选的POST请求体</param>
    /// <param name="authorizationToken">授权令牌</param>
    /// <param name="maxRetryAttempts">最大重试次数（默认3次）</param>
    /// <param name="delayBetweenRetries">重试之间的基础延迟时间（毫秒，默认500ms）</param>
    /// <returns>异步返回响应内容</returns>
    public static async Task<string> PutUrlAsync(
        string url,
        object? postModel = null,
        string authorizationToken = "",
        int maxRetryAttempts = 3,
        int delayBetweenRetries = 500)
    {
        int retryCount = 0;
        string json= JsonHepler.ToJson(postModel);
        while (true)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13 | SecurityProtocolType.Tls11;
                using var client = new HttpClient();

                // 设置超时时间，避免长时间等待
                client.Timeout = TimeSpan.FromSeconds(30);

                // 如果提供了授权令牌，添加 Authorization 请求头
                if (!string.IsNullOrWhiteSpace(authorizationToken))
                {
                    var customHeaders = new Dictionary<string, string> { { "Authorization", "bearer " + authorizationToken } };

                    foreach (var header in customHeaders)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                // 根据 postModel 是否为空来决定请求的 Content-Type
                HttpContent content;

                if (postModel != null)
                {
                    // 如果有 postModel，使用 JSON 格式
                    content = new StringContent(json, Encoding.UTF8, "application/json");
                }
                else
                {
                    // 使用x-www-form-urlencoded格式进行POST请求
                    content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
                }

                // 发起PUT请求并等待响应
                HttpResponseMessage response = await client.PutAsync(url, content);

                // 确保响应成功
                response.EnsureSuccessStatusCode();

                // 获取响应内容
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex) when (retryCount < maxRetryAttempts)
            {
                retryCount++;

                // 计算退避延迟（指数退避策略）
                double delay = delayBetweenRetries * Math.Pow(2, retryCount - 1);

                // 添加随机抖动以避免多个客户端同时重试
                Random jitterer = new Random();
                delay = delay * (0.8 + 0.4 * jitterer.NextDouble());

                // 记录重试信息
                IsupLog.Info($"{DateTime.Now} PutUrlAsync 请求失败，正在进行第 {retryCount} 次重试，等待 {delay:F0}ms 后重试。错误: {ex.Message}; URL：{url} Josn：{json}");

                // 等待一段时间后重试
                await Task.Delay(TimeSpan.FromMilliseconds(delay));
            }
            catch (Exception ex)
            {
                // 在捕获异常且达到最大重试次数时记录错误日志
                IsupLog.Info($"{DateTime.Now} PutUrlAsync 请求失败，已达到最大重试次数 {maxRetryAttempts}。最终错误: {ex.Message} , Josn：{json}" + url );

                // 可以根据需要选择抛出异常或返回特定的错误信息
                return "";
            }
        }



    }
    /// <summary>
    /// 异步Put请求指定地址（带重试机制）
    /// </summary>
    /// <param name="url">请求的URL</param>
    /// <param name="postModel">可选的POST请求体</param>
    /// <param name="authorizationToken">授权令牌</param>
    /// <param name="maxRetryAttempts">最大重试次数（默认3次）</param>
    /// <param name="delayBetweenRetries">重试之间的基础延迟时间（毫秒，默认500ms）</param>
    /// <returns>异步返回响应内容</returns>
    public static async Task<string> PutUrl1Async(
        string url,
        string? postModel = null,
        string authorizationToken = "",
        int maxRetryAttempts = 3,
        int delayBetweenRetries = 500)
    {
        int retryCount = 0;

        while (true)
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls13 | SecurityProtocolType.Tls11;
                using var client = new HttpClient();

                // 设置超时时间，避免长时间等待
                client.Timeout = TimeSpan.FromSeconds(30);

                // 如果提供了授权令牌，添加 Authorization 请求头
                if (!string.IsNullOrWhiteSpace(authorizationToken))
                {
                    var customHeaders = new Dictionary<string, string> { { "Authorization", "bearer " + authorizationToken } };

                    foreach (var header in customHeaders)
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                // 根据 postModel 是否为空来决定请求的 Content-Type
                HttpContent content;

                if (postModel != null)
                {
                    // 如果有 postModel，使用 JSON 格式
                    content = new StringContent(postModel, Encoding.UTF8, "application/json");
                }
                else
                {
                    // 使用x-www-form-urlencoded格式进行POST请求
                    content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded");
                }

                // 发起PUT请求并等待响应
                HttpResponseMessage response = await client.PutAsync(url, content);

                // 确保响应成功
                response.EnsureSuccessStatusCode();

                // 获取响应内容
                string result = await response.Content.ReadAsStringAsync();
                return result;
            }
            catch (Exception ex) when (retryCount < maxRetryAttempts)
            {
                retryCount++;

                // 计算退避延迟（指数退避策略）
                double delay = delayBetweenRetries * Math.Pow(2, retryCount - 1);

                // 添加随机抖动以避免多个客户端同时重试
                Random jitterer = new Random();
                delay = delay * (0.8 + 0.4 * jitterer.NextDouble());

                // 记录重试信息
                IsupLog.Info($"{DateTime.Now} PutUrlAsync 请求失败，正在进行第 {retryCount} 次重试，等待 {delay:F0}ms 后重试。错误: {ex.Message}; URL：{url} Josn：{postModel}");

                // 等待一段时间后重试
                await Task.Delay(TimeSpan.FromMilliseconds(delay));
            }
            catch (Exception ex)
            {
                // 在捕获异常且达到最大重试次数时记录错误日志
                IsupLog.Info($"{DateTime.Now} PutUrlAsync 请求失败，已达到最大重试次数 {maxRetryAttempts}。最终错误: {ex.Message} , Josn：{postModel}" + url);

                // 可以根据需要选择抛出异常或返回特定的错误信息
                return "";
            }
        }



    }

}
