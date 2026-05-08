using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Handles the building destruction lifecycle for overdue/defaulted collateral buildings.
///
/// When a loan defaults and its collateral building is auto-listed for sale, the building
/// has <see cref="GameConstants.ForeclosureWindowTicks"/> ticks (3 game days = 72 ticks) to be sold.
/// If it remains unsold after this window:
///   - The building is marked as destroyed (<see cref="Building.DestroyedAtUtc"/> is set).
///   - The collateral appraised value is treated as liquidation proceeds.
///   - Outstanding debt is paid to the lender from proceeds; any surplus is returned to the borrower.
///   - The building lot is freed (made available for purchase again).
///   - A player notification is emitted.
/// </summary>
public sealed class BuildingDestructionPhase : ITickPhase
{
    public string Name => "BuildingDestruction";

    /// <summary>Runs just before player alerts so the destruction notification is included in the same tick.</summary>
    public int Order => 955;

    /// <inheritdoc cref="GameConstants.ForeclosureWindowTicks"/>
    public static long ForeclosureWindowTicks => GameConstants.ForeclosureWindowTicks;

    public async Task ProcessAsync(TickContext context)
    {
        // Find overdue/defaulted loans with collateral buildings that have passed the foreclosure window.
        var foreclosureDeadline = context.CurrentTick - GameConstants.ForeclosureWindowTicks;
        // DefaultedAtTick is recorded on the first missed payment (OVERDUE) and
        // remains set through DEFAULTED, so both statuses are valid here.
        var overdueDefaultedLoans = await context.Db.Loans
            .Where(l => (l.Status == LoanStatus.Overdue || l.Status == LoanStatus.Defaulted)
                && l.CollateralBuildingId != null
                && l.DefaultedAtTick != null
                && l.DefaultedAtTick <= foreclosureDeadline)
            .ToListAsync();

        foreach (var loan in overdueDefaultedLoans)
        {
            var building = await context.Db.Buildings
                .FirstOrDefaultAsync(b => b.Id == loan.CollateralBuildingId!.Value && b.DestroyedAtUtc == null);

            if (building is null)
            {
                // Already destroyed or manually sold — clear the reference so we don't recheck.
                loan.CollateralBuildingId = null;
                continue;
            }

            // Only destroy if the building is still for sale (it could have been sold via the market).
            if (!building.IsForSale)
            {
                // Sold — clear so we don't keep re-checking.
                loan.CollateralBuildingId = null;
                continue;
            }

            var buildingCurrencyCode = context.CitiesById.TryGetValue(building.CityId, out var buildingCity)
                ? buildingCity.CurrencyCode
                : "EUR";
            var loanCurrencyCode = context.BuildingsById.TryGetValue(loan.BankBuildingId, out var bankBuilding)
                && context.CitiesById.TryGetValue(bankBuilding.CityId, out var bankCity)
                ? bankCity.CurrencyCode
                : buildingCurrencyCode;

            var collateralAppraisedInBuildingCurrency = decimal.Round(
                FxRateHelper.ConvertAmount(
                    Math.Max(0m, loan.CollateralAppraisedValue ?? 0m),
                    loanCurrencyCode,
                    buildingCurrencyCode,
                    context.EurFxRates),
                2,
                MidpointRounding.AwayFromZero);

            var liquidationProceeds = decimal.Round(
                collateralAppraisedInBuildingCurrency * GameConstants.ForeclosureRefundFraction,
                2,
                MidpointRounding.AwayFromZero);
            var debtOutstandingLoanCurrency = decimal.Round(Math.Max(0m, loan.RemainingPrincipal), 2, MidpointRounding.AwayFromZero);
            var debtOutstandingInBuildingCurrency = decimal.Round(
                FxRateHelper.ConvertAmount(debtOutstandingLoanCurrency, loanCurrencyCode, buildingCurrencyCode, context.EurFxRates),
                2,
                MidpointRounding.AwayFromZero);

            var debtPayoutInBuildingCurrency = Math.Min(liquidationProceeds, debtOutstandingInBuildingCurrency);
            var debtPayoutInLoanCurrency = debtPayoutInBuildingCurrency >= debtOutstandingInBuildingCurrency
                ? debtOutstandingLoanCurrency
                : decimal.Round(
                    FxRateHelper.ConvertAmount(
                        debtPayoutInBuildingCurrency,
                        buildingCurrencyCode,
                        loanCurrencyCode,
                        context.EurFxRates),
                    2,
                    MidpointRounding.AwayFromZero);
            debtPayoutInLoanCurrency = Math.Min(debtOutstandingLoanCurrency, debtPayoutInLoanCurrency);

            var borrowerSurplus = Math.Max(0m, liquidationProceeds - debtPayoutInBuildingCurrency);

            // Find the borrower company to issue the surplus.
            context.CompaniesById.TryGetValue(loan.BorrowerCompanyId, out var borrowerCompany);
            context.CompaniesById.TryGetValue(loan.LenderCompanyId, out var lenderCompany);

            if (debtPayoutInLoanCurrency > 0m && lenderCompany is not null)
            {
                var lenderAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
                    context.Db,
                    lenderCompany.Id,
                    loanCurrencyCode);
                lenderAccount.Balance += debtPayoutInLoanCurrency;
                lenderAccount.ConcurrencyToken = Guid.NewGuid();

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = lenderCompany.Id,
                    BankAccountId = lenderAccount?.Id,
                    BuildingId = building.Id,
                    Category = LedgerCategory.LoanRepaymentPrincipal,
                    Description = $"Foreclosure liquidation payout for '{building.Name}' (loan #{loan.Id})",
                    Amount = debtPayoutInLoanCurrency,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            // Credit any surplus back to the borrower's company bank accounts.
            if (borrowerSurplus > 0m && borrowerCompany is not null)
            {
                var borrowerAccount = await CompanyBankingService.EnsurePreferredAccountAsync(
                    context.Db,
                    borrowerCompany.Id,
                    buildingCurrencyCode);
                borrowerAccount.Balance += borrowerSurplus;
                borrowerAccount.ConcurrencyToken = Guid.NewGuid();

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = borrowerCompany.Id,
                    BankAccountId = borrowerAccount?.Id,
                    BuildingId = building.Id,
                    Category = LedgerCategory.BuildingSale,
                    Description = $"Foreclosure liquidation surplus for '{building.Name}' – loan #{loan.Id}",
                    Amount = borrowerSurplus,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            // Free the building lot.
            var lot = await context.Db.BuildingLots
                .FirstOrDefaultAsync(l => l.BuildingId == building.Id);
            if (lot is not null)
            {
                lot.OwnerCompanyId = null;
                lot.BuildingId = null;
            }

            // Mark the building as destroyed.
            building.IsForSale = false;
            building.AskingPrice = null;
            building.ListedAtUtc = null;
            building.DestroyedAtUtc = DateTime.UtcNow;

            // Clear the collateral reference on the loan so we don't reprocess.
            loan.RemainingPrincipal = Math.Max(0m, debtOutstandingLoanCurrency - debtPayoutInLoanCurrency);
            loan.CollateralBuildingId = null;
            if (loan.RemainingPrincipal <= 0m)
            {
                loan.RemainingPrincipal = 0m;
                loan.Status = LoanStatus.Repaid;
                loan.ClosedAtUtc = DateTime.UtcNow;
            }

            // Persist an audit record.
            context.Db.BuildingDestructionRecords.Add(new BuildingDestructionRecord
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                BuildingName = building.Name,
                LoanId = loan.Id,
                CityId = building.CityId,
                OwnerCompanyId = building.CompanyId,
                OriginalPropertyValue = collateralAppraisedInBuildingCurrency,
                CompensationPaid = borrowerSurplus,
                DestructionTickCount = context.CurrentTick,
                DestructionReason = BuildingDestructionReason.GracePeriodExpired,
                CreatedAtUtc = DateTime.UtcNow,
            });

            // Emit player notification.
            if (borrowerCompany is not null)
            {
                PlayerNotificationService.Add(
                    context.Db,
                    borrowerCompany.PlayerId,
                    PlayerNotificationType.BuildingDestroyedByDefault,
                    "Building foreclosed and destroyed",
                    $"'{building.Name}' was unsold for 3 game days after loan default. The property has been destroyed. Debt payout: {debtPayoutInLoanCurrency:0.##} {loanCurrencyCode}. Surplus returned: {borrowerSurplus:0.##} {buildingCurrencyCode}.",
                    context.CurrentTick,
                    borrowerCompany.Id,
                    building.Id,
                    loanId: loan.Id);
            }
        }
    }
}
