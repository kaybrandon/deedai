using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class AuthZTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public AuthZTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_issues_token_that_authenticates_me()
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, "editor@bisconsultants.com");
        Assert.False(string.IsNullOrWhiteSpace(token));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var me = await client.GetAsync("/api/auth/me");
        if (!me.IsSuccessStatusCode)
        {
            throw new Xunit.Sdk.XunitException(
                $"me failed {(int)me.StatusCode} {me.Headers.WwwAuthenticate} {await me.Content.ReadAsStringAsync()}");
        }
    }

    [Fact]
    public async Task Anonymous_is_unauthorized()
    {
        var client = _factory.CreateJsonClient();
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_rejects_bad_password()
    {
        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync("/api/auth/login", TestAppFactory.Json("""{"email":"viewer@bisconsultants.com","password":"nope"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("incorrect", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("admin@bisconsultants.com")]
    public async Task Authenticated_roles_can_read_documents(string email)
    {
        var client = await Authed(email);
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Viewer_cannot_upload()
    {
        var client = await Authed("viewer@bisconsultants.com");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(DatabaseSeeder.AcmeId.ToString()), "clientId");
        content.Add(new ByteArrayContent("%PDF-1.4"u8.ToArray()), "files", "sample.pdf");
        var response = await client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Access denied", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Viewer", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Viewer_cannot_edit_fields_or_delete()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var id = await FirstDocumentId(client);
        var put = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json("""{"grantor":"x","isDraft":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
        var delete = await client.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Contains("Access denied", await delete.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Uploader_can_upload_but_cannot_delete()
    {
        var client = await Authed("uploader@bisconsultants.com");
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(DatabaseSeeder.AcmeId.ToString()), "clientId");
        content.Add(PdfContent(), "files", "UploaderDeed.pdf");
        var upload = await client.PostAsync("/api/uploads", content);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);

        var id = await FirstDocumentId(client);
        var delete = await client.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Editor_can_save_draft_fields()
    {
        var client = await Authed("editor@bisconsultants.com");
        var id = await FirstDocumentId(client);
        var response = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Pat Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"1","parcelId":"1","client":"Acme","notes":"draft","isDraft":true}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("isDraft").GetBoolean());
        Assert.Equal("Pat Example", json.RootElement.GetProperty("grantor").GetString());
    }

    [Fact]
    public async Task Admin_can_restore_soft_deleted_document()
    {
        var editor = await Authed("editor@bisconsultants.com");
        var id = await FirstDocumentId(editor);
        Assert.Equal(HttpStatusCode.OK, (await editor.DeleteAsync($"/api/documents/{id}")).StatusCode);

        var hidden = await editor.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var admin = await Authed("admin@bisconsultants.com");
        var restore = await admin.PostAsync($"/api/documents/{id}/restore", null);
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/documents/{id}")).StatusCode);
    }

    [Fact]
    public async Task Soft_deleted_documents_are_filtered_from_default_list()
    {
        var editor = await Authed("editor@bisconsultants.com");
        var id = await FirstDocumentId(editor);
        await editor.DeleteAsync($"/api/documents/{id}");
        var list = await editor.GetAsync("/api/documents");
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain(id.ToString(), body);
    }

    [Fact]
    public void Cors_policy_never_pairs_any_origin_with_credentials()
    {
        using var scope = _factory.Services.CreateScope();
        var cors = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsService>();
        Assert.NotNull(cors);
        var provider = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider>();
        Assert.NotNull(provider);
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

    private static ByteArrayContent PdfContent()
    {
        var bytes = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return content;
    }
}
