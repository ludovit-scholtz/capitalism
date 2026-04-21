using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Processes all MEDIA_HOUSE buildings each tick:
/// <list type="number">
///   <item>Decays each house's <see cref="Building.ContentValue"/> by 0.5% (<see cref="GameConstants.MediaHouseContentDecayRate"/>).</item>
///   <item>
///     When a house has a positive <see cref="Building.ContentBudgetPerTick"/>, deducts
///     the configured spend from the owning company's cash and converts a fraction of it
///     to content using the level-based efficiency formula:
///     <c>efficiency = 1 – 1/(level+1)</c>  (50% at level 1, 66% at level 2, …).
///   </item>
///   <item>Records a ledger entry for each spending event.</item>
/// </list>
/// Runs at order 650 — after OperatingCostPhase (500) and before MarketingPhase (700)
/// so the content ranking is current when marketing effectiveness is computed.
/// </summary>
public sealed class MediaHouseContentPhase : ITickPhase
{
    public string Name => "MediaHouseContent";
    public int Order => 650;

    public Task ProcessAsync(TickContext context)
    {
        if (!context.BuildingsByType.TryGetValue(BuildingType.MediaHouse, out var mediaHouses))
            return Task.CompletedTask;

        foreach (var building in mediaHouses)
        {
            // Skip buildings under construction — no production, no decay.
            if (building.IsUnderConstruction) continue;

            // 1. Decay existing content value.
            if (building.ContentValue > 0m)
            {
                building.ContentValue = Math.Max(0m, building.ContentValue * (1m - GameConstants.MediaHouseContentDecayRate));
            }

            // 2. Apply content budget spend.
            if (building.ContentBudgetPerTick is null or <= 0m) continue;
            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company)) continue;
            if (company.Cash <= 0m) continue;

            var spend = Math.Min(building.ContentBudgetPerTick.Value, company.Cash);
            if (spend <= 0m) continue;

            var efficiency = GameConstants.MediaHouseContentEfficiency(building.Level);
            var contentGain = spend * efficiency;

            company.Cash -= spend;
            building.ContentValue += contentGain;

            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.MediaHouseContent,
                Description = $"Content investment ({building.MediaType?.ToLowerInvariant() ?? "media"}) – {efficiency:P0} efficiency",
                Amount = -spend,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        return Task.CompletedTask;
    }
}
