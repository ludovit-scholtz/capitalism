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
        await BuildingConfigurationService.ApplyDuePlansAsync(context.Db, context.CurrentTick);
    }
}
