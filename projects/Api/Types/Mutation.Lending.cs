using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// Bank lending mutations: accepting collateral-based loans from any bank (including government banks).
/// Players select a bank, pledge one of their buildings as collateral, and borrow up to 70% of its
/// appraised value — provided the bank has sufficient deposit capacity.
/// </summary>
public sealed partial class Mutation
{
    // ── Bank Lending ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Requests a collateral-backed loan from a bank building.
    /// The borrower must own the company; a collateral building is required; the bank must be open
    /// (BaseCapitalDeposited = true) and have sufficient deposit capacity (90% of TotalDeposits).
    /// Self-lending is blocked at both company and player level.
    /// </summary>
    [Authorize]
    public async Task<Loan> AcceptLoan(
        AcceptLoanInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Verify borrower owns the company.
        var borrower = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == input.BorrowerCompanyId && c.PlayerId == userId);

        if (borrower is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Borrower company not found or you do not own it.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        }

        return await AcceptLoanFromBankDirectAsync(input, borrower, db, userId, httpContextAccessor);
    }

    /// <param name="userId">Pre-extracted from the JWT claim to avoid double-extraction; also used for player-level self-lending check (bank.Company.PlayerId).</param>
    /// <param name="httpContextAccessor">Still required to pass RequestAborted to async helpers.</param>
    private async Task<Loan> AcceptLoanFromBankDirectAsync(
        AcceptLoanInput input,
        Company borrower,
        AppDbContext db,
        Guid userId,
        IHttpContextAccessor httpContextAccessor)
    {
        var bank = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstOrDefaultAsync(b => b.Id == input.LoanOfferId && b.Type == BuildingType.Bank);

        if (bank is null || !bank.BaseCapitalDeposited)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Bank not found or is not open for lending.")
                    .SetCode("BANK_NOT_FOUND")
                    .Build());
        }

        // Self-lending guard: borrower cannot borrow from their own bank.
        if (bank.CompanyId == borrower.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A company cannot borrow from its own bank.")
                    .SetCode("SELF_LENDING_NOT_ALLOWED")
                    .Build());
        }

        if (bank.Company?.PlayerId == userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You cannot borrow from a bank you own.")
                    .SetCode("SELF_LENDING_NOT_ALLOWED")
                    .Build());
        }

        if (input.PrincipalAmount < 1_000m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Minimum loan amount is $1,000.")
                    .SetCode("INVALID_PRINCIPAL")
                    .Build());
        }

        if (!input.CollateralBuildingId.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A collateral building is required for bank loan requests.")
                    .SetCode("COLLATERAL_REQUIRED")
                    .Build());
        }

        if (input.DurationTicks.HasValue && (input.DurationTicks.Value < 1 || input.DurationTicks.Value > 87_600))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Loan duration must be between 1 tick (1 in-game hour) and 87,600 ticks (10 in-game years).")
                    .SetCode("INVALID_DURATION")
                    .Build());
        }

        var outstandingPrincipal = await db.Loans
            .Where(l => l.BankBuildingId == bank.Id && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .SumAsync(l => (decimal?)l.RemainingPrincipal) ?? 0m;
        var availableLendingCapacity = Math.Max(0m, (bank.TotalDeposits * 0.90m) - outstandingPrincipal);

        if (input.PrincipalAmount > availableLendingCapacity)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"The bank only has {availableLendingCapacity:C0} of lending capacity available.")
                    .SetCode("INSUFFICIENT_CAPACITY")
                    .Build());
        }

        var lenderAccounts = await LoadActiveCompanyBankAccountsAsync(
            db,
            bank.CompanyId,
            httpContextAccessor.HttpContext!.RequestAborted);

        if (CompanyBankingService.GetTotalBalance(lenderAccounts) < input.PrincipalAmount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The lender does not have sufficient funds to cover this loan at this time.")
                    .SetCode("LENDER_INSUFFICIENT_FUNDS")
                    .Build());
        }

        var collateralBuilding = await db.Buildings
            .FirstOrDefaultAsync(b => b.Id == input.CollateralBuildingId.Value && b.CompanyId == borrower.Id);

        if (collateralBuilding is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Collateral building not found or is not owned by your company.")
                    .SetCode("COLLATERAL_NOT_OWNED")
                    .Build());
        }

        var alreadyPledged = await db.Loans
            .AnyAsync(l => l.CollateralBuildingId == input.CollateralBuildingId.Value
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue));
        if (alreadyPledged)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building is already pledged as collateral for another active loan.")
                    .SetCode("COLLATERAL_ALREADY_PLEDGED")
                    .Build());
        }

        var collateralAppraisedValue = WealthCalculator.GetBuildingValue(collateralBuilding);
        var maxBorrowable = decimal.Round(collateralAppraisedValue * 0.70m, 2, MidpointRounding.AwayFromZero);
        if (input.PrincipalAmount > maxBorrowable)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"The requested principal of {input.PrincipalAmount:C0} exceeds the collateral lending capacity of {maxBorrowable:C0}.")
                    .SetCode("EXCEEDS_COLLATERAL_LIMIT")
                    .Build());
        }

        var currentTick = await db.GameStates.AsNoTracking().Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();
        var durationTicks = input.DurationTicks ?? GameConstants.TicksPerYear;
        var annualRate = bank.LendingInterestRatePercent ?? 8m;

        var ticksPerPayment = 1L;
        var totalPayments = (int)Math.Max(1L, durationTicks);
        var bankCurrencyCode = bank.City?.CurrencyCode ?? "EUR";
        var paymentAmount = ComputeEstimatedTickPayment(input.PrincipalAmount, annualRate, totalPayments);

        var borrowerAccount = await ResolveLoanSettlementAccountAsync(
            db,
            borrower.Id,
            bankCurrencyCode,
            input.BankAccountId,
            httpContextAccessor.HttpContext.RequestAborted);

        CompanyBankingService.TryDebit(lenderAccounts, input.PrincipalAmount);
        borrowerAccount.Balance += input.PrincipalAmount;

        var internalOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = bank.CompanyId,
            AnnualInterestRatePercent = annualRate,
            MaxPrincipalPerLoan = input.PrincipalAmount,
            TotalCapacity = input.PrincipalAmount,
            UsedCapacity = input.PrincipalAmount,
            DurationTicks = durationTicks,
            IsActive = false,
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = internalOffer.Id,
            BorrowerCompanyId = borrower.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = bank.CompanyId,
            OriginalPrincipal = input.PrincipalAmount,
            RemainingPrincipal = input.PrincipalAmount,
            AnnualInterestRatePercent = annualRate,
            DurationTicks = durationTicks,
            StartTick = currentTick,
            DueTick = currentTick + durationTicks,
            NextPaymentTick = currentTick + ticksPerPayment,
            PaymentAmount = paymentAmount,
            PaymentsMade = 0,
            TotalPayments = totalPayments,
            BorrowerBankAccountId = borrowerAccount.Id,
            Status = LoanStatus.Active,
            MissedPayments = 0,
            AccumulatedPenalty = 0m,
            AcceptedAtUtc = DateTime.UtcNow,
            CollateralBuildingId = collateralBuilding.Id,
            CollateralAppraisedValue = collateralAppraisedValue,
        };

        db.LoanOffers.Add(internalOffer);
        db.Loans.Add(loan);
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = borrower.Id,
            Category = LedgerCategory.LoanOrigination,
            Description = $"Loan received from {bank.Company!.Name} via {bank.Name} – {annualRate}% p.a. over {durationTicks} in-game hours (secured against {collateralBuilding.Name})",
            Amount = input.PrincipalAmount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return loan;
    }

    private static decimal ComputeEstimatedTickPayment(decimal principalAmount, decimal annualRatePercent, int totalPayments)
    {
        var safeTotalPayments = Math.Max(1, totalPayments);
        var principalPerTick = principalAmount / safeTotalPayments;
        var firstTickInterest = principalAmount * (annualRatePercent / 100m) / GameConstants.TicksPerYear;
        return decimal.Round(principalPerTick + firstTickInterest, 4, MidpointRounding.AwayFromZero);
    }

    private static async Task<BankAccount> ResolveLoanSettlementAccountAsync(
        AppDbContext db,
        Guid borrowerCompanyId,
        string requiredCurrencyCode,
        Guid? requestedBankAccountId,
        CancellationToken cancellationToken)
    {
        if (requestedBankAccountId.HasValue)
        {
            var requestedAccount = await db.BankAccounts
                .Include(a => a.Company)
                .FirstOrDefaultAsync(
                    a => a.Id == requestedBankAccountId.Value
                        && a.CompanyId == borrowerCompanyId
                        && a.ClosedAtUtc == null,
                    cancellationToken);

            if (requestedAccount is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The selected settlement account was not found for the borrower company.")
                        .SetCode("ACCOUNT_NOT_FOUND")
                        .Build());
            }

            if (!string.Equals(requestedAccount.CurrencyCode, requiredCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"The selected settlement account currency ({requestedAccount.CurrencyCode}) does not match the bank city currency ({requiredCurrencyCode}).")
                        .SetCode("CURRENCY_MISMATCH")
                        .Build());
            }

            return requestedAccount;
        }

        return await ResolveCompanyTransferAccountAsync(
            db,
            borrowerCompanyId,
            requiredCurrencyCode,
            cancellationToken: cancellationToken);
    }
}
