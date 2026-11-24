using System.Globalization;
using System.Text.Json;
using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public static class RecordFields
{
    public static readonly JsonSerializerOptions Json = RecordSchema.Json;
    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    public static Dictionary<string, JsonElement> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        }

        using var doc = JsonDocument.Parse(json);
        var map = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in doc.RootElement.EnumerateObject())
        {
            map[property.Name] = property.Value.Clone();
        }

        return map;
    }

    public static string ToJson(IReadOnlyDictionary<string, JsonElement> fields) =>
        JsonSerializer.Serialize(fields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase), Json);

    public static string ToJson(IReadOnlyDictionary<string, object?> fields) =>
        JsonSerializer.Serialize(fields, Json);

    public static void ApplyIndexed(LifeRecord record, RecordSchema schema, IReadOnlyDictionary<string, JsonElement> fields)
    {
        var titleField = schema.OrderedFields().FirstOrDefault(field => field.Id == "title" || field.Kind == RecordFieldKind.Text);
        var dateField = schema.OrderedFields().FirstOrDefault(field => field.Kind == RecordFieldKind.Date);
        var numberField = schema.OrderedFields().FirstOrDefault(field => field.Kind == RecordFieldKind.Number);
        var bodyField = schema.OrderedFields().FirstOrDefault(field =>
            field.Id == "body" || string.Equals(field.Name, "Texto", StringComparison.OrdinalIgnoreCase));

        record.Title = ReadString(fields, titleField?.Id) ?? record.Title;
        if (string.IsNullOrWhiteSpace(record.Title))
        {
            record.Title = ReadString(fields, "title") ?? "Sem título";
        }

        record.Body = ReadString(fields, bodyField?.Id) ?? ReadString(fields, "body");
        record.OccurredOn = ReadDate(fields, dateField?.Id) ?? ReadDate(fields, "when") ?? ReadDate(fields, "date");
        record.NumberValue = ReadNumber(fields, numberField?.Id) ?? ReadNumber(fields, "number") ?? ReadNumber(fields, "valor") ?? ReadNumber(fields, "km");
        record.FieldsJson = ToJson(fields);
    }

    public static string? ReadString(IReadOnlyDictionary<string, JsonElement> fields, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !fields.TryGetValue(id, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => "sim",
            JsonValueKind.False => "não",
            JsonValueKind.Null => null,
            _ => value.ToString(),
        };
    }

    public static DateOnly? ReadDate(IReadOnlyDictionary<string, JsonElement> fields, string? id)
    {
        var raw = ReadString(fields, id);
        return ParseDate(raw);
    }

    public static decimal? ReadNumber(IReadOnlyDictionary<string, JsonElement> fields, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || !fields.TryGetValue(id, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return ParseNumber(value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString());
    }

    public static DateOnly? ParseDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().ToLowerInvariant();
        if (DateOnly.TryParse(text, Pt, DateTimeStyles.None, out var ptDate))
        {
            return ptDate;
        }

        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant))
        {
            return invariant;
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"(\d{1,2})\s+de\s+([a-zç]+)\s+de\s+(\d{4})");
        if (match.Success)
        {
            var month = MonthFromName(match.Groups[2].Value);
            if (month is int monthNumber
                && int.TryParse(match.Groups[1].Value, out var day)
                && int.TryParse(match.Groups[3].Value, out var year)
                && DateOnly.TryParse($"{year:0000}-{monthNumber:00}-{day:00}", out var named))
            {
                return named;
            }
        }

        return null;
    }

    public static decimal? ParseNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Replace("R$", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        var match = System.Text.RegularExpressions.Regex.Match(text, @"-?\d(?:[\d.,]*\d)?");
        var token = match.Success ? match.Value : text;
        var lastComma = token.LastIndexOf(',');
        var lastDot = token.LastIndexOf('.');
        if (lastComma >= 0 && lastDot >= 0)
        {
            token = lastComma > lastDot
                ? token.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.')
                : token.Replace(",", string.Empty, StringComparison.Ordinal);
        }
        else if (lastComma >= 0)
        {
            token = token.Replace(',', '.');
        }

        return decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    public static JsonElement FromObject(object? value)
    {
        return JsonSerializer.SerializeToElement(value, Json);
    }

    private static int? MonthFromName(string name) => name switch
    {
        "janeiro" => 1,
        "fevereiro" => 2,
        "março" or "marco" => 3,
        "abril" => 4,
        "maio" => 5,
        "junho" => 6,
        "julho" => 7,
        "agosto" => 8,
        "setembro" => 9,
        "outubro" => 10,
        "novembro" => 11,
        "dezembro" => 12,
        _ => null,
    };
}
