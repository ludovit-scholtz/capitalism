using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Completes building construction orders whose timers have expired.
/// Buildings created via the city-map purchase flow start as <c>IsUnderConstruction = true</c>
/// with a scheduled <c>ConstructionCompletesAtTick</c>.  This phase clears the flag so the
/// building becomes fully operational for all subsequent tick phases.
/// </summary>
public sealed class ConstructionPhase : ITickPhase
{
    public string Name => "Construction";

    /// <summary>
    /// Runs before all production phases (Order = 5) so that a building which completes
    /// on the current tick can already participate in mining, manufacturing, etc. this tick.
    /// </summary>
    public int Order => 5;

    public async Task ProcessAsync(TickContext context)
    {
        var completingBuildings = await context.Db.Buildings
            .Include(building => building.Company)
            .Where(b => b.IsUnderConstruction
                        && b.ConstructionCompletesAtTick.HasValue
                        && b.ConstructionCompletesAtTick.Value <= context.CurrentTick)
            .ToListAsync();

        foreach (var building in completingBuildings)
        {
            building.IsUnderConstruction = false;
            building.ConstructionCompletesAtTick = null;

            PlayerNotificationService.Add(
                context.Db,
                building.Company.PlayerId,
                PlayerNotificationType.BuildingConstructionCompleted,
                "Construction completed",
                $"{building.Name} is now operational.",
                context.CurrentTick,
                building.CompanyId,
                building.Id,
                bankAccountId: building.BankAccountId);
        }
    }
}
