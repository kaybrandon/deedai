namespace DeedAi.Domain.Abstractions;

public sealed record NotifyRecipient(string Email, string DisplayName, string Reason);

public sealed record NotifyPreview(
    bool Enabled,
    bool NotifyUploader,
    IReadOnlyList<NotifyRecipient> Recipients,
    IReadOnlyList<string> Events);

public interface IOcrNotifier
{
    Task NotifyStatusAsync(Guid documentId, string status, string? errorMessage, CancellationToken cancellationToken);
    Task<NotifyPreview> PreviewAsync(Guid documentId, CancellationToken cancellationToken);
}
