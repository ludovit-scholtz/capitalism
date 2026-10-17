using Api.Data.Entities;
using Api.Engine;

namespace Api.Tests;

public sealed class TickContextOperationalEfficiencyTests
{
    private static TickContext ContextAtTick(long tick) => new()
    {
        Db = null!,
        GameState = new GameState { CurrentTick = tick },
    };

    private static Building Powered(decimal staffing = 1m, long? strikeUntil = null) => new()
    {
        Id = Guid.NewGuid(),
        PowerStatus = PowerStatus.Powered,
        StaffingFactor = staffing,
        StrikeStartedAtTick = strikeUntil.HasValue ? 1 : null,
        StrikeUntilTick = strikeUntil,
    };

    [Fact]
    public void FullyStaffedPoweredBuilding_RunsAtFullEfficiency()
    {
        Assert.Equal(1m, ContextAtTick(5).GetOperationalEfficiency(Powered()));
    }

    [Fact]
    public void Staffing_ScalesEfficiency()
    {
        Assert.Equal(0.4m, ContextAtTick(5).GetOperationalEfficiency(Powered(staffing: 0.4m)));
    }

    [Fact]
    public void StrikeCoversExactlyTheComputedNumberOfTicks()
    {
        // The labor phase of tick 10 starts an 8-tick strike: until = 10 + 8 = 18. Ticks 11..18 are struck, 19 is not.
        var building = Powered(strikeUntil: 18);

        for (var tick = 11L; tick <= 18; tick++)
        {
            Assert.Equal(0m, ContextAtTick(tick).GetOperationalEfficiency(building));
        }

        Assert.Equal(1m, ContextAtTick(19).GetOperationalEfficiency(building));
    }

    [Fact]
    public void OfflineBuilding_StaysAtZero()
    {
        var offline = Powered();
        offline.PowerStatus = PowerStatus.Offline;

        Assert.Equal(0m, ContextAtTick(5).GetOperationalEfficiency(offline));
    }
}
