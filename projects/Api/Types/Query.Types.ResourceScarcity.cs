namespace Api.Types;

public sealed class LandResourceStatus
{
    public Guid LandId { get; set; }
    public Guid CityId { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public string? ResourceName { get; set; }
    public bool IsDepletable { get; set; }
    public bool IsDepleted { get; set; }
    public decimal? QuantityRemaining { get; set; }
    public decimal? InitialQuantity { get; set; }
    public decimal? QualityIndex { get; set; }
    public decimal EfficiencyFactor { get; set; }
    public decimal? EstimatedTicksRemaining { get; set; }
}

public sealed class CityResourceMapEntry
{
    public Guid LandId { get; set; }
    public Guid CityId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public string? ResourceName { get; set; }
    public bool IsDepleted { get; set; }
    public decimal? QuantityRemaining { get; set; }
    public decimal? InitialQuantity { get; set; }
    public decimal? QualityIndex { get; set; }
    public decimal EfficiencyFactor { get; set; }
    public decimal? EstimatedTicksRemaining { get; set; }
}
