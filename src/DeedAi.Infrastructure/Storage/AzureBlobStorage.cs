using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Storage;

public sealed class AzureBlobStorage(BlobServiceClient serviceClient, string containerName) : IBlobStorage
{
    public async Task UploadAsync(string path, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(containerName);
        await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var blob = container.GetBlobClient(path);
        await blob.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(path);
        var response = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return response.Value.Content;
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(path);
        return await blob.ExistsAsync(cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        var container = serviceClient.GetBlobContainerClient(containerName);
        var blob = container.GetBlobClient(path);
        await blob.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }
}
