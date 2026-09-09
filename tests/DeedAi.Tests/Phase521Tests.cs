using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Tests;

public sealed class Phase521Tests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;

    public Phase521Tests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Review_uses_locked_field_names_and_mask_f_chrome()
    {
        var review = Read("spa/src/pages/ReviewPage.tsx");
        var css = Read("spa/src/styles.css");
        var api = Read("spa/src/api.ts");
        var contracts = Read("src/DeedAi.Api/Contracts/DocumentContracts.cs");

        foreach (var name in new[] { "documentNumber", "volume", "page", "deedType", "pid", "mailingStreet", "mailingCity", "mailingState", "mailingZip", "grantors", "grantees" })
        {
            Assert.Contains(name, review, StringComparison.Ordinal);
            Assert.Contains(name, api, StringComparison.Ordinal);
            Assert.Contains(char.ToUpperInvariant(name[0]) + name[1..].Replace("[]", ""), contracts, StringComparison.Ordinal);
        }

        Assert.Contains("review-queue", review, StringComparison.Ordinal);
        Assert.Contains("review-grid", review, StringComparison.Ordinal);
        Assert.Contains("PDF Preview", review, StringComparison.Ordinal);
        Assert.Contains("Extracted Fields", review, StringComparison.Ordinal);
        Assert.Contains("Document Number", review, StringComparison.Ordinal);
        Assert.Contains("Deed Type", review, StringComparison.Ordinal);
        Assert.Contains("Mailing Street", review, StringComparison.Ordinal);
        Assert.Contains("Mailing ZIP", review, StringComparison.Ordinal);
        Assert.Contains("Software Search", review, StringComparison.Ordinal);
        Assert.Contains("Push to Software", review, StringComparison.Ordinal);
        Assert.Contains("ConfirmSheet", review, StringComparison.Ordinal);
        Assert.Contains("Remove Grantor?", review, StringComparison.Ordinal);
        Assert.Contains("Retry Extract", review, StringComparison.Ordinal);
        Assert.Contains("shownStatus !== \"Ready\"", review, StringComparison.Ordinal);
        Assert.Contains("pdf-placeholder", review, StringComparison.Ordinal);
        Assert.Contains("softwareLookupKeys", review, StringComparison.Ordinal);

        Assert.DoesNotContain("docNo", review, StringComparison.Ordinal);
        Assert.DoesNotContain("\"vol\"", review, StringComparison.Ordinal);
        Assert.DoesNotContain("County", review, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", review, StringComparison.Ordinal);
        Assert.DoesNotContain("Search queue", review, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("minmax(200px, 0.55fr) minmax(0, 1.2fr) minmax(280px, 0.9fr)", css, StringComparison.Ordinal);
        Assert.Contains(".party-row input", css, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--action-h)", css, StringComparison.Ordinal);
        Assert.Contains(".software-result", css, StringComparison.Ordinal);
        Assert.Contains(".review-queue-item", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Party_names_keep_order_and_reject_empty_rows()
    {
        Assert.Equal(new[] { "Pat Example", "Jane Example" }, PartyNames.Normalize(["Pat Example", "Jane Example"]));
        Assert.Equal("Pat Example", PartyNames.Primary(["Pat Example", "Jane Example"]));
        Assert.Null(PartyNames.EmptyRowsMessage(null, "grantor"));
        Assert.Null(PartyNames.EmptyRowsMessage([""], "grantor"));
        Assert.Null(PartyNames.EmptyRowsMessage(["Jane Example"], "grantor"));
        Assert.Equal("Fill or remove empty grantor rows.", PartyNames.EmptyRowsMessage(["Jane Example", ""], "grantor"));
        Assert.Equal("Fill or remove empty grantee rows.", PartyNames.EmptyRowsMessage(["", "Acme Holdings LLC"], "grantee"));
    }

    [Fact]
    public async Task Review_save_persists_locked_fields_and_party_order()
    {
        var client = await Authed(DatabaseSeeder.EditorEmail);
        var id = await FirstReadyId(client);

        var empty = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Jane Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"250000","parcelId":"12-345-678","client":"Acme","notes":"empty row","isDraft":false,"grantors":["Jane Example",""],"grantees":["Acme Holdings LLC"]}"""));
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Contains("empty grantor rows", await empty.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        var save = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Pat Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"250000","parcelId":"99-111-222","client":"Acme","notes":"depth","isDraft":false,"deedType":"Warranty Deed","documentNumber":"2024-0999","volume":"200","page":"44","pid":"99-111-222","mailingStreet":"200 Oak Ave","mailingCity":"Peoria","mailingState":"IL","mailingZip":"61602","grantors":["Pat Example","Sam Example"],"grantees":["Acme Holdings LLC","North Holdings"]}"""));
        save.EnsureSuccessStatusCode();

        var detail = await client.GetAsync($"/api/documents/{id}");
        detail.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Equal("2024-0999", root.GetProperty("documentNumber").GetString());
        Assert.Equal("200", root.GetProperty("volume").GetString());
        Assert.Equal("44", root.GetProperty("page").GetString());
        Assert.Equal("Warranty Deed", root.GetProperty("deedType").GetString());
        Assert.Equal("99-111-222", root.GetProperty("pid").GetString());
        Assert.Equal("200 Oak Ave", root.GetProperty("mailingStreet").GetString());
        Assert.Equal("Peoria", root.GetProperty("mailingCity").GetString());
        Assert.Equal("IL", root.GetProperty("mailingState").GetString());
        Assert.Equal("61602", root.GetProperty("mailingZip").GetString());
        Assert.Equal(new[] { "Pat Example", "Sam Example" }, root.GetProperty("grantors").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Equal(new[] { "Acme Holdings LLC", "North Holdings" }, root.GetProperty("grantees").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.False(root.TryGetProperty("docNo", out _));
        Assert.False(root.TryGetProperty("vol", out _));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var document = await db.Documents.Include(x => x.Fields).FirstAsync(x => x.Id == id);
        Assert.Equal(new[] { "Pat Example", "Sam Example" }, document.Grantors);
        Assert.Equal("Pat Example", document.Fields?.Grantor);
        Assert.Equal("99-111-222", document.Pid);
        Assert.Equal("99-111-222", document.Fields?.ParcelId);
    }

    [Fact]
    public async Task Legacy_grantor_payload_still_saves_and_software_lookup_is_client_scoped()
    {
        var client = await Authed(DatabaseSeeder.EditorEmail);
        var id = await FirstId(client, "Scan_bad");
        var save = await client.PutAsync($"/api/documents/{id}/fields", TestAppFactory.Json(
            """{"grantor":"Lee Example","grantee":"Acme Holdings LLC","instrumentDate":"2024-08-12","consideration":"1","parcelId":"55-000-001","client":"Acme","notes":"legacy","isDraft":true}"""));
        save.EnsureSuccessStatusCode();

        var detail = await client.GetAsync($"/api/documents/{id}");
        using var json = JsonDocument.Parse(await detail.Content.ReadAsStringAsync());
        Assert.Equal("Lee Example", json.RootElement.GetProperty("grantors")[0].GetString());
        Assert.Equal("55-000-001", json.RootElement.GetProperty("pid").GetString());

        var lookup = await client.GetAsync("/api/software/lookup?parcelId=12-345-678&client=Acme");
        lookup.EnsureSuccessStatusCode();
        using var found = JsonDocument.Parse(await lookup.Content.ReadAsStringAsync());
        Assert.Equal("12-345-678", found.RootElement.GetProperty("parcelId").GetString());
        Assert.Equal("100 Main St", found.RootElement.GetProperty("extra").GetProperty("mailingStreet").GetString());
        Assert.Equal("Springfield", found.RootElement.GetProperty("extra").GetProperty("mailingCity").GetString());
        Assert.DoesNotContain("CAMA", await lookup.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Viewer_cannot_push_from_review_endpoint()
    {
        var editor = await Authed(DatabaseSeeder.EditorEmail);
        var id = await FirstReadyId(editor);
        var viewer = await Authed(DatabaseSeeder.ViewerEmail);
        var response = await viewer.PostAsync($"/api/documents/{id}/software/push", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public void Phase521_adds_no_ef_migration()
    {
        var migrations = Path.Combine(RepoRoot(), "src", "DeedAi.Infrastructure", "Data", "Migrations");
        Assert.False(Directory.EnumerateFiles(migrations, "*Phase521*").Any());
        Assert.False(Directory.EnumerateFiles(migrations, "*ReviewDepth*").Any());
        var names = Directory.EnumerateFiles(migrations).Select(Path.GetFileName).ToList();
        Assert.Contains("20260909220000_DocumentListFields.cs", names);
        Assert.Contains("20260909230000_DocumentListFieldNullDefaults.cs", names);
    }

    private async Task<HttpClient> Authed(string email)
    {
        var client = _factory.CreateJsonClient();
        var token = await _factory.LoginAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Guid> FirstReadyId(HttpClient client) => await FirstId(client, "Deed_2024_0812");

    private static async Task<Guid> FirstId(HttpClient client, string search)
    {
        var response = await client.GetAsync($"/api/documents?search={Uri.EscapeDataString(search)}");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0, search);
        return json.RootElement[0].GetProperty("id").GetGuid();
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
