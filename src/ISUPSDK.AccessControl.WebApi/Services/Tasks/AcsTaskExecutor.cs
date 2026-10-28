using System.Text.Json;
using ISUPSDK.AccessControl.WebApi.Data;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Models.Requests;
using ISUPSDK.AccessControl.WebApi.Services;

namespace ISUPSDK.AccessControl.WebApi.Services.Tasks;

/// <summary>任务执行结果分类。</summary>
public enum AcsTaskExecutionOutcome
{
    /// <summary>设备接受了操作。</summary>
    Success,

    /// <summary>设备明确拒绝（参数/业务错误），不再重试。</summary>
    DeviceFailed,

    /// <summary>临时性错误（网络/SDK 超时等），可重试。</summary>
    TemporaryError,

    /// <summary>设备离线或未注册，等待设备上线后再执行。</summary>
    DeviceUnavailable,
}

/// <summary>一次任务执行的结果。</summary>
public sealed record AcsTaskExecutionResult(
    AcsTaskExecutionOutcome Outcome,
    string? Error = null,
    IsapiOperationResult? Result = null);

/// <summary>
/// 异步任务执行器：把入队时保存的请求载荷，按 <see cref="AcsTaskOperation"/>
/// 分派到 <see cref="IsupIsapiService"/> 的对应下发方法。
/// </summary>
public sealed class AcsTaskExecutor
{
    private readonly IsupCmsService _cms;
    private readonly IsupIsapiService _accessControl;
    private readonly ILogger<AcsTaskExecutor> _logger;

    public AcsTaskExecutor(
        IsupCmsService cms,
        IsupIsapiService accessControl,
        ILogger<AcsTaskExecutor> logger)
    {
        _cms = cms;
        _accessControl = accessControl;
        _logger = logger;
    }

    public async Task<AcsTaskExecutionResult> ExecuteAsync(AcsTask task, CancellationToken ct)
    {
        if (!_cms.TryGetDeviceByDeviceId(task.DeviceId, out var device))
        {
            return new AcsTaskExecutionResult(AcsTaskExecutionOutcome.DeviceUnavailable, $"设备 {task.DeviceId} 未注册");
        }

        if (!device.Online)
        {
            return new AcsTaskExecutionResult(AcsTaskExecutionOutcome.DeviceUnavailable, $"设备 {task.DeviceId} 离线");
        }

        try
        {
            var result = await DispatchAsync(task, device, ct).ConfigureAwait(false);

            if (result is null)
            {
                return new AcsTaskExecutionResult(AcsTaskExecutionOutcome.Success);
            }

            return result.DeviceSuccess
                ? new AcsTaskExecutionResult(AcsTaskExecutionOutcome.Success, Result: result)
                : new AcsTaskExecutionResult(
                    AcsTaskExecutionOutcome.DeviceFailed,
                    result.StepError ?? result.Status?.SubStatusCode ?? result.SdkErrorText ?? "设备拒绝",
                    result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IsupApiException ex)
        {
            return new AcsTaskExecutionResult(AcsTaskExecutionOutcome.TemporaryError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "任务 {Id} 执行异常：{Op}", task.Id, task.Operation);
            return new AcsTaskExecutionResult(AcsTaskExecutionOutcome.TemporaryError, ex.Message);
        }
    }

    private Task<IsapiOperationResult?> DispatchAsync(AcsTask task, IsupDevice device, CancellationToken ct)
    {
        var timeout = task.TimeoutMs == 0 ? 15000u : task.TimeoutMs;

        switch (task.Operation)
        {
            // ---------------------------------------------------------------- 人员
            case AcsTaskOperation.PersonAdd:
                return WrapAsync(_accessControl.AddUserAsync(device, Deserialize<IsapiUserInfo>(task), timeout, ct));

            case AcsTaskOperation.PersonUpdate:
                return WrapAsync(_accessControl.UpdateUserAsync(device, Deserialize<IsapiUserInfo>(task), timeout, ct));

            case AcsTaskOperation.PersonSetUp:
                return WrapAsync(_accessControl.SetUpUserAsync(device, Deserialize<IsapiUserInfo>(task), timeout, ct));

            case AcsTaskOperation.PersonBatchAdd:
            case AcsTaskOperation.PersonBatchUpdate:
            {
                var request = Deserialize<UserBatchRequest>(task);
                var update = task.Operation == AcsTaskOperation.PersonBatchUpdate || request.Update;
                return WrapAsync(_accessControl.UpsertUsersAsync(device, request.Users, update, request.TimeoutMs, ct));
            }

            case AcsTaskOperation.PersonDelete:
                return WrapAsync(_accessControl.DeleteUsersAsync(device, Deserialize<string[]>(task), timeout, ct));

            case AcsTaskOperation.PersonDeleteAll:
                return WrapAsync(_accessControl.DeleteAllUsersAsync(device, timeout, ct));

            // ---------------------------------------------------------------- 卡片
            case AcsTaskOperation.CardAdd:
                return WrapAsync(_accessControl.AddCardAsync(device, Deserialize<IsapiCardInfo>(task), timeout, ct));

            case AcsTaskOperation.CardUpdate:
                return WrapAsync(_accessControl.UpdateCardAsync(device, Deserialize<IsapiCardInfo>(task), timeout, ct));

            case AcsTaskOperation.CardBatchAdd:
            case AcsTaskOperation.CardBatchUpdate:
            {
                var request = Deserialize<CardBatchRequest>(task);
                var update = task.Operation == AcsTaskOperation.CardBatchUpdate || request.Update;
                return WrapAsync(_accessControl.UpsertCardsAsync(device, request.Cards, update, request.TimeoutMs, ct));
            }

            case AcsTaskOperation.CardDelete:
                return WrapAsync(_accessControl.DeleteCardsAsync(device, Deserialize<string[]>(task), timeout, ct));

            case AcsTaskOperation.CardDeleteByEmployee:
                return WrapAsync(_accessControl.DeleteCardsByEmployeeAsync(device, Deserialize<string[]>(task), timeout, ct));

            case AcsTaskOperation.CardDeleteAll:
                return WrapAsync(_accessControl.DeleteAllCardsAsync(device, timeout, ct));

            // ---------------------------------------------------------------- 人脸
            case AcsTaskOperation.FaceUpload:
            case AcsTaskOperation.FaceUploadByImage:
            {
                var request = Deserialize<FaceDownloadRequest>(task);
                var bytes = ReadBinary(task);
                return WrapAsync(_accessControl.UploadFaceAsync(device, request, bytes, request.TimeoutMs, ct));
            }

            case AcsTaskOperation.FaceBatchUpload:
                return FaceBatchAsync(task, device, ct);

            case AcsTaskOperation.FaceUploadByUrl:
                return WrapAsync(_accessControl.UploadFaceByUrlAsync(device, Deserialize<IsapiFaceDataRecord>(task), timeout, ct));

            case AcsTaskOperation.FaceDelete:
            {
                var request = Deserialize<FaceDeletePayload>(task);
                return WrapAsync(_accessControl.DeleteFaceAsync(device, request.FpId, request.FdId, request.FaceLibType, timeout, ct));
            }

            case AcsTaskOperation.FaceLibraryCreate:
                return WrapAsync(_accessControl.CreateFaceLibraryAsync(device, Deserialize<IsapiFdLib>(task), timeout, ct));

            case AcsTaskOperation.FaceLibraryUpdate:
                return WrapAsync(_accessControl.UpdateFaceLibraryAsync(device, Deserialize<IsapiFdLib>(task), timeout, ct));

            case AcsTaskOperation.FaceLibraryDelete:
            {
                var request = Deserialize<FaceLibDeletePayload>(task);
                return WrapAsync(_accessControl.DeleteFaceLibraryAsync(device, request.FdId, timeout, ct));
            }

            // ------------------------------------------------- 平台人员同步（复合操作）
            // 设备要求"先有人员，才能下发卡片和人脸"：三步在同一条任务内顺序执行，
            // 人员那一步失败就直接返回，不会继续下发卡/人脸。
            case AcsTaskOperation.PersonIssue:
                return IssuePersonAsync(task, device, ct);

            case AcsTaskOperation.PersonIssueDelete:
                return DeletePersonAsync(task, device, ct);

            default:
                throw new IsupApiException($"未知的任务操作：{task.Operation}");
        }
    }

    private async Task<IsapiOperationResult?> FaceBatchAsync(AcsTask task, IsupDevice device, CancellationToken ct)
    {
        var requests = Deserialize<List<FaceUploadFaceItem>>(task);
        var binaryPaths = DeserializeBinaryList(task);

        IsapiOperationResult? last = null;

        for (var i = 0; i < requests.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var item = requests[i];
            var bytes = i < binaryPaths.Count && File.Exists(binaryPaths[i])
                ? await File.ReadAllBytesAsync(binaryPaths[i], ct).ConfigureAwait(false)
                : Array.Empty<byte>();

            last = await _accessControl
                .UploadFaceAsync(device, item.ToDownloadRequest(), bytes, item.TimeoutMs, ct)
                .ConfigureAwait(false);

            if (!last.DeviceSuccess)
            {
                break;
            }
        }

        return last;
    }

    private static async Task<IsapiOperationResult?> WrapAsync(Task<IsapiOperationResult> operation)
        => await operation.ConfigureAwait(false);

    /// <summary>
    /// 人员下发（复合操作）：人员 →（删旧卡）→ 卡 → 人脸，<b>严格顺序</b>。
    /// 任何一步设备没接受就立即返回，后面的步骤一律不下发（设备要求"先有人员，才能下发卡片和人脸"）。
    /// </summary>
    private async Task<IsapiOperationResult?> IssuePersonAsync(AcsTask task, IsupDevice device, CancellationToken ct)
    {
        var payload = Deserialize<PersonIssuePayload>(task);
        var timeout = task.TimeoutMs == 0 ? 15000u : task.TimeoutMs;

        if (payload.Person is null)
        {
            throw new IsupApiException($"任务 {task.Id}（{task.Operation}）缺少人员信息。");
        }

        // 第一步：人员。没成功就到此为止。
        ct.ThrowIfCancellationRequested();
        var result = await _accessControl
            .SetUpUserAsync(device, payload.Person, timeout, ct)
            .ConfigureAwait(false);
        if (!result.DeviceSuccess)
        {
            return WithStep(result, "人员信息");
        }

        // 第二步：换卡时先把旧卡覆盖掉（CardInfo/SetUp + deleteCard=true，与平台同步下发的报文一致）
        if (!string.IsNullOrWhiteSpace(payload.OldCardNo))
        {
            ct.ThrowIfCancellationRequested();
            result = await _accessControl
                .SetUpCardAsync(
                    device,
                    new IsapiCardInfo
                    {
                        CardNo = payload.OldCardNo!,
                        EmployeeNo = payload.EmployeeNo,
                        CardType = "normalCard",
                        DeleteCard = true,
                    },
                    timeout,
                    ct)
                .ConfigureAwait(false);
            if (!result.DeviceSuccess)
            {
                return WithStep(result, "删除旧卡");
            }
        }

        // 第三步：卡（走 CardInfo/SetUp，报文与平台同步下发一致）
        if (payload.Card is not null)
        {
            ct.ThrowIfCancellationRequested();
            result = await _accessControl
                .SetUpCardAsync(device, payload.Card, timeout, ct)
                .ConfigureAwait(false);
            if (!result.DeviceSuccess)
            {
                return WithStep(result, "卡片");
            }
        }

        // 第四步：人脸（有照片就下发；没有照片就把设备上的旧人脸清掉）
        if (payload.Face is not null)
        {
            ct.ThrowIfCancellationRequested();
            result = await _accessControl
                .UploadFaceByUrlAsync(device, payload.Face, timeout, ct)
                .ConfigureAwait(false);
            if (!result.DeviceSuccess)
            {
                return WithStep(result, "人脸");
            }
        }
        else if (!string.IsNullOrWhiteSpace(payload.DeleteFaceId))
        {
            ct.ThrowIfCancellationRequested();
            result = await _accessControl
                .DeleteFaceAsync(device, payload.DeleteFaceId!, "1", "blackFD", timeout, ct)
                .ConfigureAwait(false);
            if (!result.DeviceSuccess)
            {
                return WithStep(result, "删除旧人脸");
            }
        }

        return result;
    }

    /// <summary>人员删除（复合操作）：删人员 → 删人脸。</summary>
    private async Task<IsapiOperationResult?> DeletePersonAsync(AcsTask task, IsupDevice device, CancellationToken ct)
    {
        var payload = Deserialize<PersonIssuePayload>(task);
        var timeout = task.TimeoutMs == 0 ? 15000u : task.TimeoutMs;
        var employeeNo = string.IsNullOrWhiteSpace(payload.EmployeeNo) ? task.DeviceId : payload.EmployeeNo;
        var deleteFaceId = string.IsNullOrWhiteSpace(payload.DeleteFaceId) ? employeeNo : payload.DeleteFaceId!;

        ct.ThrowIfCancellationRequested();
        var result = await _accessControl
            .DeleteUsersAsync(device, [employeeNo], timeout, ct)
            .ConfigureAwait(false);
        if (!result.DeviceSuccess)
        {
            return WithStep(result, "删除人员");
        }

        ct.ThrowIfCancellationRequested();
        result = await _accessControl
            .DeleteFaceAsync(device, deleteFaceId, "1", "blackFD", timeout, ct)
            .ConfigureAwait(false);
        return result.DeviceSuccess ? result : WithStep(result, "删除人脸");
    }

    /// <summary>给失败结果打上"哪一步失败了"，供任务错误信息展示。</summary>
    private static IsapiOperationResult WithStep(IsapiOperationResult result, string step)
    {
        var subStatus = result.Status?.SubStatusCode;
        result.StepError = string.IsNullOrWhiteSpace(subStatus)
            ? $"{step}下发失败"
            : $"{step}下发失败（subStatusCode={subStatus}）";
        return result;
    }

    private static T Deserialize<T>(AcsTask task)
    {
        if (string.IsNullOrWhiteSpace(task.Payload))
        {
            throw new IsupApiException($"任务 {task.Id}（{task.Operation}）缺少请求载荷。");
        }

        return JsonSerializer.Deserialize<T>(task.Payload, IsupJson.Options)
               ?? throw new IsupApiException($"任务 {task.Id}（{task.Operation}）请求载荷反序列化失败。");
    }

    private static byte[] ReadBinary(AcsTask task)
    {
        if (string.IsNullOrWhiteSpace(task.BinaryPath) || !File.Exists(task.BinaryPath))
        {
            throw new IsupApiException($"任务 {task.Id} 的人脸图片文件不存在：{task.BinaryPath}");
        }

        return File.ReadAllBytes(task.BinaryPath);
    }

    private static List<string> DeserializeBinaryList(AcsTask task)
    {
        if (string.IsNullOrWhiteSpace(task.BinaryPath))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(task.BinaryPath, IsupJson.Options) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string> { task.BinaryPath };
        }
    }
}

/// <summary>人脸删除任务载荷。</summary>
public sealed class FaceDeletePayload
{
    public string FpId { get; set; } = string.Empty;

    public string FdId { get; set; } = "1";

    public string FaceLibType { get; set; } = "blackFD";
}

/// <summary>人脸库删除任务载荷。</summary>
public sealed class FaceLibDeletePayload
{
    public string? FdId { get; set; }
}

/// <summary>批量人脸上传任务项（含 Base64/URL，执行时转 multipart）。</summary>
public sealed class FaceUploadFaceItem
{
    public string FpId { get; set; } = string.Empty;

    public string FdId { get; set; } = "1";

    public string FaceLibType { get; set; } = "blackFD";

    public string? Name { get; set; }

    public string? EmployeeNo { get; set; }

    public bool OverWrite { get; set; } = true;

    public uint TimeoutMs { get; set; } = 15000;

    public FaceDownloadRequest ToDownloadRequest() => new()
    {
        FpId = FpId,
        FdId = FdId,
        FaceLibType = FaceLibType,
        Name = Name,
        EmployeeNo = EmployeeNo,
        OverWrite = OverWrite,
        ContentType = "image/jpeg",
        FileName = $"{FpId}.jpg",
        TimeoutMs = TimeoutMs,
    };
}
