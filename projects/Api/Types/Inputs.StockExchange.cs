using System.ComponentModel.DataAnnotations;

namespace Api.Types;

public sealed class ProposeDividendInput
{
    [Required, MaxLength(40)]
    public string StockSymbol { get; set; } = string.Empty;

    public decimal DividendPerShare { get; set; }
}

public sealed class VoteDividendProposalInput
{
    public Guid ProposalId { get; set; }

    [Required, MaxLength(10)]
    public string Choice { get; set; } = string.Empty;
}
