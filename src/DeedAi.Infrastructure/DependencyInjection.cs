using Azure;
using Azure.AI.DocumentIntelligence;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DeedAi.Domain.Abstractions;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Email;
using DeedAi.Infrastructure.Ocr;
using DeedAi.Infrastructure.Queueing;
using DeedAi.Infrastructure.Software;
using DeedAi.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DeedAi.Infrastructure;

public static class DependencyInjection
{
    public const string DeedsContainer = "deeds";
    public const string OcrQueueName = "ocr-jobs";

    public static IServiceCollection AddDeedAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<OcrOptions>(options =>
        {
            if (int.TryParse(configuration["Ocr:PoisonDequeueCount"], out var poison))
            {
                options.PoisonDequeueCount = poison;
            }

            if (int.TryParse(configuration["Ocr:VisibilityTimeoutSeconds"], out var visibility))
            {
                options.VisibilityTimeoutSeconds = visibility;
            }
        });
        services.AddDbContext<DeedAiDbContext>(options => ConfigureDatabase(options, configuration));
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<OcrProcessor>();
        services.AddSingleton<Health.OcrPipelineSignal>();
        services.AddScoped<Health.RuntimeHealth>();
        services.AddScoped<IOcrNotifier, OcrNotifier>();
        services.AddHttpClient(nameof(SendGridEmailSender));
        services.AddHttpClient(nameof(HttpSoftwareClient));
        AddEmail(services, configuration);
        AddSoftware(services, configuration);
        AddStorage(services, configuration);
        AddQueue(services, configuration);
        AddDocumentIntelligence(services, configuration);
        return services;
    }

    public static void ConfigureDatabase(DbContextOptionsBuilder options, IConfiguration configuration)
    {
        var provider = configuration["Database:Provider"] ?? "Sqlite";
        options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
        {
            var sql = FirstValue(configuration, "SqlConnection", "ConnectionStrings:Sql")
                      ?? throw new InvalidOperationException("SqlConnection (or ConnectionStrings:Sql) is required when Database:Provider=SqlServer.");
            options.UseSqlServer(sql);
            return;
        }

        var sqlite = configuration.GetConnectionString("Sqlite") ?? "Data Source=deedai.db";
        options.UseSqlite(sqlite);
    }

    private static void AddEmail(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SendGridOptions>(options =>
        {
            options.ApiKey = FirstValue(configuration, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey");
            options.FromEmail = FirstValue(configuration, "SendGridFromEmail", "SendGrid:FromEmail") ?? options.FromEmail;
            options.FromName = FirstValue(configuration, "SendGridFromName", "SendGrid:FromName") ?? options.FromName;
        });

        var apiKey = FirstValue(configuration, "SendGridApiKey", "SendGrid:ApiKey", "SendGrid__ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
            return;
        }

        services.AddSingleton<IEmailSender, SendGridEmailSender>();
    }

    private static void AddSoftware(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SoftwareOptions>(options =>
        {
            options.BaseUrl = FirstValue(configuration, "SoftwareBaseUrl", "Software:BaseUrl");
            options.ApiKey = FirstValue(configuration, "SoftwareApiKey", "Software:ApiKey", "Software__ApiKey");
        });

        var mode = configuration["Software:Mode"];
        var baseUrl = FirstValue(configuration, "SoftwareBaseUrl", "Software:BaseUrl");
        if (string.Equals(mode, "Http", StringComparison.OrdinalIgnoreCase)
            || (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)))
        {
            services.AddSingleton<ISoftwareClient, HttpSoftwareClient>();
            return;
        }

        services.AddSingleton<ISoftwareClient, MockSoftwareClient>();
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        var mode = configuration["Storage:Mode"] ?? "Local";
        if (string.Equals(mode, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            var connection = FirstValue(configuration, "StorageConnection", "ConnectionStrings:Storage")
                             ?? throw new InvalidOperationException("StorageConnection (or ConnectionStrings:Storage) is required when Storage:Mode=Azure.");
            var container = configuration["Storage:Container"] ?? DeedsContainer;
            services.AddSingleton(_ => new BlobServiceClient(connection));
            services.AddSingleton<IBlobStorage>(sp =>
                new AzureBlobStorage(sp.GetRequiredService<BlobServiceClient>(), container));
            return;
        }

        if (string.Equals(mode, "InMemory", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IBlobStorage, InMemoryBlobStorage>();
            return;
        }

        var root = configuration["Storage:LocalRoot"] ?? Path.Combine(AppContext.BaseDirectory, "data", "blobs");
        services.AddSingleton<IBlobStorage>(_ => new LocalBlobStorage(root));
    }

    private static void AddQueue(IServiceCollection services, IConfiguration configuration)
    {
        var mode = configuration["Queue:Mode"] ?? configuration["Storage:Mode"] ?? "InMemory";
        if (string.Equals(mode, "Azure", StringComparison.OrdinalIgnoreCase))
        {
            var connection = FirstValue(configuration, "StorageConnection", "ConnectionStrings:Storage")
                             ?? throw new InvalidOperationException("StorageConnection (or ConnectionStrings:Storage) is required when Queue:Mode=Azure.");
            var queueName = configuration["Queue:Name"] ?? OcrQueueName;
            services.AddSingleton(_ => new QueueClient(connection, queueName));
            services.AddSingleton<IOcrJobQueue>(sp =>
                new AzureOcrJobQueue(sp.GetRequiredService<QueueClient>()));
            return;
        }

        services.AddSingleton<IOcrJobQueue, InMemoryOcrJobQueue>();
    }

    private static void AddDocumentIntelligence(IServiceCollection services, IConfiguration configuration)
    {
        // App Setting / Key Vault names. Ignore leftover DocumentIntelligenceEndpoint.
        var endpoint = FirstValue(configuration, "BISDocumentIntelligenceEndpoint", "DocumentIntelligence:Endpoint");
        var key = FirstValue(configuration, "DocumentIntelligenceKey", "DocumentIntelligence:Key");
        if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(key))
        {
            services.AddSingleton<IDocumentIntelligenceClient, MockDocumentIntelligenceClient>();
            return;
        }

        services.AddSingleton(_ => new DocumentIntelligenceClient(new Uri(endpoint), new AzureKeyCredential(key)));
        services.AddSingleton<IDocumentIntelligenceClient, AzureDocumentIntelligenceClient>();
    }

    public static string? FirstValue(IConfiguration configuration, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = configuration[key];
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
