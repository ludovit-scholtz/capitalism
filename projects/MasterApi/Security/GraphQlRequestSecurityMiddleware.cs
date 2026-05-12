using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MasterApi.Configuration;
using HotChocolate.Language;
using Microsoft.Extensions.Options;

namespace MasterApi.Security;

public sealed class GraphQlRequestSecurityMiddleware(
    RequestDelegate next,
    IOptions<GraphQlSecurityOptions> options,
    ILogger<GraphQlRequestSecurityMiddleware> logger,
    IWebHostEnvironment environment)
{
    private static readonly IReadOnlyDictionary<string, int> FieldWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["gameServers"] = 20,
        ["globalGameAdminGrants"] = 25,
        ["gameNewsFeed"] = 40,
        ["goldTokenBalances"] = 20,
        ["goldTokenTransactions"] = 35,
    };

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals("/graphql", StringComparison.OrdinalIgnoreCase)
            || !HttpMethods.IsPost(context.Request.Method))
        {
            await next(context);
            return;
        }

        context.Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        context.Request.Body.Position = 0;

        if (!TryExtractQuery(requestBody, out var query) || string.IsNullOrWhiteSpace(query))
        {
            await next(context);
            return;
        }

        if (!TryParseDocument(query, out var document))
        {
            await next(context);
            return;
        }

        if (!environment.IsDevelopment() && ContainsIntrospection(document))
        {
            await RejectAsync(context, "FORBIDDEN", "This operation is forbidden.", "IntrospectionForbidden");
            return;
        }

        var maxDepth = Math.Max(1, options.Value.MaxDepth);
        var depth = ComputeMaxDepth(document);
        if (depth > maxDepth)
        {
            await RejectAsync(context, "MAX_DEPTH_EXCEEDED", "Request exceeds the allowed query depth.", "MaxDepthExceeded");
            return;
        }

        var maxComplexity = Math.Max(1, options.Value.MaxComplexity);
        var complexity = ComputeComplexity(document);
        if (complexity > maxComplexity)
        {
            await RejectAsync(context, "MAX_COMPLEXITY_EXCEEDED", "Request exceeds the allowed query complexity.", "MaxComplexityExceeded");
            return;
        }

        await next(context);
    }

    private async Task RejectAsync(HttpContext context, string code, string message, string violationType)
    {
        var playerId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        var email = context.User.FindFirstValue(ClaimTypes.Email) ?? "anonymous";
        logger.LogWarning(
            "GraphQL security rejection detected: {ViolationType}. PlayerId={PlayerId} Email={Email}",
            violationType,
            playerId,
            email);

        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new
            {
                errors = new[]
                {
                    new
                    {
                        message,
                        extensions = new { code },
                    }
                }
            }));
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static bool TryExtractQuery(string requestBody, out string? query)
    {
        try
        {
            using var document = JsonDocument.Parse(requestBody);
            query = document.RootElement.TryGetProperty("query", out var queryElement)
                ? queryElement.GetString()
                : null;
            return true;
        }
        catch (JsonException)
        {
            query = null;
            return false;
        }
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

    private static bool ContainsIntrospection(DocumentNode document)
    {
        return document.Definitions
            .OfType<OperationDefinitionNode>()
            .Any(definition => SelectionSetContainsIntrospection(definition.SelectionSet));
    }

    private static bool SelectionSetContainsIntrospection(SelectionSetNode selectionSet)
    {
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    if (field.Name.Value is "__schema" or "__type")
                    {
                        return true;
                    }

                    if (field.SelectionSet is not null && SelectionSetContainsIntrospection(field.SelectionSet))
                    {
                        return true;
                    }
                    break;
                case InlineFragmentNode inlineFragment
                    when SelectionSetContainsIntrospection(inlineFragment.SelectionSet):
                    return true;
            }
        }

        return false;
    }

    private static int ComputeMaxDepth(DocumentNode document)
    {
        var fragments = document.Definitions
            .OfType<FragmentDefinitionNode>()
            .ToDictionary(definition => definition.Name.Value, definition => definition, StringComparer.Ordinal);

        var maxDepth = 0;
        foreach (var operation in document.Definitions.OfType<OperationDefinitionNode>())
        {
            maxDepth = Math.Max(maxDepth, ComputeDepth(operation.SelectionSet, 1, fragments, new HashSet<string>(StringComparer.Ordinal)));
        }

        return maxDepth;
    }

    private static int ComputeDepth(
        SelectionSetNode selectionSet,
        int depth,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments,
        HashSet<string> visitedFragments)
    {
        var maxDepth = depth;
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field when field.SelectionSet is not null:
                    maxDepth = Math.Max(maxDepth, ComputeDepth(field.SelectionSet, depth + 1, fragments, visitedFragments));
                    break;
                case InlineFragmentNode inlineFragment:
                    maxDepth = Math.Max(maxDepth, ComputeDepth(inlineFragment.SelectionSet, depth + 1, fragments, visitedFragments));
                    break;
                case FragmentSpreadNode spread
                    when fragments.TryGetValue(spread.Name.Value, out var fragment)
                         && visitedFragments.Add(spread.Name.Value):
                    maxDepth = Math.Max(maxDepth, ComputeDepth(fragment.SelectionSet, depth + 1, fragments, visitedFragments));
                    visitedFragments.Remove(spread.Name.Value);
                    break;
            }
        }

        return maxDepth;
    }

    private static int ComputeComplexity(DocumentNode document)
    {
        var fragments = document.Definitions
            .OfType<FragmentDefinitionNode>()
            .ToDictionary(definition => definition.Name.Value, definition => definition, StringComparer.Ordinal);

        var total = 0;
        foreach (var operation in document.Definitions.OfType<OperationDefinitionNode>())
        {
            total += ComputeSelectionSetComplexity(operation.SelectionSet, fragments, new HashSet<string>(StringComparer.Ordinal));
        }

        return total;
    }

    private static int ComputeSelectionSetComplexity(
        SelectionSetNode selectionSet,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments,
        HashSet<string> visitedFragments)
    {
        var total = 0;
        foreach (var selection in selectionSet.Selections)
        {
            switch (selection)
            {
                case FieldNode field:
                    total += ResolveFieldWeight(field.Name.Value);
                    if (field.SelectionSet is not null)
                    {
                        total += ComputeSelectionSetComplexity(field.SelectionSet, fragments, visitedFragments);
                    }
                    break;
                case InlineFragmentNode inlineFragment:
                    total += ComputeSelectionSetComplexity(inlineFragment.SelectionSet, fragments, visitedFragments);
                    break;
                case FragmentSpreadNode spread
                    when fragments.TryGetValue(spread.Name.Value, out var fragment)
                         && visitedFragments.Add(spread.Name.Value):
                    total += ComputeSelectionSetComplexity(fragment.SelectionSet, fragments, visitedFragments);
                    visitedFragments.Remove(spread.Name.Value);
                    break;
            }
        }

        return total;
    }

    private static int ResolveFieldWeight(string fieldName)
        => FieldWeights.TryGetValue(fieldName, out var weight) ? weight : 1;
}
