using System.ComponentModel.DataAnnotations;

namespace Api.Types;

public sealed class ProposeDividendInput
{
    [MaxLength(40)]
    public string? StockSymbol { get; set; }

    public decimal? DividendPerShare { get; set; }

    public Guid? CompanyId { get; set; }

    public decimal? DividendPercent { get; set; }
}

public sealed class VoteDividendProposalInput
{
    public Guid ProposalId { get; set; }

    [Required, MaxLength(10)]
    public string Choice { get; set; } = string.Empty;
}
