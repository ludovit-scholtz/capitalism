using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a company owned by a player. Players can own multiple companies.
/// Companies own buildings and accumulate wealth through operations.
/// </summary>
public sealed class Company
{
    /// <summary>Unique identifier for the company.</summary>
    public Guid Id { get; set; }

    /// <summary>The player who owns this company.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>Navigation property to the owning player.</summary>
    public Player Player { get; set; } = null!;

    /// <summary>Company name displayed in the game.</summary>
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Available cash balance in game currency.</summary>
    public decimal Cash { get; set; }

    /// <summary>Total issued shares used for ownership, exchange pricing, and dividend distribution.</summary>
    public decimal TotalSharesIssued { get; set; } = 10_000m;

    /// <summary>Portion of post-tax annual profit paid out as dividends. Stored as a 0–1 ratio.</summary>
    public decimal DividendPayoutRatio { get; set; } = 0.2m;

    /// <summary>UTC timestamp when the company was founded.</summary>
    public DateTime FoundedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Authoritative in-game tick when the company was founded.</summary>
    public long FoundedAtTick { get; set; }

    /// <summary>Buildings owned by this company.</summary>
    public ICollection<Building> Buildings { get; set; } = new List<Building>();

    /// <summary>Per-city salary multipliers chosen by the player.</summary>
    public ICollection<CompanyCitySalarySetting> CitySalarySettings { get; set; } = new List<CompanyCitySalarySetting>();

    /// <summary>Share positions representing direct ownership in this company.</summary>
    public ICollection<Shareholding> Shareholdings { get; set; } = new List<Shareholding>();

    /// <summary>Dividend payments made by this company to shareholders.</summary>
    public ICollection<DividendPayment> DividendPayments { get; set; } = new List<DividendPayment>();
}
