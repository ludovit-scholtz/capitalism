using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Configurable advertising unit inside a MEDIA_HOUSE building.
/// Active units spend campaign budget and operating costs each tick and
/// boost the targeted company's brand quality.
/// </summary>
public sealed class MediaHouseUnit
{
    public Guid Id { get; set; }

    public Guid BuildingId { get; set; }
    public Building Building { get; set; } = null!;

    public Guid TargetCompanyId { get; set; }
    public Company TargetCompany { get; set; } = null!;

    [Required, MaxLength(20)]
    public string MediaType { get; set; } = Entities.MediaType.Newspaper;

    public decimal CampaignBudgetPerTick { get; set; }
    public decimal BrandQualityBoostPerTick { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal LaborCostPerTick { get; set; }
    public decimal EnergyCostPerTick { get; set; }
}
