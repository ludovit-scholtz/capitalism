using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public static partial class BuildingConfigurationService
{
    public const string UpgradeBlockReasonInsufficientFunds = "INSUFFICIENT_FUNDS";
    public const string UpgradeBlockReasonAccountCurrencyMismatch = "BANK_ACCOUNT_CURRENCY_MISMATCH";

    public static async Task<string?> GetUpgradeBlockReasonAsync(
        AppDbContext db,
        BuildingConfigurationPlan plan,
        long currentTick,
        CancellationToken cancellationToken = default)
    {
        var hydratedPlan = await db.BuildingConfigurationPlans
            .AsNoTracking()
            .Include(candidate => candidate.Units)
            .Include(candidate => candidate.Building)
            .ThenInclude(building => building.Units)
            .Include(candidate => candidate.Building)
            .ThenInclude(building => building.City)
            .Include(candidate => candidate.Building)
            .ThenInclude(building => building.BankAccount)
            .AsSplitQuery()
            .FirstOrDefaultAsync(candidate => candidate.Id == plan.Id, cancellationToken);

        if (hydratedPlan is null)
        {
            return null;
        }

        var cityCurrencyCode = hydratedPlan.Building.City?.CurrencyCode?.ToUpperInvariant() ?? "EUR";
        var liveUnitsByPosition = hydratedPlan.Building.Units
            .DistinctBy(unit => (unit.GridX, unit.GridY))
            .ToDictionary(unit => (unit.GridX, unit.GridY));

        var dueUnits = hydratedPlan.Units
            .Where(unit => unit.IsChanged && unit.AppliesAtTick <= currentTick)
            .ToList();

        if (dueUnits.Count == 0)
        {
            return null;
        }

        var fxRatesLookup = await FxRateHelper.BuildEurRatesLookupAsync(db, [cityCurrencyCode]);
        var fxRate = FxRateHelper.GetEurRate(fxRatesLookup, cityCurrencyCode);
        var totalActivationCost = dueUnits
            .Select(unit => decimal.Round(
                BuildingConfigurationEconomics.CalculateActivationCost(
                    liveUnitsByPosition.GetValueOrDefault((unit.GridX, unit.GridY)),
                    unit) * fxRate,
                2,
                MidpointRounding.AwayFromZero))
            .Where(cost => cost > 0m)
            .Sum();

        if (totalActivationCost <= 0m)
        {
            return null;
        }

        if (hydratedPlan.Building.BankAccount is not null
            && !string.Equals(hydratedPlan.Building.BankAccount.CurrencyCode, cityCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            return $"{UpgradeBlockReasonAccountCurrencyMismatch}:{hydratedPlan.Building.BankAccount.CurrencyCode}:{cityCurrencyCode}";
        }

        var fundingAccount = hydratedPlan.Building.BankAccount;
        if (fundingAccount is null)
        {
            fundingAccount = await db.BankAccounts
                .AsNoTracking()
                .Where(account => account.CompanyId == hydratedPlan.Building.CompanyId
                    && account.CurrencyCode != null
                    && account.CurrencyCode.ToUpper() == cityCurrencyCode)
                .OrderBy(account => account.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (fundingAccount is null || fundingAccount.Balance < totalActivationCost)
        {
            return $"{UpgradeBlockReasonInsufficientFunds}:{totalActivationCost:F2}:{cityCurrencyCode}";
        }

        return null;
    }
}