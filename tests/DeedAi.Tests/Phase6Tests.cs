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
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Data.Migrations;
using DeedAi.Infrastructure.Health;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DeedAi.Tests;

public sealed class Phase6Tests
{
    private const string MigrationId = Phase6AiExtract.MigrationId;
    private const string PriorMigrationId = Phase6AiExtract.PriorMigrationId;

    [Fact]
    public void Review_and_documents_use_locked_fields_confirm_and_one_path()
    {
        var review = Read("spa/src/pages/ReviewPage.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var processor = Read("src/DeedAi.Infrastructure/Ocr/OcrProcessor.cs");
        var di = Read("src/DeedAi.Infrastructure/DependencyInjection.cs");
        var api = Read("spa/src/api.ts");

        foreach (var name in new[]
                 {
                     "documentNumber", "volume", "page", "deedType", "pid", "mailingStreet", "mailingCity",
                     "mailingState", "mailingZip", "grantors", "grantees"
                 })
        {
            Assert.Contains(name, review, StringComparison.Ordinal);
            Assert.Contains(name, api, StringComparison.Ordinal);
        }

        Assert.Contains("ConfirmSheet", review, StringComparison.Ordinal);
        Assert.Contains("Re-extract this deed?", review, StringComparison.Ordinal);
        Assert.Contains("confidence-chip", review, StringComparison.Ordinal);
        Assert.Contains("Raw AI Extract", review, StringComparison.Ordinal);
        Assert.Contains("Re-extract Selected", documents, StringComparison.Ordinal);
        Assert.Contains("Re-extract failed deeds?", documents, StringComparison.Ordinal);
        Assert.Contains("/api/documents/re-extract", api, StringComparison.Ordinal);
        Assert.Contains("extract-raw", api, StringComparison.Ordinal);

        Assert.Contains("IAiExtractClient", processor, StringComparison.Ordinal);
        Assert.Contains("FailClosedAiExtractClient", di, StringComparison.Ordinal);
        Assert.Contains("AzureOpenAIExtractClient", di, StringComparison.Ordinal);
        AssertNoDualFieldFillPath();

        Assert.DoesNotContain("County", review, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", review, StringComparison.Ordinal);
        Assert.DoesNotContain("docNo", review, StringComparison.Ordinal);
        Assert.Contains("Phase 6", Read("docs/PHASE-6-AI-EXTRACT-AC.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Di_field_fill_is_removed_with_no_dual_path_or_flag()
    {
        AssertNoDualFieldFillPath();

        using var factory = TestAppFactory.Create();
        Assert.IsAssignableFrom<IAiExtractClient>(factory.Services.GetRequiredService<IAiExtractClient>());
        Assert.Null(typeof(OcrJobMessage).Assembly.GetType("DeedAi.Domain.Abstractions.IDocumentIntelligenceClient"));
        Assert.Null(typeof(OcrProcessor).Assembly.GetType("DeedAi.Infrastructure.Ocr.AzureDocumentIntelligenceClient"));
        Assert.Null(typeof(OcrProcessor).Assembly.GetType("DeedAi.Infrastructure.Ocr.MockDocumentIntelligenceClient"));
        Assert.Null(typeof(OcrJobMessage).Assembly.GetType("DeedAi.Domain.Ocr.ExtractedDeedFields"));
        Assert.Null(typeof(OcrJobMessage).Assembly.GetType("DeedAi.Domain.Ocr.DocumentIntelligenceResult"));
    }

    [Fact]
    public void Migration_is_designer_first_after_statuses_catalog()
    {
        Assert.True(string.CompareOrdinal(PriorMigrationId, MigrationId) < 0);

        var type = typeof(Phase6AiExtract);
        Assert.Equal(MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(MigrationId, discovered);
        Assert.Equal(type, db.GetService<IMigrationsAssembly>().Migrations[MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260910040000_Phase6AiExtract.Designer.cs");
        var snapshot = Read("src/DeedAi.Infrastructure/Data/Migrations/DeedAiDbContextModelSnapshot.cs");
        Assert.Contains($"[Migration(\"{MigrationId}\")]", designer, StringComparison.Ordinal);
        foreach (var name in new[] { "AiRawBlobPath", "ExtractConfidenceJson" })
        {
            Assert.Contains($"\"{name}\"", designer, StringComparison.Ordinal);
            Assert.Contains($"\"{name}\"", snapshot, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_backfills_nulls_and_adds_defaults()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Phase6AiExtract).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(Activator.CreateInstance(typeof(Phase6AiExtract)), [builder]);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        foreach (var column in Phase6AiExtract.StringColumns)
        {
            Assert.Contains($"UPDATE Documents SET [{column}]", sql, StringComparison.Ordinal);
            Assert.Contains($"DF_Documents_{column}", sql, StringComparison.Ordinal);
            Assert.Contains($"DEFAULT N'' FOR [{column}]", sql, StringComparison.Ordinal);
        }

        Assert.Contains(builder.Operations.OfType<AddColumnOperation>(), x => x.Name == "AiRawBlobPath" && x.IsNullable);
        Assert.Contains(builder.Operations.OfType<AddColumnOperation>(), x => x.Name == "ExtractConfidenceJson" && x.IsNullable);
        Assert.DoesNotContain("County", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Extract_fills_locked_fields_stores_raw_blob_and_confidence()
    {
        await using var db = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var document = await SeedDocument(db, blobs, "Deed_ok.pdf");
        var processor = new OcrProcessor(
            db,
            blobs,
            new MockAiExtractClient(),
            Options.Create(new OcrOptions()),
            NullLogger<OcrProcessor>.Instance,
            new NullOcrNotifier(),
            new OcrHealthRecorder(db, new OcrHealthSignal()));

        await processor.ProcessAsync(Delivery(document), CancellationToken.None);

        var updated = await db.Documents.Include(x => x.Fields).SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Ready, updated.Status);
        Assert.Equal("2024-0812", updated.DocumentNumber);
        Assert.Equal("184", updated.Volume);
        Assert.Equal("12", updated.Page);
        Assert.Equal("Warranty Deed", updated.DeedType);
        Assert.Equal("12-345-678", updated.Pid);
        Assert.Equal("100 Main St", updated.MailingStreet);
        Assert.Equal("Springfield", updated.MailingCity);
        Assert.Equal("IL", updated.MailingState);
        Assert.Equal("62701", updated.MailingZip);
        Assert.Equal(new[] { "Jane Example" }, updated.Grantors);
        Assert.Equal(new[] { "Acme Holdings LLC" }, updated.Grantees);
        Assert.Equal("Jane Example", updated.Fields?.Grantor);
        Assert.Equal("12-345-678", updated.Fields?.ParcelId);
        Assert.StartsWith("ai-raw/", updated.AiRawBlobPath);
        Assert.True(await blobs.ExistsAsync(updated.AiRawBlobPath!, CancellationToken.None));
        var confidence = ExtractConfidence.Parse(updated.ExtractConfidenceJson);
        Assert.True(confidence["documentNumber"] >= 0.85);
        Assert.Equal(ExtractConfidence.High, ExtractConfidence.Chip(confidence["documentNumber"]));
        Assert.Equal(ExtractConfidence.Med, ExtractConfidence.Chip(0.72));
        Assert.Equal(ExtractConfidence.Low, ExtractConfidence.Chip(0.2));
    }

    [Fact]
    public async Task Unconfigured_extract_fails_closed()
    {
        await using var db = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var document = await SeedDocument(db, blobs, "Deed_ok.pdf");
        var processor = new OcrProcessor(
            db,
            blobs,
            new FailClosedAiExtractClient(),
            Options.Create(new OcrOptions()),
            NullLogger<OcrProcessor>.Instance,
            new NullOcrNotifier(),
            new OcrHealthRecorder(db, new OcrHealthSignal()));

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(Delivery(document), CancellationToken.None));
        Assert.Contains("not configured", thrown.Message, StringComparison.OrdinalIgnoreCase);

        var updated = await db.Documents.SingleAsync(x => x.Id == document.Id);
        Assert.Equal(DocumentStatuses.Failed, updated.Status);
        Assert.Contains("not configured", updated.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Pricier_model_is_rejected_without_cos_override()
    {
        Assert.Equal("gpt-4o-mini", AiExtractModels.Default);
        var settings = new AzureOpenAIOptions { Model = "gpt-4o", AllowPricierModel = false };
        var thrown = Assert.Throws<InvalidOperationException>(() => AzureOpenAIExtractClient.EnsureCheapModel(settings));
        Assert.Contains("Chief of Staff", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("gpt-4o", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("Do not raise quotas", thrown.Message, StringComparison.Ordinal);

        var viaDeployment = Assert.Throws<InvalidOperationException>(() =>
            AzureOpenAIExtractClient.EnsureCheapModel(new AzureOpenAIOptions
            {
                Model = "gpt-4o-mini",
                Deployment = "gpt-4o"
            }));
        Assert.Contains("Chief of Staff", viaDeployment.Message, StringComparison.Ordinal);

        AzureOpenAIExtractClient.EnsureCheapModel(new AzureOpenAIOptions { Model = "gpt-4o-mini", Deployment = "deed-extract" });
        AzureOpenAIExtractClient.EnsureCheapModel(new AzureOpenAIOptions { Model = "gpt-4o", Deployment = "gpt-4o", AllowPricierModel = true });
        Assert.True(AiExtractModels.IsCheap("gpt-4o-mini"));
        Assert.True(AiExtractModels.IsPricier("o1"));
    }

    [Fact]
    public void Azure_json_maps_locked_fields_and_rejects_invalid()
    {
        var parsed = AzureOpenAIExtractClient.ParseExtract(
            """{"documentNumber":"A-1","volume":"2","page":"3","deedType":"Quitclaim","pid":"99","mailingStreet":"1 Oak","mailingCity":"Peoria","mailingState":"IL","mailingZip":"61602","grantors":["Pat"],"grantees":["Acme"],"confidence":{"pid":0.4}}""");
        Assert.Equal("A-1", parsed.Fields.DocumentNumber);
        Assert.Equal(new[] { "Pat" }, parsed.Fields.Grantors);
        Assert.Equal(0.4, parsed.Confidence["pid"]);
        Assert.Throws<InvalidOperationException>(() => AzureOpenAIExtractClient.ParseExtract("not-json{"));
    }

    [Fact]
    public async Task Health_and_registration_fail_closed_without_keys()
    {
        await using var factory = TestAppFactory.Create(extraSettings: new Dictionary<string, string?>
        {
            ["AzureOpenAI:Mode"] = ""
        });
        Assert.IsType<FailClosedAiExtractClient>(factory.Services.GetRequiredService<IAiExtractClient>());

        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var response = await admin.GetAsync("/api/health/detail");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("AzureOpenAIKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PLACEHOLDER", body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        var azure = json.RootElement.GetProperty("checks").GetProperty("azureOpenAI");
        Assert.Equal("fail", azure.GetProperty("status").GetString());
        Assert.False(azure.GetProperty("reachable").GetBoolean());
        Assert.False(azure.GetProperty("configured").GetBoolean());
        Assert.Equal("Unconfigured", azure.GetProperty("mode").GetString());
        Assert.Equal("ok", json.RootElement.GetProperty("checks").GetProperty("sql").GetProperty("status").GetString());
        var di = json.RootElement.GetProperty("checks").GetProperty("documentIntelligence");
        Assert.Equal("ok", di.GetProperty("status").GetString());
        Assert.Equal("Removed", di.GetProperty("mode").GetString());
        Assert.False(di.GetProperty("configured").GetBoolean());
        Assert.Contains("AI extract only", di.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Kv_bind_reads_app_setting_names()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AzureOpenAIEndpoint"] = "https://example.openai.azure.com/",
            ["AzureOpenAIKey"] = "secret",
            ["AzureOpenAIDeployment"] = "gpt-4o-mini",
            ["AzureOpenAIModel"] = "gpt-4o-mini",
            ["ConnectionStrings:AzureOpenAIKey"] = "connection-string-must-not-bind"
        }).Build();
        var options = DependencyInjection.BindAzureOpenAI(config);
        Assert.Equal("https://example.openai.azure.com/", options.Endpoint);
        Assert.Equal("secret", options.Key);
        Assert.Equal("gpt-4o-mini", options.Deployment);
        Assert.Equal("gpt-4o-mini", options.Model);
        Assert.False(options.AllowPricierModel);

        var empty = DependencyInjection.BindAzureOpenAI(new ConfigurationBuilder().AddInMemoryCollection().Build());
        Assert.Equal("gpt-4o-mini", empty.Model);
        Assert.Null(empty.Key);
        Assert.False(empty.AllowPricierModel);

        var bind = Read("src/DeedAi.Infrastructure/DependencyInjection.cs");
        Assert.Contains("AzureOpenAIEndpoint", bind, StringComparison.Ordinal);
        Assert.Contains("AzureOpenAIKey", bind, StringComparison.Ordinal);
        Assert.Contains("AzureOpenAIDeployment", bind, StringComparison.Ordinal);
        Assert.DoesNotContain("ConnectionStrings:AzureOpenAI", bind, StringComparison.Ordinal);
        Assert.DoesNotContain("quota", Read("src/DeedAi.Infrastructure/Ocr/AzureOpenAIExtractClient.cs"), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editor_reextract_and_admin_raw_blob_audit()
    {
        await using var factory = TestAppFactory.Create();
        Guid id;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var blobs = scope.ServiceProvider.GetRequiredService<InMemoryBlobStorage>();
            var document = new Document
            {
                Id = Guid.NewGuid(),
                Name = "Phase6_ready.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Ready,
                BlobPath = $"deeds/phase6-{Guid.NewGuid():N}.pdf",
                AiRawBlobPath = $"ai-raw/{Guid.NewGuid():N}.json",
                ExtractConfidenceJson = """{"pid":0.9}""",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Documents.Add(document);
            await db.SaveChangesAsync();
            await using (var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray()))
            {
                await blobs.UploadAsync(document.BlobPath, pdf, "application/pdf", CancellationToken.None);
            }

            await using (var raw = new MemoryStream("""{"source":"mock","pid":"12"}"""u8.ToArray()))
            {
                await blobs.UploadAsync(document.AiRawBlobPath!, raw, "application/json", CancellationToken.None);
            }

            id = document.Id;
        }

        var editor = await Authed(factory, DatabaseSeeder.EditorEmail);
        var viewer = await Authed(factory, DatabaseSeeder.ViewerEmail);
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);

        var denied = await viewer.PostAsync("/api/documents/re-extract", TestAppFactory.Json($"{{\"documentIds\":[\"{id}\"]}}"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var empty = await editor.PostAsync("/api/documents/re-extract", TestAppFactory.Json("""{"documentIds":[]}"""));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);

        var queued = await editor.PostAsync("/api/documents/re-extract", TestAppFactory.Json($"{{\"documentIds\":[\"{id}\"]}}"));
        queued.EnsureSuccessStatusCode();
        using var queuedJson = JsonDocument.Parse(await queued.Content.ReadAsStringAsync());
        Assert.Equal(1, queuedJson.RootElement.GetProperty("count").GetInt32());
        Assert.Contains("AI extract", queuedJson.RootElement.GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);

        var viewerRaw = await viewer.GetAsync($"/api/documents/{id}/extract-raw");
        Assert.Equal(HttpStatusCode.Forbidden, viewerRaw.StatusCode);

        var rawAudit = await admin.GetAsync($"/api/documents/{id}/extract-raw");
        rawAudit.EnsureSuccessStatusCode();
        Assert.Equal("application/json", rawAudit.Content.Headers.ContentType?.MediaType);
        Assert.Contains("mock", await rawAudit.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var detail = await editor.GetAsync($"/api/documents/{id}");
        detail.EnsureSuccessStatusCode();
        using var detailJson = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.True(detailJson.RootElement.TryGetProperty("extractConfidence", out _));
        Assert.True(detailJson.RootElement.TryGetProperty("aiRawBlobPath", out _));
        Assert.False(detailJson.RootElement.TryGetProperty("docNo", out _));
    }

    [Fact]
    public async Task Null_ai_columns_materialize_without_throwing()
    {
        await using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE Documents SET AiRawBlobPath = NULL, ExtractConfidenceJson = NULL");
        db.ChangeTracker.Clear();

        var thrown = await Record.ExceptionAsync(() => db.Documents.ToListAsync());
        Assert.Null(thrown);
        foreach (var document in await db.Documents.ToListAsync())
        {
            document.CoalesceNullListFields();
            Assert.NotNull(document.AiRawBlobPath);
            Assert.NotNull(document.ExtractConfidenceJson);
        }
    }

    private static void AssertNoDualFieldFillPath()
    {
        var processor = Read("src/DeedAi.Infrastructure/Ocr/OcrProcessor.cs");
        var di = Read("src/DeedAi.Infrastructure/DependencyInjection.cs");
        var health = Read("src/DeedAi.Infrastructure/Health/RuntimeHealth.cs");
        var job = Read("src/DeedAi.Domain/Ocr/OcrJobMessage.cs");
        var csproj = Read("src/DeedAi.Infrastructure/DeedAi.Infrastructure.csproj");
        var root = RepoRoot();

        Assert.False(File.Exists(Path.Combine(root, "src/DeedAi.Domain/Abstractions/IDocumentIntelligenceClient.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src/DeedAi.Infrastructure/Ocr/AzureDocumentIntelligenceClient.cs")));
        Assert.False(File.Exists(Path.Combine(root, "src/DeedAi.Infrastructure/Ocr/MockDocumentIntelligenceClient.cs")));

        foreach (var source in new[] { processor, di, health, job })
        {
            Assert.DoesNotContain("IDocumentIntelligenceClient", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AzureDocumentIntelligenceClient", source, StringComparison.Ordinal);
            Assert.DoesNotContain("MockDocumentIntelligenceClient", source, StringComparison.Ordinal);
            Assert.DoesNotContain("ExtractedDeedFields", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseDocumentIntelligence", source, StringComparison.Ordinal);
            Assert.DoesNotContain("EnableDiFieldFill", source, StringComparison.Ordinal);
            Assert.DoesNotContain("DualExtract", source, StringComparison.Ordinal);
            Assert.DoesNotContain("UseAiExtract", source, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("AddDocumentIntelligence", di, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure.AI.DocumentIntelligence", di, StringComparison.Ordinal);
        Assert.DoesNotContain("Azure.AI.DocumentIntelligence", csproj, StringComparison.Ordinal);
        Assert.DoesNotContain("documentIntelligence.AnalyzeAsync", processor, StringComparison.Ordinal);
        Assert.Contains("IAiExtractClient", processor, StringComparison.Ordinal);
        Assert.Contains("DocumentIntelligenceRemoved", health, StringComparison.Ordinal);
    }

    private static OcrQueueDelivery Delivery(Document document) =>
        new()
        {
            Job = new OcrJobMessage { DocumentId = document.Id, BlobPath = document.BlobPath },
            MessageId = "1",
            PopReceipt = "r",
            DequeueCount = 1
        };

    private static async Task<DeedAiDbContext> CreateDb()
    {
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source=file:phase6-{Guid.NewGuid():N}?mode=memory&cache=shared")
            .Options;
        var db = new DeedAiDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        db.Clients.Add(new Client { Id = DatabaseSeeder.AcmeId, Name = "Acme" });
        await db.SaveChangesAsync();
        return db;
    }

    private static async Task<Document> SeedDocument(DeedAiDbContext db, InMemoryBlobStorage blobs, string name)
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Name = name,
            ClientId = DatabaseSeeder.AcmeId,
            Status = DocumentStatuses.Queued,
            BlobPath = $"deeds/{Guid.NewGuid():N}.pdf",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.Documents.Add(document);
        await db.SaveChangesAsync();
        await using var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
        await blobs.UploadAsync(document.BlobPath, pdf, "application/pdf", CancellationToken.None);
        return document;
    }

    private static async Task<HttpClient> Authed(TestAppFactory factory, string email)
    {
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static string Read(string relative)
    {
        var path = Path.Combine(RepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), path);
        return File.ReadAllText(path);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeedAi.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate DeedAi.slnx from the test output.");
    }
}
