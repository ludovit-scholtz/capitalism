using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    private const long DividendVotingWindowTicks = 10;
    private const long DividendPolicyVotingWindowTicks = GameConstants.TicksPerDay * 5;

    private async Task<DividendProposalResult> ProposeDividendPolicyAsync(
        ProposeDividendInput input,
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ObjectAuthorizationService objectAuthorization)
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
            .FirstOrDefaultAsync(candidate => candidate.Id == input.CompanyId.Value);
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        if (company is null || company.PlayerId != userId)
        {
            throw objectAuthorization.Deny(
                userId,
                "company",
                input.CompanyId.Value,
                company is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
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
}
