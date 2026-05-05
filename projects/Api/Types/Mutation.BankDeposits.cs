using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank deposit top-up, rate configuration, and activation mutations.
/// Extends the banking mutations with deposit management and bank setup flows.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Adds additional funds to an existing active deposit (top-up).
    /// The depositor company must own the deposit and have sufficient cash.
    /// </summary>
    [Authorize]
    public async Task<BankDepositSummary> TopUpDeposit(
        TopUpDepositInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        _ = input;
        _ = db;
        _ = httpContextAccessor;

        throw new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Use bank-account transfer on the Forex page to add funds to an existing bank account.")
                .SetCode("USE_FOREX_TRANSFER")
                .Build());
    }

    // ── Bank Rate Configuration ───────────────────────────────────────────────

    /// <summary>
    /// Configures the deposit and lending interest rates for a bank building.
    /// Only the bank's owning player can set rates.
    /// Existing deposits keep their snapshotted rate; only new deposits get the updated rate.
    /// </summary>
    [Authorize]
    public async Task<BankInfoSummary> SetBankRates(
        SetBankRatesInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var bank = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null || bank.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found or you do not own it.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        if (input.DepositInterestRatePercent < 0m || input.DepositInterestRatePercent > 100m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Deposit interest rate must be between 0% and 100%.")
                    .SetCode("INVALID_INTEREST_RATE")
                    .Build());
        }

        if (input.LendingInterestRatePercent < 0.1m || input.LendingInterestRatePercent > 200m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Lending interest rate must be between 0.1% and 200%.")
                    .SetCode("INVALID_INTEREST_RATE")
                    .Build());
        }

        bank.DepositInterestRatePercent = input.DepositInterestRatePercent;
        bank.LendingInterestRatePercent = input.LendingInterestRatePercent;

        await db.SaveChangesAsync();

        return await BuildBankInfoAsync(db, bank);
    }

    // ── Bank Activation ──────────────────────────────────────────────────────

    /// <summary>
    /// Initiates the mandatory base capital deposit for a newly purchased bank building.
    /// The required amount is determined by the city's currency (e.g. 10M EUR, 240M CZK).
    /// This is the first action a bank owner must take to open the bank for business.
    /// Only the owning player may call this mutation, and the bank company must have sufficient cash.
    /// </summary>
    [Authorize]
    public async Task<BankInfoSummary> InitiateBaseDeposit(
        Guid bankBuildingId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var bank = await db.Buildings
            .Include(b => b.Company)
            .ThenInclude(c => c.Player)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == bankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null || bank.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found or you do not own it.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        if (bank.BaseCapitalDeposited)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This bank has already completed its base capital deposit.")
                    .SetCode("BASE_DEPOSIT_ALREADY_DONE")
                    .Build());
        }

        var cityCurrencyCode = bank.City?.CurrencyCode ?? "EUR";
        var baseCapitalRequired = GetBaseCapitalRequirement(cityCurrencyCode);
        var currencySymbol = GetCurrencySymbol(cityCurrencyCode);

        var fundingAccount = await ResolveCompanyTransferAccountAsync(
            db,
            bank.CompanyId,
            cityCurrencyCode,
            cancellationToken: httpContextAccessor.HttpContext!.RequestAborted);

        if (fundingAccount.Balance < baseCapitalRequired)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient company funds. The base capital deposit requires {currencySymbol}{baseCapitalRequired:N0}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();

        // Transfer cash from owning company into the bank base-capital account
        fundingAccount.Balance -= baseCapitalRequired;
        bank.TotalDeposits += baseCapitalRequired;
        bank.BaseCapitalDeposited = true;

        // Initialize default interest rates if not already set
        bank.DepositInterestRatePercent ??= 3m;    // 3% deposit rate
        bank.LendingInterestRatePercent ??= 8m;    // 8% lending rate

        // Create the permanent base-capital deposit record (not withdrawable)
        var deposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = cityCurrencyCode,
            CompanyId = bank.CompanyId,
            BankBuildingId = bank.Id,
            Balance = baseCapitalRequired,
            DepositInterestRatePercent = 0m, // Owner's own base capital earns no interest
            IsBaseCapitalDeposit = true,
            DepositedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            TotalInterestPaid = 0m,
            IsGovernmentAccount = false,
        };

        db.BankAccounts.Add(deposit);

        // Ledger: record the base capital transfer as an operating expense for the company
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = bank.CompanyId,
            BuildingId = bank.Id,
            BankAccountId = fundingAccount.Id,
            Category = LedgerCategory.DepositMade,
            Description = $"Base capital deposit to activate {bank.Name}",
            Amount = -baseCapitalRequired,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        return await BuildBankInfoAsync(db, bank);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static BankDepositSummary MapToDepositSummary(BankAccount d, Building bank, Company? depositorCompany, Player? depositorPlayer) => new()
    {
        Id = d.Id,
        BankBuildingId = d.BankBuildingId!.Value,
        BankBuildingName = bank.Name,
        DepositorCompanyId = d.CompanyId ?? Guid.Empty,
        DepositorCompanyName = depositorCompany?.Name ?? depositorPlayer?.DisplayName ?? string.Empty,
        Amount = d.Balance,
        DepositInterestRatePercent = d.DepositInterestRatePercent ?? 0m,
        IsBaseCapital = d.IsBaseCapitalDeposit,
        IsActive = d.ClosedAtUtc is null,
        DepositedAtTick = d.DepositedAtTick ?? 0L,
        DepositedAtUtc = d.CreatedAtUtc,
        WithdrawnAtTick = d.ClosedAtTick,
        WithdrawnAtUtc = d.ClosedAtUtc,
        TotalInterestPaid = d.TotalInterestPaid,
        CityCurrencyCode = bank.City?.CurrencyCode ?? "EUR",
    };

    internal static async Task<BankInfoSummary> BuildBankInfoAsync(AppDbContext db, Building bank)
    {
        var city = bank.City ?? await db.Cities.FindAsync(bank.CityId);
        var company = bank.Company ?? await db.Companies.FindAsync(bank.CompanyId);

        var currencyCode = city?.CurrencyCode ?? "EUR";
        var baseCapitalRequirement = GetBaseCapitalRequirement(currencyCode);

        var outstandingPrincipal = await db.Loans
            .Where(l => l.BankBuildingId == bank.Id && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => (decimal?)l.RemainingPrincipal) ?? 0m;

        var lendable = bank.TotalDeposits * LendableRatio;
        var available = Math.Max(0m, lendable - outstandingPrincipal);

        // ── Liquidity calculation ─────────────────────────────────────────────
        var reserveRequirement = bank.TotalDeposits * ReserveRatio;
        var availableCash = company is null
            ? 0m
            : await CompanyBankingService.GetTotalBalanceAsync(db, company.Id);
        var reserveShortfall = Math.Max(0m, reserveRequirement - availableCash);
        var centralBankRate = ComputeCentralBankRate(db);

        string liquidityStatus;
        if (bank.CentralBankDebt > 0m && reserveShortfall > 0m)
            liquidityStatus = BankLiquidityStatus.Critical;
        else if (bank.CentralBankDebt > 0m || reserveShortfall > 0m)
            liquidityStatus = BankLiquidityStatus.Pressured;
        else
            liquidityStatus = BankLiquidityStatus.Healthy;

        return new BankInfoSummary
        {
            BankBuildingId = bank.Id,
            BankBuildingName = bank.Name,
            CityId = bank.CityId,
            CityName = city?.Name ?? string.Empty,
            CityCurrencyCode = currencyCode,
            CityCurrencySymbol = GetCurrencySymbol(currencyCode),
            BaseCapitalRequirement = baseCapitalRequirement,
            LenderCompanyId = bank.CompanyId,
            LenderCompanyName = company?.Name ?? string.Empty,
            DepositInterestRatePercent = bank.DepositInterestRatePercent ?? 0m,
            LendingInterestRatePercent = bank.LendingInterestRatePercent ?? 0m,
            TotalDeposits = bank.TotalDeposits,
            LendableCapacity = lendable,
            OutstandingLoanPrincipal = outstandingPrincipal,
            AvailableLendingCapacity = available,
            BaseCapitalDeposited = bank.BaseCapitalDeposited,
            CentralBankDebt = bank.CentralBankDebt,
            CentralBankInterestRatePercent = centralBankRate,
            ReserveRequirement = reserveRequirement,
            AvailableCash = availableCash,
            ReserveShortfall = reserveShortfall,
            LiquidityStatus = liquidityStatus,
            PendingDepositInterestRatePercent = bank.PendingDepositInterestRatePercent,
            PendingDepositRateEffectiveTick = bank.PendingDepositRateEffectiveTick,
        };
    }

    /// <summary>
    /// Computes the current central-bank interest rate (2–5% p.a.) based on how many banks
    /// are actively borrowing from the central bank.  More borrowers → higher rate (market stress).
    /// </summary>
    internal static decimal ComputeCentralBankRate(AppDbContext db)
    {
        // Count banks with outstanding central-bank debt (synchronous — called from sync context only)
        var borrowingBanks = db.Buildings
            .Where(b => b.Type == BuildingType.Bank && b.CentralBankDebt > 0m)
            .Count();

        // Linear interpolation: 0 banks → 2%, 5+ banks → 5%
        const decimal minRate = 2m;
        const decimal maxRate = 5m;
        const int maxBanksForMaxRate = 5;
        var rate = minRate + (maxRate - minRate) * Math.Min(1m, (decimal)borrowingBanks / maxBanksForMaxRate);
        return Math.Round(rate, 2);
    }
}
