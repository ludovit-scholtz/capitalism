namespace Api.Configuration;

public sealed class SeedDataOptions
{
    public const string SectionName = "SeedData";

    public string AdminEmail { get; init; } = "admin@capitalism.local";
    public string AdminDisplayName { get; init; } = "Platform Admin";
    public string AdminPassword { get; init; } = "__SET_IN_ENV__";
    public int TickIntervalSeconds { get; set; } = 10;
}
