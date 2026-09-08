using System.Text;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Ocr;

public sealed class OcrOptions
{
    public const string SectionName = "Ocr";
    public int PoisonDequeueCount { get; set; } = 5;
    public int VisibilityTimeoutSeconds { get; set; } = 120;
}

public sealed class OcrProcessor(
    DeedAiDbContext db,
    IBlobStorage blobs,
    IDocumentIntelligenceClient documentIntelligence,
    IOptions<OcrOptions> options,
    ILogger<OcrProcessor> logger)
{
    public async Task ProcessAsync(OcrQueueDelivery delivery, CancellationToken cancellationToken)
    {
        var document = await db.Documents
            .IgnoreQueryFilters()
            .Include(x => x.Fields)
            .Include(x => x.Client)
            .FirstOrDefaultAsync(x => x.Id == delivery.Job.DocumentId, cancellationToken);

        if (document is null || document.DeletedAt is not null)
        {
            logger.LogWarning("Skipping OCR job for missing or deleted document {DocumentId}", delivery.Job.DocumentId);
            return;
        }

        if (delivery.DequeueCount >= options.Value.PoisonDequeueCount)
        {
            document.Status = DocumentStatuses.Failed;
            document.ErrorMessage = "OCR poison: max attempts exceeded. Use Retry.";
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Marked document {DocumentId} Failed after poison dequeue count {Count}", document.Id, delivery.DequeueCount);
            return;
        }

        document.Status = DocumentStatuses.Processing;
        document.ErrorMessage = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await using var pdf = await blobs.OpenReadAsync(document.BlobPath, cancellationToken);
            var result = await documentIntelligence.AnalyzeAsync(document.Name, pdf, cancellationToken);

            var rawPath = $"di-raw/{document.Id:N}.json";
            await using var rawStream = new MemoryStream(Encoding.UTF8.GetBytes(result.RawJson));
            await blobs.UploadAsync(rawPath, rawStream, "application/json", cancellationToken);
            document.DiRawBlobPath = rawPath;

            var fields = document.Fields ?? new DocumentFields
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id
            };

            fields.Grantor = result.Fields.Grantor;
            fields.Grantee = result.Fields.Grantee;
            fields.InstrumentDate = result.Fields.InstrumentDate;
            fields.Consideration = result.Fields.Consideration;
            fields.ParcelId = result.Fields.ParcelId;
            fields.Client = result.Fields.Client ?? document.Client.Name;
            fields.Notes = result.Fields.Notes;
            fields.IsDraft = false;
            fields.UpdatedAt = DateTimeOffset.UtcNow;

            if (document.Fields is null)
            {
                db.DocumentFields.Add(fields);
            }

            document.Status = DocumentStatuses.Ready;
            document.ErrorMessage = null;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCR failed for document {DocumentId}", document.Id);
            document.Status = DocumentStatuses.Failed;
            document.ErrorMessage = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}
