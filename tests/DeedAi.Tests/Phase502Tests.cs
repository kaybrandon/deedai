using DeedAi.Api;

namespace DeedAi.Tests;

public sealed class Phase502Tests
{
    [Fact]
    public void Volume_over_time_title_stays_title_case()
    {
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var pdf = Read("src/DeedAi.Infrastructure/Export/DeedPdfWriter.cs");
        Assert.Contains("<h2>Volume Over Time</h2>", page, StringComparison.Ordinal);
        Assert.DoesNotContain("<h2>Volume over time</h2>", page, StringComparison.Ordinal);
        Assert.Contains("Volume Over Time", pdf, StringComparison.Ordinal);
    }

    [Fact]
    public void Volume_chart_is_weekly_bars_not_line_or_area()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var volumeFn = SliceFunction(charts, "export function VolumeChart");
        Assert.Contains("<Bar", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("<Line", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("fill: true", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("tension:", volumeFn, StringComparison.Ordinal);
        Assert.Contains("maxBarThickness: 44", volumeFn, StringComparison.Ordinal);
        Assert.Contains("from: bucket.from, to: bucket.to, clientId", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("from: day, to: day", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("County", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", volumeFn, StringComparison.Ordinal);
    }

    [Fact]
    public void Volume_chart_plots_one_teal_uploaded_series_not_stacked_status()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var volumeFn = SliceFunction(charts, "export function VolumeChart");
        Assert.Contains("item.key === \"total\"", volumeFn, StringComparison.Ordinal);
        Assert.Contains("#0D8A7F", volumeFn, StringComparison.Ordinal);
        Assert.Contains("backgroundColor: VOLUME_TEAL", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("stacked: true", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("key !== \"total\"", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("plotted.map", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("statuses.filter", volumeFn, StringComparison.Ordinal);
        Assert.DoesNotContain("seriesColor(", volumeFn, StringComparison.Ordinal);
    }

    [Fact]
    public void Week_bar_links_are_keyboard_accessible_and_preserve_client()
    {
        var charts = Read("spa/src/components/DashboardCharts.tsx");
        var css = Read("spa/src/styles.css");
        var documents = Read("spa/src/pages/DocumentsPage.tsx");
        var volumeFn = SliceFunction(charts, "export function VolumeChart");

        Assert.Contains("chart-legend-link", volumeFn, StringComparison.Ordinal);
        Assert.Contains("View documents from ${bucket.from} to ${bucket.to}", volumeFn, StringComparison.Ordinal);
        Assert.Contains("min-height: 44px", css, StringComparison.Ordinal);
        Assert.Contains("min-width: 44px", css, StringComparison.Ordinal);
        Assert.Contains(".chart-legend-link:focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("No Documents Match", documents, StringComparison.Ordinal);
        Assert.Contains("<VolumeChart data={volume} {...applied} />", Read("spa/src/pages/DashboardPage.tsx"), StringComparison.Ordinal);
    }

    [Fact]
    public void Iso_weeks_are_monday_through_sunday()
    {
        Assert.Equal(new DateTime(2024, 8, 12), VolumeWeeks.StartOfIsoWeek(new DateTime(2024, 8, 12)));
        Assert.Equal(new DateTime(2024, 8, 12), VolumeWeeks.StartOfIsoWeek(new DateTime(2024, 8, 15)));
        Assert.Equal(new DateTime(2024, 7, 29), VolumeWeeks.StartOfIsoWeek(new DateTime(2024, 8, 1)));
        Assert.Equal(new DateTime(2024, 8, 18), VolumeWeeks.EndOfIsoWeek(new DateTime(2024, 8, 12)));
        Assert.Equal("2024-08-12", VolumeWeeks.FormatDay(new DateTime(2024, 8, 12)));
        Assert.Equal(104, VolumeWeeks.MaxFilledWeeks);
    }

    [Fact]
    public void Volume_api_uses_iso_week_buckets()
    {
        var controller = Read("src/DeedAi.Api/Controllers/DashboardController.cs");
        var contracts = Read("src/DeedAi.Api/Contracts/DocumentContracts.cs");
        var api = Read("spa/src/api.ts");
        Assert.Contains("VolumeWeekStarts", controller, StringComparison.Ordinal);
        Assert.Contains("VolumeWeeks.StartOfIsoWeek", controller, StringComparison.Ordinal);
        Assert.Contains("DashboardVolumeBucket", contracts, StringComparison.Ordinal);
        Assert.Contains("buckets: DashboardVolumeBucket[]", api, StringComparison.Ordinal);
        Assert.DoesNotContain("AddDays(offset)", controller, StringComparison.Ordinal);
        Assert.Contains("\"Week\"", Read("src/DeedAi.Infrastructure/Export/DeedPdfWriter.cs"), StringComparison.Ordinal);
    }

    [Fact]
    public void Phase502_adds_no_ef_migration_and_stays_off_mask_f()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase502*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*VolumeWeek*").Any());
        var theme = Read("spa/src/theme.ts");
        var css = Read("spa/src/styles.css");
        Assert.Contains("#4F7C8A", theme, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#1E2430", css, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("County", theme, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", theme, StringComparison.Ordinal);
    }

    private static string SliceFunction(string source, string marker)
    {
        var start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing {marker}");
        return source[start..];
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
