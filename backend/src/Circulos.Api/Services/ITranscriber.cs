namespace Circulos.Api.Services;

public interface ITranscriber
{
    bool IsAvailable { get; }
    string ProviderName { get; }
    Task<TranscriptionResult> TranscribeAsync(Stream audio, string fileName, string contentType, CancellationToken cancellationToken);
}

public sealed record TranscriptionResult(string Text, bool Available, string? Error = null);
