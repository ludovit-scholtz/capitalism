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
    private const long DividendVotingWindowTicks = 10;
    private const long DividendPolicyVotingWindowTicks = GameConstants.TicksPerDay * 5;

    [Authorize]
    public async Task<DividendProposalResult> ProposeDividend(
        ProposeDividendInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (input.CompanyId.HasValue || input.DividendPercent.HasValue)
        {
            return await ProposeDividendPolicyAsync(input, db, httpContextAccessor);
        }

        if (!input.DividendPerShare.HasValue || input.DividendPerShare.Value <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Dividend per share must be greater than zero.")
                    .SetCode("INVALID_DIVIDEND_PER_SHARE")
                    .Build());
        }

        if (!StockSymbolCodec.TryParseCompanyId(input.StockSymbol, out var companyId))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid stock symbol.")
                    .SetCode("INVALID_STOCK_SYMBOL")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var account = await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext.User);
        var company = await db.Companies
            .Include(candidate => candidate.BankAccounts)
            .FirstOrDefaultAsync(candidate => candidate.Id == companyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        var governmentCompanyIds = await GovernmentCompanyQueries.GetGovernmentCompanyIdsAsync(db);
        if (IsGovernmentCompany(governmentCompanyIds, company))
        {
            throw CreateGovernmentSharesNotTradeableException();
        }

        var currentTick = await GetCurrentTickAsync(db);
        var hasOpenProposal = await db.DividendProposals.AnyAsync(proposal =>
            proposal.CompanyId == companyId
            && proposal.Status == DividendProposalStatus.Voting
            && proposal.VotingCloseTick >= currentTick);
        if (hasOpenProposal)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("There is already an open dividend vote for this company.")
                    .SetCode("DIVIDEND_PROPOSAL_ALREADY_OPEN")
                    .Build());
        }

        var controlledCompanyIds = await db.Companies
            .AsNoTracking()
            .Where(candidate => candidate.PlayerId == userId)
            .Select(candidate => candidate.Id)
            .ToListAsync();
        var shareholdings = await db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.CompanyId == companyId && holding.ShareCount > 0m)
            .ToListAsync();

        var combinedOwnedShares = shareholdings
            .Where(holding => holding.OwnerPlayerId == userId
                || (holding.OwnerCompanyId.HasValue && controlledCompanyIds.Contains(holding.OwnerCompanyId.Value)))
            .Sum(holding => holding.ShareCount);
        var ownershipRatio = company.TotalSharesIssued > 0m
            ? combinedOwnedShares / company.TotalSharesIssued
            : 0m;
        var canPropose = company.PlayerId == userId || ownershipRatio > 0.5m;
        if (!canPropose)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only a majority shareholder or company board owner can propose dividends.")
                    .SetCode("DIVIDEND_PROPOSAL_NOT_ALLOWED")
                    .Build());
        }

        var dividendPerShare = decimal.Round(input.DividendPerShare.Value, 4, MidpointRounding.AwayFromZero);
        var totalPayout = decimal.Round(dividendPerShare * company.TotalSharesIssued, 4, MidpointRounding.AwayFromZero);
        if (totalPayout <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Total payout must be greater than zero.")
                    .SetCode("INVALID_DIVIDEND_TOTAL_PAYOUT")
                    .Build());
        }

        if (CompanyBankingService.GetTotalBalance(company.BankAccounts) < totalPayout)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company does not have enough cash to reserve this dividend payout.")
                    .SetCode("INSUFFICIENT_COMPANY_FUNDS")
                    .Build());
        }

        if (!CompanyBankingService.TryDebit(company.BankAccounts, totalPayout))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company does not have enough cash to reserve this dividend payout.")
                    .SetCode("INSUFFICIENT_COMPANY_FUNDS")
                    .Build());
        }

        var proposal = new DividendProposal
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            StockSymbol = StockSymbolCodec.FromCompanyId(companyId),
            ProposedByAccountId = account.Company?.Id ?? player.Id,
            ProposedByAccountType = account.AccountType,
            DividendPerShare = dividendPerShare,
            TotalPayout = totalPayout,
            Status = DividendProposalStatus.Voting,
            ProposedAtTick = currentTick,
            VotingOpenTick = currentTick,
            VotingCloseTick = currentTick + DividendVotingWindowTicks,
        };

        db.DividendProposals.Add(proposal);
        AddCompanyLedgerEntry(
            db,
            company,
            LedgerCategory.Dividend,
            $"Reserved dividend payout proposal ({dividendPerShare:0.####} per share) for shareholder vote.",
            -totalPayout,
            currentTick);

        await NotifyShareholdersAboutDividendProposalAsync(
            db,
            companyId,
            currentTick,
            "Dividend proposal opened",
            $"{company.Name} proposed a dividend of {dividendPerShare:0.####} per share. Voting closes at tick {proposal.VotingCloseTick}.",
            PlayerNotificationType.DividendProposalOpened);

        await db.SaveChangesAsync();

        return MapDividendProposalResult(proposal, forVotes: 0m, againstVotes: 0m, currentTick: currentTick, myVote: null);
    }

    [Authorize]
    public async Task<DividendVoteResult> VoteDividendProposal(
        VoteDividendProposalInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var choice = NormalizeVoteChoice(input.Choice);
        var currentTick = await GetCurrentTickAsync(db);

        var proposal = await db.DividendProposals
            .FirstOrDefaultAsync(candidate => candidate.Id == input.ProposalId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Dividend proposal not found.")
                    .SetCode("DIVIDEND_PROPOSAL_NOT_FOUND")
                    .Build());

        if (proposal.Status != DividendProposalStatus.Voting || currentTick > proposal.VotingCloseTick)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Voting is closed for this dividend proposal.")
                    .SetCode("DIVIDEND_VOTING_CLOSED")
                    .Build());
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var account = await ResolveActiveTradingAccountAsync(db, player, httpContextAccessor.HttpContext.User);
        var voterAccountId = account.Company?.Id ?? player.Id;

        var alreadyVoted = await db.DividendVotes.AnyAsync(vote =>
            vote.ProposalId == proposal.Id
            && vote.VoterAccountId == voterAccountId);
        if (alreadyVoted)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This account has already voted on the proposal.")
                    .SetCode("DIVIDEND_ALREADY_VOTED")
                    .Build());
        }

        IQueryable<Shareholding> sharesQuery = db.Shareholdings
            .AsNoTracking()
            .Where(holding => holding.CompanyId == proposal.CompanyId && holding.ShareCount > 0m);
        sharesQuery = account.Company is null
            ? sharesQuery.Where(holding => holding.OwnerPlayerId == player.Id)
            : sharesQuery.Where(holding => holding.OwnerCompanyId == account.Company.Id);
        var sharesVoted = await sharesQuery.SumAsync(holding => holding.ShareCount);
        if (sharesVoted <= 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only shareholders can vote on dividend proposals.")
                    .SetCode("DIVIDEND_NOT_SHAREHOLDER")
                    .Build());
        }

        var vote = new DividendVote
        {
            Id = Guid.NewGuid(),
            ProposalId = proposal.Id,
            VoterAccountId = voterAccountId,
            VoterAccountType = account.AccountType,
            SharesVoted = decimal.Round(sharesVoted, 4, MidpointRounding.AwayFromZero),
            VoteChoice = choice,
            CastAtTick = currentTick,
        };

        db.DividendVotes.Add(vote);
        await db.SaveChangesAsync();

        return new DividendVoteResult
        {
            Id = vote.Id,
            ProposalId = vote.ProposalId,
            VoterAccountId = vote.VoterAccountId,
            VoterAccountType = vote.VoterAccountType,
            SharesVoted = vote.SharesVoted,
            VoteChoice = vote.VoteChoice,
            CastAtTick = vote.CastAtTick,
        };
    }

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

    private async Task<DividendProposalResult> ProposeDividendPolicyAsync(
        ProposeDividendInput input,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor)
    {
        if (!input.CompanyId.HasValue || !input.DividendPercent.HasValue)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("companyId and dividendPercent are required.")
                    .SetCode("INVALID_DIVIDEND_PROPOSAL_INPUT")
                    .Build());
        }

        var company = await db.Companies
            .FirstOrDefaultAsync(candidate => candidate.Id == input.CompanyId.Value)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        if (company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only the acting CEO can propose dividend changes.")
                    .SetCode("NOT_CEO")
                    .Build());
        }

        var currentTick = await GetCurrentTickAsync(db);
        var hasOpenProposal = await db.DividendProposals.AnyAsync(proposal =>
            proposal.CompanyId == company.Id
            && proposal.Status == DividendProposalStatus.Voting
            && proposal.TotalPayout <= 0m
            && proposal.VotingCloseTick >= currentTick);
        if (hasOpenProposal)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("There is already a pending dividend proposal.")
                    .SetCode("PROPOSAL_ALREADY_PENDING")
                    .Build());
        }

        var ratio = decimal.Round(
            Math.Clamp(input.DividendPercent.Value, 0m, 100m) / 100m,
            4,
            MidpointRounding.AwayFromZero);
        var proposal = new DividendProposal
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            StockSymbol = StockSymbolCodec.FromCompanyId(company.Id),
            ProposedByAccountId = userId,
            ProposedByAccountType = "PERSON",
            DividendPerShare = ratio,
            TotalPayout = 0m,
            Status = DividendProposalStatus.Voting,
            ProposedAtTick = currentTick,
            VotingOpenTick = currentTick,
            VotingCloseTick = currentTick + DividendPolicyVotingWindowTicks,
        };
        db.DividendProposals.Add(proposal);
        await db.SaveChangesAsync();
        return MapDividendProposalResult(proposal, 0m, 0m, currentTick, null);
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
