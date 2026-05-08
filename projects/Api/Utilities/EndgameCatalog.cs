namespace Api.Utilities;

public sealed record RealWorldBillionaireBenchmark(string Name, decimal WealthUsd);

public static class EndgameCatalog
{
    /// <summary>
    /// Fallback spot price used when no AMM pool currently provides a valid implied gold price.
    /// </summary>
    public const decimal DefaultGoldPriceUsd = 3_000m;

    public static readonly IReadOnlyList<RealWorldBillionaireBenchmark> DefaultTopTenRichestPeople =
    [
        new("Elon Musk", 430_000_000_000m),
        new("Jeff Bezos", 245_000_000_000m),
        new("Mark Zuckerberg", 216_000_000_000m),
        new("Larry Ellison", 192_000_000_000m),
        new("Bernard Arnault", 178_000_000_000m),
        new("Larry Page", 144_000_000_000m),
        new("Sergey Brin", 138_000_000_000m),
        new("Warren Buffett", 133_000_000_000m),
        new("Steve Ballmer", 130_000_000_000m),
        new("Jensen Huang", 116_000_000_000m),
    ];

    public const decimal DefaultWinningThresholdUsd = 430_000_000_000m;
}
