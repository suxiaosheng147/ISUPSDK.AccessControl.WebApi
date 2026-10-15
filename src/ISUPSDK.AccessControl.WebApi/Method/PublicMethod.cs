using System;
using System.Text;
using System.Text.RegularExpressions;

namespace ISUPSDK.AccessControl.WebApi
{
    /// <summary>
    /// 公共方法
    /// </summary>
    public class PublicMethod
    {

        /// <summary>
        /// 将指定的16进制字符串转换为byte数组
        /// </summary>
        /// <param name="s">16进制字符串(如：“7F 2C 4A”或“7F2C4A”都可以)</param>
        /// <param name="separate">分隔符（默认为空字符）</param>
        /// <returns>16进制字符串对应的byte数组</returns>
        public static byte[] HexStringToByteArray(string s, char separate = '\0')
        {
            s = s.Replace(Convert.ToString(separate), "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }

        /// <summary>
        /// 将一个byte数组转换成一个格式化的16进制字符串
        /// </summary>
        /// <param name="data">byte数组</param>
        /// <param name="separate">分隔符（默认为空字符）</param>
        /// <returns>格式化的16进制字符串</returns>
        public static string ByteArrayToHexString(byte[] data, char separate = '\0')
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
            {
                if (separate == '\0')
                    sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
                else
                {
                    sb.Append(Convert.ToString(b, 16).PadLeft(2, '0').PadLeft(3, separate));
                }
            }
            return sb.ToString().ToLower().Trim();
        }
        /// <summary>
        /// 判断一个字符串是否为url
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static bool IsUrl(string str)
        {
            try
            {
                string Url = @"^http(s)?://";
                return Regex.IsMatch(str, Url);
            }
            catch
            {
                return false;
            }
        }
        /// <summary>
        /// 转换日期
        /// </summary>
        /// <param name="strDateTime">日期字符串，格式：yyyyMMdd或yyyyMMddHHmmss</param>
        public static DateTime ConvertDateTime(string strDateTime)
        {
            if (strDateTime == null || strDateTime.Length != 8 && strDateTime.Length != 14)
                throw new Exception("输入的字符串无效");

            DateTime d = DateTime.MinValue;
            int year = 1;
            int month = 1;
            int day = 1;
            int hour = 0;
            int minute = 0;
            int second = 0;

            year = Convert.ToInt32(strDateTime.Substring(0, 4));
            month = Convert.ToInt32(strDateTime.Substring(4, 2));
            day = Convert.ToInt32(strDateTime.Substring(6, 2));

            if (strDateTime.Length == 14)
            {
                hour = Convert.ToInt32(strDateTime.Substring(8, 2));
                minute = Convert.ToInt32(strDateTime.Substring(10, 2));
                second = Convert.ToInt32(strDateTime.Substring(12, 2));
            }

            d = new DateTime(year, month, day, hour, minute, second);

            return d;
        }


    }
}
