using System.Net;
using System.Net.Mail;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Email;

public sealed class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var cfg = options.Value;
        if (!EmailKv.HasSecret(cfg.SmtpHost) || cfg.SmtpPort is not > 0
            || !EmailKv.HasSecret(cfg.SmtpUsername) || !EmailKv.HasSecret(cfg.SmtpPassword))
        {
            throw new EmailNotConfiguredException(EmailModes.Smtp);
        }

        var fromEmail = message.FromEmail ?? cfg.FromEmail;
        var fromName = message.FromName ?? cfg.FromName;
        using var mail = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true
        };
        mail.To.Add(message.To);
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.TextBody, null, "text/plain"));

        using var client = new SmtpClient(cfg.SmtpHost!.Trim(), cfg.SmtpPort.Value)
        {
            EnableSsl = cfg.SmtpTls ?? true,
            Credentials = new NetworkCredential(cfg.SmtpUsername!.Trim(), cfg.SmtpPassword),
            Timeout = Math.Clamp(cfg.SmtpTimeoutSeconds, 5, 120) * 1000
        };

        try
        {
            await client.SendMailAsync(mail, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SMTP rejected mail to {To}", message.To);
            throw new InvalidOperationException("Could not send email.");
        }
    }
}
