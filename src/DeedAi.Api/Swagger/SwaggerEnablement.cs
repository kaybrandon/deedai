using DeedAi.Domain.Entities;
using DeedAi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeedAi.Api.Swagger;

public interface ISwaggerEnablement
{
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}

/// <summary>
/// Swagger UI on/off from the <c>AppSettings</c> table. Missing row falls back
/// to the non-Prod <c>Swagger:Enabled</c> seed (off by default, including
/// Production). Writes persist across process restarts.
/// </summary>
public sealed class SwaggerEnablement(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    IHostEnvironment environment) : ISwaggerEnablement
{
    public const string SettingKey = AppSetting.SwaggerEnabledKey;

    private const int Unknown = -1;
    private int _cached = Unknown;

    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var cached = Volatile.Read(ref _cached);
        if (cached != Unknown)
        {
            return cached == 1;
        }

        var enabled = await ReadAsync(cancellationToken);
        Volatile.Write(ref _cached, enabled ? 1 : 0);
        return enabled;
    }

    public async Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);
        if (row is null)
        {
            db.AppSettings.Add(new AppSetting
            {
                Key = SettingKey,
                Value = Format(enabled),
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Value = Format(enabled);
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        Volatile.Write(ref _cached, enabled ? 1 : 0);
    }

    private async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeedAiDbContext>();
        var row = await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == SettingKey, cancellationToken);
        if (row is not null && bool.TryParse(row.Value, out var stored))
        {
            return stored;
        }

        return SwaggerExtensions.EnvironmentSeed(configuration, environment);
    }

    private static string Format(bool enabled) => enabled ? bool.TrueString : bool.FalseString;
}
