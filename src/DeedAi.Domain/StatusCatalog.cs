namespace DeedAi.Domain;

/// <summary>
/// Client/Software status catalog (Phase 5.2.5). Display names are Title Case.
/// Each catalog entry maps to an internal pipeline <see cref="DocumentStatuses"/>
/// or review <see cref="ReviewWorkflow"/> state — or a documented extension.
/// OCR automation stays Queued / Processing / Ready / Failed on <c>Document.Status</c>.
/// </summary>
public static class StatusCatalog
{
    public const string Complete = "Complete";
    public const string InQueue = "InQueue";
    public const string NeedsWork = "NeedsWork";
    public const string New = "New";
    public const string NotNeeded = "NotNeeded";
    public const string Pending = "Pending";
    public const string Research = "Research";
    public const string UploadError = "UploadError";

    public const string KindPipeline = "Pipeline";
    public const string KindReview = "Review";
    public const string KindCatalog = "Catalog";

    public static readonly StatusCatalogEntry[] Must =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000014"), New, "New", "#C5D4F0", KindReview, New, 10),
        new(Guid.Parse("10000000-0000-0000-0000-000000000012"), InQueue, "In Queue", "#C5CED6", KindPipeline, DocumentStatuses.Queued, 20),
        new(Guid.Parse("10000000-0000-0000-0000-000000000016"), Pending, "Pending", "#E8C96A", KindPipeline, DocumentStatuses.Processing, 30),
        new(Guid.Parse("10000000-0000-0000-0000-000000000013"), NeedsWork, "Needs Work", "#C5E8E4", KindReview, ReviewWorkflow.NeedsReview, 40),
        new(Guid.Parse("10000000-0000-0000-0000-000000000017"), Research, "Research", "#B7D9D4", KindReview, Research, 50),
        new(Guid.Parse("10000000-0000-0000-0000-000000000011"), Complete, "Complete", "#D8F0EA", KindReview, DocumentStatuses.Ready, 60),
        new(Guid.Parse("10000000-0000-0000-0000-000000000015"), NotNeeded, "Not Needed", "#D5DBE3", KindReview, NotNeeded, 70),
        new(Guid.Parse("10000000-0000-0000-0000-000000000018"), UploadError, "Upload Error", "#F5D6D3", KindPipeline, DocumentStatuses.Failed, 80)
    ];

    public static readonly string[] MustLabels = Must.Select(x => x.DisplayName).ToArray();
    public static readonly string[] MustCodes = Must.Select(x => x.Code).ToArray();
    public static readonly string[] KnownKinds = [KindPipeline, KindReview, KindCatalog];

    public static bool IsMustCode(string? code) =>
        Must.Any(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase)
                      || string.Equals(x.DisplayName, code, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnownKind(string? kind) =>
        KnownKinds.Any(x => string.Equals(x, kind, StringComparison.OrdinalIgnoreCase));

    public static string NormalizeKind(string? kind) =>
        KnownKinds.FirstOrDefault(x => string.Equals(x, kind, StringComparison.OrdinalIgnoreCase))
        ?? KindCatalog;

    public static string ToTitleCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(part =>
            part.Length == 1
                ? part.ToUpperInvariant()
                : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
    }

    public static string CodeFromDisplayName(string displayName) =>
        string.Concat(ToTitleCase(displayName).Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static StatusCatalogEntry? Find(string? codeOrName) =>
        string.IsNullOrWhiteSpace(codeOrName)
            ? null
            : Must.FirstOrDefault(x =>
                string.Equals(x.Code, codeOrName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.DisplayName, codeOrName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Assigned catalog codes that are not the Needs Work / Complete review aliases.
    /// Shown on chips instead of the OCR pipeline status. Does not change Document.Status.
    /// </summary>
    public static bool IsAssignedCatalog(string? reviewStatus) =>
        !string.IsNullOrWhiteSpace(reviewStatus)
        && !ReviewWorkflow.IsNeedsReview(reviewStatus)
        && !ReviewWorkflow.IsApproved(reviewStatus)
        && (Find(reviewStatus) is not null
            || string.Equals(reviewStatus, New, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reviewStatus, NotNeeded, StringComparison.OrdinalIgnoreCase)
            || string.Equals(reviewStatus, Research, StringComparison.OrdinalIgnoreCase));

    public static IQueryable<Entities.Document> ApplyFilter(IQueryable<Entities.Document> query, string status)
    {
        if (string.Equals(status, InQueue, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DocumentStatuses.Queued, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.Status == DocumentStatuses.Queued || x.ReviewStatus == InQueue);
        }

        if (string.Equals(status, Pending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DocumentStatuses.Processing, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.Status == DocumentStatuses.Processing || x.ReviewStatus == Pending);
        }

        if (string.Equals(status, UploadError, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DocumentStatuses.Failed, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.Status == DocumentStatuses.Failed || x.ReviewStatus == UploadError);
        }

        if (string.Equals(status, DocumentStatuses.Ready, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.Status == DocumentStatuses.Ready || x.ReviewStatus == DocumentStatuses.Ready);
        }

        if (string.Equals(status, Complete, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ReviewWorkflow.Approved, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x =>
                x.ReviewStatus == Complete
                || x.ReviewStatus == ReviewWorkflow.Approved
                || (x.Status == DocumentStatuses.Ready
                    && (x.ReviewStatus == null || x.ReviewStatus == "")));
        }

        if (string.Equals(status, NeedsWork, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, ReviewWorkflow.NeedsReview, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x =>
                x.ReviewStatus == NeedsWork || x.ReviewStatus == ReviewWorkflow.NeedsReview);
        }

        if (string.Equals(status, New, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.ReviewStatus == New);
        }

        if (string.Equals(status, NotNeeded, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.ReviewStatus == NotNeeded);
        }

        if (string.Equals(status, Research, StringComparison.OrdinalIgnoreCase))
        {
            return query.Where(x => x.ReviewStatus == Research);
        }

        return query.Where(x => x.Status == status || x.ReviewStatus == status);
    }
}

public sealed record StatusCatalogEntry(
    Guid Id,
    string Code,
    string DisplayName,
    string Color,
    string Kind,
    string MapsTo,
    int SortOrder);
