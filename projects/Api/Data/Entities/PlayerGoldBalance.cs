using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Tracks a player's personal XAU (gold token) balance on the game server.
/// Gold is the special in-game safe-haven asset backed 1:1 by 1 gram of real gold.
/// </summary>
public sealed class PlayerGoldBalance
{
    public Guid Id { get; set; }
    public Guid PlayerId { get; set; }
    public Player Player { get; set; } = null!;
    /// <summary>Total gold balance (XAU). Always >= 0.</summary>
    public decimal Balance { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
