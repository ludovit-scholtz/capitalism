using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Processes bank deposit interest payments each tick.
///
/// For each active deposit in each bank building:
///   - Calculates the per-tick interest: amount × (rate / 100) / TicksPerYear
///   - Transfers interest from the bank company's cash to the depositor company's cash
///   - Writes ledger entries for both sides
///
/// If a bank lacks sufficient cash to pay interest (illiquid), it pays what it can
/// and the shortfall is recorded as a central-bank-borrow entry. This prevents
/// bank failures from crashing the simulation while keeping the economic signal visible.
///
/// Order: 960 (after LoanRepayment at 950, before Tax at 1000)
/// </summary>
public sealed class BankInterestPhase : ITickPhase
{
    public string Name => "BankInterest";

    /// <summary>Runs after loan repayments so the bank's cash balance reflects incoming payments before paying out interest.</summary>
    public int Order => 960;

    public async Task ProcessAsync(TickContext context)
    {
        // Load all active deposits grouped by bank building.
        var deposits = await context.Db.BankDeposits
            .Where(d => d.IsActive && d.Amount > 0m)
            .Include(d => d.BankBuilding)
            .ToListAsync();

        if (deposits.Count == 0)
            return;

        // Group by bank building for efficient per-bank cash checks.
        var depositsByBank = deposits
            .GroupBy(d => d.BankBuildingId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (bankBuildingId, bankDeposits) in depositsByBank)
        {
            if (!context.BuildingsById.TryGetValue(bankBuildingId, out var bank))
                continue;

            if (!context.CompaniesById.TryGetValue(bank.CompanyId, out var bankCompany))
                continue;

            foreach (var deposit in bankDeposits)
            {
                // Skip own base-capital deposits (bank doesn't pay itself interest)
                if (deposit.IsBaseCapital && deposit.DepositorCompanyId == bank.CompanyId)
                    continue;

                if (!context.CompaniesById.TryGetValue(deposit.DepositorCompanyId, out var depositor))
                    continue;

                // Per-tick interest = annual_rate% * amount / TicksPerYear
                var annualRate = deposit.DepositInterestRatePercent / 100m;
                var interestThisTick = decimal.Round(
                    deposit.Amount * annualRate / GameConstants.TicksPerYear,
                    4,
                    MidpointRounding.AwayFromZero);

                if (interestThisTick <= 0m)
                    continue;

                // Pay what the bank can; if illiquid, emit a central-bank-borrow entry for the shortfall.
                var actualPaid = Math.Min(interestThisTick, Math.Max(0m, bankCompany.Cash));
                var shortfall = interestThisTick - actualPaid;

                bankCompany.Cash -= actualPaid;
                depositor.Cash += actualPaid;
                deposit.TotalInterestPaid += actualPaid;

                // Ledger – bank pays deposit interest (expense)
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.DepositInterestPaid,
                    Description = $"Deposit interest to {depositor.Name}",
                    Amount = -actualPaid,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });

                // Ledger – depositor receives interest (income)
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = depositor.Id,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.DepositInterestReceived,
                    Description = $"Deposit interest from {bank.Name}",
                    Amount = actualPaid,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });

                if (shortfall > 0m)
                {
                    // Bank is illiquid – record central-bank emergency funding
                    context.Db.LedgerEntries.Add(new LedgerEntry
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = bank.CompanyId,
                        BuildingId = bank.Id,
                        Category = LedgerCategory.CentralBankBorrow,
                        Description = $"Central bank emergency funding for interest shortfall to {depositor.Name}",
                        Amount = -shortfall,
                        RecordedAtTick = context.CurrentTick,
                        RecordedAtUtc = DateTime.UtcNow,
                    });
                }
            }
        }
    }
}
