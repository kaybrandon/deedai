using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using DeedAi.Infrastructure.Software;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase523Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase523Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Migration_is_designer_first_after_null_defaults()
    {
        Assert.True(string.CompareOrdinal(Phase523SoftwareDepth.PriorMigrationId, Phase523SoftwareDepth.MigrationId) < 0);

        var type = typeof(Phase523SoftwareDepth);
        Assert.Equal(Phase523SoftwareDepth.MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(Phase523SoftwareDepth.MigrationId, discovered);

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260910020000_Phase523SoftwareDepth.Designer.cs");
        var snapshot = Read("src/DeedAi.Infrastructure/Data/Migrations/DeedAiDbContextModelSnapshot.cs");
        var up = Read("src/DeedAi.Infrastructure/Data/Migrations/20260910020000_Phase523SoftwareDepth.cs");
        foreach (var name in new[] { "GranteeCombiner", "CertifiedYear", "DefaultYear", "LookupImageCode", "PushImageCode", "SoftwareImageCode", "SalesRatioCode", "FinanceCode", "InstrumentCode", "DeletePolicySettings", "WhoCanDelete" })
        {
            Assert.Contains(name, designer, StringComparison.Ordinal);
            Assert.Contains(name, snapshot, StringComparison.Ordinal);
        }

        Assert.Contains("UPDATE SoftwareClientConfigs SET [GranteeCombiner] = 'first'", up, StringComparison.Ordinal);
        Assert.Contains("DF_SoftwareClientConfigs_{column}", up, StringComparison.Ordinal);
        Assert.Contains("DEFAULT {defaultSql} FOR [{column}]", up, StringComparison.Ordinal);
        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("camaSettings", up, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Cama", up, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_backfills_nulls_and_adds_sql_server_defaults()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Phase523SoftwareDepth).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new Phase523SoftwareDepth(), [builder]);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        foreach (var column in Phase523SoftwareDepth.StringColumns)
        {
            Assert.Contains($"UPDATE SoftwareClientConfigs SET [{column}]", sql, StringComparison.Ordinal);
            Assert.Contains($"DF_SoftwareClientConfigs_{column}", sql, StringComparison.Ordinal);
        }

        Assert.Contains(
            builder.Operations.OfType<CreateTableOperation>(),
            x => x.Name == "SoftwareImageCodes");
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("cama", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Grantee_combiner_and_years_validate()
    {
        Assert.Equal("Pat Example and Acme Holdings LLC", GranteeCombiners.Combine(["Pat Example", "Acme Holdings LLC"], null, GranteeCombiners.And));
        Assert.Equal("Acme Holdings LLC", GranteeCombiners.Combine(["Pat Example", "Acme Holdings LLC"], null, GranteeCombiners.Last));
        Assert.Equal("Pat Example", GranteeCombiners.Combine(["Pat Example", "Acme Holdings LLC"], null, GranteeCombiners.First));
        Assert.Equal("first", GranteeCombiners.Normalize(null));
        Assert.True(SoftwareYears.IsValid(null, out _));
        Assert.True(SoftwareYears.IsValid(DateTime.UtcNow.Year, out _));
        Assert.False(SoftwareYears.IsValid(1800, out var low));
        Assert.Contains("1900", low);
        Assert.False(SoftwareYears.IsValid(DateTime.UtcNow.Year + 10, out var high));
        Assert.Contains("Year must be between", high);
        Assert.Equal(DateTime.UtcNow.Year, SoftwareYears.Prefer(DateTime.UtcNow.Year, DateTime.UtcNow.Year - 1));
    }

    [Fact]
    public void Software_ui_uses_mask_f_and_software_labels()
    {
        var page = Read("spa/src/pages/SoftwarePage.tsx");
        var api = Read("spa/src/api.ts");
        var css = Read("spa/src/styles.css");
        Assert.Contains("software.imageCodes", page, StringComparison.Ordinal);
        Assert.Contains("software.grantee", page, StringComparison.Ordinal);
        Assert.Contains("Certified Year", page, StringComparison.Ordinal);
        Assert.Contains("Default Year", page, StringComparison.Ordinal);
        Assert.Contains("Image Codes", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Grantee\"", page, StringComparison.Ordinal);
        Assert.Contains("documentNumber", api, StringComparison.Ordinal);
        Assert.Contains("mailingStreet", api, StringComparison.Ordinal);
        Assert.Contains("imageCode", api, StringComparison.Ordinal);
        Assert.Contains("className=\"software-form\"", page, StringComparison.Ordinal);
        Assert.Contains(".software-form { max-width: var(--detail-w); }", css, StringComparison.Ordinal);
        Assert.Contains("Software Instances", page, StringComparison.Ordinal);
        Assert.DoesNotContain("County", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", page, StringComparison.Ordinal);
        Assert.DoesNotContain("cama", page, StringComparison.Ordinal);
        Assert.DoesNotContain("field-help-btn", page, StringComparison.Ordinal);
        Assert.DoesNotContain(">?</button>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Property defaults", page, StringComparison.OrdinalIgnoreCase);
        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.Contains("data-nav=\"system-mid\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/software\"", shell, StringComparison.Ordinal);
        Assert.Contains("Settings → System → Software", Read("docs/PHASE-5.2.3-SOFTWARE-DEPTH-AC.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void Fuller_field_map_keys_cover_locked_document_fields()
    {
        foreach (var field in new[]
        {
            DeedFields.DocumentNumber, DeedFields.Volume, DeedFields.Page, DeedFields.DeedType, DeedFields.Pid,
            DeedFields.MailingStreet, DeedFields.MailingCity, DeedFields.MailingState, DeedFields.MailingZip,
            DeedFields.LegalDescription, DeedFields.ImageCode, DeedFields.CertifiedYear, DeedFields.DefaultYear
        })
        {
            Assert.True(DeedFields.IsKnown(field), field);
        }

        Assert.DoesNotContain(DeedFields.All, x => x.Contains("cama", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Admin_manages_client_scoped_image_codes_and_non_admin_cannot()
    {
        var viewer = await Authed(DatabaseSeeder.ViewerEmail);
        var denied = await viewer.PostAsync("/api/software/image-codes", TestAppFactory.Json(
            "{\"clientId\":\"" + DatabaseSeeder.AcmeId + "\",\"code\":\"DT\",\"label\":\"Deed of Trust\",\"useOnLookup\":true,\"useOnPush\":true,\"isActive\":true,\"sortOrder\":3}"));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var created = await admin.PostAsync("/api/software/image-codes", TestAppFactory.Json(
            "{\"clientId\":\"" + DatabaseSeeder.AcmeId + "\",\"code\":\"DT\",\"label\":\"Deed of Trust\",\"deedType\":\"Deed of Trust\",\"useOnLookup\":true,\"useOnPush\":true,\"isActive\":true,\"sortOrder\":3}"));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("DT", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(DatabaseSeeder.AcmeId, json.RootElement.GetProperty("clientId").GetGuid());
        Assert.False(json.RootElement.TryGetProperty("camaCode", out _));
        var id = json.RootElement.GetProperty("id").GetGuid();

        var listed = await admin.GetAsync("/api/software/image-codes");
        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Contains("\"code\":\"DT\"", await listed.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/software/image-codes/{id}")).StatusCode);
    }

    [Fact]
    public async Task Client_software_depth_persists_and_rejects_bad_years()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var bad = await admin.PutAsync(
            $"/api/software/client-config/{DatabaseSeeder.AcmeId}",
            TestAppFactory.Json(DepthJson(certifiedYear: 1800)));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Year must be between", await bad.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var combiner = await admin.PutAsync(
            $"/api/software/client-config/{DatabaseSeeder.AcmeId}",
            TestAppFactory.Json(DepthJson(combiner: "not-a-combiner")));
        Assert.Equal(HttpStatusCode.BadRequest, combiner.StatusCode);
        Assert.Contains("Grantee", await combiner.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var year = DateTime.UtcNow.Year;
        var saved = await admin.PutAsync(
            $"/api/software/client-config/{DatabaseSeeder.AcmeId}",
            TestAppFactory.Json(DepthJson(combiner: "and", certifiedYear: year - 1, defaultYear: year)));
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        var body = await saved.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Assert.Equal("and", json.RootElement.GetProperty("granteeCombiner").GetString());
        Assert.Equal(year - 1, json.RootElement.GetProperty("certifiedYear").GetInt32());
        Assert.Equal(year, json.RootElement.GetProperty("defaultYear").GetInt32());
        Assert.Equal("WD", json.RootElement.GetProperty("lookupImageCode").GetString());
        Assert.Equal("SR", json.RootElement.GetProperty("salesRatioCode").GetString());
        Assert.Equal("CV", json.RootElement.GetProperty("financeCode").GetString());
        Assert.Equal("WD", json.RootElement.GetProperty("instrumentCode").GetString());
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
        Assert.DoesNotContain("cama", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ApiKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.False(json.RootElement.TryGetProperty("apiKey", out _));
    }

    [Fact]
    public async Task Push_and_lookup_use_combiner_years_image_code_and_fuller_maps()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var year = DateTime.UtcNow.Year;
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(
            $"/api/software/client-config/{DatabaseSeeder.AcmeId}",
            TestAppFactory.Json(DepthJson(combiner: "and", certifiedYear: year - 1, defaultYear: year)))).StatusCode);

        var editor = await Authed(DatabaseSeeder.EditorEmail);
        var id = await FirstReadyId(editor);
        var fields = await editor.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Jane Example","grantee":"Acme Holdings LLC","grantors":["Jane Example"],"grantees":["Acme Holdings LLC","Pat Example"],"instrumentDate":"2024-08-12","consideration":"250000","parcelId":"00099","documentNumber":"2024-0812","volume":"12","page":"44","deedType":"Warranty Deed","pid":"00099","mailingStreet":"100 Main St","mailingCity":"Springfield","mailingState":"IL","mailingZip":"62701","client":"Acme","notes":"depth push","isDraft":false}"""));
        Assert.Equal(HttpStatusCode.OK, fields.StatusCode);

        var lookup = await editor.PostAsync($"/api/documents/{id}/software/lookup", null);
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        using var lookupJson = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        Assert.Equal("Acme Holdings LLC and Pat Example", lookupJson.RootElement.GetProperty("extra").GetProperty("grantee").GetString());
        Assert.Equal(year.ToString(), lookupJson.RootElement.GetProperty("extra").GetProperty("year").GetString());
        Assert.Equal("WD", lookupJson.RootElement.GetProperty("extra").GetProperty("imageCode").GetString());

        var push = await editor.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.OK, push.StatusCode);
        var mock = (MockSoftwareClient)_factory.Services.GetRequiredService<ISoftwareClient>();
        Assert.NotNull(mock.LastPush);
        Assert.Equal("Acme Holdings LLC and Pat Example", mock.LastPush.Grantee);
        Assert.Equal("WD", mock.LastPush.MappedFields["Image.Code"]);
        Assert.Equal((year - 1).ToString(), mock.LastPush.MappedFields["Year.Certified"]);
        Assert.Equal(year.ToString(), mock.LastPush.MappedFields["Year.Default"]);
        Assert.Equal("SR", mock.LastPush.MappedFields["SalesRatio.Code"]);
        Assert.Equal("CV", mock.LastPush.MappedFields["Finance.Code"]);
        Assert.Equal("WD", mock.LastPush.MappedFields["Instrument.Code"]);
        Assert.Contains(mock.LastPush.MappedFields.Keys, key =>
            key.Contains("DocumentNumber", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Volume", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(mock.LastPush.MappedFields.Keys, key => key.Contains("cama", StringComparison.OrdinalIgnoreCase));

        var viewer = await Authed(DatabaseSeeder.ViewerEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/documents/{id}/software/push", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsync($"/api/documents/{id}/software/retry", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.PostAsync($"/api/documents/{id}/software/lookup", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_map_fuller_deed_fields()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var created = await admin.PostAsync("/api/software/field-maps", TestAppFactory.Json(
            """{"deedField":"mailingStreet","softwareField":"SitusStreet","softwareGroup":"Mailing","isActive":true,"sortOrder":40}"""));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("mailingStreet", json.RootElement.GetProperty("deedField").GetString());

        var listed = await admin.GetAsync("/api/software/field-maps");
        var body = await listed.Content.ReadAsStringAsync();
        foreach (var field in new[] { "documentNumber", "volume", "page", "mailingStreet", "imageCode", "certifiedYear" })
        {
            Assert.Contains($"\"deedField\":\"{field}\"", body, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("cama", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Property_defaults_stay_gone()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var response = await admin.GetAsync("/api/settings/property-defaults");
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone);
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
        var response = await client.GetAsync("/api/documents?search=Deed_2024_0812");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0);
        return json.RootElement[0].GetProperty("id").GetGuid();
    }

    private static string DepthJson(string combiner = "and", int? certifiedYear = 2024, int? defaultYear = 2025) =>
        $$"""
        {
          "vendor":"LegacySoft",
          "apiUrl":"https://software.example.test/api",
          "groupCode":"ACME",
          "removeLeadingZeros":true,
          "dateLabelDepth":2,
          "displaySalesTab":true,
          "sendConsideration":true,
          "considerationThreshold":1,
          "resetExemptions":false,
          "resetSupplementYear":false,
          "resetSalesLetter":false,
          "resetSalesTab":false,
          "resetAgents":false,
          "resetMortgageCodes":false,
          "granteeCombiner":"{{combiner}}",
          "certifiedYear":{{(certifiedYear is null ? "null" : certifiedYear.ToString())}},
          "defaultYear":{{(defaultYear is null ? "null" : defaultYear.ToString())}},
          "lookupImageCode":"WD",
          "pushImageCode":"WD",
          "salesRatioCode":"SR",
          "financeCode":"CV",
          "instrumentCode":"WD"
        }
        """;

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
