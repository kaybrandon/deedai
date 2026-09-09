using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase522Tests : IClassFixture<TestAppFactory>
{
    private const string MigrationId = "20260909220000_DocumentListFields";
    private readonly TestAppFactory _factory;

    public Phase522Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Documents_is_a_mask_f_data_table_with_one_search()
    {
        var page = Read("spa/src/pages/DocumentsPage.tsx");
        var css = Read("spa/src/styles.css");
        var shell = Read("spa/src/components/AppShell.tsx");

        Assert.Contains("className=\"documents-table\"", page, StringComparison.Ordinal);
        Assert.Contains("data-table=\"documents\"", page, StringComparison.Ordinal);
        Assert.Contains("<table", page, StringComparison.Ordinal);
        Assert.Contains("<thead>", page, StringComparison.Ordinal);
        Assert.Contains("documents-table-wrap", page, StringComparison.Ordinal);
        Assert.Contains("<OcrRibbon", page, StringComparison.Ordinal);
        Assert.Contains("Retry Failed", page, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", page, StringComparison.Ordinal);
        Assert.Contains("Soft-delete", page, StringComparison.Ordinal);
        Assert.Contains("className=\"search-field\"", page, StringComparison.Ordinal);
        Assert.Equal(1, Count(page, "placeholder=\"Search deeds\""));
        Assert.Equal(1, Count(page, "className=\"search-field"));
        Assert.DoesNotContain("placeholder=\"Search", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"search\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("card-stack", page, StringComparison.Ordinal);
        Assert.DoesNotContain("docNo", page, StringComparison.Ordinal);
        Assert.DoesNotContain("County", page, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", page, StringComparison.Ordinal);

        Assert.Contains("max-width: 360px", css, StringComparison.Ordinal);
        Assert.Contains("--table-row-h: 44px", css, StringComparison.Ordinal);
        Assert.Contains("--table-cell-pad-y: 6px", css, StringComparison.Ordinal);
        Assert.Contains("--table-header-bg: #E8EEF2", css, StringComparison.Ordinal);
        Assert.Contains("--table-stripe: #E8EEF2", css, StringComparison.Ordinal);
        Assert.Contains(".documents-table thead th", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--action-h)", css, StringComparison.Ordinal);
        Assert.Contains(".documents-table tbody tr:nth-child(even)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Column_headings_sort_filter_and_persist_required_fields()
    {
        var page = Read("spa/src/pages/DocumentsPage.tsx");
        var helper = Read("spa/src/documentsTable.ts");

        foreach (var heading in new[] { "Status", "Client", "Volume", "Page", "Type", "PID", "Doc #", "Assignee", "Updated" })
        {
            Assert.Contains($"label=\"{heading}\"", page, StringComparison.Ordinal);
        }

        foreach (var key in new[] { "status", "client", "volume", "page", "type", "pid", "documentNumber", "assignee", "updated" })
        {
            Assert.Contains($"sortKey=\"{key}\"", page, StringComparison.Ordinal);
            Assert.Contains($"\"{key}\"", helper, StringComparison.Ordinal);
        }

        Assert.Contains("aria-sort", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Status\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Client\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Assignee\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Type\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"From date\"", page, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"To date\"", page, StringComparison.Ordinal);
        Assert.Contains("useSearchParams", page, StringComparison.Ordinal);
        Assert.Contains("DOCUMENTS_TABLE_STORAGE_KEY", helper, StringComparison.Ordinal);
        Assert.Contains("deedai.documents.table", helper, StringComparison.Ordinal);
        Assert.Contains("DOCUMENTS_PAGE_SIZE = 50", helper, StringComparison.Ordinal);
        Assert.Contains("No Documents Yet", page, StringComparison.Ordinal);
        Assert.Contains("No Documents Match", page, StringComparison.Ordinal);
        Assert.DoesNotContain("docNo", helper, StringComparison.Ordinal);
        Assert.DoesNotContain("\"vol\"", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Search_consumes_locked_field_names()
    {
        var helper = Read("spa/src/documentsTable.ts");
        var mapping = Read("src/DeedAi.Api/DocumentListMapping.cs");
        var filters = Read("src/DeedAi.Api/DocumentFilters.cs");
        var entity = Read("src/DeedAi.Domain/Entities/Document.cs");
        var listItem = Read("src/DeedAi.Api/Contracts/DocumentContracts.cs");

        foreach (var name in new[] { "documentNumber", "volume", "page", "deedType", "pid", "mailingStreet", "mailingCity", "mailingState", "mailingZip", "grantors", "grantees" })
        {
            Assert.Contains(name, helper, StringComparison.Ordinal);
        }

        foreach (var name in new[] { "DocumentNumber", "Volume", "Page", "DeedType", "Pid", "MailingStreet", "MailingCity", "MailingState", "MailingZip", "Grantors", "Grantees" })
        {
            Assert.Contains(name, entity, StringComparison.Ordinal);
            Assert.Contains(name, mapping, StringComparison.Ordinal);
            Assert.Contains(name, filters, StringComparison.Ordinal);
            Assert.Contains(name, listItem, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("docNo", mapping, StringComparison.Ordinal);
        Assert.DoesNotContain("DocNo", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("County", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", helper, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_is_designer_first_after_phase491()
    {
        Assert.True(string.CompareOrdinal("20260909190000_Phase491RemovePropertyDefaults", MigrationId) < 0);

        var type = typeof(DocumentListFields);
        Assert.Equal(MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(MigrationId, discovered);
        Assert.Equal(typeof(DocumentListFields), db.GetService<IMigrationsAssembly>().Migrations[MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260909220000_DocumentListFields.Designer.cs");
        Assert.Contains($"[Migration(\"{MigrationId}\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        foreach (var name in new[] { "DocumentNumber", "Volume", "Page", "Pid", "MailingStreet", "MailingCity", "MailingState", "MailingZip", "Grantors", "Grantees" })
        {
            Assert.Contains($"\"{name}\"", designer, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("DocNo", designer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task List_search_hits_locked_document_fields()
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, DatabaseSeeder.EditorEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        await AssertSearchHits(client, "2024-0812", "documentNumber");
        await AssertSearchHits(client, "142", "volume");
        await AssertSearchHits(client, "Jane Example", "grantors");
        await AssertSearchHits(client, "Acme Holdings", "grantees");
        await AssertSearchHits(client, "12-345-678", "pid");
        await AssertSearchHits(client, "Springfield", "mailingCity");
        await AssertSearchHits(client, "62701", "mailingZip");
        await AssertSearchHits(client, "Warranty Deed", "deedType");
    }

    private static async Task AssertSearchHits(HttpClient client, string search, string jsonName)
    {
        var response = await client.GetAsync($"/api/documents?search={Uri.EscapeDataString(search)}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0, $"search={search} returned no rows");
        var row = json.RootElement[0];
        Assert.True(row.TryGetProperty(jsonName, out _), $"missing JSON property {jsonName}");
        Assert.False(row.TryGetProperty("docNo", out _), "must not invent docNo");
        Assert.False(row.TryGetProperty("vol", out _), "must not invent vol");
        Assert.True(row.TryGetProperty("grantors", out var grantors));
        Assert.Equal(JsonValueKind.Array, grantors.ValueKind);
        Assert.True(row.TryGetProperty("grantees", out var grantees));
        Assert.Equal(JsonValueKind.Array, grantees.ValueKind);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = 0; (index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0; index += value.Length)
        {
            count++;
        }

        return count;
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
