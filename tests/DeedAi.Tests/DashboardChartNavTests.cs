namespace DeedAi.Tests;

public sealed class DashboardChartNavTests
{
    [Fact]
    public void Chart_titles_match_dashboard_sentence_case()
    {
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var pdf = Read("src/DeedAi.Infrastructure/Export/DeedPdfWriter.cs");

        Assert.Contains("<h2>Status mix</h2>", page, StringComparison.Ordinal);
        Assert.Contains("<h2>By user</h2>", page, StringComparison.Ordinal);
        Assert.Contains("<h2>Volume over time</h2>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("Status Mix", page, StringComparison.Ordinal);
        Assert.DoesNotContain("By Users", page, StringComparison.Ordinal);
        Assert.DoesNotContain("By users", page, StringComparison.Ordinal);
        Assert.Contains("Status mix", charts, StringComparison.Ordinal);
        Assert.Contains("Status mix", pdf, StringComparison.Ordinal);
        Assert.Contains("By user", pdf, StringComparison.Ordinal);
        AssertNoLegacyNames(page);
        AssertNoLegacyNames(charts);
    }

    [Fact]
    public void Status_cards_and_donut_share_documents_status_filter()
    {
        var helper = Read("spa/src/documentsPath.ts");
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");

        Assert.Contains("params.set(\"status\", filters.status)", helper, StringComparison.Ordinal);
        Assert.Contains("params.set(\"clientId\", filters.clientId)", helper, StringComparison.Ordinal);
        Assert.Contains("`/documents?${query}`", helper, StringComparison.Ordinal);

        Assert.Contains("import { documentsPath } from \"../documentsPath\"", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ clientId: applied.clientId })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Queued\", clientId: applied.clientId })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Processing\", clientId: applied.clientId })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Ready\", clientId: applied.clientId })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Failed\", clientId: applied.clientId })", page, StringComparison.Ordinal);
        Assert.Contains("<StatusMixChart data={mix} clientId={applied.clientId} />", page, StringComparison.Ordinal);

        Assert.Contains("onClick:", charts, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status, clientId })", charts, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: slice.status, clientId })", charts, StringComparison.Ordinal);
        Assert.Contains("goStatus(slice.status)", charts, StringComparison.Ordinal);

        Assert.Contains("params.get(\"status\")", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"status\", status)", documents, StringComparison.Ordinal);
    }

    [Fact]
    public void By_user_bars_reuse_documents_assignee_filter()
    {
        var helper = Read("spa/src/documentsPath.ts");
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");

        Assert.Contains("params.set(\"assigneeUserId\", filters.assigneeUserId)", helper, StringComparison.Ordinal);
        Assert.Contains("<ByUserChart data={byUser} clientId={applied.clientId} />", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ assigneeUserId: user.userId, clientId })", charts, StringComparison.Ordinal);
        Assert.Contains("goUser(data.users[elements[0]?.index ?? -1])", charts, StringComparison.Ordinal);
        Assert.Contains("if (!user?.userId)", charts, StringComparison.Ordinal);
        Assert.Contains("chart-legend-static", charts, StringComparison.Ordinal);

        Assert.Contains("params.get(\"assigneeUserId\")", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"assigneeUserId\", assignee)", documents, StringComparison.Ordinal);
    }

    [Fact]
    public void Volume_stays_non_clickable_because_documents_has_no_date_filter()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var page = Read("spa/src/pages/DashboardPage.tsx");

        var volumeFn = SliceFunction(charts, "export function VolumeChart");
        Assert.DoesNotContain("documentsPath", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("onClick", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("useNavigate", volumeFn, StringComparison.Ordinal);
        Assert.Contains("<VolumeChart data={volume} />", page, StringComparison.Ordinal);

        Assert.DoesNotContain("params.get(\"from\")", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("params.set(\"from\"", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("params.get(\"to\")", documents, StringComparison.Ordinal);
        Assert.DoesNotContain("params.set(\"to\"", documents, StringComparison.Ordinal);
    }

    [Fact]
    public void Interactive_chart_hit_targets_are_at_least_44px_and_mask_a_is_unchanged()
    {
        var css = Read("spa/src/styles.css");
        var theme = Read("spa/src/theme.ts");
        var charts = Read("spa/src/charts.ts");

        Assert.Contains(".chart-legend-link", css, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px", css, StringComparison.Ordinal);
        Assert.Contains("maxBarThickness: 44", Read("spa/src/components/DashboardCharts.tsx"), StringComparison.Ordinal);

        foreach (var token in new[] { "#F4F6F8", "#E8EEF2", "#4F7C8A", "#2C3A45", "#A8D5C0", "#E8B4B0" })
        {
            Assert.Contains(token, css, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(token, theme, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("maskA", charts, StringComparison.Ordinal);
        Assert.DoesNotContain("County", css, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Existing_chart_component_exports_and_api_routes_stay()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var api = Read("spa/src/api.ts");
        var page = Read("spa/src/pages/DashboardPage.tsx");

        Assert.Contains("export function StatusMixChart", charts, StringComparison.Ordinal);
        Assert.Contains("export function ByUserChart", charts, StringComparison.Ordinal);
        Assert.Contains("export function VolumeChart", charts, StringComparison.Ordinal);
        Assert.Contains("/api/dashboard/charts/status-mix", api, StringComparison.Ordinal);
        Assert.Contains("/api/dashboard/charts/by-user", api, StringComparison.Ordinal);
        Assert.Contains("/api/dashboard/charts/volume", api, StringComparison.Ordinal);
        Assert.Contains("StatusMixChart", page, StringComparison.Ordinal);
        Assert.Contains("ByUserChart", page, StringComparison.Ordinal);
        Assert.Contains("VolumeChart", page, StringComparison.Ordinal);
    }

    [Fact]
    public void Adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*ChartNav*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*ChartClick*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase48*").Any());
    }

    private static string SliceFunction(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {marker}");
        return source[start..];
    }

    private static void AssertNoLegacyNames(string text)
    {
        Assert.DoesNotContain("County", text, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", text, StringComparison.Ordinal);
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
