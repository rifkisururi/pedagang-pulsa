using System.Collections.Concurrent;

namespace PedagangPulsa.Api.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    // In-memory counter (for production, use Redis)
    private static readonly ConcurrentDictionary<string, RateLimitCounter> _counters = new();
    private static readonly RateLimitRule[] _rules =
    [
        new("login", "/api/auth/login", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.IP),
        new("register", "/api/auth/register", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.IP),
        new("transaction", "/api/transaction", HttpMethods.Post, 10, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("pin_verify", "/api/auth/pin/verify", HttpMethods.Post, 10, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("transfer", "/api/transfer", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.User),
        new("topup", "/api/topup", HttpMethods.Post, 60, TimeSpan.FromMinutes(1), RateLimitKeyType.User)
    ];

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (GetRateLimitRule(context.Request.Path.Value, context.Request.Method) is not RateLimitRule rule)
        {
            await _next(context);
            return;
        }

        var identifier = GetIdentifier(context, rule.KeyType);
        var key = $"{rule.Prefix}:{identifier}";
        var counter = _counters.GetOrAdd(key, static _ => new RateLimitCounter());
        var now = DateTime.UtcNow;
        var snapshot = counter.RegisterRequest(now, rule.Window);

        ApplyRateLimitHeaders(context.Response.Headers, rule, snapshot);

        if (snapshot.Count > rule.Limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {Key}. Count: {Count}, Limit: {Limit}",
                key,
                snapshot.Count,
                rule.Limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers["Retry-After"] = GetRetryAfterSeconds(snapshot.ResetAt, now).ToString();

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

        return _rules.FirstOrDefault(rule =>
            string.Equals(method, rule.Method, StringComparison.OrdinalIgnoreCase) &&
            requestPath.StartsWith(rule.PathPrefix, StringComparison.OrdinalIgnoreCase));
    }

    private static void ApplyRateLimitHeaders(
        IHeaderDictionary headers,
        RateLimitRule rule,
        RateLimitSnapshot snapshot)
    {
        headers["X-RateLimit-Limit"] = rule.Limit.ToString();
        headers["X-RateLimit-Remaining"] = Math.Max(rule.Limit - snapshot.Count, 0).ToString();
        headers["X-RateLimit-Reset"] = snapshot.ResetAt.ToString("yyyy-MM-ddTHH:mm:ssZ");
    }

    private static int GetRetryAfterSeconds(DateTime resetAt, DateTime now)
    {
        return Math.Max((int)Math.Ceiling((resetAt - now).TotalSeconds), 1);
    }

    private sealed class RateLimitCounter
    {
        private readonly Lock _lock = new();
        private int _count;
        private DateTime _windowStart = DateTime.UtcNow;

        public RateLimitSnapshot RegisterRequest(DateTime now, TimeSpan window)
        {
            lock (_lock)
            {
                if ((now - _windowStart) > window)
                {
                    _count = 0;
                    _windowStart = now;
                }

                _count++;

                return new RateLimitSnapshot(_count, _windowStart.Add(window));
            }
        }
    }

    private readonly record struct RateLimitSnapshot(int Count, DateTime ResetAt);

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
