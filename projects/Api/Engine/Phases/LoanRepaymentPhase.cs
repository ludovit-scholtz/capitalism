using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Processes scheduled loan repayments each tick.
/// For each active loan whose NextPaymentTick has been reached:
///   - If the borrower has sufficient cash, deduct the instalment (principal + interest split),
///     add the cash to the lender, and write ledger entries for both sides.
///   - If the borrower cannot cover the payment, record a missed payment, accumulate a penalty,
///     and set the loan status to OVERDUE or DEFAULTED.
/// When the final payment is made, mark the loan REPAID and free the offer's capacity.
/// </summary>
public sealed class LoanRepaymentPhase : ITickPhase
{
    public string Name => "LoanRepayment";

    /// <summary>Runs after operating costs but before tax, so companies pay debts from real cash.</summary>
    public int Order => 950;

    /// <summary>Penalty rate applied to the remaining principal on each missed payment (5%).</summary>
    private const decimal MissedPaymentPenaltyRate = 0.05m;

    /// <summary>Number of missed payments before the loan is considered DEFAULTED.</summary>
    private const int DefaultedMissedPaymentThreshold = 3;
    private const long ForeclosureWindowTicks = GameConstants.ForeclosureWindowTicks;

    public Task ProcessAsync(TickContext context)
    {
        // Load loans with NextPaymentTick <= currentTick that are not yet fully repaid.
        var dueLoans = context.Db.Loans
            .Include(l => l.LoanOffer)
            .Where(l => l.NextPaymentTick <= context.CurrentTick
                && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue))
            .ToList();

        foreach (var loan in dueLoans)
        {
            if (!context.CompaniesById.TryGetValue(loan.BorrowerCompanyId, out var borrower))
                continue;
            if (!context.CompaniesById.TryGetValue(loan.LenderCompanyId, out var lender))
                continue;

            // Determine how many payments are due (handles tick-skipping edge cases).
            var paymentsDue = ComputePaymentsDue(loan, context.CurrentTick);

            for (var paymentIndex = 0; paymentIndex < paymentsDue; paymentIndex++)
            {
                var paymentTick = loan.NextPaymentTick;
                var isLastPayment = loan.PaymentsMade + 1 >= loan.TotalPayments;

                var interestPerPayment = ComputeTickInterest(loan);
                var principalPayment = ComputePrincipalForPayment(loan, isLastPayment);
                var totalPayment = principalPayment + interestPerPayment;

                ProcessSinglePayment(context, loan, borrower, lender, totalPayment, principalPayment, interestPerPayment, paymentTick, isLastPayment);

                // Stop processing further payments if the loan was closed or defaulted.
                if (loan.Status == LoanStatus.Repaid || loan.Status == LoanStatus.Defaulted)
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private static int ComputePaymentsDue(Loan loan, long currentTick)
    {
        if (loan.TotalPayments <= 0) return 0;
        var ticksPerPayment = loan.DurationTicks / loan.TotalPayments;
        if (ticksPerPayment <= 0) return 1;

        var paymentsDue = 0;
        var checkTick = loan.NextPaymentTick;
        while (checkTick <= currentTick && loan.PaymentsMade + paymentsDue < loan.TotalPayments)
        {
            paymentsDue++;
            checkTick += ticksPerPayment;
        }

        return Math.Max(1, paymentsDue);
    }

    private static decimal ComputeTickInterest(Loan loan)
    {
        var periodicRate = (loan.AnnualInterestRatePercent / 100m) / GameConstants.TicksPerYear;
        return decimal.Round(loan.RemainingPrincipal * periodicRate, 4, MidpointRounding.AwayFromZero);
    }

    private static decimal ComputePrincipalForPayment(Loan loan, bool isLastPayment)
    {
        if (isLastPayment)
        {
            return decimal.Round(loan.RemainingPrincipal, 4, MidpointRounding.AwayFromZero);
        }

        var paymentsRemaining = Math.Max(1, loan.TotalPayments - loan.PaymentsMade);
        var principal = decimal.Round(loan.RemainingPrincipal / paymentsRemaining, 4, MidpointRounding.AwayFromZero);
        return Math.Min(principal, loan.RemainingPrincipal);
    }

    private static void ProcessSinglePayment(
        TickContext context,
        Loan loan,
        Company borrower,
        Company lender,
        decimal totalPayment,
        decimal principalPayment,
        decimal interestPayment,
        long paymentTick,
        bool isLastPayment)
    {
        var ticksPerPayment = loan.TotalPayments > 0 ? loan.DurationTicks / loan.TotalPayments : loan.DurationTicks;
        if (ticksPerPayment <= 0)
        {
            ticksPerPayment = 1;
        }

        var borrowerSettlementAccount = loan.BorrowerBankAccountId.HasValue
            && context.BankAccountsById.TryGetValue(loan.BorrowerBankAccountId.Value, out var configuredAccount)
            ? configuredAccount
            : null;
        var availableBorrowerBalance = borrowerSettlementAccount?.Balance ?? context.GetCompanyBankBalance(borrower.Id);

        if (availableBorrowerBalance >= totalPayment)
        {
            // Successful payment.
            if (borrowerSettlementAccount is not null)
            {
                borrowerSettlementAccount.Balance -= totalPayment;
            }
            else
            {
                CompanyBankingService.TryDebit(context.GetCompanyBankAccounts(borrower.Id), totalPayment);
            }

            CompanyBankingService.TryCredit(context.GetCompanyBankAccounts(lender.Id), totalPayment, null, out var lenderCreditedAccount);
            loan.RemainingPrincipal = Math.Max(0m, loan.RemainingPrincipal - principalPayment);
            loan.PaymentsMade++;
            loan.PaymentAmount = totalPayment;
            loan.NextPaymentTick = paymentTick + ticksPerPayment;

            // If borrower was overdue, restore to active.
            if (loan.Status == LoanStatus.Overdue)
            {
                loan.Status = LoanStatus.Active;
                loan.MissedPayments = 0;
                loan.DefaultedAtTick = null;
            }

            // Borrower ledger: principal repayment.
            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = borrower.Id,
                BankAccountId = borrowerSettlementAccount?.Id,
                Category = LedgerCategory.LoanRepaymentPrincipal,
                Description = $"Loan repayment (principal) – payment {loan.PaymentsMade}/{loan.TotalPayments}",
                Amount = -principalPayment,
                RecordedAtTick = context.CurrentTick,
                RecordedAtUtc = DateTime.UtcNow
            });

            // Lender ledger: principal income.
            if (principalPayment > 0m)
            {
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = lender.Id,
                    BankAccountId = lenderCreditedAccount?.Id,
                    Category = LedgerCategory.LoanRepaymentPrincipal,
                    Description = $"Loan repayment (principal) from {borrower.Name} – payment {loan.PaymentsMade}/{loan.TotalPayments}",
                    Amount = principalPayment,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // Borrower ledger: interest expense.
            if (interestPayment > 0m)
            {
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = borrower.Id,
                    BankAccountId = borrowerSettlementAccount?.Id,
                    Category = LedgerCategory.LoanInterestExpense,
                    Description = $"Loan interest expense – payment {loan.PaymentsMade}/{loan.TotalPayments}",
                    Amount = -interestPayment,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // Lender ledger: interest income.
            if (interestPayment > 0m)
            {
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = lender.Id,
                    BankAccountId = lenderCreditedAccount?.Id,
                    Category = LedgerCategory.LoanInterestIncome,
                    Description = $"Loan interest income from {borrower.Name} – payment {loan.PaymentsMade}/{loan.TotalPayments}",
                    Amount = interestPayment,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // Check if fully repaid.
            if (isLastPayment || loan.PaymentsMade >= loan.TotalPayments || loan.RemainingPrincipal <= 0m)
            {
                loan.Status = LoanStatus.Repaid;
                loan.RemainingPrincipal = 0m;
                loan.ClosedAtUtc = DateTime.UtcNow;

                // Free the capacity on the offer.
                loan.LoanOffer.UsedCapacity = Math.Max(0m, loan.LoanOffer.UsedCapacity - loan.OriginalPrincipal);
            }
        }
        else
        {
            // Missed payment.
            loan.MissedPayments++;
            loan.NextPaymentTick = paymentTick + ticksPerPayment;

            // Apply penalty on remaining principal.
            var penalty = decimal.Round(loan.RemainingPrincipal * MissedPaymentPenaltyRate, 4, MidpointRounding.AwayFromZero);
            loan.AccumulatedPenalty += penalty;
            loan.RemainingPrincipal += penalty;

            // Charge borrower whatever cash they have as partial payment (optional, simple model: skip full payment).
            // For first pass: record missed payment only (no partial payment collection).

            // Record penalty in ledger for borrower.
            if (penalty > 0m)
            {
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = borrower.Id,
                    BankAccountId = borrowerSettlementAccount?.Id,
                    Category = LedgerCategory.LoanPenalty,
                    Description = $"Missed loan payment penalty (missed payment #{loan.MissedPayments})",
                    Amount = -penalty,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow
                });
            }

            // Update status.
            loan.Status = loan.MissedPayments >= DefaultedMissedPaymentThreshold
                ? LoanStatus.Defaulted
                : LoanStatus.Overdue;

            if (!loan.DefaultedAtTick.HasValue)
            {
                loan.DefaultedAtTick = context.CurrentTick;
            }

            // Auto-list collateral building for sale at (1 − ForeclosureAutoListDiscount) of appraised value.
            if (loan.CollateralBuildingId.HasValue && loan.CollateralAppraisedValue.HasValue)
            {
                var collateralBuilding = context.Db.Buildings
                    .FirstOrDefault(b => b.Id == loan.CollateralBuildingId.Value && !b.IsForSale && b.DestroyedAtUtc == null);
                if (collateralBuilding is not null)
                {
                    collateralBuilding.IsForSale = true;
                    collateralBuilding.AskingPrice = decimal.Round(loan.CollateralAppraisedValue.Value * (1m - GameConstants.ForeclosureAutoListDiscount), 2, MidpointRounding.AwayFromZero);
                    collateralBuilding.ListedAtUtc = DateTime.UtcNow;
                }
            }

            if (loan.Status == LoanStatus.Defaulted)
            {
                // Capacity remains locked (lender is owed money but capacity was consumed).
                loan.ClosedAtUtc = DateTime.UtcNow;
            }

            var overdueAmount = decimal.Round(principalPayment + interestPayment + penalty, 2, MidpointRounding.AwayFromZero);
            var bankBuildingName = context.BuildingsById.TryGetValue(loan.BankBuildingId, out var bankBuilding)
                ? bankBuilding.Name
                : "Bank";
            var collateralBuildingName = loan.CollateralBuildingId.HasValue
                && context.BuildingsById.TryGetValue(loan.CollateralBuildingId.Value, out var collateral)
                ? collateral.Name
                : "Collateral building";
            var ticksRemaining = Math.Max(0L, (loan.DefaultedAtTick ?? context.CurrentTick) + ForeclosureWindowTicks - context.CurrentTick);

            PlayerNotificationService.Add(
                context.Db,
                borrower.PlayerId,
                PlayerNotificationType.LoanPaymentMissed,
                "Loan payment missed",
                $"You have a missed payment at {bankBuildingName}. {overdueAmount:0.##} overdue. Building {collateralBuildingName} will be seized in {ticksRemaining} ticks if unresolved.",
                context.CurrentTick,
                borrower.Id,
                loan.CollateralBuildingId,
                loanId: loan.Id,
                bankAccountId: loan.BorrowerBankAccountId);
        }
    }
}
