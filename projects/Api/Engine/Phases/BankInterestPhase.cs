using Api.Data.Entities;
using Api.Types;
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
/// If a bank lacks sufficient cash to pay interest (illiquid), the central bank
/// provides emergency funding: the shortfall is added to Building.CentralBankDebt
/// and the depositor is still paid in full.
///
/// Central-bank debt incurs variable interest (2–5% p.a.) charged to the bank each tick.
/// If the bank later has surplus cash, it automatically repays central-bank debt first.
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

        // Load bank buildings with outstanding central-bank debt for interest charging
        var bankBuildings = await context.Db.Buildings
            .Where(b => b.Type == BuildingType.Bank && b.BaseCapitalDeposited)
            .ToListAsync();

        if (deposits.Count == 0 && bankBuildings.Count == 0)
            return;

        // Compute central-bank rate (shared across all banks this tick)
        var borrowingBankCount = bankBuildings.Count(b => b.CentralBankDebt > 0m);
        const decimal minCbRate = 2m;
        const decimal maxCbRate = 5m;
        const int maxBanksForMaxRate = 5;
        var centralBankRatePercent = minCbRate + (maxCbRate - minCbRate) * Math.Min(1m, (decimal)borrowingBankCount / maxBanksForMaxRate);

        // Group deposits by bank building for efficient per-bank cash checks.
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

                // Pay what the bank can from own cash; central bank covers the rest
                var actualPaid = Math.Min(interestThisTick, Math.Max(0m, bankCompany.Cash));
                var shortfall = interestThisTick - actualPaid;

                bankCompany.Cash -= actualPaid;
                depositor.Cash += interestThisTick; // depositor always gets the full amount
                deposit.TotalInterestPaid += interestThisTick;

                if (shortfall > 0m)
                {
                    // Central bank covers the shortfall and records the debt
                    bank.CentralBankDebt += shortfall;
                }

                // Ledger – bank pays deposit interest (expense)
                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.DepositInterestPaid,
                    Description = $"Deposit interest to {depositor.Name}",
                    Amount = -interestThisTick,
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
                    Amount = interestThisTick,
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

        // ── Central-bank debt interest and auto-repayment ─────────────────────
        foreach (var bank in bankBuildings)
        {
            if (bank.CentralBankDebt <= 0m)
                continue;

            if (!context.CompaniesById.TryGetValue(bank.CompanyId, out var bankCompany))
                continue;

            // Charge interest on central-bank debt each tick
            var cbInterest = decimal.Round(
                bank.CentralBankDebt * (centralBankRatePercent / 100m) / GameConstants.TicksPerYear,
                4,
                MidpointRounding.AwayFromZero);

            if (cbInterest > 0m)
            {
                // Interest compounds onto the debt if bank can't afford it
                var interestPaid = Math.Min(cbInterest, Math.Max(0m, bankCompany.Cash));
                bankCompany.Cash -= interestPaid;
                bank.CentralBankDebt += cbInterest - interestPaid; // unpaid interest compounds

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.LoanInterestExpense,
                    Description = $"Central bank debt interest at {centralBankRatePercent:F2}% p.a.",
                    Amount = -cbInterest,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }

            // Auto-repay central-bank debt with any surplus cash (above reserve requirement)
            var reserveNeeded = bank.TotalDeposits * 0.10m;
            var surplusCash = bankCompany.Cash - reserveNeeded;
            if (surplusCash > 0m && bank.CentralBankDebt > 0m)
            {
                var repayment = Math.Min(surplusCash, bank.CentralBankDebt);
                bankCompany.Cash -= repayment;
                bank.CentralBankDebt -= repayment;

                context.Db.LedgerEntries.Add(new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = bank.CompanyId,
                    BuildingId = bank.Id,
                    Category = LedgerCategory.CentralBankRepay,
                    Description = $"Automatic central bank debt repayment from surplus cash",
                    Amount = -repayment,
                    RecordedAtTick = context.CurrentTick,
                    RecordedAtUtc = DateTime.UtcNow,
                });
            }
        }
    }
}
