using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISAPI 响应归一化：设备可能忽略 <c>?format=json</c> 而返回 XML（尤其是出错时），
/// 或返回 multipart 报文。本类把这些形态统一转换为 JSON 文本，便于后续统一解析。
/// </summary>
public static class IsapiResponseNormalizer
{
    /// <summary>
    /// 把响应文本转换为 JSON；无法转换时返回 null（调用方回退到原始文本）。
    /// </summary>
    public static string? ToJson(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var trimmed = responseText.TrimStart();

        // 以 JSON 开头：截到第一个完整 JSON 值 —— SDK 透传的输出缓冲常在正文后跟垃圾
        // （0x03 结束符，或缓冲区残留的 '1' / '5' / 0xEF 等），JsonDocument / 反序列化会直接报错。
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return TruncateToFirstJsonValue(trimmed);
        }

        // 非 JSON：清掉尾部控制字符后再做 multipart / XML / 纯文本判断。
        var cleaned = TrimTrailingControlChars(responseText);
        trimmed = cleaned.TrimStart();

        // multipart 中的 JSON 分段
        if (trimmed.StartsWith("--", StringComparison.Ordinal) ||
            trimmed.Contains("Content-Disposition", StringComparison.OrdinalIgnoreCase))
        {
            var extracted = IsapiMultipartResponse.ExtractJson(cleaned);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted;
            }
        }

        // XML 响应（成功或失败的 ResponseStatus）
        if (trimmed.StartsWith('<'))
        {
            return XmlToJson(cleaned);
        }

        // 纯文本响应（例如 "OK"）包成对象，避免反序列化报错
        return "{\"rawText\":" + JsonSerializer.Serialize(trimmed) + "}";
    }

    /// <summary>
    /// 去掉响应文本尾部的控制字符（字符码 &lt; 0x20，如 <c>0x03</c> = ETX、<c>0x00</c>）：
    /// 设备侧常在报文结尾跟一个结束符，会让 JSON 解析报
    /// "Additional text encountered after finished reading JSON content"。
    /// </summary>
    public static string TrimTrailingControlChars(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var end = text.Length;
        while (end > 0 && text[end - 1] < ' ')
        {
            end--;
        }

        return end == text.Length ? text : text[..end];
    }

    /// <summary>
    /// 按第一个 NUL 字符截断（读取原生输出缓冲时用）：SDK 可能按"缓冲区大小"返回长度，
    /// 把零填充一起带出来，读成字符串后尾部就是一串 <c>\0</c>。
    /// </summary>
    public static string TruncateAtNul(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var nul = text.IndexOf('\0');
        return nul < 0 ? text : text[..nul];
    }

    /// <summary>
    /// 截出响应文本里的<b>第一个完整 JSON 值</b>（只对以 <c>{</c> / <c>[</c> 开头的文本生效，否则原样返回）。
    /// <para>
    /// SDK 透传的输出缓冲里常残留多余数据，表现为"合法 JSON 后面还跟着垃圾" ——
    /// 日志里的 <c>'1' / '5' / 0xEF is invalid after a single JSON value</c> 就是它。
    /// 只清尾部控制字符救不了（垃圾可能是 <c>1</c>、<c>5</c>、0xEF 这类可见字符），
    /// 所以这里按 JSON 结构读到第一个值结束为止，把后面的内容丢掉。
    /// </para>
    /// </summary>
    public static string TruncateToFirstJsonValue(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var trimmed = text.TrimStart();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
        {
            return text;
        }

        // 只保留到第一个完整 JSON 值：读完顶层 EndObject / EndArray（或顶层标量）即停。
        var bytes = Encoding.UTF8.GetBytes(trimmed);
        var reader = new Utf8JsonReader(bytes);
        var consumed = -1;

        try
        {
            while (reader.Read())
            {
                if (reader.CurrentDepth != 0)
                {
                    continue;
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.EndObject:
                    case JsonTokenType.EndArray:
                    case JsonTokenType.String:
                    case JsonTokenType.Number:
                    case JsonTokenType.True:
                    case JsonTokenType.False:
                    case JsonTokenType.Null:
                        consumed = (int)reader.BytesConsumed;
                        break;
                }

                if (consumed >= 0)
                {
                    break;
                }
            }
        }
        catch (JsonException)
        {
            // 文本本身不是合法 JSON（例如被截断）：原样返回，交给调用方按原样报错。
            return text;
        }

        return consumed <= 0 ? text : Encoding.UTF8.GetString(bytes, 0, consumed);
    }

    /// <summary>
    /// 把 ISAPI 的 XML 响应转换为扁平 JSON 对象（去掉命名空间前缀，便于与 JSON 响应统一解析）。
    /// <code>
    /// &lt;ResponseStatus&gt;&lt;statusCode&gt;1&lt;/statusCode&gt;...&lt;/ResponseStatus&gt;
    /// =&gt; { "statusCode": 1, "statusString": "OK", "subStatusCode": "ok" }
    /// </code>
    /// </summary>
    public static string? XmlToJson(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }

        var root = document.Root;
        if (root is null)
        {
            return null;
        }

        var builder = new StringBuilder();
        builder.Append('{');

        var first = true;
        foreach (var child in root.Elements())
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;

            builder.Append(JsonSerializer.Serialize(child.Name.LocalName));
            builder.Append(':');

            var descendants = child.Elements().ToList();
            if (descendants.Count == 0)
            {
                builder.Append(ToJsonScalar(child.Value));
            }
            else
            {
                builder.Append('{');
                var innerFirst = true;
                foreach (var grandChild in descendants)
                {
                    if (!innerFirst)
                    {
                        builder.Append(',');
                    }

                    innerFirst = false;
                    builder.Append(JsonSerializer.Serialize(grandChild.Name.LocalName));
                    builder.Append(':');
                    builder.Append(ToJsonScalar(grandChild.Value));
                }

                builder.Append('}');
            }
        }

        if (first)
        {
            // 根节点没有子元素：退化为 {"<root>": "<value>"}
            builder.Append(JsonSerializer.Serialize(root.Name.LocalName));
            builder.Append(':');
            builder.Append(ToJsonScalar(root.Value));
        }

        builder.Append('}');
        return builder.ToString();
    }

    /// <summary>把 XML 文本值尽量转换为 JSON 标量（bool / 数字 / 字符串）。</summary>
    private static string ToJsonScalar(string value)
    {
        var text = value ?? string.Empty;
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return "\"\"";
        }

        if (bool.TryParse(trimmed, out var boolean))
        {
            return boolean ? "true" : "false";
        }

        if (long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return integer.ToString(CultureInfo.InvariantCulture);
        }

        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var real))
        {
            return real.ToString(CultureInfo.InvariantCulture);
        }

        return JsonSerializer.Serialize(text);
    }
}
