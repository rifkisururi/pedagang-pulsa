using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace PedagangPulsa.Application.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred during request handling. Path: {Path}, Method: {Method}",
                context.Request.Path,
                context.Request.Method);

            throw; // Rethrow to let the default exception handler/filter deal with the response
        }
    }
}
