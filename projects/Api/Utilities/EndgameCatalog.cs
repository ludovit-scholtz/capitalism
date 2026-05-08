namespace Api.Utilities;

public sealed record RealWorldBillionaireBenchmark(string Name, decimal WealthUsd);

public static class EndgameCatalog
{
    public static readonly IReadOnlyList<RealWorldBillionaireBenchmark> TopFiveRichestPeople =
    [
        new("Elon Musk", 430_000_000_000m),
        new("Jeff Bezos", 245_000_000_000m),
        new("Mark Zuckerberg", 216_000_000_000m),
        new("Larry Ellison", 192_000_000_000m),
        new("Bernard Arnault", 178_000_000_000m),
    ];

    public static decimal WinningThresholdUsd => TopFiveRichestPeople.Min(item => item.WealthUsd);
}
