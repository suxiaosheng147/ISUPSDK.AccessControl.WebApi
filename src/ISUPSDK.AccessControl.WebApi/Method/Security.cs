using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ISUPSDK.AccessControl.WebApi
{
    /// <summary>
    /// 加密解密
    /// </summary>
    public class Security
    {
        /// <summary>
        /// 获取签名
        /// </summary>
        /// <param name="sortedDictionary">数据词典</param>
        /// <param name="keyName">key名称</param>
        /// <param name="key">验证码</param>
        /// <returns></returns>
        public static string GetSign(SortedDictionary<string, string> sortedDictionary, string keyName, string key)
        {
            string strUrl = "";
            foreach (KeyValuePair<string, string> pair in sortedDictionary)
                strUrl += "&" + pair.Key + "=" + pair.Value;

            strUrl += "&" + keyName + "=" + key;

            if (!string.IsNullOrEmpty(strUrl))
                strUrl = strUrl.Substring(1);

            string strSign = Common.Encrypt32(strUrl, Encoding.UTF8);
            return strSign;
        }

        /// <summary>
        /// 获取 AES 设置
        /// </summary>
        /// <param name="key">密文</param>
        /// <returns></returns>
#pragma warning disable SYSLIB0022 // 类型或成员已过时
        private static RijndaelManaged GetAESSetting(string key)
#pragma warning restore SYSLIB0022 // 类型或成员已过时
        {
#pragma warning disable SYSLIB0022 // 类型或成员已过时
            return new RijndaelManaged
            {
                Key = Encoding.UTF8.GetBytes(key),
                Mode = CipherMode.ECB,
                Padding = PaddingMode.Zeros,
                BlockSize = 128
            };
#pragma warning restore SYSLIB0022 // 类型或成员已过时
        }
        /// <summary>
        ///  AES 加密
        /// </summary>
        /// <param name="str">明文（待加密）</param>
        /// <param name="key">密文</param>
        /// <returns></returns>
        public static string? AESEncrypt(string str, string key)
        {
            if (string.IsNullOrEmpty(str)) return null;
            byte[] toEncryptArray = Encoding.UTF8.GetBytes(str);

#pragma warning disable SYSLIB0022 // 类型或成员已过时
            RijndaelManaged rm = GetAESSetting(key);
#pragma warning restore SYSLIB0022 // 类型或成员已过时

            ICryptoTransform cTransform = rm.CreateEncryptor();
            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);

            return PublicMethod.ByteArrayToHexString(resultArray);
        }
        /// <summary>
        ///  AES 解密
        /// </summary>
        /// <param name="str">明文（待解密）</param>
        /// <param name="key">密文</param>
        /// <returns></returns>
        public static string? AESDecrypt(string? str, string? key)
        {
            if (string.IsNullOrEmpty(str))
                return null;

            byte[] toEncryptArray = PublicMethod.HexStringToByteArray(str);

#pragma warning disable SYSLIB0022 // 类型或成员已过时
            RijndaelManaged rm = GetAESSetting(key);
#pragma warning restore SYSLIB0022 // 类型或成员已过时

            ICryptoTransform cTransform = rm.CreateDecryptor();

            byte[] resultArray = cTransform.TransformFinalBlock(toEncryptArray, 0, toEncryptArray.Length);
            List<byte> listByte = resultArray.ToList();
            for (int i = resultArray.Length - 1; i >= 0; i--)
            {
                if (resultArray[i] == 0x00)
                    listByte.RemoveAt(i);
                else
                    break;
            }

            return Encoding.UTF8.GetString(listByte.ToArray());
        }
    }
}
