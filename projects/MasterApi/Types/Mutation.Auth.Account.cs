using System.Security.Claims;
using System.Diagnostics;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Security;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{

    [HotChocolate.Authorization.Authorize]
    public async Task<UpdatePersonalAccountNamePayload> UpdatePersonalAccountName(
        UpdatePersonalAccountNameInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] PersonalAccountNamePropagationService propagationService,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None;
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var personalAccountName = input.PersonalAccountName?.Trim();
        if (string.IsNullOrWhiteSpace(personalAccountName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name is required.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_REQUIRED")
                    .Build());
        }

        if (personalAccountName.Length > 40)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Personal account name must be 40 characters or fewer.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_TOO_LONG")
                    .Build());
        }

        string? normalizedGender = null;
        if (input.Gender is not null)
        {
            var trimmedGender = input.Gender.Trim().ToUpperInvariant();
            if (!PlayerGender.IsValid(trimmedGender))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Gender must be MALE, FEMALE, or UNSPECIFIED.")
                        .SetCode("INVALID_GENDER")
                        .Build());
            }

            normalizedGender = trimmedGender;
        }

        var duplicateExists = await db.PlayerAccounts
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.Id != player.Id
                && candidate.DisplayName.ToLower() == personalAccountName.ToLower());
        if (duplicateExists)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This personal account name is already taken.")
                    .SetCode("PERSONAL_ACCOUNT_NAME_NOT_UNIQUE")
                    .Build());
        }

        player.DisplayName = personalAccountName;
        if (normalizedGender is not null)
        {
            player.Gender = normalizedGender;
        }
        await db.SaveChangesAsync(ct);
        await propagationService.PropagateAsync(player.Email, player.DisplayName, player.Gender, ct);

        return new UpdatePersonalAccountNamePayload
        {
            PlayerId = player.Id,
            PersonalAccountName = player.DisplayName,
            Gender = player.Gender,
        };
    }

    /// <summary>Prolongs (or creates) a Pro subscription for the authenticated player.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<SubscriptionInfo> ProlongSubscription(
        ProlongSubscriptionInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService)
    {
        if (input.Months < 1 || input.Months > 12)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Months must be between 1 and 12.")
                    .SetCode("INVALID_MONTHS")
                    .Build());
        }

        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;
        var now = DateTime.UtcNow;
        var hasReferralDiscount = player.ReferredByEmail is not null;
        var monthlyPrice = ResolveGoldPriceWithReferralDiscount(MonthlyProPriceGold, hasReferralDiscount);
        var totalPrice = decimal.Round(monthlyPrice * input.Months, 8, MidpointRounding.AwayFromZero);
        EnsureSufficientGoldBalance(player, totalPrice, "Not enough tokenized gold for monthly Pro purchase.");
        var balanceBeforeCharge = player.GoldTokenBalance;
        ApplyGoldDebit(player, totalPrice);
        AddSystemGoldTransaction(
            db,
            player,
            -totalPrice,
            balanceBeforeCharge,
            $"PRO_SUBSCRIPTION_PURCHASE:{input.Months}M");

        var latestSub = await GetLatestSubscriptionAsync(db, userId);
        var resultSub = GrantOrCreateProSubscription(db, userId, now, input.Months, latestSub);

        await db.SaveChangesAsync();

        // Award RECOMMEND_GOOD_FRIEND bounty to the referrer when a referred player subscribes.
        if (player.ReferredByEmail is not null)
        {
            try
            {
                await rankingService.IngestEventAsync(
                    MasterRankingBountyCodes.RecommendGoodFriend,
                    player.ReferredByEmail,
                    serverKey: null,
                    externalEventId: null,
                    uniqueScopeKey: $"sub:{player.Id}",
                    idempotencyKey: null,
                    proofReference: null,
                    payloadJson: "{}",
                    occurredAtUtc: now,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Bounty failure must never block the subscription flow.
            }
        }

        return Query.BuildSubscriptionInfo(resultSub, now);
    }

    /// <summary>Claims the one-time startup pack on the master portal and grants 3 months of Pro access.</summary>
    [HotChocolate.Authorization.Authorize]
    public async Task<SubscriptionInfo> ClaimStartupPack(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService)
    {
        var player = await Query.GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        var userId = player.Id;
        var now = DateTime.UtcNow;
        var hasReferralDiscount = player.ReferredByEmail is not null;
        var startupPackPrice = ResolveGoldPriceWithReferralDiscount(StartupPackPriceGold, hasReferralDiscount);

        var latestSub = await GetLatestSubscriptionAsync(db, userId);

        if (player.StartupPackClaimedAtUtc is not null)
        {
            return Query.BuildSubscriptionInfo(latestSub, now);
        }

        EnsureSufficientGoldBalance(player, startupPackPrice, "Not enough tokenized gold for startup pack purchase.");
        var balanceBeforeCharge = player.GoldTokenBalance;
        ApplyGoldDebit(player, startupPackPrice);
        AddSystemGoldTransaction(
            db,
            player,
            -startupPackPrice,
            balanceBeforeCharge,
            "STARTUP_PACK_PURCHASE:3M");

        var resultSub = GrantOrCreateProSubscription(db, userId, now, StartupPackDurationMonths, latestSub);
        player.StartupPackClaimedAtUtc = now;

        await db.SaveChangesAsync();

        // Award RECOMMEND_GOOD_FRIEND bounty to the referrer when a referred player claims the startup pack.
        if (player.ReferredByEmail is not null)
        {
            try
            {
                await rankingService.IngestEventAsync(
                    MasterRankingBountyCodes.RecommendGoodFriend,
                    player.ReferredByEmail,
                    serverKey: null,
                    externalEventId: null,
                    uniqueScopeKey: $"startup:{player.Id}",
                    idempotencyKey: null,
                    proofReference: null,
                    payloadJson: "{}",
                    occurredAtUtc: now,
                    cancellationToken: CancellationToken.None);
            }
            catch
            {
                // Bounty failure must never block the startup pack claim flow.
            }
        }

        return Query.BuildSubscriptionInfo(resultSub, now);
    }
}
