using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Runs the intra-city energy spot market each tick:
/// matches OFFLINE/CONSTRAINED buildings to cheapest available spot listings,
/// settles trades in city local currency, and writes ledger entries.
///
/// Order 12 — runs after PowerDistributionPhase (10) and before PowerGridEconomicsPhase (15).
/// </summary>
public sealed class EnergySpotMarketPhase : ITickPhase
{
    public string Name => "EnergySpotMarket";
    public int Order => 12;

    public async Task ProcessAsync(TickContext context)
    {
        var activeListings = await context.Db.EnergyListings
            .Where(l => l.IsActive)
            .Include(l => l.Building)
            .ToListAsync();

        if (activeListings.Count == 0)
            return;

        // Reset available capacity at the start of each tick.
        foreach (var listing in activeListings)
            listing.AvailableKw = listing.CapacityKw;

        // Group by city, cheapest-first within each city.
        var listingsByCity = activeListings
            .GroupBy(l => l.CityId)
            .ToDictionary(g => g.Key, g => g.OrderBy(l => l.PricePerKwhLocal).ToList());

        // Buildings that want spot-market power, sorted by priority.
        var needyBuildings = context.BuildingsById.Values
            .Where(b => b.Type != BuildingType.PowerPlant
                && b.MaxEnergyBidPrice.HasValue && b.MaxEnergyBidPrice.Value > 0m
                && (b.PowerStatus == PowerStatus.Offline || b.PowerStatus == PowerStatus.Constrained))
            .OrderByDescending(b => b.PowerPriority)
            .ThenBy(b => b.Id)
            .ToList();

        if (needyBuildings.Count == 0)
            return;

        foreach (var buyer in needyBuildings)
        {
            if (!listingsByCity.TryGetValue(buyer.CityId, out var cityListings))
                continue;
            if (!context.CitiesById.TryGetValue(buyer.CityId, out var city))
                continue;

            var demandKw = buyer.PowerConsumption * 1000m; // MW → kW
            var maxBid = buyer.MaxEnergyBidPrice!.Value;
            decimal purchasedKw = 0m;
            decimal totalCost = 0m;

            foreach (var listing in cityListings)
            {
                if (listing.PricePerKwhLocal > maxBid) break;
                if (listing.AvailableKw <= 0m) continue;

                var needed = demandKw - purchasedKw;
                if (needed <= 0m) break;

                var allocateKw = Math.Min(needed, listing.AvailableKw);
                var cost = decimal.Round(
                    allocateKw * listing.PricePerKwhLocal,
                    2, MidpointRounding.AwayFromZero);

                var buyerAccount = context.GetBuildingFundingAccount(buyer)
                    ?? context.GetCompanyFundingAccount(buyer.CompanyId, city.CurrencyCode);
                if (buyerAccount is null || buyerAccount.Balance < cost)
                    continue;

                buyerAccount.Balance -= cost;
                totalCost += cost;
                purchasedKw += allocateKw;
                listing.AvailableKw -= allocateKw;

                // Credit seller's account.
                var sellerPlant = listing.Building;
                BankAccount? sellerAccount = sellerPlant.BankAccountId.HasValue
                    && context.BankAccountsById.TryGetValue(sellerPlant.BankAccountId.Value, out var sa)
                    ? sa
                    : context.GetCompanyFundingAccount(listing.CompanyId, city.CurrencyCode);
                if (sellerAccount is not null)
                    sellerAccount.Balance += cost;

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = listing.CompanyId,
                    BuildingId = listing.BuildingId,
                    Category = LedgerCategory.EnergyRevenue,
                    Description = $"Spot market: sold {allocateKw:F1} kW @ {listing.PricePerKwhLocal:F4} {city.CurrencyCode}/kWh",
                    Amount = cost,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            if (purchasedKw <= 0m) continue;

            if (totalCost > 0m)
            {
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = buyer.CompanyId,
                    BuildingId = buyer.Id,
                    Category = LedgerCategory.SpotMarketEnergyCost,
                    Description = $"Spot market: bought {purchasedKw:F1} kW for {totalCost:F2} {city.CurrencyCode}",
                    Amount = -totalCost,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            if (demandKw > 0m && purchasedKw / demandKw >= 1m)
                buyer.PowerStatus = PowerStatus.Powered;
        }
    }
}
