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

        foreach (var path in new[]
                 {
                     "/swagger", "/swagger/", "/swagger/index.html", "/swagger/v1/swagger.json",
                     "/swagger/deedai-swagger-authorize.js", "/swagger/deedai-swagger-authorize.css"
                 })
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
        AssertAuthorizeHitTargetIndex(html);

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
        AssertAuthorizeHitTargetHeadIsCssOnly(SwaggerExtensions.AuthorizeHitTargetHead);
        AssertAuthorizeHitTargetHeadIsCssOnly(SwaggerAuthorizeHitTarget.HeadContent);
        AssertAuthorizeHitTargetRuntime(SwaggerAuthorizeHitTarget.JavaScript);
        AssertAuthorizeHitTargetRuntime(SwaggerAuthorizeHitTarget.MeasureFixtureHtml());
        Assert.Contains("swagger-after-paint-win", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("swagger-react-reset", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("id=\"top-authorize\"", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("id=\"modal-authorize\"", SwaggerAuthorizeHitTarget.MeasureFixtureHtml(), StringComparison.Ordinal);
        Assert.Contains("__deedAiMeasureAuthorize", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.Contains("POLL_MS = 250", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.Contains("max-height", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.Contains("__deedAiAuthorizeRuntimeVersion = \"4.2.3\"", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.Contains("deedaiAuthorizeError", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("County", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", SwaggerAuthorizeHitTarget.JavaScript, StringComparison.Ordinal);
        Assert.DoesNotContain("JwtSigningKey", SwaggerAuthorizeHitTarget.HeadContent, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdminSeedPassword", SwaggerAuthorizeHitTarget.HeadContent, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("4.2.3", SwaggerAuthorizeHitTarget.RuntimeVersion);
        AssertCustomIndexLoadsScriptLast(SwaggerAuthorizeHitTarget.IndexHtml);
    }

    [Fact]
    public async Task Authorize_assets_are_served_before_swashbuckle_when_swagger_on()
    {
        await using var factory = TestAppFactory.Create();
        var client = factory.CreateJsonClient();
        await Authed(factory, client, DatabaseSeeder.AdminEmail);
        var on = await client.PutAsync("/api/settings/swagger", TestAppFactory.Json("""{"enabled":true}"""));
        Assert.Equal(HttpStatusCode.OK, on.StatusCode);

        client.DefaultRequestHeaders.Authorization = null;
        var js = await client.GetAsync("/swagger/deedai-swagger-authorize.js");
        Assert.Equal(HttpStatusCode.OK, js.StatusCode);
        var jsBody = await js.Content.ReadAsStringAsync();
        Assert.Contains("__deedAiMeasureAuthorize", jsBody, StringComparison.Ordinal);
        Assert.Contains("POLL_MS = 250", jsBody, StringComparison.Ordinal);
        Assert.True(
            js.Headers.TryGetValues("Cache-Control", out var cache)
            && cache.Any(v => v.Contains("no-store", StringComparison.OrdinalIgnoreCase)),
            "Authorize JS must be Cache-Control: no-store so Azure/browser cannot keep a #18 pin");

        var css = await client.GetAsync("/swagger/deedai-swagger-authorize.css");
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        var cssBody = await css.Content.ReadAsStringAsync();
        Assert.Contains("max-height: none !important", cssBody, StringComparison.Ordinal);
        Assert.Contains("display: inline-flex !important", cssBody, StringComparison.Ordinal);

        var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        var html = await ui.Content.ReadAsStringAsync();
        AssertCustomIndexLoadsScriptLast(html);
        AssertAuthorizeHitTargetIndex(html);
        AssertAuthorizeHitTargetRuntime(jsBody);
        AssertNoStore(ui, "/swagger/index.html");
    }

    [Fact]
    public async Task Swagger_index_html_and_index_js_are_no_store_when_enabled()
    {
        await using var factory = TestAppFactory.Create(null, new Dictionary<string, string?>
        {
            ["Swagger:Enabled"] = "true"
        });
        var client = factory.CreateJsonClient();

        Assert.True(SwaggerExtensions.IsSwaggerShellAsset("/swagger/index.html"));
        Assert.True(SwaggerExtensions.IsSwaggerShellAsset("/swagger/index.js"));
        Assert.False(SwaggerExtensions.IsSwaggerShellAsset("/swagger/v1/swagger.json"));
        Assert.False(SwaggerExtensions.IsSwaggerShellAsset("/swagger/deedai-swagger-authorize.js"));

        var html = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, html.StatusCode);
        AssertNoStore(html, "/swagger/index.html");
        var body = await html.Content.ReadAsStringAsync();
        AssertCustomIndexLoadsScriptLast(body);
        AssertAuthorizeHitTargetIndex(body);
        Assert.DoesNotContain("InjectJavascript", SwaggerExtensions.AuthorizeHitTargetHead, StringComparison.Ordinal);

        var indexJs = await client.GetAsync("/swagger/index.js");
        Assert.Equal(HttpStatusCode.OK, indexJs.StatusCode);
        AssertNoStore(indexJs, "/swagger/index.js");
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
        Assert.Contains("\"recoveredFromSwaggerInlineReset\": true", stdout, StringComparison.Ordinal);
        Assert.Contains("\"version\": \"4.2.3\"", stdout, StringComparison.Ordinal);
        Assert.Contains("\"helperDefinedAfterEval\": true", stdout, StringComparison.Ordinal);
        Assert.Contains("\"helperDefinedAfterReentry\": true", stdout, StringComparison.Ordinal);
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
        AssertNoStore(seededUi, "/swagger/index.html");

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

    private static void AssertNoStore(HttpResponseMessage response, string path)
    {
        var values = new List<string>();
        if (response.Headers.TryGetValues("Cache-Control", out var header))
        {
            values.AddRange(header);
        }

        if (response.Content.Headers.TryGetValues("Cache-Control", out var content))
        {
            values.AddRange(content);
        }

        var joined = string.Join("; ", values);
        Assert.True(
            values.Any(v =>
                v.Contains("no-store", StringComparison.OrdinalIgnoreCase)
                || (v.Contains("max-age=0", StringComparison.OrdinalIgnoreCase)
                    && v.Contains("must-revalidate", StringComparison.OrdinalIgnoreCase))),
            $"{path} must be Cache-Control: no-store (or max-age=0, must-revalidate); got '{joined}'");
        Assert.DoesNotContain("604800", joined, StringComparison.Ordinal);
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
        Assert.Contains("max-height: none !important", css, StringComparison.Ordinal);
        Assert.Contains("padding: 10px 16px !important", css, StringComparison.Ordinal);
        Assert.Contains("box-sizing: border-box !important", css, StringComparison.Ordinal);
        Assert.Contains("float: none !important", css, StringComparison.Ordinal);
    }

    /// <summary>
    /// Custom index must load our file once, after Swashbuckle <c>index.js</c>.
    /// No inline full-runtime dump and no early src before the bundles.
    /// </summary>
    private static void AssertCustomIndexLoadsScriptLast(string html)
    {
        Assert.Contains("deedai authorize hit runtime v4.2.3", html, StringComparison.Ordinal);
        var firstOurs = html.IndexOf("deedai-swagger-authorize.js", StringComparison.Ordinal);
        var lastOurs = html.LastIndexOf("deedai-swagger-authorize.js", StringComparison.Ordinal);
        Assert.True(firstOurs >= 0, "custom index missing authorize.js");
        Assert.Equal(firstOurs, lastOurs);
        var indexJs = html.LastIndexOf("index.js", StringComparison.Ordinal);
        Assert.True(indexJs >= 0, "custom index missing Swashbuckle index.js");
        Assert.True(lastOurs > indexJs, "Authorize runtime must load after index.js, not in head");
        Assert.Contains("id=\"deedai-swagger-authorize-src-last\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("id=\"deedai-swagger-authorize-runtime\"", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Live index: CSS link in head, JS once after bundles. HeadContent must
    /// not dump the 12k runtime before <c>swagger-ui-bundle.js</c>.
    /// </summary>
    private static void AssertAuthorizeHitTargetIndex(string html)
    {
        AssertAuthorizeHitTargetHeadIsCssOnly(ExtractHead(html));
        AssertCustomIndexLoadsScriptLast(html);
        var bundle = html.IndexOf("swagger-ui-bundle", StringComparison.OrdinalIgnoreCase);
        var ours = html.IndexOf("deedai-swagger-authorize.js", StringComparison.Ordinal);
        if (bundle >= 0)
        {
            Assert.True(ours > bundle, "authorize.js must not load before swagger-ui-bundle");
        }
    }

    private static void AssertAuthorizeHitTargetHeadIsCssOnly(string head)
    {
        Assert.Contains("deedai-swagger-authorize.css", head, StringComparison.Ordinal);
        Assert.Contains("deedai-swagger-authorize-href", head, StringComparison.Ordinal);
        Assert.DoesNotContain("deedai-swagger-authorize.js", head, StringComparison.Ordinal);
        Assert.DoesNotContain("__deedAiMeasureAuthorize", head, StringComparison.Ordinal);
        Assert.DoesNotContain("MutationObserver", head, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", head, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractHead(string html)
    {
        var start = html.IndexOf("<head", StringComparison.OrdinalIgnoreCase);
        var end = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < 0 || end <= start)
        {
            return html;
        }

        return html[start..end];
    }

    /// <summary>
    /// Runtime contract: after Swagger paints (and after React resets
    /// <c>display:inline</c>), JS must pin 44×44 via inline <c>!important</c>,
    /// 250ms poll, MutationObserver, and SwaggerUIBundle onComplete.
    /// After script eval: <c>typeof window.__deedAiMeasureAuthorize === "function"</c>.
    /// <c>html[data-deedai-authorize-hit=pass]</c>.
    /// </summary>
    private static void AssertAuthorizeHitTargetRuntime(string source)
    {
        if (source.Contains("<style", StringComparison.Ordinal)
            || source.Contains("deedai-swagger-authorize {", StringComparison.Ordinal)
            || source.Contains(".swagger-ui .btn.authorize", StringComparison.Ordinal))
        {
            AssertAuthorizeHitTargetCss(source);
        }

        Assert.Contains("MutationObserver", source, StringComparison.Ordinal);
        Assert.Contains("SwaggerUIBundle", source, StringComparison.Ordinal);
        Assert.Contains("onComplete", source, StringComparison.Ordinal);
        Assert.Contains("setProperty", source, StringComparison.Ordinal);
        Assert.Contains("important", source, StringComparison.Ordinal);
        Assert.Contains("__deedAiMeasureAuthorize", source, StringComparison.Ordinal);
        Assert.Contains("__deedAiApplyAuthorizeHit", source, StringComparison.Ordinal);
        Assert.Contains("getBoundingClientRect", source, StringComparison.Ordinal);
        Assert.Contains("data-deedai-hit", source, StringComparison.Ordinal);
        Assert.Contains("data-deedai-authorize-hit", source, StringComparison.Ordinal);
        Assert.Contains("auth-btn-wrapper", source, StringComparison.Ordinal);
        Assert.Contains(".btn.authorize", source, StringComparison.Ordinal);
        Assert.Contains("250", source, StringComparison.Ordinal);
        Assert.Contains("max-height", source, StringComparison.Ordinal);
        Assert.Contains("__deedAiAuthorizeRuntimeVersion", source, StringComparison.Ordinal);
        Assert.Contains("4.2.3", source, StringComparison.Ordinal);
    }
}
