using DeedAi.Domain;
using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace DeedAi.Infrastructure.Data;

public sealed class DeedAiDbContext(DbContextOptions<DeedAiDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFields> DocumentFields => Set<DocumentFields>();
    public DbSet<UserClientAccess> UserClientAccess => Set<UserClientAccess>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<EmailSettings> EmailSettings => Set<EmailSettings>();
    public DbSet<FlagDefinition> FlagDefinitions => Set<FlagDefinition>();
    public DbSet<StatusDefinition> StatusDefinitions => Set<StatusDefinition>();
    public DbSet<DeedTypeMap> DeedTypeMaps => Set<DeedTypeMap>();
    public DbSet<DocumentFlag> DocumentFlags => Set<DocumentFlag>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<DocumentTeamMember> DocumentTeamMembers => Set<DocumentTeamMember>();
    public DbSet<SoftwareSyncLog> SoftwareSyncLogs => Set<SoftwareSyncLog>();
    public DbSet<SessionSettings> SessionSettings => Set<SessionSettings>();
    public DbSet<DeletePolicySettings> DeletePolicySettings => Set<DeletePolicySettings>();
    public DbSet<OcrCleanupRule> OcrCleanupRules => Set<OcrCleanupRule>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamUser> TeamUsers => Set<TeamUser>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();
    public DbSet<AppPolicy> AppPolicies => Set<AppPolicy>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<SoftwareFieldMap> SoftwareFieldMaps => Set<SoftwareFieldMap>();
    public DbSet<SoftwareClientConfig> SoftwareClientConfigs => Set<SoftwareClientConfig>();
    public DbSet<SoftwareImageCode> SoftwareImageCodes => Set<SoftwareImageCode>();
    public DbSet<SalesTabCode> SalesTabCodes => Set<SalesTabCode>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FullName).HasMaxLength(128);
            entity.Property(x => x.PhotoBlobPath).HasMaxLength(512);
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.EmailVerified).HasDefaultValue(true);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Users_Role",
                $"Role IN ('{AppRoles.Admin}','{AppRoles.Editor}','{AppRoles.Uploader}','{AppRoles.Viewer}')"));
        });

        modelBuilder.Entity<Client>(entity =>
        {
            entity.ToTable("Clients");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<UserClientAccess>(entity =>
        {
            entity.ToTable("UserClientAccess");
            entity.HasKey(x => new { x.UserId, x.ClientId });
            entity.HasOne(x => x.User)
                .WithMany(x => x.ClientAccess)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.ClientId);
        });

        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("EmailVerificationTokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.UserId);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EmailSettings>(entity =>
        {
            entity.ToTable("EmailSettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Mode).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FromName).HasMaxLength(128).IsRequired();
            entity.Property(x => x.FromAddress).HasMaxLength(256).IsRequired();
            entity.Property(x => x.LastFailReason).HasMaxLength(200);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_EmailSettings_Mode",
                $"Mode IN ('{EmailModes.SendGrid}','{EmailModes.Smtp}')"));
        });

        modelBuilder.Entity<FlagDefinition>(entity =>
        {
            entity.ToTable("FlagDefinitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Color).HasMaxLength(16).IsRequired();
        });

        modelBuilder.Entity<StatusDefinition>(entity =>
        {
            entity.ToTable("StatusDefinitions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.DisplayName).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Color).HasMaxLength(16).IsRequired();
            entity.Property(x => x.MapsTo).HasMaxLength(32).IsRequired(false).HasDefaultValue("");
            entity.Property(x => x.Kind).HasMaxLength(16).IsRequired(false).HasDefaultValue("");
            entity.Property(x => x.IsSeed).IsRequired(false).HasDefaultValue(false);
        });

        modelBuilder.Entity<DeedTypeMap>(entity =>
        {
            entity.ToTable("DeedTypeMaps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeedType).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.DeedType).IsUnique();
            entity.Property(x => x.SoftwareCode).HasMaxLength(32).IsRequired();
            entity.Property(x => x.FieldMapJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<Document>(entity =>
        {
            entity.ToTable("Documents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BlobPath).HasMaxLength(512).IsRequired();
            entity.Property(x => x.DiRawBlobPath).HasMaxLength(512);
            entity.Property(x => x.ErrorMessage).HasMaxLength(1024);
            entity.Property(x => x.DeedType).HasMaxLength(64);
            entity.Property(x => x.DocumentNumber).HasMaxLength(64);
            entity.Property(x => x.Volume).HasMaxLength(32);
            entity.Property(x => x.Page).HasMaxLength(32);
            entity.Property(x => x.Pid).HasMaxLength(64);
            entity.Property(x => x.MailingStreet).HasMaxLength(256);
            entity.Property(x => x.MailingCity).HasMaxLength(128);
            entity.Property(x => x.MailingState).HasMaxLength(32);
            entity.Property(x => x.MailingZip).HasMaxLength(16);
            var partiesComparer = new ValueComparer<List<string>?>(
                (left, right) => (left ?? new List<string>()).SequenceEqual(right ?? new List<string>()),
                names => (names ?? new List<string>()).Aggregate(0, (hash, name) => HashCode.Combine(hash, name.GetHashCode(StringComparison.Ordinal))),
                names => (names ?? new List<string>()).ToList());
            // Store type is string? so SQL Server does not GetString() on NULL
            // (existing rows after 20260909220000_DocumentListFields).
            var partyListConverter = new ValueConverter<List<string>?, string?>(
                names => PartyNames.ToJson(names),
                json => PartyNames.FromJson(json));
            entity.Property(x => x.Grantors)
                .HasMaxLength(4000)
                .HasConversion(partyListConverter)
                .IsRequired(false)
                .Metadata.SetValueComparer(partiesComparer);
            entity.Property(x => x.Grantees)
                .HasMaxLength(4000)
                .HasConversion(partyListConverter)
                .IsRequired(false)
                .Metadata.SetValueComparer(partiesComparer);
            entity.Property(x => x.ReviewStatus).HasMaxLength(32);
            entity.Property(x => x.LastSoftwareSyncStatus).HasMaxLength(16);
            entity.Property(x => x.LastSoftwareSyncDirection).HasMaxLength(16);
            entity.Property(x => x.LastSoftwareSyncFailReason).HasMaxLength(1024);
            entity.Property(x => x.SoftwareRecordId).HasMaxLength(64);
            entity.Property(x => x.SalesTabCode).HasMaxLength(32);
            entity.HasIndex(x => x.BlobPath).IsUnique();
            entity.HasIndex(x => x.ClientId);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.AssigneeUserId);
            entity.HasIndex(x => x.UpdatedAt);
            entity.HasQueryFilter(x => x.DeletedAt == null);
            entity.HasOne(x => x.Client)
                .WithMany(x => x.Documents)
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Assignee)
                .WithMany()
                .HasForeignKey(x => x.AssigneeUserId)
                .OnDelete(DeleteBehavior.SetNull);
            // SQL Server rejects a second cascade/set-null path from Users → Documents.
            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(x => x.Fields)
                .WithOne(x => x.Document)
                .HasForeignKey<DocumentFields>(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_Documents_Status",
                $"Status IN ('{DocumentStatuses.Queued}','{DocumentStatuses.Processing}','{DocumentStatuses.Ready}','{DocumentStatuses.Failed}')"));
        });

        modelBuilder.Entity<DocumentFields>(entity =>
        {
            entity.ToTable("DocumentFields");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.DocumentId).IsUnique();
            entity.Property(x => x.Grantor).HasMaxLength(256);
            entity.Property(x => x.Grantee).HasMaxLength(256);
            entity.Property(x => x.InstrumentDate).HasMaxLength(32);
            entity.Property(x => x.Consideration).HasMaxLength(64);
            entity.Property(x => x.ParcelId).HasMaxLength(64);
            entity.Property(x => x.LegalDescription).HasMaxLength(4000);
            entity.Property(x => x.Client).HasMaxLength(128);
            entity.Property(x => x.Notes).HasMaxLength(4000);
        });

        modelBuilder.Entity<DocumentFlag>(entity =>
        {
            entity.ToTable("DocumentFlags");
            entity.HasKey(x => new { x.DocumentId, x.FlagDefinitionId });
            entity.HasOne(x => x.Document)
                .WithMany(x => x.Flags)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Flag)
                .WithMany()
                .HasForeignKey(x => x.FlagDefinitionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentLink>(entity =>
        {
            entity.ToTable("DocumentLinks");
            entity.HasKey(x => new { x.SourceDocumentId, x.TargetDocumentId });
            entity.Property(x => x.Note).HasMaxLength(256);
            entity.HasOne(x => x.Source)
                .WithMany(x => x.OutgoingLinks)
                .HasForeignKey(x => x.SourceDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Target)
                .WithMany(x => x.IncomingLinks)
                .HasForeignKey(x => x.TargetDocumentId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DocumentTeamMember>(entity =>
        {
            entity.ToTable("DocumentTeamMembers");
            entity.HasKey(x => new { x.DocumentId, x.UserId });
            entity.HasOne(x => x.Document)
                .WithMany(x => x.Team)
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SoftwareSyncLog>(entity =>
        {
            entity.ToTable("SoftwareSyncLogs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Direction).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Detail).HasMaxLength(1024);
            entity.HasIndex(x => x.DocumentId);
            entity.HasOne(x => x.Document)
                .WithMany()
                .HasForeignKey(x => x.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.ToTable("Teams");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<TeamUser>(entity =>
        {
            entity.ToTable("TeamUsers");
            entity.HasKey(x => new { x.TeamId, x.UserId });
            entity.HasOne(x => x.Team)
                .WithMany(x => x.Members)
                .HasForeignKey(x => x.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.UserId);
        });

        modelBuilder.Entity<NotificationSettings>(entity =>
        {
            entity.ToTable("NotificationSettings");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<AppPolicy>(entity =>
        {
            entity.ToTable("AppPolicies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.SoftwareDefaultGroup).HasMaxLength(64);
            entity.Property(x => x.SoftwareFieldDefaultsJson).HasMaxLength(4000);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.ToTable("AppSettings");
            entity.HasKey(x => x.Key);
            entity.Property(x => x.Key).HasMaxLength(128);
            entity.Property(x => x.Value).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<SoftwareFieldMap>(entity =>
        {
            entity.ToTable("SoftwareFieldMaps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeedField).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SoftwareField).HasMaxLength(64).IsRequired();
            entity.Property(x => x.SoftwareGroup).HasMaxLength(64);
            entity.Property(x => x.DeedType).HasMaxLength(64);
            entity.HasIndex(x => new { x.DeedField, x.ClientId, x.DeedType });
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SoftwareClientConfig>(entity =>
        {
            entity.ToTable("SoftwareClientConfigs");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ClientId).IsUnique();
            entity.Property(x => x.Vendor).HasMaxLength(64);
            entity.Property(x => x.ApiUrl).HasMaxLength(256);
            entity.Property(x => x.GroupCode).HasMaxLength(32);
            entity.Property(x => x.ConsiderationThreshold).HasPrecision(18, 2);
            entity.Property(x => x.GranteeCombiner).HasMaxLength(32).HasDefaultValue(GranteeCombiners.First);
            entity.Property(x => x.LookupImageCode).HasMaxLength(32).HasDefaultValue("");
            entity.Property(x => x.PushImageCode).HasMaxLength(32).HasDefaultValue("");
            entity.Property(x => x.SalesRatioCode).HasMaxLength(32).HasDefaultValue("");
            entity.Property(x => x.FinanceCode).HasMaxLength(32).HasDefaultValue("");
            entity.Property(x => x.InstrumentCode).HasMaxLength(32).HasDefaultValue("");
            entity.Ignore(x => x.HasAnyReset);
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SoftwareImageCode>(entity =>
        {
            entity.ToTable("SoftwareImageCodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(32).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(64).IsRequired();
            entity.Property(x => x.DeedType).HasMaxLength(64);
            entity.Property(x => x.Code).HasDefaultValue("");
            entity.Property(x => x.Label).HasDefaultValue("");
            entity.Property(x => x.DeedType).HasDefaultValue("");
            entity.HasIndex(x => new { x.ClientId, x.Code }).IsUnique();
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SalesTabCode>(entity =>
        {
            entity.ToTable("SalesTabCodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Label).HasMaxLength(64).IsRequired();
            entity.Property(x => x.MinConsideration).HasPrecision(18, 2);
            entity.Property(x => x.MaxConsideration).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.ClientId, x.Code }).IsUnique();
            entity.HasOne(x => x.Client)
                .WithMany()
                .HasForeignKey(x => x.ClientId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SessionSettings>(entity =>
        {
            entity.ToTable("SessionSettings");
            entity.HasKey(x => x.Id);
        });

        modelBuilder.Entity<DeletePolicySettings>(entity =>
        {
            entity.ToTable("DeletePolicySettings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WhoCanDelete)
                .HasMaxLength(32)
                .HasDefaultValue(DeletePolicy.AllEditors);
            entity.Property(x => x.UpdatedByEmail).HasMaxLength(256);
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_DeletePolicySettings_WhoCanDelete",
                "WhoCanDelete IS NULL OR WhoCanDelete IN ('AllEditors','AdminOnly')"));
        });

        modelBuilder.Entity<OcrCleanupRule>(entity =>
        {
            entity.ToTable("OcrCleanupRules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            entity.Property(x => x.Value).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => new { x.Kind, x.Value }).IsUnique();
            entity.ToTable(t => t.HasCheckConstraint(
                "CK_OcrCleanupRules_Kind",
                $"Kind IN ('{OcrCleanupKinds.Trim}','{OcrCleanupKinds.Discard}')"));
        });
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(DocumentListFieldNullInterceptor.Instance);
    }
}
