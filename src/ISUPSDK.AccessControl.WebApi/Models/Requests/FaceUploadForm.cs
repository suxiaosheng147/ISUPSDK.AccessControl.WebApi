using Microsoft.AspNetCore.Mvc;

namespace ISUPSDK.AccessControl.WebApi.Models.Requests;

/// <summary>
/// 人脸图片上传表单（multipart/form-data）。
/// 使用单一绑定模型而不是 [FromForm] IFormFile，以便 OpenAPI/Swagger 正确生成
/// <c>multipart/form-data</c> 请求体（binary 属性）。
/// </summary>
public sealed class FaceUploadForm
{
    /// <summary>人脸图片文件（必填，JPEG，建议 480×480 以上、单张 ≤ 200KB）。</summary>
    [FromForm(Name = "image")]
    public IFormFile? Image { get; set; }

    /// <summary>人脸图片 ID，通常使用人员工号（必填）。</summary>
    [FromForm(Name = "fpId")]
    public string FpId { get; set; } = string.Empty;

    /// <summary>人脸库 ID，默认 1。</summary>
    [FromForm(Name = "fdId")]
    public string FdId { get; set; } = "1";

    /// <summary>人脸库类型，默认 FDLib。</summary>
    [FromForm(Name = "faceLibType")]
    public string FaceLibType { get; set; } = "blackFD";

    /// <summary>人员姓名（可选）。</summary>
    [FromForm(Name = "name")]
    public string? Name { get; set; }

    /// <summary>人员工号（可选）。</summary>
    [FromForm(Name = "employeeNo")]
    public string? EmployeeNo { get; set; }

    /// <summary>是否覆盖已有人脸图片，默认 true。</summary>
    [FromForm(Name = "overWrite")]
    public bool OverWrite { get; set; } = true;

    /// <summary>图片分段的表单单元名称，海康门禁要求固定为 img。</summary>
    [FromForm(Name = "imagePartName")]
    public string ImagePartName { get; set; } = "img";

    /// <summary>JSON 分段的表单单元名称，默认 FaceDataRecord。</summary>
    [FromForm(Name = "jsonPartName")]
    public string JsonPartName { get; set; } = "FaceDataRecord";

    /// <summary>透传接收超时（毫秒）。</summary>
    [FromForm(Name = "timeoutMs")]
    public uint TimeoutMs { get; set; } = 15000;
}
