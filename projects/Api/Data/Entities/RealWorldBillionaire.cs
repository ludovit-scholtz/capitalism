using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Admin-managed benchmark list of real-world billionaires used for the endgame race target.
/// Rank 1 is the richest benchmark and defines the win threshold.
/// </summary>
public sealed class RealWorldBillionaire
{
    public Guid Id { get; set; }

    /// <summary>Ranking position where 1 is the richest benchmark.</summary>
    public int Rank { get; set; }

    [Required, MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Estimated net worth in USD.</summary>
    public decimal WealthUsd { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
