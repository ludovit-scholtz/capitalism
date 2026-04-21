using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Audit record for a gold AMM swap executed by a player.
/// Direction: "FIAT_TO_GOLD" (buy gold with fiat) or "GOLD_TO_FIAT" (sell gold for fiat).
/// </summary>
public sealed class GoldAmmTradeRecord
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    public Guid PoolId { get; set; }
    public GoldAmmPool Pool { get; set; } = null!;
    [Required, MaxLength(20)]
    public string Direction { get; set; } = string.Empty;
    [Required, MaxLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal InputAmount { get; set; }
    public decimal OutputAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal ImpliedPrice { get; set; }
    public long ExecutedAtTick { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}
