using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Maintains the land market: keeps supply available, updates population index,
/// and reappraises land before public sales demand is calculated.
/// </summary>
public sealed class LandMarketPhase : ITickPhase
{
    public string Name => "LandMarket";
    public int Order => 150;

    public async Task ProcessAsync(TickContext context)
    {
        await LandService.EnsureMinimumAvailableLotsAsync(
            context.Db,
            context.CurrentTick,
            context.CitiesById.Keys);
    }
}