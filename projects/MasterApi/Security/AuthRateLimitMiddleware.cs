using System.Text.Json;
using HotChocolate.Language;
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
    public async Task InvokeAsync(
        HttpContext context,
        IMemoryCache cache,
        IOptions<AuthOptions> authOptions,
        ILogger<AuthRateLimitMiddleware> logger)
    {
        // Disable rate limiting in Development and Testing to avoid slowing local dev and CI.
        if (env.IsDevelopment() || (env.IsEnvironment("Testing") && !authOptions.Value.EnableRateLimitInTesting))
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

        string requestBody;
        try
        {
            requestBody = await ReadRequestBodyAsync(context.Request.Body, context.RequestAborted);
        }
        catch
        {
            // Malformed body — pass through and let HotChocolate handle the error.
            await next(context);
            return;
        }
        finally
        {
            context.Request.Body.Position = 0;
        }

        var authOperationCount = CountAuthOperations(requestBody);
        if (authOperationCount <= 0)
        {
            await next(context);
            return;
        }

        var ip = GetClientIp(context);
        var rateKey = $"auth_rate:{ip}";
        var windowSize = TimeSpan.FromMinutes(1);
        var limit = authOptions.Value.RateLimitRequestsPerMinute;

        // Store a long[] wrapper so Interlocked can operate atomically on the
        // in-cache reference, avoiding TOCTOU races from concurrent requests.
        var counter = cache.GetOrCreate(rateKey, entry =>
        {
            entry.SetAbsoluteExpiration(windowSize);
            return new long[] { 0 };
        });

        var count = Interlocked.Add(ref counter![0], authOperationCount);

        if (count > limit)
        {
            logger.LogWarning(
                "Auth rate limit exceeded for IP {ClientIp}: {Count} auth fields in the last minute (limit {Limit}).",
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

    private static async Task<string> ReadRequestBodyAsync(Stream body, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(body, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static bool IsGraphQlPost(HttpContext context)
    {
        return HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/graphql", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Counts the number of <c>login</c>/<c>register</c> root mutation fields in the request body,
    /// parsing the GraphQL AST so named operations, fragments, and JSON-array batched bodies cannot
    /// bypass the per-IP limiter.
    /// </summary>
    private static int CountAuthOperations(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => CountAuthOperationsInRequestItem(document.RootElement),
                JsonValueKind.Array => document.RootElement.EnumerateArray()
                    .Where(static element => element.ValueKind == JsonValueKind.Object)
                    .Sum(CountAuthOperationsInRequestItem),
                _ => 0,
            };
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static int CountAuthOperationsInRequestItem(JsonElement requestItem)
    {
        var operationName = requestItem.TryGetProperty("operationName", out var operationNameElement)
            && operationNameElement.ValueKind == JsonValueKind.String
            ? operationNameElement.GetString()
            : null;
        var query = requestItem.TryGetProperty("query", out var queryElement)
            && queryElement.ValueKind == JsonValueKind.String
            ? queryElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        if (!TryParseDocument(query, out var parsedDocument))
        {
            // If AST parsing fails, do not guess from string contents to avoid false positives.
            return 0;
        }

        return CountAuthOperationFields(parsedDocument, operationName);
    }

    private static bool TryParseDocument(string query, out DocumentNode document)
    {
        try
        {
            document = Utf8GraphQLParser.Parse(query);
            return true;
        }
        catch
        {
            document = null!;
            return false;
        }
    }

    private static int CountAuthOperationFields(DocumentNode document, string? operationName)
    {
        var fragments = document.Definitions
            .OfType<FragmentDefinitionNode>()
            .ToDictionary(definition => definition.Name.Value, definition => definition, StringComparer.Ordinal);
        var selectedOperations = SelectOperations(document, operationName);
        var count = 0;

        foreach (var operation in selectedOperations)
        {
            if (operation.Operation != OperationType.Mutation)
            {
                continue;
            }

            foreach (var field in EnumerateRootFields(operation.SelectionSet, fragments, new HashSet<string>(StringComparer.Ordinal)))
            {
                if (field.Name.Value.Equals("login", StringComparison.OrdinalIgnoreCase)
                    || field.Name.Value.Equals("register", StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private static IEnumerable<OperationDefinitionNode> SelectOperations(DocumentNode document, string? operationName)
    {
        var operations = document.Definitions.OfType<OperationDefinitionNode>().ToList();
        if (!string.IsNullOrWhiteSpace(operationName))
        {
            var selected = operations
                .Where(operation => operation.Name?.Value.Equals(operationName, StringComparison.Ordinal) == true)
                .ToList();
            if (selected.Count > 0)
            {
                return selected;
            }
        }

        // For single or unnamed multi-operation documents, inspect all operations defensively.
        return operations;
    }

    private static IEnumerable<FieldNode> EnumerateRootFields(
        SelectionSetNode selectionSet,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments,
        HashSet<string> visitedFragments)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    yield return field;
                    break;
                case InlineFragmentNode inlineFragment:
                    foreach (var nestedField in EnumerateRootFields(inlineFragment.SelectionSet, fragments, visitedFragments))
                    {
                        yield return nestedField;
                    }
                    break;
                case FragmentSpreadNode spread
                    when fragments.TryGetValue(spread.Name.Value, out var fragment)
                        && visitedFragments.Add(spread.Name.Value):
                    foreach (var nestedField in EnumerateRootFields(fragment.SelectionSet, fragments, visitedFragments))
                    {
                        yield return nestedField;
                    }
                    break;
            }
        }
    }

    private static string GetClientIp(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
