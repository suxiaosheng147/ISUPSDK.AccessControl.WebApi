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

        // 已经是 JSON
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return responseText;
        }

        // multipart 中的 JSON 分段
        if (trimmed.StartsWith("--", StringComparison.Ordinal) ||
            trimmed.Contains("Content-Disposition", StringComparison.OrdinalIgnoreCase))
        {
            var extracted = IsapiMultipartResponse.ExtractJson(responseText);
            if (!string.IsNullOrWhiteSpace(extracted))
            {
                return extracted;
            }
        }

        // XML 响应（成功或失败的 ResponseStatus）
        if (trimmed.StartsWith('<'))
        {
            return XmlToJson(responseText);
        }

        // 纯文本响应（例如 "OK"）包成对象，避免反序列化报错
        return "{\"rawText\":" + JsonSerializer.Serialize(trimmed) + "}";
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
