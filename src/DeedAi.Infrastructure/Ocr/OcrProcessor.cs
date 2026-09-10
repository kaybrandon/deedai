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
    IAiExtractClient extract,
    IOptions<OcrOptions> options,
    ILogger<OcrProcessor> logger,
    IOcrNotifier notifier,
    OcrHealthRecorder ocrHealth)
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
            logger.LogWarning("Skipping extract job for missing or deleted document {DocumentId}", delivery.Job.DocumentId);
            return;
        }

        if (delivery.DequeueCount >= options.Value.PoisonDequeueCount)
        {
            document.Status = DocumentStatuses.Failed;
            document.ErrorMessage = "Extract poison: max attempts exceeded. Use Retry.";
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

        try
        {
            await using var pdf = await blobs.OpenReadAsync(document.BlobPath, cancellationToken);
            var result = await extract.ExtractAsync(document.Name, pdf, cancellationToken);
            EnsureValidExtractJson(result.RawJson);
            await ocrHealth.RecordDiOutcomeAsync(true, cancellationToken);

            var rawPath = $"ai-raw/{document.Id:N}.json";
            await using var rawStream = new MemoryStream(Encoding.UTF8.GetBytes(result.RawJson));
            await blobs.UploadAsync(rawPath, rawStream, "application/json", cancellationToken);
            document.AiRawBlobPath = rawPath;
            document.ExtractConfidenceJson = ExtractConfidence.ToJson(result.Confidence);

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

            ApplyLockedFields(document, fields, result.Fields, trim, discard);
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
            await notifier.NotifyStatusAsync(document.Id, document.Status, null, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AI extract failed for document {DocumentId}", document.Id);
            await ocrHealth.RecordDiOutcomeAsync(false, cancellationToken);
            document.Status = DocumentStatuses.Failed;
            document.ErrorMessage = FormatFailReason(ex);
            document.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await notifier.NotifyStatusAsync(document.Id, document.Status, document.ErrorMessage, cancellationToken);
            throw;
        }
    }

    internal static void ApplyLockedFields(
        Document document,
        DocumentFields fields,
        ExtractedReviewFields extracted,
        IEnumerable<string> trim,
        IEnumerable<string> discard)
    {
        document.DocumentNumber = OcrFieldCleaner.Clean(extracted.DocumentNumber, trim, discard);
        document.Volume = OcrFieldCleaner.Clean(extracted.Volume, trim, discard);
        document.Page = OcrFieldCleaner.Clean(extracted.Page, trim, discard);
        document.DeedType = OcrFieldCleaner.Clean(extracted.DeedType, trim, discard);
        document.Pid = OcrFieldCleaner.Clean(extracted.Pid, trim, discard);
        document.MailingStreet = OcrFieldCleaner.Clean(extracted.MailingStreet, trim, discard);
        document.MailingCity = OcrFieldCleaner.Clean(extracted.MailingCity, trim, discard);
        document.MailingState = OcrFieldCleaner.Clean(extracted.MailingState, trim, discard);
        document.MailingZip = OcrFieldCleaner.Clean(extracted.MailingZip, trim, discard);

        var grantors = CleanNames(extracted.Grantors, trim, discard);
        var grantees = CleanNames(extracted.Grantees, trim, discard);
        document.Grantors = grantors;
        document.Grantees = grantees;

        fields.Grantor = PartyNames.Primary(grantors);
        fields.Grantee = PartyNames.Primary(grantees);
        fields.ParcelId = document.Pid;
        fields.InstrumentDate = OcrFieldCleaner.Clean(extracted.InstrumentDate, trim, discard);
        fields.Consideration = OcrFieldCleaner.Clean(extracted.Consideration, trim, discard);
        fields.Client = OcrFieldCleaner.Clean(extracted.Client, trim, discard) ?? document.Client.Name;
        fields.Notes = OcrFieldCleaner.Clean(extracted.Notes, trim, discard);
    }

    private static List<string> CleanNames(IReadOnlyList<string>? names, IEnumerable<string> trim, IEnumerable<string> discard)
    {
        var cleaned = new List<string>();
        foreach (var name in names ?? [])
        {
            var value = OcrFieldCleaner.Clean(name, trim, discard);
            if (!string.IsNullOrWhiteSpace(value))
            {
                cleaned.Add(value);
            }
        }

        return cleaned;
    }

    private static void EnsureValidExtractJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException("AI extract failed — empty JSON. Use Retry.");
        }

        try
        {
            using var _ = JsonDocument.Parse(rawJson);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("AI extract failed — invalid JSON. Use Retry.");
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
