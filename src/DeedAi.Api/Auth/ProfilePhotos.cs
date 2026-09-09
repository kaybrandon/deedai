using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace DeedAi.Api.Auth;

public static class ProfilePhotos
{
    public const long MaxBytes = 2L * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif"
    };

    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".png"] = "image/png",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif"
    };

    public const string LimitMessage = "Photo must be JPEG, PNG, WebP, or GIF and 2 MB or smaller.";

    public static string? Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return "Choose a photo to upload.";
        }

        if (file.Length > MaxBytes || !Extensions.ContainsKey(file.ContentType ?? ""))
        {
            return LimitMessage;
        }

        var ext = Path.GetExtension(file.FileName);
        if (!string.IsNullOrEmpty(ext) && !ContentTypes.ContainsKey(ext))
        {
            return LimitMessage;
        }

        return null;
    }

    public static string BlobPath(Guid userId, string contentType) =>
        $"users/{userId:N}/photo{Extensions[contentType]}";

    public static string ContentTypeFor(string path)
    {
        var ext = Path.GetExtension(path);
        return ContentTypes.TryGetValue(ext, out var type) ? type : "application/octet-stream";
    }

    public static async Task<string> SaveAsync(
        IBlobStorage blobs,
        UserAccount user,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var path = BlobPath(user.Id, file.ContentType);
        if (!string.IsNullOrWhiteSpace(user.PhotoBlobPath)
            && !string.Equals(user.PhotoBlobPath, path, StringComparison.OrdinalIgnoreCase)
            && await blobs.ExistsAsync(user.PhotoBlobPath, cancellationToken))
        {
            await blobs.DeleteAsync(user.PhotoBlobPath, cancellationToken);
        }

        await using var stream = file.OpenReadStream();
        await blobs.UploadAsync(path, stream, file.ContentType, cancellationToken);
        user.PhotoBlobPath = path;
        return path;
    }

    public static async Task ClearAsync(IBlobStorage blobs, UserAccount user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.PhotoBlobPath))
        {
            return;
        }

        if (await blobs.ExistsAsync(user.PhotoBlobPath, cancellationToken))
        {
            await blobs.DeleteAsync(user.PhotoBlobPath, cancellationToken);
        }

        user.PhotoBlobPath = null;
    }

    public static async Task<IActionResult?> OpenAsync(
        IBlobStorage blobs,
        UserAccount user,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.PhotoBlobPath)
            || !await blobs.ExistsAsync(user.PhotoBlobPath, cancellationToken))
        {
            return null;
        }

        var stream = await blobs.OpenReadAsync(user.PhotoBlobPath, cancellationToken);
        return new FileStreamResult(stream, ContentTypeFor(user.PhotoBlobPath));
    }
}
