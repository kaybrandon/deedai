using System.Text.Encodings.Web;
using System.Text.Json;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Queueing;
using DeedAi.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace DeedAi.Tests;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    private string _dbPath = Path.Combine(Path.GetTempPath(), $"deedai-tests-{Guid.NewGuid():N}.db");
    private bool _ownsDb = true;
    private Dictionary<string, string?> _extra = new();

    public TestAppFactory()
    {
    }

    public static TestAppFactory Create(string? dbPath = null, IReadOnlyDictionary<string, string?>? extraSettings = null)
    {
        var factory = new TestAppFactory();
        if (dbPath is not null)
        {
            factory._ownsDb = false;
            factory._dbPath = dbPath;
        }

        if (extraSettings is not null)
        {
            factory._extra = new Dictionary<string, string?>(extraSettings);
        }

        return factory;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_extra.GetValueOrDefault("ASPNETCORE_ENVIRONMENT") ?? "Development");
        builder.UseSetting("Database:Provider", "Sqlite");
        builder.UseSetting("ConnectionStrings:Sqlite", $"Data Source={_dbPath}");
        builder.UseSetting("Storage:Mode", "InMemory");
        builder.UseSetting("Queue:Mode", "InMemory");
        builder.UseSetting("Ocr:RunInProcess", "false");
        builder.UseSetting("Ocr:PoisonDequeueCount", "5");
        builder.UseSetting("Jwt:Key", "TEST_ONLY_JWT_KEY_MUST_BE_32_CHARS_MIN");
        builder.UseSetting("Jwt:Issuer", "deedai");
        builder.UseSetting("Jwt:Audience", "deedai-spa");
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:5173");
        builder.UseSetting("DocumentIntelligence:Endpoint", "");
        builder.UseSetting("DocumentIntelligence:Key", "");
        builder.UseSetting("AzureOpenAI:Mode", "Mock");
        builder.UseSetting("SendGridApiKey", "sg-test-configured-0000");
        foreach (var pair in _extra)
        {
            builder.UseSetting(pair.Key, pair.Value ?? "");
        }
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var values = new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Sqlite",
                ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}",
                ["Storage:Mode"] = "InMemory",
                ["Queue:Mode"] = "InMemory",
                ["Ocr:RunInProcess"] = "false",
                ["Jwt:Key"] = "TEST_ONLY_JWT_KEY_MUST_BE_32_CHARS_MIN",
                ["Jwt:Issuer"] = "deedai",
                ["Jwt:Audience"] = "deedai-spa",
                ["Cors:AllowedOrigins:0"] = "http://localhost:5173",
                ["SendGridApiKey"] = "sg-test-configured-0000",
                ["AzureOpenAI:Mode"] = "Mock"
            };
            foreach (var pair in _extra)
            {
                values[pair.Key] = pair.Value;
            }
            config.AddInMemoryCollection(values);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<RecordingEmailSender>());
            services.AddSingleton<InMemoryBlobStorage>();
            services.AddSingleton<IBlobStorage>(sp => sp.GetRequiredService<InMemoryBlobStorage>());
            services.AddSingleton<InMemoryOcrJobQueue>();
            services.AddSingleton<IOcrJobQueue>(sp => sp.GetRequiredService<InMemoryOcrJobQueue>());
        });
    }

    public HttpClient CreateJsonClient()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        return client;
    }

    public async Task<string> LoginAsync(HttpClient client, string email, string password = DatabaseSeeder.SeedPassword)
    {
        var response = await client.PostAsync("/api/auth/login", Json("{\"email\":\"" + email + "\",\"password\":\"" + password + "\"}"));
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString()
               ?? throw new InvalidOperationException("Login response missing token.");
    }

    public static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    public T GetRequiredService<T>() where T : notnull =>
        Services.GetRequiredService<T>();

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_ownsDb && File.Exists(_dbPath))
        {
            try { File.Delete(_dbPath); } catch (IOException) { }
        }
    }

    public static string Encode(string value) => UrlEncoder.Default.Encode(value);
}
