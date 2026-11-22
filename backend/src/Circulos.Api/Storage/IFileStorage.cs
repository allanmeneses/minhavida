namespace Circulos.Api.Storage;

public interface IFileStorage
{
    Task SaveAsync(string key, Stream content, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken);
    Task DeleteAsync(string key, CancellationToken cancellationToken);
}
