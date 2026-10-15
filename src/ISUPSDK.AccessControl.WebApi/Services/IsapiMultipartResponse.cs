using System.Text;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISAPI multipart 响应解析工具。
///
/// 部分接口（例如 <c>FDSearch</c>）即使请求时指定了 <c>?format=json</c>，
/// 仍以 <c>multipart/form-data</c> 响应，业务 JSON 位于某个分段内。
/// 本工具从原始响应文本中提取出 JSON 片段。
/// </summary>
public static class IsapiMultipartResponse
{
    /// <summary>
    /// 若响应文本为 multipart 报文或包含 JSON 片段，则返回其中的 JSON 文本；否则返回 null。
    /// </summary>
    public static string? ExtractJson(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        var text = responseText;

        // 已经是纯 JSON
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return text;
        }

        // multipart：取第一个 '{' 到最后一个 '}' 之间（含 JSON 分段）
        var firstBrace = text.IndexOf('{');
        if (firstBrace < 0)
        {
            return null;
        }

        // 从第一个 '{' 开始做括号配平，跳过字符串字面量。
        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var index = firstBrace; index < text.Length; index++)
        {
            var ch = text[index];

            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                }
                else if (ch == '\\')
                {
                    escaped = true;
                }
                else if (ch == '"')
                {
                    inString = false;
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    inString = true;
                    break;
                case '{':
                    depth++;
                    break;
                case '}':
                    depth--;
                    if (depth == 0)
                    {
                        return text.Substring(firstBrace, index - firstBrace + 1);
                    }

                    break;
            }
        }

        return null;
    }

    /// <summary>把非 UTF-8 的响应字节按 UTF-8 尽力解码为文本。</summary>
    public static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
