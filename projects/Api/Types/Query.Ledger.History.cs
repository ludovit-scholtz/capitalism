using Api.Data.Entities;
using Api.Utilities;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Pre-aggregated ledger rows of one game year: the sum of all amounts of one category and sign
    /// (positive or non-positive) plus the first and last tick among those rows.
    /// </summary>
    private readonly record struct LedgerHistoryAggregate(string Category, decimal Total, long FirstTick, long LastTick);

    /// <summary>
    /// Builds a ledger history year summary from database-side aggregates, so a company's whole ledger
    /// never has to be loaded into memory. A "positive" aggregate has Total &gt; 0 and a "negative" one
    /// Total &lt; 0, which matches the previous row-by-row Amount &gt; 0 / &lt; 0 filters.
    /// </summary>
    private static CompanyLedgerHistoryYear BuildCompanyLedgerHistoryYear(
        int gameYear,
        int currentGameYear,
        decimal taxRate,
        IReadOnlyCollection<LedgerHistoryAggregate> aggregates)
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
