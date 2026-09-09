using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Api.Auth;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase45Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase45Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Me_includes_effective_clients_and_profile_fields()
    {
        var client = await Authed(DatabaseSeeder.AdminEmail);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal("Admin", json.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("Admin User", json.RootElement.GetProperty("fullName").GetString());
        Assert.False(json.RootElement.GetProperty("hasPhoto").GetBoolean());
        Assert.True(json.RootElement.GetProperty("clients").GetArrayLength() >= 2);
        Assert.DoesNotContain("County", await me.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", await me.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Single_client_scope_is_exactly_one_client_on_me()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var created = await admin.PostAsync("/api/admin/users", TestAppFactory.Json(
            "{\"email\":\"northonly@bisconsultants.com\",\"displayName\":\"North\",\"fullName\":\"North Only\",\"role\":\"Viewer\",\"password\":\"ChangeMe!1\",\"isActive\":true,\"clientIds\":[\"" + DatabaseSeeder.NorthsideId + "\"]}"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await MarkVerified("northonly@bisconsultants.com");

        var client = await Authed("northonly@bisconsultants.com");
        var me = await client.GetAsync("/api/auth/me");
        using var json = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("clients").GetArrayLength());
        Assert.Equal(DatabaseSeeder.NorthsideId, json.RootElement.GetProperty("clients")[0].GetProperty("id").GetGuid());
        Assert.Equal("Northside", json.RootElement.GetProperty("clients")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_user_requires_full_name()
    {
        var client = await Authed(DatabaseSeeder.AdminEmail);
        var response = await client.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"nofull@bisconsultants.com","displayName":"No Full","role":"Viewer","password":"ChangeMe!1","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Full name is required", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"field\":\"fullName\"", body);
    }

    [Fact]
    public async Task User_can_update_own_profile_and_password()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var created = await admin.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"pat.profile@bisconsultants.com","displayName":"Pat","fullName":"Pat Profile","role":"Editor","password":"ChangeMe!1","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        await MarkVerified("pat.profile@bisconsultants.com");

        var client = await Authed("pat.profile@bisconsultants.com");
        var response = await client.PutAsync("/api/auth/me", TestAppFactory.Json(
            """{"displayName":"Pat P","fullName":"Patricia Profile","password":"ChangeMe!9"}"""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Pat P", json.RootElement.GetProperty("displayName").GetString());
        Assert.Equal("Patricia Profile", json.RootElement.GetProperty("fullName").GetString());

        var fresh = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(fresh, "pat.profile@bisconsultants.com", "ChangeMe!9");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Profile_rejects_weak_password()
    {
        var client = await Authed(DatabaseSeeder.ViewerEmail);
        var response = await client.PutAsync("/api/auth/me", TestAppFactory.Json(
            """{"displayName":"Riley","fullName":"Riley Viewer","password":"password"}"""));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("uppercase", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"field\":\"password\"", body);
    }

    [Fact]
    public async Task Photo_upload_replace_clear_and_limits()
    {
        var client = await Authed(DatabaseSeeder.AdminEmail);
        using (var first = Jpeg("file", MinimalJpeg()))
        {
            var upload = await client.PostAsync("/api/auth/me/photo", first);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var uploaded = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            Assert.True(uploaded.RootElement.GetProperty("hasPhoto").GetBoolean());
        }

        var photo = await client.GetAsync($"/api/users/{DatabaseSeeder.AdminId}/photo");
        Assert.Equal(HttpStatusCode.OK, photo.StatusCode);
        Assert.Equal("image/jpeg", photo.Content.Headers.ContentType?.MediaType);
        Assert.True((await photo.Content.ReadAsByteArrayAsync()).Length > 8);

        using (var replace = Png("file", MinimalPng()))
        {
            var again = await client.PostAsync("/api/auth/me/photo", replace);
            Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        }

        var replaced = await client.GetAsync($"/api/users/{DatabaseSeeder.AdminId}/photo");
        Assert.Equal("image/png", replaced.Content.Headers.ContentType?.MediaType);

        var clear = await client.DeleteAsync("/api/auth/me/photo");
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        using var cleared = JsonDocument.Parse(await clear.Content.ReadAsStringAsync());
        Assert.False(cleared.RootElement.GetProperty("hasPhoto").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/users/{DatabaseSeeder.AdminId}/photo")).StatusCode);

        using (var pdf = Pdf("file"))
        {
            var badType = await client.PostAsync("/api/auth/me/photo", pdf);
            Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
            Assert.Contains("2 MB", await badType.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }

        using (var huge = Jpeg("file", new byte[ProfilePhotos.MaxBytes + 8]))
        {
            var badSize = await client.PostAsync("/api/auth/me/photo", huge);
            Assert.Equal(HttpStatusCode.BadRequest, badSize.StatusCode);
        }
    }

    [Fact]
    public async Task Admin_can_set_user_photo_non_admin_cannot()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        using (var jpeg = Jpeg("file", MinimalJpeg()))
        {
            var upload = await admin.PostAsync($"/api/admin/users/{DatabaseSeeder.UploaderId}/photo", jpeg);
            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            using var json = JsonDocument.Parse(await upload.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.GetProperty("hasPhoto").GetBoolean());
        }

        var editor = await Authed(DatabaseSeeder.EditorEmail);
        using (var jpeg = Jpeg("file", MinimalJpeg()))
        {
            var denied = await editor.PostAsync($"/api/admin/users/{DatabaseSeeder.UploaderId}/photo", jpeg);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }
    }

    [Fact]
    public void Spa_groups_users_by_client_and_wires_identity()
    {
        var users = Read("spa/src/pages/UsersPage.tsx");
        Assert.Contains("user.clientIds.includes(client.id)", users, StringComparison.Ordinal);
        Assert.Contains("user-group-toggle", users, StringComparison.Ordinal);
        Assert.Contains("Full Name", users, StringComparison.Ordinal);
        Assert.Contains("PasswordPair", users, StringComparison.Ordinal);
        Assert.Contains("PhotoEditor", users, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", users, StringComparison.Ordinal);
        Assert.DoesNotContain("County", users, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", users, StringComparison.Ordinal);

        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.Contains("clients?.length === 1", shell, StringComparison.Ordinal);
        Assert.Contains("${clients[0].name} Deed AI", shell, StringComparison.Ordinal);
        Assert.Contains("return \"Deed AI\"", shell, StringComparison.Ordinal);
        Assert.Contains("Logged In As", shell, StringComparison.Ordinal);
        Assert.Contains("My Profile", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/profile\"", shell, StringComparison.Ordinal);
        Assert.Contains("UserAvatar", shell, StringComparison.Ordinal);

        var profile = Read("spa/src/pages/ProfilePage.tsx");
        Assert.Contains("My Profile", profile, StringComparison.Ordinal);
        Assert.Contains("Full Name", profile, StringComparison.Ordinal);
        Assert.Contains("PasswordPair", profile, StringComparison.Ordinal);
        Assert.Contains("PhotoEditor", profile, StringComparison.Ordinal);
        Assert.Contains("Assigned Client(s)", profile, StringComparison.Ordinal);
        Assert.Contains("me?.clients", profile, StringComparison.Ordinal);
        Assert.Contains("No Client assigned", profile, StringComparison.Ordinal);
        Assert.Contains("EmptyState", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("County", profile, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", profile, StringComparison.Ordinal);

        var photo = Read("spa/src/components/PhotoEditor.tsx");
        Assert.Contains("ConfirmSheet", photo, StringComparison.Ordinal);
        Assert.Contains("Remove this photo?", photo, StringComparison.Ordinal);

        var pair = Read("spa/src/components/PasswordPair.tsx");
        Assert.Contains("Confirm New Password", pair, StringComparison.Ordinal);
        Assert.Contains("confirmPasswordError", pair, StringComparison.Ordinal);

        var css = Read("spa/src/styles.css");
        Assert.Contains(".sidebar-profile", css, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--action-h)", css, StringComparison.Ordinal);
        Assert.Contains(".user-group-toggle", css, StringComparison.Ordinal);
        Assert.Contains(".topbar-title", css, StringComparison.Ordinal);
        Assert.Contains(".profile-client-chip", css, StringComparison.Ordinal);
        Assert.Contains(".profile-client-list { display: grid; grid-template-columns: 1fr; }", css, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_with_no_client_access_returns_empty_clients()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var created = await admin.PostAsync("/api/admin/users", TestAppFactory.Json(
            """{"email":"noclient.profile@bisconsultants.com","displayName":"No Client","fullName":"No Client Profile","role":"Viewer","password":"ChangeMe!1","isActive":true,"clientIds":[]}"""));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var client = await Authed("noclient.profile@bisconsultants.com");
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var body = await me.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal(0, json.RootElement.GetProperty("clients").GetArrayLength());
        Assert.Equal(0, json.RootElement.GetProperty("clientIds").GetArrayLength());
        Assert.DoesNotContain("County", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_settings_nav_is_systems_with_existing_route_and_admin_gate()
    {
        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.Contains("aria-controls=\"settings-nav\"", shell, StringComparison.Ordinal);
        Assert.Contains("              Settings\n              <span className=\"nav-group-caret\"", shell, StringComparison.Ordinal);
        Assert.Contains("<NavLink to=\"/settings\" end onClick={closeNav}>\n                    Systems", shell, StringComparison.Ordinal);
        Assert.Contains("navigate(\"/denied\", { state: { action: \"change settings\" } })", shell, StringComparison.Ordinal);
        Assert.Contains("                    Systems\n                  </button>", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("<NavLink to=\"/settings\" end onClick={closeNav}>\n                    Settings", shell, StringComparison.Ordinal);
        Assert.Contains("canAdmin", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/software\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/users\"", shell, StringComparison.Ordinal);

        var settings = Read("spa/src/pages/SettingsPage.tsx");
        Assert.Contains("<h1>Systems</h1>", settings, StringComparison.Ordinal);
        Assert.Contains("if (!canAdmin)", settings, StringComparison.Ordinal);
        Assert.Contains("navigate(\"/denied\", { state: { action: \"change settings\" } })", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("County", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", settings, StringComparison.Ordinal);

        var app = Read("spa/src/App.tsx");
        Assert.Contains("<Route path=\"/settings\" element={<SettingsPage />} />", app, StringComparison.Ordinal);
        Assert.Contains("<Route path=\"/profile\" element={<ProfilePage />} />", app, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase45_migration_is_designer_first_after_phase41()
    {
        const string id = "20260909160000_Phase45UsersIdentity";
        Assert.True(string.CompareOrdinal("20260909140000_Phase41SwaggerHelp", id) < 0);

        var type = typeof(Phase45UsersIdentity);
        Assert.Equal(id, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(id, discovered);
        Assert.Equal(typeof(Phase45UsersIdentity), db.GetService<IMigrationsAssembly>().Migrations[id].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260909160000_Phase45UsersIdentity.Designer.cs");
        Assert.Contains("[Migration(\"20260909160000_Phase45UsersIdentity\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        Assert.Contains("FullName", designer, StringComparison.Ordinal);
        Assert.Contains("PhotoBlobPath", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task MarkVerified(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var user = await db.Users.SingleAsync(x => x.Email == email);
        user.EmailVerified = true;
        await db.SaveChangesAsync();
    }

    private static MultipartFormDataContent Jpeg(string name, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(part, name, "avatar.jpg");
        return content;
    }

    private static MultipartFormDataContent Png(string name, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(part, name, "avatar.png");
        return content;
    }

    private static MultipartFormDataContent Pdf(string name)
    {
        var content = new MultipartFormDataContent();
        var part = new ByteArrayContent("%PDF-1.4\n"u8.ToArray());
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(part, name, "not-a-photo.pdf");
        return content;
    }

    private static byte[] MinimalJpeg() =>
        [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    private static byte[] MinimalPng() =>
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

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
