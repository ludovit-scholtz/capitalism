using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

public sealed partial class PublicSalesPhase
{
    private static async Task EmitMarketSignalNotificationsAsync(
        TickContext context,
        City city,
        Guid itemId,
        List<SalesOffer> groupList,
        decimal effectiveCityDemand,
        decimal groupTotalSold)
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

        var currentRows = context.Db.PublicSalesRecords.Local
            .Where(record =>
                record.Tick == context.CurrentTick
                && record.CityId == city.Id
                && ((record.ProductTypeId.HasValue && record.ProductTypeId == itemId)
                    || (record.ResourceTypeId.HasValue && record.ResourceTypeId == itemId)))
            .ToList();

        var currentRevenue = currentRows.Sum(record => record.Revenue);
        var currentQuantity = currentRows.Sum(record => record.QuantitySold);
        var currentAveragePrice = currentQuantity > 0m ? currentRevenue / currentQuantity : 0m;

        if (context.CurrentTick > 0)
        {
            var previousRows = await context.Db.PublicSalesRecords
                .Where(record =>
                    record.CityId == city.Id
                    && record.Tick == context.CurrentTick - 1
                    && ((record.ProductTypeId.HasValue && record.ProductTypeId == itemId)
                        || (record.ResourceTypeId.HasValue && record.ResourceTypeId == itemId)))
                .ToListAsync();
            var previousQuantity = previousRows.Sum(record => record.QuantitySold);
            if (previousQuantity > 0m)
            {
                var previousAveragePrice = previousRows.Sum(record => record.Revenue) / previousQuantity;
                if (previousAveragePrice > 0m && currentAveragePrice > previousAveragePrice * 1.3m)
                {
                    foreach (var companyOffer in distinctCompanies)
                    {
                        if (PlayerNotificationService.HasUnreadDuplicate(
                            context.Db,
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
                if (PlayerNotificationService.HasUnreadDuplicate(
                    context.Db,
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
