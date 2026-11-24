using System.Text;

namespace Circulos.Api.Services;

public sealed class FakeTranscriber : ITranscriber
{
    public const string DefaultText = "Tomei a vacina da hepatite B no dia 10 de março de 2024 na UBS Centro";

    public bool IsAvailable => true;
    public string ProviderName => "fake";

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audio,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await audio.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        var asText = TryUtf8(bytes);
        if (!string.IsNullOrWhiteSpace(asText))
        {
            return new TranscriptionResult(asText.Trim(), true);
        }

        if (!string.IsNullOrWhiteSpace(fileName)
            && fileName.Contains("livro", StringComparison.OrdinalIgnoreCase))
        {
            return new TranscriptionResult("Terminei o livro O Alienista no dia 12 de janeiro de 2026", true);
        }

        return new TranscriptionResult(DefaultText, true);
    }

    private static string? TryUtf8(byte[] bytes)
    {
        if (bytes.Length == 0 || bytes.Length > 4000)
        {
            return null;
        }

        if (bytes.Any(item => item == 0))
        {
            return null;
        }

        var text = Encoding.UTF8.GetString(bytes).Trim();
        if (text.Length < 4)
        {
            return null;
        }

        var letters = text.Count(char.IsLetter);
        return letters >= 4 ? text : null;
    }
}
