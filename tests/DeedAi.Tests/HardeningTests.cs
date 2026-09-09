using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Health;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Infrastructure.Security;
using DeedAi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeedAi.Tests;

public sealed class HardeningTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public HardeningTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Documents_to_users_fks_are_sql_server_safe()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var errors = DocumentUserFkRules.Validate(db.Model);
        Assert.Empty(errors);
    }

    [Fact]
    public void Phase3_sql_is_idempotent_and_uploadedby_is_no_action()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Phase3).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new Phase3(), [builder]);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("IF COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF OBJECT_ID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ON DELETE NO ACTION", sql, StringComparison.Ordinal);
        Assert.Contains("FK_Documents_Users_UploadedByUserId", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("FK_Documents_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FK_Documents_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase4_sql_server_hardening_tables_are_idempotent()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeUp<Phase4Hardening>(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("IF OBJECT_ID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("OcrCleanupRules", sql, StringComparison.Ordinal);
        Assert.Contains("SessionSettings", sql, StringComparison.Ordinal);
        Assert.Contains("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void All_ef_migrations_are_discoverable_including_phase4()
    {
        var migrationTypes = typeof(DeedAiDbContext).Assembly.GetTypes()
            .Where(t => typeof(Migration).IsAssignableFrom(t) && t is { IsAbstract: false, IsGenericType: false })
            .ToList();
        Assert.NotEmpty(migrationTypes);

        foreach (var type in migrationTypes)
        {
            Assert.True(
                type.GetCustomAttribute<MigrationAttribute>() is not null,
                $"{type.Name} is missing [Migration] (Designer) and would be skipped on Azure SQL.");
            Assert.True(
                type.GetCustomAttribute<DbContextAttribute>() is not null,
                $"{type.Name} is missing [DbContext] (Designer) and would be skipped on Azure SQL.");
            Assert.True(
                type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic) is not null,
                $"{type.Name} is missing BuildTargetModel — add a *.Designer.cs like the other migrations.");
        }

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var assembly = db.GetService<IMigrationsAssembly>();
        var discovered = assembly.Migrations.Keys.ToList();
        Assert.Contains(Phase4SqlServerSchema.Phase4HardeningId, discovered);
        Assert.Contains(Phase4SqlServerSchema.Phase4AId, discovered);
        Assert.Contains(Phase4SqlServerSchema.Phase4AQaId, discovered);
        Assert.Contains(Phase4SqlServerSchema.Phase4AzureRepairId, discovered);
        Assert.Contains("20260909140000_Phase41SwaggerHelp", discovered);
        Assert.Contains("20260909160000_Phase45UsersIdentity", discovered);
        Assert.Contains(Phase48AdminEmail.Id, discovered);
        Assert.Contains("20260909190000_Phase491RemovePropertyDefaults", discovered);
        Assert.DoesNotContain("20260909180000_Phase491RemovePropertyDefaults", discovered);
        Assert.DoesNotContain("20260909120000_Phase41SwaggerHelp", discovered);
        Assert.DoesNotContain("20260909151048_Phase48AdminEmail", discovered);
        Assert.Equal(typeof(Phase4A), assembly.Migrations[Phase4SqlServerSchema.Phase4AId].AsType());
        Assert.Equal(typeof(Phase4AQa), assembly.Migrations[Phase4SqlServerSchema.Phase4AQaId].AsType());
        Assert.Equal(typeof(Phase4AzureRepair), assembly.Migrations[Phase4SqlServerSchema.Phase4AzureRepairId].AsType());
        Assert.Equal(typeof(Phase41SwaggerHelp), assembly.Migrations["20260909140000_Phase41SwaggerHelp"].AsType());
        Assert.Equal(typeof(Phase45UsersIdentity), assembly.Migrations["20260909160000_Phase45UsersIdentity"].AsType());
        Assert.Equal(typeof(Phase48AdminEmail), assembly.Migrations[Phase48AdminEmail.Id].AsType());

        var ids = db.Database.GetMigrations().ToList();
        Assert.Contains(Phase4SqlServerSchema.Phase4AId, ids);
        Assert.Contains(Phase4SqlServerSchema.Phase4AQaId, ids);
        Assert.Contains("20260909140000_Phase41SwaggerHelp", ids);
        Assert.Contains("20260909160000_Phase45UsersIdentity", ids);
        Assert.Contains(Phase48AdminEmail.Id, ids);
        Assert.Contains("20260909190000_Phase491RemovePropertyDefaults", ids);
        Assert.True(
            string.CompareOrdinal(Phase4SqlServerSchema.Phase4AId, Phase4SqlServerSchema.Phase4AQaId) < 0);
        Assert.True(
            string.CompareOrdinal(Phase4SqlServerSchema.Phase4AQaId, Phase4SqlServerSchema.Phase4AzureRepairId) < 0);
        Assert.True(
            string.CompareOrdinal(Phase4SqlServerSchema.Phase4AzureRepairId, "20260909140000_Phase41SwaggerHelp") < 0);
        Assert.True(
            string.CompareOrdinal("20260909140000_Phase41SwaggerHelp", "20260909160000_Phase45UsersIdentity") < 0);
        Assert.True(
            string.CompareOrdinal("20260909160000_Phase45UsersIdentity", Phase48AdminEmail.Id) < 0);
        Assert.True(
            string.CompareOrdinal(Phase48AdminEmail.Id, "20260909190000_Phase491RemovePropertyDefaults") < 0);
    }

    [Fact]
    public void Phase4_sql_server_up_is_guarded_sql_only()
    {
        foreach (var type in new[] { typeof(Phase4A), typeof(Phase4AQa), typeof(Phase4Hardening), typeof(Phase4AzureRepair) })
        {
            var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
            InvokeUp(type, builder);
            Assert.Empty(builder.Operations.OfType<CreateTableOperation>());
            Assert.Empty(builder.Operations.OfType<AddColumnOperation>());
            Assert.NotEmpty(builder.Operations.OfType<SqlOperation>());
        }
    }

    [Fact]
    public void Phase4_sql_covers_required_objects_and_is_safe_to_reinvoke()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeUp<Phase4AzureRepair>(builder);
        InvokeUp<Phase4AzureRepair>(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("IF OBJECT_ID", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF COL_LENGTH", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("IF EXISTS", sql, StringComparison.OrdinalIgnoreCase);

        foreach (var table in Phase4SqlServerSchema.RequiredTables)
        {
            Assert.Contains(table, sql, StringComparison.Ordinal);
            Assert.Contains($"IF OBJECT_ID(N'dbo.{table}', N'U') IS NULL", sql, StringComparison.Ordinal);
        }

        Assert.Contains("IF COL_LENGTH(N'dbo.Documents', N'SalesTabCode') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ResetExemptions", sql, StringComparison.Ordinal);
        Assert.Contains("ResetSupplementYear", sql, StringComparison.Ordinal);
        Assert.Contains("ResetSalesLetter", sql, StringComparison.Ordinal);
        Assert.Contains("ResetSalesTab", sql, StringComparison.Ordinal);
        Assert.Contains("ResetAgents", sql, StringComparison.Ordinal);
        Assert.Contains("ResetMortgageCodes", sql, StringComparison.Ordinal);
        Assert.Contains("SoftwareFieldMaps", sql, StringComparison.Ordinal);
        Assert.Contains("FK_Documents_Users_UploadedByUserId", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE NO ACTION", sql, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "FK_Documents_Users_UploadedByUserId] FOREIGN KEY ([UploadedByUserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE",
            sql,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE TABLE [Clients]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE [Clients]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE [Documents]", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("County", sql, StringComparison.Ordinal);
        Assert.All(builder.Operations, op => Assert.IsType<SqlOperation>(op));
    }

    [Fact]
    public void Phase4_repair_is_a_no_op_on_sqlite()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        InvokeUp<Phase4AzureRepair>(builder);
        Assert.Empty(builder.Operations);

        InvokeUp<Phase4A>(builder);
        Assert.Contains(builder.Operations.OfType<CreateTableOperation>(), x => x.Name == "AppPolicies");
        Assert.Contains(builder.Operations.OfType<CreateTableOperation>(), x => x.Name == "PropertyDefaults");
        Assert.Contains(builder.Operations.OfType<CreateTableOperation>(), x => x.Name == "SoftwareFieldMaps");
    }

    [Fact]
    public async Task Public_health_is_shallow_and_admin_health_has_checks_without_secrets()
    {
        var anon = _factory.CreateJsonClient();
        var shallow = await anon.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, shallow.StatusCode);
        var shallowBody = await shallow.Content.ReadAsStringAsync();
        using (var json = JsonDocument.Parse(shallowBody))
        {
            Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
            Assert.Equal("Deed AI", json.RootElement.GetProperty("product").GetString());
            Assert.False(json.RootElement.TryGetProperty("checks", out _));
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/health/detail")).StatusCode);

        var viewer = await Authed("viewer@bisconsultants.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/health/detail")).StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var detail = await admin.GetAsync("/api/health/detail");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await detail.Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        using var detailed = JsonDocument.Parse(body);
        var overall = detailed.RootElement.GetProperty("status").GetString();
        Assert.True(overall is "ok" or "degraded");
        var checks = detailed.RootElement.GetProperty("checks");
        foreach (var name in new[] { "sql", "storage", "queue", "blob", "documentIntelligence", "ocrPipeline" })
        {
            var check = checks.GetProperty(name);
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("status").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(check.GetProperty("mode").GetString()));
        }
        Assert.True(checks.GetProperty("sql").GetProperty("reachable").GetBoolean());
        Assert.True(checks.GetProperty("storage").GetProperty("reachable").GetBoolean());
        Assert.True(checks.GetProperty("queue").GetProperty("reachable").GetBoolean());
        Assert.Equal("Pass", checks.GetProperty("blob").GetProperty("detail").GetString());
        Assert.False(checks.GetProperty("documentIntelligence").GetProperty("configured").GetBoolean());
        Assert.True(detailed.RootElement.TryGetProperty("ocrQueue", out var ocrQueue));
        Assert.True(ocrQueue.TryGetProperty("depth", out _));
        Assert.True(ocrQueue.TryGetProperty("oldestWaitingAgeSeconds", out _));
        Assert.True(ocrQueue.TryGetProperty("poisonCount", out _));
        Assert.True(ocrQueue.TryGetProperty("failedCount", out _));
        Assert.True(ocrQueue.TryGetProperty("lastDiSuccessAt", out _));
        Assert.True(ocrQueue.TryGetProperty("lastDiFailAt", out _));
    }

    [Fact]
    public async Task AdminSeedPassword_updates_existing_admin_hash_without_logging_secret()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-reseed-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var first = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeedPassword"] = "FirstSeed!1"
        }).Build();
        var logger = new RecordingLogger();
        await new DatabaseSeeder(db, first, logger).SeedAsync(CancellationToken.None);

        var admin = await db.Users.SingleAsync(x => x.Email == DatabaseSeeder.AdminEmail);
        var firstHash = admin.PasswordHash;
        Assert.True(PasswordHasher.Verify("FirstSeed!1", firstHash));

        var second = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin__SeedPassword"] = "RotatedSeed!2"
        }).Build();
        await new DatabaseSeeder(db, second, logger).SeedAsync(CancellationToken.None);

        await db.Entry(admin).ReloadAsync();
        Assert.NotEqual(firstHash, admin.PasswordHash);
        Assert.True(PasswordHasher.Verify("RotatedSeed!2", admin.PasswordHash));
        Assert.False(PasswordHasher.Verify("FirstSeed!1", admin.PasswordHash));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("FirstSeed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(logger.Messages, x => x.Contains("RotatedSeed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logger.Messages, x => x.Contains(DatabaseSeeder.AdminEmail, StringComparison.OrdinalIgnoreCase));

        File.Delete(dbPath);
    }

    [Fact]
    public async Task Empty_AdminSeedPassword_does_not_reset_existing_hash()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-reseed-empty-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await new DatabaseSeeder(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeedPassword"] = "KeepMe!1"
        }).Build(), NullLogger<DatabaseSeeder>.Instance).SeedAsync(CancellationToken.None);

        var before = (await db.Users.SingleAsync(x => x.Email == DatabaseSeeder.AdminEmail)).PasswordHash;
        await new DatabaseSeeder(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeedPassword"] = ""
        }).Build(), NullLogger<DatabaseSeeder>.Instance).SeedAsync(CancellationToken.None);

        var after = (await db.Users.SingleAsync(x => x.Email == DatabaseSeeder.AdminEmail)).PasswordHash;
        Assert.Equal(before, after);
        File.Delete(dbPath);
    }

    [Fact]
    public async Task Session_idle_default_is_30_and_admin_can_update()
    {
        var viewer = await Authed("viewer@bisconsultants.com");
        var read = await viewer.GetAsync("/api/settings/session");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        using (var json = JsonDocument.Parse(await read.Content.ReadAsStringAsync()))
        {
            Assert.Equal(30, json.RootElement.GetProperty("defaultMinutes").GetInt32());
            Assert.InRange(json.RootElement.GetProperty("idleTimeoutMinutes").GetInt32(), 5, 1440);
        }

        var forbidden = await viewer.PutAsync("/api/settings/session", TestAppFactory.Json("""{"idleTimeoutMinutes":45}"""));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var updated = await admin.PutAsync("/api/settings/session", TestAppFactory.Json("""{"idleTimeoutMinutes":45}"""));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        using var saved = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal(45, saved.RootElement.GetProperty("idleTimeoutMinutes").GetInt32());
        Assert.Equal("admin", saved.RootElement.GetProperty("source").GetString());
    }

    [Fact]
    public async Task Ocr_cleanup_is_seeded_and_admin_can_add_rule()
    {
        var admin = await Authed("admin@bisconsultants.com");
        var list = await admin.GetAsync("/api/settings/ocr-cleanup");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("Discard", body);
        Assert.Contains("N/A", body);
        Assert.DoesNotContain("PLACEHOLDER", body, StringComparison.OrdinalIgnoreCase);

        var created = await admin.PostAsync("/api/settings/ocr-cleanup", TestAppFactory.Json("""{"kind":"Discard","value":"VOID","isActive":true,"sortOrder":99}"""));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var viewer = await Authed("viewer@bisconsultants.com");
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync("/api/settings/ocr-cleanup", TestAppFactory.Json("""{"kind":"Discard","value":"NOPE","isActive":true,"sortOrder":1}"""))).StatusCode);
    }

    [Fact]
    public void Ocr_field_cleaner_trims_and_discards_words()
    {
        var trim = new[] { "\"", ".", "," };
        var discard = new[] { "N/A", "NONE" };
        Assert.Equal("Jane Example", OcrFieldCleaner.Clean("  \"Jane Example\". ", trim, discard));
        Assert.Null(OcrFieldCleaner.Clean("N/A", trim, discard));
        Assert.Equal("Acme Holdings", OcrFieldCleaner.Clean("Acme NONE Holdings", trim, discard));
    }

    [Fact]
    public async Task Ocr_extract_applies_cleanup_rules()
    {
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source=file:ocr-clean-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Clients.Add(new Client { Id = DatabaseSeeder.AcmeId, Name = "Acme" });
        db.OcrCleanupRules.AddRange(
            new OcrCleanupRule { Id = Guid.NewGuid(), Kind = OcrCleanupKinds.Trim, Value = "\"", IsActive = true, SortOrder = 1 },
            new OcrCleanupRule { Id = Guid.NewGuid(), Kind = OcrCleanupKinds.Discard, Value = "N/A", IsActive = true, SortOrder = 2 });
        await db.SaveChangesAsync();

        var blobs = new InMemoryBlobStorage();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = "Clean_me.pdf",
            ClientId = DatabaseSeeder.AcmeId,
            Status = DocumentStatuses.Queued,
            BlobPath = $"deeds/{Guid.NewGuid():N}.pdf",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        await using (var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray()))
        {
            await blobs.UploadAsync(document.BlobPath, pdf, "application/pdf", CancellationToken.None);
        }

        var processor = new OcrProcessor(
            db,
            blobs,
            new DirtyFieldIntelligenceClient(),
            Microsoft.Extensions.Options.Options.Create(new OcrOptions()),
            NullLogger<OcrProcessor>.Instance,
            new NullOcrNotifier(),
            new OcrHealthRecorder(db, new OcrHealthSignal()));
        await processor.ProcessAsync(new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "c1",
            PopReceipt = "r",
            DequeueCount = 1
        }, CancellationToken.None);

        var updated = await db.Documents.Include(x => x.Fields).SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Ready, updated.Status);
        Assert.Equal("Jane Example", updated.Fields?.Grantor);
        Assert.Null(updated.Fields?.ParcelId);
    }

    [Fact]
    public async Task Failed_ocr_retry_requeues_to_processing_path()
    {
        Guid failedId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var document = new Document
            {
                Id = Guid.NewGuid(),
                Name = "Hardening_fail.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Failed,
                BlobPath = $"deeds/hardening-{Guid.NewGuid():N}.pdf",
                ErrorMessage = "OCR failed — invalid JSON. Use Retry.",
                DiRawBlobPath = "di-raw/broken.json",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();
            failedId = document.Id;
        }

        var client = await Authed("editor@bisconsultants.com");
        var list = await client.GetAsync("/api/documents");
        using (var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync()))
        {
            var failed = json.RootElement.EnumerateArray().First(x => x.GetProperty("id").GetGuid() == failedId);
            Assert.True(failed.GetProperty("canRetry").GetBoolean());
            Assert.Contains("JSON", failed.GetProperty("errorMessage").GetString(), StringComparison.OrdinalIgnoreCase);
        }

        var retry = await client.PostAsync($"/api/documents/{failedId}/retry", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var body = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.Equal("Queued", body.RootElement.GetProperty("status").GetString());
        Assert.Contains("JSON", body.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        var detail = await client.GetAsync($"/api/documents/{failedId}");
        using var deed = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal("Queued", deed.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Invalid_json_extract_fails_with_retry_reason()
    {
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source=file:ocr-json-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Clients.Add(new Client { Id = DatabaseSeeder.AcmeId, Name = "Acme" });
        await db.SaveChangesAsync();
        var blobs = new InMemoryBlobStorage();
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = "Broken.json.pdf",
            ClientId = DatabaseSeeder.AcmeId,
            Status = DocumentStatuses.Queued,
            BlobPath = $"deeds/{Guid.NewGuid():N}.pdf",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        await using (var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray()))
        {
            await blobs.UploadAsync(document.BlobPath, pdf, "application/pdf", CancellationToken.None);
        }

        var processor = new OcrProcessor(
            db,
            blobs,
            new BrokenJsonIntelligenceClient(),
            Microsoft.Extensions.Options.Options.Create(new OcrOptions()),
            NullLogger<OcrProcessor>.Instance,
            new NullOcrNotifier(),
            new OcrHealthRecorder(db, new OcrHealthSignal()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "j1",
            PopReceipt = "r",
            DequeueCount = 1
        }, CancellationToken.None));

        var updated = await db.Documents.SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Failed, updated.Status);
        Assert.Contains("JSON", updated.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Retry", updated.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_can_requeue_all_failed()
    {
        var admin = await Authed("admin@bisconsultants.com");
        var response = await admin.PostAsync("/api/documents/requeue-failed", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("count").GetInt32() >= 0);
    }

    [Fact]
    public void AdminSeedPassword_reads_sendgrid_style_aliases()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin__SeedPassword"] = "AliasPass!1"
        }).Build();
        Assert.Equal("AliasPass!1", DatabaseSeeder.ReadAdminSeedPassword(config));
        Assert.Equal("AliasPass!1", DependencyInjection.FirstValue(config, "AdminSeedPassword", "Admin:SeedPassword", "Admin__SeedPassword"));
    }

    private static void InvokeUp<TMigration>(MigrationBuilder builder) where TMigration : Migration, new() =>
        InvokeUp(typeof(TMigration), builder);

    private static void InvokeUp(Type migrationType, MigrationBuilder builder)
    {
        var up = migrationType.GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(Activator.CreateInstance(migrationType), [builder]);
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertNoSecrets(string body)
    {
        Assert.DoesNotContain("AccountKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DefaultEndpoints", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlConnection", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StorageConnection", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DocumentIntelligenceKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("BISDocumentIntelligenceEndpoint", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JwtSigningKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChangeMe", body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class DirtyFieldIntelligenceClient : IDocumentIntelligenceClient
    {
        public Task<DocumentIntelligenceResult> AnalyzeAsync(string documentName, Stream pdf, CancellationToken cancellationToken)
        {
            _ = documentName;
            _ = pdf;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DocumentIntelligenceResult
            {
                RawJson = """{"ok":true}""",
                Fields = new ExtractedDeedFields
                {
                    Grantor = "\"Jane Example\"",
                    Grantee = "Acme Holdings LLC",
                    ParcelId = "N/A"
                }
            });
        }
    }

    private sealed class BrokenJsonIntelligenceClient : IDocumentIntelligenceClient
    {
        public Task<DocumentIntelligenceResult> AnalyzeAsync(string documentName, Stream pdf, CancellationToken cancellationToken)
        {
            _ = documentName;
            _ = pdf;
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DocumentIntelligenceResult
            {
                RawJson = "not-json{",
                Fields = new ExtractedDeedFields()
            });
        }
    }

    private sealed class RecordingLogger : Microsoft.Extensions.Logging.ILogger<DatabaseSeeder>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }
    }
}
