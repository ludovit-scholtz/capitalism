namespace Api.Types;

public sealed class EndgameStatusResult
{
    public bool GameEnded { get; set; }
    public Guid? WinnerPlayerId { get; set; }
    public string? WinnerDisplayName { get; set; }
    public string? WinnerCompanyName { get; set; }
    public DateTime? GameEndedAtUtc { get; set; }
    public decimal WinningThresholdUsd { get; set; }
    public List<RealWorldWealthResult> TopRealWorldRichest { get; set; } = [];

    /// <summary>Display name of the current server-wide leader (highest personal net worth).</summary>
    public string? LeaderDisplayName { get; set; }

    /// <summary>Current server-wide leader's personal net worth in USD (cash + portfolio).</summary>
    public decimal LeaderNetWorthUsd { get; set; }
}

public sealed class RealWorldWealthResult
{
    public Guid Id { get; set; }
    public int Rank { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal WealthUsd { get; set; }
}
