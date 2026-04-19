using PedagangPulsa.Application.Abstractions.Caching;

namespace PedagangPulsa.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IRedisService _redis;

    private static readonly RateLimitRule[] _rules =
    [
        //new("login", "/api/auth/login", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.IP),
        new("register", "/api/auth/register", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.IP),
        new("pin_verify", "/api/auth/pin/verify", HttpMethods.Post, 10, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("change_pin", "/api/auth/pin", HttpMethods.Put, 5, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("change_password", "/api/auth/password", HttpMethods.Put, 5, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("transaction", "/api/transaction", HttpMethods.Post, 10, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("transfer", "/api/transfer", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("topup", "/api/topup", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.User)
    ];

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IRedisService redis)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (GetRateLimitRule(context.Request.Path.Value, context.Request.Method) is not RateLimitRule rule)
        {
            await _next(context);
            return;
        }

        var identifier = GetIdentifier(context, rule.KeyType);
        var key = $"ratelimit:{rule.Prefix}:{identifier}";

        // Use Redis INCR with expiry for atomic rate limiting
        var currentCountStr = await _redis.GetAsync(key);
        int currentCount;
        if (currentCountStr == null)
        {
            // First request in this window
            currentCount = 1;
            await _redis.SetAsync(key, "1", rule.Window);
        }
        else
        {
            currentCount = int.Parse(currentCountStr) + 1;
            await _redis.SetAsync(key, currentCount.ToString(), rule.Window);
        }

        // Calculate remaining time for headers
        var ttl = await _redis.TtlAsync(key);
        var resetAt = DateTime.UtcNow.AddSeconds(Math.Max(ttl, 1));

        // Apply rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = rule.Limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(rule.Limit - currentCount, 0).ToString();
        context.Response.Headers["X-RateLimit-Reset"] = resetAt.ToString("yyyy-MM-ddTHH:mm:ssZ");

        if (currentCount > rule.Limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Key}. Count: {Count}, Limit: {Limit}",
                key,
                currentCount,
                rule.Limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = Math.Max((int)Math.Ceiling((resetAt - DateTime.UtcNow).TotalSeconds), 1).ToString();

            await context.Response.WriteAsJsonAsync(new
            {
                success = false,
                message = $"Rate limit exceeded. Maximum {rule.Limit} requests per {rule.Window.TotalMinutes} minute(s).",
                errorCode = "RATE_LIMIT_EXCEEDED"
            });
            return;
        }

        await _next(context);
    }

    private static string GetIdentifier(HttpContext context, RateLimitKeyType keyType)
    {
        return keyType switch
        {
            RateLimitKeyType.IP => context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            RateLimitKeyType.User => context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous",
            _ => "unknown"
        };
    }

    private static RateLimitRule? GetRateLimitRule(string? path, string method)
    {
        var requestPath = path ?? string.Empty;

        foreach (var rule in _rules)
        {
            if (string.Equals(method, rule.Method, StringComparison.OrdinalIgnoreCase) &&
                requestPath.StartsWith(rule.PathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return rule;
            }
        }

        return null;
    }

    private readonly record struct RateLimitRule(
        string Prefix,
        string PathPrefix,
        string Method,
        int Limit,
        TimeSpan Window,
        RateLimitKeyType KeyType);

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
