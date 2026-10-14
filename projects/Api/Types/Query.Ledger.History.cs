using Api.Data.Entities;
using Api.Engine;
using Api.Utilities;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>One database-side group: a game year (0-based index), category and sign of a company's ledger rows.</summary>
    internal sealed record LedgerYearAggregateRow(long YearIndex, string Category, decimal Total, long FirstTick, long LastTick);

    /// <summary>
    /// Groups a company's ledger rows per game year / category / sign (positive vs non-positive amounts) in the
    /// database. Kept as one named query so a test can prove it translates for PostgreSQL (the unit tests run on
    /// the InMemory provider, which would not notice an untranslatable expression).
    /// </summary>
    internal static IQueryable<LedgerYearAggregateRow> HistoryAggregateQuery(IQueryable<LedgerEntry> ledgerEntries, Guid companyId) =>
        ledgerEntries
            .Where(e => e.CompanyId == companyId)
            .GroupBy(e => new
            {
                YearIndex = (e.RecordedAtTick < 0L ? 0L : e.RecordedAtTick) / GameConstants.TicksPerYear,
                e.Category,
                IsPositive = e.Amount > 0m,
            })
            .Select(group => new LedgerYearAggregateRow(
                group.Key.YearIndex,
                group.Key.Category,
                group.Sum(e => e.Amount),
                group.Min(e => e.RecordedAtTick),
                group.Max(e => e.RecordedAtTick)));

    /// <summary>
    /// Builds a ledger history year summary from database-side aggregates, so a company's whole ledger
    /// never has to be loaded into memory. A "positive" aggregate has Total &gt; 0 and a "negative" one
    /// Total &lt; 0, which matches the previous row-by-row Amount &gt; 0 / &lt; 0 filters.
    /// </summary>
    private static CompanyLedgerHistoryYear BuildCompanyLedgerHistoryYear(
        int gameYear,
        int currentGameYear,
        decimal taxRate,
        IReadOnlyCollection<LedgerYearAggregateRow> aggregates)
    {
        var totalRevenue = aggregates.Where(e => (e.Category == LedgerCategory.Revenue || e.Category == LedgerCategory.GovernmentContractRevenue) && e.Total > 0).Sum(e => e.Total);
        var totalMediaHouseIncome = aggregates.Where(e => e.Category == LedgerCategory.MediaHouseIncome && e.Total > 0).Sum(e => e.Total);
        var totalRentIncome = aggregates.Where(e => e.Category == LedgerCategory.RentIncome && e.Total > 0).Sum(e => e.Total);
        var totalPropertyMaintenance = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.PropertyMaintenance && e.Total < 0).Sum(e => e.Total));
        var totalPurchasingCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.PurchasingCost && e.Total < 0).Sum(e => e.Total));
        var totalShippingCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.ShippingCost && e.Total < 0).Sum(e => e.Total));
        var totalLaborCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.LaborCost && e.Total < 0).Sum(e => e.Total));
        var totalEnergyCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.EnergyCost && e.Total < 0).Sum(e => e.Total));
        var totalMarketingCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.Marketing && e.Total < 0).Sum(e => e.Total));
        var totalTaxPaid = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.Tax && e.Total < 0).Sum(e => e.Total));
        var totalOtherCosts = Math.Abs(aggregates.Where(e => e.Category == LedgerCategory.Other && e.Total < 0).Sum(e => e.Total));
        // Taxable income = revenue + media house income + rent income minus all deductible operating costs (excluding tax itself).
        var taxableIncome = Math.Max(totalRevenue + totalMediaHouseIncome + totalRentIncome - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts - totalMarketingCosts - totalPropertyMaintenance, 0m);
        var estimatedIncomeTax = gameYear == currentGameYear
            ? GameTime.ComputeEstimatedIncomeTax(taxableIncome, taxRate)
            : totalTaxPaid;

        return new CompanyLedgerHistoryYear
        {
            GameYear = gameYear,
            IsCurrentGameYear = gameYear == currentGameYear,
            TotalRevenue = totalRevenue,
            TotalLaborCosts = totalLaborCosts,
            TotalEnergyCosts = totalEnergyCosts,
            NetIncome = totalRevenue + totalMediaHouseIncome + totalRentIncome - totalPurchasingCosts - totalShippingCosts - totalLaborCosts - totalEnergyCosts - totalMarketingCosts - totalPropertyMaintenance - totalTaxPaid - totalOtherCosts,
            TotalTaxPaid = totalTaxPaid,
            TaxableIncome = taxableIncome,
            EstimatedIncomeTax = estimatedIncomeTax,
            FirstRecordedTick = aggregates.Count > 0 ? aggregates.Min(e => e.FirstTick) : 0,
            LastRecordedTick = aggregates.Count > 0 ? aggregates.Max(e => e.LastTick) : 0,
        };
    }

}
