namespace DeedAi.Domain;

/// <summary>
/// Certified and default Software year range used on lookup and push.
/// </summary>
public static class SoftwareYears
{
    public const int Min = 1900;

    public static int Max => DateTime.UtcNow.Year + 2;

    public static bool IsValid(int? year, out string? error)
    {
        if (year is null)
        {
            error = null;
            return true;
        }

        if (year < Min || year > Max)
        {
            error = $"Year must be between {Min} and {Max}.";
            return false;
        }

        error = null;
        return true;
    }

    public static int? Prefer(int? defaultYear, int? certifiedYear) =>
        defaultYear ?? certifiedYear;

    public static string? Format(int? year) =>
        year is { } value && IsValid(value, out _) ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : null;
}
