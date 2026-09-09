namespace DeedAi.Domain.Entities;

public sealed class EmailSettings
{
    public static readonly Guid SingletonId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = SingletonId;
    public string Mode { get; set; } = EmailModes.SendGrid;
    public string FromName { get; set; } = "Deed AI";
    public string FromAddress { get; set; } = "noreply@bisconsultants.com";
    public bool VerifyRequired { get; set; } = true;
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastFailAt { get; set; }
    public string? LastFailReason { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
