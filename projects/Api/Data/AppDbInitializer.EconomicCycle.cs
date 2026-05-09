using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private async Task EnsureEconomicCycleSeedAsync()
    {
        var existing = await dbContext.EconomicCycles.AnyAsync();
        if (existing) return;

        var currentTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        dbContext.EconomicCycles.Add(new EconomicCycle
        {
            Id = Guid.NewGuid(),
            Phase = EconomicCyclePhase.Expansion,
            PhaseStartedTick = currentTick,
            ExpectedDurationTicks = GameConstants.TicksPerMonth * 3,
            IntensityFactor = 1.2m,
        });
        await dbContext.SaveChangesAsync();
    }
}
