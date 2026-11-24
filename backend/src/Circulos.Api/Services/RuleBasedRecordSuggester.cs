using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public sealed class RuleBasedRecordSuggester : IRecordSuggester
{
    public string ProviderName => "rules";

    private static readonly (string Slug, string[] Keys)[] GalleryKeys =
    [
        ("vacinas", ["vacina", "hepatite", "lote", "ubs", "posto", "imuniz"]),
        ("diario", ["diario", "diário", "paragrafo", "parágrafo", "hoje eu"]),
        ("documento", ["documento", "arquivo", "carteira", "rg", "cnh"]),
        ("remedio", ["remedio", "remédio", "comprimido", "tomei o", "dose"]),
        ("pet", ["pet", "cachorro", "gato", "veterinar", "vermifugo", "vermífugo"]),
        ("carro", ["carro", "revisao", "revisão", "oleo", "óleo", "quilometr", " km"]),
        ("livro", ["livro", "autor", "capitulo", "capítulo", "li o"]),
        ("compra", ["compra", "comprei", "paguei", " supermercado", "reais"]),
    ];

    public RecordTypeSuggestion SuggestType(string sentence, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery)
    {
        var text = sentence?.Trim() ?? string.Empty;
        var normalized = Normalize(text);
        var existingHit = existing.FirstOrDefault(type => normalized.Contains(Normalize(type.Name)));
        if (existingHit is not null)
        {
            return FromType(existingHit, existingHit.Id, null);
        }

        var slug = MatchSlug(normalized);
        var galleryHit = gallery.FirstOrDefault(type => type.Slug == slug)
            ?? gallery.FirstOrDefault(type => normalized.Contains(Normalize(type.Name)));
        var named = ExtractTypeName(text);
        var extra = ExtractNamedFields(text);
        if (galleryHit is not null)
        {
            var schema = RecordSchema.Parse(galleryHit.SchemaJson);
            var fields = schema.OrderedFields().Select(ToSuggested).ToList();
            MergeExtra(fields, extra);
            return new RecordTypeSuggestion(
                named ?? galleryHit.Name,
                galleryHit.Description,
                galleryHit.Slug,
                galleryHit.Id,
                null,
                fields);
        }

        var fieldsOnly = extra.Count > 0 ? extra : DefaultFields();
        return new RecordTypeSuggestion(
            named ?? "Livre",
            "Tipo criado a partir do que você descreveu.",
            Slugify(named ?? "livre"),
            null,
            null,
            fieldsOnly);
    }

    public RecordFillSuggestion SuggestRecord(string text, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery)
    {
        var raw = text?.Trim() ?? string.Empty;
        var type = SuggestType(raw, existing, gallery);
        RecordType? chosen = null;
        if (type.ExistingTypeId is Guid existingId)
        {
            chosen = existing.FirstOrDefault(item => item.Id == existingId);
        }
        else if (type.GalleryTypeId is Guid galleryId)
        {
            chosen = existing.FirstOrDefault(item => item.OriginTemplateId == galleryId)
                ?? gallery.FirstOrDefault(item => item.Id == galleryId);
        }

        var schema = chosen is null ? new RecordSchema { Fields = type.Fields.Select(ToField).ToList() } : RecordSchema.Parse(chosen.SchemaJson);
        var filled = Fill(raw, schema);
        var isNew = type.ExistingTypeId is null && existing.All(item => item.OriginTemplateId != type.GalleryTypeId || type.GalleryTypeId is null);
        if (chosen is { UserId: not null })
        {
            isNew = false;
        }

        var summary = chosen is not null
            ? $"Sugerimos o tipo {chosen.Name}."
            : $"Sugerimos criar o tipo {type.Name}.";
        return new RecordFillSuggestion(chosen?.UserId is null ? type.ExistingTypeId : chosen.Id, isNew, type, filled, summary);
    }

    public Task<RecordFillSuggestion> SuggestRecordAsync(
        string text,
        IReadOnlyList<RecordType> existing,
        IReadOnlyList<RecordType> gallery,
        CancellationToken cancellationToken) =>
        Task.FromResult(SuggestRecord(text, existing, gallery));

    internal static IReadOnlyDictionary<string, string?> Fill(string text, RecordSchema schema)
    {
        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var date = FirstDate(text);
        var number = FirstNumber(text);
        var place = FirstPlace(text);
        var title = TitleFrom(text);
        foreach (var field in schema.OrderedFields())
        {
            map[field.Id] = field.Kind switch
            {
                RecordFieldKind.Date => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                RecordFieldKind.Number => number?.ToString(CultureInfo.InvariantCulture),
                RecordFieldKind.Place => place,
                RecordFieldKind.Bool => ContainsDone(text) ? "true" : null,
                RecordFieldKind.Text when field.Id == "title" => title,
                RecordFieldKind.Text when field.Id is "body" or "lote" => LoteOrBody(text, field.Id),
                RecordFieldKind.Text when field.Id == "autor" => AuthorFrom(text),
                _ => field.Id == "title" ? title : null,
            };
        }

        if (schema.Field("title") is not null && string.IsNullOrWhiteSpace(map["title"]))
        {
            map["title"] = title;
        }

        return map;
    }

    internal static DateOnly? FirstDate(string text)
    {
        foreach (Match match in Regex.Matches(text, @"\d{1,2}\s+de\s+[A-Za-zçÇáéíóúâêôãõ]+(?:\s+de\s+\d{4})?|\d{4}-\d{2}-\d{2}|\d{1,2}/\d{1,2}/\d{2,4}"))
        {
            var parsed = RecordFields.ParseDate(match.Value);
            if (parsed is not null)
            {
                return parsed;
            }

            if (Regex.IsMatch(match.Value, @"^\d{1,2}\s+de\s+") && !match.Value.Contains("de 20", StringComparison.Ordinal) && !match.Value.Contains("de 19", StringComparison.Ordinal))
            {
                parsed = RecordFields.ParseDate(match.Value + " de 2024");
                if (parsed is not null)
                {
                    return parsed;
                }
            }
        }

        return RecordFields.ParseDate(text);
    }

    internal static decimal? FirstNumber(string text)
    {
        foreach (Match match in Regex.Matches(text, @"(\d+(?:[.,]\d+)?)\s*(km|kg|mg|ml|reais|r\$)?" , RegexOptions.IgnoreCase))
        {
            if (match.Value.Contains('/') || Regex.IsMatch(match.Value, @"\d{4}-\d{2}-\d{2}"))
            {
                continue;
            }

            if (RecordFields.ParseDate(match.Groups[1].Value) is not null && match.Groups[1].Value.Length >= 8)
            {
                continue;
            }

            var number = RecordFields.ParseNumber(match.Groups[1].Value);
            if (number is null)
            {
                continue;
            }

            if (match.Groups[2].Success || number is > 0 and < 100000)
            {
                return number;
            }
        }

        return null;
    }

    private static string? FirstPlace(string text)
    {
        var named = Regex.Match(text, @"\b(UBS\s+[\wÁ-ú]+|posto\s+[\wÁ-ú]+)", RegexOptions.IgnoreCase);
        if (named.Success)
        {
            return named.Groups[1].Value.Trim();
        }

        var match = Regex.Match(text, @"\b(?:na|no|em)\s+(?!dia\b|mes\b|mês\b)([\wÁ-ú]{3,})", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        return match.Groups[1].Value.Trim();
    }

    private static string TitleFrom(string text)
    {
        var compact = Regex.Replace(text, @"\s+", " ").Trim();
        if (compact.Length <= 80)
        {
            return compact;
        }

        return compact[..80].Trim();
    }

    private static string? LoteOrBody(string text, string id)
    {
        if (id == "lote")
        {
            var lote = Regex.Match(text, @"lote\s+([A-Za-z0-9\-]+)", RegexOptions.IgnoreCase);
            return lote.Success ? lote.Groups[1].Value : null;
        }

        return text;
    }

    private static string? AuthorFrom(string text)
    {
        var match = Regex.Match(text, @"de\s+([A-ZÁÉÍÓÚ][\wÁ-ú]+(?:\s+[A-ZÁÉÍÓÚ][\wÁ-ú]+)*)");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool ContainsDone(string text)
    {
        var normalized = Normalize(text);
        return normalized.Contains("tomei") || normalized.Contains("feito") || normalized.Contains("ja fiz") || normalized.Contains("já fiz");
    }

    private static string? MatchSlug(string normalized)
    {
        foreach (var (slug, keys) in GalleryKeys)
        {
            if (keys.Any(key => normalized.Contains(Normalize(key))))
            {
                return slug;
            }
        }

        return null;
    }

    private static string? ExtractTypeName(string text)
    {
        var match = Regex.Match(text, @"anotar\s+(?:as|os|o|a)?\s*([^,]+?)(?:,| com |$)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var name = match.Groups[1].Value.Trim();
            name = Regex.Replace(name, @"\s+do meu filho$", string.Empty, RegexOptions.IgnoreCase);
            if (name.Length is >= 3 and <= 40)
            {
                return CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(name.ToLowerInvariant());
            }
        }

        return null;
    }

    private static List<SuggestedField> ExtractNamedFields(string text)
    {
        var match = Regex.Match(text, @"\bcom\s+(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return [];
        }

        var chunk = match.Groups[1].Value;
        var parts = Regex.Split(chunk, @"\s*,\s*|\s+e\s+", RegexOptions.IgnoreCase)
            .Select(item => item.Trim().Trim('.'))
            .Where(item => item.Length is >= 2 and <= 40)
            .ToList();
        var fields = new List<SuggestedField>
        {
            new("title", "Título", RecordFieldKind.Text, true, 0),
        };
        var order = 1;
        foreach (var part in parts)
        {
            var kind = InferKind(part);
            var id = Slugify(part);
            if (fields.Any(field => field.Id == id))
            {
                continue;
            }

            fields.Add(new SuggestedField(id, CultureInfo.GetCultureInfo("pt-BR").TextInfo.ToTitleCase(part.ToLowerInvariant()), kind, false, order));
            order++;
        }

        return fields;
    }

    private static string InferKind(string name)
    {
        var normalized = Normalize(name);
        if (normalized is "data" or "quando" or "dia")
        {
            return RecordFieldKind.Date;
        }

        if (normalized is "posto" or "onde" or "lugar" or "local")
        {
            return RecordFieldKind.Place;
        }

        if (normalized is "peso" or "dose" or "valor" or "km" or "nota" or "numero" or "número")
        {
            return RecordFieldKind.Number;
        }

        if (normalized is "tomado" or "feito")
        {
            return RecordFieldKind.Bool;
        }

        if (normalized is "foto")
        {
            return RecordFieldKind.Photo;
        }

        if (normalized is "arquivo")
        {
            return RecordFieldKind.File;
        }

        return RecordFieldKind.Text;
    }

    private static List<SuggestedField> DefaultFields() =>
    [
        new("title", "Título", RecordFieldKind.Text, true, 0),
        new("when", "Quando", RecordFieldKind.Date, false, 1),
        new("body", "Texto", RecordFieldKind.Text, false, 2),
    ];

    private static void MergeExtra(List<SuggestedField> fields, List<SuggestedField> extra)
    {
        foreach (var field in extra)
        {
            if (fields.Any(item => item.Id == field.Id || string.Equals(item.Name, field.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            fields.Add(field with { Order = fields.Count });
        }
    }

    private static RecordTypeSuggestion FromType(RecordType type, Guid? existingId, Guid? galleryId)
    {
        var schema = RecordSchema.Parse(type.SchemaJson);
        return new RecordTypeSuggestion(
            type.Name,
            type.Description,
            type.Slug,
            galleryId,
            existingId,
            schema.OrderedFields().Select(ToSuggested).ToList());
    }

    private static SuggestedField ToSuggested(RecordField field) =>
        new(field.Id, field.Name, field.Kind, field.Required, field.Order, field.Options);

    private static RecordField ToField(SuggestedField field) =>
        RecordField.Create(field.Id, field.Name, field.Kind, field.Order, field.Required, field.Options);

    public static string Normalize(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(ch);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string Slugify(string value)
    {
        var normalized = Normalize(value);
        var slug = Regex.Replace(normalized, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "livre" : slug.Length > 40 ? slug[..40] : slug;
    }
}
