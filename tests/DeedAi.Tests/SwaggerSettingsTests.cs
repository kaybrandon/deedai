using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Infrastructure.Data;

namespace DeedAi.Tests;

public sealed class SwaggerSettingsTests
{
    [Fact]
    public async Task Swagger_is_off_by_default_and_returns_404()
    {
        await using var factory = TestAppFactory.Create();
        var client = factory.CreateJsonClient();

        foreach (var path in new[] { "/swagger", "/swagger/", "/swagger/index.html", "/swagger/v1/swagger.json" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("swagger-ui", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<div id=\"root\">", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Admin_toggle_on_serves_swagger_off_is_404()
    {
        await using var factory = TestAppFactory.Create();
        var client = factory.CreateJsonClient();
        await Authed(factory, client, DatabaseSeeder.AdminEmail);

        var off = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":false}"""));
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);
        Assert.False(ReadEnabled(await off.Content.ReadAsStringAsync()));

        client.DefaultRequestHeaders.Authorization = null;
        foreach (var path in new[] { "/swagger", "/swagger/", "/swagger/index.html", "/swagger/v1/swagger.json" })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("swagger-ui", body, StringComparison.OrdinalIgnoreCase);
        }

        await Authed(factory, client, DatabaseSeeder.AdminEmail);
        var on = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);
        Assert.True(ReadEnabled(await on.Content.ReadAsStringAsync()));

        var stored = await client.GetAsync("/api/settings/swagger");
        Assert.Equal(HttpStatusCode.OK, stored.StatusCode);
        Assert.True(ReadEnabled(await stored.Content.ReadAsStringAsync()));

        client.DefaultRequestHeaders.Authorization = null;
        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        var html = await ui.Content.ReadAsStringAsync();
        Assert.Contains("swagger", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("deedai-swagger-authorize", html, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", html, StringComparison.Ordinal);

        var spec = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, spec.StatusCode);
        using var doc = JsonDocument.Parse(await spec.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Fact]
    public async Task Non_admin_cannot_read_or_toggle_swagger()
    {
        await using var factory = TestAppFactory.Create();
        var client = factory.CreateJsonClient();

        var anonGet = await client.GetAsync("/api/settings/swagger");
        Assert.Equal(HttpStatusCode.Unauthorized, anonGet.StatusCode);
        var anonPut = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, anonPut.StatusCode);

        foreach (var email in new[] { DatabaseSeeder.ViewerEmail, DatabaseSeeder.UploaderEmail, DatabaseSeeder.EditorEmail })
        {
            await Authed(factory, client, email);
            var forbiddenGet = await client.GetAsync("/api/settings/swagger");
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenGet.StatusCode);
            var forbiddenPut = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
            Assert.Equal(HttpStatusCode.Forbidden, forbiddenPut.StatusCode);
        }
    }

    [Fact]
    public async Task Enabling_swagger_does_not_open_anonymous_api()
    {
        await using var factory = TestAppFactory.Create();
        var client = factory.CreateJsonClient();
        await Authed(factory, client, DatabaseSeeder.AdminEmail);
        var enable = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var swagger = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);

        var unauth = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, unauth.StatusCode);

        var token = await factory.LoginAsync(client, DatabaseSeeder.ViewerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var list = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
    }

    [Fact]
    public async Task Swagger_setting_survives_host_restart()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-swagger-{Guid.NewGuid():N}.db");
        try
        {
            await using (var factory = TestAppFactory.Create(dbPath))
            {
                var client = factory.CreateJsonClient();
                await Authed(factory, client, DatabaseSeeder.AdminEmail);
                var put = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
                Assert.Equal(HttpStatusCode.OK, put.StatusCode);
            }

            await using (var restarted = TestAppFactory.Create(dbPath))
            {
                var client = restarted.CreateJsonClient();
                var ui = await client.GetAsync("/swagger/index.html");
                Assert.Equal(HttpStatusCode.OK, ui.StatusCode);

                await Authed(restarted, client, DatabaseSeeder.AdminEmail);
                var setting = await client.GetAsync("/api/settings/swagger");
                Assert.True(ReadEnabled(await setting.Content.ReadAsStringAsync()));

                var off = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":false}"""));
                Assert.Equal(HttpStatusCode.OK, off.StatusCode);
            }

            await using var third = TestAppFactory.Create(dbPath);
            var missing = await third.CreateJsonClient().GetAsync("/swagger");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        finally
        {
            try { File.Delete(dbPath); } catch (IOException) { }
        }
    }

    [Fact]
    public async Task App_setting_can_seed_swagger_on_in_non_prod_only()
    {
        await using var seeded = TestAppFactory.Create(null, new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        var onClient = seeded.CreateJsonClient();
        var seededUi = await onClient.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, seededUi.StatusCode);

        await using var prod = TestAppFactory.Create(null, new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true",
            ["ASPNETCORE_ENVIRONMENT"] = "Production"
        });
        var prodUi = await prod.CreateJsonClient().GetAsync("/swagger");
        Assert.Equal(HttpStatusCode.NotFound, prodUi.StatusCode);
    }

    private static async Task Authed(TestAppFactory factory, HttpClient client, string email)
    {
        var token = await factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static bool ReadEnabled(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("enabled").GetBoolean();
    }
}
