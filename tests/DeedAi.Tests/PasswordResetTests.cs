using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain.Abstractions;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class PasswordResetTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public PasswordResetTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Forgot_and_reset_happy_path_updates_password()
    {
        var client = _factory.CreateJsonClient();
        var forgot = await client.PostAsync("/api/auth/forgot-password", TestAppFactory.Json("""{"email":"viewer@bisconsultants.com"}"""));
        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Contains("reset link", await forgot.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var email = LastEmailTo("viewer@bisconsultants.com");
        Assert.NotNull(email);
        var token = ExtractToken(email!.TextBody);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await client.PostAsync(
            "/api/auth/reset-password",
            TestAppFactory.Json("{\"token\":\"" + token + "\",\"password\":\"NewPass!99\"}"));
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        var oldLogin = await client.PostAsync("/api/auth/login", TestAppFactory.Json("""{"email":"viewer@bisconsultants.com","password":"ChangeMe!1"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        var tokenValue = await _factory.LoginAsync(client, "viewer@bisconsultants.com", "NewPass!99");
        Assert.False(string.IsNullOrWhiteSpace(tokenValue));
    }

    [Fact]
    public async Task Reset_fails_for_unknown_token()
    {
        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync(
            "/api/auth/reset-password",
            TestAppFactory.Json("""{"token":"not-a-real-token","password":"NewPass!99"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid or has expired", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reset_fails_for_expired_token()
    {
        var raw = TokenHasher.NewToken();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            db.PasswordResetTokens.Add(new PasswordResetToken
            {
                Id = Guid.NewGuid(),
                UserId = DatabaseSeeder.ViewerId,
                TokenHash = TokenHasher.Hash(raw),
                CreatedAt = DateTimeOffset.UtcNow.AddHours(-3),
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync(
            "/api/auth/reset-password",
            TestAppFactory.Json("{\"token\":\"" + raw + "\",\"password\":\"NewPass!99\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("invalid or has expired", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Forgot_does_not_reveal_unknown_email()
    {
        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync("/api/auth/forgot-password", TestAppFactory.Json("""{"email":"nobody@bisconsultants.com"}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("If that email is on file", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reset_rejects_weak_password_before_token_lookup()
    {
        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync(
            "/api/auth/reset-password",
            TestAppFactory.Json("""{"token":"not-checked-yet","password":"password"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("uppercase", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("symbol", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"field\":\"password\"", body);
    }

    [Fact]
    public async Task Disabled_user_cannot_sign_in()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var user = await db.Users.FindAsync(DatabaseSeeder.UploaderId);
            Assert.NotNull(user);
            user!.IsActive = false;
            await db.SaveChangesAsync();
        }

        var client = _factory.CreateJsonClient();
        var response = await client.PostAsync("/api/auth/login", TestAppFactory.Json("""{"email":"uploader@bisconsultants.com","password":"ChangeMe!1"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("disabled", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    private EmailMessage? LastEmailTo(string to) =>
        _factory.GetRequiredService<RecordingEmailSender>().Sent.FirstOrDefault(x => x.To == to);

    private static string ExtractToken(string body)
    {
        const string marker = "Reset token: ";
        var start = body.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, body);
        return body[(start + marker.Length)..].Split('\n', '\r')[0].Trim();
    }
}

public sealed class UsersAuthZTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public UsersAuthZTests(TestAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    public async Task Non_admin_cannot_manage_users(string email)
    {
        var client = await Authed(email);
        var list = await client.GetAsync("/api/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, list.StatusCode);
        Assert.Contains("Access denied", await list.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var create = await client.PostAsync("/api/admin/users", TestAppFactory.Json("""{"email":"x@y.com","displayName":"X","role":"Viewer","password":"ChangeMe!1","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
    }

    [Fact]
    public async Task Admin_can_create_assign_role_and_map_clients()
    {
        var client = await Authed("admin@bisconsultants.com");
        var response = await client.PostAsync("/api/admin/users", TestAppFactory.Json(
            "{\"email\":\"pat@bisconsultants.com\",\"displayName\":\"Pat\",\"fullName\":\"Pat Editor\",\"role\":\"Editor\",\"password\":\"ChangeMe!2\",\"isActive\":true,\"clientIds\":[\"" + DatabaseSeeder.NorthsideId + "\"]}"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Editor", json.RootElement.GetProperty("role").GetString());
        Assert.Equal("Pat Editor", json.RootElement.GetProperty("fullName").GetString());
        Assert.False(json.RootElement.GetProperty("hasPhoto").GetBoolean());
        Assert.Equal(1, json.RootElement.GetProperty("clientIds").GetArrayLength());
        Assert.Equal(DatabaseSeeder.NorthsideId, json.RootElement.GetProperty("clientIds")[0].GetGuid());
    }

    [Fact]
    public async Task Admin_cannot_create_user_with_weak_password()
    {
        var client = await Authed("admin@bisconsultants.com");
        var response = await client.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"weak@bisconsultants.com","displayName":"Weak","role":"Viewer","password":"password","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("uppercase", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("digit", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("symbol", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"field\":\"password\"", body);
    }

    [Fact]
    public async Task Admin_cannot_change_user_to_weak_password()
    {
        var client = await Authed("admin@bisconsultants.com");
        var response = await client.PutAsync(
            $"/api/admin/users/{DatabaseSeeder.ViewerId}",
            TestAppFactory.Json(
                "{\"email\":\"viewer@bisconsultants.com\",\"displayName\":\"Riley\",\"role\":\"Viewer\",\"password\":\"NoSymbol12\",\"isActive\":true,\"clientIds\":[]}"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("symbol", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"field\":\"password\"", body);
    }

    [Fact]
    public async Task Viewer_cannot_change_settings()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.PostAsync("/api/settings/flags", TestAppFactory.Json("""{"name":"Nope","color":"#000","sortOrder":1,"isActive":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Viewer", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Client_access_hides_other_clients_documents()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var access = db.UserClientAccess.Where(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId);
            db.UserClientAccess.RemoveRange(access);
            await db.SaveChangesAsync();
        }

        var client = await Authed("viewer@bisconsultants.com");
        var list = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Acme", body);
        Assert.Contains("Northside", body);
    }

    [Fact]
    public async Task Viewer_cannot_push_to_software()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var id = await FirstDocumentId(client);
        var response = await client.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Access denied", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
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
}
