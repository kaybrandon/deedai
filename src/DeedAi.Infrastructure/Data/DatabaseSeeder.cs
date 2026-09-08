using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeedAi.Infrastructure.Data;

public sealed class DatabaseSeeder(DeedAiDbContext db, IConfiguration configuration, ILogger<DatabaseSeeder> logger)
{
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UploaderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid AcmeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid NorthsideId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public const string SeedPassword = "ChangeMe!1";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var password = DependencyInjection.FirstValue(configuration, "AdminSeedPassword") ?? SeedPassword;
            db.Users.AddRange(
                new UserAccount
                {
                    Id = AdminId,
                    Email = "admin@bisconsultants.com",
                    DisplayName = "Admin",
                    Role = AppRoles.Admin,
                    PasswordHash = PasswordHasher.Hash(password)
                },
                new UserAccount
                {
                    Id = EditorId,
                    Email = "editor@bisconsultants.com",
                    DisplayName = "Alex",
                    Role = AppRoles.Editor,
                    PasswordHash = PasswordHasher.Hash(password)
                },
                new UserAccount
                {
                    Id = UploaderId,
                    Email = "uploader@bisconsultants.com",
                    DisplayName = "Sam",
                    Role = AppRoles.Uploader,
                    PasswordHash = PasswordHasher.Hash(password)
                },
                new UserAccount
                {
                    Id = ViewerId,
                    Email = "viewer@bisconsultants.com",
                    DisplayName = "Riley",
                    Role = AppRoles.Viewer,
                    PasswordHash = PasswordHasher.Hash(password)
                });
        }

        if (!await db.Clients.AnyAsync(cancellationToken))
        {
            db.Clients.AddRange(
                new Client { Id = AcmeId, Name = "Acme" },
                new Client { Id = NorthsideId, Name = "Northside" });
        }

        await db.SaveChangesAsync(cancellationToken);

        var seedDemo = string.Equals(configuration["Seed:DemoDocuments"], "true", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
        if (seedDemo && !await db.Documents.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            SeedDemoDocuments();
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Database seed complete.");
    }

    private void SeedDemoDocuments()
    {
        var now = new DateTimeOffset(2024, 8, 12, 12, 0, 0, TimeSpan.Zero);
        AddDoc("Deed_2024_0812.pdf", AcmeId, DocumentStatuses.Ready, now, EditorId, "deeds/demo/Deed_2024_0812.pdf",
            new DocumentFields
            {
                Id = Guid.NewGuid(),
                Grantor = "Jane Example",
                Grantee = "Acme Holdings LLC",
                InstrumentDate = "2024-08-12",
                Consideration = "250000",
                ParcelId = "12-345-678",
                Client = "Acme",
                Notes = "Example mapped fields — edit and Save.",
                IsDraft = false,
                UpdatedAt = now
            });
        AddDoc("Batch_44.pdf", NorthsideId, DocumentStatuses.Processing, now.AddDays(1), UploaderId, "deeds/demo/Batch_44.pdf", null);
        AddDoc("Scan_bad.pdf", AcmeId, DocumentStatuses.Failed, now.AddDays(2), null, "deeds/demo/Scan_bad.pdf", null);
        AddDoc("Queued_north.pdf", NorthsideId, DocumentStatuses.Queued, now.AddDays(3), null, "deeds/demo/Queued_north.pdf", null);
    }

    private void AddDoc(
        string name,
        Guid clientId,
        string status,
        DateTimeOffset at,
        Guid? assignee,
        string blobPath,
        DocumentFields? fields)
    {
        var id = Guid.NewGuid();
        db.Documents.Add(new Document
        {
            Id = id,
            Name = name,
            ClientId = clientId,
            Status = status,
            BlobPath = blobPath,
            AssigneeUserId = assignee,
            UploadedByUserId = UploaderId,
            CreatedAt = at,
            UpdatedAt = at,
            ErrorMessage = status == DocumentStatuses.Failed ? "OCR failed — Retry extract" : null
        });

        if (fields is not null)
        {
            fields.DocumentId = id;
            db.DocumentFields.Add(fields);
        }
    }
}
