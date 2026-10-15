using Newtonsoft.Json;
using System.Text;


namespace HKEDUBasicDataSync.Methods
{
    public static class JsonHepler
    {
        /// <summary>
        /// 序列化对象为Json字符串
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">转换对象</param>
        /// <returns>json字符串</returns>
        public static string ToJson<T>(this T value)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore // 忽略 null 值
            };

            return JsonConvert.SerializeObject(value, settings);
        }
        /// <summary>
        /// 序列化Json字符串为对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="value">json字符串</param>
        /// <returns>对象</returns>
        public static T FromJson<T>(this string value)
        {
            return SerializationHelper.DeserializeFromJson<T>(value);
        }
    }

    /// <summary>
    /// 帮助类：序列化/反序列化
    /// </summary>
    public static class SerializationHelper
    {
        /// <summary>
        /// 序列化对象为JSON格式，格式化为垂直对齐
        /// </summary>
        /// <param name="obj">对象</param>
        /// <param name="dateFormat">日期序列化格式</param>
        /// <param name="ignoreNull">是否忽略NULL值</param>
        /// <returns>Json字符串</returns>
        public static string SerializeToJson(object obj, string dateFormat = "yyyy-MM-dd HH:mm:ss", bool ignoreNull = false)
        {
            var json = JsonConvert.SerializeObject(obj, Formatting.None, new JsonSerializerSettings
            {
                DateFormatString = dateFormat,
                NullValueHandling = ignoreNull ? NullValueHandling.Ignore : NullValueHandling.Include
            });

            return FormatJson(json);
        }
        /// <summary>
        /// 反序列化JSON对象
        /// </summary>
        /// <typeparam name="T">对象类型</typeparam>
        /// <param name="json">json字符串</param>
        /// <returns>json对象</returns>
        public static T DeserializeFromJson<T>(string json)
        {
#pragma warning disable CS8603 // 可能返回 null 引用。
            return JsonConvert.DeserializeObject<T>(json);
#pragma warning restore CS8603 // 可能返回 null 引用。
        }

        /// <summary>
        /// 格式化 JSON 字符串为垂直对齐
        /// </summary>
        /// <param name="json">原始 JSON 字符串</param>
        /// <returns>格式化后的 JSON 字符串</returns>
        private static string FormatJson(string json)
        {
            var result = new StringBuilder();
            var indent = 0;
            var inString = false;

            foreach (var character in json)
            {
                if (character == '"')
                {
                    inString = !inString;
                }

                if (!inString)
                {
                    if (character == '{' || character == '[')
                    {
                        result.Append(character);
                        result.AppendLine();
                        indent++;
                        result.Append(new string(' ', indent * 4)); // 4个空格缩进
                    }
                    else if (character == '}' || character == ']')
                    {
                        result.AppendLine();
                        indent--;
                        result.Append(new string(' ', indent * 4)); // 4个空格缩进
                        result.Append(character);
                    }
                    else if (character == ',')
                    {
                        result.Append(character);
                        result.AppendLine();
                        result.Append(new string(' ', indent * 4)); // 4个空格缩进
                    }
                    else
                    {
                        result.Append(character);
                    }
                }
                else
                {
                    result.Append(character);
                }
            }

            return result.ToString();
        }
    }



}
