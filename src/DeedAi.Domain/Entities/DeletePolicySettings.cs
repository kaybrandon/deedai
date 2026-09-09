namespace DeedAi.Domain.Entities;

/// <summary>
/// Singleton row for who may soft-delete documents. WhoCanDelete is nullable
/// so SQL NULL materializes without GetString throwing; callers normalize
/// null/blank to <see cref="DeletePolicy.AllEditors"/>.
/// </summary>
public sealed class DeletePolicySettings
{
    public static readonly Guid SingletonId = Guid.Parse("40000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public string? WhoCanDelete { get; set; } = DeletePolicy.AllEditors;
    public string? UpdatedByEmail { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public void CoalesceNulls()
    {
        WhoCanDelete = DeletePolicy.Normalize(WhoCanDelete);
    }
}
