using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Domain.Ocr;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Ocr;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase3Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase3Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void UploadedBy_fk_is_no_action_so_sql_server_allows_assignee_set_null()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var document = db.Model.FindEntityType(typeof(Document));
        Assert.NotNull(document);

        var uploadedBy = document.FindNavigation(nameof(Document.UploadedBy))?.ForeignKey;
        var assignee = document.FindNavigation(nameof(Document.Assignee))?.ForeignKey;
        Assert.NotNull(uploadedBy);
        Assert.NotNull(assignee);
        Assert.Equal(DeleteBehavior.NoAction, uploadedBy.DeleteBehavior);
        Assert.Equal(DeleteBehavior.SetNull, assignee.DeleteBehavior);
    }

    [Fact]
    public void Phase3_sql_server_creates_uploadedby_fk_with_no_action()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        ApplyPhase3Up(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("FK_Documents_Users_UploadedByUserId", sql, StringComparison.Ordinal);
        Assert.Contains("ON DELETE NO ACTION", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ON DELETE SET NULL", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase3_non_sql_server_uploadedby_fk_is_no_action()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        ApplyPhase3Up(builder);

        var fk = builder.Operations.OfType<AddForeignKeyOperation>()
            .Single(x => x.Name == "FK_Documents_Users_UploadedByUserId");
        Assert.Equal(ReferentialAction.NoAction, fk.OnDelete);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    public async Task Authenticated_roles_can_export_report_pdf(string email)
    {
        var client = await Authed(email);
        var response = await client.GetAsync("/api/reports/documents?format=pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 8);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4].ToArray());
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("Deed AI", text);
        Assert.DoesNotContain("CAMA", text, StringComparison.Ordinal);
        Assert.DoesNotContain("County", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Report_pdf_empty_filter_is_not_a_blank_file()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync($"/api/reports/documents?status=Ready&clientId={Guid.NewGuid()}&format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("blank PDF is not returned", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%PDF", body);
    }

    [Fact]
    public async Task Reviewed_deed_pdf_uses_client_gate_and_exports_fields()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var readyId = await FirstReadyId(client);
        var response = await client.GetAsync($"/api/reports/documents/{readyId}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4].ToArray());
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("Reviewed deed", text);
        Assert.Contains("Jane Example", text);
        Assert.Contains("Acme", text);
    }

    [Fact]
    public async Task Reviewed_deed_pdf_without_fields_is_not_blank()
    {
        var client = await Authed("viewer@bisconsultants.com");
        Guid queuedId;
        using (var json = JsonDocument.Parse(await (await client.GetAsync("/api/documents")).Content.ReadAsStringAsync()))
        {
            queuedId = json.RootElement.EnumerateArray().First(x => x.GetProperty("status").GetString() == "Queued").GetProperty("id").GetGuid();
        }

        var response = await client.GetAsync($"/api/reports/documents/{queuedId}/pdf");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not ready to export", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com", "/api/settings/teams")]
    [InlineData("editor@bisconsultants.com", "/api/settings/clients")]
    [InlineData("uploader@bisconsultants.com", "/api/settings/notifications")]
    [InlineData("viewer@bisconsultants.com", "/api/settings/flags")]
    public async Task Non_admin_cannot_mutate_new_settings(string email, string path)
    {
        var client = await Authed(email);
        var response = await client.PostAsync(path, TestAppFactory.Json("""{"name":"Nope","isActive":true,"userIds":[],"enabled":true,"notifyUploader":false,"color":"#000","sortOrder":1,"displayName":"Nope","code":"X"}"""));
        if (path.EndsWith("notifications", StringComparison.Ordinal))
        {
            response = await client.PutAsync(path, TestAppFactory.Json("""{"enabled":false,"notifyUploader":true}"""));
        }

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_cannot_retry_software_push()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var id = await FirstReadyId(client);
        var response = await client.PostAsync($"/api/documents/{id}/software/retry", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editor_can_lookup_by_key_fields_and_retry_push()
    {
        var client = await Authed("editor@bisconsultants.com");
        var id = await FirstReadyId(client);
        var lookup = await client.PostAsync($"/api/documents/{id}/software/lookup", null);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var lookupBody = await lookup.Content.ReadAsStringAsync();
        Assert.Contains("Jane Example", lookupBody);
        Assert.DoesNotContain("CAMA", lookupBody, StringComparison.Ordinal);

        var retry = await client.PostAsync($"/api/documents/{id}/software/retry", null);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var json = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("succeeded").GetBoolean());
        Assert.Equal("Ok", json.RootElement.GetProperty("lastSyncStatus").GetString());

        var detail = await client.GetAsync($"/api/documents/{id}");
        using var deed = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal("Ok", deed.RootElement.GetProperty("lastSoftwareSyncStatus").GetString());
        Assert.Equal("Retry", deed.RootElement.GetProperty("lastSoftwareSyncDirection").GetString());
        Assert.False(string.IsNullOrWhiteSpace(deed.RootElement.GetProperty("softwareRecordId").GetString()));
    }

    [Fact]
    public async Task Software_lookup_without_parcel_uses_grantor()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync("/api/software/lookup?grantor=Jane%20Example&client=Acme");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Jane Example", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Admin_can_crud_teams_and_clients()
    {
        var client = await Authed("admin@bisconsultants.com");
        var team = await client.PostAsync("/api/settings/teams", TestAppFactory.Json(
            "{\"name\":\"QA\",\"isActive\":true,\"userIds\":[\"" + DatabaseSeeder.EditorId + "\"]}"));
        Assert.Equal(HttpStatusCode.OK, team.StatusCode);
        using var teamJson = JsonDocument.Parse(await team.Content.ReadAsStringAsync());
        var teamId = teamJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("QA", teamJson.RootElement.GetProperty("name").GetString());
        Assert.Equal(1, teamJson.RootElement.GetProperty("members").GetArrayLength());

        var created = await client.PostAsync("/api/settings/clients", TestAppFactory.Json("""{"name":"Westside","isActive":true}"""));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var clientJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var clientId = clientJson.RootElement.GetProperty("id").GetGuid();
        Assert.Equal("Westside", clientJson.RootElement.GetProperty("name").GetString());
        Assert.True(clientJson.RootElement.GetProperty("isActive").GetBoolean());

        var deactivated = await client.PutAsync($"/api/settings/clients/{clientId}", TestAppFactory.Json("""{"name":"Westside","isActive":false}"""));
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        var visible = await client.GetAsync("/api/clients");
        var visibleBody = await visible.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Westside", visibleBody);

        var export = await client.GetAsync("/api/settings/export?format=json");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        var exportBody = await export.Content.ReadAsStringAsync();
        Assert.Contains("Westside", exportBody);
        Assert.Contains("QA", exportBody);
        Assert.Contains("Notifications", exportBody);

        var deleteTeam = await client.DeleteAsync($"/api/settings/teams/{teamId}");
        Assert.Equal(HttpStatusCode.OK, deleteTeam.StatusCode);
    }

    [Fact]
    public async Task Notify_preview_shows_assignee_and_admin_can_switch_off()
    {
        var client = await Authed("editor@bisconsultants.com");
        var id = await FirstReadyId(client);
        var preview = await client.GetAsync($"/api/documents/{id}/notify-preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        using var json = JsonDocument.Parse(await preview.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Contains("Ready", json.RootElement.GetProperty("events").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("OCR Failed", json.RootElement.GetProperty("events").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains(json.RootElement.GetProperty("recipients").EnumerateArray(), x => x.GetProperty("reason").GetString() == "Assignee");

        var admin = await Authed("admin@bisconsultants.com");
        var off = await admin.PutAsync("/api/settings/notifications", TestAppFactory.Json("""{"enabled":false,"notifyUploader":false}"""));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        using var offJson = JsonDocument.Parse(await off.Content.ReadAsStringAsync());
        Assert.False(offJson.RootElement.GetProperty("enabled").GetBoolean());

        await admin.PutAsync("/api/settings/notifications", TestAppFactory.Json("""{"enabled":true,"notifyUploader":false}"""));
    }

    [Fact]
    public async Task Ocr_ready_emails_assignee_and_off_switch_stops_mail()
    {
        var recorder = _factory.GetRequiredService<RecordingEmailSender>();
        recorder.Clear();

        Guid documentId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var blobs = scope.ServiceProvider.GetRequiredService<IBlobStorage>();
            documentId = Guid.NewGuid();
            var blobPath = $"deeds/notify-{documentId:N}.pdf";
            db.Documents.Add(new Document
            {
                Id = documentId,
                Name = "Notify_ready.pdf",
                ClientId = DatabaseSeeder.AcmeId,
                Status = DocumentStatuses.Queued,
                BlobPath = blobPath,
                AssigneeUserId = DatabaseSeeder.EditorId,
                UploadedByUserId = DatabaseSeeder.UploaderId,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
            await using var pdf = new MemoryStream("%PDF-1.4 test"u8.ToArray());
            await blobs.UploadAsync(blobPath, pdf, "application/pdf", CancellationToken.None);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var processor = scope.ServiceProvider.GetRequiredService<OcrProcessor>();
            var document = await scope.ServiceProvider.GetRequiredService<DeedAiDbContext>().Documents.AsNoTracking().FirstAsync(x => x.Id == documentId);

            await processor.ProcessAsync(new OcrQueueDelivery
            {
                Job = new OcrJobMessage { DocumentId = documentId, BlobPath = document.BlobPath },
                MessageId = "n1",
                PopReceipt = "r",
                DequeueCount = 1
            }, CancellationToken.None);
        }

        Assert.Contains(recorder.Sent, x => x.To == "editor@bisconsultants.com" && x.Subject.Contains("Ready", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(recorder.Sent, x => x.To == "uploader@bisconsultants.com");

        recorder.Clear();
        var admin = await Authed("admin@bisconsultants.com");
        await admin.PutAsync("/api/settings/notifications", TestAppFactory.Json("""{"enabled":false,"notifyUploader":true}"""));

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var doc = await db.Documents.FirstAsync(x => x.Id == documentId);
            doc.Status = DocumentStatuses.Queued;
            await db.SaveChangesAsync();
            var processor = scope.ServiceProvider.GetRequiredService<OcrProcessor>();
            await processor.ProcessAsync(new OcrQueueDelivery
            {
                Job = new OcrJobMessage { DocumentId = documentId, BlobPath = doc.BlobPath },
                MessageId = "n2",
                PopReceipt = "r",
                DequeueCount = 1
            }, CancellationToken.None);
        }

        Assert.Empty(recorder.Sent);
        await admin.PutAsync("/api/settings/notifications", TestAppFactory.Json("""{"enabled":true,"notifyUploader":false}"""));
    }

    [Fact]
    public void SendGrid_and_software_secrets_read_kv_names_only()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SendGrid__ApiKey"] = "sg-from-kv",
            ["Software__ApiKey"] = "sw-from-kv"
        }).Build();
        Assert.Equal("sg-from-kv", DependencyInjection.FirstValue(config, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey"));
        Assert.Equal("sw-from-kv", DependencyInjection.FirstValue(config, "SoftwareApiKey", "Software:ApiKey", "Software__ApiKey"));
    }

    [Fact]
    public async Task Client_access_hides_other_clients_from_report_pdf()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var access = db.UserClientAccess.Where(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId);
            db.UserClientAccess.RemoveRange(access);
            await db.SaveChangesAsync();
        }

        try
        {
            var client = await Authed("viewer@bisconsultants.com");
            var response = await client.GetAsync("/api/reports/documents?format=pdf");
            var text = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain("Acme", text);
            Assert.Contains("Northside", text);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            if (!await db.UserClientAccess.AnyAsync(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId))
            {
                db.UserClientAccess.Add(new UserClientAccess { UserId = DatabaseSeeder.ViewerId, ClientId = DatabaseSeeder.AcmeId });
                await db.SaveChangesAsync();
            }
        }
    }

    private static void ApplyPhase3Up(MigrationBuilder builder)
    {
        var up = typeof(Phase3).GetMethod("Up", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new Phase3(), [builder]);
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
        var response = await client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.EnumerateArray().First(x => x.GetProperty("status").GetString() == "Ready").GetProperty("id").GetGuid();
    }
}
