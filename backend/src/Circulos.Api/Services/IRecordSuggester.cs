using Circulos.Api.Domain;

namespace Circulos.Api.Services;

public interface IRecordSuggester
{
    string ProviderName { get; }
    RecordTypeSuggestion SuggestType(string sentence, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery);
    RecordFillSuggestion SuggestRecord(string text, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery);
    Task<RecordFillSuggestion> SuggestRecordAsync(
        string text,
        IReadOnlyList<RecordType> existing,
        IReadOnlyList<RecordType> gallery,
        CancellationToken cancellationToken);
}

public sealed record SuggestedField(string Id, string Name, string Kind, bool Required, int Order, string[]? Options = null);

public sealed record RecordTypeSuggestion(
    string Name,
    string Description,
    string Slug,
    Guid? GalleryTypeId,
    Guid? ExistingTypeId,
    IReadOnlyList<SuggestedField> Fields);

public sealed record RecordFillSuggestion(
    Guid? TypeId,
    bool IsNewType,
    RecordTypeSuggestion Type,
    IReadOnlyDictionary<string, string?> Fields,
    string Summary);
