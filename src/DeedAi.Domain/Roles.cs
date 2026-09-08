namespace DeedAi.Domain;

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Uploader = "Uploader";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Editor, Uploader, Viewer];

    public static bool CanUpload(string role) =>
        role is Admin or Editor or Uploader;

    public static bool CanEdit(string role) =>
        role is Admin or Editor;

    public static bool CanAdmin(string role) =>
        role is Admin;
}

public static class RolePolicies
{
    public const string CanUpload = "CanUpload";
    public const string CanEdit = "CanEdit";
    public const string CanAdmin = "CanAdmin";
}

public static class DocumentStatuses
{
    public const string Queued = "Queued";
    public const string Processing = "Processing";
    public const string Ready = "Ready";
    public const string Failed = "Failed";

    public static readonly string[] All = [Queued, Processing, Ready, Failed];
}
