namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 一次 ISAPI multipart/form-data 报文中的分段（对应 SDK 的 <c>OTAP_CMS_PT_MULTIPART_DATA</c>）。
/// 人脸下发典型用法：
/// <list type="number">
/// <item>name="FaceImage"，Content-Type="image/jpeg"，内容为二进制人脸图片；</item>
/// <item>name 任取（SDK 示例为 testname1），Content-Type="application/json"，内容为 JSON 元数据。</item>
/// </list>
/// </summary>
public sealed class IsapiMultipartPart
{
    /// <summary>表单单元名称（name）。</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>表单单元数据名称（filename）。</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>MIME 类型，例如 application/json、image/jpeg。</summary>
    public string ContentType { get; set; } = "application/json";

    /// <summary>分段内容（二进制或 UTF-8 文本）。</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>创建 JSON 文本分段。</summary>
    public static IsapiMultipartPart Json(string name, string json, string fileName = "metadata.json")
        => new()
        {
            Name = name,
            FileName = fileName,
            ContentType = "application/json",
            Content = System.Text.Encoding.UTF8.GetBytes(json),
        };

    /// <summary>创建图片（二进制）分段。</summary>
    public static IsapiMultipartPart Image(string name, byte[] content, string contentType, string fileName)
        => new()
        {
            Name = name,
            FileName = fileName,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "image/jpeg" : contentType,
            Content = content,
        };
}
