namespace Api.Types;

public sealed class SupplyContractCard
{
    public Guid Id { get; set; }
    public Guid SellerCompanyId { get; set; }
    public string SellerCompanyName { get; set; } = string.Empty;
    public Guid BuyerCompanyId { get; set; }
    public string BuyerCompanyName { get; set; } = string.Empty;
    public Guid SellerBuildingUnitId { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public string? ResourceTypeName { get; set; }
    public Guid? ProductTypeId { get; set; }
    public string? ProductTypeName { get; set; }
    public decimal QuantityPerTick { get; set; }
    public decimal PricePerUnit { get; set; }
    public int DurationTicks { get; set; }
    public int RemainingTicks { get; set; }
    public long StartTick { get; set; }
    public decimal PenaltyRatePercent { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public string Status { get; set; } = string.Empty;
    public long CreatedAtTick { get; set; }
    public decimal TotalDeliveredQuantity { get; set; }
    public decimal TotalUndeliveredQuantity { get; set; }
    public decimal TotalPenaltyAmount { get; set; }
    public int PenaltyCount { get; set; }
}

public sealed class ProposeSupplyContractInput
{
    public Guid SellerCompanyId { get; set; }
    public Guid BuyerCompanyId { get; set; }
    public Guid SellerBuildingUnitId { get; set; }
    public Guid? ResourceTypeId { get; set; }
    public Guid? ProductTypeId { get; set; }
    public decimal QuantityPerTick { get; set; }
    public decimal PricePerUnit { get; set; }
    public int DurationTicks { get; set; }
    public decimal PenaltyRatePercent { get; set; }
}

public sealed class CounterProposeSupplyContractInput
{
    public decimal? QuantityPerTick { get; set; }
    public decimal? PricePerUnit { get; set; }
    public int? DurationTicks { get; set; }
    public decimal? PenaltyRatePercent { get; set; }
}

public sealed class SupplyContractActionResult
{
    public SupplyContractCard Contract { get; set; } = new();
    public bool Success { get; set; }
    public string? Message { get; set; }
}
