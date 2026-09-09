using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Export;
using DeedAi.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DeedAi.Infrastructure.Data;

public sealed class DatabaseSeeder(
    DeedAiDbContext db,
    IConfiguration configuration,
    ILogger<DatabaseSeeder> logger,
    IBlobStorage? blobs = null)
{
    public static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid EditorId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UploaderId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ViewerId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid AcmeId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid NorthsideId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid NeedsReviewFlagId = ReviewWorkflow.NeedsReviewFlagId;
    public static readonly Guid MissingParcelFlagId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid LegalHoldFlagId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    public static readonly Guid ReviewTeamId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");

    public const string SeedPassword = "ChangeMe!1";
    public const string AdminEmail = "admin@bisconsultants.com";
    public const string EditorEmail = "editor@bisconsultants.com";
    public const string UploaderEmail = "uploader@bisconsultants.com";
    public const string ViewerEmail = "viewer@bisconsultants.com";

    public static readonly IReadOnlyList<string> SeedEmails =
    [
        AdminEmail,
        EditorEmail,
        UploaderEmail,
        ViewerEmail
    ];

    public static string? ReadAdminSeedPassword(IConfiguration configuration) =>
        DependencyInjection.FirstValue(
            configuration,
            "AdminSeedPassword",
            "Admin:SeedPassword",
            "Admin__SeedPassword",
            "Seed:AdminPassword");

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }
        else
        {
            await MigrationRunner.MigrateAsync(db, logger, cancellationToken);
        }

        var password = ReadAdminSeedPassword(configuration) ?? SeedPassword;
        if (!await db.Users.AnyAsync(cancellationToken))
        {
            var now = DateTimeOffset.UtcNow;
            db.Users.AddRange(
                new UserAccount
                {
                    Id = AdminId,
                    Email = AdminEmail,
                    DisplayName = "Admin",
                    FullName = "Admin User",
                    Role = AppRoles.Admin,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = EditorId,
                    Email = EditorEmail,
                    DisplayName = "Alex",
                    FullName = "Alex Editor",
                    Role = AppRoles.Editor,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = UploaderId,
                    Email = UploaderEmail,
                    DisplayName = "Sam",
                    FullName = "Sam Uploader",
                    Role = AppRoles.Uploader,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = now
                },
                new UserAccount
                {
                    Id = ViewerId,
                    Email = ViewerEmail,
                    DisplayName = "Riley",
                    FullName = "Riley Viewer",
                    Role = AppRoles.Viewer,
                    PasswordHash = PasswordHasher.Hash(password),
                    IsActive = true,
                    EmailVerified = true,
                    CreatedAt = now
                });
        }
        else if (!string.IsNullOrWhiteSpace(ReadAdminSeedPassword(configuration)))
        {
            await SyncSeedPasswordsAsync(password, cancellationToken);
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
        await SeedSoftwareParityAsync(cancellationToken);
        await SeedSessionAsync(cancellationToken);
        await SeedOcrCleanupAsync(cancellationToken);
        await SeedSwaggerSettingAsync(cancellationToken);
        await SeedEmailSettingsAsync(cancellationToken);

        var seedDemo = string.Equals(configuration["Seed:DemoDocuments"], "true", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
        if (seedDemo && !await db.Documents.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            SeedDemoDocuments();
            await db.SaveChangesAsync(cancellationToken);
        }

        await EnsureReviewConsistencyAsync(cancellationToken);
        await EnsureDemoPdfsAsync(cancellationToken);

        logger.LogInformation("Database seed complete.");
    }

    private async Task SyncSeedPasswordsAsync(string password, CancellationToken cancellationToken)
    {
        var users = await db.Users
            .Where(x => SeedEmails.Contains(x.Email))
            .ToListAsync(cancellationToken);
        var updated = 0;
        foreach (var user in users)
        {
            var isAdmin = string.Equals(user.Email, AdminEmail, StringComparison.OrdinalIgnoreCase);
            var stillDefault = PasswordHasher.Verify(SeedPassword, user.PasswordHash);
            if (!isAdmin && !stillDefault && !PasswordHasher.Verify(password, user.PasswordHash))
            {
                // Demo users whose password was changed in-app are left alone.
                continue;
            }

            if (PasswordHasher.Verify(password, user.PasswordHash))
            {
                continue;
            }

            user.PasswordHash = PasswordHasher.Hash(password);
            updated++;
            logger.LogInformation("Updated seed password hash for {Email}.", user.Email);
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
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
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Code = DocumentStatuses.Queued, DisplayName = "Queued", Color = "#C5CED6", IsSystem = true, SortOrder = 1 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Code = DocumentStatuses.Processing, DisplayName = "Processing", Color = "#E8C96A", IsSystem = true, SortOrder = 2 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Code = DocumentStatuses.Ready, DisplayName = "Ready", Color = "#D8F0EA", IsSystem = true, SortOrder = 3 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Code = DocumentStatuses.Failed, DisplayName = "Failed", Color = "#F5D6D3", IsSystem = true, SortOrder = 4 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Code = ReviewWorkflow.NeedsReview, DisplayName = ReviewWorkflow.NeedsReviewFlagName, Color = "#C5E8E4", IsSystem = false, SortOrder = 5 },
                new StatusDefinition { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Code = ReviewWorkflow.Approved, DisplayName = "Approved", Color = "#D8F0EA", IsSystem = false, SortOrder = 6 });
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

    private async Task SeedSoftwareParityAsync(CancellationToken cancellationToken)
    {
        if (!await db.AppPolicies.AnyAsync(cancellationToken))
        {
            db.AppPolicies.Add(new AppPolicy
            {
                Id = AppPolicy.SingletonId,
                SoftwarePushEnabled = true,
                SoftwareDefaultGroup = "Property",
                SoftwareFieldDefaultsJson = """{"consideration":"0"}""",
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }

        if (!await db.SoftwareFieldMaps.AnyAsync(cancellationToken))
        {
            db.SoftwareFieldMaps.AddRange(
                Map(DeedFields.Grantor, "GrantorName", "Parties", 1),
                Map(DeedFields.Grantee, "GranteeName", "Parties", 2),
                Map(DeedFields.ParcelId, "ParcelNumber", "Property", 3),
                Map(DeedFields.InstrumentDate, "InstrumentDate", "Property", 4),
                Map(DeedFields.Consideration, "Consideration", "Consideration", 5),
                Map(DeedFields.Client, "ClientName", "Property", 6),
                Map(DeedFields.Notes, "Notes", "Notes", 7));
        }

        if (!await db.SoftwareClientConfigs.AnyAsync(cancellationToken))
        {
            db.SoftwareClientConfigs.AddRange(
                new SoftwareClientConfig
                {
                    Id = Guid.Parse("51000000-0000-0000-0000-000000000001"),
                    ClientId = AcmeId,
                    Vendor = "LegacySoft",
                    ApiUrl = "https://software.example.test/api",
                    GroupCode = "ACME",
                    RemoveLeadingZeros = true,
                    DateLabelDepth = 2,
                    DisplaySalesTab = true,
                    SendConsideration = true,
                    ConsiderationThreshold = 1,
                    ResetExemptions = false,
                    ResetSupplementYear = false,
                    ResetSalesLetter = false,
                    ResetSalesTab = false,
                    ResetAgents = false,
                    ResetMortgageCodes = false
                },
                new SoftwareClientConfig
                {
                    Id = Guid.Parse("51000000-0000-0000-0000-000000000002"),
                    ClientId = NorthsideId,
                    Vendor = "LegacySoft",
                    ApiUrl = "https://software.example.test/api",
                    GroupCode = "NORTH",
                    RemoveLeadingZeros = false,
                    DateLabelDepth = 1,
                    DisplaySalesTab = false,
                    SendConsideration = true,
                    ConsiderationThreshold = 0
                });
        }

        if (!await db.SalesTabCodes.AnyAsync(cancellationToken))
        {
            db.SalesTabCodes.AddRange(
                new SalesTabCode
                {
                    Id = Guid.Parse("52000000-0000-0000-0000-000000000001"),
                    Code = "QS",
                    Label = "Qualified sale",
                    MinConsideration = 1,
                    MaxConsideration = null,
                    IsActive = true,
                    SortOrder = 1
                },
                new SalesTabCode
                {
                    Id = Guid.Parse("52000000-0000-0000-0000-000000000002"),
                    Code = "NS",
                    Label = "Nominal sale",
                    MinConsideration = 0,
                    MaxConsideration = 0.99m,
                    IsActive = true,
                    SortOrder = 2
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static SoftwareFieldMap Map(string deedField, string softwareField, string group, int sort) =>
        new()
        {
            Id = Guid.Parse($"41000000-0000-0000-0000-00000000000{sort}"),
            DeedField = deedField,
            SoftwareField = softwareField,
            SoftwareGroup = group,
            IsActive = true,
            SortOrder = sort
        };

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

    private async Task SeedSessionAsync(CancellationToken cancellationToken)
    {
        if (await db.SessionSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        var configured = DependencyInjection.FirstValue(
            configuration,
            "SessionIdleTimeoutMinutes",
            "Session:IdleTimeoutMinutes");
        var minutes = SessionSettings.DefaultIdleTimeoutMinutes;
        if (int.TryParse(configured, out var parsed))
        {
            minutes = SessionSettings.Clamp(parsed);
        }

        db.SessionSettings.Add(new SessionSettings
        {
            Id = SessionSettings.SingletonId,
            IdleTimeoutMinutes = minutes,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedOcrCleanupAsync(CancellationToken cancellationToken)
    {
        if (await db.OcrCleanupRules.AnyAsync(cancellationToken))
        {
            return;
        }

        var trim = new[] { "\"", "'", ",", ".", ";", ":", "(", ")", "[", "]", "*", "#" };
        var discard = new[] { "N/A", "NA", "NONE", "UNKNOWN", "NULL", "TBD", "--" };
        var order = 1;
        foreach (var value in trim)
        {
            db.OcrCleanupRules.Add(new OcrCleanupRule
            {
                Id = Guid.NewGuid(),
                Kind = OcrCleanupKinds.Trim,
                Value = value,
                IsActive = true,
                SortOrder = order++
            });
        }

        foreach (var value in discard)
        {
            db.OcrCleanupRules.Add(new OcrCleanupRule
            {
                Id = Guid.NewGuid(),
                Kind = OcrCleanupKinds.Discard,
                Value = value,
                IsActive = true,
                SortOrder = order++
            });
        }

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
                LegalDescription = "Lot 12, Block 4, Acme Subdivision",
                Client = "Acme",
                Notes = "Example mapped fields — edit and Save.",
                IsDraft = false,
                UpdatedAt = now
            },
            ReviewWorkflow.NeedsReview);
        var failedId = AddDoc("Scan_bad.pdf", AcmeId, DocumentStatuses.Failed, now.AddDays(2), null, "deeds/demo/Scan_bad.pdf", "Quitclaim Deed", null);
        AddDoc("Batch_44.pdf", NorthsideId, DocumentStatuses.Processing, now.AddDays(1), UploaderId, "deeds/demo/Batch_44.pdf", null, null);
        AddDoc("Queued_north.pdf", NorthsideId, DocumentStatuses.Queued, now.AddDays(3), null, "deeds/demo/Queued_north.pdf", null, null);

        db.DocumentFlags.Add(new DocumentFlag { DocumentId = failedId, FlagDefinitionId = MissingParcelFlagId });
        db.DocumentFlags.Add(new DocumentFlag { DocumentId = readyId, FlagDefinitionId = NeedsReviewFlagId });
        db.DocumentTeamMembers.Add(new DocumentTeamMember { DocumentId = readyId, UserId = EditorId });
        db.DocumentTeamMembers.Add(new DocumentTeamMember { DocumentId = readyId, UserId = UploaderId });
        db.DocumentLinks.Add(new DocumentLink { SourceDocumentId = readyId, TargetDocumentId = failedId, Note = "Related scan" });
    }

    private async Task EnsureReviewConsistencyAsync(CancellationToken cancellationToken)
    {
        var flagId = await db.FlagDefinitions
            .Where(x => x.Id == NeedsReviewFlagId || x.Name == ReviewWorkflow.NeedsReviewFlagName)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (flagId is null)
        {
            return;
        }

        var flaggedIds = await db.DocumentFlags
            .Where(x => x.FlagDefinitionId == flagId)
            .Select(x => x.DocumentId)
            .ToListAsync(cancellationToken);
        var reviewIds = await db.Documents.IgnoreQueryFilters()
            .Where(x => x.ReviewStatus == ReviewWorkflow.NeedsReview)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        var ids = flaggedIds.Union(reviewIds).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var documents = await db.Documents.IgnoreQueryFilters()
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            var hasFlag = flaggedIds.Contains(document.Id);
            if (hasFlag && !ReviewWorkflow.IsNeedsReview(document.ReviewStatus))
            {
                document.ReviewStatus = ReviewWorkflow.NeedsReview;
            }
            else if (!hasFlag && ReviewWorkflow.IsNeedsReview(document.ReviewStatus))
            {
                db.DocumentFlags.Add(new DocumentFlag { DocumentId = document.Id, FlagDefinitionId = flagId.Value });
                flaggedIds.Add(document.Id);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureDemoPdfsAsync(CancellationToken cancellationToken)
    {
        if (blobs is null)
        {
            return;
        }

        var documents = await db.Documents.IgnoreQueryFilters()
            .Include(x => x.Client)
            .Include(x => x.Fields)
            .Include(x => x.Flags).ThenInclude(x => x.Flag)
            .Where(x => x.BlobPath.StartsWith(DemoDeedPdf.BlobPrefix))
            .ToListAsync(cancellationToken);
        foreach (var document in documents)
        {
            await DemoDeedPdf.EnsureUploadedAsync(blobs, document, cancellationToken);
        }
    }

    private Guid AddDoc(
        string name,
        Guid clientId,
        string status,
        DateTimeOffset at,
        Guid? assignee,
        string blobPath,
        string? deedType,
        DocumentFields? fields,
        string? reviewStatus = null)
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
            DocumentNumber = fields?.ParcelId is not null ? "2024-0812" : null,
            Volume = fields?.ParcelId is not null ? "142" : null,
            Page = fields?.ParcelId is not null ? "18" : null,
            Pid = fields?.ParcelId,
            MailingStreet = fields?.ParcelId is not null ? "100 Main St" : null,
            MailingCity = fields?.ParcelId is not null ? "Springfield" : null,
            MailingState = fields?.ParcelId is not null ? "IL" : null,
            MailingZip = fields?.ParcelId is not null ? "62701" : null,
            Grantors = fields?.Grantor is { Length: > 0 } grantor ? [grantor] : [],
            Grantees = fields?.Grantee is { Length: > 0 } grantee ? [grantee] : [],
            ReviewStatus = reviewStatus,
            ErrorMessage = status == DocumentStatuses.Failed ? "OCR failed — Retry extract" : null
        });

        if (fields is not null)
        {
            fields.DocumentId = id;
            db.DocumentFields.Add(fields);
        }

        return id;
    }

    private async Task SeedEmailSettingsAsync(CancellationToken cancellationToken)
    {
        if (await db.EmailSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        var fromEmail = DependencyInjection.FirstValue(
                            configuration,
                            "SendGridFromEmail",
                            "SendGrid:FromEmail",
                            "SmtpFromEmail")
                        ?? "noreply@bisconsultants.com";
        var fromName = DependencyInjection.FirstValue(
                           configuration,
                           "SendGridFromName",
                           "SendGrid:FromName")
                       ?? "Deed AI";
        db.EmailSettings.Add(new EmailSettings
        {
            Id = EmailSettings.SingletonId,
            Mode = EmailModes.SendGrid,
            FromName = fromName,
            FromAddress = fromEmail,
            VerifyRequired = true,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedSwaggerSettingAsync(CancellationToken cancellationToken)
    {
        if (await db.AppSettings.AnyAsync(x => x.Key == AppSetting.SwaggerEnabledKey, cancellationToken))
        {
            return;
        }

        db.AppSettings.Add(new AppSetting
        {
            Key = AppSetting.SwaggerEnabledKey,
            Value = ShouldSeedSwaggerOn() ? bool.TrueString : bool.FalseString,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    private bool ShouldSeedSwaggerOn()
    {
        var env = configuration["ASPNETCORE_ENVIRONMENT"]
                  ?? configuration["DOTNET_ENVIRONMENT"]
                  ?? string.Empty;
        if (string.Equals(env, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(configuration["Swagger:Enabled"], "true", StringComparison.OrdinalIgnoreCase);
    }
}
