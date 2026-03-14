using System.Collections.Concurrent;

namespace PedagangPulsa.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IServiceProvider _serviceProvider;

    // In-memory counter (for production, use Redis)
    private static readonly ConcurrentDictionary<string, RateLimitCounter> _counters = new();

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IServiceProvider serviceProvider)
    {
        _next = next;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var path = context.Request.Path.Value ?? string.Empty;

        // Define rate limit rules
        var rateLimitRule = GetRateLimitRule(path, context.Request.Method);

        if (rateLimitRule != null)
        {
            var identifier = GetIdentifier(context, rateLimitRule.KeyType);
            var key = $"{rateLimitRule.Prefix}:{identifier}";

            var counter = _counters.GetOrAdd(key, _ => new RateLimitCounter());

            // Simple thread-safe implementation using Interlocked
            var newCount = Interlocked.Increment(ref counter.Count);

            // Reset counter if window expired
            var now = DateTime.UtcNow;
            if ((now - counter.WindowStart) > rateLimitRule.Window)
            {
                Interlocked.Exchange(ref counter.Count, 1);
                counter.WindowStart = now;
                newCount = 1;
            }

            if (newCount > rateLimitRule.Limit)
            {
                _logger.LogWarning("Rate limit exceeded for {Key}. Count: {Count}, Limit: {Limit}",
                    key, newCount, rateLimitRule.Limit);

                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.Response.Headers.Add("Retry-After", ((int)rateLimitRule.Window.TotalSeconds).ToString());

                var response = System.Text.Json.JsonSerializer.Serialize(new
                {
                    success = false,
                    message = $"Rate limit exceeded. Maximum {rateLimitRule.Limit} requests per {rateLimitRule.Window.TotalMinutes} minute(s).",
                    errorCode = "RATE_LIMIT_EXCEEDED"
                });

                context.Response.ContentType = "application/json";
                context.Response.WriteAsync(response);
                return;
            }

            // Add rate limit headers
            context.Response.Headers.Add("X-RateLimit-Limit", rateLimitRule.Limit.ToString());
            context.Response.Headers.Add("X-RateLimit-Remaining", (rateLimitRule.Limit - newCount).ToString());
            context.Response.Headers.Add("X-RateLimit-Reset", counter.WindowStart.Add(rateLimitRule.Window).ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }

        await _next(context);
    }

    private string GetIdentifier(HttpContext context, RateLimitKeyType keyType)
    {
        return keyType switch
        {
            RateLimitKeyType.IP => context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            RateLimitKeyType.User => context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            _ => "unknown"
        };
    }

    private RateLimitRule? GetRateLimitRule(string path, string method)
    {
        // Login endpoint: 5 requests per minute per IP
        if (path.Contains("/api/auth/login", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return new RateLimitRule
            {
                Prefix = "login",
                Limit = 5,
                Window = TimeSpan.FromMinutes(1),
                KeyType = RateLimitKeyType.IP
            };
        }

        // Register endpoint: 3 requests per hour per IP
        if (path.Contains("/api/auth/register", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return new RateLimitRule
            {
                Prefix = "register",
                Limit = 3,
                Window = TimeSpan.FromHours(1),
                KeyType = RateLimitKeyType.IP
            };
        }

        // Transaction endpoints: 10 requests per minute per user
        if (path.Contains("/api/transactions", StringComparison.OrdinalIgnoreCase))
        {
            return new RateLimitRule
            {
                Prefix = "transaction",
                Limit = 10,
                Window = TimeSpan.FromMinutes(1),
                KeyType = RateLimitKeyType.User
            };
        }

        // PIN verify: 10 requests per minute per user
        if (path.Contains("/api/auth/pin/verify", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return new RateLimitRule
            {
                Prefix = "pin_verify",
                Limit = 10,
                Window = TimeSpan.FromMinutes(1),
                KeyType = RateLimitKeyType.User
            };
        }

        // Transfer endpoint: 10 requests per minute per user
        if (path.Contains("/api/transfer", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return new RateLimitRule
            {
                Prefix = "transfer",
                Limit = 10,
                Window = TimeSpan.FromMinutes(1),
                KeyType = RateLimitKeyType.User
            };
        }

        // Topup request: 5 requests per hour per user
        if (path.Contains("/api/topup", StringComparison.OrdinalIgnoreCase) && method == "POST")
        {
            return new RateLimitRule
            {
                Prefix = "topup",
                Limit = 5,
                Window = TimeSpan.FromHours(1),
                KeyType = RateLimitKeyType.User
            };
        }

        return null;
    }

    private class RateLimitCounter
    {
        public int Count;
        public DateTime WindowStart = DateTime.UtcNow;
    }

    private class RateLimitRule
    {
        public required string Prefix { get; set; }
        public int Limit { get; set; }
        public TimeSpan Window { get; set; }
        public RateLimitKeyType KeyType { get; set; }
    }

    private enum RateLimitKeyType
    {
        IP,
        User
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<RateLimitingMiddleware>();
    }
}
