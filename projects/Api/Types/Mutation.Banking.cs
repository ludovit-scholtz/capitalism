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

    /// <summary>
    /// Returns the base capital deposit required to open a bank in the given currency.
    /// The reference amount is 10,000,000 USD. Other currencies are scaled by approximate FX rates.
    /// </summary>
    internal static decimal GetBaseCapitalRequirement(string currencyCode) => currencyCode.ToUpperInvariant() switch
    {
        "CZK" => 240_000_000m, // ~10M USD at ~24 CZK/USD
        "EUR" => 10_000_000m,
        "USD" => 10_000_000m,
        "GBP" => 8_600_000m,   // ~10M USD at ~0.86 GBP/USD
        "CNY" => 72_000_000m,  // ~10M USD at ~7.2 CNY/USD
        "INR" => 835_000_000m, // ~10M USD at ~83.5 INR/USD
        _ => 10_000_000m,      // fallback for future currencies
    };

    /// <summary>
    /// Returns the display symbol for a given ISO 4217 currency code.
    /// </summary>
    internal static string GetCurrencySymbol(string currencyCode) => currencyCode.ToUpperInvariant() switch
    {
        "CZK" => "Kč",
        "EUR" => "€",
        "USD" => "$",
        "GBP" => "£",
        "CNY" => "¥",
        "INR" => "₹",
        _ => currencyCode,
    };

    // ── Bank Account Open/Close Flows ────────────────────────────────────────

    /// <summary>
    /// Opens a bank account in a bank building.
    /// The opening balance earns interest each tick at the bank's current deposit rate.
    /// Players may not open an external interest-bearing account in their own bank company
    /// (use the owner base-capital path instead).
    /// </summary>
    [Authorize]
    public async Task<BankDepositSummary> OpenBankAccount(
        OpenBankAccountInput input,
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
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.BankBuildingId && b.Type == BuildingType.Bank);

        if (bank is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank building not found.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        // Bank's own company cannot deposit into its own bank (no self-interest)
        if (depositorCompany.Id == bank.CompanyId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A bank's founder company cannot deposit into its own bank. Use the bank's capital directly.")
                    .SetCode("SELF_DEPOSIT_NOT_ALLOWED")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var cityCurrencyCode = bank.City?.CurrencyCode ?? "EUR";
        var baseCapitalRequirement = GetBaseCapitalRequirement(cityCurrencyCode);

        if (!bank.BaseCapitalDeposited)
        {
            if (input.Amount == baseCapitalRequirement)
            {
                // finish setup

                // Set default interest rates
                bank.DepositInterestRatePercent = 3m;   // 3% deposit rate
                bank.LendingInterestRatePercent = 8m;   // 8% lending rate

                bank.TotalDeposits = baseCapitalRequirement;
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
                    .SetMessage("Minimum opening balance is 1,000.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        if (depositorCompany.Cash < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient company funds to open this bank account.")
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

        var deposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = cityCurrencyCode,
            CompanyId = depositorCompany.Id,
            BankBuildingId = bank.Id,
            Balance = input.Amount,
            DepositInterestRatePercent = depositRate,
            IsBaseCapitalDeposit = false,
            DepositedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            TotalInterestPaid = 0m,
            IsGovernmentAccount = false,
        };

        db.BankAccounts.Add(deposit);

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
    /// Withdraws funds from an existing bank account.
    /// The bank must be able to cover the withdrawal from available cash.
    /// If the bank lacks sufficient funds, a central-bank borrowing is arranged automatically.
    /// When the full balance is withdrawn, the account is closed.
    /// </summary>
    [Authorize]
    public async Task<BankDepositSummary> CloseBankAccount(
        CloseBankAccountInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var deposit = await db.BankAccounts
            .Include(d => d.Company)
            .Include(d => d.BankBuilding)
            .ThenInclude(b => b!.Company)
            .FirstOrDefaultAsync(d => d.Id == input.DepositId && d.BankBuildingId != null && d.ClosedAtUtc == null);

        if (deposit is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Active bank account not found.")
                    .SetCode("DEPOSIT_NOT_FOUND")
                    .Build());
        }

        var depositorCompany = deposit.Company;
        if (depositorCompany?.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not own the depositor company.")
                    .SetCode("UNAUTHORIZED")
                    .Build());
        }

        if (deposit.IsBaseCapitalDeposit)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The bank's base-capital account cannot be closed by external players.")
                    .SetCode("WITHDRAWAL_NOT_ALLOWED")
                    .Build());
        }

        if (input.Amount <= 0m || input.Amount > deposit.Balance)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Withdrawal amount must be between $1 and the deposit balance of {deposit.Balance:C0}.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var bank = deposit.BankBuilding!;
        var bankCompany = bank.Company!;

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
        depositorCompany.Cash += payout;
        bank.TotalDeposits -= input.Amount;
        deposit.Balance -= input.Amount;

        var isFullyWithdrawn = deposit.Balance <= 0m;
        if (isFullyWithdrawn)
        {
            deposit.ClosedAtTick = currentTick;
            deposit.ClosedAtUtc = DateTime.UtcNow;
        }

        // Ledger: depositor receives withdrawal
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = deposit.CompanyId!.Value,
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

        return MapToDepositSummary(deposit, bank, depositorCompany);
    }

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
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var deposit = await db.BankAccounts
            .Include(d => d.Company)
            .Include(d => d.BankBuilding)
            .ThenInclude(b => b!.Company)
            .FirstOrDefaultAsync(d => d.Id == input.DepositId && d.BankBuildingId != null && d.ClosedAtUtc == null);

        if (deposit is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Active deposit not found.")
                    .SetCode("DEPOSIT_NOT_FOUND")
                    .Build());
        }

        var depositorCompany = deposit.Company;
        if (depositorCompany?.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You do not own the depositor company.")
                    .SetCode("UNAUTHORIZED")
                    .Build());
        }

        if (input.Amount < 1_000m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum top-up amount is $1,000.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        if (depositorCompany.Cash < input.Amount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient company funds for this top-up.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();
        var bank = deposit.BankBuilding!;

        // Transfer cash: depositor -> bank
        depositorCompany.Cash -= input.Amount;
        bank.Company!.Cash += input.Amount;
        bank.TotalDeposits += input.Amount;

        // Create a NEW deposit record for this top-up tranche.
        // Each tranche has its own DepositedAtTick (current tick) and the bank's CURRENT
        // deposit rate — not the original deposit's snapshotted rate.  This prevents the
        // retroactive-yield exploit where funds added at tick T could appear to have earned
        // interest since the original deposit date, and prevents locking in old rates.
        var currentRate = bank.DepositInterestRatePercent ?? 0m;
        var topUpDeposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = deposit.CurrencyCode,
            CompanyId = deposit.CompanyId,
            BankBuildingId = bank.Id,
            Balance = input.Amount,
            DepositInterestRatePercent = currentRate,
            IsBaseCapitalDeposit = false,
            DepositedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
            TotalInterestPaid = 0m,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.Add(topUpDeposit);

        // Auto-repay central-bank debt from surplus (same logic as CreateDeposit)
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
                    Description = $"Central bank repayment from deposit top-up (surplus above reserve)",
                    Amount = -repayment,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = deposit.CompanyId!.Value,
            BuildingId = bank.Id,
            Category = LedgerCategory.DepositMade,
            Description = $"Top-up deposit into {bank.Name} at {currentRate}% p.a.",
            Amount = -input.Amount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();

        return MapToDepositSummary(topUpDeposit, bank, depositorCompany);
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

        if (bank.Company.Cash < baseCapitalRequired)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient company funds. The base capital deposit requires {currencySymbol}{baseCapitalRequired:N0}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultAsync();

        // Transfer cash from owning company into the bank
        bank.Company.Cash -= baseCapitalRequired;
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

    internal static BankDepositSummary MapToDepositSummary(BankAccount d, Building bank, Company depositor) => new()
    {
        Id = d.Id,
        BankBuildingId = d.BankBuildingId!.Value,
        BankBuildingName = bank.Name,
        DepositorCompanyId = d.CompanyId ?? Guid.Empty,
        DepositorCompanyName = depositor.Name,
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
