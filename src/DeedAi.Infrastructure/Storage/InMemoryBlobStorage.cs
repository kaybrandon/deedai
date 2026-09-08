using System.Collections.Concurrent;
using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Storage;

public sealed class InMemoryBlobStorage : IBlobStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _blobs = new(StringComparer.OrdinalIgnoreCase);

    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, cancellationToken);
        _blobs[path] = ms.ToArray();
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_blobs.TryGetValue(path, out var bytes))
        {
            throw new FileNotFoundException("Blob not found.", path);
        }

        return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_blobs.ContainsKey(path));
    }
}
