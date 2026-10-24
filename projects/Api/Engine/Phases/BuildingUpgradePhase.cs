using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies building configuration plans whose timers have completed.
/// Delegates to the existing <see cref="BuildingConfigurationService"/>.
/// </summary>
public sealed class BuildingUpgradePhase : ITickPhase
{
    public string Name => "BuildingUpgrade";
    public int Order => 100;

    public async Task ProcessAsync(TickContext context)
    {
        // The service loads the plans' buildings with their units from the database. Attaching our instances first makes it work on the
        // very same unit objects the other phases use, exactly as when every unit is tracked, so a unit it upgrades is already
        // upgraded for the phases that follow in this tick.
        context.AttachUnitsOfBuildingsWithPendingPlans();
        await BuildingConfigurationService.ApplyDuePlansAsync(context.Db, context.CurrentTick);
    }
}
