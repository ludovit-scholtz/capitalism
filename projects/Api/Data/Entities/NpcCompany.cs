using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Autonomous AI-controlled competitor company profile.
/// Linked 1:1 with a regular Company entity that participates in the economy.
/// </summary>
public sealed class NpcCompany
{
    public Guid Id { get; set; }

    /// <summary>Backing company that owns buildings, inventory, and bank accounts.</summary>
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    /// <summary>Home city used by default expansion logic.</summary>
    public Guid HomeCityId { get; set; }
    public City HomeCity { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string Archetype { get; set; } = NpcArchetype.Conglomerate;

    /// <summary>Difficulty level from 1 (easy) to 5 (ruthless).</summary>
    public int DifficultyLevel { get; set; } = 2;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<NpcDecisionLog> DecisionLogs { get; set; } = [];
}

public static class NpcArchetype
{
    public const string RawMaterials = "RAW_MATERIALS";
    public const string Manufacturer = "MANUFACTURER";
    public const string Retailer = "RETAILER";
    public const string Financier = "FINANCIER";
    public const string Conglomerate = "CONGLOMERATE";

    public static readonly string[] All = [RawMaterials, Manufacturer, Retailer, Financier, Conglomerate];
}

