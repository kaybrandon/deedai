namespace DeedAi.Domain.Abstractions;

public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string HtmlBody,
    string? FromEmail = null,
    string? FromName = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface IEmailOutbound
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
    bool IsActiveConfigured();
}
