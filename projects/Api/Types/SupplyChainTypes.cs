namespace Api.Types;

/// <summary>
/// Health score for a building's supply chain: RED (critical stalls),
/// YELLOW (early stalls), or GREEN (operating normally).
/// </summary>
public enum SupplyChainHealth
{
    Green,
    Yellow,
    Red
}

/// <summary>
/// Represents a unit within a supply chain diagram, including its
/// operational status and resource fill level.
/// </summary>
public sealed class SupplyChainUnitSummary
{
    /// <summary>Unit ID and basic info.</summary>
    public Guid BuildingUnitId { get; set; }

    /// <summary>Unit type (PURCHASE, MANUFACTURING, STORAGE, etc.).</summary>
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid position on the 4×4 building grid.</summary>
    public int GridX { get; set; }
    public int GridY { get; set; }

    /// <summary>Unit level (1-n).</summary>
    public int Level { get; set; }

    /// <summary>
    /// Operational status: ACTIVE, IDLE, BLOCKED, FULL, or UNCONFIGURED.
    /// </summary>
    public string Status { get; set; } = "UNCONFIGURED";

    /// <summary>Number of consecutive ticks with no activity.</summary>
    public int IdleTicks { get; set; }

    /// <summary>Current inventory fill percentage (0-100).</summary>
    public decimal FillPercent { get; set; }

    /// <summary>Resource or product type held in this unit.</summary>
    public Guid? ResourceTypeId { get; set; }
    public Guid? ProductTypeId { get; set; }

    /// <summary>Resource/product name (for display).</summary>
    public string? ResourceOrProductName { get; set; }

    /// <summary>Estimated transit cost to send output from this unit.</summary>
    public decimal? EstimatedTransitCost { get; set; }
}

/// <summary>
/// Represents a directional link from one unit to another with transit cost information.
/// </summary>
public sealed class SupplyChainLink
{
    /// <summary>Source unit ID.</summary>
    public Guid FromUnitId { get; set; }

    /// <summary>Destination unit ID.</summary>
    public Guid ToUnitId { get; set; }

    /// <summary>Link direction: RIGHT, DOWN, DIAGONAL_DR, etc.</summary>
    public string Direction { get; set; } = string.Empty;

    /// <summary>Estimated transit cost per unit moved across this link.</summary>
    public decimal EstimatedTransitCost { get; set; }
}

/// <summary>
/// Complete supply chain diagram for a factory building showing all linked units,
/// their operational status, inventory fill levels, and inter-unit connections.
/// </summary>
public sealed class BuildingSupplyChainDiagram
{
    /// <summary>Building ID.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Building name.</summary>
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>Building type (should be FACTORY).</summary>
    public string BuildingType { get; set; } = string.Empty;

    /// <summary>All units in this building's supply chain.</summary>
    public List<SupplyChainUnitSummary> Units { get; set; } = new();

    /// <summary>All directional links between units.</summary>
    public List<SupplyChainLink> Links { get; set; } = new();

    /// <summary>Overall supply chain health: RED/YELLOW/GREEN.</summary>
    public SupplyChainHealth HealthScore { get; set; } = SupplyChainHealth.Green;

    /// <summary>Detailed health reason for UI display.</summary>
    public string HealthReason { get; set; } = string.Empty;

    /// <summary>IDs of units that are currently in critical stall (RED).</summary>
    public List<Guid> CriticalUnitIds { get; set; } = new();

    /// <summary>IDs of units that are in warning stall (YELLOW).</summary>
    public List<Guid> WarningUnitIds { get; set; } = new();
}
