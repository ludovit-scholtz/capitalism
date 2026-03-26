using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Defines a type of raw material that can be mined or used in manufacturing.
/// Examples: Coal, Iron Ore, Gold, Chemical Minerals, Wood.
/// </summary>
public sealed class ResourceType
{
    /// <summary>Unique identifier for the resource type.</summary>
    public Guid Id { get; set; }

    /// <summary>Name of the resource (e.g., "Coal", "Iron Ore").</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-friendly identifier.</summary>
    [Required, MaxLength(100)]
    public string Slug { get; set; } = string.Empty;

    /// <summary>Category of the resource: RAW_MATERIAL, MINERAL, ORGANIC.</summary>
    [Required, MaxLength(30)]
    public string Category { get; set; } = "RAW_MATERIAL";

    /// <summary>Base price per unit on the exchange.</summary>
    public decimal BasePrice { get; set; }

    /// <summary>Weight per unit in kg (affects transport costs).</summary>
    public decimal WeightPerUnit { get; set; }

    /// <summary>Display name for the base trade unit (e.g. Ton, Kilogram).</summary>
    [MaxLength(50)]
    public string UnitName { get; set; } = "Ton";

    /// <summary>Short display symbol for the base trade unit (e.g. t, kg).</summary>
    [MaxLength(20)]
    public string UnitSymbol { get; set; } = "t";

    /// <summary>Preview image used in the manufacturing encyclopaedia.</summary>
    [MaxLength(6000)]
    public string? ImageUrl { get; set; }

    /// <summary>Description shown in the encyclopaedia.</summary>
    [MaxLength(1000)]
    public string? Description { get; set; }
}
