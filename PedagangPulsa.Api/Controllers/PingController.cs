using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PingController : ControllerBase
{
    private readonly IAppDbContext _dbContext;

    public PingController(IAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public IActionResult Ping()
    {
        var sw = Stopwatch.StartNew();
        sw.Stop();
        return Ok(new { message = "pong", durationMs = sw.Elapsed.TotalMilliseconds });
    }

    [HttpGet("db")]
    public async Task<IActionResult> PingDb()
    {
        var sw = Stopwatch.StartNew();
        await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
        sw.Stop();
        return Ok(new { message = "pong", durationMs = sw.Elapsed.TotalMilliseconds });
    }
}
