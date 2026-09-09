using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;

namespace DeedAi.Infrastructure.Export;

/// <summary>
/// Seeds a readable demo PDF for <c>deeds/demo/…</c> rows so Ready preview works
/// when the original file was never uploaded.
/// </summary>
public static class DemoDeedPdf
{
    public const string BlobPrefix = "deeds/demo/";

    public static bool IsDemoPath(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath))
        {
            return false;
        }

        var normalized = blobPath.Replace('\\', '/').TrimStart('/');
        return normalized.StartsWith(BlobPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public static byte[] Create(Document document)
    {
        var fields = document.Fields;
        var flags = document.Flags
            .Select(x => x.Flag?.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .ToList();
        return DeedPdfWriter.ReviewedDeed(
            document.Name,
            document.Client?.Name ?? fields?.Client ?? "Client",
            document.Status,
            document.ReviewStatus,
            document.DeedType,
            document.Assignee?.DisplayName,
            document.UpdatedAt == default ? DateTimeOffset.UtcNow : document.UpdatedAt,
            [
                ("Grantor", fields?.Grantor),
                ("Grantee", fields?.Grantee),
                ("Instrument date", fields?.InstrumentDate),
                ("Consideration", fields?.Consideration),
                ("Parcel ID", fields?.ParcelId),
                ("Client", fields?.Client),
                ("Notes", fields?.Notes)
            ],
            flags);
    }

    public static async Task<bool> EnsureUploadedAsync(
        IBlobStorage blobs,
        Document document,
        CancellationToken cancellationToken)
    {
        if (!IsDemoPath(document.BlobPath))
        {
            return false;
        }

        if (await blobs.ExistsAsync(document.BlobPath, cancellationToken))
        {
            return true;
        }

        await using var stream = new MemoryStream(Create(document));
        await blobs.UploadAsync(document.BlobPath, stream, "application/pdf", cancellationToken);
        return true;
    }
}
