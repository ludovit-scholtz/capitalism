using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

public sealed class DividendVote
{
    public Guid Id { get; set; }

    public Guid ProposalId { get; set; }
    public DividendProposal Proposal { get; set; } = null!;

    public Guid VoterAccountId { get; set; }

    [Required, MaxLength(20)]
    public string VoterAccountType { get; set; } = "PERSON";

    public decimal SharesVoted { get; set; }

    [Required, MaxLength(10)]
    public string VoteChoice { get; set; } = DividendVoteChoice.For;

    public long CastAtTick { get; set; }
    public DateTime CastAtUtc { get; set; } = DateTime.UtcNow;
}

public static class DividendVoteChoice
{
    public const string For = "FOR";
    public const string Against = "AGAINST";
}
