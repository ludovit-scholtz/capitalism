using System.Text.Json.Serialization;

namespace GraphQLSurfaceInventory;

internal sealed record CoverageStatus(
    bool HasNegativeCoverage,
    bool HasPositiveCoverage,
    IReadOnlyList<string> NegativeTests,
    IReadOnlyList<string> PositiveTests);

internal sealed record OperationInventory(
    string Kind,
    string MethodName,
    string GraphQlName,
    string Domain,
    string SourceFile,
    bool HasExplicitAuthorize,
    CoverageStatus Coverage)
{
    [JsonIgnore]
    public string Key => $"{Kind}:{GraphQlName}";

    [JsonIgnore]
    public bool IsSensitive => Domain is "finance" or "shareholder" or "ranking" or "lending" or "admin";
}

internal sealed record InventorySnapshot(
    string GeneratedAtUtc,
    IReadOnlyList<OperationInventory> Operations);

internal sealed record MissingCoverage(OperationInventory Operation, string Reason);
