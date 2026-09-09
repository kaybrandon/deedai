using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Api.Swagger;
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
        AssertAuthorizeHitTargetRuntime(html);

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
    public void Authorize_hit_target_css_beats_swagger_inline_display()
    {
        AssertAuthorizeHitTargetCss(SwaggerExtensions.AuthorizeHitTargetCss);
    }

    [Fact]
    public void Authorize_hit_target_runtime_runs_after_swagger_paint()
    {
        AssertAuthorizeHitTargetRuntime(SwaggerExtensions.AuthorizeHitTargetHead);
        AssertAuthorizeHitTargetRuntime(SwaggerAuthorizeHitTarget.HeadContent);
        AssertAuthorizeHitTargetRuntime(SwaggerAuthorizeHitTarget.MeasureFixtureHtml());
        Assert.Contains("swagger-after-paint-win", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("id=\"top-authorize\"", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("id=\"modal-authorize\"", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("__deedAiMeasureAuthorize", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("County", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("JwtSigningKey", SwaggerAuthorizeHitTarget.HeadContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdminSeedPassword", SwaggerAuthorizeHitTarget.HeadContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Authorize_runtime_pins_inline_important_after_swagger_css_wins()
    {
        var repo = RepoRoot();
        var script = Path.Combine(repo, "tests", "DeedAi.Tests", "swagger-authorize-hit-runtime.mjs");
        Assert.True(File.Exists(script), script);
        var node = FindNode();
        Assert.False(string.IsNullOrEmpty(node), "node is required to measure the Authorize runtime pin");
        var start = new System.Diagnostics.ProcessStartInfo(node, script)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        using var process = System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("failed to start node");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        Assert.True(process.WaitForExit(15_000), "Authorize runtime measure timed out");
        Assert.True(process.ExitCode == 0, stdout + stderr);
        Assert.Contains("\"ok\": true", stdout, StringComparison.Ordinal);
        Assert.Contains("\"height\": 44", stdout, StringComparison.Ordinal);
        Assert.Contains("\"marker\": \"pass\"", stdout, StringComparison.Ordinal);
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

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "DeedAi.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not locate DeedAi.slnx from the test output.");
    }

    private static string? FindNode()
    {
        foreach (var name in new[] { "node", "nodejs" })
        {
            var found = FindOnPath(name);
            if (found is not null) return found;
        }

        return File.Exists("/exec-daemon/node") ? "/exec-daemon/node" : null;
    }

    private static string? FindOnPath(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, name);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static bool ReadEnabled(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("enabled").GetBoolean();
    }

    /// <summary>
    /// Stylesheet contract (still shipped). #17 proved this can be present
    /// on the live page and still lose after Swagger paints.
    /// </summary>
    private static void AssertAuthorizeHitTargetCss(string css)
    {
        Assert.Contains("deedai-swagger-authorize", css, StringComparison.Ordinal);
        Assert.Contains(".swagger-ui .btn.authorize", css, StringComparison.Ordinal);
        Assert.Contains(".swagger-ui .auth-wrapper .authorize", css, StringComparison.Ordinal);
        Assert.Contains(".swagger-ui .auth-btn-wrapper .btn", css, StringComparison.Ordinal);
        Assert.Contains(".swagger-ui .btn.modal-btn.authorize", css, StringComparison.Ordinal);
        Assert.Contains("display: inline-flex !important", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px !important", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px !important", css, StringComparison.Ordinal);
        Assert.Contains("height: 44px !important", css, StringComparison.Ordinal);
        Assert.Contains("padding: 10px 16px !important", css, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box !important", css, StringComparison.Ordinal);
        Assert.Contains("float: none !important", css, StringComparison.Ordinal);
    }

    /// <summary>
    /// Runtime contract: after Swagger paints, JS must pin 44×44 via inline
    /// <c>!important</c>, MutationObserver, and SwaggerUIBundle onComplete.
    /// QA2: <c>window.__deedAiMeasureAuthorize()</c> on /swagger — every
    /// item width/height ≥ 44. <c>html[data-deedai-authorize-hit=pass]</c>.
    /// </summary>
    private static void AssertAuthorizeHitTargetRuntime(string html)
    {
        AssertAuthorizeHitTargetCss(html);
        Assert.Contains("deedai-swagger-authorize-runtime", html, StringComparison.Ordinal);
        Assert.Contains("MutationObserver", html, StringComparison.Ordinal);
        Assert.Contains("SwaggerUIBundle", html, StringComparison.Ordinal);
        Assert.Contains("onComplete", html, StringComparison.Ordinal);
        Assert.Contains("setProperty", html, StringComparison.Ordinal);
        Assert.Contains("important", html, StringComparison.Ordinal);
        Assert.Contains("__deedAiMeasureAuthorize", html, StringComparison.Ordinal);
        Assert.Contains("__deedAiApplyAuthorizeHit", html, StringComparison.Ordinal);
        Assert.Contains("getBoundingClientRect", html, StringComparison.Ordinal);
        Assert.Contains("data-deedai-hit", html, StringComparison.Ordinal);
        Assert.Contains("data-deedai-authorize-hit", html, StringComparison.Ordinal);
        Assert.Contains("auth-btn-wrapper", html, StringComparison.Ordinal);
        Assert.Contains(".btn.authorize", html, StringComparison.Ordinal);
    }
}
