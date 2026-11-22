using System.Text.Json;

namespace Circulos.Api.Domain;

public static class TagList
{
    public const int MaxCount = 12;
    public const int MaxLength = 40;

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string[] Normalize(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var raw in tags)
        {
            var tag = (raw ?? string.Empty).Trim();
            if (tag.Length == 0)
            {
                continue;
            }

            if (tag.Length > MaxLength)
            {
                tag = tag[..MaxLength];
            }

            if (!seen.Add(tag))
            {
                continue;
            }

            result.Add(tag);
            if (result.Count >= MaxCount)
            {
                break;
            }
        }

        return [.. result];
    }

    public static string ToJson(IEnumerable<string>? tags) =>
        JsonSerializer.Serialize(Normalize(tags), Json);

    public static string[] FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return Normalize(JsonSerializer.Deserialize<string[]>(json, Json));
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
