using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    [HttpGet]
    public IActionResult Ping()
    {
        var sw = Stopwatch.StartNew();
        // do nothing - just measure baseline processing time
        sw.Stop();
        return Ok(new { message = "pong", durationMs = sw.Elapsed.TotalMilliseconds });
    }
}
