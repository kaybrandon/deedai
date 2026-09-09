using System.Text;

namespace DeedAi.Api.Swagger;

/// <summary>
/// Phase 4.2.2 Authorize tap-target. #17 CSS and #18 HeadContent runtime still
/// lost on live Azure: Swashbuckle 9 puts <c>HeadContent</c> / InjectJavascript
/// in <c>&lt;head&gt;</c> before <c>swagger-ui-bundle.js</c>, and Swagger React
/// re-applies <c>display:inline</c> after paint (height ignored → ~34px top bar,
/// ~30px modal). Custom <c>index.html</c> loads our JS last; assets are served
/// at <c>/swagger/deedai-swagger-authorize.{js,css}</c> before Swashbuckle;
/// the script polls every 250ms and re-pins inline <c>!important</c>.
/// </summary>
public static class SwaggerAuthorizeHitTarget
{
    public const int MinPx = 44;
    public const string RuntimeVersion = "4.2.2";
    public const string StyleId = "deedai-swagger-authorize";
    public const string ScriptId = "deedai-swagger-authorize-runtime";
    public const string MeasureFunction = "__deedAiMeasureAuthorize";
    public const string JsFileName = "deedai-swagger-authorize.js";
    public const string CssFileName = "deedai-swagger-authorize.css";
    public const string JsUrl = "/" + SwaggerExtensions.RoutePrefix + "/" + JsFileName + "?v=" + RuntimeVersion;
    public const string CssUrl = "/" + SwaggerExtensions.RoutePrefix + "/" + CssFileName + "?v=" + RuntimeVersion;

    public static string CssRules { get; } = ReadSibling(CssFileName);
    public static string JavaScript { get; } = ReadSibling(JsFileName);

    public static string StyleTag =>
        "<style id=\"" + StyleId + "\">" + CssRules + "</style>";

    public static string ScriptTag =>
        "<script id=\"" + ScriptId + "\">" + JavaScript + "</script>";

    public static string StylesheetLink =>
        "<link rel=\"stylesheet\" type=\"text/css\" href=\"" + CssUrl + "\" id=\"deedai-swagger-authorize-href\" />";

    public static string ScriptSrcTag =>
        "<script src=\"" + JsUrl + "\" charset=\"utf-8\" id=\"deedai-swagger-authorize-src\"></script>";

    /// <summary>
    /// Inline fallback in <c>&lt;head&gt;</c> if the body-end file 404s.
    /// The JS guard <c>__deedAiAuthorizeRuntime</c> prevents double init.
    /// </summary>
    public static string HeadContent => StylesheetLink + StyleTag + ScriptTag;

    /// <summary>
    /// Swashbuckle 9 default index with our CSS in head and JS after
    /// <c>index.js</c> so <c>SwaggerUIBundle</c> already exists before we wrap it.
    /// Placeholders must match Swashbuckle.AspNetCore.SwaggerUI 9.0.6.
    /// </summary>
    public static string IndexHtml { get; } =
        """
        <!-- HTML for static distribution bundle build -->
        <!-- deedai authorize hit runtime v4.2.2 — JS last, after index.js -->
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <title>%(DocumentTitle)</title>
            <link rel="stylesheet" type="text/css" href="%(StylesPath)">
            <link rel="stylesheet" type="text/css" href="./index.css">
            <link rel="icon" type="image/png" href="./favicon-32x32.png" sizes="32x32" />
            <link rel="icon" type="image/png" href="./favicon-16x16.png" sizes="16x16" />
            %(HeadContent)
        </head>
        <body>
            <div id="swagger-ui"></div>
            <script src="%(ScriptBundlePath)" charset="utf-8"></script>
            <script src="%(ScriptPresetsPath)" charset="utf-8"></script>
            <script src="index.js" charset="utf-8"></script>
            <script src="/swagger/deedai-swagger-authorize.js?v=4.2.2" charset="utf-8" id="deedai-swagger-authorize-src-last"></script>
        </body>
        </html>
        """;

    public static Stream OpenIndexHtml() =>
        new MemoryStream(Encoding.UTF8.GetBytes(IndexHtml));

    /// <summary>
    /// Serves the JS/CSS before <c>UseSwaggerUI</c> so Swashbuckle cannot 404
    /// <c>/swagger/deedai-swagger-authorize.*</c>. Caller already confirmed
    /// Swagger is enabled (otherwise the enablement middleware 404s /swagger/*).
    /// </summary>
    public static async Task<bool> TryServeAssetAsync(HttpContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = context.Request.Path.Value ?? "";
        if (path.EndsWith("/" + JsFileName, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "application/javascript; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(JavaScript, cancellationToken);
            return true;
        }

        if (path.EndsWith("/" + CssFileName, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.ContentType = "text/css; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsync(CssRules, cancellationToken);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Fixture that replays Swagger's after-paint CSS win <em>and</em> a late
    /// React-style <c>display:inline</c> reset after our first pin. Open the
    /// HTML and run <c>window.__deedAiMeasureAuthorize()</c> — still ≥44×44.
    /// </summary>
    public static string MeasureFixtureHtml()
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine("<title>Deed AI Swagger Authorize hit-target measure</title>");
        sb.AppendLine(StyleTag);
        sb.AppendLine("""
            <style id="swagger-after-paint-win">
            /* Swagger default + a later equal-specificity !important rule
               (the #17 / #18 failure mode). Runtime must still win. */
            .swagger-ui .btn { font-size: 14px; border: 2px solid #49cc90; background: #fff; }
            .swagger-ui .btn.authorize,
            .swagger-ui .auth-btn-wrapper .btn,
            .swagger-ui .btn.modal-btn.authorize,
            .swagger-ui .btn-done {
              display: inline !important;
              line-height: 1 !important;
              padding: 5px 23px !important;
              height: auto !important;
              max-height: 30px !important;
              min-height: 0 !important;
              min-width: 0 !important;
            }
            .swagger-ui .btn.authorize span { float: left; padding: 4px 20px 0 0; }
            </style>
            """);
        sb.AppendLine("</head><body>");
        sb.AppendLine("""
            <div class="swagger-ui">
              <div class="scheme-container">
                <div class="auth-wrapper">
                  <button type="button" class="btn authorize unlocked" id="top-authorize">
                    <span>Authorize</span>
                    <svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"></svg>
                  </button>
                </div>
              </div>
              <div class="modal-ux dialog-ux">
                <div class="auth-btn-wrapper">
                  <button type="button" class="btn modal-btn auth authorize" id="modal-authorize">Authorize</button>
                  <button type="button" class="btn modal-btn auth btn-done" id="modal-close">Close</button>
                </div>
              </div>
            </div>
            """);
        sb.AppendLine(ScriptTag);
        sb.AppendLine("""
            <script id="swagger-react-reset">
            /* Simulate Swagger React re-applying display:inline AFTER our pin. */
            function deedAiSwaggerResetAuthorize() {
              var nodes = document.querySelectorAll(".btn.authorize, .auth-btn-wrapper .btn, .btn-done");
              for (var i = 0; i < nodes.length; i++) {
                nodes[i].style.setProperty("display", "inline", "");
                nodes[i].style.setProperty("height", "auto", "important");
                nodes[i].style.setProperty("min-height", "0", "important");
                nodes[i].style.setProperty("max-height", "30px", "important");
              }
            }
            setTimeout(deedAiSwaggerResetAuthorize, 20);
            </script>
            """);
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string ReadSibling(string fileName)
    {
        var asm = typeof(SwaggerAuthorizeHitTarget).Assembly;
        var resource = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal));
        if (resource is not null)
        {
            using var stream = asm.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException("Missing embedded " + fileName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        var dir = Path.GetDirectoryName(asm.Location);
        if (!string.IsNullOrEmpty(dir))
        {
            var onDisk = Path.Combine(dir, fileName);
            if (File.Exists(onDisk))
            {
                return File.ReadAllText(onDisk);
            }

            var published = Path.Combine(dir, "wwwroot", "swagger", fileName);
            if (File.Exists(published))
            {
                return File.ReadAllText(published);
            }
        }

        var source = Path.Combine(AppContext.BaseDirectory, "Swagger", fileName);
        if (File.Exists(source))
        {
            return File.ReadAllText(source);
        }

        throw new InvalidOperationException(
            $"Swagger Authorize hit-target asset '{fileName}' was not embedded. Check DeedAi.Api.csproj.");
    }
}
