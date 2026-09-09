using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeedAi.Domain.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DeedAi.Infrastructure.Email;

public sealed class SendGridOptions
{
    public string? ApiKey { get; set; }
    public string FromEmail { get; set; } = "noreply@bisconsultants.com";
    public string FromName { get; set; } = "Deed AI";
}

public sealed class SendGridEmailSender(
    IHttpClientFactory httpFactory,
    IOptions<SendGridOptions> options,
    ILogger<SendGridEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var key = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("SendGridApiKey is not configured.");
        }

        var payload = new
        {
            personalizations = new[] { new { to = new[] { new { email = message.To } } } },
            from = new { email = options.Value.FromEmail, name = options.Value.FromName },
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
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("SendGrid rejected mail ({Status}): {Body}", (int)response.StatusCode, body);
            throw new InvalidOperationException("Could not send email.");
        }
    }
}
