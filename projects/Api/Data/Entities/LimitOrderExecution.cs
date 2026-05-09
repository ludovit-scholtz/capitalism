namespace Api.Data.Entities;

/// <summary>
/// Audit-trail record for a matched stock limit-order trade.
/// </summary>
public sealed class LimitOrderExecution
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public string StockSymbol { get; set; } = string.Empty;

    public Guid BuyOrderId { get; set; }
    public LimitOrder BuyOrder { get; set; } = null!;

    public Guid SellOrderId { get; set; }
    public LimitOrder SellOrder { get; set; } = null!;

    public decimal Price { get; set; }
    public int Quantity { get; set; }
    public long ExecutedAtTick { get; set; }
    public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;
}
