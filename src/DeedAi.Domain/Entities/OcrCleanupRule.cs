namespace DeedAi.Domain.Entities;

public static class OcrCleanupKinds
{
    public const string Trim = "Trim";
    public const string Discard = "Discard";
}

public sealed class OcrCleanupRule
{
    public Guid Id { get; set; }
    public required string Kind { get; set; }
    public required string Value { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
