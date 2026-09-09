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
using DeletePolicyMigration = DeedAi.Infrastructure.Data.Migrations.DeletePolicy;

namespace DeedAi.Tests;

public sealed class Phase524Tests
{
    private const string MigrationId = DeletePolicyMigration.MigrationId;
    private const string PriorMigrationId = "20260909230000_DocumentListFieldNullDefaults";

    [Fact]
    public void Domain_defaults_all_editors_and_never_lets_viewer_or_uploader_delete()
    {
        Assert.Equal(DeletePolicy.AllEditors, DeletePolicy.Normalize(null));
        Assert.Equal(DeletePolicy.AllEditors, DeletePolicy.Normalize(""));
        Assert.Equal(DeletePolicy.AllEditors, DeletePolicy.Normalize("unknown"));
        Assert.Equal(DeletePolicy.AdminOnly, DeletePolicy.Normalize("adminonly"));
        Assert.Equal("All Editors", DeletePolicy.Label(null));
        Assert.Equal("Admin only", DeletePolicy.Label(DeletePolicy.AdminOnly));

        Assert.True(DeletePolicy.Allows(AppRoles.Admin, DeletePolicy.AdminOnly));
        Assert.True(DeletePolicy.Allows(AppRoles.Admin, DeletePolicy.AllEditors));
        Assert.True(DeletePolicy.Allows(AppRoles.Editor, DeletePolicy.AllEditors));
        Assert.False(DeletePolicy.Allows(AppRoles.Editor, DeletePolicy.AdminOnly));
        Assert.False(DeletePolicy.Allows(AppRoles.Uploader, DeletePolicy.AllEditors));
        Assert.False(DeletePolicy.Allows(AppRoles.Uploader, DeletePolicy.AdminOnly));
        Assert.False(DeletePolicy.Allows(AppRoles.Viewer, DeletePolicy.AllEditors));
        Assert.False(DeletePolicy.Allows(AppRoles.Viewer, DeletePolicy.AdminOnly));
    }

    [Fact]
    public void Settings_and_documents_ui_gate_delete_policy_without_legacy_jargon()
    {
        var settings = Read("spa/src/pages/SettingsPage.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var api = Read("spa/src/api.ts");
        var css = Read("spa/src/styles.css");

        Assert.Contains("Delete Policy", settings, StringComparison.Ordinal);
        Assert.Contains("Who Can Delete", settings, StringComparison.Ordinal);
        Assert.Contains("All Editors", settings, StringComparison.Ordinal);
        Assert.Contains("Admin only", settings, StringComparison.Ordinal);
        Assert.Contains("updateDeletePolicy", settings, StringComparison.Ordinal);
        Assert.Contains("settings.deletePolicy", settings, StringComparison.Ordinal);
        Assert.Contains("/api/settings/delete-policy", api, StringComparison.Ordinal);

        Assert.Contains("canDelete && !row.isDeleted", documents, StringComparison.Ordinal);
        Assert.Contains("delete-policy-hint", documents, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", documents, StringComparison.Ordinal);
        Assert.Contains("Soft-delete", documents, StringComparison.Ordinal);
        Assert.Contains("setPendingDelete", documents, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", css, StringComparison.Ordinal);
        Assert.Contains(".delete-policy-choice { min-height: var(--action-h); }", css, StringComparison.Ordinal);

        foreach (var file in new[] { settings, documents, api })
        {
            Assert.DoesNotContain("Super Admin", file, StringComparison.Ordinal);
            Assert.DoesNotContain("CAMA", file, StringComparison.Ordinal);
            Assert.DoesNotContain("County", file, StringComparison.Ordinal);
            Assert.DoesNotContain("Delete Deed", file, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Migration_is_designer_first_after_null_defaults()
    {
        Assert.True(string.CompareOrdinal(PriorMigrationId, MigrationId) < 0);

        var type = typeof(DeletePolicyMigration);
        Assert.Equal(MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(MigrationId, discovered);
        Assert.Equal(type, db.GetService<IMigrationsAssembly>().Migrations[MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260910010000_DeletePolicy.Designer.cs");
        Assert.Contains($"[Migration(\"{MigrationId}\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        Assert.Contains("\"WhoCanDelete\"", designer, StringComparison.Ordinal);
        Assert.Contains("DeletePolicySettings", designer, StringComparison.Ordinal);
        Assert.Contains("HasDefaultValue(\"AllEditors\")", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("Super Admin", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_backfills_nulls_and_adds_sql_server_default()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeUp(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        Assert.Contains("UPDATE DeletePolicySettings SET WhoCanDelete = 'AllEditors' WHERE WhoCanDelete IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("DF_DeletePolicySettings_WhoCanDelete", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT N'AllEditors' FOR [WhoCanDelete]", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO DeletePolicySettings", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("County", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);

        var create = builder.Operations.OfType<CreateTableOperation>().Single(x => x.Name == "DeletePolicySettings");
        var who = create.Columns.Single(x => x.Name == "WhoCanDelete");
        Assert.True(who.IsNullable);
        Assert.Equal("AllEditors", who.DefaultValue);
    }

    [Fact]
    public async Task Null_who_can_delete_materializes_as_all_editors()
    {
        await using var factory = TestAppFactory.Create();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        await db.Database.ExecuteSqlRawAsync("UPDATE DeletePolicySettings SET WhoCanDelete = NULL");
        db.ChangeTracker.Clear();

        var thrown = await Record.ExceptionAsync(() => db.DeletePolicySettings.ToListAsync());
        Assert.Null(thrown);
        var row = Assert.Single(await db.DeletePolicySettings.ToListAsync());
        Assert.Equal(DeletePolicy.AllEditors, DeletePolicy.Normalize(row.WhoCanDelete));
        Assert.Equal(DeletePolicy.AllEditors, row.WhoCanDelete);

        var property = db.Model.FindEntityType(typeof(DeletePolicySettings))!.FindProperty(nameof(DeletePolicySettings.WhoCanDelete));
        Assert.NotNull(property);
        Assert.True(property.IsNullable);
    }

    [Fact]
    public async Task Settings_persist_admin_only_and_all_editors()
    {
        await using var factory = TestAppFactory.Create();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var editor = await Authed(factory, DatabaseSeeder.EditorEmail);

        var initial = await admin.GetAsync("/api/settings/delete-policy");
        Assert.Equal(HttpStatusCode.OK, initial.StatusCode);
        using (var json = JsonDocument.Parse(await initial.Content.ReadAsStringAsync()))
        {
            Assert.Equal(DeletePolicy.AllEditors, json.RootElement.GetProperty("whoCanDelete").GetString());
            Assert.True(json.RootElement.GetProperty("canDelete").GetBoolean());
        }

        var saveAdminOnly = await admin.PutAsync(
            "/api/settings/delete-policy",
            TestAppFactory.Json("""{"whoCanDelete":"AdminOnly"}"""));
        Assert.Equal(HttpStatusCode.OK, saveAdminOnly.StatusCode);
        using (var json = JsonDocument.Parse(await saveAdminOnly.Content.ReadAsStringAsync()))
        {
            Assert.Equal(DeletePolicy.AdminOnly, json.RootElement.GetProperty("whoCanDelete").GetString());
            Assert.Equal("Admin only", json.RootElement.GetProperty("label").GetString());
            Assert.Equal(DatabaseSeeder.AdminEmail, json.RootElement.GetProperty("updatedByEmail").GetString());
        }

        var editorView = await editor.GetAsync("/api/settings/delete-policy");
        Assert.Equal(HttpStatusCode.OK, editorView.StatusCode);
        using (var json = JsonDocument.Parse(await editorView.Content.ReadAsStringAsync()))
        {
            Assert.Equal(DeletePolicy.AdminOnly, json.RootElement.GetProperty("whoCanDelete").GetString());
            Assert.False(json.RootElement.GetProperty("canDelete").GetBoolean());
        }

        var editorPut = await editor.PutAsync(
            "/api/settings/delete-policy",
            TestAppFactory.Json("""{"whoCanDelete":"AllEditors"}"""));
        Assert.Equal(HttpStatusCode.Forbidden, editorPut.StatusCode);

        var saveAll = await admin.PutAsync(
            "/api/settings/delete-policy",
            TestAppFactory.Json("""{"whoCanDelete":"AllEditors"}"""));
        Assert.Equal(HttpStatusCode.OK, saveAll.StatusCode);
        var reload = await editor.GetAsync("/api/settings/delete-policy");
        using var reloaded = JsonDocument.Parse(await reload.Content.ReadAsStringAsync());
        Assert.Equal(DeletePolicy.AllEditors, reloaded.RootElement.GetProperty("whoCanDelete").GetString());
        Assert.True(reloaded.RootElement.GetProperty("canDelete").GetBoolean());
    }

    [Fact]
    public async Task Admin_only_denies_editor_with_403_and_still_lets_admin_delete_and_restore()
    {
        await using var factory = TestAppFactory.Create();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var editor = await Authed(factory, DatabaseSeeder.EditorEmail);
        var id = await FirstDocumentId(editor);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(
            "/api/settings/delete-policy",
            TestAppFactory.Json("""{"whoCanDelete":"AdminOnly"}"""))).StatusCode);

        var denied = await editor.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var deniedBody = await denied.Content.ReadAsStringAsync();
        Assert.Contains("Access denied", deniedBody, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Editor", deniedBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"status\":404", deniedBody, StringComparison.Ordinal);

        Assert.Equal(HttpStatusCode.OK, (await admin.DeleteAsync($"/api/documents/{id}")).StatusCode);
        var hidden = await editor.GetAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var editorRestore = await editor.PostAsync($"/api/documents/{id}/restore", null);
        Assert.Equal(HttpStatusCode.Forbidden, editorRestore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/documents/{id}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/documents/{id}")).StatusCode);
    }

    [Fact]
    public async Task All_editors_allows_editor_and_viewer_never_deletes()
    {
        await using var factory = TestAppFactory.Create();
        var admin = await Authed(factory, DatabaseSeeder.AdminEmail);
        var editor = await Authed(factory, DatabaseSeeder.EditorEmail);
        var viewer = await Authed(factory, DatabaseSeeder.ViewerEmail);
        var uploader = await Authed(factory, DatabaseSeeder.UploaderEmail);
        var id = await FirstDocumentId(editor);

        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsync(
            "/api/settings/delete-policy",
            TestAppFactory.Json("""{"whoCanDelete":"AllEditors"}"""))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await editor.DeleteAsync($"/api/documents/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsync($"/api/documents/{id}/restore", null)).StatusCode);

        var viewerDelete = await viewer.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, viewerDelete.StatusCode);
        Assert.Contains("Access denied", await viewerDelete.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var uploaderDelete = await uploader.DeleteAsync($"/api/documents/{id}");
        Assert.Equal(HttpStatusCode.Forbidden, uploaderDelete.StatusCode);
    }

    [Fact]
    public async Task Health_stays_200_and_restore_path_stays_admin()
    {
        await using var factory = TestAppFactory.Create();
        var health = await factory.CreateJsonClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        var restore = Read("spa/src/pages/RestorePage.tsx");
        Assert.Contains("canAdmin", restore, StringComparison.Ordinal);
        var controller = Read("src/DeedAi.Api/Controllers/DocumentsController.cs");
        Assert.Contains("RolePolicies.CanAdmin", controller, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status403Forbidden", controller, StringComparison.Ordinal);
    }

    private static async Task<HttpClient> Authed(TestAppFactory factory, string email)
    {
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, email);
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

    private static void InvokeUp(MigrationBuilder builder)
    {
        var up = typeof(DeletePolicyMigration).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new DeletePolicyMigration(), [builder]);
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
