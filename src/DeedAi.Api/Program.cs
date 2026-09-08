using DeedAi.Api.Auth;
using DeedAi.Domain;
using DeedAi.Infrastructure;
using DeedAi.Infrastructure.Data;
using DeedAi.Infrastructure.Ocr;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52 * 1024 * 1024;
});
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52 * 1024 * 1024;
});

builder.Services.Configure<JwtSettings>(options =>
{
    builder.Configuration.GetSection(JwtSettings.SectionName).Bind(options);
    options.Key = DeedAi.Infrastructure.DependencyInjection.FirstValue(
                      builder.Configuration, "JwtSigningKey", "Jwt:Key")
                  ?? options.Key;
});
builder.Services.AddDeedAiInfrastructure(builder.Configuration);
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, ConfigureJwtBearerOptions>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, RoleDeniedHandler>();
builder.Services.AddControllers();

var jwtKey = DeedAi.Infrastructure.DependencyInjection.FirstValue(builder.Configuration, "JwtSigningKey", "Jwt:Key");
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters. See .env.example.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(RolePolicies.CanUpload, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Editor, AppRoles.Uploader))
    .AddPolicy(RolePolicies.CanEdit, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Editor))
    .AddPolicy(RolePolicies.CanAdmin, policy => policy.RequireRole(AppRoles.Admin));

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("DeedAi", policy =>
    {
        // Never pair AllowAnyOrigin with AllowCredentials.
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
        else
        {
            policy.SetIsOriginAllowed(_ => false);
        }
    });
});

if (builder.Configuration.GetValue("Ocr:RunInProcess", false))
{
    builder.Services.AddHostedService<InProcessOcrWorker>();
}

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

app.UseCors("DeedAi");
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Run();

public sealed class InProcessOcrWorker(
    IServiceScopeFactory scopes,
    DeedAi.Domain.Abstractions.IOcrJobQueue queue,
    Microsoft.Extensions.Options.IOptions<OcrOptions> options,
    ILogger<InProcessOcrWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("In-process OCR worker using queue long-poll (not a 1s busy loop).");
        var visibility = TimeSpan.FromSeconds(Math.Max(30, options.Value.VisibilityTimeoutSeconds));
        while (!stoppingToken.IsCancellationRequested)
        {
            var delivery = await queue.ReceiveAsync(visibility, stoppingToken);
            if (delivery is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopes.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OcrProcessor>();
                await processor.ProcessAsync(delivery, stoppingToken);
                await queue.DeleteAsync(delivery, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "In-process OCR failed for {DocumentId}", delivery.Job.DocumentId);
            }
        }
    }
}

public partial class Program;
