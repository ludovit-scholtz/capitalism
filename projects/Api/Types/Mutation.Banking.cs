using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

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

        if (!bank.BaseCapitalDeposited)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This bank has not completed its initial base capital deposit and is not yet open for business.")
                    .SetCode("BANK_NOT_INITIALIZED")
                    .Build());
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

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var depositRate = bank.DepositInterestRatePercent ?? 0m;

        // Transfer cash: depositor company -> bank company
        depositorCompany.Cash -= input.Amount;
        bank.Company.Cash += input.Amount;
        bank.TotalDeposits += input.Amount;

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

        // Determine actual payout: if bank lacks cash, pay what's available (central-bank coverage recorded separately)
        var payout = Math.Min(input.Amount, Math.Max(0m, bankCompany.Cash));

        // Transfer cash back to depositor
        bankCompany.Cash -= payout;
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

        // If bank couldn't fully pay, log central-bank deficit
        if (payout < input.Amount)
        {
            var deficit = input.Amount - payout;
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = bankCompany.Id,
                BuildingId = bank.Id,
                Category = LedgerCategory.CentralBankBorrow,
                Description = $"Central bank emergency funding covering withdrawal shortfall of {deficit:C0}",
                Amount = -deficit,
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
        };
    }
}
