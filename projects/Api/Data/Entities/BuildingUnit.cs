using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a unit placed in a building's 4x4 grid.
/// Units define the operational capabilities of a building.
/// Grid positions range from (0,0) to (3,3).
/// </summary>
public sealed class BuildingUnit
{
    /// <summary>Unique identifier for the unit.</summary>
    public Guid Id { get; set; }

    /// <summary>The building this unit belongs to.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the parent building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>
    /// Unit type determining its function. Valid types depend on building type:
    /// Mine: MINING, STORAGE, B2B_SALES
    /// Factory: PURCHASE, MANUFACTURING, BRANDING, STORAGE, B2B_SALES
    /// Sales Shop: PURCHASE, MARKETING, PUBLIC_SALES
    /// R&amp;D: PRODUCT_QUALITY, BRAND_QUALITY
    /// </summary>
    [Required, MaxLength(30)]
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>Current upgrade level of this unit.</summary>
    public int Level { get; set; } = 1;

    /// <summary>Whether the link to the unit above is active.</summary>
    public bool LinkUp { get; set; }

    /// <summary>Whether the link to the unit below is active.</summary>
    public bool LinkDown { get; set; }

    /// <summary>Whether the link to the unit on the left is active.</summary>
    public bool LinkLeft { get; set; }

    /// <summary>Whether the link to the unit on the right is active.</summary>
    public bool LinkRight { get; set; }

    /// <summary>Whether the diagonal link to the unit above-left is active.</summary>
    public bool LinkUpLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit above-right is active.</summary>
    public bool LinkUpRight { get; set; }

    /// <summary>Whether the diagonal link to the unit below-left is active.</summary>
    public bool LinkDownLeft { get; set; }

    /// <summary>Whether the diagonal link to the unit below-right is active.</summary>
    public bool LinkDownRight { get; set; }
}

/// <summary>Defines valid unit types for each building type.</summary>
public static class UnitType
{
    // Mine units
    public const string Mining = "MINING";

    // Shared units
    public const string Storage = "STORAGE";
    public const string B2BSales = "B2B_SALES";
    public const string Purchase = "PURCHASE";

    // Factory units
    public const string Manufacturing = "MANUFACTURING";
    public const string Branding = "BRANDING";

    // Sales shop units
    public const string Marketing = "MARKETING";
    public const string PublicSales = "PUBLIC_SALES";

    // R&D units
    public const string ProductQuality = "PRODUCT_QUALITY";
    public const string BrandQuality = "BRAND_QUALITY";
}
