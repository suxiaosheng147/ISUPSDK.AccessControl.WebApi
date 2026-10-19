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
        => ExtractAllJson(responseText).FirstOrDefault();

    /// <summary>
    /// 提取响应里<b>所有</b>顶层 JSON 片段。
    /// <para>
    /// multipart 响应可能把一趟查询拆成多个分段返回（例如 <c>FDSearch</c> 每张人脸一个 JSON 分段），
    /// 只取第一段会丢数据；这里返回全部片段，由调用方合并。
    /// 纯 JSON 响应直接返回单元素列表。
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> ExtractAllJson(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return Array.Empty<string>();
        }

        var text = responseText;

        // 纯 JSON 数组（少见）：整体作为一段返回。
        var trimmed = text.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return new[] { text };
        }

        // 以 '{' 开头且"第一个 JSON 块"正好吃掉整段文本时才是纯 JSON；
        // 否则（多段拼接、尾部还有别的分段）继续走多段提取，避免把整段当成一个 JSON 解析失败。
        if (trimmed.StartsWith('{'))
        {
            var singleBrace = text.IndexOf('{');
            var singleEnd = singleBrace < 0 ? -1 : FindBalancedEnd(text, singleBrace);
            if (singleEnd > 0 && string.IsNullOrWhiteSpace(text[(singleEnd + 1)..]))
            {
                return new[] { text };
            }
        }

        var segments = new List<string>();
        var cursor = 0;

        while (cursor < text.Length)
        {
            var firstBrace = text.IndexOf('{', cursor);
            if (firstBrace < 0)
            {
                break;
            }

            var end = FindBalancedEnd(text, firstBrace);
            if (end < 0)
            {
                break;
            }

            segments.Add(text.Substring(firstBrace, end - firstBrace + 1));
            cursor = end + 1;
        }

        return segments;
    }

    /// <summary>找出从 <paramref name="start"/> 处 <c>{</c> 开始、括号配平的结束位置；失败返回 -1。</summary>
    private static int FindBalancedEnd(string text, int start)
    {
        var depth = 0;
        var inString = false;
        var escaped = false;

        // 从第一个 '{' 开始做括号配平，跳过字符串字面量。
        for (var index = start; index < text.Length; index++)
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
                        return index;
                    }

                    break;
            }
        }

        return -1;
    }

    /// <summary>把非 UTF-8 的响应字节按 UTF-8 尽力解码为文本。</summary>
    public static string Decode(byte[] bytes) => Encoding.UTF8.GetString(bytes);
}
