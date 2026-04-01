using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace NoMercyBot.Api.Controllers;

[ApiController]
[Tags("Home")]
[ApiVersion(1.0)]
[Route("api")]
public class HomeController : BaseController
{
    [HttpGet]
    [AllowAnonymous]
    [Route("/status")]
    public IActionResult Status()
    {
        return Ok(
            new
            {
                Status = "ok",
                Version = "1.0",
                Message = "NoMercyBot Server is running",
                Timestamp = DateTime.UtcNow,
            }
        );
    }
}
