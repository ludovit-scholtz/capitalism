using Api.Data;
using Api.Engine.Campaign;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Revenue, running costs and loan payments of the signed-in player's own companies since <paramref name="sinceTick"/> (#602).
    /// Auth decision: [Authorize], always the caller's own ledger (no company or player argument, so there is no foreign-owner case).
    /// The tick is only a window start supplied by the client; it is clamped to [now - 30 days, now] and cannot reach other
    /// players' data. Read-only.
    /// </summary>
    [Authorize]
    public async Task<AwaySummaryResult> GetAwaySummary(
        long sinceTick,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync(ct);
        var fromTick = AwaySummaryCalculator.ClampSinceTick(sinceTick, currentTick);

        var entries = await db.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.Company.PlayerId == userId
                && entry.RecordedAtTick > fromTick
                && entry.RecordedAtTick <= currentTick
                && AwaySummaryCalculator.RelevantCategories.Contains(entry.Category))
            .Select(entry => new { entry.Category, entry.Amount, CurrencyCode = entry.BankAccount != null ? entry.BankAccount.CurrencyCode : "USD" })
            .ToListAsync(ct);

        var eurRates = await FxRateHelper.BuildEurRatesLookupAsync(db, entries.Select(entry => entry.CurrencyCode).Append("USD"));
        var totals = AwaySummaryCalculator.Summarize(
            entries.Select(entry => new AwayLedgerRow(entry.Category, FxRateHelper.ConvertToUsd(entry.Amount, entry.CurrencyCode, eurRates))));

        return new AwaySummaryResult
        {
            SinceTick = fromTick,
            CurrentTick = currentTick,
            TicksAway = currentTick - fromTick,
            RevenueUsd = decimal.Round(totals.RevenueUsd, 2),
            CostsUsd = decimal.Round(totals.CostsUsd, 2),
            NetUsd = decimal.Round(totals.NetUsd, 2),
            LoanPaymentsCount = totals.LoanPaymentsCount,
        };
    }
}
