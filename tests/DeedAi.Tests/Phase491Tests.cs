using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase491Tests : IClassFixture<TestAppFactory>
{
    public const string MigrationId = "20260909190000_Phase491RemovePropertyDefaults";

    private readonly TestAppFactory _factory;

    public Phase491Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Phase491_migration_is_designer_first_after_phase45()
    {
        Assert.True(string.CompareOrdinal("20260909160000_Phase45UsersIdentity", MigrationId) < 0);
        Assert.True(string.CompareOrdinal("20260909180000_Phase48AdminEmail", MigrationId) < 0);

        var type = typeof(Phase491RemovePropertyDefaults);
        Assert.Equal(MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var assembly = db.GetService<IMigrationsAssembly>();
        Assert.Contains(MigrationId, assembly.Migrations.Keys);
        Assert.Equal(typeof(Phase491RemovePropertyDefaults), assembly.Migrations[MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260909190000_Phase491RemovePropertyDefaults.Designer.cs");
        Assert.Contains("[Migration(\"20260909190000_Phase491RemovePropertyDefaults\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        Assert.Contains("DeedAi.Domain.Entities.EmailSettings", designer, StringComparison.Ordinal);
        Assert.Contains("EmailVerified", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("DeedAi.Domain.Entities.PropertyDefault", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("ToTable(\"PropertyDefaults\"", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase491_up_drops_property_defaults_table()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        var up = typeof(Phase491RemovePropertyDefaults).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new Phase491RemovePropertyDefaults(), [builder]);
        Assert.Contains(builder.Operations.OfType<DropTableOperation>(), x => x.Name == "PropertyDefaults");
    }

    [Fact]
    public void Current_model_and_seed_do_not_keep_property_defaults()
    {
        Assert.False(File.Exists(Path.Combine(RepoRoot(), "src", "DeedAi.Domain", "Entities", "PropertyDefault.cs")));

        var snapshot = Read("src/DeedAi.Infrastructure/Data/Migrations/DeedAiDbContextModelSnapshot.cs");
        Assert.Contains("DeedAi.Domain.Entities.EmailSettings", snapshot, StringComparison.Ordinal);
        Assert.Contains("DeedAi.Domain.Entities.EmailVerificationToken", snapshot, StringComparison.Ordinal);
        Assert.Contains("EmailVerified", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("DeedAi.Domain.Entities.PropertyDefault", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("ToTable(\"PropertyDefaults\"", snapshot, StringComparison.Ordinal);

        var context = Read("src/DeedAi.Infrastructure/Data/DeedAiDbContext.cs");
        Assert.DoesNotContain("PropertyDefault", context, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyDefaults", context, StringComparison.Ordinal);

        var seeder = Read("src/DeedAi.Infrastructure/Data/DatabaseSeeder.cs");
        Assert.DoesNotContain("PropertyDefault", seeder, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyDefaults", seeder, StringComparison.Ordinal);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.ClrType.Name == "PropertyDefault");
        Assert.DoesNotContain(db.Model.GetEntityTypes(), x => x.GetTableName() == "PropertyDefaults");
    }

    [Fact]
    public async Task Property_defaults_api_returns_404_or_410()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var editor = await Authed(DatabaseSeeder.EditorEmail);
        var viewer = await Authed(DatabaseSeeder.ViewerEmail);
        foreach (var client in new[] { admin, editor, viewer })
        {
            AssertGone(await client.GetAsync("/api/settings/property-defaults"));
            AssertGone(await client.PostAsync("/api/settings/property-defaults", TestAppFactory.Json(
                "{\"scope\":\"Client\",\"clientId\":\"" + DatabaseSeeder.AcmeId + "\",\"fieldKey\":\"client\",\"defaultValue\":\"Acme\"}")));
            AssertGone(await client.PutAsync($"/api/settings/property-defaults/{Guid.NewGuid()}", TestAppFactory.Json(
                "{\"scope\":\"Client\",\"clientId\":\"" + DatabaseSeeder.AcmeId + "\",\"fieldKey\":\"client\",\"defaultValue\":\"Acme\"}")));
            AssertGone(await client.DeleteAsync($"/api/settings/property-defaults/{Guid.NewGuid()}"));
            AssertGone(await client.PostAsync("/api/settings/property-defaults/reset", TestAppFactory.Json(
                "{\"scope\":\"Client\",\"clientId\":\"" + DatabaseSeeder.AcmeId + "\"}")));
        }
    }

    [Fact]
    public async Task Software_field_maps_and_six_push_resets_remain()
    {
        var admin = await Authed(DatabaseSeeder.AdminEmail);
        var maps = await admin.GetAsync("/api/software/field-maps");
        Assert.Equal(HttpStatusCode.OK, maps.StatusCode);
        var mapsBody = await maps.Content.ReadAsStringAsync();
        Assert.Contains("softwareField", mapsBody, StringComparison.OrdinalIgnoreCase);

        var settings = await admin.GetAsync("/api/software/settings");
        Assert.Equal(HttpStatusCode.OK, settings.StatusCode);
        var body = await settings.Content.ReadAsStringAsync();
        Assert.Contains("resetExemptions", body, StringComparison.Ordinal);
        Assert.Contains("resetSupplementYear", body, StringComparison.Ordinal);
        Assert.Contains("resetSalesLetter", body, StringComparison.Ordinal);
        Assert.Contains("resetSalesTab", body, StringComparison.Ordinal);
        Assert.Contains("resetAgents", body, StringComparison.Ordinal);
        Assert.Contains("resetMortgageCodes", body, StringComparison.Ordinal);
        Assert.DoesNotContain("property-defaults", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PropertyDefault", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_spa_and_docs_stop_referring_to_live_property_defaults()
    {
        var settings = Read("spa/src/pages/SettingsPage.tsx");
        Assert.DoesNotContain("Property defaults", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("propertyDefaults", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("createPropertyDefault", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("resetPropertyDefaults", settings, StringComparison.Ordinal);
        Assert.Contains("Software Defaults", settings, StringComparison.Ordinal);
        Assert.Contains("OCR Trim / Discard", settings, StringComparison.Ordinal);
        Assert.Contains("Deed-Type Maps", settings, StringComparison.Ordinal);

        var api = Read("spa/src/api.ts");
        Assert.DoesNotContain("property-defaults", api, StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyDefaultItem", api, StringComparison.Ordinal);
        Assert.Contains("softwareFieldMaps", api, StringComparison.Ordinal);

        var app = Read("spa/src/App.tsx");
        Assert.DoesNotContain("property-defaults", app, StringComparison.Ordinal);
        Assert.DoesNotContain("Property defaults", app, StringComparison.Ordinal);

        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.DoesNotContain("Property defaults", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("property-defaults", shell, StringComparison.Ordinal);

        var changelog = Read("docs/CHANGELOG.md");
        Assert.Contains("Phase 4.9.1", changelog, StringComparison.Ordinal);
        Assert.Contains("remove Property defaults", changelog, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Property defaults as a live", changelog, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertGone(HttpResponseMessage response) =>
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone,
            $"Expected 404/410 for Property defaults, got {(int)response.StatusCode}.");

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
