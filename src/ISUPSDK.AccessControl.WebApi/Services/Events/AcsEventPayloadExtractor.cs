using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ISUPSDK.AccessControl.WebApi.Data.EventStore;
using ISUPSDK.AccessControl.WebApi.Models;

namespace ISUPSDK.AccessControl.WebApi.Services.Events;

/// <summary>
/// 从事件报文里"尽力"提取常用查询列（工号、卡号、门号、主/次类型、认证方式等）。
///
/// 原则：只做只读解析，任何异常/缺字段都留空，绝不因为解析失败而影响入库。
/// 原始报文始终完整保存在 <see cref="AcsEventRecord.Payload"/>，需要时可回查。
/// </summary>
internal static class AcsEventPayloadExtractor
{
    /// <summary>填充可解析列（<see cref="AcsEventRecord.Payload"/> 之外的业务字段）。</summary>
    public static void Fill(AcsEventRecord record, IsupAlarmEvent evt)
    {
        record.AlarmTopic = Truncate(evt.AlarmTopic, 256);
        record.PictureFileNames = evt.PictureFileNames is { Count: > 0 }
            ? JsonSerializer.Serialize(evt.PictureFileNames)
            : null;
        record.ListenHandle = evt.ListenHandle == 0 ? null : evt.ListenHandle;

        if (string.IsNullOrWhiteSpace(evt.Payload))
        {
            return;
        }

        // ISUP / EHome 5.0 的报警报文是 PPVSPMessage XML，这里单独做一次轻量取值。
        if (evt.Payload.TrimStart().StartsWith('<'))
        {
            FillFromPpvspXml(record, evt.Payload);
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(evt.Payload);
        }
        catch (JsonException)
        {
            // 非 JSON（如 XML 上报）：保留原文，不做列级解析。
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            record.EventType ??= Truncate(ReadString(root, "eventType"), 128);
            record.EventTime = ReadTime(root, "dateTime") ?? ReadTime(root, "time") ?? record.EventTime;
            record.EventSerialNo ??= ReadSerialNo(root);

            // 实时上报的载荷在 AccessControllerEvent 下；历史查询的载荷在 AcsEvent 下。
            var container = root;
            if (root.TryGetProperty("AccessControllerEvent", out var ace) && ace.ValueKind == JsonValueKind.Object)
            {
                container = ace;
            }
            else if (root.TryGetProperty("AcsEvent", out var acs) && acs.ValueKind == JsonValueKind.Object)
            {
                container = acs;
            }

            record.Major ??= ReadInt(container, "majorEventType") ?? ReadInt(container, "major");
            record.Minor ??= ReadInt(container, "subEventType") ?? ReadInt(container, "minor");
            record.DoorNo ??= ReadInt(container, "doorNo");
            record.EmployeeNo ??= Truncate(ReadString(container, "employeeNoString") ?? ReadString(container, "employeeNo"), 64);
            record.CardNo ??= Truncate(ReadString(container, "cardNo"), 64);
            record.PersonName ??= Truncate(ReadString(container, "name"), 128);
            record.VerifyMode ??= Truncate(ReadString(container, "currentVerifyMode"), 64);
            record.EventSerialNo ??= ReadSerialNo(container);
        }
    }

    private static string? ReadSerialNo(JsonElement element)
        => Truncate(ReadString(element, "serialNo"), 64);

    private static string? ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        var value = property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.ToString(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var value) => value,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null,
        };
    }

    private static DateTimeOffset? ReadTime(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        if (text is null)
        {
            return null;
        }

        return DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// 从 ISUP / EHome 5.0 的 PPVSPMessage XML 取值。
    /// <para>
    /// 注意 <c>MajorType</c> / <c>MinorType</c> 是十六进制字符串（如 <c>0x5</c>），
    /// 转成整型后与 ISAPI 的 <c>majorEventType</c> / <c>subEventType</c> 语义一致（5=门禁）。
    /// </para>
    /// </summary>
    private static void FillFromPpvspXml(AcsEventRecord record, string xml)
    {
        if (string.IsNullOrWhiteSpace(record.DeviceId))
        {
            record.DeviceId = Truncate(ReadXml(xml, "DeviceID"), 64) ?? record.DeviceId;
        }

        record.EventSerialNo ??= Truncate(ReadXml(xml, "serialNo"), 64);
        record.DoorNo ??= ParseXmlInt(xml, "DoorNo", 10);
        record.Major ??= ParseXmlInt(xml, "MajorType", 16);
        record.Minor ??= ParseXmlInt(xml, "MinorType", 16);
        record.CardNo ??= Truncate(ReadXml(xml, "CardNo"), 64);
        record.VerifyMode ??= Truncate(ReadXml(xml, "currentVerifyMode"), 64);

        var time = ReadXml(xml, "Time");
        if (time is not null &&
            DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            record.EventTime = parsed;
        }
    }

    private static string? ReadXml(string xml, string tag)
    {
        var match = Regex.Match(xml, "<" + tag + ">([^<]*)</" + tag + ">", RegexOptions.IgnoreCase);
        var value = match.Success ? match.Groups[1].Value?.Trim() : null;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? ParseXmlInt(string xml, string tag, int numberBase)
    {
        var text = ReadXml(xml, tag);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(
            text,
            numberBase == 16 ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
