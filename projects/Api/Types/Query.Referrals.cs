using Api.Data;
using Api.Security;
using Capitalism.Shared.Referrals;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>Returns the authenticated player's referral code and usage summary.</summary>
    [Authorize]
    public async Task<ReferralProgramSummary> GetMyReferralProgram(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var referralCode = await db.ReferralCodes
            .AsNoTracking()
            .FirstOrDefaultAsync(code => code.CreatorPlayerId == userId);

        return new ReferralProgramSummary
        {
            Code = referralCode?.Code,
            UsageCount = referralCode?.UsageCount ?? 0,
            CreatedAtUtc = referralCode?.CreatedAtUtc,
            DiscountRate = ReferralProgramConstants.PurchaseDiscountRate,
        };
    }
}
