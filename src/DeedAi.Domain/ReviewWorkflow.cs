namespace DeedAi.Domain;

/// <summary>
/// Legacy-sensible coupling: pipeline <see cref="DocumentStatuses"/> is OCR only.
/// Needs review is a flag that drives review workflow and <c>ReviewStatus</c>.
/// Ready + Needs review must not appear as two competing statuses — the chip
/// shows Needs review while the flag (or ReviewStatus) is on.
/// </summary>
public static class ReviewWorkflow
{
    public const string NeedsReview = "NeedsReview";
    public const string Approved = "Approved";
    public const string NeedsReviewFlagName = "Needs review";
    public static readonly Guid NeedsReviewFlagId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    public static bool IsNeedsReview(string? reviewStatus) =>
        string.Equals(reviewStatus, NeedsReview, StringComparison.OrdinalIgnoreCase);

    public static bool IsApproved(string? reviewStatus) =>
        string.Equals(reviewStatus, Approved, StringComparison.OrdinalIgnoreCase);

    public static string? NormalizeReviewStatus(string? reviewStatus) =>
        string.IsNullOrWhiteSpace(reviewStatus) ? null : reviewStatus.Trim();

    public static bool HasNeedsReviewFlag(IEnumerable<(Guid FlagId, string? Name)> flags) =>
        flags.Any(flag =>
            flag.FlagId == NeedsReviewFlagId
            || string.Equals(flag.Name, NeedsReviewFlagName, StringComparison.OrdinalIgnoreCase));

    public static string DisplayStatus(string pipelineStatus, string? reviewStatus, bool needsReviewFlag)
    {
        if (needsReviewFlag || IsNeedsReview(reviewStatus))
        {
            return NeedsReview;
        }

        if (IsApproved(reviewStatus))
        {
            return Approved;
        }

        return pipelineStatus;
    }
}
