using Circulos.Api.Storage;

namespace Circulos.Api.Tests;

public sealed class InspectableMemoryFileStorage : IFileStorage
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, byte[]> Snapshot()
    {
        lock (_files)
        {
            return _files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
        }
    }

    public async Task SaveAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        lock (_files)
        {
            _files[key] = memory.ToArray();
        }
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        lock (_files)
        {
            if (!_files.TryGetValue(key, out var bytes))
            {
                throw new FileNotFoundException("Arquivo não encontrado no armazenamento.", key);
            }

            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        lock (_files)
        {
            _files.Remove(key);
        }

        return Task.CompletedTask;
    }
}
