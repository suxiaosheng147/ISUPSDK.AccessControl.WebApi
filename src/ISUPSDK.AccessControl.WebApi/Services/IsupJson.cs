using System.Text.Json;
using System.Text.Json.Serialization;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// ISAPI 报文的 JSON 序列化配置。设备侧字段为 camelCase，且不应下发 null 值。
/// </summary>
public static class IsupJson
{
    /// <summary>用于构建设备请求报文（写入设备）。</summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>用于读取设备响应报文（宽松一些，允许注释与尾随逗号）。</summary>
    public static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            // 设备在只返回 1 条记录时会输出裸对象而非数组。
            new SingleOrArrayConverter<Models.Isapi.IsapiUserInfo>(),
            new SingleOrArrayConverter<Models.Isapi.IsapiCardInfo>(),
            new SingleOrArrayConverter<Models.Isapi.MatchListItem>(),
        },
    };

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, ReadOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    /// <summary>
    /// 反序列化时额外注入一个转换器（用于泛型模型：泛型实参无法写在特性里）。
    /// </summary>
    public static T? Deserialize<T>(string? json, params JsonConverter[] converters)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        if (converters.Length == 0)
        {
            return Deserialize<T>(json);
        }

        var options = new JsonSerializerOptions(ReadOptions);
        foreach (var converter in converters)
        {
            options.Converters.Add(converter);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
