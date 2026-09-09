using Microsoft.OpenApi.Models;

namespace DeedAi.Api.Swagger;

public static class SwaggerExtensions
{
    public const string RoutePrefix = "swagger";
    public const string DocumentName = "v1";
    public const string BearerSchemeId = "Bearer";
    public const string ConfigKey = "Swagger:Enabled";

    /// <summary>
    /// Swagger UI's default Authorize control is ~34px because
    /// <c>.swagger-ui .btn.authorize { display: inline; line-height: 1 }</c>
    /// (padding 5px + 20px lock icon + 5px + 2px borders). <c>min-height</c>
    /// does not apply to inline boxes, so a HeadContent-only min-height
    /// override is ignored. Force a flex box and a 44px tap target to match
    /// Copy Bearer and the rest of the Admin Settings UI.
    /// </summary>
    public const string AuthorizeHitTargetCss =
        """
        <style id="deedai-swagger-authorize">
        .swagger-ui .btn.authorize,
        .swagger-ui .auth-wrapper .authorize,
        .swagger-ui .scheme-container .btn.authorize,
        .swagger-ui .modal-ux .btn.modal-btn,
        .swagger-ui .modal-ux .btn.modal-btn.authorize,
        .swagger-ui .auth-btn-wrapper .btn,
        .swagger-ui .auth-btn-wrapper .btn.modal-btn,
        .swagger-ui .auth-btn-wrapper .btn.modal-btn.authorize,
        .swagger-ui .btn.modal-btn.authorize,
        .swagger-ui .btn-done {
          display: inline-flex !important;
          align-items: center !important;
          justify-content: center !important;
          box-sizing: border-box !important;
          min-height: 44px !important;
          min-width: 44px !important;
          height: auto !important;
          padding: 10px 16px !important;
          line-height: 1.2 !important;
        }
        .swagger-ui .btn.authorize span,
        .swagger-ui .auth-wrapper .authorize span {
          float: none !important;
          display: inline !important;
          padding: 0 8px 0 0 !important;
          line-height: inherit !important;
        }
        </style>
        """;

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
            }

            await next();
        });

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint($"/{RoutePrefix}/{DocumentName}/swagger.json", "Deed AI API v1");
            options.RoutePrefix = RoutePrefix;
            options.EnablePersistAuthorization();
            options.HeadContent = AuthorizeHitTargetCss;
        });
        return app;
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
