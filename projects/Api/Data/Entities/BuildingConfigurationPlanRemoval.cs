namespace Api.Data.Entities;

/// <summary>
/// Represents a pending change that will leave a grid position empty once it completes.
/// </summary>
public sealed class BuildingConfigurationPlanRemoval
{
    /// <summary>Unique identifier for the pending removal.</summary>
    public Guid Id { get; set; }

    /// <summary>The queued configuration this pending removal belongs to.</summary>
    public Guid BuildingConfigurationPlanId { get; set; }

    /// <summary>Navigation property to the queued configuration.</summary>
    public BuildingConfigurationPlan BuildingConfigurationPlan { get; set; } = null!;

    /// <summary>Grid column position (0-3).</summary>
    public int GridX { get; set; }

    /// <summary>Grid row position (0-3).</summary>
    public int GridY { get; set; }

    /// <summary>The tick when this removal or cancellation work started.</summary>
    public long StartedAtTick { get; set; }

    /// <summary>The tick when this position will resolve to its empty target state.</summary>
    public long AppliesAtTick { get; set; }

    /// <summary>Total ticks required for this pending removal or cancellation.</summary>
    public int TicksRequired { get; set; }

    /// <summary>Whether this removal exists only to cancel an in-progress change.</summary>
    public bool IsReverting { get; set; }
}