using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;

namespace DeedAi.Tests;

public sealed class DashboardChartTests : IClassFixture<TestAppFactory>
{
    private static readonly string[] ChartRoutes =
    [
        "/api/dashboard/charts/status-mix",
        "/api/dashboard/charts/by-user",
        "/api/dashboard/charts/volume"
    ];

    private readonly TestAppFactory _factory;

    public DashboardChartTests(TestAppFactory factory) => _factory = factory;

    [Theory]
    [InlineData("/api/dashboard/counts")]
    [InlineData("/api/dashboard/charts/status-mix")]
    [InlineData("/api/dashboard/charts/by-user")]
    [InlineData("/api/dashboard/charts/volume")]
    public async Task Chart_routes_require_auth_and_do_not_fall_through_to_html(string path)
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            HandleCookies = false,
            AllowAutoRedirect = false
        });
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<!DOCTYPE", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("text/html", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("viewer@bisconsultants.com")]
    [InlineData("uploader@bisconsultants.com")]
    [InlineData("editor@bisconsultants.com")]
    [InlineData("admin@bisconsultants.com")]
    public async Task Authenticated_roles_receive_json_chart_series(string email)
    {
        var client = await Authed(email);
        foreach (var path in ChartRoutes)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertJson(response);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.True(json.RootElement.TryGetProperty("series", out var series));
            Assert.Equal(JsonValueKind.Array, series.ValueKind);
        }

        var counts = await client.GetAsync("/api/dashboard/counts");
        Assert.Equal(HttpStatusCode.OK, counts.StatusCode);
        AssertJson(counts);
        using var countJson = JsonDocument.Parse(await counts.Content.ReadAsStringAsync());
        Assert.True(countJson.RootElement.GetProperty("uploaded").GetInt32() >= 4);
        Assert.Equal(1, countJson.RootElement.GetProperty("queued").GetInt32());
        Assert.Equal(1, countJson.RootElement.GetProperty("processing").GetInt32());
        Assert.Equal(1, countJson.RootElement.GetProperty("ready").GetInt32());
        Assert.Equal(1, countJson.RootElement.GetProperty("failed").GetInt32());
    }

    [Fact]
    public async Task Status_mix_returns_one_slice_per_document_status()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync("/api/dashboard/charts/status-mix");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(4, json.RootElement.GetProperty("total").GetInt32());
        var series = json.RootElement.GetProperty("series");
        Assert.Equal(DocumentStatuses.All.Length, series.GetArrayLength());
        foreach (var status in DocumentStatuses.All)
        {
            var slice = series.EnumerateArray().Single(x => x.GetProperty("status").GetString() == status);
            Assert.Equal(1, slice.GetProperty("count").GetInt32());
            Assert.False(string.IsNullOrWhiteSpace(slice.GetProperty("label").GetString()));
        }

        AssertNoSecretsOrLegacyNames(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task By_user_groups_status_counts_by_assignee_display_name()
    {
        var client = await Authed("editor@bisconsultants.com");
        var response = await client.GetAsync("/api/dashboard/charts/by-user");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        var users = json.RootElement.GetProperty("users").EnumerateArray().ToList();
        Assert.Equal(labels.Count, users.Count);
        Assert.Contains("Alex", labels);
        Assert.Contains("Sam", labels);
        Assert.Contains("Unassigned", labels);
        Assert.Equal("Unassigned", labels[^1]);

        var alex = users.Single(x => x.GetProperty("displayName").GetString() == "Alex");
        Assert.Equal(DatabaseSeeder.EditorId, alex.GetProperty("userId").GetGuid());
        Assert.False(alex.TryGetProperty("email", out _));

        var series = json.RootElement.GetProperty("series");
        Assert.Equal(DocumentStatuses.All.Length, series.GetArrayLength());
        foreach (var item in series.EnumerateArray())
        {
            Assert.Equal(labels.Count, item.GetProperty("data").GetArrayLength());
        }

        Assert.Equal(1, CountAt(json, "Ready", "Alex"));
        Assert.Equal(1, CountAt(json, "Processing", "Sam"));
        Assert.Equal(1, CountAt(json, "Failed", "Unassigned"));
        Assert.Equal(1, CountAt(json, "Queued", "Unassigned"));
        AssertNoSecretsOrLegacyNames(body);
    }

    [Fact]
    public async Task Volume_returns_daily_total_and_status_breakdown()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync("/api/dashboard/charts/volume");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(new[] { "2024-08-12", "2024-08-13", "2024-08-14", "2024-08-15" }, labels);

        var series = json.RootElement.GetProperty("series");
        Assert.Equal(1 + DocumentStatuses.All.Length, series.GetArrayLength());
        var total = series.EnumerateArray().Single(x => x.GetProperty("key").GetString() == "total");
        Assert.Equal("Uploaded", total.GetProperty("label").GetString());
        Assert.Equal(new[] { 1, 1, 1, 1 }, total.GetProperty("data").EnumerateArray().Select(x => x.GetInt32()).ToArray());
        Assert.Equal(1, VolumeCount(json, DocumentStatuses.Ready, "2024-08-12"));
        Assert.Equal(1, VolumeCount(json, DocumentStatuses.Processing, "2024-08-13"));
        Assert.Equal(1, VolumeCount(json, DocumentStatuses.Failed, "2024-08-14"));
        Assert.Equal(1, VolumeCount(json, DocumentStatuses.Queued, "2024-08-15"));
        AssertNoSecretsOrLegacyNames(body);
    }

    [Fact]
    public async Task Date_range_filters_counts_and_all_chart_series()
    {
        var client = await Authed("viewer@bisconsultants.com");
        const string query = "?from=2024-08-12T00:00:00Z&to=2024-08-12T23:59:59Z";

        var counts = await client.GetAsync($"/api/dashboard/counts{query}");
        Assert.Equal(HttpStatusCode.OK, counts.StatusCode);
        using (var json = JsonDocument.Parse(await counts.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, json.RootElement.GetProperty("uploaded").GetInt32());
            Assert.Equal(1, json.RootElement.GetProperty("ready").GetInt32());
            Assert.Equal(0, json.RootElement.GetProperty("queued").GetInt32());
        }

        var mix = await client.GetAsync($"/api/dashboard/charts/status-mix{query}");
        AssertJson(mix);
        using (var json = JsonDocument.Parse(await mix.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
            Assert.Equal(1, SliceCount(json, DocumentStatuses.Ready));
            Assert.Equal(0, SliceCount(json, DocumentStatuses.Failed));
        }

        var byUser = await client.GetAsync($"/api/dashboard/charts/by-user{query}");
        AssertJson(byUser);
        using (var json = JsonDocument.Parse(await byUser.Content.ReadAsStringAsync()))
        {
            var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Equal(new[] { "Alex" }, labels);
            Assert.Equal(1, CountAt(json, DocumentStatuses.Ready, "Alex"));
        }

        var volume = await client.GetAsync($"/api/dashboard/charts/volume{query}");
        AssertJson(volume);
        using (var json = JsonDocument.Parse(await volume.Content.ReadAsStringAsync()))
        {
            var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
            Assert.Equal(new[] { "2024-08-12" }, labels);
            Assert.Equal(1, VolumeCount(json, "total", "2024-08-12"));
        }
    }

    [Fact]
    public async Task Empty_range_returns_empty_series_not_error()
    {
        var client = await Authed("viewer@bisconsultants.com");
        const string query = "?from=1990-01-01T00:00:00Z&to=1990-01-02T00:00:00Z";

        foreach (var path in new[]
        {
            $"/api/dashboard/counts{query}",
            $"/api/dashboard/charts/status-mix{query}",
            $"/api/dashboard/charts/by-user{query}",
            $"/api/dashboard/charts/volume{query}"
        })
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertJson(response);
        }

        using var mix = JsonDocument.Parse(await (await client.GetAsync($"/api/dashboard/charts/status-mix{query}")).Content.ReadAsStringAsync());
        Assert.Equal(0, mix.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(0, mix.RootElement.GetProperty("series").GetArrayLength());

        using var byUser = JsonDocument.Parse(await (await client.GetAsync($"/api/dashboard/charts/by-user{query}")).Content.ReadAsStringAsync());
        Assert.Equal(0, byUser.RootElement.GetProperty("labels").GetArrayLength());
        Assert.Equal(0, byUser.RootElement.GetProperty("users").GetArrayLength());
        Assert.Equal(0, byUser.RootElement.GetProperty("series").GetArrayLength());

        using var volume = JsonDocument.Parse(await (await client.GetAsync($"/api/dashboard/charts/volume{query}")).Content.ReadAsStringAsync());
        Assert.Equal(0, volume.RootElement.GetProperty("labels").GetArrayLength());
        Assert.Equal(0, volume.RootElement.GetProperty("series").GetArrayLength());

        using var counts = JsonDocument.Parse(await (await client.GetAsync($"/api/dashboard/counts{query}")).Content.ReadAsStringAsync());
        Assert.Equal(0, counts.RootElement.GetProperty("uploaded").GetInt32());
    }

    [Fact]
    public async Task Client_filter_scopes_chart_series()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var response = await client.GetAsync($"/api/dashboard/charts/status-mix?clientId={DatabaseSeeder.AcmeId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, SliceCount(json, DocumentStatuses.Ready));
        Assert.Equal(1, SliceCount(json, DocumentStatuses.Failed));
        Assert.Equal(0, SliceCount(json, DocumentStatuses.Queued));
    }

    [Fact]
    public async Task Authenticated_html_accept_still_returns_json_not_spa_html()
    {
        var client = await Authed("viewer@bisconsultants.com");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
        var response = await client.GetAsync("/api/dashboard/charts/status-mix");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(response);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", body, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("{", body.TrimStart());
    }

    [Fact]
    public async Task Documents_list_honors_from_to_the_same_way_as_dashboard()
    {
        var client = await Authed("viewer@bisconsultants.com");
        var day = await client.GetAsync("/api/documents?status=Ready&from=2024-08-12T00:00:00Z&to=2024-08-12T23:59:59Z");
        Assert.Equal(HttpStatusCode.OK, day.StatusCode);
        using (var json = JsonDocument.Parse(await day.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, json.RootElement.GetArrayLength());
            Assert.Equal("Ready", json.RootElement[0].GetProperty("status").GetString());
        }

        var miss = await client.GetAsync("/api/documents?status=Ready&from=2024-08-13T00:00:00Z&to=2024-08-13T23:59:59Z");
        Assert.Equal(HttpStatusCode.OK, miss.StatusCode);
        using (var json = JsonDocument.Parse(await miss.Content.ReadAsStringAsync()))
        {
            Assert.Equal(0, json.RootElement.GetArrayLength());
        }
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static void AssertJson(HttpResponseMessage response)
    {
        var media = response.Content.Headers.ContentType?.MediaType ?? "";
        Assert.Contains("json", media, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("text/html", media, StringComparison.OrdinalIgnoreCase);
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

    private static int SliceCount(JsonDocument json, string status) =>
        json.RootElement.GetProperty("series").EnumerateArray()
            .Single(x => x.GetProperty("status").GetString() == status)
            .GetProperty("count").GetInt32();

    private static int CountAt(JsonDocument json, string status, string displayName)
    {
        var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        var index = labels.IndexOf(displayName);
        Assert.True(index >= 0, $"Missing label {displayName}");
        var series = json.RootElement.GetProperty("series").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == status);
        return series.GetProperty("data")[index].GetInt32();
    }

    private static int VolumeCount(JsonDocument json, string key, string day)
    {
        var labels = json.RootElement.GetProperty("labels").EnumerateArray().Select(x => x.GetString()).ToList();
        var index = labels.IndexOf(day);
        Assert.True(index >= 0, $"Missing day {day}");
        var series = json.RootElement.GetProperty("series").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == key);
        return series.GetProperty("data")[index].GetInt32();
    }
}
