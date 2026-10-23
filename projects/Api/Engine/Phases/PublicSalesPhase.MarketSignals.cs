using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

public sealed partial class PublicSalesPhase
{
    /// <summary>Quantity sold and revenue earned for one (city, item) over one tick.</summary>
    private sealed record MarketWindow(decimal Quantity, decimal Revenue);

    /// <summary>
    /// Loads last tick's sales per (city, item) for the given cities in a single grouped query. A record counts for its product and,
    /// when set, its resource, matching the per-group lookup this replaces.
    /// </summary>
    private static async Task<Dictionary<(Guid CityId, Guid ItemId), MarketWindow>> LoadPreviousTickMarketAsync(TickContext context, List<Guid> cityIds)
    {
        var result = new Dictionary<(Guid CityId, Guid ItemId), MarketWindow>();
        if (context.CurrentTick <= 0 || cityIds.Count == 0)
        {
            return result;
        }

        var previousTick = context.CurrentTick - 1;
        var rows = await context.Db.PublicSalesRecords
            .AsNoTracking()
            .Where(record => record.Tick == previousTick && cityIds.Contains(record.CityId))
            .GroupBy(record => new { record.CityId, record.ProductTypeId, record.ResourceTypeId })
            .Select(group => new
            {
                group.Key.CityId,
                group.Key.ProductTypeId,
                group.Key.ResourceTypeId,
                Quantity = group.Sum(record => record.QuantitySold),
                Revenue = group.Sum(record => record.Revenue),
            })
            .ToListAsync();

        foreach (var row in rows)
        {
            foreach (var itemId in new[] { row.ProductTypeId, row.ResourceTypeId }.Where(id => id.HasValue).Select(id => id!.Value).Distinct())
            {
                var key = (row.CityId, itemId);
                result[key] = result.TryGetValue(key, out var existing)
                    ? new MarketWindow(existing.Quantity + row.Quantity, existing.Revenue + row.Revenue)
                    : new MarketWindow(row.Quantity, row.Revenue);
            }
        }

        return result;
    }

    private static void EmitMarketSignalNotifications(
        TickContext context,
        City city,
        Guid itemId,
        List<SalesOffer> groupList,
        decimal effectiveCityDemand,
        decimal groupTotalSold,
        MarketWindow currentWindow,
        Dictionary<(Guid CityId, Guid ItemId), MarketWindow> previousTickMarket)
    {
        var soldOffers = groupList.Where(offer => offer.Inventory.ProductTypeId.HasValue && offer.ProductName is not null).ToList();
        if (soldOffers.Count == 0)
        {
            return;
        }

        var distinctCompanies = soldOffers
            .GroupBy(offer => offer.Company.Id)
            .Select(group => group.First())
            .ToList();

        // This group's records of the current tick, accumulated while the sales were made (the group is exactly one city and item).
        var currentAveragePrice = currentWindow.Quantity > 0m ? currentWindow.Revenue / currentWindow.Quantity : 0m;

        if (context.CurrentTick > 0)
        {
            previousTickMarket.TryGetValue((city.Id, itemId), out var previousWindow);
            var previousQuantity = previousWindow?.Quantity ?? 0m;
            if (previousQuantity > 0m)
            {
                var previousAveragePrice = previousWindow!.Revenue / previousQuantity;
                if (previousAveragePrice > 0m && currentAveragePrice > previousAveragePrice * 1.3m)
                {
                    foreach (var companyOffer in distinctCompanies)
                    {
                        if (context.HasUnreadNotification(
                            companyOffer.Company.PlayerId,
                            PlayerNotificationType.PriceSpike,
                            relatedEntityId: itemId,
                            companyId: companyOffer.Company.Id))
                        {
                            continue;
                        }

                        PlayerNotificationService.Add(
                            context.Db,
                            companyOffer.Company.PlayerId,
                            PlayerNotificationType.PriceSpike,
                            "Market price spike",
                            $"{companyOffer.ProductName ?? "Product"} selling price in {city.Name} rose by more than 30% in the last tick.",
                            context.CurrentTick,
                            companyOffer.Company.Id,
                            severity: PlayerNotificationSeverity.Warning,
                            relatedEntityType: "MARKET_ITEM",
                            relatedEntityId: itemId,
                            expiresAtUtc: DateTime.UtcNow.AddDays(7));
                    }
                }
            }
        }

        var satisfactionRate = effectiveCityDemand > 0m ? Math.Clamp(groupTotalSold / effectiveCityDemand, 0m, 1m) : 1m;
        if (satisfactionRate < 0.3m)
        {
            foreach (var companyOffer in distinctCompanies)
            {
                if (context.HasUnreadNotification(
                    companyOffer.Company.PlayerId,
                    PlayerNotificationType.OversupplyWarning,
                    relatedEntityId: itemId,
                    companyId: companyOffer.Company.Id))
                {
                    continue;
                }

                PlayerNotificationService.Add(
                    context.Db,
                    companyOffer.Company.PlayerId,
                    PlayerNotificationType.OversupplyWarning,
                    "Oversupply warning",
                    $"{companyOffer.ProductName ?? "Product"} demand satisfaction in {city.Name} dropped below 30%.",
                    context.CurrentTick,
                    companyOffer.Company.Id,
                    severity: PlayerNotificationSeverity.Warning,
                    relatedEntityType: "MARKET_ITEM",
                    relatedEntityId: itemId,
                    expiresAtUtc: DateTime.UtcNow.AddDays(7));
            }
        }
    }
}
