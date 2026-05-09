using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    [Authorize]
    public async Task<List<DividendProposalResult>> GetDividendProposals(
        string stockSymbol,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (!StockSymbolCodec.TryParseCompanyId(stockSymbol, out var companyId))
        {
            return [];
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var controlledCompanyIds = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == userId)
            .Select(company => company.Id)
            .ToListAsync();
        var accountIds = controlledCompanyIds.Append(userId).Distinct().ToList();
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gameState => (long?)gameState.CurrentTick)
            .FirstOrDefaultDeterministicAsync() ?? 0L;

        var proposals = await db.DividendProposals
            .AsNoTracking()
            .Where(proposal => proposal.CompanyId == companyId)
            .OrderByDescending(proposal => proposal.ProposedAtTick)
            .ThenByDescending(proposal => proposal.CreatedAtUtc)
            .Take(200)
            .ToListAsync();
        if (proposals.Count == 0)
        {
            return [];
        }

        var proposalIds = proposals.Select(proposal => proposal.Id).ToList();
        var votes = await db.DividendVotes
            .AsNoTracking()
            .Where(vote => proposalIds.Contains(vote.ProposalId))
            .ToListAsync();

        return proposals
            .Select(proposal =>
            {
                var proposalVotes = votes.Where(vote => vote.ProposalId == proposal.Id).ToList();
                var forVotes = proposalVotes
                    .Where(vote => vote.VoteChoice == DividendVoteChoice.For)
                    .Sum(vote => vote.SharesVoted);
                var againstVotes = proposalVotes
                    .Where(vote => vote.VoteChoice == DividendVoteChoice.Against)
                    .Sum(vote => vote.SharesVoted);
                var myVote = proposalVotes.FirstOrDefault(vote => accountIds.Contains(vote.VoterAccountId));
                return MapDividendProposalResult(proposal, forVotes, againstVotes, currentTick, myVote);
            })
            .ToList();
    }

    [Authorize]
    public async Task<List<DividendVoteResult>> GetMyDividendVotes(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var controlledCompanyIds = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == userId)
            .Select(company => company.Id)
            .ToListAsync();
        var accountIds = controlledCompanyIds.Append(userId).Distinct().ToList();

        var votes = await db.DividendVotes
            .AsNoTracking()
            .Where(vote => accountIds.Contains(vote.VoterAccountId))
            .OrderByDescending(vote => vote.CastAtTick)
            .ThenByDescending(vote => vote.CastAtUtc)
            .Take(500)
            .ToListAsync();

        return votes.Select(vote => new DividendVoteResult
        {
            Id = vote.Id,
            ProposalId = vote.ProposalId,
            VoterAccountId = vote.VoterAccountId,
            VoterAccountType = vote.VoterAccountType,
            SharesVoted = vote.SharesVoted,
            VoteChoice = vote.VoteChoice,
            CastAtTick = vote.CastAtTick,
        }).ToList();
    }

    [Authorize]
    public async Task<int> GetMyOpenDividendProposalCount(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var controlledCompanyIds = await db.Companies
            .AsNoTracking()
            .Where(company => company.PlayerId == userId)
            .Select(company => company.Id)
            .ToListAsync();

        var proposalCompanyIds = await db.Shareholdings
            .AsNoTracking()
            .Where(holding =>
                holding.ShareCount > 0m
                && (holding.OwnerPlayerId == userId
                    || (holding.OwnerCompanyId.HasValue && controlledCompanyIds.Contains(holding.OwnerCompanyId.Value))))
            .Select(holding => holding.CompanyId)
            .Distinct()
            .ToListAsync();
        if (proposalCompanyIds.Count == 0)
        {
            return 0;
        }

        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(gameState => (long?)gameState.CurrentTick)
            .FirstOrDefaultDeterministicAsync() ?? 0L;

        return await db.DividendProposals
            .AsNoTracking()
            .Where(proposal =>
                proposalCompanyIds.Contains(proposal.CompanyId)
                && proposal.Status == DividendProposalStatus.Voting
                && proposal.VotingCloseTick >= currentTick)
            .CountAsync();
    }

    private static DividendProposalResult MapDividendProposalResult(
        DividendProposal proposal,
        decimal forVotes,
        decimal againstVotes,
        long currentTick,
        DividendVote? myVote)
    {
        var ticksRemaining = proposal.Status == DividendProposalStatus.Voting
            ? Math.Max(0, proposal.VotingCloseTick - currentTick)
            : 0;
        var outcome = forVotes > againstVotes ? DividendProposalStatus.Approved : DividendProposalStatus.Rejected;

        return new DividendProposalResult
        {
            Id = proposal.Id,
            CompanyId = proposal.CompanyId,
            StockSymbol = proposal.StockSymbol,
            ProposedByAccountId = proposal.ProposedByAccountId,
            ProposedByAccountType = proposal.ProposedByAccountType,
            DividendPerShare = proposal.DividendPerShare,
            TotalPayout = proposal.TotalPayout,
            Status = proposal.Status,
            Outcome = outcome,
            ProposedAtTick = proposal.ProposedAtTick,
            VotingOpenTick = proposal.VotingOpenTick,
            VotingCloseTick = proposal.VotingCloseTick,
            SettledAtTick = proposal.SettledAtTick,
            TicksRemaining = ticksRemaining,
            ForVotes = forVotes,
            AgainstVotes = againstVotes,
            MyVoteChoice = myVote?.VoteChoice,
            MySharesVoted = myVote?.SharesVoted,
        };
    }
}
