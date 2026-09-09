using DeedAi.Domain;
using DeedAi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Infrastructure.Data;

public sealed class DeedAiDbContext(DbContextOptions<DeedAiDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentFields> DocumentFields => Set<DocumentFields>();
    public DbSet<UserClientAccess> UserClientAccess => Set<UserClientAccess>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<FlagDefinition> FlagDefinitions => Set<FlagDefinition>();
    public DbSet<StatusDefinition> StatusDefinitions => Set<StatusDefinition>();
    public DbSet<DeedTypeMap> DeedTypeMaps => Set<DeedTypeMap>();
    public DbSet<DocumentFlag> DocumentFlags => Set<DocumentFlag>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<DocumentTeamMember> DocumentTeamMembers => Set<DocumentTeamMember>();
    public DbSet<SoftwareSyncLog> SoftwareSyncLogs => Set<SoftwareSyncLog>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamUser> TeamUsers => Set<TeamUser>();
    public DbSet<NotificationSettings> NotificationSettings => Set<NotificationSettings>();

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
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
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
            entity.Property(x => x.ReviewStatus).HasMaxLength(32);
            entity.Property(x => x.LastSoftwareSyncStatus).HasMaxLength(16);
            entity.Property(x => x.LastSoftwareSyncDirection).HasMaxLength(16);
            entity.Property(x => x.LastSoftwareSyncFailReason).HasMaxLength(1024);
            entity.Property(x => x.SoftwareRecordId).HasMaxLength(64);
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
            entity.HasOne(x => x.UploadedBy)
                .WithMany()
                .HasForeignKey(x => x.UploadedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
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
    }
}
