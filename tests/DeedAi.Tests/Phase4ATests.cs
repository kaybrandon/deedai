using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase4ATests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase4ATests(TestAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    public async Task Viewer_and_uploader_cannot_push_to_software(string email)
    {
        var client = await Authed(email);
        var id = await FirstDocumentId(client);
        var response = await client.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    public async Task Viewer_and_uploader_cannot_open_sales(string email)
    {
        var client = await Authed(email);
        var response = await client.GetAsync("/api/sales");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editor_can_read_sales()
    {
        var client = await Authed("editor@bisconsultants.com");
        var response = await client.GetAsync("/api/sales");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0);
        Assert.False(json.RootElement[0].TryGetProperty("county", out _));
    }

    [Theory]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    [InlineData("viewer@bisconsultants.com")]
    public async Task Non_admin_cannot_list_or_hard_delete_deleted_deeds(string email)
    {
        var client = await Authed(email);
        var list = await client.GetAsync("/api/admin/documents/deleted");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);

        var hard = await client.DeleteAsync($"/api/admin/documents/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Forbidden, hard.StatusCode);

        var purge = await client.PostAsync("/api/admin/documents/purge-deleted", null);
        Assert.Equal(HttpStatusCode.Forbidden, purge.StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_restore_and_hard_delete_soft_deleted_deed()
    {
        var editor = await Authed("editor@bisconsultants.com");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(DatabaseSeeder.AcmeId.ToString()), "clientId");
        var pdf = new ByteArrayContent("%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray());
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(pdf, "files", "RestoreMe.pdf");
        var upload = await editor.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        using var uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
        var id = uploaded.RootElement.GetProperty("documents")[0].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await editor.DeleteAsync($"/api/documents/{id}")).StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var listed = await admin.GetAsync($"/api/admin/documents/deleted?clientId={DatabaseSeeder.AcmeId}");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains(id.ToString(), await listed.Content.ReadAsStringAsync());

        var restore = await admin.PostAsync($"/api/documents/{id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/documents/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/documents/{id}")).StatusCode);
        var hard = await admin.DeleteAsync($"/api/admin/documents/{id}");
        Assert.Equal(HttpStatusCode.OK, hard.StatusCode);

        var after = await admin.GetAsync("/api/admin/documents/deleted");
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.DoesNotContain(id.ToString(), await after.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Report_filters_include_flag_and_date_range()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var flagged = await client.GetAsync($"/api/reports/documents?flagId={DatabaseSeeder.NeedsReviewFlagId}");
        Assert.Equal(HttpStatusCode.OK, flagged.StatusCode);
        using var json = JsonDocument.Parse(await flagged.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() >= 1);
        Assert.Contains("Needs review", json.RootElement[0].GetProperty("flags").EnumerateArray().Select(x => x.GetString()));

        var empty = await client.GetAsync("/api/reports/documents?from=1990-01-01T00:00:00Z&to=1990-01-02T00:00:00Z&format=pdf");
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("blank PDF is not returned", await empty.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Admin_can_manage_field_maps_and_non_admin_cannot()
    {
        var viewer = await Authed("viewer@bisconsultants.com");
        var denied = await viewer.PostAsync("/api/software/field-maps", TestAppFactory.Json(
            """{"deedField":"grantor","softwareField":"Owner","softwareGroup":"Parties","isActive":true,"sortOrder":1}"""));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var created = await admin.PostAsync("/api/software/field-maps", TestAppFactory.Json(
            """{"deedField":"notes","softwareField":"Memo","softwareGroup":"Notes","isActive":true,"sortOrder":20}"""));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("notes", json.RootElement.GetProperty("deedField").GetString());
        Assert.Equal("Memo", json.RootElement.GetProperty("softwareField").GetString());
    }

    [Fact]
    public async Task Property_reset_is_admin_only_and_clears_client_defaults()
    {
        var editor = await Authed("editor@bisconsultants.com");
        var denied = await editor.PostAsync("/api/settings/property-defaults/reset", TestAppFactory.Json(
            "{\"scope\":\"Client\",\"clientId\":\"" + DatabaseSeeder.AcmeId + "\"}"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var reset = await admin.PostAsync("/api/settings/property-defaults/reset", TestAppFactory.Json(
            "{\"scope\":\"Client\",\"clientId\":\"" + DatabaseSeeder.AcmeId + "\"}"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains("Reset", await reset.Content.ReadAsStringAsync());

        var list = await admin.GetAsync("/api/settings/property-defaults");
        using var json = JsonDocument.Parse(await list.Content.ReadAsStringAsync());
        Assert.DoesNotContain(
            json.RootElement.EnumerateArray(),
            x => x.GetProperty("scope").GetString() == "Client"
                 && x.GetProperty("clientId").GetGuid() == DatabaseSeeder.AcmeId);
    }

    [Fact]
    public async Task Disabled_software_push_is_rejected_for_editor()
    {
        var admin = await Authed("admin@bisconsultants.com");
        var disable = await admin.PutAsync("/api/software/settings", TestAppFactory.Json(
            """{"pushEnabled":false,"defaultGroup":"Property","fieldDefaultsJson":"{\"consideration\":\"0\"}"}"""));
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);

        var editor = await Authed("editor@bisconsultants.com");
        var id = await FirstDocumentId(editor);
        var push = await editor.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.BadRequest, push.StatusCode);
        Assert.Contains("disabled", await push.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var enable = await admin.PutAsync("/api/software/settings", TestAppFactory.Json(
            """{"pushEnabled":true,"defaultGroup":"Property","fieldDefaultsJson":null}"""));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
    }

    [Fact]
    public async Task Software_status_does_not_expose_secrets()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync("/api/software/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"mode\"", body);
        Assert.DoesNotContain("ApiKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PLACEHOLDER", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("County", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Editor_can_push_when_enabled()
    {
        var client = await Authed("editor@bisconsultants.com");
        var id = await FirstReadyId(client);
        var response = await client.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("succeeded").GetBoolean());
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> FirstDocumentId(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0);
        return json.RootElement[0].GetProperty("id").GetGuid();
    }

    private static async Task<Guid> FirstReadyId(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents?status=Ready");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ready = json.RootElement.EnumerateArray().FirstOrDefault(x => x.GetProperty("status").GetString() == DocumentStatuses.Ready);
        Assert.NotEqual(default, ready.ValueKind);
        return ready.GetProperty("id").GetGuid();
    }
}
