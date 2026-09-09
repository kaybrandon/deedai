namespace DeedAi.Infrastructure.Security;

public static class PasswordRules
{
    public const int MinLength = 8;

    public const string RequirementMessage =
        "Password must be at least 8 characters and include an uppercase letter, a lowercase letter, a digit, and a symbol.";

    public const string RequiredMessage = "Password is required.";

    public static string? Validate(string? password, bool required = true)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return required ? RequiredMessage : null;
        }

        if (password.Length < MinLength
            || !password.Any(char.IsUpper)
            || !password.Any(char.IsLower)
            || !password.Any(char.IsDigit)
            || !password.Any(static c => !char.IsLetterOrDigit(c)))
        {
            return RequirementMessage;
        }

        return null;
    }
}
