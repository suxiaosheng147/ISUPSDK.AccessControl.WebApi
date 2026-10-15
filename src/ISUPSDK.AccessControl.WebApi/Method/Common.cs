using ISUPSDK.AccessControl.WebApi.Services.Logging;
using ISUPSDK.AccessControl.WebApi.Model;
using Newtonsoft.Json;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
namespace ISUPSDK.AccessControl.WebApi
{
    public class Common
    {
        /// <summary>
        /// 获得本机IPv4 IP
        /// </summary>
        /// <returns></returns>
        public static string GetLocalIP()
        {
            try
            {
                string HostName = Dns.GetHostName(); //得到主机名
                IPHostEntry IpEntry = Dns.GetHostEntry(HostName);
                for (int i = 0; i < IpEntry.AddressList.Length; i++)
                {
                    //从IP地址列表中筛选出IPv4类型的IP地址
                    //AddressFamily.InterNetwork表示此IP为IPv4,
                    //AddressFamily.InterNetworkV6表示此地址为IPv6类型
                    if (IpEntry.AddressList[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        return IpEntry.AddressList[i].ToString();
                    }
                }
                return "";
            }
            catch (Exception)
            {
                return "";
            }
        }
        
        
        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="Adress">指向页</param>
        /// <param name="MachineID">设备编号</param>
        /// <param name="PageSize">页码数量</param>
        /// <returns></returns>
        public static string GetUrl(string Adress, im_machine machine, int PageSize = 0)
        {
            var PageSizes = "";
            string? token = machine.Password;
            var MachineID = machine.Id.ToString();
            Random a = new(DateTime.Now.Millisecond);
            string RandKey = a.Next(10).ToString();
            Dictionary<string, string> sd = new();
            sd.Add("MachineID", MachineID);
            sd.Add("Rand", RandKey);
            sd.Add("Token", token??"");
            if (PageSize > 0)
            {
                PageSizes = "&PageSize=" + PageSize;
                sd.Add("PageSize", PageSize.ToString());
            }
            var Sign = GetSign(sd, token ?? "");
            string url = Adress + "?" +
                "MachineID=" + MachineID +
                "&Rand=" + RandKey +
                "&Sign=" + Sign +
                PageSizes;
            return url;
        }
        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="Adress">指向页</param>
        /// <param name="PageSize">页码数量</param>
        /// <returns></returns>
        public static string GetUrl(string Adress, string MachineSn, int PageSize = 0)
        {
            var PageSizes = "";
            if (PageSize > 0)
            {
                PageSizes = "&PageSize=" + PageSize;
            }
            string url = Adress + "?MachineSn=" + MachineSn +PageSizes;
            return url;
        }
        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="Adress">指向页</param>
        /// <param name="machine">机器数据</param>
        /// <param name="FromQuery"></param>
        /// <returns></returns>
        public static string GetUrl(string Adress, im_machine machine, Dictionary<string, string> FromQuery)
        {
            string FromQueryString = "";
            string? token = machine.Password;
            var MachineID = machine.Id.ToString();
            Random a = new(DateTime.Now.Millisecond);
            string RandKey = a.Next(10).ToString();
            Dictionary<string, string> sd = new Dictionary<string, string>
            {
                { "MachineID", MachineID },
                { "Rand", RandKey },
                { "Token", token??"" }
            };
            foreach (var item in FromQuery)
            {
                FromQueryString += "&" + item.Key + "=" + item.Value;
                sd.Add(item.Key, item.Value);

            }
            var Sign = GetSign(sd, token ?? "");
            string url = Adress + "?" +
                "MachineID=" + MachineID +
                "&Rand=" + RandKey +
                "&Sign=" + Sign +
                FromQueryString;
            return url;
        }
        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="Adress">指向页</param>
        /// <param name="DataType">数据类型，1：幼儿园，2：班级，3：幼儿，4：家长，5：机器，6：门禁，7：大屏通知，8：职工，9：报读机</param>
        /// <returns></returns>
        public static string GetReturnUrl(string Adress, im_machine machine, string? DataId, int DataType)
        {
            string? token = machine.Password;
            Random a = new(DateTime.Now.Millisecond);
            string RandKey = a.Next(10).ToString();
            var MachineID = machine.Id.ToString();
            Dictionary<string, string> sd = new()
            {
                { "MachineID", MachineID },
                { "Rand", RandKey }
            };
            if (!string.IsNullOrEmpty(DataId))
                sd.Add("DataId", DataId);
            sd.Add("DataType", DataType.ToString());
            sd.Add("Token", token??"");
            var Sign = Common.GetSign(sd, token ?? "");
            string url = Adress + "?" +
                "MachineID=" + MachineID +
                "&Rand=" + RandKey +
                (string.IsNullOrWhiteSpace(DataId) ? string.Empty : ("&DataId=" + DataId)) +
                "&DataType=" + DataType.ToString() +
                "&Sign=" + Sign;
            return url;
        }
        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="Adress">指向页</param>
        /// <param name="DataType">数据类型，1：幼儿园，2：班级，3：幼儿，4：家长，5：机器，6：门禁，7：大屏通知，8：职工，9：报读机</param>
        /// <returns></returns>
        public static string GetReturnUrl(string Adress, string MachineSn, string? DataId, int DataType)
        {
            string url = Adress + "?" +
                "MachineSn=" + MachineSn +
                (string.IsNullOrWhiteSpace(DataId) ? string.Empty : ("&DataId=" + DataId)) +
                "&DataType=" + DataType.ToString(); 
                
            return url;
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
        /// 获取签名
        /// </summary>
        /// <param name="sd">参数数组，key为参数名，value为can书值</param>
        /// <param name="token">保留的token值</param>
        /// <returns></returns>
        public static string GetSign(Dictionary<string, string> sd, string token)
        {
            string originalString = "";
            SortedDictionary<string, string> sdCal = new SortedDictionary<string, string>();

            foreach (KeyValuePair<string, string> kvp in sd)
            {
                string key = kvp.Key;
                string value = kvp.Value;

                if (key.ToLower() != "token" && key.ToLower() != "sign" && value != null)
                    sdCal.Add(key, value);
            }

            sdCal.Add("Token", token);

            foreach (KeyValuePair<string, string> kvp in sdCal)
            {
                string key = kvp.Key;
                string value = kvp.Value;

                originalString += "&" + key + "=" + value;
            }

            if (!string.IsNullOrEmpty(originalString))
                originalString = originalString.Substring(1);

            return Encrypt32(originalString, Encoding.UTF8);
        }
        /// <summary>
        /// 32位MD5加密
        /// </summary>
        /// <param name="input">需要加密的字符串</param>
        /// <param name="encoding">编码格式</param>
        /// <returns>MD5加密后的字符串（32位）</returns>
        public static string Encrypt32(string input, Encoding? encoding = null)
        {
            StringBuilder sb = new();
            using (MD5 md5 = MD5.Create())
            {
                if (encoding == null)
                {
                    encoding = Encoding.UTF8;
                }
                byte[] buff = md5.ComputeHash(encoding.GetBytes(input));
                foreach (byte t in buff)
                {
                    sb.AppendFormat("{0:x2}", t);
                }
            }
            return sb.ToString();
        }
        
        

      

        /// <summary>
        /// 拼接url
        /// </summary>
        /// <param name="localIP"></param>
        /// <param name="Adress"></param>
        /// <param name="sn"></param>
        /// <param name="ip"></param>
        /// <param name="port"></param>
        /// <param name="token"></param>
        /// <param name="Mid"></param>
        /// <param name="HKLoginId"></param>
        /// <param name="HKLoginTime"></param>
        /// <param name="HKLogoutTime"></param>
        /// <param name="action"></param>
        /// <param name="TempLoginCount"></param>   
        /// <returns></returns>
        public static string SplicingUrl(string Adress, string sn, string ip, string port, string token, int Mid, int HKLoginId, DateTime? HKLoginTime, DateTime? HKLogoutTime, int action, int TempLoginCount)
        {
            Random a = new(DateTime.Now.Millisecond);
            _ = a.Next(10).ToString();
            string url = Adress + "?MachineSn=" + sn +
                "&CompanyID=11&Token=" + token + "&Ip=" + ip + "&Port=" + port + "&Mid=" + Mid + "&HKLoginId=" + HKLoginId + "&HKLoginTime=" + HKLoginTime + "&HKLogoutTime=" + HKLogoutTime + "&action=" + action + "&TempLoginCount=" + TempLoginCount;
            return url;
        }
        private readonly static object _Sync = new();

        /// <summary>日志根目录，默认程序目录；由宿主按 ISUP:LogDirectory 覆盖。</summary>
        public static string LogRootDirectory
        {
            get => IsupLog.LogRootDirectory;
            set => IsupLog.LogRootDirectory = value;
            }

        /// 写TXT文件
        public static void WriteFile(string fileName, bool isAppend, string fileContent)
            => IsupLog.Write(fileName, isAppend, fileContent);

        public static void WriteFileHours(string fileName, bool isAppend, string fileContent)
            => IsupLog.WriteHours(fileName, isAppend, fileContent);

        /// <summary>
        /// 获取签名
        /// </summary>
        /// <param name="nonce">随机字符串</param>
        /// <param name="timestamp">时间戳</param>
        /// <param name="timestamp">DES解密字符串</param>
        /// <returns></returns>
        public static string GetSign(string nonce, long timestamp, string checkcode)
        {
            //NoWutF8ssoMGUS0RqL8MOP

            return Encrypt32($"nonce={nonce}&timestamp={timestamp}&checkcode={checkcode}");

        }

        
        /// <summary>
        /// 16位MD5加密
        /// </summary>
        /// <param name="input">需要加密的字符串</param>
        /// <param name="encoding">编码格式</param>
        /// <returns>MD5加密后的字符串（16位）</returns>
        public static string Encrypt16(string input, Encoding? encoding = null)
        {
            StringBuilder sb = new StringBuilder();
            using (MD5 md5 = MD5.Create())
            {
                if (encoding == null)
                {
                    encoding = Encoding.UTF8;
                }
                byte[] buff = md5.ComputeHash(encoding.GetBytes(input));
                foreach (byte t in buff)
                {
                    sb.AppendFormat("{0:x2}", t);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// DES加密字符串
        /// </summary>
        /// <param name="input">待加密的字符串</param>
        /// <param name="encryptKey">8位加密密钥</param>
        /// <returns>加密后的字符串</returns>
        public static string Encrypt(string input, string encryptKey)
        {
            try
            {
                byte[] rgbKey = Encoding.UTF8.GetBytes(encryptKey.Substring(0, 8));
                byte[] rgbIV = _keys;
                string str = Encoding.Default.GetString(rgbIV);
                byte[] inputByteArray = Encoding.UTF8.GetBytes(input);
                DES dCSP = DES.Create();
                using var ms = new MemoryStream();
                var cStream = new CryptoStream(ms, dCSP.CreateEncryptor(rgbKey, rgbIV), CryptoStreamMode.Write);
                cStream.Write(inputByteArray, 0, inputByteArray.Length);
                cStream.FlushFinalBlock();
                return Convert.ToBase64String(ms.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
        //默认密钥向量
        private static byte[] _keys = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
        /// <summary>
        /// DES解密字符串
        /// </summary>
        /// <param name="input">待解密的字符串</param>
        /// <param name="decryptKey">8位解密密钥（同加密密钥）</param>
        /// <returns>原字符串</returns>
        public static string Decrypt(string input, string decryptKey)
        {
            try
            {
                
                byte[] rgbKey = Encoding.UTF8.GetBytes(decryptKey);
                byte[] rgbIV = _keys;
                byte[] inputByteArray = Convert.FromBase64String(input);
                DES DCSP = DES.Create();
                MemoryStream ms = new();
                CryptoStream cStream = new(ms, DCSP.CreateDecryptor(rgbKey, rgbIV), CryptoStreamMode.Write);
                cStream.Write(inputByteArray, 0, inputByteArray.Length);
                cStream.FlushFinalBlock();
                return Encoding.UTF8.GetString(ms.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
        private static DateTime StartTime = new DateTime(1970, 1, 1) + TimeZoneInfo.Local.BaseUtcOffset;
        /// <summary>
        /// 获取指定时间为终止值的10位时间戳
        /// </summary>
        /// <param name="dt">终止时间</param>
        /// <returns>时间戳</returns>
        public static long GetTimeStamp(DateTime dt)
        {
            TimeSpan ts = dt - StartTime;
            return (long)ts.TotalSeconds;
        }

        /// <summary>
        /// 获取13位时间戳
        /// </summary>
        /// <returns>时间戳</returns>
        public static long GetLongTimeStamp()
        {
            return GetLongTimeStamp(DateTime.Now);
        }
        /// <summary>
        /// 获取指定时间为终止值的13位时间戳 
        /// </summary>
        /// <param name="dt">终止时间</param>
        /// <returns>时间戳</returns>
        public static long GetLongTimeStamp(DateTime dt)
        {
            TimeSpan ts = dt - StartTime;
            return (long)ts.TotalMilliseconds;
        }
        /// <summary>
        /// 产生随机种子
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static int GetRandomSeed()
        {
            var bytes = new byte[4];
#pragma warning disable SYSLIB0023 // 类型或成员已过时
            var rng = new RNGCryptoServiceProvider();
#pragma warning restore SYSLIB0023 // 类型或成员已过时
            rng.GetBytes(bytes);
            return BitConverter.ToInt32(bytes, 0);
        }
        /// <summary>
        /// 随机字符串
        /// </summary>
        /// <param name="Length">长度</param>
        /// <param name="Type">类型（1：数字，2：数字大写字母，3：数字小写字母，4：数字大写小写字母，5：大写字母，6：小写字母，7：大写小写字母）</param>
        /// <returns></returns>
        public static string RandStr(int Length, int Type)
        {
            var ret = "";

            char[] arrchar1 = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9' };
            char[] arrchar2 = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
            char[] arrchar3 = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            char[] arrchar4 = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            char[] arrchar5 = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z' };
            char[] arrchar6 = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };
            char[] arrchar7 = { 'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z', 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

            char[] character;
            switch (Type)
            {
                case 1:
                    character = arrchar1;
                    break;
                case 2:
                    character = arrchar2;
                    break;
                case 3:
                    character = arrchar3;
                    break;
                case 4:
                    character = arrchar4;
                    break;
                case 5:
                    character = arrchar5;
                    break;
                case 6:
                    character = arrchar6;
                    break;
                case 7:
                    character = arrchar7;
                    break;
                default:
                    character = arrchar1;
                    break;
            }

            var rnd = new Random(GetRandomSeed());

            for (int i = 0; i < Length; i++)
            {
                ret += character[rnd.Next(character.Length)];
            }
            return ret;
        }

        public static string SetToken(string  WebCheckCode1)
        {
            long timestamp = Common.GetLongTimeStamp();
            string randStr = Common.RandStr(5, 2);
            string sign = Common.GetSign(randStr, timestamp, WebCheckCode1);  //获取签名
            string authorizationToken = $"nonce={randStr}&timestamp={timestamp}&sign={sign}";
            return authorizationToken;
        }
        /// <summary>
        /// 获取北京时间（NTP）
        /// </summary>
        /// <param name="ntpServer">NTP 服务器地址</param>
        /// <param name="ntpPort">NTP 服务器端口</param>
        /// <returns>从 NTP 服务器获取的北京时间</returns>
        public static DateTime GetBeijingTime(string ntpServer, int ntpPort = 123)
        {
            try
            {
                using UdpClient udpClient = new();
                udpClient.Client.SendTimeout = 5000;
                udpClient.Client.ReceiveTimeout = 5000;

                // NTP 请求报文
                byte[] ntpData = new byte[48];
                ntpData[0] = 0x1B; // LI=0, Version=3, Mode=3 (Client)

                // 获取服务器IP（支持IPv4/IPv6）
                IPAddress[] addresses = Dns.GetHostAddresses(ntpServer);
                IPAddress address = addresses.FirstOrDefault(ip =>
                    ip.AddressFamily == AddressFamily.InterNetwork ||
                    ip.AddressFamily == AddressFamily.InterNetworkV6
                ) ?? throw new Exception($"无法解析NTP服务器地址: {ntpServer}");

                IPEndPoint ipEndPoint = new(address, ntpPort);
                udpClient.Send(ntpData, ntpData.Length, ipEndPoint);

                byte[] response = udpClient.Receive(ref ipEndPoint);
                if (response.Length < 48)
                    throw new Exception($"NTP响应过短 (需要48字节, 实际收到 {response.Length}字节)");

                // 解析服务器发送时间戳 (位置32-39)
                const byte timeStartIndex = 32;

                // 大端序转换
                uint seconds = (uint)(
                    (response[timeStartIndex] << 24) |
                    (response[timeStartIndex + 1] << 16) |
                    (response[timeStartIndex + 2] << 8) |
                    response[timeStartIndex + 3]
                );

                uint fractions = (uint)(
                    (response[timeStartIndex + 4] << 24) |
                    (response[timeStartIndex + 5] << 16) |
                    (response[timeStartIndex + 6] << 8) |
                    response[timeStartIndex + 7]
                );

                // NTP时间戳转换 (1900年1月1日 -> 1970年1月1日)
                const ulong ntpEpochOffset = 2208988800UL;
                ulong totalSeconds = seconds - ntpEpochOffset;
                double fractionalSeconds = fractions / 4294967296.0; // 2^32

                // 创建UTC时间并转换为北京时间
                DateTime utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(totalSeconds)
                    .AddSeconds(fractionalSeconds);

                // 转换为北京时间 (UTC+8)
                DateTime beijingTime = utcTime.AddHours(8);
                IsupLog.Info("{0}", beijingTime);
                return beijingTime;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取北京时间失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步获取北京时间（NTP）
        /// </summary>
        /// <param name="ntpServer">NTP 服务器地址</param>
        /// <param name="ntpPort">NTP 服务器端口</param>
        /// <returns>从 NTP 服务器获取的北京时间（yyyy-MM-ddTHH:mm:ss）</returns>
        public static async Task<string> GetBeijingTimeAsync(string ntpServer, int ntpPort = 123)
        {
            UdpClient? udpClient = null;
            try
            {
                // 异步解析DNS - 获取所有IP地址
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(ntpServer);
                if (addresses == null || addresses.Length == 0)
                {
                    throw new Exception($"无法解析NTP服务器地址: {ntpServer}");
                }

                // 按优先级排序：IPv4优先
                var sortedAddresses = addresses
                    .OrderBy(ip => ip.AddressFamily == AddressFamily.InterNetwork ? 0 : 1)
                    .ToArray();

                byte[]? response = null;
                Exception? lastException = null;

                // 尝试所有可用地址
                foreach (var address in sortedAddresses)
                {
                    try
                    {
                        // 根据地址族创建正确的UDP客户端
                        udpClient = address.AddressFamily == AddressFamily.InterNetworkV6
                            ? new UdpClient(AddressFamily.InterNetworkV6)
                            : new UdpClient(AddressFamily.InterNetwork);

                        udpClient.Client.SendTimeout = 5000;
                        udpClient.Client.ReceiveTimeout = 5000;

                        IPEndPoint ipEndPoint = new(address, ntpPort);

                        // NTP 请求报文
                        byte[] ntpData = new byte[48];
                        ntpData[0] = 0x1B; // LI=0, Version=3, Mode=3 (Client)

                        // 发送请求
                        await udpClient.SendAsync(ntpData, ntpData.Length, ipEndPoint);

                        // 设置接收超时
                        var receiveTask = udpClient.ReceiveAsync();
                        var timeoutTask = Task.Delay(5000);
                        var completedTask = await Task.WhenAny(receiveTask, timeoutTask);

                        if (completedTask == timeoutTask)
                        {
                            throw new TimeoutException("接收NTP响应超时");
                        }

                        response = (await receiveTask).Buffer;
                        if (response.Length >= 48)
                        {
                            lastException = null; // 清除之前可能的异常
                            break; // 成功获取响应，退出循环
                        }
                    }
                    catch (Exception ex)
                    {
                        // 记录异常但继续尝试其他地址
                        lastException = ex;
                        udpClient?.Dispose();
                        udpClient = null;
                    }
                }

                // 检查是否成功获取响应
                if (response == null || response.Length < 48)
                {
                    throw null as Exception ?? new Exception("所有NTP服务器地址尝试失败");
                }

                // 解析服务器发送时间戳 (位置32-39)
                const byte timeStartIndex = 32;

                // 大端序转换
                uint seconds = (uint)(
                    (response[timeStartIndex] << 24) |
                    (response[timeStartIndex + 1] << 16) |
                    (response[timeStartIndex + 2] << 8) |
                    response[timeStartIndex + 3]
                );

                uint fractions = (uint)(
                    (response[timeStartIndex + 4] << 24) |
                    (response[timeStartIndex + 5] << 16) |
                    (response[timeStartIndex + 6] << 8) |
                    response[timeStartIndex + 7]
                );

                // NTP时间戳转换 (1900年1月1日 -> 1970年1月1日)
                const ulong ntpEpochOffset = 2208988800UL;
                ulong totalSeconds = seconds - ntpEpochOffset;
                double fractionalSeconds = fractions / 4294967296.0; // 2^32

                // 创建UTC时间并转换为北京时间
                DateTime utcTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(totalSeconds)
                    .AddSeconds(fractionalSeconds);

                // 转换为北京时间 (UTC+8)
                return utcTime.AddHours(8).ToString("yyyy-MM-ddTHH:mm:ss");
            }
            catch (Exception ex)
            {
                //throw new Exception($"获取北京时间失败: {ex.Message}", ex);
                IsupLog.Info($"获取北京时间失败: {ex}");
                return "";
            }
            finally
            {
                udpClient?.Dispose();
            }
        }

        /// <summary>
        /// 比较两个XML字符串是否相等
        /// </summary>
        /// <param name="xml1"></param>
        /// <param name="xml2"></param>
        /// <returns></returns>
        public static bool AreXmlEqual(string xml1, string xml2)
        {
            try
            {
                XDocument doc1 = XDocument.Parse(xml1);
                XDocument doc2 = XDocument.Parse(xml2);
                return XNode.DeepEquals(doc1, doc2);
            }
            catch (Exception ex)
            {
                // 解析失败时可根据需求返回 false 或抛出异常
                IsupLog.Info($"XML 解析错误: {ex.Message}");
                return false;
            }
        }
    }
}
