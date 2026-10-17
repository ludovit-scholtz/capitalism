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
    public async Task<DividendProposalResult> ProposeDividend(
        ProposeDividendInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        // New governance-policy contract:
        // proposeDividend(companyId, dividendPercent) + voteDividend(companyId, vote)
        if (input.CompanyId.HasValue || input.DividendPercent.HasValue)
        {
            return await ProposeDividendPolicyAsync(input, db, httpContextAccessor, objectAuthorization);
        }

        // Legacy per-share payout proposal contract used by stock-exchange flow:
        // proposeDividend(stockSymbol, dividendPerShare)
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
}
