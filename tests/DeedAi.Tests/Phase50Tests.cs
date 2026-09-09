using DeedAi.Domain;

namespace DeedAi.Tests;

public sealed class Phase50Tests
{
    [Fact]
    public void Mask_f_tokens_are_wired_in_css_and_theme()
    {
        var css = Read("spa/src/styles.css");
        var theme = Read("spa/src/theme.ts");
        foreach (var token in new[] { "#1E2430", "#F0F2F5", "#FFFFFF", "#1A1F2A", "#5C6573", "#0D8A7F", "#3B82F6", "#D8F0EA", "#0B5F56", "#F5D6D3", "#8B2E28" })
        {
            Assert.Contains(token, css, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(token, theme, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("--sidebar-w: 220px", css, StringComparison.Ordinal);
        Assert.Contains("--topbar-h: 48px", css, StringComparison.Ordinal);
        Assert.Contains("--ribbon-h: 42px", css, StringComparison.Ordinal);
        Assert.Contains("--btn-h: 32px", css, StringComparison.Ordinal);
        Assert.Contains("--search-max: 360px", css, StringComparison.Ordinal);
        Assert.Contains("--detail-w: 680px", css, StringComparison.Ordinal);
        Assert.Contains("--content-max: 1440px", css, StringComparison.Ordinal);
        Assert.Contains("--pad-x: 16px", css, StringComparison.Ordinal);
        Assert.Contains("--pad-y: 16px", css, StringComparison.Ordinal);
        Assert.Contains("gap: 12px", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 220px", css, StringComparison.Ordinal);
        Assert.Contains("max-width: 280px", css, StringComparison.Ordinal);
        Assert.Contains("max-width: var(--form-w)", css, StringComparison.Ordinal);
        Assert.Contains("--form-w: 32rem", css, StringComparison.Ordinal);
        Assert.Contains("--action-h: 44px", css, StringComparison.Ordinal);
        Assert.Contains("chip-failed", css, StringComparison.Ordinal);
        Assert.Contains("chip-queued", css, StringComparison.Ordinal);
        Assert.Contains("--failed-border", css, StringComparison.Ordinal);
        Assert.Contains("--queued-border", css, StringComparison.Ordinal);
        Assert.Contains("--processing-bg", css, StringComparison.Ordinal);
        Assert.Contains("--review-bg", css, StringComparison.Ordinal);
        Assert.Contains(".ocr-ribbon", css, StringComparison.Ordinal);
        Assert.Contains("position: sticky", css, StringComparison.Ordinal);
        Assert.Contains("data-theme=\"mask-f\"", Read("spa/index.html"), StringComparison.Ordinal);
    }

    [Fact]
    public void Soft_dense_shell_keeps_settings_nesting_and_no_top_level_review()
    {
        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.Contains("to=\"/software\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/users\"", shell, StringComparison.Ordinal);
        Assert.Contains("settings-nav", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/settings\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("to=\"/review\"", shell, StringComparison.Ordinal);
        Assert.DoesNotContain(">Review<", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Editor mode", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("County", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/dashboard\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/documents\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/upload\"", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/reports\"", shell, StringComparison.Ordinal);
    }

    [Fact]
    public void Must_ship_ocr_ribbon_and_failed_review_ux()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var upload = Read("spa/src/pages/UploadPage.tsx");
        var review = Read("spa/src/pages/ReviewPage.tsx");
        var ribbon = Read("spa/src/components/OcrRibbon.tsx");
        var theme = Read("spa/src/theme.ts");

        Assert.Contains("<OcrRibbon", documents, StringComparison.Ordinal);
        Assert.Contains("<OcrRibbon", upload, StringComparison.Ordinal);
        Assert.Contains("<OcrRibbon", review, StringComparison.Ordinal);
        foreach (var step in new[] { "Upload", "Queued", "Processing", "Review", "Ready" })
        {
            Assert.Contains(step, ribbon, StringComparison.Ordinal);
            Assert.Contains($"\"{step}\"", theme, StringComparison.Ordinal);
        }

        Assert.Contains("Retry", documents, StringComparison.Ordinal);
        Assert.Contains("Retry Extract", review, StringComparison.Ordinal);
        Assert.Contains("Incomplete", review, StringComparison.Ordinal);
        Assert.Contains("ocr-failed-banner", review, StringComparison.Ordinal);
        Assert.Contains("shownStatus !== \"Ready\"", review, StringComparison.Ordinal);
        Assert.Contains("chip-saved", review, StringComparison.Ordinal);
        Assert.DoesNotContain("chip-ready", review, StringComparison.Ordinal);
        Assert.Contains("pdf-placeholder", review, StringComparison.Ordinal);
        Assert.Contains("pdf-placeholder-mark", review, StringComparison.Ordinal);
        Assert.DoesNotContain("dashed box", review, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dashboard_keeps_counts_donut_volume_and_empty_state()
    {
        var dashboard = Read("spa/src/pages/DashboardPage.tsx");
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        Assert.Contains("CountCard", dashboard, StringComparison.Ordinal);
        Assert.Contains("StatusMixChart", dashboard, StringComparison.Ordinal);
        Assert.Contains("VolumeChart", dashboard, StringComparison.Ordinal);
        Assert.Contains("EmptyState", charts, StringComparison.Ordinal);
        Assert.Contains("maskF", Read("spa/src/charts.ts"), StringComparison.Ordinal);
    }

    [Fact]
    public void Four_roles_client_software_only_and_confirm_sheet()
    {
        var users = Read("spa/src/pages/UsersPage.tsx");
        Assert.Contains("Admin", users, StringComparison.Ordinal);
        Assert.Contains("Editor", users, StringComparison.Ordinal);
        Assert.Contains("Uploader", users, StringComparison.Ordinal);
        Assert.Contains("Viewer", users, StringComparison.Ordinal);
        Assert.Equal(4, AppRoles.All.Length);
        Assert.Contains("ConfirmSheet", Read("spa/src/pages/DocumentsPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("Soft-delete", Read("spa/src/pages/DocumentsPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("confirmLabel", Read("spa/src/components/ConfirmSheet.tsx"), StringComparison.Ordinal);

        foreach (var relative in new[]
        {
            "spa/src/components/AppShell.tsx",
            "spa/src/pages/LoginPage.tsx",
            "spa/src/pages/DashboardPage.tsx",
            "spa/src/pages/DocumentsPage.tsx",
            "spa/src/pages/ReviewPage.tsx",
            "spa/src/pages/UsersPage.tsx",
            "spa/src/pages/SettingsPage.tsx",
            "spa/src/pages/ReportsPage.tsx",
            "spa/src/pages/SoftwarePage.tsx",
            "spa/src/theme.ts"
        })
        {
            var text = Read(relative);
            Assert.DoesNotContain("County", text, StringComparison.Ordinal);
            Assert.DoesNotContain("CAMA", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Phase50_adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase50*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase5*").Any());
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
