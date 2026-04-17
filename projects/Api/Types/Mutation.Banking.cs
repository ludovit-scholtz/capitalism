using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace Api.Types;

/// <summary>
/// Bank deposit and rate-configuration mutations.
/// Deposits, withdrawals, and interest-rate management for bank buildings.
/// </summary>
public sealed partial class Mutation
{
    private const decimal BankBaseCapitalRequirement = 10_000_000m;
    private const decimal ReserveRatio = 0.10m;  // 10% must be kept as reserve
    private const decimal LendableRatio = 0.90m; // 90% can be lent out

    // ── Deposit Flows ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a cash deposit in a bank building.
    /// The deposit earns interest each tick at the bank's current deposit rate.
    /// Players may not deposit into their own bank company (use the owner deposit path instead).
    /// </summary>
    [Authorize]
    public async Task<BankDepositSummary> CreateDeposit(
        CreateDepositInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var depositorCompany = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == input.DepositorCompanyId && c.PlayerId == userId);

        if (depositorCompany is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Depositor company not found or you do not own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        var bank = await db.Buildings
            .Include(b => b.Company)
            .ThenInclude(c => c.Player)
            .FirstOrDefaultAsync(b => b.Id == input.BankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();

        if (!bank.BaseCapitalDeposited)
        {
            if (input.Amount == Mutation.BankBaseCapitalRequirement)
            {
                // finish setup

                // Set default interest rates
                bank.DepositInterestRatePercent = 3m;   // 3% deposit rate
                bank.LendingInterestRatePercent = 8m;   // 8% lending rate

                bank.TotalDeposits = Mutation.BankBaseCapitalRequirement;
                bank.BaseCapitalDeposited = true;
            }
            else
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("This bank has not completed its initial base capital deposit and is not yet open for business.")
                        .SetCode("BANK_NOT_INITIALIZED")
                        .Build());
            }
        }

        if (input.Amount < 1_000m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum deposit amount is $1,000.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        if (depositorCompany.Cash < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient company funds for this deposit.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var depositRate = bank.DepositInterestRatePercent ?? 0m;

        // Transfer cash: depositor company -> bank company
        depositorCompany.Cash -= input.Amount;
        bank.Company.Cash += input.Amount;
        bank.TotalDeposits += input.Amount;

        // Auto-repay central-bank debt only from surplus cash above the reserve requirement.
        // This mirrors BankInterestPhase auto-repayment: the deposit has already increased
        // TotalDeposits (and therefore the required reserve), so we must preserve the full
        // reserve before applying any payment toward CB debt. Using all available cash would
        // drain the bank below the reserve that the same deposit just raised.
        if (bank.CentralBankDebt > 0m)
        {
            var reserveNeeded = bank.TotalDeposits * ReserveRatio;
            var surplusCash = bank.Company.Cash - reserveNeeded;
            var repayment = Math.Min(bank.CentralBankDebt, Math.Max(0m, surplusCash));
            if (repayment > 0m)
            {
                bank.Company.Cash -= repayment;
                bank.CentralBankDebt -= repayment;
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.CentralBankRepay,
                    Description = $"Central bank repayment from incoming deposit (surplus above reserve)",
                    Amount = -repayment,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            DepositorCompanyId = depositorCompany.Id,
            Amount = input.Amount,
            DepositInterestRatePercent = depositRate,
            IsBaseCapital = false,
            IsActive = true,
            DepositedAtTick = currentTick,
            DepositedAtUtc = DateTime.UtcNow,
            TotalInterestPaid = 0m,
        };

        db.BankDeposits.Add(deposit);

        // Ledger: depositor makes deposit
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = depositorCompany.Id,
            BuildingId = bank.Id,
            Category = LedgerCategory.DepositMade,
            Description = $"Deposit into {bank.Name} at {depositRate}% p.a.",
            Amount = -input.Amount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        return MapToDepositSummary(deposit, bank, depositorCompany);
    }

    /// <summary>
    /// Withdraws funds from an existing bank deposit.
    /// The bank must be able to cover the withdrawal from available cash.
    /// If the bank lacks sufficient funds, a central-bank borrowing is arranged automatically.
    /// </summary>
    [Authorize]
    public async Task<BankDepositSummary> WithdrawDeposit(
        WithdrawDepositInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var deposit = await db.BankDeposits
            .Include(d => d.DepositorCompany)
            .Include(d => d.BankBuilding)
            .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(d => d.Id == input.DepositId && d.IsActive);

        if (deposit is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Active deposit not found.")
                    .SetCode("DEPOSIT_NOT_FOUND")
                    .Build());
        }

        if (deposit.DepositorCompany.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not own the depositor company.")
                    .SetCode("DEPOSIT_NOT_FOUND")
                    .Build());
        }

        if (deposit.IsBaseCapital)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The bank's base-capital deposit cannot be withdrawn by external players.")
                    .SetCode("WITHDRAWAL_NOT_ALLOWED")
                    .Build());
        }

        if (input.Amount <= 0m || input.Amount > deposit.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Withdrawal amount must be between $1 and the deposit balance of {deposit.Amount:C0}.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var bank = deposit.BankBuilding;
        var bankCompany = bank.Company;

        // Determine actual payout: if bank lacks cash, central bank covers the shortfall
        var payout = input.Amount;
        var actualBankPay = Math.Min(payout, Math.Max(0m, bankCompany.Cash));
        var centralBankCoverage = payout - actualBankPay;

        // Transfer cash back to depositor
        bankCompany.Cash -= actualBankPay;
        if (centralBankCoverage > 0m)
        {
            // Central bank injects the shortfall directly — bank owes it back
            bank.CentralBankDebt += centralBankCoverage;
        }
        deposit.DepositorCompany.Cash += payout;
        bank.TotalDeposits -= input.Amount;
        deposit.Amount -= input.Amount;

        var isFullyWithdrawn = deposit.Amount <= 0m;
        if (isFullyWithdrawn)
        {
            deposit.IsActive = false;
            deposit.WithdrawnAtTick = currentTick;
            deposit.WithdrawnAtUtc = DateTime.UtcNow;
        }

        // Ledger: depositor receives withdrawal
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = deposit.DepositorCompanyId,
            BuildingId = bank.Id,
            Category = LedgerCategory.DepositWithdrawn,
            Description = $"Withdrawal from {bank.Name}",
            Amount = payout,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        // If bank couldn't fully pay from own cash, record central-bank emergency coverage
        if (centralBankCoverage > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = bankCompany.Id,
                BuildingId = bank.Id,
                Category = LedgerCategory.CentralBankBorrow,
                Description = $"Central bank emergency funding covering withdrawal shortfall of {centralBankCoverage:C0}",
                Amount = -centralBankCoverage,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        return MapToDepositSummary(deposit, bank, deposit.DepositorCompany);
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

    // ── Helpers ───────────────────────────────────────────────────────────────

    internal static BankDepositSummary MapToDepositSummary(BankDeposit d, Building bank, Company depositor) => new()
    {
        Id = d.Id,
        BankBuildingId = d.BankBuildingId,
        BankBuildingName = bank.Name,
        DepositorCompanyId = d.DepositorCompanyId,
        DepositorCompanyName = depositor.Name,
        Amount = d.Amount,
        DepositInterestRatePercent = d.DepositInterestRatePercent,
        IsBaseCapital = d.IsBaseCapital,
        IsActive = d.IsActive,
        DepositedAtTick = d.DepositedAtTick,
        DepositedAtUtc = d.DepositedAtUtc,
        WithdrawnAtTick = d.WithdrawnAtTick,
        WithdrawnAtUtc = d.WithdrawnAtUtc,
        TotalInterestPaid = d.TotalInterestPaid,
    };

    internal static async Task<BankInfoSummary> BuildBankInfoAsync(AppDbContext db, Building bank)
    {
        var city = bank.City ?? await db.Cities.FindAsync(bank.CityId);
        var company = bank.Company ?? await db.Companies.FindAsync(bank.CompanyId);

        var outstandingPrincipal = await db.Loans
            .Where(l => l.BankBuildingId == bank.Id && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => (decimal?)l.RemainingPrincipal) ?? 0m;

        var lendable = bank.TotalDeposits * LendableRatio;
        var available = Math.Max(0m, lendable - outstandingPrincipal);

        // ── Liquidity calculation ─────────────────────────────────────────────
        var reserveRequirement = bank.TotalDeposits * ReserveRatio;
        var availableCash = company?.Cash ?? 0m;
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
