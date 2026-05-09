using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

public sealed class DividendProposal
{
    public Guid Id { get; set; }

    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;

    [Required, MaxLength(40)]
    public string StockSymbol { get; set; } = string.Empty;

    public Guid ProposedByAccountId { get; set; }

    [Required, MaxLength(20)]
    public string ProposedByAccountType { get; set; } = "PERSON";

    public decimal DividendPerShare { get; set; }

    public decimal TotalPayout { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = DividendProposalStatus.Voting;

    public long ProposedAtTick { get; set; }
    public long VotingOpenTick { get; set; }
    public long VotingCloseTick { get; set; }
    public long? SettledAtTick { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAtUtc { get; set; }

    public ICollection<DividendVote> Votes { get; set; } = new List<DividendVote>();
}

public static class DividendProposalStatus
{
    public const string Pending = "PENDING";
    public const string Voting = "VOTING";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Settled = "SETTLED";
    public const string Cancelled = "CANCELLED";
}
