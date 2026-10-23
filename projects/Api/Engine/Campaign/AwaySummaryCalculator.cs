using Api.Data.Entities;

namespace Api.Engine.Campaign;

/// <summary>One ledger entry reduced to what the away summary needs, already converted to USD.</summary>
public sealed record AwayLedgerRow(string Category, decimal AmountUsd);

public sealed record AwaySummaryTotals(decimal RevenueUsd, decimal CostsUsd, decimal NetUsd, int LoanPaymentsCount);

/// <summary>
/// Pure rules for the "while you were away" summary (#602): which ledger categories count as revenue and as running costs.
/// Investments (property, construction), financing inflows, transfers and dividends are deliberately left out so the net figure
/// answers "how did my business do", not "how much cash moved".
/// </summary>
public static class AwaySummaryCalculator
{
    /// <summary>The summary looks back at most 30 in-game days (1 tick = 1 hour), which bounds the ledger scan.</summary>
    public const int MaxWindowTicks = 30 * 24;

    private static readonly string[] RevenueCategories =
    [
        LedgerCategory.Revenue,
        LedgerCategory.RentIncome,
        LedgerCategory.MediaHouseIncome,
        LedgerCategory.GridSurplusIncome,
    ];

    private static readonly string[] CostCategories =
    [
        LedgerCategory.PurchasingCost,
        LedgerCategory.LaborCost,
        LedgerCategory.EnergyCost,
        LedgerCategory.ShippingCost,
        LedgerCategory.Marketing,
        LedgerCategory.Tax,
        LedgerCategory.PropertyMaintenance,
        LedgerCategory.FuelCost,
        LedgerCategory.MediaHouseContent,
        LedgerCategory.MediaHouseExpense,
        LedgerCategory.LoanInterestExpense,
        LedgerCategory.LoanPenalty,
        LedgerCategory.GridFine,
    ];

    /// <summary>Every category the summary reads, so the query can filter in the database.</summary>
    public static readonly string[] RelevantCategories =
        [.. RevenueCategories, .. CostCategories, LedgerCategory.LoanRepaymentPrincipal];

    public static AwaySummaryTotals Summarize(IEnumerable<AwayLedgerRow> rows)
    {
        var revenue = 0m;
        var costs = 0m;
        var loanPayments = 0;
        foreach (var row in rows)
        {
            if (RevenueCategories.Contains(row.Category)) revenue += row.AmountUsd;
            else if (CostCategories.Contains(row.Category)) costs += row.AmountUsd;
            else if (row.Category == LedgerCategory.LoanRepaymentPrincipal) loanPayments++;
        }

        return new AwaySummaryTotals(revenue, costs, revenue + costs, loanPayments);
    }

    /// <summary>The client's remembered tick may be stale, in the future, or ancient; keep it inside [now - window, now].</summary>
    public static long ClampSinceTick(long sinceTick, long currentTick)
        => Math.Clamp(sinceTick, Math.Max(0, currentTick - MaxWindowTicks), Math.Max(0, currentTick));
}
