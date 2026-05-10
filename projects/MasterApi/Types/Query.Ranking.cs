using System.Security.Claims;
using Capitalism.Shared.Ranking;
using Capitalism.Shared.Security;
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
                DisplayName = PlayerDisplayNameProvisioning.ResolveDisplayName(
                    snapshot.PlayerAccount.DisplayName,
                    snapshot.PlayerAccount.Email,
                    snapshot.PlayerAccountId.ToString()),
                PersonalAccountName = PlayerDisplayNameProvisioning.ResolveDisplayName(
                    snapshot.PlayerAccount.DisplayName,
                    snapshot.PlayerAccount.Email,
                    snapshot.PlayerAccountId.ToString()),
                TotalPoints = snapshot.TotalPoints,
                GlobalRank = snapshot.GlobalRank,
                RankMovement = snapshot.PreviousGlobalRank - snapshot.GlobalRank,
            })
            .ToListAsync();
    }

    public async Task<List<TutorialBountyStatusInfo>> GetTutorialBountyStatuses(
        GetTutorialBountyStatusesInput input,
        [Service] MasterDbContext db,
        [Service] IOptions<MasterServerOptions> masterServerOptions)
    {
        Query.EnsureServiceAccess(input, masterServerOptions, requireRegistrationKey: true, requireServerKey: false);

        var playerEmail = Query.NormalizeEmail(input.PlayerEmail, "INVALID_PLAYER_EMAIL");
        var playerId = await db.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.Email == playerEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync();

        Dictionary<string, MasterRankingRewardRecord>? rewardsByCode = null;
        if (playerId.HasValue)
        {
            var tutorialBountyCodes = TutorialRankingBountyCatalog.ByBountyCode.Keys.ToList();
            rewardsByCode = await db.MasterRankingRewardRecords
                .AsNoTracking()
                .Include(record => record.BountyDefinition)
                .Where(record => record.PlayerAccountId == playerId.Value && record.Status == RankingRewardStatus.Awarded)
                .Where(record => tutorialBountyCodes.Contains(record.BountyDefinition.Code))
                .GroupBy(record => record.BountyDefinition.Code)
                .Select(group => group.OrderByDescending(item => item.AwardedAtUtc).First())
                .ToDictionaryAsync(record => record.BountyDefinition.Code, StringComparer.Ordinal);
        }

        return TutorialRankingBountyCatalog.All
            .Select(entry =>
            {
                MasterRankingRewardRecord? reward = null;
                var hasReward = rewardsByCode is not null && rewardsByCode.TryGetValue(entry.BountyCode, out reward);
                return new TutorialBountyStatusInfo
                {
                    Milestone = entry.Milestone,
                    BountyCode = entry.BountyCode,
                    IsAwarded = hasReward,
                    AwardedAtUtc = reward?.AwardedAtUtc,
                    RewardPoints = entry.RewardPoints,
                };
            })
            .ToList();
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
    public async Task<List<RankingBountyDashboardItemInfo>> GetMyRankingBountyDashboard(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db)
    {
        var player = await GetCurrentUserAsync(claimsPrincipal, db)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var definitions = await db.MasterRankingBountyDefinitions
            .AsNoTracking()
            .Where(definition => definition.IsEnabled && definition.IsVisibleToPlayers)
            .OrderBy(definition => definition.DisplayName)
            .ToListAsync();

        var rewards = await db.MasterRankingRewardRecords
            .AsNoTracking()
            .Where(record => record.PlayerAccountId == player.Id && record.Status == RankingRewardStatus.Awarded)
            .ToListAsync();

        var rewardsByDefinitionId = rewards
            .GroupBy(record => record.BountyDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var todayUtc = DateTime.UtcNow.Date;

        return definitions
            .Select(definition =>
            {
                var definitionRewards = rewardsByDefinitionId.TryGetValue(definition.Id, out var items)
                    ? items
                    : [];

                var lastAward = definitionRewards
                    .OrderByDescending(item => item.AwardedAtUtc)
                    .FirstOrDefault();
                var awardedToday = definitionRewards.Any(item => item.EventDateUtc.Date == todayUtc);

                var (isAvailableNow, nextAvailableAtUtc) = ComputeAvailability(definition.CooldownMode, awardedToday, definitionRewards.Count);

                return new RankingBountyDashboardItemInfo
                {
                    Id = definition.Id,
                    Code = definition.Code,
                    DisplayName = definition.DisplayName,
                    Description = definition.Description,
                    RewardPoints = definition.RewardPoints,
                    CooldownMode = definition.CooldownMode,
                    ProofRequirement = definition.ProofRequirement,
                    RequiresModeration = definition.RequiresModeration,
                    AwardedToday = awardedToday,
                    IsAvailableNow = isAvailableNow,
                    NextAvailableAtUtc = nextAvailableAtUtc,
                    LastAwardedAtUtc = lastAward?.AwardedAtUtc,
                    TotalAwards = definitionRewards.Count,
                };
            })
            .ToList();
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<bool> GetCanAccessRankingAdminDashboard(
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = GetEmailFromClaims(claimsPrincipal);
        var access = await BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        return access.CanAccessEveryGameDashboard;
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

    private static (bool isAvailableNow, DateTime? nextAvailableAtUtc) ComputeAvailability(
        string cooldownMode,
        bool awardedToday,
        int totalAwards)
    {
        var todayUtc = DateTime.UtcNow.Date;
        return cooldownMode switch
        {
            RankingCooldownMode.Once => (totalAwards == 0, null),
            RankingCooldownMode.UtcDay => (!awardedToday, awardedToday ? todayUtc.AddDays(1) : null),
            RankingCooldownMode.UtcDayPerServer => (!awardedToday, awardedToday ? todayUtc.AddDays(1) : null),
            RankingCooldownMode.PerUniqueKey => (true, null),
            _ => (true, null),
        };
    }
}
