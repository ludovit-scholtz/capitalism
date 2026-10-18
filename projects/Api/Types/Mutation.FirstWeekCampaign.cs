using Api.Data;
using Api.Data.Entities;
using Api.Engine.Campaign;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Claims the reward of one first-week goal: server-authoritative (the goal must be unlocked and complete according to existing data),
    /// idempotent (a goal pays at most once; the unique (player, goal) index makes simultaneous claims a single winner) and paid as a
    /// credit to the company's USD bank account with a CAMPAIGN_REWARD ledger entry.
    /// Auth decision: [Authorize]; an explicit company must belong to the caller, otherwise the uniform FORBIDDEN (missing and foreign look the same).
    /// </summary>
    [Authorize]
    public async Task<ClaimCampaignRewardResult> ClaimCampaignReward(
        ClaimCampaignRewardInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        var player = await db.Players.FirstOrDefaultAsync(p => p.Id == userId, ct)
            ?? throw CampaignError("Player not found.", "PLAYER_NOT_FOUND");
        var goal = FirstWeekCampaignCatalog.Find(input.GoalCode)
            ?? throw CampaignError("Unknown campaign goal.", "CAMPAIGN_GOAL_UNKNOWN");

        Company company;
        if (input.CompanyId is { } requestedCompanyId)
        {
            company = await objectAuthorization.RequireOwnedAsync(
                userId, "company", requestedCompanyId,
                token => db.Companies.FirstOrDefaultAsync(c => c.Id == requestedCompanyId, token),
                c => c.PlayerId == userId,
                ct);
        }
        else
        {
            company = await db.Companies
                .Where(c => c.PlayerId == userId)
                .OrderByDescending(c => c.Id == player.ActiveCompanyId)
                .ThenBy(c => c.FoundedAtUtc)
                .FirstOrDefaultAsync(ct)
                ?? throw CampaignError("You need a company to receive the reward.", "CAMPAIGN_NO_COMPANY");
        }

        var state = await FirstWeekCampaignLoader.LoadAsync(db, player, DateTime.UtcNow, ct);
        if (!state.Started) throw CampaignError("The campaign starts once onboarding is finished.", "CAMPAIGN_NOT_STARTED");
        var goalState = state.Goals.Single(g => g.Code == goal.Code);
        if (goalState.IsClaimed)
        {
            return new ClaimCampaignRewardResult { Claimed = false, GoalCode = goal.Code, Amount = goal.RewardAmount, CurrencyCode = FirstWeekCampaignCatalog.RewardCurrency };
        }

        if (!goalState.IsUnlocked) throw CampaignError("This goal unlocks on day " + goal.Day + ".", "CAMPAIGN_GOAL_LOCKED");
        if (!goalState.IsComplete) throw CampaignError("Finish the goal before claiming its reward.", "CAMPAIGN_GOAL_NOT_COMPLETE");

        var currentTick = (await db.GameStates.FirstOrDefaultDeterministicAsync(ct))?.CurrentTick ?? 0;
        var account = await CompanyBankingService.EnsurePreferredAccountAsync(db, company.Id, FirstWeekCampaignCatalog.RewardCurrency, ct);
        account.Balance += goal.RewardAmount;
        account.ConcurrencyToken = Guid.NewGuid();
        db.PlayerCampaignClaims.Add(new PlayerCampaignClaim
        {
            Id = Guid.NewGuid(),
            PlayerId = userId,
            CompanyId = company.Id,
            GoalCode = goal.Code,
            Amount = goal.RewardAmount,
            CurrencyCode = FirstWeekCampaignCatalog.RewardCurrency,
            ClaimedAtTick = currentTick,
            ClaimedAtUtc = DateTime.UtcNow,
        });
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BankAccountId = account.Id,
            Category = LedgerCategory.CampaignReward,
            Description = $"First-week campaign reward: {goal.Code}",
            Amount = goal.RewardAmount,
            RecordedAtTick = currentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        try
        {
            // One SaveChanges: the claim row, the credit and the ledger entry commit together or not at all.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A simultaneous claim of the same goal won the unique (player, goal) index; its transaction paid, ours rolled back.
            db.ChangeTracker.Clear();
            if (await db.PlayerCampaignClaims.AsNoTracking().AnyAsync(c => c.PlayerId == userId && c.GoalCode == goal.Code, ct))
            {
                return new ClaimCampaignRewardResult { Claimed = false, GoalCode = goal.Code, Amount = goal.RewardAmount, CurrencyCode = FirstWeekCampaignCatalog.RewardCurrency };
            }

            throw;
        }

        return new ClaimCampaignRewardResult { Claimed = true, GoalCode = goal.Code, Amount = goal.RewardAmount, CurrencyCode = FirstWeekCampaignCatalog.RewardCurrency };
    }

    private static GraphQLException CampaignError(string message, string code)
        => new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());
}
