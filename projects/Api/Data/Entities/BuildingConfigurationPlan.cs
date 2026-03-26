using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a queued building configuration upgrade that becomes active on a future tick.
/// </summary>
public sealed class BuildingConfigurationPlan
{
    /// <summary>Unique identifier for the queued configuration.</summary>
    public Guid Id { get; set; }

    /// <summary>The building this queued configuration belongs to.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Navigation property to the building.</summary>
    public Building Building { get; set; } = null!;

    /// <summary>UTC timestamp when the upgrade was submitted.</summary>
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Current game tick when the upgrade was submitted.</summary>
    public long SubmittedAtTick { get; set; }

    /// <summary>Game tick when the queued configuration becomes active.</summary>
    public long AppliesAtTick { get; set; }

    /// <summary>Total ticks required for the whole configuration to apply.</summary>
    public int TotalTicksRequired { get; set; }

    /// <summary>Snapshot of units that will become active once the upgrade is applied.</summary>
    public ICollection<BuildingConfigurationPlanUnit> Units { get; set; } = [];
}
