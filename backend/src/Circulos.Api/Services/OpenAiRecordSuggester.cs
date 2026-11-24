using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Circulos.Api.Domain;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Services;

public sealed class OpenAiRecordSuggester(
    HttpClient http,
    IOptions<TranscriptionOptions> options,
    RuleBasedRecordSuggester fallback) : IRecordSuggester
{
    public string ProviderName => options.Value.HasKey ? "openai" : fallback.ProviderName;

    public RecordTypeSuggestion SuggestType(string sentence, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery) =>
        fallback.SuggestType(sentence, existing, gallery);

    public RecordFillSuggestion SuggestRecord(string text, IReadOnlyList<RecordType> existing, IReadOnlyList<RecordType> gallery) =>
        fallback.SuggestRecord(text, existing, gallery);

    public async Task<RecordFillSuggestion> SuggestRecordAsync(
        string text,
        IReadOnlyList<RecordType> existing,
        IReadOnlyList<RecordType> gallery,
        CancellationToken cancellationToken)
    {
        var local = fallback.SuggestRecord(text, existing, gallery);
        if (!options.Value.HasKey)
        {
            return local;
        }

        try
        {
            var payload = new
            {
                model = options.Value.SuggestModel,
                temperature = 0,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new { role = "system", content = "Responda só JSON com name, slug, fields (id, name, kind, value). Kinds: text, number, date, place, bool, select, photo, file. Datas em yyyy-MM-dd. Idioma pt-BR." },
                    new { role = "user", content = text },
                },
            };
            using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.SuggestUrl)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return local;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                return local;
            }

            var content = choices[0].GetProperty("message").GetProperty("content").GetString();
            if (string.IsNullOrWhiteSpace(content))
            {
                return local;
            }

            using var parsed = JsonDocument.Parse(content);
            var name = parsed.RootElement.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : local.Type.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return local;
            }

            return local;
        }
        catch (HttpRequestException)
        {
            return local;
        }
        catch (JsonException)
        {
            return local;
        }
    }
}
