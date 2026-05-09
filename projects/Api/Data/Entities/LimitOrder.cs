using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Native stock-exchange limit order persisted for tick-based matching.
/// </summary>
public sealed class LimitOrder
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [MaxLength(40)]
    public string StockSymbol { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Side { get; set; } = LimitOrderSide.Buy;

    public decimal LimitPrice { get; set; }

    public int Quantity { get; set; }

    public int FilledQuantity { get; set; }

    [MaxLength(20)]
    public string Status { get; set; } = LimitOrderStatus.Open;

    public Guid? OwnerPlayerId { get; set; }
    public Player? OwnerPlayer { get; set; }

    public Guid? OwnerCompanyId { get; set; }
    public Company? OwnerCompany { get; set; }

    /// <summary>
    /// Settlement account to debit/credit for fills and reserve releases.
    /// </summary>
    public Guid SettlementBankAccountId { get; set; }
    public BankAccount SettlementBankAccount { get; set; } = null!;

    /// <summary>
    /// Remaining reserved cash for BUY orders.
    /// </summary>
    public decimal ReservedCashRemaining { get; set; }

    public long CreatedAtTick { get; set; }
    public long UpdatedAtTick { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class LimitOrderSide
{
    public const string Buy = "BUY";
    public const string Sell = "SELL";

    public static string Normalize(string? side)
        => side?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool IsValid(string? side)
        => string.Equals(side, Buy, StringComparison.OrdinalIgnoreCase)
            || string.Equals(side, Sell, StringComparison.OrdinalIgnoreCase);
}

public static class LimitOrderStatus
{
    public const string Open = "OPEN";
    public const string PartiallyFilled = "PARTIALLY_FILLED";
    public const string Filled = "FILLED";
    public const string Cancelled = "CANCELLED";
}
