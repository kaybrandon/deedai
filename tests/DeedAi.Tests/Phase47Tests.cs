using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase47Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase47Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Export_requires_auth_and_does_not_fall_through_to_html()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var response = await client.GetAsync("/api/dashboard/export");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%PDF", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("admin@bisconsultants.com")]
    public async Task Authenticated_roles_can_export_filtered_dashboard_pdf(string email)
    {
        var client = await Authed(email);
        var response = await client.GetAsync("/api/dashboard/export?from=2024-08-12T00:00:00Z&to=2024-08-15T23:59:59Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 8);
        Assert.Equal("%PDF"u8.ToArray(), bytes[..4].ToArray());
        var text = Encoding.ASCII.GetString(bytes);
        Assert.Contains("Deed AI", text);
        Assert.Contains("Counts", text);
        Assert.Contains("Status Mix", text);
        Assert.Contains("By Users", text);
        Assert.Contains("Volume Over Time", text);
        Assert.Contains("Page 1 of", text);
        Assert.Contains("Generated ", text);
        AssertNoSecretsOrLegacyNames(text);
    }

    [Fact]
    public void Dashboard_filename_keeps_local_end_of_day_calendar_date()
    {
        var eastern = TimeSpan.FromHours(-4);
        var from = new DateTimeOffset(2024, 8, 14, 0, 0, 0, eastern);
        var to = new DateTimeOffset(2024, 8, 14, 23, 59, 59, eastern);

        Assert.Equal(new DateTime(2024, 8, 15, 3, 59, 59), to.UtcDateTime);
        Assert.Equal("2024-08-15", to.UtcDateTime.ToString("yyyy-MM-dd"));
        Assert.Equal("deedai-dashboard-2024-08-14-to-2024-08-14.pdf", DeedPdfWriter.DashboardFileName(from, to));
        Assert.Equal("2024-08-14", DeedPdfWriter.FilterCalendarDate(to));
    }

    [Fact]
    public void Dashboard_filename_uses_picker_dates_when_query_instant_already_utc_shifted()
    {
        var from = new DateTimeOffset(2024, 8, 14, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 8, 15, 3, 59, 59, TimeSpan.Zero);

        Assert.Equal(
            "deedai-dashboard-2024-08-14-to-2024-08-15.pdf",
            DeedPdfWriter.DashboardFileName(from, to));
        Assert.Equal(
            "deedai-dashboard-2024-08-14-to-2024-08-14.pdf",
            DeedPdfWriter.DashboardFileName(from, to, "2024-08-14", "2024-08-14"));
    }

    [Fact]
    public async Task Export_filename_includes_date_range()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var from = new DateTimeOffset(2024, 8, 12, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 8, 12, 23, 59, 59, TimeSpan.Zero);
        var response = await client.GetAsync(
            $"/api/dashboard/export?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = DeedPdfWriter.DashboardFileName(from, to);
        Assert.Equal("deedai-dashboard-2024-08-12-to-2024-08-12.pdf", expected);
        var disposition = response.Content.Headers.ContentDisposition?.FileName
            ?? response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ToString();
        Assert.Contains("2024-08-12", disposition, StringComparison.Ordinal);
        Assert.Contains("deedai-dashboard", disposition, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("2024-08-13", disposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_filename_does_not_shift_local_end_of_day_to_next_utc_day()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var from = new DateTimeOffset(2024, 8, 12, 0, 0, 0, TimeSpan.FromHours(-4));
        var to = new DateTimeOffset(2024, 8, 12, 23, 59, 59, TimeSpan.FromHours(-4));
        Assert.Equal(new DateTime(2024, 8, 13, 3, 59, 59), to.UtcDateTime);

        var response = await client.GetAsync(
            $"/api/dashboard/export?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expected = DeedPdfWriter.DashboardFileName(from, to);
        Assert.Equal("deedai-dashboard-2024-08-12-to-2024-08-12.pdf", expected);
        var disposition = response.Content.Headers.ContentDisposition?.FileName
            ?? response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ToString();
        Assert.Contains("deedai-dashboard-2024-08-12-to-2024-08-12.pdf", disposition, StringComparison.Ordinal);
        Assert.DoesNotContain("2024-08-13", disposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Export_filename_uses_picker_calendar_dates_over_utc_shifted_to()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var from = new DateTimeOffset(2024, 8, 12, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2024, 8, 13, 3, 59, 59, TimeSpan.Zero);
        var response = await client.GetAsync(
            $"/api/dashboard/export?from={Uri.EscapeDataString(from.ToString("o"))}&to={Uri.EscapeDataString(to.ToString("o"))}&fromDate=2024-08-12&toDate=2024-08-12");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var disposition = response.Content.Headers.ContentDisposition?.FileName
            ?? response.Content.Headers.ContentDisposition?.FileNameStar
            ?? response.Content.Headers.ToString();
        Assert.Contains("deedai-dashboard-2024-08-12-to-2024-08-12.pdf", disposition, StringComparison.Ordinal);
        Assert.DoesNotContain("2024-08-13", disposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Date_filter_matches_counts_api_and_is_not_an_unfiltered_dump()
    {
        var client = await Authed("viewer@bisconsultants.com");
        const string query = "?from=2024-08-12T00:00:00Z&to=2024-08-12T23:59:59Z";

        using var counts = JsonDocument.Parse(await (await client.GetAsync($"/api/dashboard/counts{query}")).Content.ReadAsStringAsync());
        Assert.Equal(1, counts.RootElement.GetProperty("uploaded").GetInt32());
        Assert.Equal(1, counts.RootElement.GetProperty("ready").GetInt32());
        Assert.Equal(0, counts.RootElement.GetProperty("queued").GetInt32());

        var response = await client.GetAsync($"/api/dashboard/export{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Uploaded: 1", text);
        Assert.Contains("Ready: 1", text);
        Assert.Contains("Queued: 0", text);
        Assert.Contains("2024-08-12", text);
        Assert.DoesNotContain("2024-08-15", text);
        Assert.Contains("Alex", text);
        Assert.DoesNotContain("Sam", text);
    }

    [Fact]
    public async Task Client_filter_scopes_export_and_uses_client_title()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var query = $"?from=2024-08-01T00:00:00Z&to=2024-08-31T23:59:59Z&clientId={DatabaseSeeder.AcmeId}";
        var response = await client.GetAsync($"/api/dashboard/export{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Acme Deed AI", text);
        Assert.Contains("Client: Acme", text);
        Assert.Contains("Uploaded: 2", text);
        Assert.DoesNotContain("Northside", text);
        Assert.DoesNotContain("All clients", text);
    }

    [Fact]
    public async Task All_clients_export_keeps_deed_ai_title()
    {
        var client = await Authed("admin@bisconsultants.com");
        var response = await client.GetAsync("/api/dashboard/export?from=2024-08-01T00:00:00Z&to=2024-08-31T23:59:59Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Deed AI", text);
        Assert.DoesNotContain("Acme Deed AI", text);
        Assert.DoesNotContain("Northside Deed AI", text);
        Assert.Contains("Client: All clients", text);
        Assert.Contains("Uploaded: 4", text);
        Assert.Contains("Alex", text);
        Assert.Contains("Sam", text);
    }

    [Fact]
    public async Task Empty_filter_is_not_a_blank_pdf()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync("/api/dashboard/export?from=1990-01-01T00:00:00Z&to=1990-01-02T00:00:00Z");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("blank PDF is not returned", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("date range or Client", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("%PDF", body);
    }

    [Fact]
    public async Task Client_access_hides_other_clients_from_dashboard_pdf()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var access = db.UserClientAccess.Where(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId);
            db.UserClientAccess.RemoveRange(access);
            await db.SaveChangesAsync();
        }

        try
        {
            var client = await Authed("viewer@bisconsultants.com");
            var response = await client.GetAsync("/api/dashboard/export?from=2024-08-01T00:00:00Z&to=2024-08-31T23:59:59Z");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var text = Encoding.ASCII.GetString(await response.Content.ReadAsByteArrayAsync());
            Assert.DoesNotContain("Acme", text);
            Assert.Contains("Northside Deed AI", text);
            Assert.Contains("Uploaded: 2", text);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            if (!await db.UserClientAccess.AnyAsync(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId))
            {
                db.UserClientAccess.Add(new DeedAi.Domain.Entities.UserClientAccess
                {
                    UserId = DatabaseSeeder.ViewerId,
                    ClientId = DatabaseSeeder.AcmeId
                });
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public async Task Inaccessible_client_filter_is_empty_not_an_unfiltered_dump()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            var access = db.UserClientAccess.Where(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId);
            db.UserClientAccess.RemoveRange(access);
            await db.SaveChangesAsync();
        }

        try
        {
            var client = await Authed("viewer@bisconsultants.com");
            var response = await client.GetAsync(
                $"/api/dashboard/export?from=2024-08-01T00:00:00Z&to=2024-08-31T23:59:59Z&clientId={DatabaseSeeder.AcmeId}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            Assert.Contains("blank PDF is not returned", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("%PDF", body);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
            if (!await db.UserClientAccess.AnyAsync(x => x.UserId == DatabaseSeeder.ViewerId && x.ClientId == DatabaseSeeder.AcmeId))
            {
                db.UserClientAccess.Add(new DeedAi.Domain.Entities.UserClientAccess
                {
                    UserId = DatabaseSeeder.ViewerId,
                    ClientId = DatabaseSeeder.AcmeId
                });
                await db.SaveChangesAsync();
            }
        }
    }

    [Fact]
    public void Spa_print_and_export_honor_applied_filters_and_hide_shell()
    {
        var page = Read("spa/src/pages/DashboardPage.tsx");
        var css = Read("spa/src/styles.css");
        var api = Read("spa/src/api.ts");

        Assert.Contains("window.print()", page);
        Assert.Contains("Export PDF", page);
        Assert.Contains(">Print<", page);
        Assert.Contains("dashboard-export-actions", page);
        Assert.Contains("endpoints.exportDashboard(exportParams", page);
        Assert.Contains("buildQuery", page);
        Assert.Contains("buildExportQuery", page);
        Assert.Contains("fromDate", page);
        Assert.Contains("toDate", page);
        Assert.Contains("applied", page);
        Assert.Contains("Deed AI", page);
        Assert.DoesNotContain("County", page);
        Assert.DoesNotContain("CAMA", page);

        Assert.Contains("exportDashboard", api);
        Assert.Contains("/api/dashboard/export", api);
        Assert.Contains("fileNameFromDisposition", api);
        Assert.Contains(@"filename\*=", api);
        Assert.DoesNotContain(@"filename=""?([^""]+)""?", api);

        var printAt = css.LastIndexOf("@media print", StringComparison.Ordinal);
        Assert.True(printAt >= 0, "Dashboard print CSS is missing.");
        var printCss = css[printAt..];
        Assert.Contains(".sidebar", printCss);
        Assert.Contains(".topbar", printCss);
        Assert.Contains(".site-footer", printCss);
        Assert.Contains(".no-print", printCss);
        Assert.Contains(".print-only", printCss);
        Assert.Contains("display: none !important", printCss);
        Assert.Contains("dashboard-export-actions button", css);
        Assert.Contains("min-height: 44px", css);
        Assert.Contains("min-width: 44px", css);
        Assert.DoesNotContain("County", css);
        Assert.DoesNotContain("CAMA", css);
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertNoSecretsOrLegacyNames(string body)
    {
        Assert.DoesNotContain("@bisconsultants.com", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionString", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Jwt", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("County", body, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", body, StringComparison.Ordinal);
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
