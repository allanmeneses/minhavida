using System.Text.Json;
using System.Text.Json.Serialization;

namespace Circulos.Api.Domain;

public sealed class RecordSchema
{
    public List<RecordField> Fields { get; set; } = [];

    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static RecordSchema Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RecordSchema();
        }

        return JsonSerializer.Deserialize<RecordSchema>(json, Json) ?? new RecordSchema();
    }

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public IReadOnlyList<RecordField> OrderedFields() =>
        Fields.OrderBy(field => field.Order).ThenBy(field => field.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<RecordField> QuickFields()
    {
        var ordered = OrderedFields().Where(field => field.Kind is not RecordFieldKind.Photo and not RecordFieldKind.File).ToList();
        if (ordered.Count <= 3)
        {
            return ordered;
        }

        var picked = new List<RecordField>();
        var title = ordered.FirstOrDefault(field => field.Id == "title" || string.Equals(field.Name, "Título", StringComparison.OrdinalIgnoreCase));
        if (title is not null)
        {
            picked.Add(title);
        }

        var date = ordered.FirstOrDefault(field => field.Kind == RecordFieldKind.Date && !picked.Contains(field));
        if (date is not null)
        {
            picked.Add(date);
        }

        foreach (var field in ordered)
        {
            if (picked.Count >= 3)
            {
                break;
            }

            if (!picked.Contains(field))
            {
                picked.Add(field);
            }
        }

        return picked;
    }

    public RecordField? Field(string id) =>
        Fields.FirstOrDefault(field => string.Equals(field.Id, id, StringComparison.OrdinalIgnoreCase));
}

public sealed class RecordField
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = RecordFieldKind.Text;
    public bool Required { get; set; }
    public int Order { get; set; }
    public string[]? Options { get; set; }

    public static RecordField Create(string id, string name, string kind, int order, bool required = false, string[]? options = null) =>
        new()
        {
            Id = id,
            Name = name,
            Kind = RecordFieldKind.Normalize(kind),
            Order = order,
            Required = required,
            Options = options,
        };
}
