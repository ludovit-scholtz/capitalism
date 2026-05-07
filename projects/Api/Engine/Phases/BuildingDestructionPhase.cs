using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Handles the building destruction lifecycle for loan-defaulted collateral buildings.
///
/// When a loan defaults and its collateral building is auto-listed for sale, the building
/// has <see cref="GameConstants.ForeclosureWindowTicks"/> ticks (3 game days = 72 ticks) to be sold.
/// If it remains unsold after this window:
///   - The building is marked as destroyed (<see cref="Building.DestroyedAtUtc"/> is set).
///   - The owning company receives <see cref="GameConstants.ForeclosureRefundFraction"/> of the
///     collateral appraised value as a refund.
///   - The building lot is freed (made available for purchase again).
///   - A player notification is emitted.
/// </summary>
public sealed class BuildingDestructionPhase : ITickPhase
{
    public string Name => "BuildingDestruction";

    /// <summary>Runs just before player alerts so the destruction notification is included in the same tick.</summary>
    public int Order => 955;

    // Expose constants for tests — now delegating to GameConstants.
    /// <inheritdoc cref="GameConstants.ForeclosureWindowTicks"/>
    public static long ForeclosureWindowTicks => GameConstants.ForeclosureWindowTicks;

    /// <inheritdoc cref="GameConstants.ForeclosureRefundFraction"/>
    public static decimal RefundFraction => GameConstants.ForeclosureRefundFraction;

    public async Task ProcessAsync(TickContext context)
    {
        // Find defaulted loans with collateral buildings that have passed the foreclosure window.
        var foreclosureDeadline = context.CurrentTick - GameConstants.ForeclosureWindowTicks;
        var overdueDefaultedLoans = await context.Db.Loans
            .Where(l => l.Status == LoanStatus.Defaulted
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

            // Determine refund amount: ForeclosureRefundFraction of collateral appraised value.
            var refundAmount = ComputeRefund(loan.CollateralAppraisedValue);

            // Find the borrower company to issue the refund.
            context.CompaniesById.TryGetValue(loan.BorrowerCompanyId, out var borrowerCompany);

            // Credit the refund to the company's bank accounts.
            if (refundAmount > 0m && borrowerCompany is not null)
            {
                var companyAccounts = context.GetCompanyBankAccounts(borrowerCompany.Id);
                CompanyBankingService.TryCredit(companyAccounts, refundAmount, null, out _);

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = borrowerCompany.Id,
                    Category = LedgerCategory.BuildingAcquisition, // refund credit
                    Description = $"Building foreclosure refund ({GameConstants.ForeclosureRefundFraction * 100:0}%) for '{building.Name}' – defaulted loan #{loan.Id}",
                    Amount = refundAmount,
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
            loan.CollateralBuildingId = null;

            // Persist an audit record.
            context.Db.BuildingDestructionRecords.Add(new BuildingDestructionRecord
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                BuildingName = building.Name,
                LoanId = loan.Id,
                CityId = building.CityId,
                OwnerCompanyId = building.CompanyId,
                OriginalPropertyValue = loan.CollateralAppraisedValue ?? 0m,
                CompensationPaid = refundAmount,
                DestructionTickCount = context.CurrentTick,
                DestructionReason = BuildingDestructionReason.GracePeriodExpired,
                CreatedAtUtc = DateTime.UtcNow,
            });
            loan.CollateralBuildingId = null;

            // Emit player notification.
            if (borrowerCompany is not null)
            {
                PlayerNotificationService.Add(
                    context.Db,
                    borrowerCompany.PlayerId,
                    PlayerNotificationType.BuildingDestroyedByDefault,
                    "Building foreclosed and destroyed",
                    $"'{building.Name}' was unsold for 3 game days after loan default. The property has been destroyed. A refund of {refundAmount:0.##} has been credited to your account.",
                    context.CurrentTick,
                    borrowerCompany.Id,
                    building.Id,
                    loanId: loan.Id);
            }
        }
    }

    /// <summary>
    /// Computes the foreclosure refund for the given appraised value.
    /// Returns <see cref="GameConstants.ForeclosureRefundFraction"/> of the value, rounded to 2 decimal places.
    /// Returns 0 when <paramref name="appraisedValue"/> is null or zero.
    /// </summary>
    public static decimal ComputeRefund(decimal? appraisedValue) =>
        appraisedValue.HasValue && appraisedValue.Value > 0m
            ? decimal.Round(appraisedValue.Value * GameConstants.ForeclosureRefundFraction, 2, MidpointRounding.AwayFromZero)
            : 0m;
}
