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
    public static readonly Guid NeedsReviewFlagId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid MissingParcelFlagId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid LegalHoldFlagId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static readonly Guid ReviewTeamId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    public const string SeedPassword = "ChangeMe!1";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await db.Database.MigrateAsync(cancellationToken);
        }

        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var password = DependencyInjection.FirstValue(configuration, "AdminSeedPassword") ?? SeedPassword;
            var now = DateTimeOffset.UtcNow;
            db.Users.AddRange(
                new UserAccount
                {
                    Id = AdminId,
                    Email = "admin@bisconsultants.com",
                    DisplayName = "Admin",
                    Role = AppRoles.Admin,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = EditorId,
                    Email = "editor@bisconsultants.com",
                    DisplayName = "Alex",
                    Role = AppRoles.Editor,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = UploaderId,
                    Email = "uploader@bisconsultants.com",
                    DisplayName = "Sam",
                    Role = AppRoles.Uploader,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = ViewerId,
                    Email = "viewer@bisconsultants.com",
                    DisplayName = "Riley",
                    Role = AppRoles.Viewer,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    CreatedAt = now
                });
        }

        if (!await db.Clients.AnyAsync(cancellationToken))
        {
            db.Clients.AddRange(
                new Client { Id = AcmeId, Name = "Acme", IsActive = true },
                new Client { Id = NorthsideId, Name = "Northside", IsActive = true });
        }

        await db.SaveChangesAsync(cancellationToken);
        await SeedClientAccessAsync(cancellationToken);
        await SeedSettingsAsync(cancellationToken);
        await SeedTeamsAsync(cancellationToken);
        await SeedNotificationsAsync(cancellationToken);

        var seedDemo = string.Equals(configuration["Seed:DemoDocuments"], "true", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
        if (seedDemo && !await db.Documents.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            SeedDemoDocuments();
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Database seed complete.");
    }

    private async Task SeedClientAccessAsync(CancellationToken cancellationToken)
    {
        if (await db.UserClientAccess.AnyAsync(cancellationToken))
        {
            return;
        }

        var userIds = await db.Users.Select(x => x.Id).ToListAsync(cancellationToken);
        var clientIds = await db.Clients.Select(x => x.Id).ToListAsync(cancellationToken);
        foreach (var userId in userIds)
        {
            foreach (var clientId in clientIds)
            {
                db.UserClientAccess.Add(new UserClientAccess { UserId = userId, ClientId = clientId });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSettingsAsync(CancellationToken cancellationToken)
    {
        if (!await db.FlagDefinitions.AnyAsync(cancellationToken))
        {
            db.FlagDefinitions.AddRange(
                new FlagDefinition { Id = NeedsReviewFlagId, Name = "Needs review", Color = "#3730a3", SortOrder = 1 },
                new FlagDefinition { Id = MissingParcelFlagId, Name = "Missing parcel", Color = "#b45309", SortOrder = 2 },
                new FlagDefinition { Id = LegalHoldFlagId, Name = "Legal hold", Color = "#b91c1c", SortOrder = 3 });
        }

        if (!await db.StatusDefinitions.AnyAsync(cancellationToken))
        {
            db.StatusDefinitions.AddRange(
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Code = DocumentStatuses.Queued, DisplayName = "Queued", Color = "#374151", IsSystem = true, SortOrder = 1 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Code = DocumentStatuses.Processing, DisplayName = "Processing", Color = "#3730a3", IsSystem = true, SortOrder = 2 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Code = DocumentStatuses.Ready, DisplayName = "Ready", Color = "#166534", IsSystem = true, SortOrder = 3 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Code = DocumentStatuses.Failed, DisplayName = "Failed", Color = "#b91c1c", IsSystem = true, SortOrder = 4 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Code = "NeedsReview", DisplayName = "Needs review", Color = "#1d4ed8", IsSystem = false, SortOrder = 5 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Code = "Approved", DisplayName = "Approved", Color = "#047857", IsSystem = false, SortOrder = 6 });
        }

        if (!await db.DeedTypeMaps.AnyAsync(cancellationToken))
        {
            db.DeedTypeMaps.AddRange(
                new DeedTypeMap
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                    DeedType = "Warranty Deed",
                    SoftwareCode = "WD",
                    FieldMapJson = """{"grantor":"GrantorName","grantee":"GranteeName","parcelId":"ParcelNumber","consideration":"Consideration"}"""
                },
                new DeedTypeMap
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    DeedType = "Quitclaim Deed",
                    SoftwareCode = "QCD",
                    FieldMapJson = """{"grantor":"GrantorName","grantee":"GranteeName","parcelId":"ParcelNumber"}"""
                },
                new DeedTypeMap
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                    DeedType = "Special Warranty Deed",
                    SoftwareCode = "SWD",
                    FieldMapJson = """{"grantor":"GrantorName","grantee":"GranteeName","parcelId":"ParcelNumber","instrumentDate":"InstrumentDate"}"""
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTeamsAsync(CancellationToken cancellationToken)
    {
        if (await db.Teams.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Teams.Add(new Team { Id = ReviewTeamId, Name = "Review", IsActive = true });
        db.TeamUsers.AddRange(
            new TeamUser { TeamId = ReviewTeamId, UserId = EditorId },
            new TeamUser { TeamId = ReviewTeamId, UserId = UploaderId });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedNotificationsAsync(CancellationToken cancellationToken)
    {
        if (await db.NotificationSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        db.NotificationSettings.Add(new NotificationSettings
        {
            Id = NotificationSettings.SingletonId,
            Enabled = true,
            NotifyUploader = false,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private void SeedDemoDocuments()
    {
        var now = new DateTimeOffset(2024, 8, 12, 12, 0, 0, TimeSpan.Zero);
        var readyId = AddDoc("Deed_2024_0812.pdf", AcmeId, DocumentStatuses.Ready, now, EditorId, "deeds/demo/Deed_2024_0812.pdf", "Warranty Deed",
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
        var failedId = AddDoc("Scan_bad.pdf", AcmeId, DocumentStatuses.Failed, now.AddDays(2), null, "deeds/demo/Scan_bad.pdf", "Quitclaim Deed", null);
        AddDoc("Batch_44.pdf", NorthsideId, DocumentStatuses.Processing, now.AddDays(1), UploaderId, "deeds/demo/Batch_44.pdf", null, null);
        AddDoc("Queued_north.pdf", NorthsideId, DocumentStatuses.Queued, now.AddDays(3), null, "deeds/demo/Queued_north.pdf", null, null);

        db.DocumentFlags.Add(new DocumentFlag { DocumentId = failedId, FlagDefinitionId = MissingParcelFlagId });
        db.DocumentFlags.Add(new DocumentFlag { DocumentId = readyId, FlagDefinitionId = NeedsReviewFlagId });
        db.DocumentTeamMembers.Add(new DocumentTeamMember { DocumentId = readyId, UserId = EditorId });
        db.DocumentTeamMembers.Add(new DocumentTeamMember { DocumentId = readyId, UserId = UploaderId });
        db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = readyId, TargetDocumentId = failedId, Note = "Related scan" });
    }

    private Guid AddDoc(
        string name,
        Guid clientId,
        string status,
        DateTimeOffset at,
        Guid? assignee,
        string blobPath,
        string? deedType,
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
            DeedType = deedType,
            ErrorMessage = status == DocumentStatuses.Failed ? "OCR failed — Retry extract" : null
        });

        if (fields is not null)
        {
            fields.DocumentId = id;
            db.DocumentFields.Add(fields);
        }

        return id;
    }
}
