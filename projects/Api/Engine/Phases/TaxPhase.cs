using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Applies annual income tax on the configured tax-cycle boundary.
/// </summary>
public sealed class TaxPhase : ITickPhase
{
    public string Name => "Tax";
    public int Order => 1000;

    public Task ProcessAsync(TickContext context)
    {
        var gs = context.GameState;
        if (gs.TaxCycleTicks <= 0) return Task.CompletedTask;
        if (gs.CurrentTick % gs.TaxCycleTicks != 0) return Task.CompletedTask;

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
            var settledTax = Math.Min(company.Cash, incomeTaxDue);

            if (settledTax <= 0m)
            {
                continue;
            }

            company.Cash -= settledTax;

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

        return Task.CompletedTask;
    }
}
