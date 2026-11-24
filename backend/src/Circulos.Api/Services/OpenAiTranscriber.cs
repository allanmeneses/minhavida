using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Circulos.Api.Services;

public sealed class OpenAiTranscriber(HttpClient http, IOptions<TranscriptionOptions> options) : ITranscriber
{
    public bool IsAvailable => options.Value.HasKey;
    public string ProviderName => "openai";

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (!options.Value.HasKey)
        {
            return new TranscriptionResult(string.Empty, false, "A transcrição não está configurada.");
        }

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(options.Value.Model), "model");
        form.Add(new StringContent("pt"), "language");
        var fileContent = new StreamContent(audio);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "audio.webm" : fileName);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.TranscribeUrl)
        {
            Content = form,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return new TranscriptionResult(string.Empty, false, "Não foi possível transcrever o áudio agora.");
        }

        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
        var text = doc.RootElement.TryGetProperty("text", out var value) ? value.GetString() : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return new TranscriptionResult(string.Empty, false, "A transcrição voltou vazia.");
        }

        return new TranscriptionResult(text.Trim(), true);
    }
}

public sealed class TranscriptionOptions
{
    public const string Section = "Transcription";

    public string Provider { get; set; } = "fake";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "whisper-1";
    public string TranscribeUrl { get; set; } = "https://api.openai.com/v1/audio/transcriptions";
    public string SuggestModel { get; set; } = "gpt-4o-mini";
    public string SuggestUrl { get; set; } = "https://api.openai.com/v1/chat/completions";

    public bool HasKey => !string.IsNullOrWhiteSpace(ApiKey);
    public bool UseOpenAi =>
        HasKey && (string.Equals(Provider, "openai", StringComparison.OrdinalIgnoreCase) || string.Equals(Provider, "whisper", StringComparison.OrdinalIgnoreCase));
}

public static class TranscriptionConfig
{
    public static void Bind(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TranscriptionOptions>(configuration.GetSection(TranscriptionOptions.Section));
        services.PostConfigure<TranscriptionOptions>(options =>
        {
            options.Provider = First(configuration["TRANSCRIBE_PROVIDER"], configuration["Transcription:Provider"], options.Provider) ?? "fake";
            options.ApiKey = First(configuration["OPENAI_API_KEY"], configuration["Transcription:ApiKey"], options.ApiKey) ?? string.Empty;
            options.Model = First(configuration["OPENAI_TRANSCRIBE_MODEL"], configuration["Transcription:Model"], options.Model) ?? "whisper-1";
            options.TranscribeUrl = First(configuration["OPENAI_TRANSCRIBE_URL"], configuration["Transcription:TranscribeUrl"], options.TranscribeUrl)
                ?? "https://api.openai.com/v1/audio/transcriptions";
            options.SuggestModel = First(configuration["OPENAI_SUGGEST_MODEL"], configuration["Transcription:SuggestModel"], options.SuggestModel) ?? "gpt-4o-mini";
            options.SuggestUrl = First(configuration["OPENAI_SUGGEST_URL"], configuration["Transcription:SuggestUrl"], options.SuggestUrl)
                ?? "https://api.openai.com/v1/chat/completions";
        });
    }

    private static string? First(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
