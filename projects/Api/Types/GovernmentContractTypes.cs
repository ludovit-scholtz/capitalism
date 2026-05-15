namespace Api.Types;

public sealed class GovernmentContractCard
{
    public Guid Id { get; set; }
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "EUR";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProductTypeId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
    public decimal MinimumQuality { get; set; }
    public decimal BudgetCap { get; set; }
    public long DeadlineTick { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid? WinnerCompanyId { get; set; }
    public string? WinnerCompanyName { get; set; }
    public long CreatedAtTick { get; set; }
    public int BidCount { get; set; }
    public decimal? AwardedBidPricePerUnit { get; set; }
    public decimal? FulfilledQuantity { get; set; }
    public decimal? FulfillmentPercent { get; set; }
}

public sealed class GovernmentContractDetailResult
{
    public GovernmentContractCard Contract { get; set; } = new();
    public int CompetingBidCount { get; set; }
    public GovernmentContractEligibilityResult? Eligibility { get; set; }
}

public sealed class GovernmentContractEligibilityResult
{
    public bool IsEligible { get; set; }
    public string? ReasonCode { get; set; }
    public string? ReasonMessage { get; set; }
    public decimal CurrentQualityLevel { get; set; }
}

public sealed class ContractBidResult
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public decimal BidPricePerUnit { get; set; }
    public long EstimatedDeliveryTick { get; set; }
    public long SubmittedAtTick { get; set; }
    public string ContractStatus { get; set; } = string.Empty;
}

public sealed class ContractFulfillmentResult
{
    public Guid ContractId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal QuantityDelivered { get; set; }
    public decimal QuantityRequired { get; set; }
    public decimal FulfillmentPercent { get; set; }
    public decimal? SettledRevenue { get; set; }
    public bool LatePenaltyApplied { get; set; }
}

public sealed class SubmitContractBidInput
{
    public Guid ContractId { get; set; }
    public Guid CompanyId { get; set; }
    public decimal BidPricePerUnit { get; set; }
    public long EstimatedDeliveryTick { get; set; }
}

public sealed class FulfillContractShipmentInput
{
    public Guid ContractId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class GenerateGovernmentContractsInput
{
    public Guid? CityId { get; set; }
    public int CountPerCity { get; set; } = 1;
}
