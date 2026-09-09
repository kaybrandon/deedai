using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Email;

public sealed class EmailOutbound(
    DeedAiDbContext db,
    IEmailSender sender,
    SendGridEmailSender sendGrid,
    SmtpEmailSender smtp,
    IOptions<EmailOptions> options,
    IConfiguration configuration,
    ILogger<EmailOutbound> logger) : IEmailOutbound
{
    public const int VerifyHours = 48;

    public EmailKvStatus KvStatus() => EmailKv.Read(configuration);

    public bool IsActiveConfigured() => KvStatus().ConfiguredFor(CurrentMode());

    public string CurrentMode()
    {
        var row = db.EmailSettings.AsNoTracking().FirstOrDefault();
        return EmailModes.Normalize(row?.Mode);
    }

    public async Task<EmailSettings> EnsureAsync(CancellationToken cancellationToken)
    {
        var item = await db.EmailSettings.FirstOrDefaultAsync(cancellationToken);
        if (item is not null)
        {
            return item;
        }

        item = new EmailSettings
        {
            Id = EmailSettings.SingletonId,
            Mode = EmailModes.SendGrid,
            FromName = options.Value.FromName,
            FromAddress = options.Value.FromEmail,
            VerifyRequired = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.EmailSettings.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        return item;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = await EnsureAsync(cancellationToken);
        var mode = EmailModes.Normalize(settings.Mode);
        var kv = KvStatus();
        if (!kv.ConfiguredFor(mode))
        {
            await RecordFailureAsync(settings, "Email is not configured for the active mode.", cancellationToken);
            throw new EmailNotConfiguredException(mode);
        }

        var fromEmail = string.IsNullOrWhiteSpace(settings.FromAddress) ? options.Value.FromEmail : settings.FromAddress.Trim();
        var fromName = string.IsNullOrWhiteSpace(settings.FromName) ? options.Value.FromName : settings.FromName.Trim();
        var envelope = message with { FromEmail = fromEmail, FromName = fromName };

        try
        {
            if (sender is RecordingEmailSender)
            {
                await sender.SendAsync(envelope, cancellationToken);
            }
            else if (mode == EmailModes.Smtp)
            {
                await smtp.SendAsync(envelope, cancellationToken);
            }
            else
            {
                await sendGrid.SendAsync(envelope, cancellationToken);
            }

            await RecordSuccessAsync(settings, cancellationToken);
        }
        catch (EmailNotConfiguredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email send failed for {Mode} to {To}", mode, message.To);
            await RecordFailureAsync(settings, EmailKv.SanitizeReason(ex.Message), cancellationToken);
            throw;
        }
    }

    public async Task RecordSuccessAsync(EmailSettings settings, CancellationToken cancellationToken)
    {
        settings.LastSuccessAt = DateTimeOffset.UtcNow;
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordFailureAsync(EmailSettings settings, string reason, CancellationToken cancellationToken)
    {
        settings.LastFailAt = DateTimeOffset.UtcNow;
        settings.LastFailReason = EmailKv.SanitizeReason(reason);
        settings.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
