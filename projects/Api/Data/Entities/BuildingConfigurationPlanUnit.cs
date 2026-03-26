using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Snapshot of a single unit inside a queued building configuration upgrade.
/// </summary>
public sealed class BuildingConfigurationPlanUnit
{
    /// <summary>Unique identifier for the queued unit snapshot.</summary>
    public Guid Id { get; set; }

    /// <summary>The queued configuration this unit belongs to.</summary>
    public Guid BuildingConfigurationPlanId { get; set; }

    /// <summary>Navigation property to the queued configuration.</summary>
    public BuildingConfigurationPlan BuildingConfigurationPlan { get; set; } = null!;

    /// <summary>Unit type that will be active after the upgrade applies.</summary>
    [Required, MaxLength(30)]
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>Upgrade level that will be active after the upgrade applies.</summary>
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

    /// <summary>Ticks this unit is unavailable while the upgrade is processed.</summary>
    public int TicksRequired { get; set; }

    /// <summary>Whether this snapshot differs from the currently active unit at the same grid position.</summary>
    public bool IsChanged { get; set; }
}
