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
                && l.RemainingPrincipal > 0m
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted))
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
}
