namespace Api.Data.Entities;

/// <summary>
/// Audit record created when a perishable inventory's quality reaches zero
/// and the entire stock is automatically written off by the QualityDecayPhase.
/// </summary>
public sealed class InventorySpoilageRecord
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public Guid? BuildingUnitId { get; set; }
    public BuildingUnit? BuildingUnit { get; set; }

    public Guid ProductTypeId { get; set; }
    public ProductType ProductType { get; set; } = null!;

    /// <summary>Quantity of product that spoiled.</summary>
    public decimal QuantitySpoiled { get; set; }

    /// <summary>Quality level at the time the stock was written off (always ≤ 0).</summary>
    public decimal QualityAtSpoilage { get; set; }

    /// <summary>Estimated financial loss: QuantitySpoiled × ProductType.BasePrice × SourcingCostTotal factor.</summary>
    public decimal EstimatedLossValue { get; set; }

    public long RecordedAtTick { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
