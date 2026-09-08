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
    }
}
