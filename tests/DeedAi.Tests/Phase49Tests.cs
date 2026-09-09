using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Health;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Infrastructure.Queueing;
using DeedAi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DeedAi.Tests;

public sealed class Phase49Tests
{
    [Fact]
    public async Task Health_detail_has_probes_queue_metrics_and_no_secrets()
    {
        await using var factory = TestAppFactory.Create();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var detail = await admin.GetAsync("/api/health/detail");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await detail.Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        using var json = JsonDocument.Parse(body);
        var overall = json.RootElement.GetProperty("status").GetString();
        Assert.True(overall is "ok" or "degraded");
        var checks = json.RootElement.GetProperty("checks");

        AssertReachable(checks.GetProperty("sql"), "Sqlite");
        AssertReachable(checks.GetProperty("storage"), "InMemory");
        AssertReachable(checks.GetProperty("queue"), "InMemory");

        var blob = checks.GetProperty("blob");
        Assert.Equal("ok", blob.GetProperty("status").GetString());
        Assert.True(blob.GetProperty("reachable").GetBoolean());
        Assert.Equal("Pass", blob.GetProperty("detail").GetString());
        Assert.Equal("InMemory", blob.GetProperty("mode").GetString());

        var di = checks.GetProperty("documentIntelligence");
        Assert.Equal("ok", di.GetProperty("status").GetString());
        Assert.True(di.GetProperty("reachable").GetBoolean());
        Assert.False(di.GetProperty("configured").GetBoolean());
        Assert.Equal("Mock", di.GetProperty("mode").GetString());

        var pipeline = checks.GetProperty("ocrPipeline");
        Assert.Equal("fail", pipeline.GetProperty("status").GetString());
        Assert.False(pipeline.GetProperty("reachable").GetBoolean());
        Assert.Equal("Queue reachable · no worker heartbeat", pipeline.GetProperty("detail").GetString());
        Assert.NotEqual(di.GetProperty("status").GetString(), pipeline.GetProperty("status").GetString());

        var queue = json.RootElement.GetProperty("ocrQueue");
        Assert.Equal(0, queue.GetProperty("depth").GetInt32());
        Assert.Equal(JsonValueKind.Null, queue.GetProperty("oldestWaitingAgeSeconds").ValueKind);
        Assert.Equal(0, queue.GetProperty("poisonCount").GetInt32());
        Assert.True(queue.GetProperty("failedCount").GetInt32() >= 0);
        Assert.True(queue.TryGetProperty("lastDiSuccessAt", out _));
        Assert.True(queue.TryGetProperty("lastDiFailAt", out _));
    }

    [Fact]
    public async Task Document_intelligence_fail_is_distinct_from_blob_pass_and_hides_secrets()
    {
        await using var factory = TestAppFactory.Create(extraSettings: new Dictionary<string, string?>
        {
            ["BISDocumentIntelligenceEndpoint"] = "https://di-unreachable.invalid/",
            ["DocumentIntelligenceKey"] = "fake-di-key-AccountKey-lookalike",
            ["DocumentIntelligence:Endpoint"] = "https://di-unreachable.invalid/",
            ["DocumentIntelligence:Key"] = "fake-di-key-AccountKey-lookalike"
        });
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var body = await (await admin.GetAsync("/api/health/detail")).Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        Assert.DoesNotContain("di-unreachable", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".invalid", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fake-di-key", body, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(body);
        var checks = json.RootElement.GetProperty("checks");
        Assert.Equal("Pass", checks.GetProperty("blob").GetProperty("detail").GetString());
        Assert.True(checks.GetProperty("blob").GetProperty("reachable").GetBoolean());
        var di = checks.GetProperty("documentIntelligence");
        Assert.True(di.GetProperty("configured").GetBoolean());
        Assert.Equal("Azure", di.GetProperty("mode").GetString());
        Assert.Equal("fail", di.GetProperty("status").GetString());
        Assert.False(di.GetProperty("reachable").GetBoolean());
    }

    [Fact]
    public async Task Queue_visibility_and_pipeline_heartbeat_are_present_for_qa()
    {
        await using var factory = TestAppFactory.Create();
        using (var scope = factory.Services.CreateScope())
        {
            var queue = scope.ServiceProvider.GetRequiredService<InMemoryOcrJobQueue>();
            await queue.EnqueueAsync(new OcrJobMessage { DocumentId = Guid.NewGuid(), BlobPath = "deeds/wait.pdf" }, CancellationToken.None);
            await queue.EnqueuePoisonForTestsAsync(new OcrJobMessage { DocumentId = Guid.NewGuid(), BlobPath = "deeds/poison.pdf" }, 5);
            var health = scope.ServiceProvider.GetRequiredService<OcrHealthRecorder>();
            await health.RecordWorkerHeartbeatAsync(CancellationToken.None);
            await health.RecordDiOutcomeAsync(true, CancellationToken.None);
            await health.RecordDiOutcomeAsync(false, CancellationToken.None);
        }

        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var body = await (await admin.GetAsync("/api/health/detail")).Content.ReadAsStringAsync();
        AssertNoSecrets(body);
        using var json = JsonDocument.Parse(body);
        var pipeline = json.RootElement.GetProperty("checks").GetProperty("ocrPipeline");
        Assert.Equal("ok", pipeline.GetProperty("status").GetString());
        Assert.True(pipeline.GetProperty("reachable").GetBoolean());
        Assert.Contains("heartbeat", pipeline.GetProperty("detail").GetString(), StringComparison.OrdinalIgnoreCase);

        var di = json.RootElement.GetProperty("checks").GetProperty("documentIntelligence");
        Assert.Equal("ok", di.GetProperty("status").GetString());
        Assert.NotEqual(pipeline.GetProperty("detail").GetString(), di.GetProperty("detail").GetString());

        var ocrQueue = json.RootElement.GetProperty("ocrQueue");
        Assert.True(ocrQueue.GetProperty("depth").GetInt32() >= 2);
        Assert.True(ocrQueue.GetProperty("oldestWaitingAgeSeconds").GetInt32() >= 0);
        Assert.True(ocrQueue.GetProperty("poisonCount").GetInt32() >= 1);
        Assert.False(string.IsNullOrWhiteSpace(ocrQueue.GetProperty("lastDiSuccessAt").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(ocrQueue.GetProperty("lastDiFailAt").GetString()));
        Assert.DoesNotContain("deeds/wait.pdf", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("deeds/poison.pdf", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ocr_processor_records_di_success_and_fail_timestamps()
    {
        await using var db = await CreateDb();
        var blobs = new InMemoryBlobStorage();
        var signal = new OcrHealthSignal();
        var recorder = new OcrHealthRecorder(db, signal);
        var ok = await SeedDocument(db, blobs, "Deed_ok.pdf");
        var processor = new OcrProcessor(
            db,
            blobs,
            new MockDocumentIntelligenceClient(),
            Options.Create(new OcrOptions()),
            NullLogger<OcrProcessor>.Instance,
            new NullOcrNotifier(),
            recorder);
        await processor.ProcessAsync(new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = ok.Id, BlobPath = ok.BlobPath },
            MessageId = "1",
            PopReceipt = "r",
            DequeueCount = 1
        }, CancellationToken.None);
        Assert.NotNull(signal.LastDiSuccessAt);

        var bad = await SeedDocument(db, blobs, "Scan_bad.pdf");
        await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(new OcrQueueDelivery
        {
            Job = new OcrJobMessage { DocumentId = bad.Id, BlobPath = bad.BlobPath },
            MessageId = "2",
            PopReceipt = "r",
            DequeueCount = 1
        }, CancellationToken.None));
        Assert.NotNull(signal.LastDiFailAt);

        var snapshot = await recorder.ReadAsync(CancellationToken.None);
        Assert.NotNull(snapshot.LastDiSuccessAt);
        Assert.NotNull(snapshot.LastDiFailAt);
    }

    [Fact]
    public void Settings_spa_shows_probes_metrics_and_44px_refresh()
    {
        var root = Path.Combine(RepoRoot(), "spa", "src");
        var panel = File.ReadAllText(Path.Combine(root, "components", "SystemHealthPanel.tsx"));
        var css = File.ReadAllText(Path.Combine(root, "styles.css"));
        Assert.Contains("SQL", panel);
        Assert.Contains("Storage", panel);
        Assert.Contains("Queue", panel);
        Assert.Contains("Blob", panel);
        Assert.Contains("Document Intelligence", panel);
        Assert.Contains("OCR pipeline", panel);
        Assert.Contains("Queue depth", panel);
        Assert.Contains("Oldest waiting", panel);
        Assert.Contains("Poison / Failed", panel);
        Assert.Contains("Last DI success", panel);
        Assert.Contains("Last DI fail", panel);
        Assert.Contains("health-refresh", panel);
        Assert.Contains(".health-refresh", css);
        Assert.Contains("min-height: 44px", css);
        Assert.DoesNotContain("County", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewer_cannot_read_health_detail()
    {
        await using var factory = TestAppFactory.Create();
        var viewer = await Authed(factory, DatabaseSeeder.ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/health/detail")).StatusCode);
        var anon = factory.CreateJsonClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/health/detail")).StatusCode);
        var shallow = await (await anon.GetAsync("/api/health")).Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(shallow);
        Assert.False(json.RootElement.TryGetProperty("checks", out _));
        Assert.False(json.RootElement.TryGetProperty("ocrQueue", out _));
        AssertNoSecrets(shallow);
    }

    private static void AssertReachable(JsonElement check, string mode)
    {
        Assert.True(check.GetProperty("reachable").GetBoolean());
        Assert.Equal("ok", check.GetProperty("status").GetString());
        Assert.Equal(mode, check.GetProperty("mode").GetString());
    }

    private static async Task<HttpClient> Authed(TestAppFactory factory, string email)
    {
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertNoSecrets(string body)
    {
        foreach (var needle in new[]
                 {
                     "AccountKey", "DefaultEndpoints", "Password=", "Server=", "SqlConnection",
                     "StorageConnection", "DocumentIntelligenceKey", "BISDocumentIntelligenceEndpoint",
                     "JwtSigningKey", "ChangeMe", "connectionString", "SharedAccessSignature"
                 })
        {
            Assert.DoesNotContain(needle, body, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain("County", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
    }

    private static async Task<DeedAiDbContext> CreateDb()
    {
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source=file:phase49-{Guid.NewGuid():N}?mode=memory&cache=shared")
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
