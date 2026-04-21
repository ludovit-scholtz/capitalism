namespace Api.Data.Entities;

/// <summary>
/// Tracks a player's liquidity position in a GoldAmmPool.
/// Represents the player's share of pool reserves and tracks how much fiat/gold they provided.
/// </summary>
public sealed class GoldAmmPosition
{
    public Guid Id { get; set; }
    public Guid PoolId { get; set; }
    public GoldAmmPool Pool { get; set; } = null!;
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    /// <summary>LP shares owned by this player in the pool.</summary>
    public decimal LiquidityShares { get; set; }
    /// <summary>Amount of fiat currency provided (used for blocked-resource enforcement).</summary>
    public decimal FiatProvided { get; set; }
    /// <summary>Amount of gold (XAU) provided (used for blocked-resource enforcement).</summary>
    public decimal GoldProvided { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
