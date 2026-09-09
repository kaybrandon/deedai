using Microsoft.OpenApi.Models;

namespace DeedAi.Api.Swagger;

public static class SwaggerExtensions
{
    public const string RoutePrefix = "swagger";
    public const string DocumentName = "v1";
    public const string BearerSchemeId = "Bearer";
    public const string ConfigKey = "Swagger:Enabled";

    /// <summary>
    /// Stronger Authorize CSS. Prefer <see cref="AuthorizeHitTargetHead"/>
    /// (stylesheet link only). Phase 4.2.3 serves CSS/JS as files and a
    /// custom index that loads the script once, after <c>index.js</c>.
    /// Do not inject the JS via HeadContent or InjectJavascript — that
    /// races Swagger and can leave <c>__deedAiMeasureAuthorize</c> undefined.
    /// </summary>
    public static string AuthorizeHitTargetCss => SwaggerAuthorizeHitTarget.StyleTag;

    public static string AuthorizeHitTargetScript => SwaggerAuthorizeHitTarget.ScriptTag;

    public static string AuthorizeHitTargetHead => SwaggerAuthorizeHitTarget.HeadContent;

    /// <summary>
    /// Default when the database has no Swagger row. Always off unless
    /// <c>Swagger:Enabled</c> / <c>Swagger__Enabled</c> is true <em>and</em>
    /// the host is not Production. Runtime enablement is the admin DB setting.
    /// </summary>
    public static bool EnvironmentSeed(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        if (environment.IsProduction())
        {
            return false;
        }

        return configuration.GetValue<bool?>(ConfigKey) == true;
    }

    public static IServiceCollection AddDeedAiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Deed AI API",
                Version = DocumentName,
                Description = "Use POST /api/auth/login, then Authorize with the returned JWT (Bearer). Enabling Swagger does not open anonymous API access."
            });

            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Description = "JWT from POST /api/auth/login. Paste the token only — Swagger sends Authorization: Bearer {token}.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = BearerSchemeId
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });
        return services;
    }

    /// <summary>
    /// Serves Swagger UI at /swagger when enabled. When disabled, /swagger and
    /// /swagger/* return 404 so the SPA fallback cannot claim those paths.
    /// Pass <paramref name="enabled"/> for a fixed pipeline (tests). Omit it to
    /// read <see cref="ISwaggerEnablement"/> on each Swagger request.
    /// </summary>
    public static IApplicationBuilder UseDeedAiSwagger(this IApplicationBuilder app, bool? enabled = null)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/" + RoutePrefix))
            {
                var on = enabled ?? await context.RequestServices
                    .GetRequiredService<ISwaggerEnablement>()
                    .IsEnabledAsync(context.RequestAborted);
                if (!on)
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (await SwaggerAuthorizeHitTarget.TryServeAssetAsync(context, context.RequestAborted))
                {
                    return;
                }

                // Phase 4.2.4: Swashbuckle serves index.html / index.js with
                // max-age=604800. Override on start so a 7-day stale shell
                // cannot keep an old pin while authorize.js is no-store.
                if (IsSwaggerShellAsset(context.Request.Path))
                {
                    ApplyNoStoreOnStarting(context);
                }
            }

            await next();
        });

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/{RoutePrefix}/{DocumentName}/swagger.json", "Deed AI API v1");
            options.RoutePrefix = RoutePrefix;
            options.EnablePersistAuthorization();
            options.HeadContent = AuthorizeHitTargetHead;
            options.IndexStream = SwaggerAuthorizeHitTarget.OpenIndexHtml;
        });
        return app;
    }

    /// <summary>
    /// Swashbuckle UI shell that browsers otherwise cache for 7 days.
    /// Authorize JS/CSS are already no-store; the HTML/index.js must match.
    /// </summary>
    public static bool IsSwaggerShellAsset(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.EndsWith("/index.html", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("/index.js", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sets <c>Cache-Control: no-store</c> when the response starts so
    /// Swashbuckle / static-file headers cannot win after <c>next()</c>.
    /// Same no-store contract as authorize.js / authorize.css.
    /// </summary>
    public static void ApplyNoStoreOnStarting(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Response.OnStarting(static state =>
        {
            var response = (HttpResponse)state!;
            response.Headers.CacheControl = "no-store";
            return Task.CompletedTask;
        }, context.Response);
    }

    /// <summary>
    /// Layout A SPA shell. /api and /swagger never fall through to index.html.
    /// </summary>
    public static void MapDeedAiSpaFallback(this WebApplication app)
    {
        var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var indexHtml = Path.Combine(webRoot, "index.html");
        if (!File.Exists(indexHtml))
        {
            return;
        }

        app.MapFallback(async context =>
        {
            if (context.Request.Path.StartsWithSegments("/api")
                || context.Request.Path.StartsWithSegments("/" + RoutePrefix))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            context.Response.ContentType = "text/html; charset=utf-8";
            await context.Response.SendFileAsync(indexHtml);
        });
    }
}
