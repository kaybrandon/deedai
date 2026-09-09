using System.Text;

namespace DeedAi.Api.Swagger;

/// <summary>
/// Phase 4.2.1 Authorize tap-target: CSS is not enough. Swagger UI paints
/// <c>.btn.authorize { display: inline }</c> (~34px) and can inject a later
/// equal-specificity rule after HeadContent, so computed height stays ~34px
/// (modal ~30px) even when the stylesheet is on the page. The runtime script
/// wraps <c>SwaggerUIBundle</c> <c>onComplete</c>, observes mutations, and
/// sets inline <c>!important</c> 44×44 styles after paint.
/// </summary>
public static class SwaggerAuthorizeHitTarget
{
    public const int MinPx = 44;
    public const string StyleId = "deedai-swagger-authorize";
    public const string ScriptId = "deedai-swagger-authorize-runtime";
    public const string MeasureFunction = "__deedAiMeasureAuthorize";

    public static string CssRules { get; } = ReadSibling("deedai-swagger-authorize.css");
    public static string JavaScript { get; } = ReadSibling("deedai-swagger-authorize.js");

    public static string StyleTag =>
        "<style id=\"" + StyleId + "\">" + CssRules + "</style>";

    public static string ScriptTag =>
        "<script id=\"" + ScriptId + "\">" + JavaScript + "</script>";

    public static string HeadContent => StyleTag + ScriptTag;

    /// <summary>
    /// Fixture that replays Swagger's after-paint CSS win so QA / tests can
    /// measure <c>getBoundingClientRect()</c> on top-bar and modal Authorize.
    /// Open the HTML and run <c>window.__deedAiMeasureAuthorize()</c>.
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
               (the #17 failure mode). Runtime inline styles must still win. */
            .swagger-ui .btn { font-size: 14px; border: 2px solid #49cc90; background: #fff; }
            .swagger-ui .btn.authorize,
            .swagger-ui .auth-btn-wrapper .btn,
            .swagger-ui .btn.modal-btn.authorize,
            .swagger-ui .btn-done {
              display: inline !important;
              line-height: 1 !important;
              padding: 5px 23px !important;
              height: auto !important;
              min-height: 0 !important;
              min-width: 0 !important;
            }
            .swagger-ui .btn.authorize span { float: left; padding: 4px 20px 0 0; }
            </style>
            """);
        sb.AppendLine(ScriptTag);
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
