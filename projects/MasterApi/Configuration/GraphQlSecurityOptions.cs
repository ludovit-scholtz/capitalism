namespace MasterApi.Configuration;

public sealed class GraphQlSecurityOptions
{
    public const string SectionName = "GraphQL";

    public int MaxDepth { get; init; } = 10;

    public int MaxComplexity { get; init; } = 1000;

    public int MaxPageSize { get; init; } = 100;
}
