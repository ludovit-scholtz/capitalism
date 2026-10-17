using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns collateral eligibility summaries for all buildings owned by the authenticated player.
    /// Use this to help borrowers pick an appropriate building to pledge before accepting a loan.
    /// </summary>
    [Authorize]
    public async Task<List<CollateralEligibilitySummary>> GetMyCollateralBuildings(
        Guid? bankBuildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var companyIds = await db.Companies
            .Where(c => c.PlayerId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        var buildings = await db.Buildings
            .Include(b => b.City)
            .Where(b => companyIds.Contains(b.CompanyId) && !b.IsUnderConstruction)
            .AsNoTracking()
            .ToListAsync();

        if (buildings.Count == 0)
            return [];

        var buildingIds = buildings.Select(b => b.Id).ToList();

        string? targetCurrencyCode = null;
        if (bankBuildingId.HasValue)
        {
            targetCurrencyCode = await db.Buildings
                .Where(b => b.Id == bankBuildingId.Value && b.Type == BuildingType.Bank)
                .Select(b => b.City != null ? b.City.CurrencyCode : "EUR")
                .FirstOrDefaultAsync();

            if (string.IsNullOrWhiteSpace(targetCurrencyCode))
            {
                targetCurrencyCode = null;
            }
        }

        var loanExposureRows = await db.Loans
            .Where(l => l.CollateralBuildingId.HasValue
                        && buildingIds.Contains(l.CollateralBuildingId!.Value)
                        && l.RemainingPrincipal > 0m
                        && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted))
            .Select(l => new
            {
                BuildingId = l.CollateralBuildingId!.Value,
                RemainingPrincipal = l.RemainingPrincipal,
                LoanCurrencyCode = l.BankBuilding.City != null ? l.BankBuilding.City.CurrencyCode : "EUR",
            })
            .ToListAsync();

        var currenciesToLoad = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var building in buildings)
        {
            currenciesToLoad.Add(building.City?.CurrencyCode ?? "EUR");
        }

        foreach (var row in loanExposureRows)
        {
            currenciesToLoad.Add(row.LoanCurrencyCode);
        }

        if (!string.IsNullOrWhiteSpace(targetCurrencyCode))
        {
            currenciesToLoad.Add(targetCurrencyCode);
        }

        var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(db, currenciesToLoad);

        var pledgedSet = loanExposureRows.Select(row => row.BuildingId).ToHashSet();

        return buildings.Select(b =>
        {
            var buildingCurrencyCode = b.City?.CurrencyCode ?? "EUR";
            var outputCurrencyCode = targetCurrencyCode ?? buildingCurrencyCode;

            var baseEurValue = Utilities.WealthCalculator.GetBuildingValue(b);
            var buildingFx = FxRateHelper.GetEurRate(fxRates, buildingCurrencyCode);
            var appraisedInBuildingCurrency = decimal.Round(baseEurValue * buildingFx, 2, MidpointRounding.AwayFromZero);
            var appraised = decimal.Round(
                FxRateHelper.ConvertAmount(
                    appraisedInBuildingCurrency,
                    buildingCurrencyCode,
                    outputCurrencyCode,
                    fxRates),
                2,
                MidpointRounding.AwayFromZero);

            var maxBorrowable = decimal.Round(appraised * 0.70m, 2, MidpointRounding.AwayFromZero);

            var exposure = loanExposureRows
                .Where(row => row.BuildingId == b.Id)
                .Sum(row => FxRateHelper.ConvertAmount(row.RemainingPrincipal, row.LoanCurrencyCode, outputCurrencyCode, fxRates));
            exposure = decimal.Round(exposure, 2, MidpointRounding.AwayFromZero);

            var remaining = Math.Max(0m, maxBorrowable - exposure);
            var isPledged = pledgedSet.Contains(b.Id);

            return new CollateralEligibilitySummary
            {
                BuildingId = b.Id,
                BuildingName = b.Name,
                BuildingType = b.Type,
                Level = b.Level,
                AppraisedValue = appraised,
                MaxBorrowable = maxBorrowable,
                ExistingSecuredExposure = exposure,
                RemainingBorrowingCapacity = remaining,
                CurrencyCode = outputCurrencyCode,
                IsEligible = !isPledged,
                IneligibilityReason = isPledged
                    ? "This building is already pledged as collateral for an unpaid loan."
                    : null,
            };
        }).OrderByDescending(s => s.RemainingBorrowingCapacity).ToList();
    }

    /// <summary>
    /// Evaluates what a PURCHASE unit would do on the next tick given its current
    /// configuration and live market state, without mutating any data.
    /// Returns expected source, delivered price, quality, and any block reasons.
    /// Requires authentication (the player must own the building).
    /// </summary>
    [Authorize]
    public async Task<ProcurementPreview?> GetProcurementPreview(
        Guid buildingUnitId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var unit = await db.BuildingUnits
            .Include(u => u.Building)
            .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(u => u.Id == buildingUnitId);

        if (unit is null || unit.Building.Company.PlayerId != userId)
            return null;

        if (unit.UnitType != Data.Entities.UnitType.Purchase)
            return null;

        var company = unit.Building.Company;
        return await ProcurementPreviewService.ComputeAsync(db, unit, company);
    }

    /// <summary>
    /// Returns the full ranked list of sourcing candidates for a PURCHASE unit,
    /// including global exchange offers from every city, player-placed exchange orders,
    /// and local B2B suppliers. Each candidate includes the full landed-cost breakdown
    /// (exchange price, transit cost, delivered price) and eligibility state so the
    /// player can compare options and understand why a cheaper-looking route may be
    /// blocked by a quality or price filter.
    ///
    /// Requires authentication. The authenticated player must own the building that
    /// contains the unit.
    /// </summary>
    [Authorize]
    public async Task<List<SourcingCandidate>> GetSourcingCandidates(
        Guid buildingUnitId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var unit = await db.BuildingUnits
            .Include(u => u.Building)
            .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(u => u.Id == buildingUnitId);

        if (unit is null || unit.Building.Company.PlayerId != userId)
            return [];

        if (unit.UnitType != Data.Entities.UnitType.Purchase)
            return [];

        return await SourcingComparisonService.GetCandidatesAsync(db, unit, unit.Building.Company);
    }

    /// <summary>
    /// Returns upgrade information for a building unit: cost, duration, and stat projections.
    /// Returns null if the unit is not found or the caller does not own it.
    /// </summary>
    [Authorize]
    public async Task<UnitUpgradeInfo?> GetUnitUpgradeInfo(
        Guid unitId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var unit = await db.BuildingUnits
            .Include(u => u.Building)
            .ThenInclude(b => b.Company)
            .Include(u => u.Building)
            .ThenInclude(b => b.City)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == unitId);

        if (unit is null || unit.Building.Company.PlayerId != userId)
            return null;

        // Look up the FX rate for the building's city so the upgrade cost is shown
        // in the city's local currency (e.g. CZK for Prague, INR for Delhi).
        var cityCurrencyCode = unit.Building.City?.CurrencyCode ?? "EUR";
        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, [cityCurrencyCode]);
        var fxRate = Utilities.FxRateHelper.GetEurRate(fxRates, cityCurrencyCode);

        var isUpgradable = Engine.GameConstants.IsUpgradableUnitType(unit.UnitType);
        var isMaxLevel = unit.Level >= Engine.GameConstants.MaxUnitLevel;
        var currentStat = Engine.GameConstants.GetUnitStat(unit.UnitType, unit.Level);
        var nextLevel = isMaxLevel ? unit.Level : unit.Level + 1;

        // Operating cost deltas — use reference wage so the numbers are reproducible
        // regardless of which city the building is in. City-specific wages are applied at
        // runtime by the tick engine; here we show the "baseline" impact.
        var currentLaborHours = Utilities.CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, unit.Level);
        var nextLaborHours    = Utilities.CompanyEconomyCalculator.GetBaseUnitLaborHours(unit.UnitType, nextLevel);
        var currentEnergyMwh  = Utilities.CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unit.UnitType, unit.Level);
        var nextEnergyMwh     = Utilities.CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unit.UnitType, nextLevel);

        return new UnitUpgradeInfo
        {
            UnitId = unit.Id,
            UnitType = unit.UnitType,
            CurrentLevel = unit.Level,
            NextLevel = nextLevel,
            IsMaxLevel = isMaxLevel,
            IsUpgradable = isUpgradable,
            UpgradeCost = isUpgradable && !isMaxLevel
                ? decimal.Round(Engine.GameConstants.UnitUpgradeCost(unit.UnitType, unit.Level) * fxRate, 2, MidpointRounding.AwayFromZero)
                : 0m,
            UpgradeTicks = isUpgradable && !isMaxLevel
                ? Engine.GameConstants.UnitUpgradeTicks(unit.Level)
                : 0,
            CurrentStat = currentStat,
            NextStat = isMaxLevel
                ? currentStat  // same as CurrentStat at max level
                : Engine.GameConstants.GetUnitStat(unit.UnitType, unit.Level + 1),
            StatLabel = Engine.GameConstants.GetUnitStatLabel(unit.UnitType),
            CurrentLaborHoursPerTick = currentLaborHours,
            NextLaborHoursPerTick    = nextLaborHours,
            CurrentEnergyMwhPerTick  = currentEnergyMwh,
            NextEnergyMwhPerTick     = nextEnergyMwh,
            CurrentLaborCostPerTick  = decimal.Round(currentLaborHours * Engine.GameConstants.ReferenceSalaryPerManhour, 2, MidpointRounding.AwayFromZero),
            NextLaborCostPerTick     = decimal.Round(nextLaborHours    * Engine.GameConstants.ReferenceSalaryPerManhour, 2, MidpointRounding.AwayFromZero),
            CurrentEnergyCostPerTick = decimal.Round(currentEnergyMwh  * Engine.GameConstants.EnergyPricePerMwh, 2, MidpointRounding.AwayFromZero),
            NextEnergyCostPerTick    = decimal.Round(nextEnergyMwh     * Engine.GameConstants.EnergyPricePerMwh, 2, MidpointRounding.AwayFromZero),
            CurrentStorageCapacity   = Engine.GameConstants.GetUnitHoldingCapacity(unit.UnitType, unit.Level),
            NextStorageCapacity      = Engine.GameConstants.GetUnitHoldingCapacity(unit.UnitType, nextLevel),
        };
    }
}
