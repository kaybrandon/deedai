namespace DeedAi.Tests;

public sealed class DashboardChartNavTests
{
    [Fact]
    public void Chart_titles_match_ba_title_case()
    {
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var pdf = Read("src/DeedAi.Infrastructure/Export/DeedPdfWriter.cs");

        Assert.Contains("<h2>Status Mix</h2>", page, StringComparison.Ordinal);
        Assert.Contains("<h2>By Users</h2>", page, StringComparison.Ordinal);
        Assert.Contains("<h2>Volume Over Time</h2>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Status mix</h2>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>By user</h2>", page, StringComparison.Ordinal);
        Assert.Contains("Status Mix", charts, StringComparison.Ordinal);
        Assert.Contains("Status Mix", pdf, StringComparison.Ordinal);
        Assert.Contains("By Users", pdf, StringComparison.Ordinal);
        Assert.Contains("Volume Over Time", pdf, StringComparison.Ordinal);
        AssertNoLegacyNames(page);
        AssertNoLegacyNames(charts);
    }

    [Fact]
    public void Spa_headings_nav_and_primary_labels_use_title_case()
    {
        var shell = Read("spa/src/components/AppShell.tsx");
        var dashboard = Read("spa/src/pages/DashboardPage.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var review = Read("spa/src/pages/ReviewPage.tsx");
        var users = Read("spa/src/pages/UsersPage.tsx");
        var settings = Read("spa/src/pages/SettingsPage.tsx");
        var software = Read("spa/src/pages/SoftwarePage.tsx");
        var login = Read("spa/src/pages/LoginPage.tsx");

        Assert.Contains("My Profile", shell, StringComparison.Ordinal);
        Assert.Contains("Logged In As", shell, StringComparison.Ordinal);
        Assert.Contains("Sign Out", shell, StringComparison.Ordinal);
        Assert.Contains("<h2>Status Mix</h2>", dashboard, StringComparison.Ordinal);
        Assert.Contains("<h2>By Users</h2>", dashboard, StringComparison.Ordinal);
        Assert.Contains("<h2>Volume Over Time</h2>", dashboard, StringComparison.Ordinal);
        Assert.Contains("<h1>Upload Documents</h1>", Read("spa/src/pages/UploadPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("<h1>Deed Review</h1>", review, StringComparison.Ordinal);
        Assert.Contains("<h1>My Profile</h1>", Read("spa/src/pages/ProfilePage.tsx"), StringComparison.Ordinal);
        Assert.Contains("<h1>Access Denied</h1>", Read("spa/src/pages/DeniedPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("<h1>Reset Password</h1>", Read("spa/src/pages/ForgotPasswordPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("Sign In", login, StringComparison.Ordinal);
        Assert.Contains("Retry Extract", review, StringComparison.Ordinal);
        Assert.Contains("Linked Documents", review, StringComparison.Ordinal);
        Assert.Contains("Software Defaults", settings, StringComparison.Ordinal);
        Assert.Contains("Notify Emails", settings, StringComparison.Ordinal);
        Assert.Contains("Client Software Settings", software, StringComparison.Ordinal);
        Assert.Contains("Needs Review", documents, StringComparison.Ordinal);
        Assert.Contains("New User", users, StringComparison.Ordinal);
        Assert.Contains("Edit User", users, StringComparison.Ordinal);

        Assert.Contains("Counts and charts for Clients you can access.", dashboard, StringComparison.Ordinal);
        Assert.Contains("Search, assign, and open deeds for your Clients.", documents, StringComparison.Ordinal);
        Assert.Contains("Sign in to your Client workspace", login, StringComparison.Ordinal);
        AssertNoLegacyNames(shell);
        AssertNoLegacyNames(dashboard);
        AssertNoLegacyNames(documents);
        AssertNoLegacyNames(review);
        AssertNoLegacyNames(settings);
        AssertNoLegacyNames(software);
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
        Assert.Contains("params.set(\"from\", filters.from)", helper, StringComparison.Ordinal);
        Assert.Contains("params.set(\"to\", filters.to)", helper, StringComparison.Ordinal);
        Assert.Contains("`/documents?${query}`", helper, StringComparison.Ordinal);

        Assert.Contains("import { documentsPath } from \"../documentsPath\"", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ ...applied })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Queued\", ...applied })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Processing\", ...applied })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Ready\", ...applied })", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: \"Failed\", ...applied })", page, StringComparison.Ordinal);
        Assert.Contains("<StatusMixChart data={mix} {...applied} />", page, StringComparison.Ordinal);

        Assert.Contains("onClick:", charts, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status, clientId, from, to })", charts, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ status: slice.status, clientId, from, to })", charts, StringComparison.Ordinal);
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
        Assert.Contains("<ByUserChart data={byUser} {...applied} />", page, StringComparison.Ordinal);
        Assert.Contains("documentsPath({ assigneeUserId: user.userId, clientId, from, to })", charts, StringComparison.Ordinal);
        Assert.Contains("goUser(data.users[elements[0]?.index ?? -1])", charts, StringComparison.Ordinal);
        Assert.Contains("if (!user?.userId)", charts, StringComparison.Ordinal);
        Assert.Contains("chart-legend-static", charts, StringComparison.Ordinal);

        Assert.Contains("params.get(\"assigneeUserId\")", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"assigneeUserId\", assignee)", documents, StringComparison.Ordinal);
    }

    [Fact]
    public void Volume_filters_documents_by_date_bucket()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var api = Read("src/DeedAi.Api/Controllers/DocumentsController.cs");

        var volumeFn = SliceFunction(charts, "export function VolumeChart");
        Assert.Contains("documentsPath({ from: day, to: day, clientId })", volumeFn, StringComparison.Ordinal);
        Assert.Contains("onClick:", volumeFn, StringComparison.Ordinal);
        Assert.Contains("chart-legend-link", volumeFn, StringComparison.Ordinal);
        Assert.Contains("<VolumeChart data={volume} {...applied} />", page, StringComparison.Ordinal);

        Assert.Contains("params.get(\"from\")", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"from\", from)", documents, StringComparison.Ordinal);
        Assert.Contains("params.get(\"to\")", documents, StringComparison.Ordinal);
        Assert.Contains("params.set(\"to\", to)", documents, StringComparison.Ordinal);
        Assert.Contains("No Documents Match", documents, StringComparison.Ordinal);
        Assert.Contains("DateTimeOffset? from", api, StringComparison.Ordinal);
        Assert.Contains("DocumentFilters.ApplyDates", api, StringComparison.Ordinal);
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
        Assert.False(Directory.EnumerateFiles(migrations, "*TitleCase*").Any());
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
