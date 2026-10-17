using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Repays the full remaining debt of an overdue/defaulted loan immediately.
    /// The authenticated borrower must own the loan's borrower company.
    /// API-key callers can target only loans owned by the authenticated principal.
    /// </summary>
    [Authorize]
    public async Task<Loan> RepayLoanDebt(
        RepayLoanDebtInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var loan = await db.Loans
            .Include(l => l.BorrowerCompany).ThenInclude(c => c.BankAccounts)
            .Include(l => l.LenderCompany).ThenInclude(c => c.BankAccounts)
            .Include(l => l.LoanOffer)
            .Include(l => l.BankBuilding).ThenInclude(b => b.City)
            .Include(l => l.CollateralBuilding)
            .FirstOrDefaultAsync(l => l.Id == input.LoanId)
            ?? throw objectAuthorization.Deny(userId, "loan", input.LoanId, ObjectAuthorizationService.NotFoundReason);

        if (loan.BorrowerCompany.PlayerId != userId)
        {
            throw objectAuthorization.Deny(userId, "loan", input.LoanId, ObjectAuthorizationService.NotOwnedReason);
        }

        if (loan.Status == LoanStatus.Repaid || loan.RemainingPrincipal <= 0m)
        {
            return loan;
        }

        if (loan.CollateralBuildingId.HasValue)
        {
            var collateralOwnerId = await db.Buildings
                .AsNoTracking()
                .Where(b => b.Id == loan.CollateralBuildingId.Value)
                .Select(b => b.CompanyId)
                .FirstOrDefaultAsync();
            if (collateralOwnerId != loan.BorrowerCompanyId)
            {
                LoanCollateralSecurityAuditLogger.Add(
                    db,
                    userId,
                    action: "LOAN_RELEASE",
                    reason: "COLLATERAL_OWNERSHIP_CONFLICT",
                    loanId: loan.Id,
                    buildingId: loan.CollateralBuildingId,
                    detail: $"Expected borrower company {loan.BorrowerCompanyId}, actual owner {collateralOwnerId}.");
                await db.SaveChangesAsync();
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Collateral ownership changed before debt repayment could be committed.")
                        .SetCode("COLLATERAL_OWNERSHIP_CONFLICT")
                        .Build());
            }
        }

        var loanCurrencyCode = loan.BankBuilding.City?.CurrencyCode ?? "EUR";

        // If the caller explicitly nominated a repayment account, validate it first.
        if (input.RepaymentBankAccountId.HasValue)
        {
            var explicitAccount = loan.BorrowerCompany.BankAccounts.FirstOrDefault(
                a => a.Id == input.RepaymentBankAccountId.Value && a.ClosedAtUtc == null);

            if (explicitAccount is null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("The specified repayment account was not found for the borrower company.")
                        .SetCode("REPAYMENT_ACCOUNT_NOT_FOUND")
                        .Build());
            }

            if (!string.Equals(explicitAccount.CurrencyCode, loanCurrencyCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"The specified repayment account currency ({explicitAccount.CurrencyCode}) does not match the loan currency ({loanCurrencyCode}). Repayment must be made in the loan's origination currency.")
                        .SetCode("CURRENCY_MISMATCH")
                        .Build());
            }
        }

        var borrowerSettlementAccount = input.RepaymentBankAccountId.HasValue
            ? loan.BorrowerCompany.BankAccounts.FirstOrDefault(a =>
                a.Id == input.RepaymentBankAccountId.Value
                && a.ClosedAtUtc == null)
            : loan.BorrowerBankAccountId.HasValue
                ? loan.BorrowerCompany.BankAccounts.FirstOrDefault(a =>
                    a.Id == loan.BorrowerBankAccountId.Value
                    && a.ClosedAtUtc == null
                    && string.Equals(a.CurrencyCode, loanCurrencyCode, StringComparison.OrdinalIgnoreCase))
                : null;
        borrowerSettlementAccount ??= CompanyBankingService.FindPreferredAccount(
            loan.BorrowerCompany.BankAccounts,
            loanCurrencyCode);
        if (borrowerSettlementAccount is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"No active repayment account available in {loanCurrencyCode} for this loan.")
                    .SetCode("REPAYMENT_ACCOUNT_NOT_FOUND")
                    .Build());
        }
        var availableBalance = borrowerSettlementAccount.Balance;
        var payoffAmount = decimal.Round(Math.Max(0m, loan.RemainingPrincipal), 2, MidpointRounding.AwayFromZero);

        if (availableBalance < payoffAmount)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Insufficient funds to repay this debt in full.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        borrowerSettlementAccount.Balance -= payoffAmount;
        borrowerSettlementAccount.ConcurrencyToken = Guid.NewGuid();

        var lenderCreditedAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
            db,
            loan.LenderCompanyId,
            loanCurrencyCode);
        lenderCreditedAccount.Balance += payoffAmount;
        lenderCreditedAccount.ConcurrencyToken = Guid.NewGuid();

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;
        var nowUtc = DateTime.UtcNow;

        // The payoff settles the accrued missed-payment penalty too: book it as an expense for the borrower and income for the
        // lender, the rest as principal. The cash moved (payoffAmount) is unchanged.
        var penaltyPart = decimal.Round(Math.Min(Math.Max(0m, loan.AccumulatedPenalty), payoffAmount), 2, MidpointRounding.AwayFromZero);
        var principalPart = payoffAmount - penaltyPart;

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = loan.BorrowerCompanyId,
            BankAccountId = borrowerSettlementAccount?.Id,
            BuildingId = loan.CollateralBuildingId,
            Category = LedgerCategory.LoanRepaymentPrincipal,
            Description = $"Manual debt repayment for loan {loan.Id}",
            Amount = -principalPart,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });

        if (penaltyPart > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = loan.BorrowerCompanyId,
                BankAccountId = borrowerSettlementAccount?.Id,
                BuildingId = loan.CollateralBuildingId,
                Category = LedgerCategory.LoanPenalty,
                Description = $"Missed-payment penalty paid with the manual repayment of loan {loan.Id}",
                Amount = -penaltyPart,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = loan.LenderCompanyId,
                BankAccountId = lenderCreditedAccount?.Id,
                BuildingId = loan.CollateralBuildingId,
                Category = LedgerCategory.LoanInterestIncome,
                Description = $"Late-payment penalty income from {loan.BorrowerCompany.Name} for loan {loan.Id}",
                Amount = penaltyPart,
                RecordedAtTick = currentTick,
                RecordedAtUtc = nowUtc,
            });
        }

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = loan.LenderCompanyId,
            BankAccountId = lenderCreditedAccount?.Id,
            BuildingId = loan.CollateralBuildingId,
            Category = LedgerCategory.LoanRepaymentPrincipal,
            Description = $"Borrower {loan.BorrowerCompany.Name} repaid overdue/defaulted debt for loan {loan.Id}",
            Amount = principalPart,
            RecordedAtTick = currentTick,
            RecordedAtUtc = nowUtc,
        });

        loan.RemainingPrincipal = 0m;
        loan.PaymentsMade = loan.TotalPayments;
        loan.MissedPayments = 0;
        loan.AccumulatedPenalty = 0m;
        loan.Status = LoanStatus.Repaid;
        loan.ClosedAtUtc = nowUtc;
        loan.DefaultedAtTick = null;
        loan.LoanOffer.UsedCapacity = Math.Max(0m, loan.LoanOffer.UsedCapacity - loan.OriginalPrincipal);
        loan.ConcurrencyToken = Guid.NewGuid();

        if (loan.CollateralBuilding is not null && loan.CollateralBuilding.DestroyedAtUtc == null)
        {
            loan.CollateralBuilding.IsForSale = false;
            loan.CollateralBuilding.AskingPrice = null;
            loan.CollateralBuilding.ListedAtUtc = null;
            loan.CollateralBuilding.ConcurrencyToken = Guid.NewGuid();
        }

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
