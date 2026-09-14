namespace DeedAi.Tests;

public sealed class Phase7Tests
{
    [Fact]
    public void Shell_uses_gis_light_rail_and_teal_header()
    {
        var css = Read("spa/src/styles.css");
        var theme = Read("spa/src/theme.ts");
        var shell = Read("spa/src/components/AppShell.tsx");
        var footer = Read("spa/src/components/SiteFooter.tsx");

        Assert.Contains("data-chrome=\"gis\"", shell, StringComparison.Ordinal);
        Assert.Contains("--header: #0D8A7F", css, StringComparison.Ordinal);
        Assert.Contains("--rail: #FFFFFF", css, StringComparison.Ordinal);
        Assert.Contains("--gold: #E8C547", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--header)", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--rail)", css, StringComparison.Ordinal);
        Assert.Contains(".gold {", css, StringComparison.Ordinal);
        Assert.Contains("text-transform: uppercase", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--teal)", css, StringComparison.Ordinal);

        Assert.Contains("teal: \"#0D8A7F\"", theme, StringComparison.Ordinal);
        Assert.Contains("header: \"#0D8A7F\"", theme, StringComparison.Ordinal);
        Assert.Contains("rail: \"#FFFFFF\"", theme, StringComparison.Ordinal);
        Assert.Contains("navy: \"#1E2430\"", theme, StringComparison.Ordinal);
        Assert.Contains("gold: \"#E8C547\"", theme, StringComparison.Ordinal);

        Assert.Contains("Powered By:", footer, StringComparison.Ordinal);
        Assert.Contains("BIS Consultants", footer, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("County", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Density_keeps_61_pane_with_gis_row_height()
    {
        var css = Read("spa/src/styles.css");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");

        Assert.Contains("--table-row-h: 32px", css, StringComparison.Ordinal);
        Assert.Contains("--table-cell-pad-y: 4px", css, StringComparison.Ordinal);
        Assert.Contains("--documents-visible-rows: 10", css, StringComparison.Ordinal);
        Assert.Contains("max-height: calc(var(--table-row-h) * (var(--documents-visible-rows) + 1))", css, StringComparison.Ordinal);
        Assert.Contains("data-documents-pane=\"hug\"", documents, StringComparison.Ordinal);
        Assert.Contains(".documents-table thead th", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains("--action-h: 44px", css, StringComparison.Ordinal);
        Assert.DoesNotContain("radial-gradient", css, StringComparison.Ordinal);
        Assert.DoesNotContain("backdrop-filter", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Status_first_does_not_reopen_61_chrome_fight()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var settings = Read("spa/src/pages/SettingsPage.tsx");
        var ribbon = Read("spa/src/components/OcrRibbon.tsx");
        var review = Read("spa/src/pages/ReviewPage.tsx");

        var filterRow = SliceBetween(documents, "documents-filter-row", "canAdmin && rows.some");
        Assert.True(
            filterRow.IndexOf("Filter Status", StringComparison.Ordinal)
            < filterRow.IndexOf("Search deeds", StringComparison.Ordinal),
            "Documents Status filter must sit before Search.");
        Assert.Contains("filter-status-first", documents, StringComparison.Ordinal);
        Assert.Equal(1, Count(documents, "data-status-chrome=\"catalog\""));
        Assert.Contains("data-status-chrome=\"catalog-chip\"", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Ready\">", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"Failed\">", documents, StringComparison.Ordinal);
        Assert.Contains("ribbonStepForStage(query.stage)", documents, StringComparison.Ordinal);
        Assert.Contains("stage=Queued", ribbon, StringComparison.Ordinal);
        Assert.DoesNotContain("status=Queued", ribbon, StringComparison.Ordinal);

        Assert.True(
            settings.IndexOf("status-first", StringComparison.Ordinal)
            < settings.IndexOf("<SystemHealthPanel", StringComparison.Ordinal),
            "Settings Statuses catalog must read first.");
        Assert.Equal(1, Count(settings, "data-testid=\"statuses-catalog\""));
        Assert.Contains("className=\"gold\"", settings, StringComparison.Ordinal);
        Assert.Contains("Export Excel", settings, StringComparison.Ordinal);

        Assert.Contains("review-header title-band", review, StringComparison.Ordinal);
        Assert.Contains("minmax(200px, 0.55fr) minmax(0, 1.2fr) minmax(280px, 0.9fr)", Read("spa/src/styles.css"), StringComparison.Ordinal);
        Assert.Contains("className=\"gold\"", review, StringComparison.Ordinal);
        Assert.Contains("Export PDF", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", review, StringComparison.Ordinal);
        Assert.DoesNotContain("County", review, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", review, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_chips_and_copy_stay_deed_ai()
    {
        var dashboard = Read("spa/src/pages/DashboardPage.tsx");
        var reports = Read("spa/src/pages/ReportsPage.tsx");
        var ac = Read("docs/PHASE-7-GIS-CHROME-AC.md");

        Assert.Contains("className=\"gold\"", dashboard, StringComparison.Ordinal);
        Assert.Contains("Export PDF", dashboard, StringComparison.Ordinal);
        Assert.Contains("className=\"gold\"", reports, StringComparison.Ordinal);
        Assert.Contains("Phase 7", ac, StringComparison.Ordinal);
        Assert.Contains("#0D8A7F", ac, StringComparison.Ordinal);
        Assert.Contains("#E8C547", ac, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("County", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("County", reports, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", reports, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_7_adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase7*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*GisChrome*").Any());
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

    private static string SliceBetween(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {startMarker}");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, $"Missing {endMarker} after {startMarker}");
        return source[start..end];
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
