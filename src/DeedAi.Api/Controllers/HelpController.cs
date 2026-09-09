using DeedAi.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DeedAi.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/help")]
public sealed class HelpController : ControllerBase
{
    [HttpGet]
    public ActionResult<IReadOnlyDictionary<string, string>> Get() => Ok(FieldHelpCatalog.All);
}
