using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    [Authorize]
    public async Task<ReplaceCeoResult> ReplaceCEO(
        ReplaceCeoInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        if (input.NewCeoPlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You can only appoint yourself as the new CEO when taking control.")
                    .SetCode("INVALID_NEW_CEO_PLAYER")
                    .Build());
        }

        var player = await db.Players.FirstOrDefaultAsync(candidate => candidate.Id == userId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var targetCompany = await ClaimCompanyControlIfEligibleAsync(db, userId, input.CompanyId);
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = targetCompany.Id;
        await db.SaveChangesAsync();

        return new ReplaceCeoResult
        {
            CompanyId = targetCompany.Id,
            CompanyName = targetCompany.Name,
            NewCeoPlayerId = userId,
            NewCeoDisplayName = PublicPlayerDisplayName.Resolve(player),
        };
    }

    private async Task<Company> ClaimCompanyControlIfEligibleAsync(AppDbContext db, Guid userId, Guid companyId)
    {
        var companies = await db.Companies.ToListAsync();
        var targetCompany = companies.FirstOrDefault(company => company.Id == companyId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Company not found.")
                    .SetCode("COMPANY_NOT_FOUND")
                    .Build());

        if (targetCompany.PlayerId == userId)
        {
            return targetCompany;
        }

        var shareholdings = await db.Shareholdings
            .Where(holding => holding.CompanyId == targetCompany.Id)
            .ToListAsync();
        var controlledOwnershipRatio = ComputeControlledOwnershipRatio(userId, targetCompany, companies, shareholdings);

        if (controlledOwnershipRatio < 0.5m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You need at least 50% combined ownership through your person account and controlled companies to take control of this company.")
                    .SetCode("COMPANY_CONTROL_REQUIRED")
                    .Build());
        }

        targetCompany.PlayerId = userId;
        return targetCompany;
    }
}
