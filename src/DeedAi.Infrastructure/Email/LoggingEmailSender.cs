using DeedAi.Domain.Abstractions;
using Microsoft.Extensions.Logging;

namespace DeedAi.Infrastructure.Email;

public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email to {To} subject {Subject}: {Body}",
            message.To,
            message.Subject,
            message.TextBody);
        return Task.CompletedTask;
    }
}
