using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static class CityUnlockService
{
    public sealed record CompanyCityUnlockStatus(
        Guid CityId,
        string CityName,
        string CountryCode,
        string Currency,
        bool IsUnlocked,
        decimal RequiredNetWorth,
        decimal CurrentNetWorth,
        int ProgressPercent,
        long? EstimatedTicksToUnlock,
        Guid? CompanyId);

    public static async Task<Guid?> ResolvePlayerActiveCompanyIdAsync(
        AppDbContext db,
        Guid playerId,
        CancellationToken ct = default)
    {
        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == playerId, ct);

        if (player is null)
        {
            return null;
        }

        if (player.ActiveCompanyId.HasValue)
        {
            var ownsActiveCompany = await db.Companies
                .AsNoTracking()
                .AnyAsync(company => company.Id == player.ActiveCompanyId.Value && company.PlayerId == playerId, ct);

            if (ownsActiveCompany)
            {
                return player.ActiveCompanyId.Value;
            }
        }

        return await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == playerId)
            .OrderBy(company => company.FoundedAtTick)
            .ThenBy(company => company.Name)
            .Select(company => (Guid?)company.Id)
            .FirstOrDefaultAsync(ct);
    }

    public static async Task<CompanyCityUnlockStatus?> GetStatusForCityAsync(
        AppDbContext db,
        Guid cityId,
        Guid? companyId,
        CancellationToken ct = default)
    {
        var statuses = await BuildStatusesAsync(db, companyId, [cityId], ct);
        return statuses.FirstOrDefault();
    }

    public static Task<List<CompanyCityUnlockStatus>> GetStatusesAsync(
        AppDbContext db,
        Guid? companyId,
        CancellationToken ct = default)
        => BuildStatusesAsync(db, companyId, null, ct);

    private static async Task<List<CompanyCityUnlockStatus>> BuildStatusesAsync(
        AppDbContext db,
        Guid? companyId,
        IEnumerable<Guid>? cityIds,
        CancellationToken ct)
    {
        var cityIdSet = cityIds?.Distinct().ToHashSet();

        var cities = await db.Cities
            .AsNoTracking()
            .Where(city => cityIdSet == null || cityIdSet.Contains(city.Id))
            .OrderBy(city => city.Name)
            .ToListAsync(ct);

        if (cities.Count == 0)
        {
            return [];
        }

        var requirements = await db.CityUnlockRequirements
            .AsNoTracking()
            .Where(requirement => cityIdSet == null || cityIdSet.Contains(requirement.CityId))
            .ToDictionaryAsync(requirement => requirement.CityId, ct);

        if (!companyId.HasValue)
        {
            var publicRates = await FxRateHelper.BuildEurRatesLookupAsync(
                db,
                cities.Select(city => city.CurrencyCode).Append("USD"));

            return cities.Select(city =>
            {
                var thresholdUsd = requirements.GetValueOrDefault(city.Id)?.RequiredNetWorthUsd ?? 0m;
                var requiredNetWorth = decimal.Round(
                    FxRateHelper.ConvertAmount(thresholdUsd, "USD", city.CurrencyCode, publicRates),
                    2,
                    MidpointRounding.AwayFromZero);
                var isUnlocked = thresholdUsd <= 0m;

                return new CompanyCityUnlockStatus(
                    CityId: city.Id,
                    CityName: city.Name,
                    CountryCode: city.CountryCode,
                    Currency: city.CurrencyCode,
                    IsUnlocked: isUnlocked,
                    RequiredNetWorth: requiredNetWorth,
                    CurrentNetWorth: 0m,
                    ProgressPercent: isUnlocked ? 100 : 0,
                    EstimatedTicksToUnlock: null,
                    CompanyId: null);
            }).ToList();
        }

        var company = await db.Companies
            .AsNoTracking()
            .Include(candidate => candidate.Buildings)
                .ThenInclude(building => building.City)
            .Include(candidate => candidate.BankAccounts)
            .FirstOrDefaultAsync(candidate => candidate.Id == companyId.Value, ct);

        if (company is null)
        {
            return [];
        }

        var ownedLots = await db.BuildingLots
            .AsNoTracking()
            .Include(lot => lot.City)
            .Where(lot => lot.OwnerCompanyId == company.Id)
            .ToListAsync(ct);

        var companyBuildingIds = company.Buildings.Select(building => building.Id).ToList();
        var inventories = companyBuildingIds.Count == 0
            ? []
            : await db.Inventories
                .AsNoTracking()
                .Include(inventory => inventory.ResourceType)
                .Include(inventory => inventory.ProductType)
                .Where(inventory => companyBuildingIds.Contains(inventory.BuildingId))
                .ToListAsync(ct);

        var primaryCurrencyCode = ResolvePrimaryCurrencyCode(company);
        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(
            db,
            cities.Select(city => city.CurrencyCode)
                .Concat(company.BankAccounts.Select(account => account.CurrencyCode))
                .Append(primaryCurrencyCode)
                .Append("USD"));

        var currentNetWorthUsd = ComputeCompanyNetWorthUsd(company, ownedLots, inventories, eurRates, primaryCurrencyCode);
        var growthPerTickUsd = await ComputeRecentGrowthPerTickUsdAsync(db, company.Id, primaryCurrencyCode, eurRates, ct);
        var unlockedCityIds = await db.CompanyCityUnlocks
            .AsNoTracking()
            .Where(unlock => unlock.CompanyId == company.Id && (cityIdSet == null || cityIdSet.Contains(unlock.CityId)))
            .Select(unlock => unlock.CityId)
            .ToListAsync(ct);
        var unlockedCityIdSet = unlockedCityIds.ToHashSet();
        var ownedCityIdSet = company.Buildings.Select(building => building.CityId)
            .Concat(ownedLots.Select(lot => lot.CityId))
            .ToHashSet();

        return cities.Select(city =>
        {
            var thresholdUsd = requirements.GetValueOrDefault(city.Id)?.RequiredNetWorthUsd ?? 0m;
            var requiredNetWorth = decimal.Round(
                FxRateHelper.ConvertAmount(thresholdUsd, "USD", city.CurrencyCode, eurRates),
                2,
                MidpointRounding.AwayFromZero);
            var currentNetWorth = decimal.Round(
                FxRateHelper.ConvertAmount(currentNetWorthUsd, "USD", city.CurrencyCode, eurRates),
                2,
                MidpointRounding.AwayFromZero);

            var isUnlocked = thresholdUsd <= 0m
                || unlockedCityIdSet.Contains(city.Id)
                || ownedCityIdSet.Contains(city.Id)
                || currentNetWorthUsd >= thresholdUsd;

            var progressPercent = isUnlocked || thresholdUsd <= 0m
                ? 100
                : (int)Math.Clamp(Math.Round((double)(currentNetWorthUsd / thresholdUsd * 100m), MidpointRounding.AwayFromZero), 0d, 99d);

            long? estimatedTicksToUnlock = null;
            var remainingUsd = thresholdUsd - currentNetWorthUsd;
            if (!isUnlocked && remainingUsd > 0m && growthPerTickUsd > 0m)
            {
                estimatedTicksToUnlock = (long)Math.Ceiling((double)(remainingUsd / growthPerTickUsd));
            }

            return new CompanyCityUnlockStatus(
                CityId: city.Id,
                CityName: city.Name,
                CountryCode: city.CountryCode,
                Currency: city.CurrencyCode,
                IsUnlocked: isUnlocked,
                RequiredNetWorth: requiredNetWorth,
                CurrentNetWorth: currentNetWorth,
                ProgressPercent: progressPercent,
                EstimatedTicksToUnlock: estimatedTicksToUnlock,
                CompanyId: company.Id);
        }).ToList();
    }

    public static decimal ComputeCompanyNetWorthUsd(
        Company company,
        IReadOnlyCollection<BuildingLot> ownedLots,
        IReadOnlyCollection<Inventory> inventories,
        IReadOnlyDictionary<string, decimal> eurRates,
        string primaryCurrencyCode)
    {
        var buildingIds = company.Buildings.Select(building => building.Id).ToHashSet();
        var bankBalanceUsd = company.BankAccounts
            .Where(account => account.ClosedAtUtc == null)
            .Sum(account => FxRateHelper.ConvertToUsd(account.Balance, account.CurrencyCode, eurRates));

        var buildingValueUsd = company.Buildings.Sum(building =>
        {
            var currencyCode = building.City?.CurrencyCode ?? primaryCurrencyCode;
            return FxRateHelper.ConvertToUsd(WealthCalculator.GetBuildingValue(building), currencyCode, eurRates);
        });

        var lotValueUsd = ownedLots.Sum(lot =>
        {
            var currencyCode = lot.City?.CurrencyCode ?? primaryCurrencyCode;
            return FxRateHelper.ConvertToUsd(WealthCalculator.GetLandValue(lot), currencyCode, eurRates);
        });

        var inventoryValueLocal = inventories
            .Where(inventory => buildingIds.Contains(inventory.BuildingId))
            .Sum(inventory => inventory.Quantity * WealthCalculator.GetItemBasePrice(inventory));
        var inventoryValueUsd = FxRateHelper.ConvertToUsd(inventoryValueLocal, primaryCurrencyCode, eurRates);

        return decimal.Round(
            bankBalanceUsd + buildingValueUsd + lotValueUsd + inventoryValueUsd,
            2,
            MidpointRounding.AwayFromZero);
    }

    private static async Task<decimal> ComputeRecentGrowthPerTickUsdAsync(
        AppDbContext db,
        Guid companyId,
        string primaryCurrencyCode,
        IReadOnlyDictionary<string, decimal> eurRates,
        CancellationToken ct)
    {
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync(ct);

        const int windowTicks = 100;
        var fromTick = Math.Max(0, currentTick - windowTicks);
        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Include(entry => entry.BankAccount)
            .Include(entry => entry.Building)
                .ThenInclude(building => building!.City)
            .Where(entry => entry.CompanyId == companyId && entry.RecordedAtTick > fromTick)
            .ToListAsync(ct);

        if (entries.Count == 0)
        {
            return 0m;
        }

        var totalGrowthUsd = entries.Sum(entry =>
        {
            var currencyCode = entry.BankAccount?.CurrencyCode
                ?? entry.Building?.City?.CurrencyCode
                ?? primaryCurrencyCode;
            return FxRateHelper.ConvertToUsd(entry.Amount, currencyCode, eurRates);
        });

        var elapsedTicks = Math.Max(1, currentTick - fromTick);
        var perTick = totalGrowthUsd / elapsedTicks;
        return perTick > 0m ? perTick : 0m;
    }

    private static string ResolvePrimaryCurrencyCode(Company company)
        => company.Buildings
            .Select(building => building.City?.CurrencyCode)
            .Concat(company.BankAccounts.Select(account => account.CurrencyCode))
            .FirstOrDefault(currencyCode => !string.IsNullOrWhiteSpace(currencyCode))
        ?? "EUR";
}
