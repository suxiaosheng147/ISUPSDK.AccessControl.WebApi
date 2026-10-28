using System.Text.Json;
using HKEDUBasicDataSync.Methods;
using ISUPSDK.AccessControl.WebApi.Model;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Services.Logging;

namespace ISUPSDK.AccessControl.WebApi.Data;

/// <summary>一条人员操作对应的下发任务（操作类型 + 载荷 + 描述）。</summary>
public sealed record PersonIssueBuildResult(AcsTaskOperation Operation, PersonIssuePayload Payload, string Description);

/// <summary>
/// 把平台下发的人员数据（<see cref="Detail"/> / <see cref="SyncPersonDataPage"/>）转成一条
/// <see cref="AcsTaskOperation.PersonIssue"/>（或 <see cref="AcsTaskOperation.PersonIssueDelete"/>）任务载荷。
/// <para>
/// 报文口径与原来的 SDK 下发一致（人员用 <c>UserInfo/SetUp</c> 语义、卡用 <c>CardInfo</c>、
/// 人脸用 <c>FDLib</c> 的 faceURL 方式），只是把"每一步的报文"放进同一个载荷，
/// 由执行器按 人员 → 卡 → 人脸 的顺序发送。
/// </para>
/// </summary>
public static class PersonOperationIssueBuilder
{
    /// <summary>按任务构造下发载荷；数据解析不出来时返回 null。</summary>
    public static async Task<PersonIssueBuildResult?> BuildAsync(
        PersonOperationTask task,
        Model.Settings settings,
        CancellationToken ct)
    {
        if (task.Flow == (int)PersonOperationFlow.Detail)
        {
            var detail = task.Payload.FromJson<Detail>();
            if (detail == null)
            {
                return null;
            }

            return await BuildDetailAsync(task, detail, settings, ct).ConfigureAwait(false);
        }

        var person = task.Payload.FromJson<SyncPersonDataPage>();
        if (person == null)
        {
            return null;
        }

        return await BuildSyncPersonDataAsync(task, person, settings, ct).ConfigureAwait(false);
    }

    private static async Task<PersonIssueBuildResult> BuildDetailAsync(
        PersonOperationTask task,
        Detail person,
        Model.Settings settings,
        CancellationToken ct)
    {
        var employeeNo = $"{task.Prefix}{person.webId}";
        var operation = (PersonOperationType)task.OperationType;

        if (operation == PersonOperationType.Delete)
        {
            return BuildDelete(person.dataId, employeeNo);
        }

        var payload = new PersonIssuePayload
        {
            EmployeeNo = employeeNo,
            // 门权限沿用原来的 doorRight=1 + RightPlan[doorNo=1, planTemplateNo=door]
            Person = new IsapiUserInfo
            {
                EmployeeNo = employeeNo,
                Name = person.name,
                UserType = "normal",
                Valid = DefaultValidPeriod(),
                DoorRight = "1",
                RightPlan =
                [
                    new IsapiRightPlan
                    {
                        DoorNo = 1,
                        PlanTemplateNo = person.door.ToString(),
                    },
                ],
            },
        };

        var steps = new List<string> { "人员" };

        // 修改人员时先删旧卡，再下发新卡
        if (!string.IsNullOrWhiteSpace(person.oldCardId))
        {
            payload.OldCardNo = person.oldCardId;
            steps.Add("删除旧卡");
        }

        if (!string.IsNullOrWhiteSpace(person.cardId))
        {
            payload.Card = new IsapiCardInfo
            {
                CardNo = person.cardId,
                EmployeeNo = employeeNo,
                // 与平台同步下发（Distribute/ISUPPostCard）的报文一致：固定普通卡
                CardType = "normalCard",
            };
            steps.Add("卡");
        }

        // 有照片且需要更新就下发人脸；没有照片时把设备上的旧人脸清掉
        if (!string.IsNullOrWhiteSpace(person.photoPath) && person.updatePhoto)
        {
            payload.Face = new IsapiFaceDataRecord
            {
                FpId = employeeNo,
                FdId = "1",
                FaceLibType = "blackFD",
                Name = person.name,
                FaceUrl = await ResolveFaceUrlAsync(person.photoPath, employeeNo, settings, ct).ConfigureAwait(false),
            };
            steps.Add("人脸");
        }
        else if (string.IsNullOrWhiteSpace(person.photoPath))
        {
            payload.DeleteFaceId = employeeNo;
            steps.Add("删除旧人脸");
        }

        payload.Steps = string.Join(" → ", steps);
        return new PersonIssueBuildResult(AcsTaskOperation.PersonIssue, payload, $"人员下发 {employeeNo}（{payload.Steps}）");
    }

    private static async Task<PersonIssueBuildResult> BuildSyncPersonDataAsync(
        PersonOperationTask task,
        SyncPersonDataPage person,
        Model.Settings settings,
        CancellationToken ct)
    {
        var employeeNo = $"{person.PersonType}{person.PersonId}";
        var operation = (PersonOperationType)task.OperationType;

        if (operation == PersonOperationType.Delete)
        {
            return BuildDelete(person.PersonId ?? 0, employeeNo);
        }

        var payload = new PersonIssuePayload
        {
            EmployeeNo = employeeNo,
            Person = new IsapiUserInfo
            {
                EmployeeNo = employeeNo,
                Name = person.PersonName,
                UserType = "normal",
                Valid = DefaultValidPeriod(),
                DoorRight = "1",
                RightPlan =
                [
                    new IsapiRightPlan
                    {
                        DoorNo = 1,
                        PlanTemplateNo = person.DoorCode,
                    },
                ],
            },
        };

        var steps = new List<string> { "人员" };

        if (!string.IsNullOrWhiteSpace(person.CardId))
        {
            payload.Card = new IsapiCardInfo
            {
                CardNo = person.CardId,
                EmployeeNo = employeeNo,
                CardType = "normalCard",
            };
            steps.Add("卡");
        }

        if (!string.IsNullOrWhiteSpace(person.Pic) && person.IsUpdatePic)
        {
            payload.Face = new IsapiFaceDataRecord
            {
                FpId = employeeNo,
                FdId = "1",
                FaceLibType = "blackFD",
                Name = person.PersonName,
                FaceUrl = await ResolveFaceUrlAsync(person.Pic, employeeNo, settings, ct).ConfigureAwait(false),
            };
            steps.Add("人脸");
        }

        payload.Steps = string.Join(" → ", steps);
        return new PersonIssueBuildResult(AcsTaskOperation.PersonIssue, payload, $"人员下发 {employeeNo}（{payload.Steps}）");
    }

    private static PersonIssueBuildResult BuildDelete(int personDataId, string employeeNo)
    {
        var payload = new PersonIssuePayload
        {
            EmployeeNo = employeeNo,
            DeletePerson = true,
            DeleteFaceId = employeeNo,
            Steps = "删除人员 → 删除人脸",
        };

        return new PersonIssueBuildResult(
            AcsTaskOperation.PersonIssueDelete,
            payload,
            $"删除人员 {employeeNo}（dataId={personDataId}）");
    }

    /// <summary>人员有效期（与原来的下发报文一致：不启用有效期限制，本地时间）。</summary>
    private static IsapiValidPeriod DefaultValidPeriod() => new()
    {
        Enable = false,
        BeginTime = "2017-08-01T17:30:08",
        EndTime = "2030-08-01T17:30:08",
        TimeType = "local",
    };

    /// <summary>
    /// 人脸图片地址处理（与原来的下发逻辑一致）：
    /// <list type="bullet">
    /// <item><c>FacePicType=1</c>：替换成平台可访问的域名；</item>
    /// <item><c>FacePicType=2</c>：先把图片下载到 <c>Settings:Photo1</c>，再用 <c>Settings:ReplaceDomainName</c> 拼地址；</item>
    /// <item>其它（含默认 0）：原样使用平台给的地址。</item>
    /// </list>
    /// </summary>
    private static async Task<string> ResolveFaceUrlAsync(
        string rawUrl,
        string fpId,
        Model.Settings settings,
        CancellationToken ct)
    {
        switch (settings.FacePicType)
        {
            case 1:
                return ReplaceDomain(rawUrl, settings.ReplaceDomainName);

            case 2:
                try
                {
                    var localPath = Path.Combine(
                        string.IsNullOrWhiteSpace(settings.Photo1) ? AppContext.BaseDirectory : settings.Photo1,
                        $"{fpId}.jpg");
                    var directory = Path.GetDirectoryName(localPath);
                    if (!string.IsNullOrEmpty(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    if (settings.ForceUpdatePic || !File.Exists(localPath))
                    {
                        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                        var bytes = await client.GetByteArrayAsync(rawUrl, ct).ConfigureAwait(false);
                        await File.WriteAllBytesAsync(localPath, bytes, ct).ConfigureAwait(false);
                    }

                    return $"{settings.ReplaceDomainName}{fpId}.jpg";
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // 下载失败不阻断：退回平台原始地址，设备侧如不可达会在任务结果里体现
                    IsupLog.Warn($"下载人脸图片失败（{rawUrl}）：{ex.Message}，改用原始地址下发");
                    return rawUrl;
                }

            default:
                return rawUrl;
        }
    }

    /// <summary>把平台图片地址的域名替换成设备可访问的地址（admin.krbb.cn 的旧路径顺带改掉）。</summary>
    private static string ReplaceDomain(string url, string newDomain)
    {
        try
        {
            var uri = new Uri(url);
            if (string.Equals(uri.Host, "admin.krbb.cn", StringComparison.OrdinalIgnoreCase))
            {
                return $"{newDomain}{uri.PathAndQuery.Replace("yeyfiles/upfiles/", "/yeyupfiles/")}";
            }

            return $"{newDomain}{uri.PathAndQuery}";
        }
        catch (UriFormatException)
        {
            return url;
        }
    }

    /// <summary>载荷转 JSON（日志 / 排查用）。</summary>
    public static string? ToJson(object? payload)
    {
        if (payload is null)
        {
            return null;
        }

        try
        {
            return JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
