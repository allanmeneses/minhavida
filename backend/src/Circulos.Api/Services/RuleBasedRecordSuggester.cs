using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public sealed class RuleBasedRecordSuggester : IRecordSuggester
{
    public string ProviderName => "rules";

    private static readonly CultureInfo Pt = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly (string Slug, string[] Strong, string[] Weak)[] GalleryKeys =
    [
        ("vacinas", ["vacina", "hepatite", "imuniz"], ["lote", "ubs", "posto"]),
        ("diario", ["diario", "hoje eu"], ["paragrafo"]),
        ("documento", ["documento", "carteira", "rg", "cnh"], ["arquivo"]),
        ("remedio", ["remedio", "comprimido", "tomei o remedio"], ["dose"]),
        ("pet", ["pet", "cachorro", "gato", "veterinar", "vermifugo"], []),
        ("carro", ["carro", "revisao", "oleo"], ["quilometragem"]),
        ("livro", ["livro", "autor", "capitulo", "li o", "terminei o livro"], []),
        ("compra", ["compra", "comprei", "paguei", "supermercado"], ["reais"]),
    ];

    private static readonly (string Name, string[] Keys)[] NounNames =
    [
        ("Corridas", ["corrida", "corri", "correndo", "trote", "corridas"]),
        ("Gastos", ["gastei", "gasto", "gastos", "despesa", "despesas"]),
        ("Livros", ["livro", "livros", "li o", "terminei o livro", "capitulo"]),
        ("Vacinas", ["vacina", "hepatite", "imuniz"]),
        ("Pet", ["pet", "cachorro", "gato", "veterinar", "vermifugo"]),
        ("Carro", ["carro", "revisao", "oleo"]),
        ("Remédio", ["remedio", "comprimido"]),
        ("Viagens", ["viagem", "viagens", "voo", "hotel", "aeroporto", "embarque"]),
        ("Filmes", ["filme", "filmes", "cinema", "sessao", "assisti"]),
    ];

    private static readonly string[] PlaceStop =
    [
        "dia", "mes", "minuto", "minutos", "hora", "horas", "km", "reais", "real",
        "janeiro", "fevereiro", "marco", "abril", "maio", "junho", "julho", "agosto",
        "setembro", "outubro", "novembro", "dezembro",
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

        var extra = FieldsFromSentence(text);
        var galleryHit = HighConfidenceGallery(normalized, gallery);
        var named = ExtractTypeName(text);
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

        var noun = NameFromNouns(normalized);
        var fieldsOnly = extra.Count > 1 ? extra : DefaultFields();
        return new RecordTypeSuggestion(
            named ?? noun ?? "Livre",
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
        var units = ExtractUnitNumbers(text);
        var number = FirstNumber(text);
        var place = FirstPlace(text);
        var person = FirstPerson(text);
        var title = TitleFrom(text);
        foreach (var field in schema.OrderedFields())
        {
            map[field.Id] = field.Kind switch
            {
                RecordFieldKind.Date => date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                RecordFieldKind.Number => NumberForField(field, units, number),
                RecordFieldKind.Place => place,
                RecordFieldKind.Bool => ContainsDone(text) ? "true" : null,
                RecordFieldKind.Text when field.Id == "title" => title,
                RecordFieldKind.Text when field.Id is "body" or "lote" => LoteOrBody(text, field.Id),
                RecordFieldKind.Text when field.Id is "autor" => AuthorFrom(text),
                RecordFieldKind.Text when field.Id is "quem" => person,
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
        var units = ExtractUnitNumbers(text);
        return units.Km ?? units.Minutes ?? units.Money ?? units.Kg ?? units.Dose ?? GenericNumber(text);
    }

    internal static UnitNumbers ExtractUnitNumbers(string text)
    {
        decimal? km = MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(km|quilometros?)\b");
        var milKm = Regex.Match(text, @"(\d+)\s*mil\s*(km|quilometros?)\b", RegexOptions.IgnoreCase);
        if (milKm.Success && RecordFields.ParseNumber(milKm.Groups[1].Value) is decimal thousands)
        {
            km = thousands * 1000;
        }

        var hours = MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(horas?)\b");
        var minutes = MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(minutos?|min)\b");
        if (hours is not null && minutes is null)
        {
            minutes = hours * 60;
        }

        var money = MatchNumber(text, @"R\$\s*(\d+(?:[.,]\d+)?)")
            ?? MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(reais)\b");
        var kg = MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(kg|quilos?)\b");
        var dose = MatchNumber(text, @"(\d+(?:[.,]\d+)?)\s*(mg|ml|comprimidos?|doses?)\b");
        return new UnitNumbers(km, minutes, money, kg, dose);
    }

    private static decimal? MatchNumber(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        return match.Success ? RecordFields.ParseNumber(match.Groups[1].Value) : null;
    }

    private static decimal? GenericNumber(string text)
    {
        foreach (Match match in Regex.Matches(text, @"(\d+(?:[.,]\d+)?)\s*(km|kg|mg|ml|reais|r\$)?", RegexOptions.IgnoreCase))
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

    private static string? NumberForField(RecordField field, UnitNumbers units, decimal? fallback)
    {
        var key = Normalize(field.Id + " " + field.Name);
        decimal? value = key switch
        {
            _ when key.Contains("distancia") || key.Contains("km") || key.Contains("quilometr") => units.Km,
            _ when key.Contains("duracao") || key.Contains("minuto") || key.Contains("tempo") => units.Minutes,
            _ when key.Contains("valor") || key.Contains("preco") || key.Contains("real") || key.Contains("gasto") => units.Money,
            _ when key.Contains("peso") || key.Contains("kg") => units.Kg,
            _ when key.Contains("dose") => units.Dose ?? fallback,
            _ => units.Km ?? units.Minutes ?? units.Money ?? units.Kg ?? units.Dose ?? fallback,
        };
        return value?.ToString(CultureInfo.InvariantCulture);
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

        var value = match.Groups[1].Value.Trim();
        if (PlaceStop.Contains(Normalize(value)))
        {
            return null;
        }

        return value;
    }

    internal static string? FirstPerson(string text)
    {
        var match = Regex.Match(
            text,
            @"\b(?:com a|com o|para a|para o|do meu|da minha)\s+([A-Za-zÁ-ú]{3,})",
            RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups[1].Value.Trim();
        if (PlaceStop.Contains(Normalize(value)))
        {
            return null;
        }

        return Pt.TextInfo.ToTitleCase(value.ToLower(Pt));
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
        return normalized.Contains("tomei")
            || normalized.Contains("feito")
            || normalized.Contains("ja fiz")
            || normalized.Contains("pago")
            || normalized.Contains("lido")
            || normalized.Contains("assisti");
    }

    private static bool SentenceHasBoolCue(string text)
    {
        var normalized = Normalize(text);
        return normalized.Contains("tomado")
            || normalized.Contains("feito")
            || normalized.Contains("ja fiz")
            || normalized.Contains("pago")
            || normalized.Contains("lido");
    }

    private static RecordType? HighConfidenceGallery(string normalized, IReadOnlyList<RecordType> gallery)
    {
        foreach (var (slug, strong, weak) in GalleryKeys)
        {
            var strongHit = strong.Any(key => normalized.Contains(Normalize(key)));
            var weakHits = weak.Count(key => normalized.Contains(Normalize(key)));
            if (strongHit || weakHits >= 2)
            {
                return gallery.FirstOrDefault(type => type.Slug == slug);
            }
        }

        return null;
    }

    private static string? NameFromNouns(string normalized)
    {
        foreach (var (name, keys) in NounNames)
        {
            if (keys.Any(key => normalized.Contains(Normalize(key))))
            {
                return name;
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
                return Pt.TextInfo.ToTitleCase(name.ToLower(Pt));
            }
        }

        return null;
    }

    private static List<SuggestedField> FieldsFromSentence(string text)
    {
        var fields = new List<SuggestedField>
        {
            new("title", "Título", RecordFieldKind.Text, true, 0),
        };
        var order = 1;

        void Add(string id, string name, string kind)
        {
            if (fields.Any(field => field.Id == id || string.Equals(field.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            fields.Add(new SuggestedField(id, name, kind, false, order));
            order++;
        }

        if (FirstDate(text) is not null || Regex.IsMatch(Normalize(text), @"\b(data|quando|dia)\b"))
        {
            Add("when", "Quando", RecordFieldKind.Date);
        }

        var units = ExtractUnitNumbers(text);
        if (units.Km is not null)
        {
            Add("distancia", "Distância", RecordFieldKind.Number);
        }

        if (units.Minutes is not null)
        {
            Add("duracao", "Duração", RecordFieldKind.Number);
        }

        if (units.Money is not null)
        {
            Add("valor", "Valor", RecordFieldKind.Number);
        }

        if (units.Kg is not null)
        {
            Add("peso", "Peso", RecordFieldKind.Number);
        }

        if (units.Dose is not null)
        {
            Add("dose", "Dose", RecordFieldKind.Number);
        }

        if (FirstPlace(text) is not null || Regex.IsMatch(Normalize(text), @"\b(onde|lugar|local|posto)\b"))
        {
            Add("where", "Onde", RecordFieldKind.Place);
        }

        if (FirstPerson(text) is not null)
        {
            Add("quem", "Quem", RecordFieldKind.Text);
        }

        if (SentenceHasBoolCue(text))
        {
            Add("done", "Feito", RecordFieldKind.Bool);
        }

        var named = ExtractNamedFields(text);
        MergeExtra(fields, named);

        return fields;
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
        var fields = new List<SuggestedField>();
        var order = 1;
        foreach (var part in parts)
        {
            var kind = InferKind(part);
            var id = Slugify(part);
            if (id is "title" or "quando" or "data" or "dia")
            {
                id = kind == RecordFieldKind.Date ? "when" : id;
            }

            if (id is "posto" or "onde" or "lugar" or "local")
            {
                id = "where";
            }

            if (fields.Any(field => field.Id == id))
            {
                continue;
            }

            var name = id == "when" ? "Quando" : id == "where" ? "Onde" : Pt.TextInfo.ToTitleCase(part.ToLower(Pt));
            fields.Add(new SuggestedField(id, name, kind, false, order));
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

        if (normalized is "peso" or "dose" or "valor" or "km" or "nota" or "numero" or "número" or "distancia" or "distância" or "duracao" or "duração")
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
            if (field.Id == "title")
            {
                continue;
            }

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

internal sealed record UnitNumbers(decimal? Km, decimal? Minutes, decimal? Money, decimal? Kg, decimal? Dose);
