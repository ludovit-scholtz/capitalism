using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
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
        var depositorCompany = default(Company);
        var depositorPlayer = default(Player);
        var isCompanyDepositor = input.DepositorCompanyId.HasValue;

        if (isCompanyDepositor)
        {
            depositorCompany = await db.Companies
                .FirstOrDefaultAsync(c => c.Id == input.DepositorCompanyId!.Value && c.PlayerId == userId);

            if (depositorCompany is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Depositor company not found or you do not own it.")
                        .SetCode("COMPANY_NOT_FOUND")
                        .Build());
            }
        }
        else
        {
            depositorPlayer = await db.Players.FirstOrDefaultAsync(p => p.Id == userId)
                ?? throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Authenticated player not found.")
                        .SetCode("PLAYER_NOT_FOUND")
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
        if (depositorCompany is not null && depositorCompany.Id == bank.CompanyId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A bank's founder company cannot deposit into its own bank. Use the bank's capital directly.")
                    .SetCode("SELF_DEPOSIT_NOT_ALLOWED")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();
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

        if (input.Amount < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Opening amount cannot be negative.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var hasExistingActiveAccount = await db.BankAccounts
            .Include(account => account.Company)
            .Include(account => account.Player)
            .AnyAsync(account =>
                account.BankBuildingId == bank.Id
                && account.ClosedAtUtc == null
                && !account.IsBaseCapitalDeposit
                && (isCompanyDepositor
                    ? account.CompanyId == depositorCompany!.Id
                    : account.PlayerId == userId));

        if (hasExistingActiveAccount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You already have an active bank account in this bank. Use forex transfer to fund it.")
                    .SetCode("ACCOUNT_ALREADY_EXISTS")
                    .Build());
        }

        var depositRate = bank.DepositInterestRatePercent ?? 0m;
        BankAccount? sourceAccount = null;
        BankAccount? bankLiquidityAccount = null;

        if (input.Amount > 0m)
        {
            if (depositorCompany is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Personal account opening supports zero-balance account creation only.")
                        .SetCode("PERSONAL_OPENING_TOPUP_NOT_SUPPORTED")
                        .Build());
            }

            sourceAccount = await ResolveCompanyTransferAccountAsync(
                db,
                depositorCompany.Id,
                cityCurrencyCode,
                cancellationToken: httpContextAccessor.HttpContext!.RequestAborted);
            bankLiquidityAccount = await ResolveCompanyTransferAccountAsync(
                db,
                bank.CompanyId,
                cityCurrencyCode,
                cancellationToken: httpContextAccessor.HttpContext.RequestAborted);

            if (sourceAccount.Balance < input.Amount)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Insufficient company funds to open this bank account.")
                        .SetCode("INSUFFICIENT_FUNDS")
                        .Build());
            }

            // Transfer cash: depositor company -> bank company liquidity account
            sourceAccount.Balance -= input.Amount;
            bankLiquidityAccount.Balance += input.Amount;
            bank.TotalDeposits += input.Amount;

            // Auto-repay central-bank debt only from surplus cash above the reserve requirement.
            // This mirrors BankInterestPhase auto-repayment: the deposit has already increased
            // TotalDeposits (and therefore the required reserve), so we must preserve the full
            // reserve before applying any payment toward CB debt. Using all available cash would
            // drain the bank below the reserve that the same deposit just raised.
            if (bank.CentralBankDebt > 0m)
            {
                var reserveNeeded = bank.TotalDeposits * ReserveRatio;
                var bankAccounts = await LoadActiveCompanyBankAccountsAsync(db, bank.CompanyId, httpContextAccessor.HttpContext.RequestAborted);
                var surplusCash = CompanyBankingService.GetTotalBalance(bankAccounts) - reserveNeeded;
                var repayment = Math.Min(bank.CentralBankDebt, Math.Max(0m, surplusCash));
                if (repayment > 0m)
                {
                    CompanyBankingService.TryDebit(bankAccounts, repayment, cityCurrencyCode);
                    bank.CentralBankDebt -= repayment;
                    db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = bank.CompanyId,
                        BuildingId = bank.Id,
                        BankAccountId = bankLiquidityAccount.Id,
                        Category = LedgerCategory.CentralBankRepay,
                        Description = $"Central bank repayment from incoming deposit (surplus above reserve)",
                        Amount = -repayment,
                        RecordedAtTick = currentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                    });
                }
            }
        }

        var deposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateRandomAccountNumber(),
            CurrencyCode = cityCurrencyCode,
            CompanyId = depositorCompany?.Id,
            PlayerId = depositorPlayer?.Id,
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

        if (input.Amount > 0m && depositorCompany is not null)
        {
            // Ledger: depositor makes deposit
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = depositorCompany.Id,
                BuildingId = bank.Id,
                BankAccountId = sourceAccount?.Id,
                Category = LedgerCategory.DepositMade,
                Description = $"Deposit into {bank.Name} at {depositRate}% p.a.",
                Amount = -input.Amount,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });

            if (bankLiquidityAccount is not null)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    BankAccountId = bankLiquidityAccount.Id,
                    Category = LedgerCategory.BankAccountTransferIn,
                    Description = $"Customer deposit received from {depositorCompany.Name}",
                    Amount = input.Amount,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        await db.SaveChangesAsync();

    return MapToDepositSummary(deposit, bank, depositorCompany, depositorPlayer);
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

        if (deposit.Balance == 0m)
        {
            if (input.Amount != 0m)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Withdrawal amount must be 0 for a zero-balance account closure.")
                        .SetCode("INVALID_AMOUNT")
                        .Build());
            }
        }
        else if (input.Amount <= 0m || input.Amount > deposit.Balance)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Withdrawal amount must be between $1 and the deposit balance of {deposit.Balance:C0}.")
                    .SetCode("INVALID_AMOUNT")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();
        var bank = deposit.BankBuilding!;
        var bankCompany = bank.Company!;

        // Determine actual payout: if bank lacks cash, central bank covers the shortfall
        var payout = input.Amount;
        var bankAccounts = await LoadActiveCompanyBankAccountsAsync(db, bankCompany.Id, httpContextAccessor.HttpContext!.RequestAborted);
        var actualBankPay = Math.Min(payout, Math.Max(0m, CompanyBankingService.GetTotalBalance(bankAccounts)));
        var centralBankCoverage = payout - actualBankPay;
        BankAccount? destinationAccount = null;
        if (payout > 0m)
        {
            destinationAccount = await ResolveCompanyTransferAccountAsync(
                db,
                depositorCompany.Id,
                deposit.CurrencyCode,
                deposit.Id,
                httpContextAccessor.HttpContext.RequestAborted);
        }

        // Transfer cash back to depositor
        CompanyBankingService.TryDebit(bankAccounts, actualBankPay, deposit.CurrencyCode);
        var bankLedgerAccount = CompanyBankingService.FindPreferredAccount(bankAccounts, deposit.CurrencyCode)
            ?? CompanyBankingService.FindAnyPreferredAccount(bankAccounts);
        if (centralBankCoverage > 0m)
        {
            // Central bank injects the shortfall directly — bank owes it back
            bank.CentralBankDebt += centralBankCoverage;
        }
        if (destinationAccount is not null)
        {
            destinationAccount.Balance += payout;
        }
        bank.TotalDeposits -= input.Amount;
        deposit.Balance -= input.Amount;

        var isFullyWithdrawn = deposit.Balance <= 0m;
        if (isFullyWithdrawn)
        {
            deposit.ClosedAtTick = currentTick;
            deposit.ClosedAtUtc = DateTime.UtcNow;
        }

        // Ledger: depositor receives withdrawal
        if (payout > 0m && destinationAccount is not null)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = deposit.CompanyId!.Value,
                BuildingId = bank.Id,
                BankAccountId = destinationAccount.Id,
                Category = LedgerCategory.DepositWithdrawn,
                Description = $"Withdrawal from {bank.Name}",
                Amount = payout,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });

            if (actualBankPay > 0m)
            {
                db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bankCompany.Id,
                    BuildingId = bank.Id,
                    BankAccountId = bankLedgerAccount?.Id,
                    Category = LedgerCategory.BankAccountTransferOut,
                    Description = $"Customer withdrawal paid to {depositorCompany?.Name ?? "customer"}",
                    Amount = -actualBankPay,
                    RecordedAtTick = currentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }

        // If bank couldn't fully pay from own cash, record central-bank emergency coverage
        if (centralBankCoverage > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = bankCompany.Id,
                BuildingId = bank.Id,
                BankAccountId = bankLedgerAccount?.Id,
                Category = LedgerCategory.CentralBankBorrow,
                Description = $"Central bank emergency funding covering withdrawal shortfall of {centralBankCoverage:C0}",
                Amount = -centralBankCoverage,
                RecordedAtTick = currentTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();

        return MapToDepositSummary(deposit, bank, depositorCompany, null);
    }
}
