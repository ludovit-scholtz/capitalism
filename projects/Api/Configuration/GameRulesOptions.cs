namespace Api.Configuration;

public sealed class GameRulesOptions
{
    public const string SectionName = "GameRules";
    public decimal BillionaireNetWorthBenchmarkUsd { get; set; } = 200_000_000_000m;
}
