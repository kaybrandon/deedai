namespace DeedAi.Tests;

public sealed class Phase51Tests
{
    [Fact]
    public void Mask_f_tokens_and_dark_rail_are_sitewide()
    {
        var css = Read("spa/src/styles.css");
        var theme = Read("spa/src/theme.ts");
        var html = Read("spa/index.html");

        foreach (var token in new[] { "#1E2430", "#F0F2F5", "#FFFFFF", "#1A1F2A", "#5C6573", "#0D8A7F", "#3B82F6", "#D8F0EA", "#0B5F56", "#F5D6D3", "#8B2E28" })
        {
            Assert.Contains(token, css, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(token, theme, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("data-theme=\"mask-f\"", html, StringComparison.Ordinal);
        Assert.Contains("background: var(--rail)", css, StringComparison.Ordinal);
        Assert.Contains("--sidebar-w: 220px", css, StringComparison.Ordinal);
        Assert.Contains("--topbar-h: 48px", css, StringComparison.Ordinal);
        Assert.Contains("--ribbon-h: 42px", css, StringComparison.Ordinal);
        Assert.Contains("--action-h: 44px", css, StringComparison.Ordinal);
        Assert.Contains("--btn-h: 32px", css, StringComparison.Ordinal);
        Assert.Contains("--search-max: 360px", css, StringComparison.Ordinal);
        Assert.Contains("--filter-w: 240px", css, StringComparison.Ordinal);
        Assert.Contains("--form-w: 32rem", css, StringComparison.Ordinal);
        Assert.Contains("--detail-w: 680px", css, StringComparison.Ordinal);
        Assert.Contains("--content-max: 1440px", css, StringComparison.Ordinal);
        Assert.Contains("--radius-sm: 5px", css, StringComparison.Ordinal);
        Assert.Contains("--radius: 8px", css, StringComparison.Ordinal);
        Assert.Contains("--space-1: 4px", css, StringComparison.Ordinal);
        Assert.Contains("font-family: Inter", css, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme=\"mask-a\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Documents_has_exactly_one_filter_row_search()
    {
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var shell = Read("spa/src/components/AppShell.tsx");
        var css = Read("spa/src/styles.css");

        Assert.Contains("className=\"search-field\"", documents, StringComparison.Ordinal);
        Assert.Equal(1, Count(documents, "placeholder=\"Search deeds\""));
        Assert.Equal(1, Count(documents, "className=\"search-field\""));
        Assert.DoesNotContain("placeholder=\"Search", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("type=\"search\"", shell, StringComparison.Ordinal);
        Assert.Contains("max-width: var(--search-max)", css, StringComparison.Ordinal);
        Assert.Contains("--search-max: 360px", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Settings_nest_highlights_one_child_and_labels_software()
    {
        var shell = Read("spa/src/components/AppShell.tsx");
        Assert.Contains("Software", shell, StringComparison.Ordinal);
        Assert.Contains("to=\"/software\"", shell, StringComparison.Ordinal);
        Assert.Contains("Workspace", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("Systems", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("County", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("nav-group-toggle${onSettingsSection", shell, StringComparison.Ordinal);
        Assert.DoesNotContain("is-active", shell, StringComparison.Ordinal);
        Assert.Contains("childClass(onWorkspace)", shell, StringComparison.Ordinal);
        Assert.Contains("childClass(onSoftware)", shell, StringComparison.Ordinal);
        Assert.Contains("childClass(onUsers)", shell, StringComparison.Ordinal);
        Assert.Contains("childClass(onApiDocs)", shell, StringComparison.Ordinal);
        Assert.Contains("<h1>Workspace</h1>", Read("spa/src/pages/SettingsPage.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void Field_help_has_no_question_pills_and_uses_native_title()
    {
        var spa = Path.Combine(RepoRoot(), "spa", "src");
        foreach (var file in Directory.EnumerateFiles(spa, "*.tsx", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain("field-help-btn", text, StringComparison.Ordinal);
            Assert.DoesNotContain("field-help-pop", text, StringComparison.Ordinal);
            Assert.DoesNotContain(">?</button>", text, StringComparison.Ordinal);
        }

        var help = Read("spa/src/components/FieldHelp.tsx");
        Assert.Contains("title={text}", help, StringComparison.Ordinal);
        Assert.Contains("aria-describedby", help, StringComparison.Ordinal);
        Assert.DoesNotContain("?", help, StringComparison.Ordinal);

        var css = Read("spa/src/styles.css");
        Assert.DoesNotContain("field-help-btn", css, StringComparison.Ordinal);
        Assert.DoesNotContain("field-help-pop", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Software_and_settings_hug_dense_detail_width()
    {
        var css = Read("spa/src/styles.css");
        Assert.Contains(".page-detail { max-width: var(--detail-w); }", css, StringComparison.Ordinal);
        Assert.Contains("--detail-w: 680px", css, StringComparison.Ordinal);
        Assert.Contains("--form-w: 32rem", css, StringComparison.Ordinal);
        Assert.Contains("className=\"page page-detail\"", Read("spa/src/pages/SettingsPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("className=\"page page-detail\"", Read("spa/src/pages/SoftwarePage.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void Volume_chart_is_single_teal_series_and_review_is_three_col()
    {
        var volume = SliceFunction(Read("spa/src/components/DashboardCharts.tsx"), "export function VolumeChart");
        Assert.Contains("maskF.teal", volume, StringComparison.Ordinal);
        Assert.Contains("#0D8A7F", Read("spa/src/theme.ts"), StringComparison.Ordinal);
        Assert.DoesNotContain("statuses.map", volume, StringComparison.Ordinal);
        Assert.Contains("review-grid", Read("spa/src/pages/ReviewPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("review-side", Read("spa/src/pages/ReviewPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("minmax(0, 1.2fr) minmax(280px, 0.85fr) minmax(240px, 0.7fr)", Read("spa/src/styles.css"), StringComparison.Ordinal);
        Assert.DoesNotContain("Search queue", Read("spa/src/pages/ReviewPage.tsx"), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Search queue", Read("spa/src/components/AppShell.tsx"), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ConfirmSheet", Read("spa/src/pages/UsersPage.tsx"), StringComparison.Ordinal);
        Assert.Contains("Disable this user?", Read("spa/src/pages/UsersPage.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void Phase51_adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase51*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase5.1*").Any());
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    private static string SliceFunction(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, marker);
        var next = source.IndexOf("\nexport function ", start + marker.Length, StringComparison.Ordinal);
        return next < 0 ? source[start..] : source[start..next];
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
