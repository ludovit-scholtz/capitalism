using System.Security.Claims;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Query
{
    [HotChocolate.Authorization.Authorize]
    public async Task<RankingSummaryInfo> GetMyRankingSummary(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var snapshot = await db.MasterRankingPlayerSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.PlayerAccountId == player.Id);

        return new RankingSummaryInfo
        {
            TotalPoints = snapshot?.TotalPoints ?? 0m,
            GlobalRank = snapshot?.GlobalRank ?? 0,
            PreviousGlobalRank = snapshot?.PreviousGlobalRank ?? 0,
            RankMovement = (snapshot?.PreviousGlobalRank ?? 0) - (snapshot?.GlobalRank ?? 0),
            UpdatedAtUtc = snapshot?.UpdatedAtUtc ?? player.CreatedAtUtc,
        };
    }

    public async Task<List<RankingLeaderboardEntryInfo>> GetRankingLeaderboard(
        [Service] MasterDbContext db,
        int limit = 100,
        int offset = 0)
    {
        var clampedLimit = Math.Clamp(limit, 1, 200);
        var clampedOffset = Math.Max(0, offset);

        return await db.MasterRankingPlayerSnapshots
            .AsNoTracking()
            .Include(snapshot => snapshot.PlayerAccount)
            .OrderBy(snapshot => snapshot.GlobalRank)
            .Skip(clampedOffset)
            .Take(clampedLimit)
            .Select(snapshot => new RankingLeaderboardEntryInfo
            {
                PlayerId = snapshot.PlayerAccountId,
                DisplayName = snapshot.PlayerAccount.DisplayName,
                TotalPoints = snapshot.TotalPoints,
                GlobalRank = snapshot.GlobalRank,
                RankMovement = snapshot.PreviousGlobalRank - snapshot.GlobalRank,
            })
            .ToListAsync();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<List<RankingRewardHistoryItem>> GetMyRankingBountyHistory(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        RankingHistoryFilterInput? input = null)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var query = db.MasterRankingRewardRecords
            .AsNoTracking()
            .Include(record => record.BountyDefinition)
            .Where(record => record.PlayerAccountId == player.Id)
            .AsQueryable();

        if (input is not null)
        {
            if (!string.IsNullOrWhiteSpace(input.BountyCode))
            {
                var code = input.BountyCode.Trim().ToUpperInvariant();
                query = query.Where(record => record.BountyDefinition.Code == code);
            }

            if (!string.IsNullOrWhiteSpace(input.ServerKey))
            {
                var serverKey = input.ServerKey.Trim();
                query = query.Where(record => record.ServerKey == serverKey);
            }

            if (!string.IsNullOrWhiteSpace(input.Status))
            {
                var status = input.Status.Trim().ToUpperInvariant();
                query = query.Where(record => record.Status == status);
            }

            if (input.FromUtc.HasValue)
            {
                query = query.Where(record => record.AwardedAtUtc >= input.FromUtc.Value);
            }

            if (input.ToUtc.HasValue)
            {
                query = query.Where(record => record.AwardedAtUtc <= input.ToUtc.Value);
            }
        }

        var limit = Math.Clamp(input?.Limit ?? 100, 1, 200);
        var offset = Math.Max(0, input?.Offset ?? 0);

        var records = await query
            .OrderByDescending(record => record.AwardedAtUtc)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();

        return records.Select(record => new RankingRewardHistoryItem
        {
            Id = record.Id,
            BountyCode = record.BountyDefinition.Code,
            BountyDisplayName = record.BountyDefinition.DisplayName,
            PointsAwarded = record.PointsAwarded,
            Status = record.Status,
            ServerKey = record.ServerKey,
            EventDateUtc = record.EventDateUtc,
            AwardedAtUtc = record.AwardedAtUtc,
            MetadataJson = record.BountyDefinition.VisibilityScope == RankingVisibilityScope.AdminOnly
                ? "{}"
                : record.AwardMetadataJson,
        }).ToList();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingAdminDashboardInfo> GetRankingAdminDashboard(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking administration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var definitions = await db.MasterRankingBountyDefinitions
            .AsNoTracking()
            .OrderBy(definition => definition.Code)
            .Select(definition => new RankingBountyDefinitionInfo
            {
                Id = definition.Id,
                Code = definition.Code,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                RewardPoints = definition.RewardPoints,
                IsEnabled = definition.IsEnabled,
                IsVisibleToPlayers = definition.IsVisibleToPlayers,
                RequiresModeration = definition.RequiresModeration,
                CooldownMode = definition.CooldownMode,
                SourceEventType = definition.SourceEventType,
                ProofRequirement = definition.ProofRequirement,
                VisibilityScope = definition.VisibilityScope,
                ValidationSettingsJson = definition.ValidationSettingsJson,
                UpdatedAtUtc = definition.UpdatedAtUtc,
            })
            .ToListAsync();

        var moderationQueue = await db.MasterRankingEvents
            .AsNoTracking()
            .Where(entry => entry.Status == RankingEventStatus.PendingModeration)
            .OrderBy(entry => entry.CreatedAtUtc)
            .Take(200)
            .Select(entry => new RankingEventModerationItem
            {
                Id = entry.Id,
                EventType = entry.EventType,
                PlayerEmail = entry.PlayerEmail,
                ServerKey = entry.ServerKey,
                ProofReference = entry.ProofReference,
                PayloadJson = entry.PayloadJson,
                Status = entry.Status,
                OccurredAtUtc = entry.OccurredAtUtc,
                CreatedAtUtc = entry.CreatedAtUtc,
            })
            .ToListAsync();

        var runs = await db.MasterRankingEvaluationRuns
            .AsNoTracking()
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(50)
            .Select(run => new RankingRunInfo
            {
                Id = run.Id,
                RunType = run.RunType,
                Status = run.Status,
                StartedAtUtc = run.StartedAtUtc,
                FinishedAtUtc = run.FinishedAtUtc,
                ProcessedEvents = run.ProcessedEvents,
                RewardRecordsCreated = run.RewardRecordsCreated,
                TotalPointsAwarded = run.TotalPointsAwarded,
                TotalPointsBeforeDecay = run.TotalPointsBeforeDecay,
                TotalPointsAfterDecay = run.TotalPointsAfterDecay,
                Notes = run.Notes,
            })
            .ToListAsync();

        return new RankingAdminDashboardInfo
        {
            Bounties = definitions,
            PendingModerationEvents = moderationQueue,
            RecentRuns = runs,
        };
    }
}
