using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;

namespace DeedAi.Tests;

public sealed class FieldHelpTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public FieldHelpTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Catalog_covers_every_must_field()
    {
        foreach (var key in FieldHelpCatalog.MustKeys)
        {
            Assert.True(FieldHelpCatalog.All.ContainsKey(key), $"Missing Help for {key}");
            Assert.False(string.IsNullOrWhiteSpace(FieldHelpCatalog.All[key]));
            Assert.InRange(FieldHelpCatalog.All[key].Length, 20, 320);
        }
    }

    [Fact]
    public void Catalog_uses_client_software_wording_and_has_no_secrets()
    {
        var joined = string.Join('\n', FieldHelpCatalog.All.Values);
        Assert.DoesNotContain("County", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CAMA", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JwtSigningKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AdminSeedPassword", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SendGridApiKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SoftwareApiKey", joined, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Client", joined, StringComparison.Ordinal);
        Assert.Contains("Software", joined, StringComparison.Ordinal);
    }

    [Fact]
    public void Spa_catalog_includes_every_must_key()
    {
        var spa = Path.Combine(RepoRoot(), "spa", "src", "helpCatalog.ts");
        Assert.True(File.Exists(spa), spa);
        var text = File.ReadAllText(spa);
        foreach (var key in FieldHelpCatalog.MustKeys)
        {
            Assert.Contains($"\"{key}\"", text);
        }

        foreach (var key in new[] { "users.role", "settings.idleTimeout", "settings.deletePolicy", "settings.ocrTrim", "settings.swagger", "settings.systemHealth", "settings.emailMode", "settings.emailTest", "settings.verifyRequired", "users.resendVerification" })
        {
            Assert.Contains($"\"{key}\"", text);
        }

        var fieldHelp = File.ReadAllText(Path.Combine(RepoRoot(), "spa", "src", "components", "FieldHelp.tsx"));
        Assert.Contains("data-help", fieldHelp);
        Assert.Contains("title={text}", fieldHelp);
        Assert.Contains("aria-describedby", fieldHelp);
        Assert.DoesNotContain("field-help-btn", fieldHelp);
        Assert.DoesNotContain("?", fieldHelp);
    }

    [Fact]
    public void Must_pages_mount_help_on_required_fields()
    {
        var root = Path.Combine(RepoRoot(), "spa", "src");
        AssertHelp(root, "pages/UploadPage.tsx", "upload.client");
        AssertHelp(root, "pages/SoftwarePage.tsx",
            "software.enablePush", "software.vendor", "software.groupCode",
            "software.removeLeadingZeros", "software.displaySalesTab",
            "software.sendConsideration", "software.dateLabelDepth",
            "software.imageCodes", "software.grantee",
            "software.certifiedYear", "software.defaultYear",
            "sales.codes", "sales.considerationThreshold");
        AssertHelp(root, "pages/SalesPage.tsx", "sales.codes", "sales.considerationThreshold");
        AssertHelp(root, "pages/ReportsPage.tsx",
            "reports.date", "reports.assignee", "reports.status", "reports.flag", "reports.client");
        AssertHelp(root, "pages/SettingsPage.tsx",
            "settings.flags", "settings.statuses", "settings.deedTypeMaps", "settings.deletePolicy");
        AssertHelp(root, "pages/RestorePage.tsx", "restore.confirmRestore", "restore.confirmHardDelete");
        AssertHelp(root, "components/SwaggerAdminPanel.tsx", "settings.swagger");
        AssertHelp(root, "components/SystemHealthPanel.tsx", "settings.systemHealth");
        AssertHelp(root, "components/AdminEmailPanel.tsx", "settings.emailMode", "settings.emailTest", "settings.verifyRequired");
        AssertHelp(root, "pages/UsersPage.tsx", "users.resendVerification");
        AssertHelp(root, "pages/ReviewPage.tsx", "review.grantors", "review.mailing", "review.softwareSearch");
    }

    [Fact]
    public async Task Help_api_returns_catalog_to_authenticated_users_without_secrets()
    {
        var client = _factory.CreateJsonClient();
        var anon = await client.GetAsync("/api/help");
        Assert.Equal(HttpStatusCode.Unauthorized, anon.StatusCode);

        var token = await _factory.LoginAsync(client, DatabaseSeeder.ViewerEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/help");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        foreach (var key in FieldHelpCatalog.MustKeys)
        {
            Assert.True(doc.RootElement.TryGetProperty(key, out var value), key);
            Assert.False(string.IsNullOrWhiteSpace(value.GetString()));
        }

        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("TEST_ONLY_JWT", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PLACEHOLDER", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertHelp(string root, string relative, params string[] keys)
    {
        var path = Path.Combine(root, relative);
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path);
        foreach (var key in keys)
        {
            Assert.Contains(key, text);
        }
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
