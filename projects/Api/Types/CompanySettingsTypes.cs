namespace Api.Types;

public sealed class CompanySettingsResult
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public decimal Cash { get; set; }
    public decimal TotalSharesIssued { get; set; }
    public decimal DividendPayoutRatio { get; set; }
    public long FoundedAtTick { get; set; }
    public decimal AdministrationOverheadRate { get; set; }
    /// <summary>0–1 fraction representing how much company age contributes to overhead (reaches 1 at 2 years).</summary>
    public decimal AgeFactor { get; set; }
    /// <summary>0–1 fraction representing how much company scale (assets) contributes to overhead.</summary>
    public decimal AssetFactor { get; set; }
    public decimal AssetValue { get; set; }
    public string CurrencyCode { get; set; } = "EUR";
    public List<CompanyCitySalarySettingResult> CitySalarySettings { get; set; } = [];
    public CompanyDividendPolicyProposalResult? PendingDividendProposal { get; set; }
}

public sealed class CompanyCitySalarySettingResult
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    /// <summary>ISO 4217 currency code of this city (e.g. "EUR", "CZK", "USD"). Wages are denominated in this currency.</summary>
    public string CurrencyCode { get; set; } = "EUR";
    public decimal BaseSalaryPerManhour { get; set; }
    public decimal SalaryMultiplier { get; set; }
    public decimal EffectiveSalaryPerManhour { get; set; }
}

public sealed class CompanyDividendPolicyProposalResult
{
    public Guid Id { get; set; }
    public decimal DividendPercent { get; set; }
    public long VotingCloseTick { get; set; }
    public long TicksRemaining { get; set; }
    public decimal ForVotes { get; set; }
    public decimal AgainstVotes { get; set; }
    public string? MyVoteChoice { get; set; }
}
