using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Records a share position in a company held either by a player's personal account or by another company.
/// Public float is represented implicitly as issued shares without a matching holding row.
/// </summary>
public sealed class Shareholding
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid? OwnerPlayerId { get; set; }
    public Player? OwnerPlayer { get; set; }

    public Guid? OwnerCompanyId { get; set; }
    public Company? OwnerCompany { get; set; }

    /// <summary>Number of issued shares held by the owner.</summary>
    [Range(typeof(decimal), "0", "999999999999999999")]
    public decimal ShareCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}