using System.Collections.Concurrent;
using DeedAi.Domain.Abstractions;

namespace DeedAi.Infrastructure.Email;

public sealed class RecordingEmailSender : IEmailSender
{
    public ConcurrentBag<EmailMessage> Sent { get; } = [];

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Sent.Add(message);
        return Task.CompletedTask;
    }
}
