using System.Text.RegularExpressions;

namespace GraphQLSurfaceInventory;

internal static partial class SurfaceInventoryAnalyzer
{
    private static readonly string[] NegativeKeywords =
    [
        "Unauthenticated",
        "Unauthorized",
        "AuthError",
        "AuthorizationError",
        "Forbidden",
        "NonOwner",
        "NotOwner",
        "NotOwned",
        "NotFoundOrNotOwned",
        "NotOwnedOrNotFound",
        "Foreign",
        "WrongPlayer",
        "Denied",
        "WithoutToken",
        "NoToken",
    ];

    public static InventorySnapshot Analyze(string apiTypesDir, string apiTestsDir)
    {
        var testMethods = ParseTestMethods(apiTestsDir);
        var operations = ParseOperations(apiTypesDir)
            .Select(op => op with { Coverage = BuildCoverage(op, testMethods) })
            .OrderBy(op => op.Kind, StringComparer.Ordinal)
            .ThenBy(op => op.GraphQlName, StringComparer.Ordinal)
            .ToList();

        return new InventorySnapshot(DateTime.UtcNow.ToString("O"), operations);
    }

    public static IReadOnlyList<MissingCoverage> FindMissingCoverageForNewSensitiveOperations(
        InventorySnapshot current,
        InventorySnapshot baseline)
    {
        var baselineKeys = baseline.Operations
            .Select(op => op.Key)
            .ToHashSet(StringComparer.Ordinal);

        var missing = new List<MissingCoverage>();

        foreach (var op in current.Operations.Where(op => op.IsSensitive && !baselineKeys.Contains(op.Key)))
        {
            var hasNegative = op.Coverage.HasNegativeCoverage;
            var hasPositive = op.Coverage.HasPositiveCoverage;
            if (hasNegative && hasPositive)
            {
                continue;
            }

            var reason = hasNegative
                ? "missing owner-success test"
                : hasPositive
                    ? "missing unauthenticated/wrong-owner test"
                    : "missing both auth-negative and owner-success tests";

            missing.Add(new MissingCoverage(op, reason));
        }

        return missing;
    }

    private static CoverageStatus BuildCoverage(OperationInventory operation, IReadOnlyList<string> testMethods)
    {
        var tokens = BuildOperationTokens(operation).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var matches = testMethods
            .Where(test => tokens.Any(token => test.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var negative = matches.Where(IsNegativeTestName).ToList();
        var positive = matches.Where(name => !IsNegativeTestName(name)).ToList();

        return new CoverageStatus(negative.Count > 0, positive.Count > 0, negative, positive);
    }

    private static IEnumerable<string> BuildOperationTokens(OperationInventory operation)
    {
        yield return operation.MethodName;
        yield return ToPascalCase(operation.GraphQlName);

        if (operation.Kind == "query" && operation.MethodName.StartsWith("Get", StringComparison.Ordinal) && operation.MethodName.Length > 3)
        {
            yield return operation.MethodName[3..];
        }
    }

    private static bool IsNegativeTestName(string testName)
        => NegativeKeywords.Any(keyword => testName.Contains(keyword, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ParseTestMethods(string apiTestsDir)
    {
        var results = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(apiTestsDir, "*.cs", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in TestMethodRegex().Matches(text))
            {
                var methodName = match.Groups["name"].Value;
                if (!string.IsNullOrWhiteSpace(methodName))
                {
                    results.Add(methodName);
                }
            }
        }

        return results.ToList();
    }

    private static IReadOnlyList<OperationInventory> ParseOperations(string apiTypesDir)
    {
        var operations = new List<OperationInventory>();

        foreach (var file in Directory.EnumerateFiles(apiTypesDir, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            var kind = fileName.StartsWith("Query", StringComparison.Ordinal)
                ? "query"
                : fileName.StartsWith("Mutation", StringComparison.Ordinal)
                    ? "mutation"
                    : null;

            if (kind is null)
            {
                continue;
            }

            var classMarker = kind == "query" ? "partial class Query" : "partial class Mutation";
            var text = File.ReadAllText(file);
            if (!text.Contains(classMarker, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in ResolverMethodRegex().Matches(text))
            {
                if (match.Groups["static"].Success)
                {
                    continue;
                }

                var methodName = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(methodName))
                {
                    continue;
                }

                var attrs = match.Groups["attrs"].Value;
                var graphQlName = ResolveGraphQlName(kind, methodName, attrs);
                var hasAuthorize = attrs.Contains("[Authorize", StringComparison.OrdinalIgnoreCase);
                var domain = ClassifyDomain(fileName, methodName, graphQlName);

                operations.Add(new OperationInventory(
                    kind,
                    methodName,
                    graphQlName,
                    domain,
                    file.Replace('\\', '/'),
                    hasAuthorize,
                    new CoverageStatus(false, false, [], [])));
            }
        }

        return operations;
    }

    private static string ResolveGraphQlName(string kind, string methodName, string attrs)
    {
        var graphQlNameMatch = GraphQlNameRegex().Match(attrs);
        if (graphQlNameMatch.Success)
        {
            return graphQlNameMatch.Groups["name"].Value;
        }

        var rawName = methodName;
        if (kind == "query"
            && methodName.StartsWith("Get", StringComparison.Ordinal)
            && methodName.Length > 3
            && char.IsUpper(methodName[3]))
        {
            rawName = methodName[3..];
        }

        return ToCamelCase(rawName);
    }

    private static string ClassifyDomain(string fileName, string methodName, string graphQlName)
    {
        var token = $"{fileName} {methodName} {graphQlName}".ToLowerInvariant();

        if (token.Contains("admin", StringComparison.Ordinal))
        {
            return "admin";
        }

        if (ContainsAny(token, "rank", "leaderboard", "telemetry", "billionaire"))
        {
            return "ranking";
        }

        if (ContainsAny(token, "loan", "lending", "collateral", "repay", "foreclosure", "debt"))
        {
            return "lending";
        }

        if (ContainsAny(token, "stock", "share", "shareholder", "dividend", "limitorder", "portfolio"))
        {
            return "shareholder";
        }

        if (ContainsAny(token, "bank", "forex", "ledger", "exchange", "fund", "deposit", "transfer", "account", "liquidity", "tax"))
        {
            return "finance";
        }

        return "other";
    }

    private static bool ContainsAny(string haystack, params string[] needles)
        => needles.Any(needle => haystack.Contains(needle, StringComparison.Ordinal));

    private static string ToCamelCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return string.Concat(char.ToLowerInvariant(value[0]), value[1..]);
    }

    private static string ToPascalCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.Contains('-', StringComparison.Ordinal))
        {
            return string.Concat(
                value
                    .Split('-', StringSplitOptions.RemoveEmptyEntries)
                    .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    [GeneratedRegex("(?ms)(?<attrs>(?:\\s*\\[[^\\]]+\\]\\s*)*)\\s*public\\s+(?<static>static\\s+)?(?:(?:async)\\s+)?(?:[A-Za-z0-9_<>,.\\?\\[\\]\\s]+?)\\s+(?<name>[A-Z][A-Za-z0-9_]*)\\s*\\(")]
    private static partial Regex ResolverMethodRegex();

    [GeneratedRegex("\\[\\s*GraphQLName\\s*\\(\\s*\"(?<name>[^\"]+)\"\\s*\\)\\s*\\]", RegexOptions.IgnoreCase)]
    private static partial Regex GraphQlNameRegex();

    [GeneratedRegex("public\\s+(?:async\\s+)?Task(?:<[^>]+>)?\\s+(?<name>[A-Za-z0-9_]+)\\s*\\(", RegexOptions.Multiline)]
    private static partial Regex TestMethodRegex();
}
