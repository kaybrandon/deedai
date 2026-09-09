using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using DeedAi.Domain;
using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DeedAi.Tests;

public sealed class DocumentListFieldNullDefaultsTests : IClassFixture<TestAppFactory>
{
    private const string PriorMigrationId = "20260909220000_DocumentListFields";
    private readonly TestAppFactory _factory;

    public DocumentListFieldNullDefaultsTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public void Migration_is_designer_first_after_document_list_fields()
    {
        Assert.True(string.CompareOrdinal(PriorMigrationId, DocumentListFieldNullDefaults.MigrationId) < 0);

        var type = typeof(DocumentListFieldNullDefaults);
        Assert.Equal(DocumentListFieldNullDefaults.MigrationId, type.GetCustomAttribute<MigrationAttribute>()?.Id);
        Assert.Equal(typeof(DeedAiDbContext), type.GetCustomAttribute<DbContextAttribute>()?.ContextType);
        Assert.NotNull(type.GetMethod("BuildTargetModel", BindingFlags.Instance | BindingFlags.NonPublic));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var discovered = db.GetService<IMigrationsAssembly>().Migrations.Keys.ToList();
        Assert.Contains(DocumentListFieldNullDefaults.MigrationId, discovered);
        Assert.Equal(
            typeof(DocumentListFieldNullDefaults),
            db.GetService<IMigrationsAssembly>().Migrations[DocumentListFieldNullDefaults.MigrationId].AsType());

        var designer = Read("src/DeedAi.Infrastructure/Data/Migrations/20260909230000_DocumentListFieldNullDefaults.Designer.cs");
        Assert.Contains($"[Migration(\"{DocumentListFieldNullDefaults.MigrationId}\")]", designer, StringComparison.Ordinal);
        Assert.Contains("[DbContext(typeof(DeedAiDbContext))]", designer, StringComparison.Ordinal);
        Assert.Contains("BuildTargetModel", designer, StringComparison.Ordinal);
        foreach (var name in DocumentListFieldNullDefaults.Columns)
        {
            Assert.Contains($"\"{name}\"", designer, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("County", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", designer, StringComparison.Ordinal);
        Assert.DoesNotContain("DocNo", designer, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_backfills_nulls_and_adds_sql_server_defaults()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.SqlServer");
        InvokeUp(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        foreach (var column in DocumentListFieldNullDefaults.Columns)
        {
            Assert.Contains($"UPDATE Documents SET [{column}] = '' WHERE [{column}] IS NULL;", sql, StringComparison.Ordinal);
            Assert.Contains($"DF_Documents_{column}", sql, StringComparison.Ordinal);
            Assert.Contains($"DEFAULT N'' FOR [{column}]", sql, StringComparison.Ordinal);
            Assert.Contains($"COL_LENGTH(N'dbo.Documents', N'{column}')", sql, StringComparison.Ordinal);
        }

        Assert.Contains("NOT EXISTS", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("County", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DocNo", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Up_on_sqlite_only_backfills_nulls()
    {
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        InvokeUp(builder);

        var sql = string.Join('\n', builder.Operations.OfType<SqlOperation>().Select(x => x.Sql));
        foreach (var column in DocumentListFieldNullDefaults.Columns)
        {
            Assert.Contains($"UPDATE Documents SET [{column}] = '' WHERE [{column}] IS NULL;", sql, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("DF_Documents_", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE dbo.Documents ADD CONSTRAINT", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Locked_field_names_stay_on_document_and_lists()
    {
        var entity = Read("src/DeedAi.Domain/Entities/Document.cs");
        var mapping = Read("src/DeedAi.Api/DocumentListMapping.cs");
        var filters = Read("src/DeedAi.Api/DocumentFilters.cs");
        var helper = Read("spa/src/documentsTable.ts");
        foreach (var name in DocumentListFieldNullDefaults.Columns)
        {
            Assert.Contains(name, entity, StringComparison.Ordinal);
            Assert.Contains(name, mapping, StringComparison.Ordinal);
            Assert.Contains(name, filters, StringComparison.Ordinal);
        }

        foreach (var name in new[] { "documentNumber", "volume", "page", "deedType", "pid", "mailingStreet", "mailingCity", "mailingState", "mailingZip", "grantors", "grantees" })
        {
            Assert.Contains(name, helper, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("docNo", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("DocNo", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("County", entity, StringComparison.Ordinal);
        Assert.DoesNotContain("CAMA", entity, StringComparison.Ordinal);
    }

    [Fact]
    public void Party_names_and_ef_conversion_tolerate_null_and_empty()
    {
        Assert.Empty(PartyNames.FromJson(null));
        Assert.Empty(PartyNames.FromJson(""));
        Assert.Empty(PartyNames.FromJson("   "));
        Assert.Empty(PartyNames.FromJson("[]"));
        Assert.Equal("[]", PartyNames.ToJson(null));
        Assert.Equal("[]", PartyNames.ToJson([]));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var entity = db.Model.FindEntityType(typeof(Document));
        Assert.NotNull(entity);

        foreach (var name in DocumentListFieldNullDefaults.Columns)
        {
            var property = entity.FindProperty(name);
            Assert.NotNull(property);
            Assert.True(property.IsNullable, $"{name} must stay nullable so SQL NULL can materialize");
        }

        foreach (var name in new[] { nameof(Document.Grantors), nameof(Document.Grantees) })
        {
            var property = entity.FindProperty(name)!;
            var converter = property.GetValueConverter();
            Assert.NotNull(converter);
            Assert.Equal(typeof(string), Nullable.GetUnderlyingType(converter.ProviderClrType) ?? converter.ProviderClrType);
            Assert.True(
                converter.ProviderClrType != typeof(string) || property.IsNullable,
                $"{name} store type must not force non-null GetString");

            var fromNull = converter.ConvertFromProvider(null);
            Assert.True(fromNull is null || !((IEnumerable<string>)fromNull).Any());
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(converter.ConvertFromProvider("")!));
            Assert.Empty(Assert.IsAssignableFrom<IEnumerable<string>>(converter.ConvertFromProvider("[]")!));
        }
    }

    [Fact]
    public async Task Materializing_documents_with_null_list_fields_does_not_throw()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-null-materialize-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await new DatabaseSeeder(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeedPassword"] = DatabaseSeeder.SeedPassword,
            ["Seed:DemoDocuments"] = "true",
            ["Database:Provider"] = "Sqlite"
        }).Build(), NullLogger<DatabaseSeeder>.Instance).SeedAsync(CancellationToken.None);

        await NullListFieldsAsync(db);
        db.ChangeTracker.Clear();

        var thrown = await Record.ExceptionAsync(() => db.Documents.IgnoreQueryFilters().ToListAsync());
        Assert.Null(thrown);

        var documents = await db.Documents.IgnoreQueryFilters().ToListAsync();
        Assert.NotEmpty(documents);
        foreach (var document in documents)
        {
            Assert.Empty(PartyNames.Normalize(document.Grantors));
            Assert.Empty(PartyNames.Normalize(document.Grantees));
        }

        File.Delete(dbPath);
    }

    [Fact]
    public async Task Review_seed_and_documents_api_survive_null_list_fields()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-null-fields-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using (var db = new DeedAiDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeedPassword"] = DatabaseSeeder.SeedPassword,
                ["Seed:DemoDocuments"] = "true",
                ["Database:Provider"] = "Sqlite"
            }).Build();

            await new DatabaseSeeder(db, config, NullLogger<DatabaseSeeder>.Instance)
                .SeedAsync(CancellationToken.None);

            await NullListFieldsAsync(db);
            db.ChangeTracker.Clear();

            var exception = await Record.ExceptionAsync(() =>
                new DatabaseSeeder(db, config, NullLogger<DatabaseSeeder>.Instance)
                    .SeedAsync(CancellationToken.None));
            Assert.Null(exception);

            db.ChangeTracker.Clear();
            var documents = await db.Documents.IgnoreQueryFilters()
                .Include(x => x.Flags)
                .ToListAsync();
            Assert.NotEmpty(documents);
            Assert.Contains(documents, x => ReviewWorkflow.IsNeedsReview(x.ReviewStatus)
                                           || x.Flags.Any(f => f.FlagDefinitionId == ReviewWorkflow.NeedsReviewFlagId));
            foreach (var document in documents)
            {
                Assert.Empty(PartyNames.Normalize(document.Grantors));
                Assert.Empty(PartyNames.Normalize(document.Grantees));
            }
        }

        using var factory = TestAppFactory.Create(dbPath);
        var client = factory.CreateJsonClient();
        var token = await factory.LoginAsync(client, DatabaseSeeder.EditorEmail);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.GetAsync("/api/documents");
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetArrayLength() > 0);
        var row = json.RootElement[0];
        foreach (var name in new[] { "documentNumber", "volume", "page", "deedType", "pid", "mailingStreet", "mailingCity", "mailingState", "mailingZip", "grantors", "grantees" })
        {
            Assert.True(row.TryGetProperty(name, out _), name);
        }

        Assert.Equal(JsonValueKind.Array, row.GetProperty("grantors").ValueKind);
        Assert.Equal(JsonValueKind.Array, row.GetProperty("grantees").ValueKind);
        File.Delete(dbPath);
    }

    [Fact]
    public async Task Backfill_sql_clears_null_list_fields()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"deedai-null-backfill-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<DeedAiDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        await using var db = new DeedAiDbContext(options);
        await db.Database.EnsureCreatedAsync();
        await new DatabaseSeeder(db, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminSeedPassword"] = DatabaseSeeder.SeedPassword,
            ["Seed:DemoDocuments"] = "true",
            ["Database:Provider"] = "Sqlite"
        }).Build(), NullLogger<DatabaseSeeder>.Instance).SeedAsync(CancellationToken.None);

        await NullListFieldsAsync(db);
        var builder = new MigrationBuilder("Microsoft.EntityFrameworkCore.Sqlite");
        InvokeUp(builder);
        foreach (var sql in builder.Operations.OfType<SqlOperation>().Select(x => x.Sql))
        {
            await db.Database.ExecuteSqlRawAsync(sql);
        }

        db.ChangeTracker.Clear();
        var documents = await db.Documents.IgnoreQueryFilters().ToListAsync();
        Assert.NotEmpty(documents);
        foreach (var document in documents)
        {
            Assert.Equal("", document.DocumentNumber);
            Assert.Equal("", document.Volume);
            Assert.Equal("", document.Page);
            Assert.Equal("", document.DeedType);
            Assert.Equal("", document.Pid);
            Assert.Equal("", document.MailingStreet);
            Assert.Equal("", document.MailingCity);
            Assert.Equal("", document.MailingState);
            Assert.Equal("", document.MailingZip);
            Assert.NotNull(document.Grantors);
            Assert.NotNull(document.Grantees);
            Assert.Empty(document.Grantors);
            Assert.Empty(document.Grantees);
        }

        File.Delete(dbPath);
    }

    private static async Task NullListFieldsAsync(DeedAiDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE Documents SET
                DocumentNumber = NULL, Volume = NULL, Page = NULL, DeedType = NULL, Pid = NULL,
                MailingStreet = NULL, MailingCity = NULL, MailingState = NULL, MailingZip = NULL,
                Grantors = NULL, Grantees = NULL
            """);
    }

    private static void InvokeUp(MigrationBuilder builder)
    {
        var up = typeof(DocumentListFieldNullDefaults).GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(up);
        up.Invoke(new DocumentListFieldNullDefaults(), [builder]);
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
