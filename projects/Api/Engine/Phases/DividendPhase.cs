using Api.Data.Entities;
using Api.Utilities;

namespace Api.Engine.Phases;

/// <summary>
/// Pays configured annual dividends after tax has been settled on the year boundary.
/// Public-float dividends leave the company as external distributions but do not create a local recipient record.
/// </summary>
public sealed class DividendPhase : ITickPhase
{
    public string Name => "Dividend";
    public int Order => 1010;

    public Task ProcessAsync(TickContext context)
    {
        var gs = context.GameState;
        if (gs.TaxCycleTicks <= 0 || gs.CurrentTick % gs.TaxCycleTicks != 0)
        {
            return Task.CompletedTask;
        }

        var settledGameYear = GameTime.GetGameYear(gs.CurrentTick - 1L);
        var startTick = GameTime.GetStartTickForGameYear(settledGameYear);
        var endTick = GameTime.GetEndTickForGameYear(settledGameYear);

        var yearlyEntriesByCompany = context.Db.LedgerEntries
            .Where(entry => entry.RecordedAtTick >= startTick
                && entry.RecordedAtTick <= endTick
                && entry.Category != LedgerCategory.Dividend)
            .ToList()
            .GroupBy(entry => entry.CompanyId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var shareholdings = context.Db.Shareholdings.ToList();
        var playersById = context.Db.Players.ToDictionary(player => player.Id);
        var companiesById = context.CompaniesById;

        foreach (var company in companiesById.Values)
        {
            if (company.TotalSharesIssued <= 0m || company.DividendPayoutRatio <= 0m)
            {
                continue;
            }

            var yearlyEntries = yearlyEntriesByCompany.GetValueOrDefault(company.Id, []);
            var taxableIncome = LedgerCalculator.ComputeTaxableIncome(yearlyEntries);
            if (taxableIncome <= 0m)
            {
                continue;
            }

            var estimatedTax = GameTime.ComputeEstimatedIncomeTax(taxableIncome, gs.TaxRate);
            var postTaxIncome = Math.Max(0m, taxableIncome - estimatedTax);
            if (postTaxIncome <= 0m)
            {
                continue;
            }

            var targetPool = decimal.Round(postTaxIncome * company.DividendPayoutRatio, 4, MidpointRounding.AwayFromZero);
            var dividendPool = Math.Min(company.Cash, targetPool);
            if (dividendPool <= 0m)
            {
                continue;
            }

            var holdings = shareholdings
                .Where(holding => holding.CompanyId == company.Id && holding.ShareCount > 0m)
                .ToList();

            if (holdings.Count == 0)
            {
                continue;
            }

            var amountPerShare = decimal.Round(dividendPool / company.TotalSharesIssued, 4, MidpointRounding.AwayFromZero);
            if (amountPerShare <= 0m)
            {
                continue;
            }

            decimal distributedAmount = 0m;

            foreach (var holding in holdings)
            {
                var payout = decimal.Round(amountPerShare * holding.ShareCount, 4, MidpointRounding.AwayFromZero);
                if (payout <= 0m)
                {
                    continue;
                }

                if (holding.OwnerPlayerId is Guid ownerPlayerId && playersById.TryGetValue(ownerPlayerId, out var player))
                {
                    player.PersonalCash += payout;
                }
                else if (holding.OwnerCompanyId is Guid ownerCompanyId && companiesById.TryGetValue(ownerCompanyId, out var ownerCompany))
                {
                    ownerCompany.Cash += payout;
                }
                else
                {
                    continue;
                }

                distributedAmount += payout;

                context.Db.DividendPayments.Add(new DividendPayment
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    RecipientPlayerId = holding.OwnerPlayerId,
                    RecipientCompanyId = holding.OwnerCompanyId,
                    ShareCount = holding.ShareCount,
                    AmountPerShare = amountPerShare,
                    TotalAmount = payout,
                    GameYear = settledGameYear,
                    RecordedAtTick = endTick,
                    RecordedAtUtc = DateTime.UtcNow,
                    Description = $"Dividend for game year {settledGameYear}"
                });
            }

            var publicFloat = SharePriceCalculator.ComputePublicFloat(company, holdings);
            var publicFloatPayout = publicFloat > 0m
                ? Math.Max(0m, decimal.Round(dividendPool - distributedAmount, 4, MidpointRounding.AwayFromZero))
                : 0m;

            var totalPaid = distributedAmount + publicFloatPayout;
            if (totalPaid <= 0m)
            {
                continue;
            }

            company.Cash -= totalPaid;

            context.Db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Category = LedgerCategory.Dividend,
                Description = $"Dividend payout for game year {settledGameYear}",
                Amount = -totalPaid,
                RecordedAtTick = endTick,
                RecordedAtUtc = DateTime.UtcNow,
            });
        }

        return Task.CompletedTask;
    }
}