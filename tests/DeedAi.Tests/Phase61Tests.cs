using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;

namespace DeedAi.Tests;

public sealed class Phase61Tests
{
    [Fact]
    public void Unified_status_chrome_splits_ribbon_stage_from_catalog()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var table = Read("spa/src/documentsTable.ts");
        var ribbon = Read("spa/src/components/OcrRibbon.tsx");
        var theme = Read("spa/src/theme.ts");
        var catalog = Read("spa/src/statusCatalog.ts");

        Assert.Contains("ribbonStepForStage(query.stage)", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("ribbonStepForDocument(", documents, StringComparison.Ordinal);
        Assert.Contains("data-status-chrome=\"catalog\"", documents, StringComparison.Ordinal);
        Assert.Contains("data-status-chrome=\"catalog-chip\"", documents, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Status\"", documents, StringComparison.Ordinal);
        Assert.Equal(1, Count(documents, "aria-label=\"Filter Status\""));
        Assert.Contains("filterStatuses", documents, StringComparison.Ordinal);
        Assert.Contains("setCatalogStatus", documents, StringComparison.Ordinal);
        Assert.Contains("catalogLabel", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("Pipeline", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Ready\">", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Failed\">", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"NeedsReview\">", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Review\">", documents, StringComparison.Ordinal);
        Assert.DoesNotContain(">Flags<", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("flag-pill", documents, StringComparison.Ordinal);
        Assert.Contains("catalogCodeForStatus", catalog, StringComparison.Ordinal);
        Assert.Contains("Queued: \"InQueue\"", catalog, StringComparison.Ordinal);
        Assert.Contains("Failed: \"UploadError\"", catalog, StringComparison.Ordinal);
        Assert.Contains("NeedsReview: \"NeedsWork\"", catalog, StringComparison.Ordinal);

        Assert.Contains("stage=Queued", ribbon, StringComparison.Ordinal);
        Assert.Contains("stage=Processing", ribbon, StringComparison.Ordinal);
        Assert.Contains("stage=Review", ribbon, StringComparison.Ordinal);
        Assert.Contains("stage=Ready", ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("status=Queued", ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("status=NeedsReview", ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("status=Ready", ribbon, StringComparison.Ordinal);

        Assert.Contains("splitDocumentsStatusAndStage", table, StringComparison.Ordinal);
        Assert.Contains("stageToApiStatus", table, StringComparison.Ordinal);
        Assert.Contains("params.get(\"stage\")", table, StringComparison.Ordinal);
        Assert.Contains("params.set(\"stage\", query.stage)", table, StringComparison.Ordinal);
        Assert.Contains("params.get(\"status\")", table, StringComparison.Ordinal);
        Assert.Contains("params.set(\"status\", query.status)", table, StringComparison.Ordinal);
        Assert.Contains("DOCUMENTS_PIPELINE_STATUSES", table, StringComparison.Ordinal);

        Assert.Contains("export function ribbonStepForStage", theme, StringComparison.Ordinal);
        foreach (var label in StatusCatalog.MustLabels)
        {
            Assert.Contains(label, catalog, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Table_pane_hugs_eight_to_twelve_rows_with_sticky_chrome()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var css = Read("spa/src/styles.css");

        Assert.Contains("documents-table-wrap", documents, StringComparison.Ordinal);
        Assert.Contains("data-documents-pane=\"hug\"", documents, StringComparison.Ordinal);
        Assert.Contains("className=\"documents-table\"", documents, StringComparison.Ordinal);
        Assert.Contains("<thead>", documents, StringComparison.Ordinal);
        Assert.Contains("<tbody>", documents, StringComparison.Ordinal);
        Assert.Contains("documents-page", documents, StringComparison.Ordinal);
        Assert.Contains("documents-filter-row", documents, StringComparison.Ordinal);

        Assert.Contains("--documents-visible-rows: 10", css, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(var(--table-row-h) * (var(--documents-visible-rows) + 1))", css, StringComparison.Ordinal);
        Assert.Contains(".documents-table thead th", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains(".documents-table-wrap", css, StringComparison.Ordinal);
        Assert.Contains("overflow: auto", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 1100px", css, StringComparison.Ordinal);
        Assert.Contains("--space-1: 4px", css, StringComparison.Ordinal);
        Assert.Contains("--space-2: 8px", css, StringComparison.Ordinal);
        Assert.Contains("--space-3: 12px", css, StringComparison.Ordinal);
        Assert.Contains("--table-row-h: 44px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("max-height: calc(100vh - var(--topbar-h) - var(--ribbon-h)", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Carry_522_columns_search_delete_and_chart_links()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var table = Read("spa/src/documentsTable.ts");
        var path = Read("spa/src/documentsPath.ts");
        var shell = Read("spa/src/components/AppShell.tsx");

        foreach (var heading in new[] { "Status", "Client", "Volume", "Page", "Type", "PID", "Doc #", "Assignee", "Updated" })
        {
            Assert.Contains($"label=\"{heading}\"", documents, StringComparison.Ordinal);
        }

        foreach (var key in new[] { "status", "client", "volume", "page", "type", "pid", "documentNumber", "assignee", "updated" })
        {
            Assert.Contains($"sortKey=\"{key}\"", documents, StringComparison.Ordinal);
        }

        Assert.Contains("aria-sort", documents, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Client\"", documents, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Assignee\"", documents, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Filter Type\"", documents, StringComparison.Ordinal);
        Assert.Contains("className=\"search-field\"", documents, StringComparison.Ordinal);
        Assert.Equal(1, Count(documents, "placeholder=\"Search deeds\""));
        Assert.Contains("ConfirmSheet", documents, StringComparison.Ordinal);
        Assert.Contains("Soft-delete", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"status\", filters.status)", path, StringComparison.Ordinal);
        Assert.Contains("data-nav=\"system-mid\"", shell, StringComparison.Ordinal);
        Assert.Contains("DOCUMENTS_PAGE_SIZE = 50", table, StringComparison.Ordinal);
        Assert.DoesNotContain("FieldHelp", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("County", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("County", table, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", table, StringComparison.Ordinal);
        Assert.DoesNotContain("docNo", documents, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_61_does_not_reopen_ai_extract()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var table = Read("spa/src/documentsTable.ts");
        var ribbon = Read("spa/src/components/OcrRibbon.tsx");
        var ac = Read("docs/PHASE-6.1-DOCUMENTS-UX-DELTA-AC.md");

        Assert.Contains("Phase 6.1", ac, StringComparison.Ordinal);
        Assert.Contains("Does **not** reopen Phase 6 AI extract", ac, StringComparison.Ordinal);
        Assert.DoesNotContain("AzureOpenAI", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("DocumentIntelligence", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("AzureOpenAI", table, StringComparison.Ordinal);
        Assert.DoesNotContain("DocumentIntelligence", ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("County", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", documents, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_filter_and_assign_still_work()
    {
        await using var factory = TestAppFactory.Create();
        var editor = factory.CreateJsonClient();
        var token = await factory.LoginAsync(editor, DatabaseSeeder.EditorEmail);
        editor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var ready = await editor.GetAsync("/api/documents?status=Ready");
        ready.EnsureSuccessStatusCode();
        using var readyJson = JsonDocument.Parse(await ready.Content.ReadAsStringAsync());
        var row = readyJson.RootElement.EnumerateArray()
            .First(x => x.GetProperty("status").GetString() == DocumentStatuses.Ready);
        var id = row.GetProperty("id").GetGuid();

        var assigned = await editor.PutAsync($"/api/documents/{id}/catalog-status", TestAppFactory.Json(
            """{"catalogStatus":"NeedsWork"}"""));
        assigned.EnsureSuccessStatusCode();
        using var assignedJson = JsonDocument.Parse(await assigned.Content.ReadAsStringAsync());
        Assert.Equal(DocumentStatuses.Ready, assignedJson.RootElement.GetProperty("status").GetString());
        Assert.Equal(StatusCatalog.NeedsWork, assignedJson.RootElement.GetProperty("reviewStatus").GetString());
        Assert.True(assignedJson.RootElement.GetProperty("pipelineUnchanged").GetBoolean());

        var filtered = await editor.GetAsync("/api/documents?status=NeedsWork");
        filtered.EnsureSuccessStatusCode();
        using var filteredJson = JsonDocument.Parse(await filtered.Content.ReadAsStringAsync());
        Assert.Contains(filteredJson.RootElement.EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        var queued = await editor.GetAsync("/api/documents?status=Queued");
        queued.EnsureSuccessStatusCode();
        using var queuedJson = JsonDocument.Parse(await queued.Content.ReadAsStringAsync());
        Assert.All(queuedJson.RootElement.EnumerateArray(), x =>
            Assert.Equal(DocumentStatuses.Queued, x.GetProperty("status").GetString()));
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
