namespace DeedAi.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public required string Status { get; set; }
    public required string BlobPath { get; set; }
    public string? DiRawBlobPath { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public UserAccount? Assignee { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public UserAccount? UploadedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DeedType { get; set; }
    public string? DocumentNumber { get; set; }
    public string? Volume { get; set; }
    public string? Page { get; set; }
    public string? Pid { get; set; }
    public string? MailingStreet { get; set; }
    public string? MailingCity { get; set; }
    public string? MailingState { get; set; }
    public string? MailingZip { get; set; }
    public List<string>? Grantors { get; set; } = [];
    public List<string>? Grantees { get; set; } = [];

    public void CoalesceNullListFields()
    {
        DocumentNumber ??= "";
        Volume ??= "";
        Page ??= "";
        DeedType ??= "";
        Pid ??= "";
        MailingStreet ??= "";
        MailingCity ??= "";
        MailingState ??= "";
        MailingZip ??= "";
        Grantors ??= [];
        Grantees ??= [];
    }

    public string? EffectivePid => string.IsNullOrWhiteSpace(Pid) ? Fields?.ParcelId : Pid;
    public string? ReviewStatus { get; set; }
    public DateTimeOffset? LastSoftwareSyncAt { get; set; }
    public string? LastSoftwareSyncStatus { get; set; }
    public string? LastSoftwareSyncDirection { get; set; }
    public string? LastSoftwareSyncFailReason { get; set; }
    public string? SoftwareRecordId { get; set; }
    public string? SalesTabCode { get; set; }
    public DocumentFields? Fields { get; set; }
    public ICollection<DocumentFlag> Flags { get; set; } = new List<DocumentFlag>();
    public ICollection<DocumentTeamMember> Team { get; set; } = new List<DocumentTeamMember>();
    public ICollection<DocumentLink> OutgoingLinks { get; set; } = new List<DocumentLink>();
    public ICollection<DocumentLink> IncomingLinks { get; set; } = new List<DocumentLink>();
}
