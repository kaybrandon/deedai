using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Email;

public sealed class SendGridEmailSender(
    IHttpClientFactory httpFactory,
    IOptions<EmailOptions> options,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var key = options.Value.SendGridApiKey;
        if (!EmailKv.HasSecret(key))
        {
            throw new EmailNotConfiguredException(EmailModes.SendGrid);
        }

        var fromEmail = message.FromEmail ?? options.Value.FromEmail;
        var fromName = message.FromName ?? options.Value.FromName;
        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = message.To } } } },
            from = new { email = fromEmail, name = fromName },
            subject = message.Subject,
            content = new object[]
            {
                new { type = "text/plain", value = message.TextBody },
                new { type = "text/html", value = message.HtmlBody }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await httpFactory.CreateClient(nameof(SendGridEmailSender))
            .SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("SendGrid rejected mail ({Status})", (int)response.StatusCode);
            throw new InvalidOperationException("Could not send email.");
        }
    }
}
