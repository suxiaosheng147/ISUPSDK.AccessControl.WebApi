using ISUPSDK.AccessControl.WebApi.Interop;
using ISUPSDK.AccessControl.WebApi.Models;
using ISUPSDK.AccessControl.WebApi.Models.Isapi;
using ISUPSDK.AccessControl.WebApi.Options;
using ISUPSDK.AccessControl.WebApi.Services.Builders;
using Microsoft.Extensions.Options;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 门禁 ISAPI 业务服务：把人员、卡片、人脸、权限计划模板与门禁控制的业务请求
/// 组装为 ISAPI 报文，并通过 <see cref="IsupCmsService"/> 透传到设备。
/// </summary>
public sealed class IsupIsapiService
{
    private const string JsonQuery = "format=json";
    private const string JsonAndTimeoutQuery = "format=json&timeout=30";

    private readonly IsupCmsService _cms;
    private readonly ILogger<IsupIsapiService> _logger;
    private readonly AcsDoorControlFormat _doorControlFormat;

    public IsupIsapiService(
        IsupCmsService cms,
        IOptions<IsupAccessControlOptions> accessControlOptions,
        ILogger<IsupIsapiService> logger)
    {
        _cms = cms;
        _logger = logger;
        _doorControlFormat = accessControlOptions.Value.ResolveDoorControlFormat();
    }

    // ==========================================================================================
    // 人员下发
    // ==========================================================================================

    /// <summary>新增人员（POST /ISAPI/AccessControl/UserInfo/Record?format=json）。</summary>
    public Task<IsapiOperationResult> AddUserAsync(IsupDevice device, IsapiUserInfo user, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.UserInfoRecord, JsonQuery, user, timeoutMs, ct);

    /// <summary>修改人员（PUT /ISAPI/AccessControl/UserInfo/Modify?format=json）。</summary>
    public Task<IsapiOperationResult> UpdateUserAsync(IsupDevice device, IsapiUserInfo user, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.UserInfoModify, JsonQuery, user, timeoutMs, ct, IsapiHttpMethod.Put);

    /// <summary>按工号整体覆盖人员（PUT /ISAPI/AccessControl/UserInfo/SetUp?format=json）。</summary>
    public Task<IsapiOperationResult> SetUpUserAsync(IsupDevice device, IsapiUserInfo user, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.UserInfoSetUp, JsonQuery, user, timeoutMs, ct, IsapiHttpMethod.Put);

    /// <summary>删除人员（PUT /ISAPI/AccessControl/UserInfo/Delete?format=json）。</summary>
    public Task<IsapiOperationResult> DeleteUsersAsync(IsupDevice device, IEnumerable<string> employeeNos, uint timeoutMs = 15000, CancellationToken ct = default)
    {
        var body = new UserInfoDelCond
        {
            EmployeeNoList = employeeNos.Select(id => new IsapiEmployeeNo { EmployeeNo = id }).ToList(),
        };

        return SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.UserInfoDelete}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserInfoDelCond", body),
            timeoutMs,
            ct);
    }

    /// <summary>清空设备上全部人员（PUT /ISAPI/AccessControl/UserInfo/Delete，mode=all）。</summary>
    public Task<IsapiOperationResult> DeleteAllUsersAsync(IsupDevice device, uint timeoutMs = 30000, CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.UserInfoDelete}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserInfoDelCond", new { mode = "all" }),
            timeoutMs,
            ct);

    /// <summary>批量新增/修改人员。</summary>
    public async Task<IsapiOperationResult> UpsertUsersAsync(IsupDevice device, IReadOnlyList<IsapiUserInfo> users, bool update, uint timeoutMs, CancellationToken ct = default)
    {
        // 设备单次请求一般只接受一个 UserInfo；这里逐个下发，便于定位失败项。
        IsapiOperationResult? last = null;
        foreach (var user in users)
        {
            ct.ThrowIfCancellationRequested();
            last = update
                ? await UpdateUserAsync(device, user, timeoutMs, ct).ConfigureAwait(false)
                : await AddUserAsync(device, user, timeoutMs, ct).ConfigureAwait(false);

            if (!last.DeviceSuccess)
            {
                _logger.LogWarning(
                    "人员下发未成功：设备={DeviceId}, 工号={EmployeeNo}, 状态={Status}",
                    device.DeviceId,
                    user.EmployeeNo,
                    last.Status?.SubStatusCode ?? last.RawResponse);
                break;
            }
        }

        return last ?? throw new IsupApiException("人员列表为空。");
    }

    /// <summary>
    /// 查询人员（单页）。分页信息见返回的 <c>searchResultPosition</c> / <c>numOfMatches</c> /
    /// <c>totalMatches</c> / <c>nextPosition</c> / <c>searchId</c>。
    /// </summary>
    public async Task<IsapiQueryResult<IsapiUserInfoSearchResponse>> SearchUsersAsync(
        IsupDevice device,
        IsapiUserInfoSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var result = await SendQueryAsync<IsapiUserInfoSearchResponse>(
            device,
            IsapiEndpoints.UserInfoSearch,
            JsonQuery,
            "UserInfoSearchCond",
            condition,
            timeoutMs,
            ct).ConfigureAwait(false);

        // 分页字段在 UserInfoSearch 内层节点，顶层的通用提取取不到，这里按强类型结果回填。
        var search = result.Data?.UserInfoSearch;
        var returned = search?.UserInfo?.Count ?? search?.NumOfMatches ?? result.NumOfMatches;

        return WithPaging(
            result,
            condition.SearchResultPosition,
            returned,
            search?.TotalMatches ?? result.TotalMatches,
            search?.ResponseStatusStrg ?? result.ResponseStatusString,
            condition.SearchId,
            condition.MaxResults);
    }

    /// <summary>
    /// 统计人员数量（GET /ISAPI/AccessControl/UserInfo/Count?format=json）。
    /// 注意：该接口是 GET，不是 POST。
    /// </summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> CountUsersAsync(
        IsupDevice device,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get($"{IsapiEndpoints.UserInfoCount}?{JsonQuery}"),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        return SendQueryRawAsync<Dictionary<string, object?>>(device, raw, ct);
    }

    /// <summary>查询人员能力集（GET /ISAPI/AccessControl/UserInfo/capabilities，返回 XML）。</summary>
    public Task<IsapiOperationResult> GetUserCapabilitiesAsync(IsupDevice device, uint timeoutMs = 10000, CancellationToken ct = default)
        => SendRawAsync(device, new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get(IsapiEndpoints.UserInfoCapabilities),
            TimeoutMs = timeoutMs,
        }, ct);

    /// <summary>
    /// 自动翻页查询人员。分页全程复用同一个 searchID（设备以 searchID 维护查询会话）。
    /// </summary>
    public async Task<List<IsapiUserInfo>> ReadAllUsersAsync(
        IsupDevice device,
        IsapiUserInfoSearchCond condition,
        uint timeoutMs,
        int maxPages,
        CancellationToken ct = default)
    {
        var all = new List<IsapiUserInfo>();
        var searchId = string.IsNullOrWhiteSpace(condition.SearchId) ? Guid.NewGuid().ToString("N") : condition.SearchId;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            condition.SearchId = searchId;
            condition.SearchResultPosition = all.Count;
            if (condition.MaxResults <= 0)
            {
                condition.MaxResults = 30;
            }

            var result = await SearchUsersAsync(device, condition, timeoutMs, ct).ConfigureAwait(false);
            var pageItems = result.Data?.UserInfoSearch?.UserInfo;
            if (pageItems is { Count: > 0 })
            {
                all.AddRange(pageItems);
            }

            if (!string.Equals(result.ResponseStatusString, "MORE", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }
        }

        return all;
    }

    // ==========================================================================================
    // 卡片下发
    // ==========================================================================================

    /// <summary>新增卡片（POST /ISAPI/AccessControl/CardInfo/Record?format=json）。</summary>
    /// <remarks>
    /// 部分门禁固件不认 <c>CardInfo/Record</c>（返回 <c>subStatusCode=MessageParametersLack</c>）；
    /// 平台人员同步（Customize）场景请用 <see cref="SetUpCardAsync"/>（<c>CardInfo/SetUp</c>）。
    /// </remarks>
    public Task<IsapiOperationResult> AddCardAsync(IsupDevice device, IsapiCardInfo card, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.CardInfoRecord, JsonQuery, card, timeoutMs, ct);

    /// <summary>修改卡片（PUT /ISAPI/AccessControl/CardInfo/Modify?format=json）。</summary>
    public Task<IsapiOperationResult> UpdateCardAsync(IsupDevice device, IsapiCardInfo card, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.CardInfoModify, JsonQuery, card, timeoutMs, ct, IsapiHttpMethod.Put);

    /// <summary>
    /// 卡片整体覆盖（PUT /ISAPI/AccessControl/CardInfo/SetUp?format=json）。
    /// <para>
    /// 与平台同步下发（原 <c>Distribute/ISUPPostCard</c>）的报文一致：
    /// <c>{"CardInfo":{"cardNo":"…","employeeNo":"…","cardType":"normalCard","deleteCard":false}}</c>。
    /// 部分门禁固件不认 <c>CardInfo/Record</c>（Post 新增卡）会返回
    /// <c>subStatusCode=MessageParametersLack</c>，平台同步场景一律走这里。
    /// </para>
    /// </summary>
    /// <param name="device">目标设备。</param>
    /// <param name="card">卡片信息（<c>deleteCard=true</c> 表示删除该卡）。</param>
    /// <param name="timeoutMs">透传超时（毫秒）。</param>
    /// <param name="ct">取消令牌。</param>
    public Task<IsapiOperationResult> SetUpCardAsync(IsupDevice device, IsapiCardInfo card, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonAsync(device, IsapiEndpoints.CardInfoSetUp, JsonQuery, card, timeoutMs, ct, IsapiHttpMethod.Put);

    /// <summary>按卡号删除卡片（PUT /ISAPI/AccessControl/CardInfo/Delete?format=json）。</summary>
    public Task<IsapiOperationResult> DeleteCardsAsync(IsupDevice device, IEnumerable<string> cardNos, uint timeoutMs = 15000, CancellationToken ct = default)
    {
        var body = new CardInfoDelCond
        {
            CardNoList = cardNos.Select(no => new IsapiCardNo { CardNo = no }).ToList(),
        };

        return SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.CardInfoDelete}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("CardInfoDelCond", body),
            timeoutMs,
            ct);
    }

    /// <summary>按工号删除该人员名下的全部卡片。</summary>
    public Task<IsapiOperationResult> DeleteCardsByEmployeeAsync(IsupDevice device, IEnumerable<string> employeeNos, uint timeoutMs = 15000, CancellationToken ct = default)
    {
        var body = new CardInfoDelCond
        {
            EmployeeNoList = employeeNos.Select(no => new IsapiEmployeeNo { EmployeeNo = no }).ToList(),
        };

        return SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.CardInfoDelete}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("CardInfoDelCond", body),
            timeoutMs,
            ct);
    }

    /// <summary>清空设备上全部卡片（mode=all）。</summary>
    public Task<IsapiOperationResult> DeleteAllCardsAsync(IsupDevice device, uint timeoutMs = 30000, CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.CardInfoDelete}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("CardInfoDelCond", new { mode = "all" }),
            timeoutMs,
            ct);

    /// <summary>批量新增/修改卡片。</summary>
    public async Task<IsapiOperationResult> UpsertCardsAsync(IsupDevice device, IReadOnlyList<IsapiCardInfo> cards, bool update, uint timeoutMs, CancellationToken ct = default)
    {
        IsapiOperationResult? last = null;
        foreach (var card in cards)
        {
            ct.ThrowIfCancellationRequested();
            last = update
                ? await UpdateCardAsync(device, card, timeoutMs, ct).ConfigureAwait(false)
                : await AddCardAsync(device, card, timeoutMs, ct).ConfigureAwait(false);

            if (!last.DeviceSuccess)
            {
                _logger.LogWarning(
                    "卡片下发未成功：设备={DeviceId}, 卡号={CardNo}, 状态={Status}",
                    device.DeviceId,
                    card.CardNo,
                    last.Status?.SubStatusCode ?? last.RawResponse);
                break;
            }
        }

        return last ?? throw new IsupApiException("卡片列表为空。");
    }

    /// <summary>
    /// 查询卡片（单页）。分页信息见返回的 <c>searchResultPosition</c> / <c>numOfMatches</c> /
    /// <c>totalMatches</c> / <c>nextPosition</c> / <c>searchId</c>。
    /// </summary>
    public async Task<IsapiQueryResult<IsapiCardInfoSearchResponse>> SearchCardsAsync(
        IsupDevice device,
        IsapiCardInfoSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var result = await SendQueryAsync<IsapiCardInfoSearchResponse>(
            device,
            IsapiEndpoints.CardInfoSearch,
            JsonQuery,
            "CardInfoSearchCond",
            condition,
            timeoutMs,
            ct).ConfigureAwait(false);

        // 分页字段在 CardInfoSearch 内层节点，顶层的通用提取取不到，这里按强类型结果回填。
        var search = result.Data?.CardInfoSearch;
        var returned = search?.CardInfo?.Count ?? search?.NumOfMatches ?? result.NumOfMatches;

        return WithPaging(
            result,
            condition.SearchResultPosition,
            returned,
            search?.TotalMatches ?? result.TotalMatches,
            search?.ResponseStatusStrg ?? result.ResponseStatusString,
            condition.SearchId,
            condition.MaxResults);
    }

    // ==========================================================================================
    // 人脸下发
    // ==========================================================================================

    /// <summary>
    /// 人脸图片下发：<c>POST /ISAPI/Intelligent/FDLib/FaceDataRecord?format=json</c>
    ///
    /// 设备要求 multipart/form-data 的两个分段名为 <c>FaceDataRecord</c>（JSON 元数据）与
    /// <c>img</c>（JPEG 二进制）。本方法按"图片分段在前、JSON 分段在后"的顺序下发，
    /// 与海康设备兼容。
    /// </summary>
    public async Task<IsapiOperationResult> UploadFaceAsync(
        IsupDevice device,
        FaceDownloadRequest request,
        byte[] imageBytes,
        uint timeoutMs,
        CancellationToken ct = default)
    {
        var metadata = new IsapiFaceDataRecord
        {
            FaceLibType = request.FaceLibType,
            FdId = request.FdId,
            FpId = request.FpId,
            Name = request.Name,
            EmployeeNo = request.EmployeeNo,
            OverWrite = request.OverWrite,
        };

        var json = IsupJson.Serialize(metadata);
        var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "image/jpeg" : request.ContentType!;
        var fileName = string.IsNullOrWhiteSpace(request.FileName) ? $"{request.FpId}.jpg" : request.FileName!;

        var parts = new List<IsapiMultipartPart>
        {
            IsapiMultipartPart.Image(request.ImagePartName, imageBytes, contentType, fileName),
            IsapiMultipartPart.Json(request.JsonPartName, json, "metadata.json"),
        };

        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Post($"{IsapiEndpoints.FaceDataRecord}?{JsonQuery}"),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        var result = await _cms.SendIsapiAsync(device.UserId, raw, parts, ct).ConfigureAwait(false);
        LogResult(device, raw.RequestUrl, result);

        return IsapiOperationResult.From(device, raw with { Body = json }, result);
    }

    /// <summary>
    /// 通过 faceURL 下发人脸图片（JSON 方式，官方推荐，避免 multipart 编码问题）。
    /// 设备会自行到该 URL 拉取图片，因此 URL 必须能被设备访问。
    /// </summary>
    public Task<IsapiOperationResult> UploadFaceByUrlAsync(
        IsupDevice device,
        IsapiFaceDataRecord metadata,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metadata.FaceUrl))
        {
            throw new IsupApiException("faceURL 不能为空。");
        }

        return SendJsonWithBodyAsync(
            device,
            // ISUP 门禁终端的 faceURL 方式走 FDSetUp(PUT)：
            // FaceDataRecord(POST) 只接受 multipart（图片体），传纯 JSON 会被设备拒绝（badJsonContent）。
            IsapiEndpoints.Put($"{IsapiEndpoints.FaceDataSetUp}?{JsonQuery}"),
            metadata,
            timeoutMs,
            ct);
    }

    /// <summary>
    /// 删除人脸图片。参数通过查询字符串传递：
    /// <c>PUT /ISAPI/Intelligent/FDLib/FDSearch/Delete?format=json&amp;FDID=1&amp;FPID=1001&amp;faceLibType=blackFD</c>
    /// </summary>
    public Task<IsapiOperationResult> DeleteFaceAsync(
        IsupDevice device,
        string fpId,
        string fdId = "1",
        string faceLibType = "blackFD",
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var query = $"{JsonQuery}&FDID={Uri.EscapeDataString(fdId)}&faceLibType={Uri.EscapeDataString(faceLibType)}";

        // ISUP 门禁终端要求待删 FPID 放在请求体里；只放 query 且 body 为空会被拒（badJsonContent）。
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Put($"{IsapiEndpoints.FaceDataDelete}?{query}"),
            Body = $"{{\"FPID\":[{{\"value\":\"{fpId}\"}}]}}",
            TimeoutMs = timeoutMs,
        };

        return SendRawAsync(device, raw, ct);
    }

    /// <summary>
    /// 检索人脸图片信息（POST /ISAPI/Intelligent/FDLib/FDSearch?format=json）。
    /// 注意：该接口的响应是 multipart/form-data，JSON 位于其中的分段内；
    /// 本实现会尝试从 multipart 响应中提取 JSON 后再解析。
    /// </summary>
    public async Task<IsapiQueryResult<IsapiFaceSearchResponse>> SearchFacesAsync(
        IsupDevice device,
        IsapiFaceSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var request = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Post($"{IsapiEndpoints.FaceSearch}?{JsonQuery}"),
            Body = IsupJson.Serialize(condition),
            TimeoutMs = timeoutMs,
        };

        var result = await _cms.SendIsapiAsync(device.UserId, request, null, ct).ConfigureAwait(false);
        LogResult(device, request.RequestUrl, result);

        var json = IsapiMultipartResponse.ExtractJson(result.ResponseText) ?? result.ResponseText;

        // FDSearch 即使带 format=json 也可能回 multipart（每张人脸一个 JSON 分段），
        // 逐段解析后合并 MatchList —— 只取第一段会少数据。
        var data = MergeFaceSearchSegments(result.ResponseText, out var matchCount);

        var returned = matchCount > 0 ? matchCount : data?.NumOfMatches ?? ExtractInt(json, "numOfMatches");
        var totalMatches = data?.TotalMatches is > 0 ? data.TotalMatches.Value : ExtractInt(json, "totalMatches");
        var statusStrg = data?.ResponseStatusStrg ?? ExtractResponseStatusString(json);

        var query = new IsapiQueryResult<IsapiFaceSearchResponse>
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            RequestUrl = request.RequestUrl,
            Success = result.SdkSuccess && (result.Status?.StatusCode is null or 1 or 200),
            Status = result.Status,
            Data = data,
            RawResponse = result.ResponseText,
            ElapsedMs = result.ElapsedMs,
        };

        return WithPaging(
            query,
            condition.SearchResultPosition,
            returned,
            totalMatches,
            statusStrg,
            condition.SearchId,
            condition.MaxResults);
    }

    /// <summary>
    /// 解析 FDSearch 响应并合并所有人脸条目。
    /// <para>
    /// 响应可能是纯 JSON，也可能是 multipart（每张人脸一个 JSON 分段）；
    /// 非 JSON 分段（图片）解析失败后跳过，MatchList 按 FPID 去重合并。
    /// </para>
    /// </summary>
    /// <param name="responseText">设备原始响应。</param>
    /// <param name="matchCount">合并后实际拿到的人脸条数。</param>
    private static IsapiFaceSearchResponse? MergeFaceSearchSegments(string? responseText, out int matchCount)
    {
        matchCount = 0;

        IsapiFaceSearchResponse? merged = null;
        List<MatchListItem>? matches = null;
        HashSet<string>? seen = null;

        foreach (var segment in IsapiMultipartResponse.ExtractAllJson(responseText))
        {
            var page = IsupJson.Deserialize<IsapiFaceSearchResponse>(segment);
            if (page is null)
            {
                // 图片等非 JSON 分段解析不出来，跳过。
                continue;
            }

            if (merged is null)
            {
                merged = page;
            }
            else
            {
                merged.NumOfMatches = Math.Max(merged.NumOfMatches ?? 0, page.NumOfMatches ?? 0);
                merged.TotalMatches = Math.Max(merged.TotalMatches ?? 0, page.TotalMatches ?? 0);
                merged.ResponseStatusStrg ??= page.ResponseStatusStrg;
                merged.StatusCode ??= page.StatusCode;
                merged.SubStatusCode ??= page.SubStatusCode;
                merged.StatusString ??= page.StatusString;
            }

            if (page.MatchList is not { Count: > 0 })
            {
                continue;
            }

            matches ??= new List<MatchListItem>();
            seen ??= new HashSet<string>(StringComparer.Ordinal);

            foreach (var item in page.MatchList)
            {
                if (seen.Add(FaceMatchKey(item)))
                {
                    matches.Add(item);
                }
            }
        }

        if (merged is not null && matches is not null)
        {
            merged.MatchList = matches;
            matchCount = matches.Count;
        }

        return merged;
    }

    /// <summary>MatchList 条目的去重键：优先 <c>FPID</c>（人脸图片 ID），否则用整条 JSON 文本。</summary>
    private static string FaceMatchKey(MatchListItem item)
    {
        foreach (var pair in item)
        {
            if (string.Equals(pair.Key, "FPID", StringComparison.OrdinalIgnoreCase) && pair.Value is not null)
            {
                return "FPID:" + pair.Value;
            }
        }

        return IsupJson.Serialize(item);
    }

    /// <summary>查询人脸库列表（GET /ISAPI/Intelligent/FDLib）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetFaceLibrariesAsync(IsupDevice device, uint timeoutMs = 10000, CancellationToken ct = default)
        => SendQueryRawAsync<Dictionary<string, object?>>(device, new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get($"{IsapiEndpoints.FaceLib}?{JsonQuery}"),
            TimeoutMs = timeoutMs,
        }, ct);

    /// <summary>创建人脸库。</summary>
    public Task<IsapiOperationResult> CreateFaceLibraryAsync(IsupDevice device, IsapiFdLib fdLib, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Post($"{IsapiEndpoints.FaceLib}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("FDLib", fdLib),
            timeoutMs,
            ct);

    /// <summary>修改人脸库。</summary>
    public Task<IsapiOperationResult> UpdateFaceLibraryAsync(IsupDevice device, IsapiFdLib fdLib, uint timeoutMs = 15000, CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.FaceLib}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("FDLib", fdLib),
            timeoutMs,
            ct);

    /// <summary>删除人脸库（DELETE /ISAPI/Intelligent/FDLib，会删除该设备上全部人脸库，慎用）。</summary>
    public Task<IsapiOperationResult> DeleteFaceLibraryAsync(IsupDevice device, string? fdId = null, uint timeoutMs = 15000, CancellationToken ct = default)
    {
        var query = string.IsNullOrWhiteSpace(fdId)
            ? JsonQuery
            : $"{JsonQuery}&FDID={Uri.EscapeDataString(fdId)}";

        return SendRawAsync(device, new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Delete($"{IsapiEndpoints.FaceLib}?{query}"),
            Body = null,
            TimeoutMs = timeoutMs,
        }, ct);
    }

    // ==========================================================================================
    // 人员权限计划（计划模板 / 周计划 / 假日组 / 假日计划）
    //
    // 依据《人员权限计划模板配置》文档：
    //   PUT/GET  /ISAPI/AccessControl/UserRightPlanTemplate/<planTemplateID>?format=json
    //   PUT/GET  /ISAPI/AccessControl/UserRightWeekPlanCfg/<weekPlanID>?format=json
    //   PUT/GET  /ISAPI/AccessControl/UserRightHolidayGroupCfg/<holidayGroupID>?format=json
    //   PUT/GET  /ISAPI/AccessControl/UserRightHolidayPlanCfg/<holidayPlanID>?format=json
    //   POST .../<Resource>/Search?format=json
    // 先用 capabilities 探测支持性，再下发。
    // ==========================================================================================

    /// <summary>
    /// 探测设备是否支持人员权限计划相关配置，读取 capabilities 中的四个开关：
    /// <c>isSupportUserRightPlanTemplate</c>、<c>isSupportCardRightWeekPlanCfg</c>、
    /// <c>isSupportUserRightHolidayGroupCfg</c>、<c>isSupportCardRightHolidayPlanCfg</c>。
    /// </summary>
    public async Task<UserRightPlanCapabilities> GetUserRightPlanCapabilitiesAsync(
        IsupDevice device,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var result = await GetAccessControlCapabilitiesAsync(device, timeoutMs, ct).ConfigureAwait(false);
        var text = result.RawResponse;

        return new UserRightPlanCapabilities
        {
            PlanTemplate = ProbeFlag(text, "isSupportUserRightPlanTemplate"),
            WeekPlan = ProbeFlag(text, "isSupportCardRightWeekPlanCfg"),
            HolidayGroup = ProbeFlag(text, "isSupportUserRightHolidayGroupCfg"),
            HolidayPlan = ProbeFlag(text, "isSupportCardRightHolidayPlanCfg"),
            RawCapabilities = text,
        };
    }

    private static bool? ProbeFlag(string? capabilities, string flagName)
    {
        if (string.IsNullOrWhiteSpace(capabilities))
        {
            return null;
        }

        // capabilities 可能是 XML 或 JSON，这里用宽松的文本匹配：
        // 同时支持 <flagName>true</flagName> 与 "flagName": true 两种形态。
        var match = System.Text.RegularExpressions.Regex.Match(
            capabilities,
            $"{System.Text.RegularExpressions.Regex.Escape(flagName)}\\s*[\"']?\\s*[:>]\\s*[\"']?(true|false)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            return null;
        }

        return string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- 计划模板

    /// <summary>下发/修改人员权限计划模板（PUT UserRightPlanTemplate/&lt;id&gt;）。</summary>
    public async Task<IsapiOperationResult> WritePlanTemplateAsync(
        IsupDevice device,
        int planTemplateId,
        IsapiPlanTemplate template,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var result = await SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.PlanTemplate}/{planTemplateId}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserRightPlanTemplate", template),
            timeoutMs,
            ct).ConfigureAwait(false);

        if (!result.DeviceSuccess)
        {
            _logger.LogWarning(
                "计划模板下发未成功：设备={DeviceId}, planTemplateID={Id}, 状态={Status}",
                device.DeviceId,
                planTemplateId,
                result.Status?.SubStatusCode ?? result.RawResponse);
        }

        return result;
    }

    /// <summary>批量下发计划模板。</summary>
    public async Task<IReadOnlyList<IsapiOperationResult>> WritePlanTemplatesAsync(
        IsupDevice device,
        IReadOnlyList<(int PlanTemplateId, IsapiPlanTemplate Template)> templates,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        var results = new List<IsapiOperationResult>(templates.Count);
        foreach (var (id, template) in templates)
        {
            ct.ThrowIfCancellationRequested();
            var result = await WritePlanTemplateAsync(device, id, template, timeoutMs, ct).ConfigureAwait(false);
            results.Add(result);

            if (!result.DeviceSuccess)
            {
                break;
            }
        }

        return results;
    }

    /// <summary>查询人员权限计划模板（POST UserRightPlanTemplate/Search）。</summary>
    public Task<IsapiQueryResult<IsapiUserRightSearchResponse<IsapiPlanTemplateMatch>>> SearchPlanTemplatesAsync(
        IsupDevice device,
        IsapiUserRightSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SearchUserRightAsync<IsapiPlanTemplateMatch>(device, IsapiEndpoints.PlanTemplateSearch, condition, timeoutMs, ct);

    /// <summary>读取单个计划模板（GET UserRightPlanTemplate/&lt;id&gt;）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetPlanTemplateRawAsync(
        IsupDevice device,
        int planTemplateId,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
        => GetRawAsync(device, $"{IsapiEndpoints.PlanTemplate}/{planTemplateId}", timeoutMs, ct);

    // ---------------------------------------------------------------- 周计划

    /// <summary>下发/修改人员权限周计划（PUT UserRightWeekPlanCfg/&lt;id&gt;）。</summary>
    public Task<IsapiOperationResult> WriteWeekPlanAsync(
        IsupDevice device,
        int weekPlanId,
        IsapiWeekPlanCfg weekPlan,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.WeekPlanCfg}/{weekPlanId}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserRightWeekPlanCfg", weekPlan),
            timeoutMs,
            ct);

    /// <summary>查询人员权限周计划（POST UserRightWeekPlanCfg/Search）。</summary>
    public Task<IsapiQueryResult<IsapiUserRightSearchResponse<IsapiWeekPlanMatch>>> SearchWeekPlansAsync(
        IsupDevice device,
        IsapiUserRightSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SearchUserRightAsync<IsapiWeekPlanMatch>(device, IsapiEndpoints.WeekPlanCfgSearch, condition, timeoutMs, ct);

    /// <summary>读取单个周计划（GET UserRightWeekPlanCfg/&lt;id&gt;）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetWeekPlanRawAsync(
        IsupDevice device,
        int weekPlanId,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
        => GetRawAsync(device, $"{IsapiEndpoints.WeekPlanCfg}/{weekPlanId}", timeoutMs, ct);

    // ---------------------------------------------------------------- 假日组

    /// <summary>下发/修改人员权限假日组（PUT UserRightHolidayGroupCfg/&lt;id&gt;）。</summary>
    public Task<IsapiOperationResult> WriteHolidayGroupAsync(
        IsupDevice device,
        int holidayGroupId,
        IsapiHolidayGroupCfg group,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.HolidayGroupCfg}/{holidayGroupId}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserRightHolidayGroupCfg", group),
            timeoutMs,
            ct);

    /// <summary>查询人员权限假日组（POST UserRightHolidayGroupCfg/Search）。</summary>
    public Task<IsapiQueryResult<IsapiUserRightSearchResponse<IsapiHolidayGroupMatch>>> SearchHolidayGroupsAsync(
        IsupDevice device,
        IsapiUserRightSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SearchUserRightAsync<IsapiHolidayGroupMatch>(device, IsapiEndpoints.HolidayGroupCfgSearch, condition, timeoutMs, ct);

    /// <summary>读取单个假日组（GET UserRightHolidayGroupCfg/&lt;id&gt;）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetHolidayGroupRawAsync(
        IsupDevice device,
        int holidayGroupId,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
        => GetRawAsync(device, $"{IsapiEndpoints.HolidayGroupCfg}/{holidayGroupId}", timeoutMs, ct);

    // ---------------------------------------------------------------- 假日计划

    /// <summary>下发/修改人员权限假日计划（PUT UserRightHolidayPlanCfg/&lt;id&gt;）。</summary>
    public Task<IsapiOperationResult> WriteHolidayPlanAsync(
        IsupDevice device,
        int holidayPlanId,
        IsapiHolidayPlanCfg holidayPlan,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SendJsonWithBodyAsync(
            device,
            IsapiEndpoints.Put($"{IsapiEndpoints.HolidayPlanCfg}/{holidayPlanId}?{JsonQuery}"),
            IsapiBodyBuilder.WrapWith("UserRightHolidayPlanCfg", holidayPlan),
            timeoutMs,
            ct);

    /// <summary>查询人员权限假日计划（POST UserRightHolidayPlanCfg/Search）。</summary>
    public Task<IsapiQueryResult<IsapiUserRightSearchResponse<IsapiHolidayPlanMatch>>> SearchHolidayPlansAsync(
        IsupDevice device,
        IsapiUserRightSearchCond condition,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
        => SearchUserRightAsync<IsapiHolidayPlanMatch>(device, IsapiEndpoints.HolidayPlanCfgSearch, condition, timeoutMs, ct);

    /// <summary>读取单个假日计划（GET UserRightHolidayPlanCfg/&lt;id&gt;）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetHolidayPlanRawAsync(
        IsupDevice device,
        int holidayPlanId,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
        => GetRawAsync(device, $"{IsapiEndpoints.HolidayPlanCfg}/{holidayPlanId}", timeoutMs, ct);

    // ---------------------------------------------------------------- 内部实现

    private async Task<IsapiQueryResult<IsapiUserRightSearchResponse<TMatch>>> SearchUserRightAsync<TMatch>(
        IsupDevice device,
        string searchEndpoint,
        IsapiUserRightSearchCond condition,
        uint timeoutMs,
        CancellationToken ct)
    {
        var request = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Post($"{searchEndpoint}?{JsonQuery}"),
            Body = IsupJson.Serialize(condition),
            TimeoutMs = timeoutMs,
        };

        var result = await _cms.SendIsapiAsync(device.UserId, request, null, ct).ConfigureAwait(false);
        LogResult(device, request.RequestUrl, result);

        var json = IsapiResponseNormalizer.ToJson(result.ResponseText) ?? result.ResponseText;
        var data = IsupJson.Deserialize<IsapiUserRightSearchResponse<TMatch>>(
            json,
            new SingleOrArrayConverter<TMatch>());

        var query = new IsapiQueryResult<IsapiUserRightSearchResponse<TMatch>>
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            RequestUrl = request.RequestUrl,
            Success = result.SdkSuccess && (result.Status?.StatusCode is null or 1 or 200),
            Status = result.Status,
            Data = data,
            RawResponse = result.ResponseText,
            ElapsedMs = result.ElapsedMs,
        };

        // 人员权限类查询的 searchResultPosition 从 1 开始，nextPosition 同样按「位置 + 本页条数」推进。
        return WithPaging(
            query,
            condition.SearchResultPosition,
            data?.MatchResults?.Count ?? data?.NumOfMatches ?? 0,
            data?.TotalMatches ?? 0,
            data?.ResponseStatus,
            condition.SearchId,
            condition.MaxResults);
    }

    /// <summary>分页拉取某个 Search 端点的全部结果（复用同一个 searchID，position 从 1 开始）。</summary>
    public async Task<List<TMatch>> ReadAllUserRightAsync<TMatch>(
        IsupDevice device,
        string searchEndpoint,
        IsapiUserRightSearchCond condition,
        uint timeoutMs,
        int maxPages,
        CancellationToken ct = default)
    {
        var all = new List<TMatch>();
        var position = condition.SearchResultPosition < 1 ? 1 : condition.SearchResultPosition;

        for (var page = 0; page < maxPages; page++)
        {
            ct.ThrowIfCancellationRequested();

            var pageCondition = condition.CloneForPage(position);
            var result = await SearchUserRightAsync<TMatch>(device, searchEndpoint, pageCondition, timeoutMs, ct)
                .ConfigureAwait(false);

            var pageItems = result.Data?.MatchResults;
            if (pageItems is { Count: > 0 })
            {
                all.AddRange(pageItems);
                position += pageItems.Count;
            }
            else
            {
                break;
            }

            if (result.Data?.HasMore != true)
            {
                break;
            }
        }

        return all;
    }

    private Task<IsapiQueryResult<Dictionary<string, object?>>> GetRawAsync(
        IsupDevice device,
        string path,
        uint timeoutMs,
        CancellationToken ct)
        => SendQueryRawAsync<Dictionary<string, object?>>(device, new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get($"{path}?{JsonQuery}"),
            TimeoutMs = timeoutMs,
        }, ct);

    // ==========================================================================================
    // 门禁控制
    // ==========================================================================================

    /// <summary>
    /// 门远程控制，报文格式由 <c>ISUP:AccessControl:DoorControlFormat</c> 决定：
    /// <list type="bullet">
    /// <item><c>Json</c>（默认）：<c>PUT /ISAPI/AccessControl/RemoteControl/door/&lt;doorNo&gt;?format=json</c>，
    /// 请求体 <c>{ "RemoteControlDoor": { "cmd": "open" } }</c>；</item>
    /// <item><c>Xml</c>：同一路径但不带 <c>format=json</c>，
    /// 请求体 <c>&lt;RemoteControlDoor&gt;&lt;cmd&gt;open&lt;/cmd&gt;&lt;/RemoteControlDoor&gt;</c>
    /// （DS-K1T670M 等固件只认这种，收 JSON 会回 <c>MessageParametersLack</c>）；</item>
    /// <item><c>Auto</c>：先发 JSON，设备以报文类错误拒绝时再发 XML。</item>
    /// </list>
    /// </summary>
    public async Task<IsapiOperationResult> ControlDoorAsync(
        IsupDevice device,
        int doorNo,
        IsapiDoorCommand command,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var cmd = ToCmdText(command);

        if (_doorControlFormat != AcsDoorControlFormat.Xml)
        {
            var body = new IsapiDoorControl
            {
                RemoteControlDoor = new IsapiDoorControlBody { Cmd = cmd },
            };

            var jsonResult = await SendJsonWithBodyAsync(
                device,
                IsapiEndpoints.Put($"{IsapiEndpoints.DoorControl(doorNo)}?{JsonQuery}"),
                body,
                timeoutMs,
                ct).ConfigureAwait(false);

            if (_doorControlFormat != AcsDoorControlFormat.Auto || !ShouldRetryDoorControlAsXml(jsonResult))
            {
                return jsonResult;
            }

            _logger.LogInformation(
                "门远程控制 JSON 报文被设备 {DeviceId} 拒绝（subStatusCode={SubStatusCode}，门={DoorNo}，cmd={Cmd}），按 DoorControlFormat=Auto 改用 XML 报文重试。",
                device.DeviceId,
                jsonResult.Status?.SubStatusCode,
                doorNo,
                cmd);
        }

        return await SendXmlDoorControlAsync(device, doorNo, cmd, timeoutMs, ct).ConfigureAwait(false);
    }

    /// <summary>以 XML 报文下发门远程控制（请求行不带 <c>format=json</c>）。</summary>
    private Task<IsapiOperationResult> SendXmlDoorControlAsync(
        IsupDevice device,
        int doorNo,
        string cmd,
        uint timeoutMs,
        CancellationToken ct)
    {
        var request = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Put(IsapiEndpoints.DoorControl(doorNo)),
            Body = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><RemoteControlDoor><cmd>{cmd}</cmd></RemoteControlDoor>",
            TimeoutMs = timeoutMs,
        };

        return SendCoreAsync(device, request, ct);
    }

    /// <summary>
    /// 判断门控制的 JSON 失败是否属于"设备不认报文格式"（而非门号错、鉴权错、SDK 故障）。
    /// 只有报文类错误才值得改用 XML 重试，避免把一次真实动作重复下发。
    /// </summary>
    private static bool ShouldRetryDoorControlAsXml(IsapiOperationResult result)
    {
        if (!result.SdkSuccess || result.DeviceSuccess)
        {
            return false;
        }

        return result.Status?.SubStatusCode is "MessageParametersLack" or "badParameters" or "notSupport";
    }

    /// <summary>门状态查询（GET /ISAPI/AccessControl/DoorStatus/&lt;doorNo&gt;）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetDoorStatusAsync(
        IsupDevice device,
        int doorNo = 1,
        uint timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get($"{IsapiEndpoints.DoorStatus}/{doorNo}?{JsonQuery}"),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        return SendQueryRawAsync<Dictionary<string, object?>>(device, raw, ct);
    }

    /// <summary>门禁工作状态查询（GET /ISAPI/AccessControl/AcsWorkStatus）。</summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> GetWorkStatusAsync(IsupDevice device, uint timeoutMs = 10000, CancellationToken ct = default)
    {
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get($"{IsapiEndpoints.AcsWorkStatus}?{JsonQuery}"),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        return SendQueryRawAsync<Dictionary<string, object?>>(device, raw, ct);
    }

    /// <summary>门禁能力集查询（GET /ISAPI/AccessControl/capabilities）—返回 XML。</summary>
    public Task<IsapiOperationResult> GetAccessControlCapabilitiesAsync(IsupDevice device, uint timeoutMs = 10000, CancellationToken ct = default)
    {
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get(IsapiEndpoints.AccessControlCapabilities),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        return SendRawAsync(device, raw, ct);
    }

    /// <summary>
    /// 门禁事件查询（POST /ISAPI/AccessControl/AcsEvent），单页。
    /// 分页参数未知时（例如对账内部调用）只做解析，不填 nextPosition。
    /// </summary>
    public Task<IsapiQueryResult<Dictionary<string, object?>>> SearchEventsAsync(
        IsupDevice device,
        object condition,
        uint timeoutMs = 30000,
        CancellationToken ct = default)
        => SearchEventsAsync(device, condition, position: 0, pageSize: 0, searchId: null, timeoutMs, ct);

    /// <summary>
    /// 门禁事件查询（单页）。分页信息见返回的 <c>searchResultPosition</c> / <c>numOfMatches</c> /
    /// <c>totalMatches</c> / <c>nextPosition</c> / <c>searchId</c>。
    /// </summary>
    public async Task<IsapiQueryResult<Dictionary<string, object?>>> SearchEventsAsync(
        IsupDevice device,
        object condition,
        int position,
        int pageSize,
        string? searchId,
        uint timeoutMs = 30000,
        CancellationToken ct = default)
    {
        var result = await SendQueryAsync<Dictionary<string, object?>>(
            device,
            IsapiEndpoints.AcsEvent,
            JsonAndTimeoutQuery,
            "AcsEventCond",
            condition,
            timeoutMs,
            ct).ConfigureAwait(false);

        // numOfMatches / totalMatches / responseStatusStrg 在 AcsEvent 内层节点。
        var (numOfMatches, totalMatches, responseStatus) = ExtractNestedPaging(result.Data, "AcsEvent");
        var returned = numOfMatches > 0 ? numOfMatches : result.NumOfMatches;

        return WithPaging(
            result,
            position,
            returned,
            totalMatches > 0 ? totalMatches : result.TotalMatches,
            responseStatus ?? result.ResponseStatusString,
            searchId,
            pageSize);
    }

    /// <summary>
    /// 触发报警输出：<c>PUT /ISAPI/System/IO/outputs/&lt;id&gt;/trigger</c>。
    /// 该接口只接受 XML：
    /// <code>&lt;IOPortData&gt;&lt;outputState&gt;high&lt;/outputState&gt;&lt;/IOPortData&gt;</code>
    /// </summary>
    public Task<IsapiOperationResult> TriggerOutputAsync(
        IsupDevice device,
        int outputId,
        string command = "high",
        uint timeoutMs = 10000,
        CancellationToken ct = default)
    {
        var xml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                  $"<IOPortData version=\"2.0\" xmlns=\"http://www.isapi.org/ver20/XMLSchema\">" +
                  $"<outputState>{System.Security.SecurityElement.Escape(command)}</outputState>" +
                  $"</IOPortData>";

        return SendRawAsync(device, new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Put(IsapiEndpoints.OutputTrigger(outputId)),
            Body = xml,
            TimeoutMs = timeoutMs,
        }, ct);
    }

    /// <summary>设备信息查询（GET /ISAPI/System/deviceInfo）—设备直连能力自检。</summary>
    public Task<IsapiOperationResult> GetDeviceInfoAsync(IsupDevice device, uint timeoutMs = 10000, CancellationToken ct = default)
    {
        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Get(IsapiEndpoints.DeviceInfo),
            Body = null,
            TimeoutMs = timeoutMs,
        };

        return SendRawAsync(device, raw, ct);
    }

    /// <summary>
    /// 给设备配置 HTTP 事件上报目标（<c>PUT /ISAPI/Event/notification/httpHosts</c>）。
    ///
    /// 这是 ACS 门禁"HTTP 上报"通道的必备配置：设备会按该目标把门禁事件以
    /// <c>multipart/form-data</c>（JSON/XML 正文 + 抓拍图片）主动 POST 到平台。
    ///
    /// 注意：<b>会覆盖设备侧第 <paramref name="id"/> 条上报配置</b>；
    /// 该方法仅支持 PUT（设备对 GET 该路径不响应）。
    /// </summary>
    public Task<IsapiOperationResult> SetHttpHostNotificationAsync(
        IsupDevice device,
        string url,
        string ipAddress,
        ushort portNo,
        string parameterFormatType = "JSON",
        int id = 1,
        uint timeoutMs = 15000,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            throw new IsupApiException("HTTP 上报目标 IP 不能为空。");
        }

        if (portNo == 0)
        {
            throw new IsupApiException("HTTP 上报目标端口不能为 0。");
        }

        var normalizedUrl = string.IsNullOrWhiteSpace(url)
            ? "/"
            : (url.StartsWith('/') ? url : "/" + url);

        var body =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
            "<HttpHostNotificationList xmlns=\"http://www.hikvision.com/ver20/XMLSchema\">\n" +
            "<HttpHostNotification>\n" +
            $"<id>{id}</id>\n" +
            $"<url>{normalizedUrl}</url>\n" +
            "<protocolType>HTTP</protocolType>\n" +
            $"<parameterFormatType>{parameterFormatType}</parameterFormatType>\n" +
            "<addressingFormatType>ipaddress</addressingFormatType>\n" +
            $"<ipAddress>{ipAddress}</ipAddress>\n" +
            $"<portNo>{portNo}</portNo>\n" +
            "<httpAuthenticationMethod>none</httpAuthenticationMethod>\n" +
            "</HttpHostNotification>\n" +
            "</HttpHostNotificationList>";

        var raw = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Put(IsapiEndpoints.HttpHostNotification),
            Body = body,
            TimeoutMs = timeoutMs,
        };

        return SendRawAsync(device, raw, ct);
    }

    /// <summary>原始 ISAPI 透传（调试/兼容特殊机型使用）。</summary>
    public Task<IsapiOperationResult> SendRawAsync(IsupDevice device, IsapiRawMessage request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.RequestUrl))
        {
            throw new IsupApiException("ISAPI 请求行为空。");
        }

        var normalized = NormalizeRequestLine(request.RequestUrl);
        var effective = request with { RequestUrl = normalized };

        return SendCoreAsync(device, effective, ct);
    }

    private async Task<IsapiOperationResult> SendCoreAsync(IsupDevice device, IsapiRawMessage request, CancellationToken ct)
    {
        var result = await _cms.SendIsapiAsync(device.UserId, request, null, ct).ConfigureAwait(false);
        LogResult(device, request.RequestUrl, result);
        return IsapiOperationResult.From(device, request, result);
    }

    // ==========================================================================================
    // 内部实现
    // ==========================================================================================

    private Task<IsapiOperationResult> SendJsonAsync<TBody>(
        IsupDevice device,
        string path,
        string query,
        TBody body,
        uint timeoutMs,
        CancellationToken ct,
        IsapiHttpMethod method = IsapiHttpMethod.Post)
        where TBody : notnull
    {
        var requestLine = method == IsapiHttpMethod.Put
            ? IsapiEndpoints.Put($"{path}?{query}")
            : IsapiEndpoints.Post($"{path}?{query}");

        var wrapped = IsapiBodyBuilder.Wrap(path, body);
        return SendJsonWithBodyAsync(device, requestLine, wrapped, timeoutMs, ct);
    }

    private async Task<IsapiOperationResult> SendJsonWithBodyAsync<TBody>(
        IsupDevice device,
        string requestLine,
        TBody body,
        uint timeoutMs,
        CancellationToken ct)
        where TBody : notnull
    {
        var json = IsupJson.Serialize(body);
        var request = new IsapiRawMessage
        {
            RequestUrl = requestLine,
            Body = json,
            TimeoutMs = timeoutMs,
        };

        var result = await _cms.SendIsapiAsync(device.UserId, request, null, ct).ConfigureAwait(false);
        LogResult(device, requestLine, result);
        return IsapiOperationResult.From(device, request, result);
    }

    private async Task<IsapiQueryResult<T>> SendQueryAsync<T>(
        IsupDevice device,
        string path,
        string query,
        string conditionRootName,
        object condition,
        uint timeoutMs,
        CancellationToken ct)
    {
        var wrapped = IsapiBodyBuilder.WrapCondition(conditionRootName, condition);
        var request = new IsapiRawMessage
        {
            RequestUrl = IsapiEndpoints.Post($"{path}?{query}"),
            Body = IsupJson.Serialize(wrapped),
            TimeoutMs = timeoutMs,
        };

        return await SendQueryRawAsync<T>(device, request, ct).ConfigureAwait(false);
    }

    private async Task<IsapiQueryResult<T>> SendQueryRawAsync<T>(IsupDevice device, IsapiRawMessage request, CancellationToken ct)
    {
        var result = await _cms.SendIsapiAsync(device.UserId, request, null, ct).ConfigureAwait(false);
        LogResult(device, request.RequestUrl, result);

        // 设备可能忽略 ?format=json 而返回 XML 或 multipart，这里统一归一化。
        var json = IsapiResponseNormalizer.ToJson(result.ResponseText) ?? result.ResponseText;
        var data = IsupJson.Deserialize<T>(json);
        var responseString = ExtractResponseStatusString(json);
        var matches = ExtractInt(json, "numOfMatches");
        var total = ExtractInt(json, "totalMatches");

        return new IsapiQueryResult<T>
        {
            UserId = device.UserId,
            DeviceId = device.DeviceId,
            RequestUrl = request.RequestUrl,
            Success = result.SdkSuccess && (result.Status?.StatusCode is null or 1 or 200),
            Status = result.Status,
            Data = data,
            RawResponse = result.ResponseText,
            ElapsedMs = result.ElapsedMs,
            ResponseStatusString = responseString,
            NumOfMatches = matches,
            TotalMatches = total,
        };
    }

    /// <summary>
    /// 统一填充分页信息（<c>searchResultPosition</c> / <c>numOfMatches</c> / <c>totalMatches</c> /
    /// <c>nextPosition</c> / <c>searchId</c>）。所有设备侧分页查询接口共用同一套规则：
    /// <c>nextPosition</c> 为 <c>null</c> 表示没有下一页；翻页时把它填回 <c>searchResultPosition</c>
    /// 并复用同一个 <c>searchId</c>。
    /// </summary>
    /// <param name="result">待填充的查询结果。</param>
    /// <param name="position">本次请求的起始位置。</param>
    /// <param name="returned">本次实际返回的条数。</param>
    /// <param name="totalMatches">设备侧符合条件的总数（未知填 0）。</param>
    /// <param name="responseStatus">设备返回的 <c>responseStatusStrg</c> / <c>responseStatus</c>。</param>
    /// <param name="searchId">本次查询会话 ID（翻页需原样复用）。</param>
    /// <param name="pageSize">本次请求的 <c>maxResults</c>（设备不给总数时用于推断是否还有下一页）。</param>
    private static IsapiQueryResult<T> WithPaging<T>(
        IsapiQueryResult<T> result,
        int position,
        int returned,
        int totalMatches,
        string? responseStatus,
        string? searchId,
        int pageSize = 0)
    {
        result.SearchResultPosition = position < 0 ? 0 : position;
        result.NumOfMatches = returned;
        result.TotalMatches = totalMatches;
        result.ResponseStatusString = responseStatus;
        result.SearchId = searchId;
        result.NextPosition = HasMore(result.SearchResultPosition, returned, totalMatches, responseStatus, pageSize)
            ? result.SearchResultPosition + returned
            : null;

        return result;
    }

    /// <summary>
    /// 判断是否还有下一页：设备明确回 <c>MORE</c> 就信它；否则按「起始位置 + 本次条数 &lt; 总数」判断；
    /// 设备连总数都没给时退化为「本页是否装满 maxResults」。
    /// </summary>
    private static bool HasMore(int position, int returned, int totalMatches, string? responseStatus, int pageSize)
        => returned > 0 &&
           (string.Equals(responseStatus, "MORE", StringComparison.OrdinalIgnoreCase) ||
            (totalMatches > 0
                ? position + returned < totalMatches
                : pageSize > 0 && returned >= pageSize));

    /// <summary>
    /// 从响应字典的指定根节点（例如 <c>AcsEvent</c>）里取分页字段。
    /// 设备把 <c>numOfMatches</c> / <c>totalMatches</c> / <c>responseStatusStrg</c> 放在内层节点，
    /// 顶层的通用提取取不到。
    /// </summary>
    private static (int numOfMatches, int totalMatches, string? responseStatus) ExtractNestedPaging(
        Dictionary<string, object?>? data,
        string rootName)
    {
        if (data is null)
        {
            return (0, 0, null);
        }

        foreach (var pair in data)
        {
            if (!string.Equals(pair.Key, rootName, StringComparison.OrdinalIgnoreCase) ||
                pair.Value is not System.Text.Json.JsonElement root ||
                root.ValueKind != System.Text.Json.JsonValueKind.Object)
            {
                continue;
            }

            var numOfMatches = root.TryGetProperty("numOfMatches", out var num) && num.TryGetInt32(out var n) ? n : 0;
            var totalMatches = root.TryGetProperty("totalMatches", out var total) && total.TryGetInt32(out var t) ? t : 0;
            var responseStatus = root.TryGetProperty("responseStatusStrg", out var status) ? status.GetString() : null;

            return (numOfMatches, totalMatches, responseStatus);
        }

        return (0, 0, null);
    }

    private static string NormalizeRequestLine(string requestUrl)
    {
        var trimmed = requestUrl.Trim();
        var upper = trimmed.ToUpperInvariant();
        if (upper.StartsWith("GET ") || upper.StartsWith("POST ") || upper.StartsWith("PUT ") || upper.StartsWith("DELETE "))
        {
            return trimmed;
        }

        return trimmed.StartsWith('/') ? $"POST {trimmed}" : $"POST /{trimmed}";
    }

    private static string ToCmdText(IsapiDoorCommand command) => command switch
    {
        IsapiDoorCommand.Open => "open",
        IsapiDoorCommand.Close => "close",
        IsapiDoorCommand.AlwaysOpen => "alwaysOpen",
        IsapiDoorCommand.AlwaysClose => "alwaysClose",
        _ => "open",
    };

    private static string? ExtractResponseStatusString(string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseText);
            return document.RootElement.TryGetProperty("responseStatusStrg", out var element)
                ? element.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static int ExtractInt(string? responseText, string property)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return 0;
        }

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(responseText);
            if (document.RootElement.TryGetProperty(property, out var element))
            {
                return element.ValueKind == System.Text.Json.JsonValueKind.Number && element.TryGetInt32(out var value)
                    ? value
                    : 0;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            return 0;
        }

        return 0;
    }

    private void LogResult(IsupDevice device, string requestLine, IsapiResult result)
    {
        if (result.SdkSuccess && result.DeviceSuccess)
        {
            _logger.LogInformation(
                "ISAPI 成功：设备={DeviceId}, 请求={Request}, 耗时={Elapsed}ms",
                device.DeviceId,
                requestLine,
                result.ElapsedMs);
            return;
        }

        _logger.LogWarning(
            "ISAPI 返回异常：设备={DeviceId}, 请求={Request}, SDK成功={SdkSuccess}, SDK错误={Error}({ErrorText}), 设备状态={StatusCode}/{SubStatus}, 耗时={Elapsed}ms, 响应={Response}",
            device.DeviceId,
            requestLine,
            result.SdkSuccess,
            result.SdkErrorCode,
            result.SdkErrorText,
            result.Status?.StatusCode,
            result.Status?.SubStatusCode,
            result.ElapsedMs,
            Truncate(result.ResponseText, 512));
    }

    private static string? Truncate(string? value, int max)
        => value is null || value.Length <= max ? value : value[..max] + "...";
}
