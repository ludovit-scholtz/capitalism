using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<DividendProposalResult> VoteDividend(
        VoteDividendInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var currentTick = await GetCurrentTickAsync(db);
        var proposal = await db.DividendProposals
            .FirstOrDefaultAsync(candidate =>
                candidate.CompanyId == input.CompanyId
                && candidate.Status == DividendProposalStatus.Voting
                && candidate.TotalPayout <= 0m
                && candidate.VotingCloseTick >= currentTick)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("No pending dividend proposal found.")
                    .SetCode("PROPOSAL_NOT_FOUND")
                    .Build());
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var alreadyVoted = await db.DividendVotes.AnyAsync(vote =>
            vote.ProposalId == proposal.Id
            && vote.VoterAccountId == userId
            && vote.VoterAccountType == "PERSON");
        if (alreadyVoted)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You have already voted on this proposal.")
                    .SetCode("ALREADY_VOTED")
                    .Build());
        }

        var sharesVoted = await db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.CompanyId == proposal.CompanyId && holding.OwnerPlayerId == userId && holding.ShareCount > 0m)
            .SumAsync(holding => holding.ShareCount);
        if (sharesVoted <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only shareholders can vote on this proposal.")
                    .SetCode("NOT_SHAREHOLDER")
                    .Build());
        }

        var voteChoice = input.Vote == DividendVoteChoiceInput.Approve
            ? DividendVoteChoice.For
            : DividendVoteChoice.Against;
        var vote = new DividendVote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterAccountId = userId,
            VoterAccountType = "PERSON",
            SharesVoted = decimal.Round(sharesVoted, 4, MidpointRounding.AwayFromZero),
            VoteChoice = voteChoice,
            CastAtTick = currentTick,
        };
        db.DividendVotes.Add(vote);
        await db.SaveChangesAsync();

        await ResolveDividendPolicyProposalAsync(db, proposal, currentTick);

        var proposalVotes = await db.DividendVotes
            .AsNoTracking()
            .Where(candidate => candidate.ProposalId == proposal.Id)
            .ToListAsync();
        var forVotes = proposalVotes
            .Where(candidate => candidate.VoteChoice == DividendVoteChoice.For)
            .Sum(candidate => candidate.SharesVoted);
        var againstVotes = proposalVotes
            .Where(candidate => candidate.VoteChoice == DividendVoteChoice.Against)
            .Sum(candidate => candidate.SharesVoted);
        var freshProposal = await db.DividendProposals
            .AsNoTracking()
            .FirstAsync(candidate => candidate.Id == proposal.Id);
        return MapDividendProposalResult(freshProposal, forVotes, againstVotes, currentTick, vote);
    }

    private static string NormalizeVoteChoice(string rawChoice)
    {
        var normalized = (rawChoice ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            DividendVoteChoice.For => DividendVoteChoice.For,
            DividendVoteChoice.Against => DividendVoteChoice.Against,
            _ => throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Vote choice must be FOR or AGAINST.")
                    .SetCode("INVALID_DIVIDEND_VOTE_CHOICE")
                    .Build()),
        };
    }

    private async Task ResolveDividendPolicyProposalAsync(AppDbContext db, DividendProposal proposal, long currentTick)
    {
        if (proposal.TotalPayout > 0m || proposal.Status != DividendProposalStatus.Voting)
        {
            return;
        }

        var totalSharesIssued = await db.Companies
            .AsNoTracking()
            .Where(company => company.Id == proposal.CompanyId)
            .Select(company => company.TotalSharesIssued)
            .FirstOrDefaultDeterministicAsync();
        if (totalSharesIssued <= 0m)
        {
            return;
        }

        var votes = await db.DividendVotes
            .AsNoTracking()
            .Where(vote => vote.ProposalId == proposal.Id)
            .ToListAsync();
        var forVotes = votes
            .Where(vote => vote.VoteChoice == DividendVoteChoice.For)
            .Sum(vote => vote.SharesVoted);
        var againstVotes = votes
            .Where(vote => vote.VoteChoice == DividendVoteChoice.Against)
            .Sum(vote => vote.SharesVoted);
        var threshold = totalSharesIssued / 2m;

        // Majority threshold is strictly >50% of total issued shares.
        // Exactly 50% is not enough to resolve and remains pending until close tick.
        if (forVotes <= threshold && againstVotes <= threshold)
        {
            return;
        }

        var trackedProposal = await db.DividendProposals.FirstAsync(candidate => candidate.Id == proposal.Id);
        trackedProposal.SettledAtTick = currentTick;
        trackedProposal.SettledAtUtc = DateTime.UtcNow;

        if (forVotes > threshold)
        {
            var trackedCompany = await db.Companies.FirstAsync(candidate => candidate.Id == proposal.CompanyId);
            trackedCompany.DividendPayoutRatio = decimal.Round(
                Math.Clamp(trackedProposal.DividendPerShare, 0m, 1m),
                4,
                MidpointRounding.AwayFromZero);
            trackedProposal.Status = DividendProposalStatus.Settled;
        }
        else
        {
            trackedProposal.Status = DividendProposalStatus.Rejected;
        }

        await db.SaveChangesAsync();
    }

    private static async Task NotifyShareholdersAboutDividendProposalAsync(
        AppDbContext db,
        Guid companyId,
        long currentTick,
        string title,
        string message,
        string notificationType)
    {
        var shareholdings = await db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.CompanyId == companyId && holding.ShareCount > 0m)
            .ToListAsync();
        var ownerCompanyIds = shareholdings
            .Where(holding => holding.OwnerCompanyId.HasValue)
            .Select(holding => holding.OwnerCompanyId!.Value)
            .Distinct()
            .ToList();
        var ownerCompanyPlayerById = ownerCompanyIds.Count == 0
            ? new Dictionary<Guid, Guid>()
            : await db.Companies
                .AsNoTracking()
                .Where(company => ownerCompanyIds.Contains(company.Id))
                .ToDictionaryAsync(company => company.Id, company => company.PlayerId);

        var playerIds = shareholdings
            .Where(holding => holding.OwnerPlayerId.HasValue)
            .Select(holding => holding.OwnerPlayerId!.Value)
            .Concat(shareholdings
                .Where(holding => holding.OwnerCompanyId.HasValue)
                .Select(holding => ownerCompanyPlayerById.GetValueOrDefault(holding.OwnerCompanyId!.Value)))
            .Where(playerId => playerId != Guid.Empty)
            .Distinct()
            .ToList();

        foreach (var playerId in playerIds)
        {
            PlayerNotificationService.Add(
                db,
                playerId,
                notificationType,
                title,
                message,
                currentTick,
                companyId: companyId);
        }
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
