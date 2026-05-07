namespace Api.Data.Entities;

/// <summary>
/// Tick-level record of Media House brand-quality boosts.
/// Used for analytics history and campaign accountability.
/// </summary>
public sealed class BrandQualityRecord
{
    public Guid Id { get; set; }

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public Guid MediaHouseUnitId { get; set; }
    public MediaHouseUnit MediaHouseUnit { get; set; } = null!;

    public Guid TargetCompanyId { get; set; }
    public Company TargetCompany { get; set; } = null!;

    public long RecordedAtTick { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal BoostApplied { get; set; }
    public decimal CampaignBudgetSpent { get; set; }
    public decimal LaborCostSpent { get; set; }
    public decimal EnergyCostSpent { get; set; }
}
