using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using HotChocolate.Language;
using Microsoft.Extensions.Options;

namespace Api.Security;

public sealed class GraphQlRequestSecurityMiddleware(
    RequestDelegate next,
    IOptions<GraphQlSecurityOptions> options,
    ILogger<GraphQlRequestSecurityMiddleware> logger,
    IWebHostEnvironment environment)
{
    private static readonly IReadOnlyDictionary<string, int> FieldWeights = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["city"] = 30,
        ["cityLots"] = 40,
        ["rankedProductTypes"] = 20,
        ["companyLedger"] = 50,
        ["ledgerDrillDown"] = 35,
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

        if (!TryParseRequestItems(requestBody, out var requestItems) || requestItems.Count == 0)
        {
            await next(context);
            return;
        }

        var maxDepth = Math.Max(1, options.Value.MaxDepth);
        var maxComplexity = Math.Max(1, options.Value.MaxComplexity);
        for (var index = 0; index < requestItems.Count; index++)
        {
            var requestItem = requestItems[index];
            if (string.IsNullOrWhiteSpace(requestItem.Query))
            {
                continue;
            }

            if (!TryParseDocument(requestItem.Query, out var document))
            {
                continue;
            }

            var operations = SelectOperations(document, requestItem.OperationName);
            if (operations.Count == 0)
            {
                continue;
            }

            var fragments = document.Definitions
                .OfType<FragmentDefinitionNode>()
                .ToDictionary(definition => definition.Name.Value, definition => definition, StringComparer.Ordinal);

            if (!environment.IsDevelopment() && TryGetIntrospectionField(operations, fragments, out var introspectionField))
            {
                await RejectAsync(
                    context,
                    code: "INTROSPECTION_DISABLED",
                    message: "GraphQL introspection is disabled outside Development.",
                    violationType: "IntrospectionDisabled",
                    extensions: new Dictionary<string, object?>
                    {
                        ["batchIndex"] = index,
                        ["field"] = introspectionField,
                    });
                return;
            }

            var depth = ComputeMaxDepth(operations, fragments);
            if (depth > maxDepth)
            {
                await RejectAsync(
                    context,
                    code: "QUERY_TOO_DEEP",
                    message: "Request exceeds the allowed query depth.",
                    violationType: "MaxDepthExceeded",
                    extensions: new Dictionary<string, object?>
                    {
                        ["batchIndex"] = index,
                        ["maxDepth"] = maxDepth,
                        ["actualDepth"] = depth,
                    });
                return;
            }

            var complexityBreakdown = ComputeComplexity(operations, fragments);
            if (complexityBreakdown.Total > maxComplexity)
            {
                await RejectAsync(
                    context,
                    code: "QUERY_TOO_COMPLEX",
                    message: "Request exceeds the allowed query complexity.",
                    violationType: "MaxComplexityExceeded",
                    extensions: new Dictionary<string, object?>
                    {
                        ["batchIndex"] = index,
                        ["maxComplexity"] = maxComplexity,
                        ["actualComplexity"] = complexityBreakdown.Total,
                        ["rootFields"] = complexityBreakdown.RootFields,
                    });
                return;
            }
        }

        await next(context);
    }

    private async Task RejectAsync(
        HttpContext context,
        string code,
        string message,
        string violationType,
        IReadOnlyDictionary<string, object?>? extensions = null)
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
                        extensions = MergeExtensions(code, extensions),
                    }
                }
            }));
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        return await reader.ReadToEndAsync();
    }

    private static IReadOnlyDictionary<string, object?> MergeExtensions(
        string code,
        IReadOnlyDictionary<string, object?>? extensions)
    {
        var merged = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = code,
        };
        if (extensions is null)
        {
            return merged;
        }

        foreach (var pair in extensions)
        {
            merged[pair.Key] = pair.Value;
        }

        return merged;
    }

    private static bool TryParseRequestItems(string requestBody, out List<GraphQlRequestItem> items)
    {
        try
        {
            using var document = JsonDocument.Parse(requestBody);
            items = document.RootElement.ValueKind switch
            {
                JsonValueKind.Object => new List<GraphQlRequestItem> { ParseRequestItem(document.RootElement) },
                JsonValueKind.Array => document.RootElement
                    .EnumerateArray()
                    .Where(static element => element.ValueKind == JsonValueKind.Object)
                    .Select(ParseRequestItem)
                    .ToList(),
                _ => new List<GraphQlRequestItem>(),
            };
            return true;
        }
        catch (JsonException)
        {
            items = [];
            return false;
        }
    }

    private static GraphQlRequestItem ParseRequestItem(JsonElement requestElement)
    {
        var query = requestElement.TryGetProperty("query", out var queryElement)
            && queryElement.ValueKind == JsonValueKind.String
            ? queryElement.GetString()
            : null;
        var operationName = requestElement.TryGetProperty("operationName", out var operationNameElement)
            && operationNameElement.ValueKind == JsonValueKind.String
            ? operationNameElement.GetString()
            : null;
        return new GraphQlRequestItem(query, operationName);
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

    private static IReadOnlyList<OperationDefinitionNode> SelectOperations(DocumentNode document, string? operationName)
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

        if (operations.Count == 1)
        {
            return operations;
        }

        // For unnamed multi-operation documents, inspect all operations defensively.
        return operations;
    }

    private static bool TryGetIntrospectionField(
        IReadOnlyList<OperationDefinitionNode> operations,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments,
        out string fieldName)
    {
        foreach (var operation in operations)
        {
            foreach (var field in EnumerateRootFields(operation.SelectionSet, fragments, new HashSet<string>(StringComparer.Ordinal)))
            {
                if (field.Name.Value is "__schema" or "__type" or "__typename")
                {
                    fieldName = field.Name.Value;
                    return true;
                }
            }
        }

        fieldName = string.Empty;
        return false;
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

    private static int ComputeMaxDepth(
        IReadOnlyList<OperationDefinitionNode> operations,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments)
    {
        var maxDepth = 0;
        foreach (var operation in operations)
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

    private static ComplexityBreakdown ComputeComplexity(
        IReadOnlyList<OperationDefinitionNode> operations,
        IReadOnlyDictionary<string, FragmentDefinitionNode> fragments)
    {
        var rootFields = new Dictionary<string, int>(StringComparer.Ordinal);
        var total = 0;

        foreach (var operation in operations)
        {
            total += ComputeSelectionSetComplexity(operation.SelectionSet, fragments, new HashSet<string>(StringComparer.Ordinal));

            foreach (var rootField in EnumerateRootFields(operation.SelectionSet, fragments, new HashSet<string>(StringComparer.Ordinal)))
            {
                var weight = ResolveFieldWeight(rootField.Name.Value);
                if (rootFields.TryGetValue(rootField.Name.Value, out var existing))
                {
                    rootFields[rootField.Name.Value] = existing + weight;
                }
                else
                {
                    rootFields[rootField.Name.Value] = weight;
                }
            }
        }

        return new ComplexityBreakdown(total, rootFields);
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

    /// <summary>
    /// Parsed GraphQL HTTP payload item with optional operation name.
    /// Supports both single-object and JSON-array batch request bodies.
    /// </summary>
    private sealed record GraphQlRequestItem(string? Query, string? OperationName);

    /// <summary>
    /// Aggregate complexity values for a selected operation set.
    /// Includes the total complexity score and per-root-field contribution map.
    /// </summary>
    private sealed record ComplexityBreakdown(int Total, IReadOnlyDictionary<string, int> RootFields);
}
