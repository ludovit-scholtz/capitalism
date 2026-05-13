using Api.Data.Entities;

namespace Api.Engine.Phases;

/// <summary>
/// Collects rent revenue from APARTMENT and COMMERCIAL buildings, applies constant
/// operating costs, and adjusts occupancy toward equilibrium.
///
/// ROADMAP rules implemented:
/// • Occupancy never drops below 50% due to overpricing (50% floor).
/// • Max achievable occupancy is 90% when priced at the location-adjusted market rate
///   + 10%; drops further to 50% above that threshold.
/// • Max achievable occupancy is 100% when priced below 60% of the adjusted rate.
/// • Constant operating costs equal rent income at 75% occupancy, so the property
///   breaks even at 75% occupancy and is profitable above it.
/// • The market rate is adjusted for the lot's PopulationIndex (location advantage).
/// </summary>
public sealed class RentPhase : ITickPhase
{
    public string Name => "Rent";
    public int Order => 900;

    public Task ProcessAsync(TickContext context)
    {
        ProcessBuildingType(context, BuildingType.Apartment);
        ProcessBuildingType(context, BuildingType.Commercial);
        return Task.CompletedTask;
    }

    private static void ProcessBuildingType(TickContext context, string buildingType)
    {
        if (!context.BuildingsByType.TryGetValue(buildingType, out var buildings))
            return;

        foreach (var building in buildings)
        {
            // Activate pending rent change if the activation tick has been reached.
            if (building.PendingPricePerSqm.HasValue
                && building.PendingPriceActivationTick.HasValue
                && context.CurrentTick >= building.PendingPriceActivationTick.Value)
            {
                building.PricePerSqm = building.PendingPricePerSqm.Value;
                building.PendingPricePerSqm = null;
                building.PendingPriceActivationTick = null;
            }

            if (!building.OccupancyPercent.HasValue)
            {
                building.OccupancyPercent = GameConstants.PropertyInitialOccupancyPercent;
            }

            if (!building.TotalAreaSqm.HasValue || building.TotalAreaSqm.Value <= 0m)
            {
                building.TotalAreaSqm = GameConstants.DefaultPropertyAreaSqm(building.Type) ?? 0m;
            }

            if (building.PricePerSqm is null || building.TotalAreaSqm is null || building.TotalAreaSqm.Value <= 0m || building.OccupancyPercent is null)
                continue;
            if (!context.CompaniesById.TryGetValue(building.CompanyId, out var company))
                continue;
            if (!context.CitiesById.TryGetValue(building.CityId, out var city))
                continue;

            var cityBaseRate = city.AverageRentPerSqm;
            if (cityBaseRate <= 0m) continue;

            // Apply the lot's PopulationIndex to derive the location-adjusted market rate.
            // Buildings in prime locations (high index) have a higher reference rate.
            var populationIndex = context.LotsByBuildingId.TryGetValue(building.Id, out var lot)
                && lot.PopulationIndex > 0m ? lot.PopulationIndex : 1m;
            var adjustedMarketRate = cityBaseRate * populationIndex;

            var fundingAccount = context.GetBuildingFundingAccount(building);

            // ── Constant operating costs ────────────────────────────────────────────
            // Costs are set equal to rent revenue at 75% occupancy, so the building
            // breaks even at 75% and is profitable above it.
            var constantCosts = building.PricePerSqm.Value
                                * building.TotalAreaSqm.Value
                                * GameConstants.PropertyBreakevenOccupancy;

            if (constantCosts > 0m && fundingAccount is not null)
            {
                fundingAccount.Balance -= constantCosts;
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = building.Id,
                    BankAccountId = fundingAccount.Id,
                    Category = LedgerCategory.PropertyMaintenance,
                    Description = $"Property maintenance – {building.Name}",
                    Amount = -constantCosts,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // ── Rent income ─────────────────────────────────────────────────────────
            var rentIncome = building.PricePerSqm.Value
                             * building.TotalAreaSqm.Value
                             * building.OccupancyPercent.Value / 100m;

            if (rentIncome > 0m)
            {
                if (fundingAccount is not null)
                {
                    fundingAccount.Balance += rentIncome;
                }

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = building.Id,
                    BankAccountId = fundingAccount?.Id,
                    Category = LedgerCategory.RentIncome,
                    Description = $"Rent income – {building.Name}",
                    Amount = rentIncome,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // ── Write RentalIncomeRecord for sparkline history ──────────────────────
            var currencyCode = city.CurrencyCode ?? "EUR";
            context.Db.RentalIncomeRecords.Add(new RentalIncomeRecord
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                Tick = context.CurrentTick,
                Revenue = rentIncome,
                Costs = constantCosts,
                OccupancyPercent = building.OccupancyPercent.Value,
                RentPerSqm = building.PricePerSqm.Value,
                CurrencyCode = currencyCode
            });

            // Prune old records — keep only the last 100 ticks per building.
            var pruneThreshold = context.CurrentTick - 100;
            var old = context.Db.RentalIncomeRecords.Local
                .Where(r => r.BuildingId == building.Id && r.Tick < pruneThreshold)
                .ToList();
            foreach (var rec in old) context.Db.RentalIncomeRecords.Remove(rec);

            // ── Occupancy adjustment ────────────────────────────────────────────────
            var priceRatio = building.PricePerSqm.Value / adjustedMarketRate;

            // Determine the maximum achievable occupancy based on pricing position.
            decimal maxOccupancy = ComputeMaxOccupancy(priceRatio);

            // Drift toward maxOccupancy at a rate proportional to the price deviation.
            var gap = maxOccupancy - building.OccupancyPercent.Value;
            if (Math.Abs(gap) > 0.001m)
            {
                // The adjustment speed is slower when going up (filling vacancies is slower
                // than losing tenants due to overpricing).
                var adjustmentMultiplier = gap > 0m ? 0.5m : 1.0m;
                var delta = Math.Abs(priceRatio - 1m) * GameConstants.OccupancyAdjustmentRate * adjustmentMultiplier;
                delta = Math.Max(delta, GameConstants.OccupancyAdjustmentRate * 0.1m); // minimum drift rate

                if (gap > 0m)
                    building.OccupancyPercent = Math.Min(maxOccupancy, building.OccupancyPercent.Value + delta);
                else
                    building.OccupancyPercent = Math.Max(maxOccupancy, building.OccupancyPercent.Value - delta);
            }
        }
    }

    /// <summary>
    /// Returns the maximum achievable occupancy for a building based on the ratio of
    /// the player's asking rent to the location-adjusted market rate.
    /// </summary>
    internal static decimal ComputeMaxOccupancy(decimal priceRatio)
    {
        if (priceRatio > GameConstants.OccupancyNinetyPctCapPriceRatio)
        {
            // Overpriced: occupancy floors at 50%
            return GameConstants.OccupancyOverpricedFloor;
        }

        if (priceRatio <= GameConstants.OccupancyFullCapPriceRatio)
        {
            // Below 60% of market: full occupancy possible
            return 100m;
        }

        // Between 60% and 110% of market: linear interpolation from 100% down to 90%.
        // At priceRatio=0.60 → 100%; at priceRatio=1.10 → 90%
        var range = GameConstants.OccupancyNinetyPctCapPriceRatio - GameConstants.OccupancyFullCapPriceRatio; // 0.50
        var factor = (priceRatio - GameConstants.OccupancyFullCapPriceRatio) / range;
        return 100m - factor * (100m - GameConstants.OccupancyNinetyPctCap); // 100 - factor * 10
    }
}
