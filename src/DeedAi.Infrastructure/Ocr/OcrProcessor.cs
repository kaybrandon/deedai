using System.Text;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Health;
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
    ILogger<OcrProcessor> logger,
    IOcrNotifier notifier,
    OcrPipelineSignal pipeline)
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
            await notifier.NotifyStatusAsync(document.Id, document.Status, document.ErrorMessage, cancellationToken);
            return;
        }

        document.Status = DocumentStatuses.Processing;
        document.ErrorMessage = null;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        pipeline.RecordAttempt();

        try
        {
            await using var pdf = await blobs.OpenReadAsync(document.BlobPath, cancellationToken);
            var result = await documentIntelligence.AnalyzeAsync(document.Name, pdf, cancellationToken);
            EnsureValidExtractJson(result.RawJson);

            var rawPath = $"di-raw/{document.Id:N}.json";
            await using var rawStream = new MemoryStream(Encoding.UTF8.GetBytes(result.RawJson));
            await blobs.UploadAsync(rawPath, rawStream, "application/json", cancellationToken);
            document.DiRawBlobPath = rawPath;

            var rules = await db.OcrCleanupRules.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(cancellationToken);
            var trim = rules.Where(x => x.Kind == OcrCleanupKinds.Trim).Select(x => x.Value);
            var discard = rules.Where(x => x.Kind == OcrCleanupKinds.Discard).Select(x => x.Value);

            var fields = document.Fields ?? new DocumentFields
            {
                Id = Guid.NewGuid(),
                DocumentId = document.Id
            };

            fields.Grantor = OcrFieldCleaner.Clean(result.Fields.Grantor, trim, discard);
            fields.Grantee = OcrFieldCleaner.Clean(result.Fields.Grantee, trim, discard);
            fields.InstrumentDate = OcrFieldCleaner.Clean(result.Fields.InstrumentDate, trim, discard);
            fields.Consideration = OcrFieldCleaner.Clean(result.Fields.Consideration, trim, discard);
            fields.ParcelId = OcrFieldCleaner.Clean(result.Fields.ParcelId, trim, discard);
            fields.Client = OcrFieldCleaner.Clean(result.Fields.Client, trim, discard) ?? document.Client.Name;
            fields.Notes = OcrFieldCleaner.Clean(result.Fields.Notes, trim, discard);
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
            pipeline.RecordSuccess();
            await notifier.NotifyStatusAsync(document.Id, document.Status, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OCR failed for document {DocumentId}", document.Id);
            document.Status = DocumentStatuses.Failed;
            document.ErrorMessage = FormatFailReason(ex);
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyStatusAsync(document.Id, document.Status, document.ErrorMessage, cancellationToken);
            throw;
        }
    }

    private static void EnsureValidExtractJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException("OCR failed — empty JSON. Use Retry.");
        }

        try
        {
            using var _ = JsonDocument.Parse(rawJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("OCR failed — invalid JSON. Use Retry.");
        }
    }

    private static string FormatFailReason(Exception ex)
    {
        var message = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
        if (message.Contains("JSON", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Retry", StringComparison.OrdinalIgnoreCase))
        {
            return message + " Use Retry.";
        }

        return message;
    }
}
