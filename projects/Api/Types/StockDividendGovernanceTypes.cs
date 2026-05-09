namespace Api.Types;

public sealed class DividendProposalResult
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string StockSymbol { get; set; } = string.Empty;
    public Guid ProposedByAccountId { get; set; }
    public string ProposedByAccountType { get; set; } = string.Empty;
    public decimal DividendPerShare { get; set; }
    public decimal TotalPayout { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public long ProposedAtTick { get; set; }
    public long VotingOpenTick { get; set; }
    public long VotingCloseTick { get; set; }
    public long? SettledAtTick { get; set; }
    public long TicksRemaining { get; set; }
    public decimal ForVotes { get; set; }
    public decimal AgainstVotes { get; set; }
    public string? MyVoteChoice { get; set; }
    public decimal? MySharesVoted { get; set; }
}

public sealed class DividendVoteResult
{
    public Guid Id { get; set; }
    public Guid ProposalId { get; set; }
    public Guid VoterAccountId { get; set; }
    public string VoterAccountType { get; set; } = string.Empty;
    public decimal SharesVoted { get; set; }
    public string VoteChoice { get; set; } = string.Empty;
    public long CastAtTick { get; set; }
}
