namespace DeedAi.Domain.Abstractions;

public interface IBlobStorage
{
    Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken);
    Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string path, CancellationToken cancellationToken);
    Task DeleteAsync(string path, CancellationToken cancellationToken);
}
