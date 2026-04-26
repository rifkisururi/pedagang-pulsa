using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PedagangPulsa.Application.Abstractions.Persistence;
using PedagangPulsa.Infrastructure.Suppliers.Otomax;
using System.Reflection;

namespace PedagangPulsa.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class PingController : ControllerBase
{
    private readonly IAppDbContext _dbContext;
    private readonly OtomaxSettings _otomaxSettings;

    public PingController(IAppDbContext dbContext, OtomaxSettings otomaxSettings)
    {
        _dbContext = dbContext;
        _otomaxSettings = otomaxSettings;
    }

    [HttpGet]
    public IActionResult Ping()
    {
        var sw = Stopwatch.StartNew();
        sw.Stop();
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return Ok(new { message = "pong", version, durationMs = sw.Elapsed.TotalMilliseconds });
    }

    [HttpGet("db")]
    public async Task<IActionResult> PingDb()
    {
        var sw = Stopwatch.StartNew();
        await _dbContext.Database.ExecuteSqlRawAsync("SELECT 1");
        sw.Stop();
        var version = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
        return Ok(new { message = "pong", version, durationMs = sw.Elapsed.TotalMilliseconds });
    }

    [HttpGet("ip")]
    public async Task<IActionResult> PingIp()
    {
        const string ipifyUrl = "https://api.ipify.org/?format=json";

        // Direct IP (server IP)
        string? serverIp = null;
        int serverIpMs = -1;
        try
        {
            using var sw = Stopwatch.StartNew();
            using var directClient = new HttpClient();
            var directResponse = await directClient.GetStringAsync(ipifyUrl);
            serverIpMs = (int)sw.ElapsedMilliseconds;
            serverIp = JsonDocument.Parse(directResponse).RootElement.GetProperty("ip").GetString();
        }
        catch (Exception ex)
        {
            serverIp = $"error: {ex.Message}";
        }

        // Proxy IP (via Otomax proxy)
        string? proxyIp = null;
        int proxyIpMs = -1;
        bool proxyUsed = false;
        try
        {
            var handler = _otomaxSettings.CreateHttpMessageHandler();
            if (handler != null)
            {
                proxyUsed = true;
                using var sw = Stopwatch.StartNew();
                using var proxyClient = new HttpClient(handler);
                var proxyResponse = await proxyClient.GetStringAsync(ipifyUrl);
                proxyIpMs = (int)sw.ElapsedMilliseconds;
                proxyIp = JsonDocument.Parse(proxyResponse).RootElement.GetProperty("ip").GetString();
            }
            else
            {
                proxyIp = "proxy not configured";
            }
        }
        catch (Exception ex)
        {
            proxyIp = $"error: {ex.Message}";
        }

        return Ok(new
        {
            serverIp,
            serverIpMs,
            proxyIp,
            proxyIpMs,
            proxyUsed,
            proxyConfig = proxyUsed
                ? new { _otomaxSettings.ProxyHost, _otomaxSettings.ProxyPort, _otomaxSettings.UseProxy }
                : null
        });
    }
}
