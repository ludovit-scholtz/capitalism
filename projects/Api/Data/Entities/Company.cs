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

    /// <summary>UTC timestamp when the company was founded.</summary>
    public DateTime FoundedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Buildings owned by this company.</summary>
    public ICollection<Building> Buildings { get; set; } = [];
}
