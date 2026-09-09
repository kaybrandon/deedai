namespace DeedAi.Domain;

/// <summary>
/// Soft-delete policy: All Editors (Admin + Editor) or Admin only.
/// Uploader and Viewer never delete. Null/unknown values default to All Editors.
/// </summary>
public static class DeletePolicy
{
    public const string AllEditors = "AllEditors";
    public const string AdminOnly = "AdminOnly";

    public static readonly string[] All = [AllEditors, AdminOnly];

    public static string Normalize(string? value) =>
        string.Equals(value?.Trim(), AdminOnly, StringComparison.OrdinalIgnoreCase)
            ? AdminOnly
            : AllEditors;

    public static bool IsKnown(string? value) =>
        string.Equals(value?.Trim(), AllEditors, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value?.Trim(), AdminOnly, StringComparison.OrdinalIgnoreCase);

    public static string Label(string? value) =>
        Normalize(value) == AdminOnly ? "Admin only" : "All Editors";

    public static bool Allows(string role, string? whoCanDelete)
    {
        if (AppRoles.CanAdmin(role))
        {
            return true;
        }

        if (!AppRoles.CanEdit(role))
        {
            return false;
        }

        return Normalize(whoCanDelete) == AllEditors;
    }
}
