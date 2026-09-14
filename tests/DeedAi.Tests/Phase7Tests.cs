namespace DeedAi.Tests;

public sealed class Phase7Tests
{
    [Fact]
    public void Shell_uses_gis_dark_sider_and_white_utility_header()
    {
        var css = Read("spa/src/styles.css");
        var theme = Read("spa/src/theme.ts");
        var shell = Read("spa/src/components/AppShell.tsx");
        var footer = Read("spa/src/components/SiteFooter.tsx");

        Assert.Contains("data-chrome=\"gis\"", shell, StringComparison.Ordinal);
        Assert.Contains("className={`app-shell", shell, StringComparison.Ordinal);
        Assert.Contains("className=\"topbar app-header ant-layout-header\"", shell, StringComparison.Ordinal);
        Assert.Contains("className=\"content-wrap", shell, StringComparison.Ordinal);
        Assert.Contains("className=\"brand\"", shell, StringComparison.Ordinal);
        Assert.Contains("BIS Consultants ·", shell, StringComparison.Ordinal);
        Assert.Contains("All Clients", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", shell, StringComparison.Ordinal);
        Assert.True(
            shell.IndexOf("id=\"app-sidebar\"", StringComparison.Ordinal)
            < shell.IndexOf("className=\"topbar app-header ant-layout-header\"", StringComparison.Ordinal),
            "Dark sider must be full-height left of the white utility header.");
        Assert.Contains("--mask-sider: #001529", css, StringComparison.Ordinal);
        Assert.Contains("--mask-primary: #1890ff", css, StringComparison.Ordinal);
        Assert.Contains("--mask-header: #fff", css, StringComparison.Ordinal);
        Assert.Contains("--mask-bg: #f0f2f5", css, StringComparison.Ordinal);
        Assert.Contains("--mask-radius: 2px", css, StringComparison.Ordinal);
        Assert.Contains(".app-header", css, StringComparison.Ordinal);
        Assert.Contains(".filter-toolbar", css, StringComparison.Ordinal);
        Assert.Contains(".kpi-card", css, StringComparison.Ordinal);
        Assert.Contains("--rail: #001529", css, StringComparison.Ordinal);
        Assert.Contains("--rail-menu: #000c17", css, StringComparison.Ordinal);
        Assert.Contains("--nav-active: #1890FF", css, StringComparison.Ordinal);
        Assert.Contains("--header: #FFFFFF", css, StringComparison.Ordinal);
        Assert.Contains("--gold: #E8C547", css, StringComparison.Ordinal);
        Assert.Contains("--teal: #0D8A7F", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--nav-active)", css, StringComparison.Ordinal);
        Assert.Contains(".sidebar a.active { background: var(--nav-active)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--rail: #F0F2F5", css, StringComparison.Ordinal);
        Assert.DoesNotContain("--rail: #FFFFFF", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".sidebar a.active { background: var(--review-bg)", css, StringComparison.Ordinal);

        Assert.Contains("teal: \"#0D8A7F\"", theme, StringComparison.Ordinal);
        Assert.Contains("header: \"#FFFFFF\"", theme, StringComparison.Ordinal);
        Assert.Contains("rail: \"#001529\"", theme, StringComparison.Ordinal);
        Assert.Contains("navActive: \"#1890FF\"", theme, StringComparison.Ordinal);
        Assert.Contains("navy: \"#1E2430\"", theme, StringComparison.Ordinal);
        Assert.Contains("gold: \"#E8C547\"", theme, StringComparison.Ordinal);

        Assert.Contains("Powered By:", footer, StringComparison.Ordinal);
        Assert.Contains("BIS Consultants", footer, StringComparison.Ordinal);
        Assert.Contains("login-wrap", Read("spa/src/components/AuthLayout.tsx"), StringComparison.Ordinal);
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
        Assert.Contains(".documents-table .documents-status-assign", css, StringComparison.Ordinal);
        Assert.Contains(".documents-table .documents-status-assign select", css, StringComparison.Ordinal);
        var statusAssign = SliceBetween(css, ".documents-status-assign {", ".token-row");
        Assert.Contains("min-height: var(--table-row-h)", statusAssign, StringComparison.Ordinal);
        Assert.DoesNotContain("--action-h", statusAssign, StringComparison.Ordinal);
        var tableControls = SliceBetween(css, ".documents-table tbody input,", ".documents-table thead th");
        Assert.Contains("max-height: var(--table-row-h)", tableControls, StringComparison.Ordinal);
        Assert.DoesNotContain("--action-h", tableControls, StringComparison.Ordinal);
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
    public void Login_shows_qa_test_credentials_without_gis_jargon()
    {
        var login = Read("spa/src/pages/LoginPage.tsx");
        var layout = Read("spa/src/components/AuthLayout.tsx");
        var css = Read("spa/src/styles.css");

        Assert.Contains("data-testid=\"test-login\"", login, StringComparison.Ordinal);
        Assert.Contains("Test login", login, StringComparison.Ordinal);
        Assert.Contains("QA AdminSeed", login, StringComparison.Ordinal);
        Assert.Contains("admin@bisconsultants.com", login, StringComparison.Ordinal);
        Assert.Contains("Bk9!De9vkOJ2JxDJhbxPJ2#", login, StringComparison.Ordinal);
        Assert.Contains("Client workspace", login, StringComparison.Ordinal);
        Assert.Contains("login-wrap", layout, StringComparison.Ordinal);
        Assert.Contains(".login-test", css, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", login, StringComparison.Ordinal);
        Assert.DoesNotContain("County", login, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", login, StringComparison.Ordinal);
        Assert.DoesNotContain("County", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", layout, StringComparison.Ordinal);
    }

    [Fact]
    public void Export_chips_and_copy_stay_deed_ai()
    {
        var dashboard = Read("spa/src/pages/DashboardPage.tsx");
        var reports = Read("spa/src/pages/ReportsPage.tsx");
        var ac = Read("docs/PHASE-7-GIS-CHROME-AC.md");

        Assert.Contains("className=\"filter-toolbar", dashboard, StringComparison.Ordinal);
        Assert.Contains("kpi-card", dashboard, StringComparison.Ordinal);
        Assert.Contains("app-header", Read("spa/src/components/AppShell.tsx"), StringComparison.Ordinal);
        Assert.Contains("content-wrap", Read("spa/src/components/AppShell.tsx"), StringComparison.Ordinal);
        Assert.Contains("Export PDF", dashboard, StringComparison.Ordinal);
        Assert.DoesNotContain("className=\"gold\"", dashboard, StringComparison.Ordinal);
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
    public void Phase_71_ships_live_gis_css_verbatim()
    {
        var gis = Read("spa/src/gis-shell.css");
        var ant = Read("spa/src/gis-ant-layout.css");
        var main = Read("spa/src/main.tsx");
        var ac = Read("docs/PHASE-7.1-GIS-SHELL-1TO1-AC.md");
        var documentsCss = Read("spa/src/styles.css");

        Assert.Contains("--mask-sider:#001529", gis, StringComparison.Ordinal);
        Assert.Contains("--mask-primary:#1890ff", gis, StringComparison.Ordinal);
        Assert.Contains("--mask-header:#fff", gis, StringComparison.Ordinal);
        Assert.Contains(".app-header{", gis, StringComparison.Ordinal);
        Assert.Contains(".filter-toolbar{", gis, StringComparison.Ordinal);
        Assert.Contains(".kpi-card{", gis, StringComparison.Ordinal);
        Assert.Contains(".page-head{", gis, StringComparison.Ordinal);
        Assert.Contains(".content-wrap{", gis, StringComparison.Ordinal);
        Assert.Contains(".app-shell .brand{", gis, StringComparison.Ordinal);
        Assert.Contains("index--oIitrwY.css", gis, StringComparison.Ordinal);
        Assert.Contains("siderBg #001529", ant, StringComparison.Ordinal);
        Assert.Contains(".ant-menu-dark .ant-menu-item-selected", ant, StringComparison.Ordinal);
        Assert.Contains("background-color: #1890ff", ant, StringComparison.Ordinal);
        Assert.Contains("height: 32px", ant, StringComparison.Ordinal);
        Assert.Contains("import \"./gis-shell.css\"", main, StringComparison.Ordinal);
        Assert.Contains("import \"./gis-ant-layout.css\"", main, StringComparison.Ordinal);
        Assert.Contains("--table-row-h: 32px", documentsCss, StringComparison.Ordinal);
        Assert.Contains("1:1", ac, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Items", Read("spa/src/components/AppShell.tsx"), StringComparison.Ordinal);
        Assert.DoesNotContain("County", Read("spa/src/pages/DashboardPage.tsx"), StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", Read("spa/src/pages/DashboardPage.tsx"), StringComparison.Ordinal);
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
