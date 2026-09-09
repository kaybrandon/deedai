using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DeedAi.Infrastructure.Email;

public sealed class OcrNotifier(DeedAiDbContext db, IEmailOutbound email, ILogger<OcrNotifier> logger) : IOcrNotifier
{
    public static readonly string[] Events = ["OCR Failed", "Ready"];

    public async Task NotifyStatusAsync(Guid documentId, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        if (status is not (DocumentStatuses.Ready or DocumentStatuses.Failed))
        {
            return;
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (!settings.Enabled)
        {
            return;
        }

        var document = await LoadDocumentAsync(documentId, cancellationToken);
        if (document is null)
        {
            return;
        }

        var recipients = Recipients(document, settings);
        if (recipients.Count == 0)
        {
            return;
        }

        if (!email.IsActiveConfigured())
        {
            logger.LogWarning("OCR notify skipped — email is not configured for the active mode.");
            return;
        }

        var eventName = status == DocumentStatuses.Ready ? "Ready" : "OCR Failed";
        var subject = status == DocumentStatuses.Ready
            ? $"Deed AI: {document.Name} is Ready"
            : $"Deed AI: OCR failed for {document.Name}";

        foreach (var recipient in recipients)
        {
            var text = status == DocumentStatuses.Ready
                ? $"{document.Name} for Client {document.Client.Name} is Ready.\nYou are receiving this as the {recipient.Reason.ToLowerInvariant()}."
                : $"{document.Name} for Client {document.Client.Name} failed OCR.\n{errorMessage ?? "OCR failed."}\nYou are receiving this as the {recipient.Reason.ToLowerInvariant()}.";
            var html = string.Join("", text.Split('\n').Select(line => $"<p>{System.Net.WebUtility.HtmlEncode(line)}</p>"));
            try
            {
                await email.SendAsync(new EmailMessage(recipient.Email, subject, text, html), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send {Event} notify to {Email} for {DocumentId}", eventName, recipient.Email, documentId);
            }
        }
    }

    public async Task<NotifyPreview> PreviewAsync(Guid documentId, CancellationToken cancellationToken)
    {
        var settings = await LoadSettingsAsync(cancellationToken);
        var document = await LoadDocumentAsync(documentId, cancellationToken);
        var recipients = document is null ? [] : Recipients(document, settings);
        return new NotifyPreview(settings.Enabled, settings.NotifyUploader, recipients, Events);
    }

    private async Task<NotificationSettings> LoadSettingsAsync(CancellationToken cancellationToken) =>
        await db.NotificationSettings.AsNoTracking().FirstOrDefaultAsync(cancellationToken)
        ?? new NotificationSettings
        {
            Id = NotificationSettings.SingletonId,
            Enabled = true,
            NotifyUploader = false,
            UpdatedAt = DateTimeOffset.UtcNow
        };

    private async Task<Document?> LoadDocumentAsync(Guid documentId, CancellationToken cancellationToken) =>
        await db.Documents.IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Assignee)
            .Include(x => x.UploadedBy)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == documentId, cancellationToken);

    private static IReadOnlyList<NotifyRecipient> Recipients(Document document, NotificationSettings settings)
    {
        var list = new List<NotifyRecipient>();
        if (document.Assignee is { IsActive: true })
        {
            list.Add(new NotifyRecipient(document.Assignee.Email, document.Assignee.DisplayName, "Assignee"));
        }

        if (settings.NotifyUploader
            && document.UploadedBy is { IsActive: true }
            && list.All(x => !string.Equals(x.Email, document.UploadedBy.Email, StringComparison.OrdinalIgnoreCase)))
        {
            list.Add(new NotifyRecipient(document.UploadedBy.Email, document.UploadedBy.DisplayName, "Uploader"));
        }

        return list;
    }
}

public sealed class NullOcrNotifier : IOcrNotifier
{
    public Task NotifyStatusAsync(Guid documentId, string status, string? errorMessage, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<NotifyPreview> PreviewAsync(Guid documentId, CancellationToken cancellationToken) =>
        Task.FromResult(new NotifyPreview(false, false, [], OcrNotifier.Events));
}
