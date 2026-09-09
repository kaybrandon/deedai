using DeedAi.Domain;
using Microsoft.Extensions.Configuration;

namespace DeedAi.Infrastructure.Email;

public sealed class EmailOptions
{
    public string? SendGridApiKey { get; set; }
    public string FromEmail { get; set; } = "noreply@bisconsultants.com";
    public string FromName { get; set; } = "Deed AI";
    public string? SmtpHost { get; set; }
    public int? SmtpPort { get; set; }
    public bool? SmtpTls { get; set; }
    public string? SmtpUsername { get; set; }
    public string? SmtpPassword { get; set; }
    public int SmtpTimeoutSeconds { get; set; } = 30;
}

public sealed record EmailKvStatus(
    bool SendGridConfigured,
    string? SendGridKeyLast4,
    bool SmtpHostConfigured,
    string? SmtpHost,
    bool SmtpPortConfigured,
    int? SmtpPort,
    bool? SmtpTls,
    bool SmtpUsernameConfigured,
    bool SmtpPasswordConfigured,
    int SmtpTimeoutSeconds)
{
    public bool SmtpConfigured =>
        SmtpHostConfigured && SmtpPort is > 0 && SmtpUsernameConfigured && SmtpPasswordConfigured;

    public bool ConfiguredFor(string mode) =>
        EmailModes.Normalize(mode) == EmailModes.Smtp ? SmtpConfigured : SendGridConfigured;
}

public static partial class EmailKv
{
    public static bool HasSecret(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

    public static string? Last4(string? secret)
    {
        if (!HasSecret(secret))
        {
            return null;
        }

        var trimmed = secret!.Trim();
        return trimmed.Length < 8 ? null : trimmed[^4..];
    }

    public static EmailKvStatus Read(IConfiguration configuration)
    {
        var key = First(configuration, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey");
        var host = First(configuration, "SmtpHost", "Smtp:Host", "Smtp__Host");
        var portRaw = First(configuration, "SmtpPort", "Smtp:Port", "Smtp__Port");
        var tlsRaw = First(configuration, "SmtpTls", "Smtp:UseTls", "Smtp:EnableSsl", "Smtp__UseTls");
        var user = First(configuration, "SmtpUsername", "Smtp:Username", "Smtp__Username");
        var password = First(configuration, "SmtpPassword", "Smtp:Password", "Smtp__Password");
        var timeoutRaw = First(configuration, "SmtpTimeoutSeconds", "Smtp:TimeoutSeconds", "Smtp__TimeoutSeconds");

        int? port = int.TryParse(portRaw, out var parsedPort) ? parsedPort : null;
        bool? tls = bool.TryParse(tlsRaw, out var parsedTls) ? parsedTls : HasSecret(host) ? true : null;
        var timeout = int.TryParse(timeoutRaw, out var parsedTimeout) ? Math.Clamp(parsedTimeout, 5, 120) : 30;

        return new EmailKvStatus(
            HasSecret(key),
            Last4(key),
            HasSecret(host),
            HasSecret(host) ? host!.Trim() : null,
            port is > 0,
            port is > 0 ? port : null,
            tls,
            HasSecret(user),
            HasSecret(password),
            timeout);
    }

    public static void Bind(EmailOptions options, IConfiguration configuration)
    {
        options.SendGridApiKey = First(configuration, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey");
        options.FromEmail = First(configuration, "SendGridFromEmail", "SendGrid:FromEmail", "SmtpFromEmail", "Smtp:FromEmail")
                            ?? options.FromEmail;
        options.FromName = First(configuration, "SendGridFromName", "SendGrid:FromName", "SmtpFromName", "Smtp:FromName")
                           ?? options.FromName;
        options.SmtpHost = First(configuration, "SmtpHost", "Smtp:Host", "Smtp__Host");
        var portRaw = First(configuration, "SmtpPort", "Smtp:Port", "Smtp__Port");
        options.SmtpPort = int.TryParse(portRaw, out var port) ? port : null;
        var tlsRaw = First(configuration, "SmtpTls", "Smtp:UseTls", "Smtp:EnableSsl", "Smtp__UseTls");
        options.SmtpTls = bool.TryParse(tlsRaw, out var tls) ? tls : null;
        options.SmtpUsername = First(configuration, "SmtpUsername", "Smtp:Username", "Smtp__Username");
        options.SmtpPassword = First(configuration, "SmtpPassword", "Smtp:Password", "Smtp__Password");
        var timeoutRaw = First(configuration, "SmtpTimeoutSeconds", "Smtp:TimeoutSeconds", "Smtp__TimeoutSeconds");
        options.SmtpTimeoutSeconds = int.TryParse(timeoutRaw, out var timeout) ? Math.Clamp(timeout, 5, 120) : 30;
    }

    public static string SanitizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return "Send failed.";
        }

        var text = reason.Trim();
        if (text.Contains("not configured", StringComparison.OrdinalIgnoreCase))
        {
            return "Email is not configured for the active mode.";
        }

        if (SecretPattern().IsMatch(text))
        {
            return "Send failed.";
        }

        return text.Length <= 200 ? text : text[..200];
    }

    private static string? First(IConfiguration configuration, params string[] keys) =>
        DependencyInjection.FirstValue(configuration, keys);

    [System.Text.RegularExpressions.GeneratedRegex(
        @"password|apikey|api.key|secret|bearer\s|accountkey|connectionstring|smtp:",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex SecretPattern();
}
