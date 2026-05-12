using System.Text.Json;
using MasterApi.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace MasterApi.Security;

/// <summary>
/// Middleware that applies per-IP rate limiting to GraphQL login and register operations.
/// Returns HTTP 429 when the configured threshold is exceeded.
/// Rate limiting is disabled in Development and Testing environments.
/// </summary>
public sealed class AuthRateLimitMiddleware(
    RequestDelegate next,
    IWebHostEnvironment env)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task InvokeAsync(
        HttpContext context,
        IMemoryCache cache,
        IOptions<AuthOptions> authOptions,
        ILogger<AuthRateLimitMiddleware> logger)
    {
        // Disable rate limiting in Development and Testing to avoid slowing local dev and CI.
        if (env.IsDevelopment() || env.IsEnvironment("Testing"))
        {
            await next(context);
            return;
        }

        if (!IsGraphQlPost(context))
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();

        GraphQlHttpRequest? requestBody = null;
        try
        {
            requestBody = await JsonSerializer.DeserializeAsync<GraphQlHttpRequest>(
                context.Request.Body, JsonOptions, context.RequestAborted);
        }
        catch
        {
            // Malformed JSON — pass through and let HotChocolate handle the error.
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        if (!IsAuthOperation(requestBody))
        {
            await next(context);
            return;
        }

        var ip = GetClientIp(context);
        var rateKey = $"auth_rate:{ip}";
        var windowSize = TimeSpan.FromMinutes(1);
        var limit = authOptions.Value.RateLimitRequestsPerMinute;

        // Store a long[] wrapper so Interlocked.Increment can operate atomically on the
        // in-cache reference, avoiding TOCTOU races from concurrent requests.
        var counter = cache.GetOrCreate(rateKey, entry =>
        {
            entry.SetAbsoluteExpiration(windowSize);
            return new long[] { 0 };
        });

        var count = Interlocked.Increment(ref counter![0]);

        if (count > limit)
        {
            logger.LogWarning(
                "Auth rate limit exceeded for IP {ClientIp}: {Count} requests in the last minute (limit {Limit}).",
                ip, count, limit);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                errors = new[]
                {
                    new
                    {
                        message = "Too many requests. Please wait a minute before trying again.",
                        extensions = new { code = "RATE_LIMIT_EXCEEDED" }
                    }
                }
            }, context.RequestAborted);
            return;
        }

        await next(context);
    }

    private static bool IsGraphQlPost(HttpContext context)
    {
        return HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/graphql", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAuthOperation(GraphQlHttpRequest? request)
    {
        if (request is null)
        {
            return false;
        }

        // Check the explicit operation name first (fastest path).
        if (!string.IsNullOrWhiteSpace(request.OperationName))
        {
            return request.OperationName.Equals("login", StringComparison.OrdinalIgnoreCase)
                || request.OperationName.Equals("register", StringComparison.OrdinalIgnoreCase);
        }

        // Fall back to query body inspection for un-named operations.
        var query = request.Query?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            return false;
        }

        return (query.Contains("login(", StringComparison.OrdinalIgnoreCase)
                || query.Contains("login (", StringComparison.OrdinalIgnoreCase)
                || query.Contains("register(", StringComparison.OrdinalIgnoreCase)
                || query.Contains("register (", StringComparison.OrdinalIgnoreCase))
            && query.Contains("mutation", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetClientIp(HttpContext context)
    {
        // Respect X-Forwarded-For set by a trusted reverse proxy.
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            // Take only the first (leftmost) address in the chain.
            var first = forwarded.Split(',')[0].Trim();
            if (!string.IsNullOrWhiteSpace(first))
            {
                return first;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private sealed class GraphQlHttpRequest
    {
        public string? OperationName { get; init; }
        public string? Query { get; init; }
    }
}
