using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Storage;

public sealed class LocalBlobStorage(string rootPath) : IBlobStorage
{
    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        _ = contentType;
        var full = FullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Stream stream = File.OpenRead(FullPath(path));
        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(FullPath(path)));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var full = FullPath(path);
        if (File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }

    private string FullPath(string path)
    {
        var sanitized = path.Replace('\\', '/').TrimStart('/');
        return Path.Combine(rootPath, sanitized.Replace('/', Path.DirectorySeparatorChar));
    }
}
