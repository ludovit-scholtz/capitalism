namespace Api.Data.Entities;

/// <summary>
/// Tracks the scheduled replenishment cycle for mining lots in each city.
/// One row exists per city. Every <see cref="GameConstants.ReplenishmentIntervalTicks"/> ticks
/// (one game year = 8 760 ticks) a fraction of fully-depleted mine lots in the city is
/// partially restored, simulating geological discovery of secondary deposits.
/// </summary>
public sealed class ResourceReplenishmentSchedule
{
    /// <summary>Unique identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>The city this replenishment schedule belongs to.</summary>
    public Guid CityId { get; set; }

    /// <summary>Navigation property to the city.</summary>
    public City City { get; set; } = null!;

    /// <summary>Game tick on which the last replenishment event ran (0 = never).</summary>
    public long LastReplenishmentTick { get; set; }

    /// <summary>Game tick on which the next replenishment event is due.</summary>
    public long NextReplenishmentTick { get; set; }
}
