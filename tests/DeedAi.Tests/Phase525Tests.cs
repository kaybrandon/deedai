using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase525Tests
{
    private const string MigrationId = Phase525StatusesCatalog.MigrationId;
    private const string PriorMigrationId = Phase525StatusesCatalog.PriorMigrationId;

    [Fact]
    public void Must_eight_labels_and_pipeline_map()
    {
        Assert.Equal(
            new[] { "Complete", "In Queue", "Needs Work", "New", "Not Needed", "Pending", "Research", "Upload Error" },
            StatusCatalog.Must.Select(x => x.DisplayName).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        Assert.Equal(DocumentStatuses.Queued, StatusCatalog.Find("In Queue")!.MapsTo);
        Assert.Equal(DocumentStatuses.Processing, StatusCatalog.Find("Pending")!.MapsTo);
        Assert.Equal(DocumentStatuses.Ready, StatusCatalog.Find("Complete")!.MapsTo);
        Assert.Equal(DocumentStatuses.Failed, StatusCatalog.Find("Upload Error")!.MapsTo);
        Assert.Equal(ReviewWorkflow.NeedsReview, StatusCatalog.Find("Needs Work")!.MapsTo);
        Assert.Equal(StatusCatalog.Research, StatusCatalog.Find("Research")!.MapsTo);
        Assert.Equal(StatusCatalog.New, StatusCatalog.Find("New")!.MapsTo);
        Assert.Equal(StatusCatalog.NotNeeded, StatusCatalog.Find("Not Needed")!.MapsTo);

        Assert.True(ReviewWorkflow.IsNeedsReview(StatusCatalog.NeedsWork));
        Assert.True(ReviewWorkflow.IsApproved(StatusCatalog.Complete));
        Assert.Equal(ReviewWorkflow.NeedsReview, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, StatusCatalog.NeedsWork, false));
        Assert.Equal(ReviewWorkflow.Approved, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, StatusCatalog.Complete, false));
        Assert.Equal(StatusCatalog.Research, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, StatusCatalog.Research, false));
        Assert.Equal(DocumentStatuses.Ready, ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, null, false));
        Assert.Equal(DocumentStatuses.Failed, ReviewWorkflow.DisplayStatus(DocumentStatuses.Failed, null, false));
        Assert.Equal("Needs Work", StatusCatalog.ToTitleCase("needs work"));
        Assert.DoesNotContain(StatusCatalog.MustLabels, x => x.Contains("CAMA", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(StatusCatalog.MustLabels, x => x.Contains("County", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Settings_documents_review_ui_uses_catalog_without_breaking_ribbon()
    {
        var settings = Read("spa/src/pages/SettingsPage.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var review = Read("spa/src/pages/ReviewPage.tsx");
        var catalog = Read("spa/src/statusCatalog.ts");
        var chip = Read("spa/src/components/StatusChip.tsx");
        var ribbon = Read("spa/src/components/OcrRibbon.tsx");
        var api = Read("spa/src/api.ts");
        var css = Read("spa/src/styles.css");

        Assert.Contains("statuses-catalog", settings, StringComparison.Ordinal);
        Assert.Contains("status-disable", settings, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", settings, StringComparison.Ordinal);
        Assert.Contains("setCatalogStatus", documents, StringComparison.Ordinal);
        Assert.Contains("setCatalogStatus", review, StringComparison.Ordinal);
        Assert.Contains("filterStatuses", documents, StringComparison.Ordinal);
        Assert.Contains("catalogLabel", documents, StringComparison.Ordinal);
        Assert.Contains("catalogLabel", review, StringComparison.Ordinal);
        Assert.Contains("/api/documents/${id}/catalog-status", api, StringComparison.Ordinal);
        foreach (var label in StatusCatalog.MustLabels)
        {
            Assert.Contains(label, catalog, StringComparison.Ordinal);
        }

        Assert.Contains("Queued", ribbon, StringComparison.Ordinal);
        Assert.Contains("Processing", ribbon, StringComparison.Ordinal);
        Assert.Contains("Ready", ribbon, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", css, StringComparison.Ordinal);
        Assert.Contains(".statuses-catalog .status-row", css, StringComparison.Ordinal);
        Assert.Contains("Needs Work", chip, StringComparison.Ordinal);

        Assert.Contains("Phase 5.2.5", Read("docs/PHASE-5.2.5-STATUSES-CATALOG-AC.md"), StringComparison.Ordinal);
        foreach (var file in new[] { settings, documents, review, catalog, api })
        {
            Assert.DoesNotContain("Super Admin", file, StringComparison.Ordinal);
            Assert.DoesNotContain("CAMA", file, StringComparison.Ordinal);
            Assert.DoesNotContain("County", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Migration_is_designer_first_after_software_depth()
    {
        Assert.True(string.CompareOrdinal(PriorMigrationId, MigrationId) < 0);

        var type = typeof(Phase525StatusesCatalog);
        Assert.Equal(MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(MigrationId, discovered);
        Assert.Equal(type, db.GetService<IMigrationsAssembly>().Migrations[MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260910030000_Phase525StatusesCatalog.Designer.cs");
        var snapshot = Read("src/DeedAi.Infrastructure/Data/Migrations/DeedAiDbContextModelSnapshot.cs");
        Assert.Contains($"[Migration(\"{MigrationId}\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        foreach (var name in new[] { "MapsTo", "Kind", "IsSeed" })
        {
            Assert.Contains($"\"{name}\"", designer, StringComparison.Ordinal);
            Assert.Contains($"\"{name}\"", snapshot, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Super Admin", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_backfills_nulls_adds_defaults_and_seeds_must_eight()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeUp(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        foreach (var column in Phase525StatusesCatalog.StringColumns)
        {
            Assert.Contains($"UPDATE StatusDefinitions SET [{column}]", sql, StringComparison.Ordinal);
            Assert.Contains($"DF_StatusDefinitions_{column}", sql, StringComparison.Ordinal);
            Assert.Contains($"DEFAULT N'' FOR [{column}]", sql, StringComparison.Ordinal);
        }

        Assert.Contains("DF_StatusDefinitions_IsSeed", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT 0 FOR [IsSeed]", sql, StringComparison.Ordinal);
        foreach (var label in StatusCatalog.MustLabels)
        {
            Assert.Contains(label, sql, StringComparison.Ordinal);
        }

        Assert.Contains(builder.Operations.OfType<AddColumnOperation>(), x => x.Name == "MapsTo" && x.IsNullable);
        Assert.Contains(builder.Operations.OfType<AddColumnOperation>(), x => x.Name == "Kind" && x.IsNullable);
        Assert.Contains(builder.Operations.OfType<AddColumnOperation>(), x => x.Name == "IsSeed" && x.IsNullable);
        Assert.DoesNotContain("County", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Null_catalog_columns_materialize_without_throwing()
    {
        await using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE StatusDefinitions SET MapsTo = NULL, Kind = NULL, IsSeed = NULL");
        db.ChangeTracker.Clear();

        var thrown = await Record.ExceptionAsync(() => db.StatusDefinitions.ToListAsync());
        Assert.Null(thrown);
        var rows = await db.StatusDefinitions.ToListAsync();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.MapsTo));
            Assert.False(string.IsNullOrWhiteSpace(row.Kind));
            Assert.NotNull(row.IsSeed);
        }

        var mapsTo = db.Model.FindEntityType(typeof(StatusDefinition))!.FindProperty(nameof(StatusDefinition.MapsTo));
        Assert.NotNull(mapsTo);
        Assert.True(mapsTo.IsNullable);
    }

    [Fact]
    public async Task Settings_seeds_must_eight_and_admin_can_rename_disable_not_hard_delete_seed()
    {
        await using var factory = TestAppFactory.Create();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var viewer = await Authed(factory, DatabaseSeeder.ViewerEmail);

        var listed = await admin.GetAsync("/api/settings/statuses");
        listed.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var names = json.RootElement.EnumerateArray().Select(x => x.GetProperty("displayName").GetString()).ToHashSet();
        foreach (var label in StatusCatalog.MustLabels)
        {
            Assert.Contains(label, names);
        }

        var needsWork = json.RootElement.EnumerateArray().First(x => x.GetProperty("code").GetString() == StatusCatalog.NeedsWork);
        var id = needsWork.GetProperty("id").GetGuid();

        var denied = await viewer.PutAsync($"/api/settings/statuses/{id}", TestAppFactory.Json(
            """{"code":"NeedsWork","displayName":"Needs Attention","color":"#C5E8E4","sortOrder":40,"isActive":true}"""));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var renamed = await admin.PutAsync($"/api/settings/statuses/{id}", TestAppFactory.Json(
            """{"code":"NeedsWork","displayName":"needs attention","color":"#C5E8E4","sortOrder":40,"isActive":true}"""));
        renamed.EnsureSuccessStatusCode();
        using var renamedJson = JsonDocument.Parse(await renamed.Content.ReadAsStringAsync());
        Assert.Equal("Needs Attention", renamedJson.RootElement.GetProperty("displayName").GetString());

        var disabled = await admin.PutAsync($"/api/settings/statuses/{id}", TestAppFactory.Json(
            """{"code":"NeedsWork","displayName":"Needs Attention","color":"#C5E8E4","sortOrder":40,"isActive":false}"""));
        disabled.EnsureSuccessStatusCode();
        using var disabledJson = JsonDocument.Parse(await disabled.Content.ReadAsStringAsync());
        Assert.False(disabledJson.RootElement.GetProperty("isActive").GetBoolean());

        var deleteSeed = await admin.DeleteAsync($"/api/settings/statuses/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, deleteSeed.StatusCode);

        var created = await admin.PostAsync("/api/settings/statuses", TestAppFactory.Json(
            """{"displayName":"hold over","color":"#C5CED6","sortOrder":99,"isActive":true,"mapsTo":"Research","kind":"Catalog"}"""));
        created.EnsureSuccessStatusCode();
        using var createdJson = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Hold Over", createdJson.RootElement.GetProperty("displayName").GetString());
        var customId = createdJson.RootElement.GetProperty("id").GetGuid();
        var removed = await admin.DeleteAsync($"/api/settings/statuses/{customId}");
        removed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Editor_assigns_catalog_status_filters_and_ocr_pipeline_stays()
    {
        await using var factory = TestAppFactory.Create();
        var editor = await Authed(factory, DatabaseSeeder.EditorEmail);
        var viewer = await Authed(factory, DatabaseSeeder.ViewerEmail);
        var id = await FirstReadyId(editor);

        var before = await editor.GetAsync($"/api/documents/{id}");
        before.EnsureSuccessStatusCode();
        using var beforeJson = JsonDocument.Parse(await before.Content.ReadAsStringAsync());
        var pipeline = beforeJson.RootElement.GetProperty("status").GetString();
        Assert.Equal(DocumentStatuses.Ready, pipeline);

        var viewerDenied = await viewer.PutAsync($"/api/documents/{id}/catalog-status", TestAppFactory.Json(
            """{"catalogStatus":"NeedsWork"}"""));
        Assert.Equal(HttpStatusCode.Forbidden, viewerDenied.StatusCode);

        var needsWork = await editor.PutAsync($"/api/documents/{id}/catalog-status", TestAppFactory.Json(
            """{"catalogStatus":"NeedsWork"}"""));
        needsWork.EnsureSuccessStatusCode();
        using var needsJson = JsonDocument.Parse(await needsWork.Content.ReadAsStringAsync());
        Assert.Equal(DocumentStatuses.Ready, needsJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(StatusCatalog.NeedsWork, needsJson.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal(ReviewWorkflow.NeedsReview, needsJson.RootElement.GetProperty("displayStatus").GetString());
        Assert.True(needsJson.RootElement.GetProperty("pipelineUnchanged").GetBoolean());

        var filteredNeeds = await editor.GetAsync("/api/documents?status=NeedsWork");
        filteredNeeds.EnsureSuccessStatusCode();
        using var filteredNeedsJson = JsonDocument.Parse(await filteredNeeds.Content.ReadAsStringAsync());
        Assert.Contains(filteredNeedsJson.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        var research = await editor.PutAsync($"/api/documents/{id}/catalog-status", TestAppFactory.Json(
            """{"catalogStatus":"Research"}"""));
        research.EnsureSuccessStatusCode();
        using var researchJson = JsonDocument.Parse(await research.Content.ReadAsStringAsync());
        Assert.Equal(DocumentStatuses.Ready, researchJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(StatusCatalog.Research, researchJson.RootElement.GetProperty("reviewStatus").GetString());
        Assert.Equal(StatusCatalog.Research, researchJson.RootElement.GetProperty("displayStatus").GetString());

        var filteredResearch = await editor.GetAsync("/api/documents?status=Research");
        filteredResearch.EnsureSuccessStatusCode();
        using var filteredResearchJson = JsonDocument.Parse(await filteredResearch.Content.ReadAsStringAsync());
        Assert.Contains(filteredResearchJson.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        var queued = await editor.GetAsync("/api/documents?status=InQueue");
        queued.EnsureSuccessStatusCode();
        using var queuedJson = JsonDocument.Parse(await queued.Content.ReadAsStringAsync());
        Assert.All(queuedJson.RootElement.EnumerateArray(), x =>
            Assert.True(
                x.GetProperty("status").GetString() == DocumentStatuses.Queued
                || x.GetProperty("reviewStatus").GetString() == StatusCatalog.InQueue));

        var ribbonReady = ReviewWorkflow.DisplayStatus(DocumentStatuses.Ready, StatusCatalog.NeedsWork, false);
        Assert.NotEqual(DocumentStatuses.Ready, ribbonReady);
        Assert.Equal(ReviewWorkflow.NeedsReview, ribbonReady);
        Assert.NotEqual("Ready", ReviewWorkflow.DisplayStatus(DocumentStatuses.Failed, null, false));
    }

    [Fact]
    public async Task Health_is_200_and_ocr_system_statuses_remain()
    {
        await using var factory = TestAppFactory.Create();
        var health = await factory.CreateJsonClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var listed = await admin.GetAsync("/api/settings/statuses");
        listed.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var codes = json.RootElement.EnumerateArray().Select(x => x.GetProperty("code").GetString()).ToHashSet();
        foreach (var pipeline in DocumentStatuses.All)
        {
            Assert.Contains(pipeline, codes);
        }
    }

    private static async Task<HttpClient> Authed(TestAppFactory factory, string email)
    {
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> FirstReadyId(HttpClient client)
    {
        var response = await client.GetAsync("/api/documents?status=Ready");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var ready = json.RootElement.EnumerateArray()
            .FirstOrDefault(x => x.GetProperty("status").GetString() == DocumentStatuses.Ready);
        Assert.NotEqual(default, ready.ValueKind);
        return ready.GetProperty("id").GetGuid();
    }

    private static void InvokeUp(MigrationBuilder builder)
    {
        var up = typeof(Phase525StatusesCatalog).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new Phase525StatusesCatalog(), [builder]);
    }

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
