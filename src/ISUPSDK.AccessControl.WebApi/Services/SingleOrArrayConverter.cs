using System.Text.Json;
using System.Text.Json.Serialization;

namespace ISUPSDK.AccessControl.WebApi.Services;

/// <summary>
/// 兼容"单条记录时返回裸对象、多条时返回数组"的 JSON 转换器。
///
/// 海康设备在查询结果恰好只有 1 条时，会把本应是数组的字段（如 <c>UserInfo</c>、
/// <c>CardInfo</c>、<c>MatchList</c>）直接序列化为一个对象。若不兼容该行为，
/// 反序列化会抛出 JsonException，导致查询接口看似"失败"。
/// </summary>
public sealed class SingleOrArrayConverter<T> : JsonConverter<List<T>>
{
    public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartArray:
            {
                var list = new List<T>();
                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.EndArray)
                    {
                        return list;
                    }

                    var item = JsonSerializer.Deserialize<T>(ref reader, options);
                    if (item is not null)
                    {
                        list.Add(item);
                    }
                }

                return list;
            }

            case JsonTokenType.StartObject:
            {
                var single = JsonSerializer.Deserialize<T>(ref reader, options);
                return single is null ? null : new List<T> { single };
            }

            default:
                return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var item in value)
        {
            JsonSerializer.Serialize(writer, item, options);
        }

        writer.WriteEndArray();
    }
}
