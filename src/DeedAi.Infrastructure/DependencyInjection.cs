using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using DeedAi.Domain;
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
        services.AddSingleton<Health.OcrHealthSignal>();
        services.AddScoped<Health.OcrHealthRecorder>();
        services.AddScoped<Health.RuntimeHealth>();
        services.AddScoped<IOcrNotifier, OcrNotifier>();
        services.AddHttpClient(nameof(SendGridEmailSender));
        services.AddHttpClient(nameof(HttpSoftwareClient));
        AddEmail(services, configuration);
        services.AddScoped<EmailOutbound>();
        services.AddScoped<IEmailOutbound>(sp => sp.GetRequiredService<EmailOutbound>());
        AddSoftware(services, configuration);
        AddStorage(services, configuration);
        AddQueue(services, configuration);
        AddAzureOpenAI(services, configuration);
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
        services.Configure<EmailOptions>(options => EmailKv.Bind(options, configuration));
        services.AddSingleton<SendGridEmailSender>();
        services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<SendGridEmailSender>());
        services.AddSingleton<SmtpEmailSender>();
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

    private static void AddAzureOpenAI(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient(AzureOpenAIExtractClient.HttpClientName);
        var options = BindAzureOpenAI(configuration);
        services.Configure<AzureOpenAIOptions>(_ =>
        {
            _.Endpoint = options.Endpoint;
            _.Key = options.Key;
            _.Deployment = options.Deployment;
            _.Model = options.Model;
            _.ApiVersion = options.ApiVersion;
            _.Mode = options.Mode;
            _.AllowPricierModel = options.AllowPricierModel;
        });

        if (string.Equals(options.Mode, "Mock", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAiExtractClient, MockAiExtractClient>();
            return;
        }

        if (HasRealValue(options.Endpoint) && HasRealValue(options.Key) && HasRealValue(options.Deployment))
        {
            services.AddSingleton<IAiExtractClient, AzureOpenAIExtractClient>();
            return;
        }

        services.AddSingleton<IAiExtractClient, FailClosedAiExtractClient>();
    }

    internal static AzureOpenAIOptions BindAzureOpenAI(IConfiguration configuration)
    {
        var model = FirstValue(configuration, "AzureOpenAIModel", "AzureOpenAI:Model") ?? AiExtractModels.Default;
        return new AzureOpenAIOptions
        {
            Endpoint = FirstValue(configuration, "AzureOpenAIEndpoint", "BISAzureOpenAIEndpoint", "AzureOpenAI:Endpoint"),
            Key = FirstValue(configuration, "AzureOpenAIKey", "AzureOpenAI:Key"),
            Deployment = FirstValue(configuration, "AzureOpenAIDeployment", "AzureOpenAI:Deployment"),
            Model = string.IsNullOrWhiteSpace(model) ? AiExtractModels.Default : model,
            ApiVersion = FirstValue(configuration, "AzureOpenAIApiVersion", "AzureOpenAI:ApiVersion") ?? "2024-10-21",
            Mode = configuration["AzureOpenAI:Mode"] ?? "",
            AllowPricierModel = string.Equals(configuration["AzureOpenAI:AllowPricierModel"], "true", StringComparison.OrdinalIgnoreCase)
        };
    }

    internal static bool HasRealValue(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && !value.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);

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
