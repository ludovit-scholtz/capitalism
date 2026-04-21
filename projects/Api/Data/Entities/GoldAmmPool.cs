using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// A constant-product AMM liquidity pool for a fiat currency / XAU (gold) pair.
/// One pool exists per fiat currency code (e.g. EUR/XAU, CZK/XAU).
/// The AMM invariant: FiatReserve * GoldReserve = K (constant product).
/// </summary>
public sealed class GoldAmmPool
{
    public Guid Id { get; set; }
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
    /// <summary>Total fiat currency held in the pool.</summary>
    public decimal FiatReserve { get; set; }
    /// <summary>Total gold (XAU) held in the pool.</summary>
    public decimal GoldReserve { get; set; }
    /// <summary>Total outstanding LP shares (Uniswap v2 style: sqrt(fiat * gold) for first deposit).</summary>
    public decimal TotalLiquidityShares { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public ICollection<GoldAmmPosition> Positions { get; set; } = new List<GoldAmmPosition>();
}
