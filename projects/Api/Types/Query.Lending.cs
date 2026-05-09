using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank lending marketplace queries: loan offers, player loans, bank loans,
/// procurement preview, sourcing candidates, and unit upgrade info.
/// </summary>
public sealed partial class Query
{
    // ── Bank Lending Marketplace ──────────────────────────────────────────────────

    /// <summary>
    /// Returns borrower-facing lending options for every open bank.
    /// Loan offers are no longer player-published; each bank is exposed directly.
    /// </summary>
    [Authorize]
    public async Task<List<LoanOfferSummary>> GetLoanOffers(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        _ = httpContextAccessor;

        var banks = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .Where(b => b.Type == BuildingType.Bank && b.BaseCapitalDeposited)
            .AsNoTracking()
            .ToListAsync();

        if (banks.Count == 0)
            return [];

        var bankIds = banks.Select(b => b.Id).ToList();
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var (interestMultiplierByCity, globalInterestMultiplier) = await LoadInterestRateMultiplierByCityAsync(db, currentTick);

        var outstandingByBank = await db.Loans
            .Where(l => bankIds.Contains(l.BankBuildingId)
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .GroupBy(l => l.BankBuildingId)
            .Select(g => new { BankBuildingId = g.Key, Outstanding = g.Sum(l => l.RemainingPrincipal) })
            .ToDictionaryAsync(x => x.BankBuildingId, x => x.Outstanding);

        const long defaultDurationTicks = GameConstants.TicksPerYear;
        var nowUtc = DateTime.UtcNow;

        return banks.Select(bank =>
        {
            var outstanding = outstandingByBank.TryGetValue(bank.Id, out var value) ? value : 0m;
            var lendable = bank.TotalDeposits * 0.90m;
            var available = Math.Max(0m, lendable - outstanding);

            return new LoanOfferSummary
            {
                // Borrower UX now treats banks as direct lending sources.
                Id = bank.Id,
                BankBuildingId = bank.Id,
                BankBuildingName = bank.Name,
                CityId = bank.CityId,
                CityName = bank.City.Name,
                LenderCompanyId = bank.CompanyId,
                LenderCompanyName = bank.Company.Name,
                AnnualInterestRatePercent = decimal.Round(
                    (bank.LendingInterestRatePercent ?? 8m)
                    * ResolveInterestMultiplier(interestMultiplierByCity, globalInterestMultiplier, bank.CityId),
                    4,
                    MidpointRounding.AwayFromZero),
                MaxPrincipalPerLoan = available,
                TotalCapacity = lendable,
                UsedCapacity = outstanding,
                RemainingCapacity = available,
                DurationTicks = defaultDurationTicks,
                IsActive = available > 0m,
                CreatedAtTick = 0,
                CreatedAtUtc = nowUtc,
            };
        }).ToList();
    }

    private static async Task<(Dictionary<Guid, decimal> byCity, decimal global)> LoadInterestRateMultiplierByCityAsync(AppDbContext db, long currentTick)
    {
        var active = await db.MarketEvents
            .AsNoTracking()
            .Where(me => me.EventType == MarketEventType.InterestRateChange
                && me.StartsAtTick <= currentTick
                && me.ExpiresAtTick >= currentTick)
            .ToListAsync();

        var byCity = active
            .Where(me => me.AffectedCityId.HasValue)
            .GroupBy(me => me.AffectedCityId!.Value)
            .ToDictionary(
                group => group.Key,
                group => Math.Clamp(
                    group.Aggregate(1m, (acc, next) => acc * next.MagnitudeMultiplier),
                    GameConstants.MarketEventMultiplierMin,
                    GameConstants.MarketEventMultiplierMax));
        var global = Math.Clamp(
            active
                .Where(me => !me.AffectedCityId.HasValue)
                .Aggregate(1m, (acc, next) => acc * next.MagnitudeMultiplier),
            GameConstants.MarketEventMultiplierMin,
            GameConstants.MarketEventMultiplierMax);
        return (byCity, global);
    }

    private static decimal ResolveInterestMultiplier(IReadOnlyDictionary<Guid, decimal> byCity, decimal globalMultiplier, Guid cityId)
    {
        if (byCity.TryGetValue(cityId, out var cityMultiplier))
            return cityMultiplier;
        return globalMultiplier;
    }

    /// <summary>
    /// Returns all loan offers published by the authenticated player's companies.
    /// Includes inactive offers and utilisation metrics.
    /// </summary>
    [Authorize]
    public async Task<List<LoanOfferSummary>> GetMyLoanOffers(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var companyIds = await db.Companies
            .Where(c => c.PlayerId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        var offers = await db.LoanOffers
            .Include(o => o.LenderCompany)
            .Include(o => o.BankBuilding)
            // Loan offers always point at bank buildings; EF skips the nested include if the optional navigation is absent.
            .ThenInclude(b => b!.City)
            .Where(o => companyIds.Contains(o.LenderCompanyId))
            .AsNoTracking()
            .ToListAsync();

        return offers.Select(o => new LoanOfferSummary
        {
            Id = o.Id,
            BankBuildingId = o.BankBuildingId,
            BankBuildingName = o.BankBuilding.Name,
            CityId = o.BankBuilding.CityId,
            CityName = o.BankBuilding.City.Name,
            LenderCompanyId = o.LenderCompanyId,
            LenderCompanyName = o.LenderCompany.Name,
            AnnualInterestRatePercent = o.AnnualInterestRatePercent,
            MaxPrincipalPerLoan = o.MaxPrincipalPerLoan,
            TotalCapacity = o.TotalCapacity,
            UsedCapacity = o.UsedCapacity,
            RemainingCapacity = o.TotalCapacity - o.UsedCapacity,
            DurationTicks = o.DurationTicks,
            IsActive = o.IsActive,
            CreatedAtTick = o.CreatedAtTick,
            CreatedAtUtc = o.CreatedAtUtc,
        }).ToList();
    }

    /// <summary>
    /// Returns all active and historical loans for the authenticated player's companies (borrower view).
    /// </summary>
    [Authorize]
    public async Task<List<LoanSummary>> GetMyLoans(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var companyIds = await db.Companies
            .Where(c => c.PlayerId == userId)
            .Select(c => c.Id)
            .ToListAsync();

        var loans = await db.Loans
            .Include(l => l.LenderCompany)
            .Include(l => l.BankBuilding)
            .ThenInclude(b => b!.City)
            .Include(l => l.BorrowerCompany)
            // Collateral is optional for unsecured loans; EF skips the nested include when the parent nav is null.
            .Include(l => l.CollateralBuilding!)
            .ThenInclude(b => b!.City)
            .Where(l => companyIds.Contains(l.BorrowerCompanyId))
            .AsNoTracking()
            .ToListAsync();

        return loans.Select(MapToLoanSummary).ToList();
    }

    /// <summary>
    /// Returns all loans issued by a specific bank building (lender view).
    /// Only the bank's owning player can access this.
    /// </summary>
    [Authorize]
    public async Task<List<LoanSummary>> GetBankLoans(
        Guid bankBuildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == bankBuildingId && b.Type == Data.Entities.BuildingType.Bank);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found or you do not own it.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        var loans = await db.Loans
            .Include(l => l.BorrowerCompany)
            .Include(l => l.LenderCompany)
            .Include(l => l.BankBuilding)
            .ThenInclude(b => b!.City)
            // Collateral is optional for unsecured loans; EF skips the nested include when the parent nav is null.
            .Include(l => l.CollateralBuilding!)
            .ThenInclude(b => b!.City)
            .Where(l => l.BankBuildingId == bankBuildingId)
            .AsNoTracking()
            .ToListAsync();

        return loans.Select(MapToLoanSummary).ToList();
    }

    private static LoanSummary MapToLoanSummary(Loan l) => new()
    {
        Id = l.Id,
        LoanOfferId = l.LoanOfferId,
        BorrowerCompanyId = l.BorrowerCompanyId,
        BorrowerCompanyName = l.BorrowerCompany.Name,
        LenderCompanyId = l.LenderCompanyId,
        LenderCompanyName = l.LenderCompany.Name,
        BankBuildingId = l.BankBuildingId,
        BankBuildingName = l.BankBuilding.Name,
        LoanCurrencyCode = l.BankBuilding.City?.CurrencyCode ?? "EUR",
        OriginalPrincipal = l.OriginalPrincipal,
        RemainingPrincipal = l.RemainingPrincipal,
        AnnualInterestRatePercent = l.AnnualInterestRatePercent,
        DurationTicks = l.DurationTicks,
        StartTick = l.StartTick,
        DueTick = l.DueTick,
        NextPaymentTick = l.NextPaymentTick,
        PaymentAmount = l.PaymentAmount,
        PaymentsMade = l.PaymentsMade,
        TotalPayments = l.TotalPayments,
        Status = l.Status,
        MissedPayments = l.MissedPayments,
        AccumulatedPenalty = l.AccumulatedPenalty,
        DefaultedAtTick = l.DefaultedAtTick,
        AcceptedAtUtc = l.AcceptedAtUtc,
        ClosedAtUtc = l.ClosedAtUtc,
        CollateralBuildingId = l.CollateralBuildingId,
        CollateralBuildingName = l.CollateralBuilding?.Name,
        CollateralAppraisedValue = l.CollateralAppraisedValue,
        CollateralListingPrice = l.CollateralBuilding?.IsForSale == true ? l.CollateralBuilding.AskingPrice : null,
        CollateralListingCurrencyCode = l.CollateralBuilding?.City?.CurrencyCode,
    };

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
                        && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
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
                    ? "This building is already pledged as collateral for another active loan."
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
