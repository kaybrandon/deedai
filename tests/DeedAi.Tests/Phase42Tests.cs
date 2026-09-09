using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase42Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase42Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Seeded_ready_demo_deed_serves_pdf_preview()
    {
        var client = await Authed(DatabaseSeeder.EditorEmail);
        var id = await FirstReadyId(client);
        var file = await client.GetAsync($"/api/documents/{id}/file");
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("application/pdf", file.Content.Headers.ContentType?.MediaType);
        var bytes = await file.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 8);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
        Assert.DoesNotContain("County", await file.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", await file.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task File_returns_placeholder_404_when_ready_deed_has_no_blob()
    {
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            id = Guid.NewGuid();
            db.Documents.Add(new Document
            {
                Id = id,
                Name = "NoFile.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Ready,
                BlobPath = $"deeds/missing/{id:N}.pdf",
                UploadedByUserId = DatabaseSeeder.UploaderId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var client = await Authed(DatabaseSeeder.EditorEmail);
        var file = await client.GetAsync($"/api/documents/{id}/file");
        Assert.Equal(HttpStatusCode.NotFound, file.StatusCode);
        var body = await file.Content.ReadAsStringAsync();
        Assert.Contains("PDF is not available", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo row", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Seeded_ready_needs_review_is_coherent_on_list_and_detail()
    {
        var client = await Authed(DatabaseSeeder.ViewerEmail);
        var list = await client.GetAsync("/api/documents?status=Ready");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using var rows = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        var ready = rows.RootElement.EnumerateArray()
            .First(x => x.GetProperty("status").GetString() == DocumentStatuses.Ready
                        && x.GetProperty("flags").EnumerateArray().Any(f => f.GetProperty("name").GetString() == ReviewWorkflow.NeedsReviewFlagName));
        Assert.Equal(ReviewWorkflow.NeedsReview, ready.GetProperty("reviewStatus").GetString());
        Assert.Equal(ReviewWorkflow.NeedsReview, ready.GetProperty("displayStatus").GetString());
        Assert.Equal(DocumentStatuses.Ready, ready.GetProperty("status").GetString());

        var filtered = await client.GetAsync("/api/documents?status=NeedsReview");
        Assert.Equal(HttpStatusCode.OK, filtered.StatusCode);
        using var needs = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Contains(
            needs.RootElement.EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == ready.GetProperty("id").GetGuid());

        var detail = await client.GetAsync($"/api/documents/{ready.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal(DocumentStatuses.Ready, json.RootElement.GetProperty("status").GetString());
        Assert.Equal(ReviewWorkflow.NeedsReview, json.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal(ReviewWorkflow.NeedsReview, json.RootElement.GetProperty("displayStatus").GetString());
    }

    [Fact]
    public async Task Review_status_and_needs_review_flag_stay_in_sync()
    {
        Guid id;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            id = Guid.NewGuid();
            db.Documents.Add(new Document
            {
                Id = id,
                Name = "ReviewSync.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Ready,
                BlobPath = $"deeds/demo/ReviewSync_{id:N}.pdf",
                UploadedByUserId = DatabaseSeeder.UploaderId,
                AssigneeUserId = DatabaseSeeder.EditorId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                ReviewStatus = ReviewWorkflow.NeedsReview
            });
            db.DocumentFlags.Add(new DocumentFlag { DocumentId = id, FlagDefinitionId = ReviewWorkflow.NeedsReviewFlagId });
            await db.SaveChangesAsync();
        }

        var client = await Authed(DatabaseSeeder.EditorEmail);

        var approve = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Jane Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"250000","parcelId":"12-345-678","client":"Acme","notes":"approved","isDraft":false,"reviewStatus":"Approved"}"""));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);

        var afterApprove = await client.GetAsync($"/api/documents/{id}");
        using (var json = JsonDocument.Parse(await afterApprove.Content.ReadAsStringAsync()))
        {
            Assert.Equal(ReviewWorkflow.Approved, json.RootElement.GetProperty("reviewStatus").GetString());
            Assert.Equal(ReviewWorkflow.Approved, json.RootElement.GetProperty("displayStatus").GetString());
            Assert.Equal(DocumentStatuses.Ready, json.RootElement.GetProperty("status").GetString());
            Assert.DoesNotContain(
                json.RootElement.GetProperty("flags").EnumerateArray(),
                x => x.GetProperty("name").GetString() == ReviewWorkflow.NeedsReviewFlagName);
        }

        var invalid = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Jane Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"250000","parcelId":"12-345-678","client":"Acme","notes":"bad","isDraft":false,"reviewStatus":"Ready"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var flagOn = await client.PutAsync(
            $"/api/documents/{id}/flags",
            TestAppFactory.Json($"{{\"flagIds\":[\"{ReviewWorkflow.NeedsReviewFlagId}\"]}}"));
        Assert.Equal(HttpStatusCode.OK, flagOn.StatusCode);
        var flagged = await client.GetAsync($"/api/documents/{id}");
        using (var json = JsonDocument.Parse(await flagged.Content.ReadAsStringAsync()))
        {
            Assert.Equal(ReviewWorkflow.NeedsReview, json.RootElement.GetProperty("reviewStatus").GetString());
            Assert.Equal(ReviewWorkflow.NeedsReview, json.RootElement.GetProperty("displayStatus").GetString());
        }

        var flagOff = await client.PutAsync($"/api/documents/{id}/flags", TestAppFactory.Json("""{"flagIds":[]}"""));
        Assert.Equal(HttpStatusCode.OK, flagOff.StatusCode);
        var cleared = await client.GetAsync($"/api/documents/{id}");
        using (var json = JsonDocument.Parse(await cleared.Content.ReadAsStringAsync()))
        {
            Assert.True(
                json.RootElement.GetProperty("reviewStatus").ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
                || string.IsNullOrEmpty(json.RootElement.GetProperty("reviewStatus").GetString()));
            Assert.Equal(DocumentStatuses.Ready, json.RootElement.GetProperty("displayStatus").GetString());
        }

        var needs = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Jane Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"250000","parcelId":"12-345-678","client":"Acme","notes":"review","isDraft":false,"reviewStatus":"NeedsReview"}"""));
        Assert.Equal(HttpStatusCode.OK, needs.StatusCode);
        var restored = await client.GetAsync($"/api/documents/{id}");
        using var restoredJson = JsonDocument.Parse(await restored.Content.ReadAsStringAsync());
        Assert.Equal(ReviewWorkflow.NeedsReview, restoredJson.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Contains(
            restoredJson.RootElement.GetProperty("flags").EnumerateArray(),
            x => x.GetProperty("id").GetGuid() == ReviewWorkflow.NeedsReviewFlagId);
    }

    [Fact]
    public void Review_workflow_display_rules()
    {
        Assert.Equal(ReviewWorkflow.NeedsReview, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, null, true));
        Assert.Equal(ReviewWorkflow.NeedsReview, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, ReviewWorkflow.NeedsReview, false));
        Assert.Equal(ReviewWorkflow.Approved, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, ReviewWorkflow.Approved, false));
        Assert.Equal(DocumentStatuses.Ready, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, null, false));
        Assert.Equal(DocumentStatuses.Failed, ReviewWorkflow.DisplayStatus(DocumentStatuses.Failed, null, false));
    }

    [Fact]
    public async Task Viewer_cannot_read_health_detail_and_admin_payload_has_no_secrets()
    {
        var viewer = await Authed(DatabaseSeeder.ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/health/detail")).StatusCode);

        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var detail = await admin.GetAsync("/api/health/detail");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var body = await detail.Content.ReadAsStringAsync();
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccountKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("County", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("checks").TryGetProperty("sql", out _));
    }

    [Fact]
    public void Settings_spa_exposes_system_health()
    {
        var root = Path.Combine(RepoRoot(), "spa", "src");
        var settings = File.ReadAllText(Path.Combine(root, "pages", "SettingsPage.tsx"));
        Assert.Contains("SystemHealthPanel", settings);
        Assert.Contains("#system-health", settings);
        var panel = File.ReadAllText(Path.Combine(root, "components", "SystemHealthPanel.tsx"));
        Assert.Contains("healthDetail", panel);
        Assert.Contains("system-health", panel);
        Assert.Contains("redacted", panel);
        Assert.DoesNotContain("County", panel, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", panel, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Demo_pdf_is_seeded_into_blob_storage()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var blobs = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
        var ready = await db.Documents.AsNoTracking()
            .FirstAsync(x => x.Status == DocumentStatuses.Ready && x.BlobPath.StartsWith("deeds/demo/"));
        Assert.True(await blobs.ExistsAsync(ready.BlobPath, CancellationToken.None));
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> FirstReadyId(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents?status=Ready");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ready = json.RootElement.EnumerateArray().First(x => x.GetProperty("status").GetString() == DocumentStatuses.Ready);
        return ready.GetProperty("id").GetGuid();
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
