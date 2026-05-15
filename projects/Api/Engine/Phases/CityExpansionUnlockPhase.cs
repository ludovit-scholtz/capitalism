using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Detects when companies first cross city-expansion thresholds and persists the unlock + player notification.
/// </summary>
public sealed class CityExpansionUnlockPhase : ITickPhase
{
    public string Name => "CityExpansionUnlock";
    public int Order => 1125;

    public async Task ProcessAsync(TickContext context)
    {
        var cities = context.CitiesById.Values.ToList();
        if (cities.Count == 0)
        {
            return;
        }

        var requirements = await context.Db.CityUnlockRequirements
            .AsNoTracking()
            .Where(requirement => requirement.RequiredNetWorthUsd > 0m)
            .ToDictionaryAsync(requirement => requirement.CityId);

        if (requirements.Count == 0)
        {
            return;
        }

        var companies = await context.Db.Companies
            .AsNoTracking()
            .Include(company => company.Player)
            .Include(company => company.Buildings)
                .ThenInclude(building => building.City)
            .Include(company => company.BankAccounts)
            .ToListAsync();

        if (companies.Count == 0)
        {
            return;
        }

        var ownedLots = await context.Db.BuildingLots
            .AsNoTracking()
            .Include(lot => lot.City)
            .Where(lot => lot.OwnerCompanyId.HasValue)
            .ToListAsync();

        var companyIds = companies.Select(company => company.Id).ToList();
        var buildingIds = companies.SelectMany(company => company.Buildings).Select(building => building.Id).ToList();
        var inventories = buildingIds.Count == 0
            ? []
            : await context.Db.Inventories
                .AsNoTracking()
                .Include(inventory => inventory.ResourceType)
                .Include(inventory => inventory.ProductType)
                .Where(inventory => buildingIds.Contains(inventory.BuildingId))
                .ToListAsync();

        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(
            context.Db,
            cities.Select(city => city.CurrencyCode)
                .Concat(companies.SelectMany(company => company.BankAccounts).Select(account => account.CurrencyCode))
                .Append("USD"));

        var existingUnlocks = await context.Db.CompanyCityUnlocks
            .Where(unlock => companyIds.Contains(unlock.CompanyId))
            .Select(unlock => new { unlock.CompanyId, unlock.CityId })
            .ToListAsync();
        var existingUnlockSet = existingUnlocks
            .Select(unlock => (unlock.CompanyId, unlock.CityId))
            .ToHashSet();

        foreach (var company in companies)
        {
            var companyBuildingIds = company.Buildings.Select(building => building.Id).ToHashSet();
            var primaryCurrencyCode = company.Buildings
                .Select(building => building.City?.CurrencyCode)
                .Concat(company.BankAccounts.Select(account => account.CurrencyCode))
                .FirstOrDefault(currencyCode => !string.IsNullOrWhiteSpace(currencyCode))
            ?? "EUR";

            var companyLots = ownedLots.Where(lot => lot.OwnerCompanyId == company.Id).ToList();
            var companyInventories = inventories.Where(inventory => companyBuildingIds.Contains(inventory.BuildingId)).ToList();
            var currentNetWorthUsd = CityUnlockService.ComputeCompanyNetWorthUsd(
                company,
                companyLots,
                companyInventories,
                eurRates,
                primaryCurrencyCode);

            var companyOwnedCityIds = company.Buildings.Select(building => building.CityId)
                .Concat(companyLots.Select(lot => lot.CityId))
                .ToHashSet();

            foreach (var city in cities)
            {
                if (!requirements.TryGetValue(city.Id, out var requirement))
                {
                    continue;
                }

                if (existingUnlockSet.Contains((company.Id, city.Id)) || companyOwnedCityIds.Contains(city.Id))
                {
                    continue;
                }

                if (currentNetWorthUsd < requirement.RequiredNetWorthUsd)
                {
                    continue;
                }

                context.Db.CompanyCityUnlocks.Add(new CompanyCityUnlock
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    CityId = city.Id,
                    UnlockedAtTick = context.CurrentTick,
                    UnlockedAtUtc = DateTime.UtcNow,
                });

                existingUnlockSet.Add((company.Id, city.Id));

                if (!PlayerNotificationService.HasUnreadDuplicate(
                        context.Db,
                        company.PlayerId,
                        PlayerNotificationType.CityExpansionUnlocked,
                        relatedEntityId: city.Id,
                        companyId: company.Id))
                {
                    PlayerNotificationService.Add(
                        context.Db,
                        company.PlayerId,
                        PlayerNotificationType.CityExpansionUnlocked,
                        $"🎉 {city.Name} is now unlocked!",
                        $"You can now expand {company.Name} into {city.Name}.",
                        context.CurrentTick,
                        companyId: company.Id,
                        severity: PlayerNotificationSeverity.Info,
                        titleKey: "cityExpansion.notificationTitle",
                        bodyKey: "cityExpansion.notificationMessage",
                        bodyParamsJson: $$"""{"city":"{{city.Name}}","company":"{{company.Name}}"}""",
                        relatedEntityType: "CITY",
                        relatedEntityId: city.Id);
                }
            }
        }
    }
}
