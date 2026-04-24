using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Applies annual income tax on the configured tax-cycle boundary.
/// </summary>
public sealed class TaxPhase : ITickPhase
{
    public string Name => "Tax";
    public int Order => 1000;

    public async Task ProcessAsync(TickContext context)
    {
        var gs = context.GameState;
        if (gs.TaxCycleTicks <= 0) return;
        if (gs.CurrentTick % gs.TaxCycleTicks != 0) return;

        var settledGameYear = GameTime.GetGameYear(gs.CurrentTick - 1L);
        var startTick = GameTime.GetStartTickForGameYear(settledGameYear);
        var endTick = GameTime.GetEndTickForGameYear(settledGameYear);

        var yearlyEntriesByCompany = context.Db.LedgerEntries
            .Where(entry => entry.RecordedAtTick >= startTick
                && entry.RecordedAtTick <= endTick
                && entry.Category != LedgerCategory.Tax)
            .ToList()
            .GroupBy(entry => entry.CompanyId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var company in context.CompaniesById.Values)
        {
            var yearlyEntries = yearlyEntriesByCompany.GetValueOrDefault(company.Id, []);
            var taxableIncome = LedgerCalculator.ComputeTaxableIncome(yearlyEntries);
            var incomeTaxDue = GameTime.ComputeEstimatedIncomeTax(taxableIncome, gs.TaxRate);
            var settledTax = Math.Min(context.GetCompanyBankBalance(company.Id), incomeTaxDue);

            if (settledTax <= 0m)
            {
                continue;
            }

            CompanyBankingService.TryDebit(context.GetCompanyBankAccounts(company.Id), settledTax);

            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Category = LedgerCategory.Tax,
                Description = $"Income tax for game year {settledGameYear} ({gs.TaxRate}% rate)",
                Amount = -settledTax,
                RecordedAtTick = endTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        // Settle personal stock-sale tax reserves at year-end.
        // The 15% that was blocked from spending is now permanently paid as tax;
        // the player's spendable cash decreases and the reserve is cleared so
        // the player is never permanently soft-locked out of further purchases.
        var playersWithReserve = context.Db.Players
            .Where(p => p.PersonalTaxReserve > 0m)
            .ToList();
        var personalCashByPlayerId = await PersonalBankAccountService.GetSettlementBalancesByPlayerIdAsync(
            context.Db,
            playersWithReserve.Select(player => player.Id));

        foreach (var player in playersWithReserve)
        {
            var taxPaid = Math.Min(
                PersonalBankAccountService.GetGrossCash(player, personalCashByPlayerId),
                player.PersonalTaxReserve);

            if (taxPaid > 0m)
            {
                await PersonalBankAccountService.DebitTrackedGrossCashAsync(context.Db, player, taxPaid);
            }

            player.PersonalTaxReserve = 0m;
        }
    }
}
