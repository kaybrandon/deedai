namespace DeedAi.Domain;

public static class EmailModes
{
    public const string SendGrid = "SendGrid";
    public const string Smtp = "Smtp";

    public static readonly string[] All = [SendGrid, Smtp];

    public static bool IsKnown(string? mode) =>
        string.Equals(mode, SendGrid, StringComparison.OrdinalIgnoreCase)
        || string.Equals(mode, Smtp, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? mode) =>
        string.Equals(mode, Smtp, StringComparison.OrdinalIgnoreCase) ? Smtp : SendGrid;
}

public sealed class EmailNotConfiguredException(string mode) : InvalidOperationException(
    $"Email is not configured for the {mode} mode.")
{
    public string Mode { get; } = mode;
}
