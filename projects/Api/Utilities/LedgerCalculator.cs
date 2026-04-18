using Api.Data.Entities;

namespace Api.Utilities;

/// <summary>
/// Centralizes yearly ledger and income-tax calculations.
/// </summary>
public static class LedgerCalculator
{
    private static readonly HashSet<string> DeductibleCategories =
    [
        LedgerCategory.PurchasingCost,
        LedgerCategory.LaborCost,
        LedgerCategory.EnergyCost,
        LedgerCategory.Marketing,
        LedgerCategory.ShippingCost,
        LedgerCategory.Other,
        LedgerCategory.UnitUpgrade,
        // Banking operating costs are deductible (interest paid to depositors, loan interest expense)
        LedgerCategory.DepositInterestPaid,
        LedgerCategory.LoanInterestExpense,
    ];

    public static decimal GetTotalRevenue(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.Revenue)
            .Sum(entry => entry.Amount);
    }

    public static decimal GetTotalPurchasingCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.PurchasingCost)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalMarketingCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.Marketing)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalLaborCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.LaborCost)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalEnergyCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.EnergyCost)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalShippingCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.ShippingCost)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalTaxPaid(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.Tax)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalOtherCosts(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.Other && entry.Amount < 0)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalPropertyPurchases(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.PropertyPurchase)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalStockPurchaseCashOut(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.StockPurchase && entry.Amount < 0m)
            .Sum(entry => entry.Amount));
    }

    public static decimal GetTotalStockSaleCashIn(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.StockSale && entry.Amount > 0m)
            .Sum(entry => entry.Amount);
    }

    // ── Banking income/expense helpers ────────────────────────────────────────

    /// <summary>Deposit interest earned by this company as a depositor (income).</summary>
    public static decimal GetTotalDepositInterestReceived(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.DepositInterestReceived)
            .Sum(entry => entry.Amount);
    }

    /// <summary>Deposit interest paid out by this company's bank to depositors (expense).</summary>
    /// <summary>
    /// Total deposit interest paid by this bank to its depositors, as an absolute (positive) expense amount.
    /// DepositInterestPaid ledger entries are stored as negative amounts (cash outflow for the bank);
    /// <see cref="Math.Abs"/> converts them to a positive expense figure for reporting.
    /// </summary>
    public static decimal GetTotalDepositInterestPaid(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.DepositInterestPaid)
            .Sum(entry => entry.Amount));
    }

    /// <summary>Loan interest income earned by this company as a bank/lender.</summary>
    public static decimal GetTotalLoanInterestIncome(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.LoanInterestIncome)
            .Sum(entry => entry.Amount);
    }

    /// <summary>Loan interest expense paid by this company as a borrower.</summary>
    public static decimal GetTotalLoanInterestExpense(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.LoanInterestExpense)
            .Sum(entry => entry.Amount));
    }

    /// <summary>Net cash out for deposits made (positive = cash left company).</summary>
    public static decimal GetTotalDepositsMade(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.DepositMade)
            .Sum(entry => entry.Amount));
    }

    /// <summary>Net cash in from deposit withdrawals (positive = cash returned).</summary>
    public static decimal GetTotalDepositsWithdrawn(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.DepositWithdrawn && entry.Amount > 0m)
            .Sum(entry => entry.Amount);
    }

    /// <summary>Total loan originations: principal received by borrower (positive = cash in).</summary>
    public static decimal GetTotalLoanOriginations(IEnumerable<LedgerEntry> entries)
    {
        return entries
            .Where(entry => entry.Category == LedgerCategory.LoanOrigination && entry.Amount > 0m)
            .Sum(entry => entry.Amount);
    }

    /// <summary>Total loan repayment principal paid out by borrower (positive value of outflow).</summary>
    public static decimal GetTotalLoanRepaymentPrincipal(IEnumerable<LedgerEntry> entries)
    {
        return Math.Abs(entries
            .Where(entry => entry.Category == LedgerCategory.LoanRepaymentPrincipal && entry.Amount < 0m)
            .Sum(entry => entry.Amount));
    }

    public static decimal ComputeTaxableIncome(IEnumerable<LedgerEntry> entries)
    {
        var ledgerEntries = entries.ToList();
        var revenue = GetTotalRevenue(ledgerEntries);
        // Banking interest income is also taxable
        var bankingIncome = GetTotalDepositInterestReceived(ledgerEntries) + GetTotalLoanInterestIncome(ledgerEntries);
        var deductibleCosts = Math.Abs(ledgerEntries
            .Where(entry => DeductibleCategories.Contains(entry.Category) && entry.Amount < 0m)
            .Sum(entry => entry.Amount));

        return Math.Max(revenue + bankingIncome - deductibleCosts, 0m);
    }
}
