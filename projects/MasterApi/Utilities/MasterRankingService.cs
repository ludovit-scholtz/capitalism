using System.Text.Json;
using Capitalism.Shared.Security;
using HotChocolate;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MasterApi.Utilities;

public sealed partial class MasterRankingService(MasterDbContext db, ILogger<MasterRankingService> logger)
{

    private const decimal DailyDecayFactor = 0.99m;
    private const string ShadowPasswordHashPrefix = "__SHADOW__:";
    private static readonly AsyncKeyedLock PlayerAccountProvisionLocks = new(StringComparer.OrdinalIgnoreCase);

    internal static bool IsShadowProvisionedPasswordHash(string? passwordHash)
    {
        return !string.IsNullOrWhiteSpace(passwordHash)
            && passwordHash.StartsWith(ShadowPasswordHashPrefix, StringComparison.Ordinal);
    }

    internal static string BuildShadowPasswordHash(string normalizedEmail)
    {
        return $"{ShadowPasswordHashPrefix}{normalizedEmail}";
    }

    public async Task<MasterRankingEvaluationRun> EvaluateHourlyAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var run = new MasterRankingEvaluationRun
        {
            Id = Guid.NewGuid(),
            RunType = RankingRunType.HourlyEvaluation,
            Status = RankingRunStatus.Succeeded,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = startedAtUtc,
            Notes = string.Empty,
        };

        db.MasterRankingEvaluationRuns.Add(run);

        try
        {
            var definitions = await db.MasterRankingBountyDefinitions
                .Where(definition => definition.IsEnabled)
                .ToListAsync(cancellationToken);

            var definitionsByEvent = definitions
                .Where(definition => !string.IsNullOrWhiteSpace(definition.SourceEventType))
                .GroupBy(definition => definition.SourceEventType, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);

            var pendingEvents = await db.MasterRankingEvents
                .Where(entry =>
                    (entry.Status == RankingEventStatus.Pending || entry.Status == RankingEventStatus.Approved)
                    && !entry.IsQuarantined)
                .OrderBy(entry => entry.CreatedAtUtc)
                .Take(5000)
                .ToListAsync(cancellationToken);

            var playerIds = pendingEvents
                .Where(entry => entry.PlayerAccountId.HasValue)
                .Select(entry => entry.PlayerAccountId!.Value)
                .Distinct()
                .ToList();

            var playerSnapshots = await db.MasterRankingPlayerSnapshots
                .Where(snapshot => playerIds.Contains(snapshot.PlayerAccountId))
                .ToDictionaryAsync(snapshot => snapshot.PlayerAccountId, cancellationToken);
            var awardedUniquenessKeysInRun = new HashSet<string>(StringComparer.Ordinal);

            var now = DateTime.UtcNow;

            foreach (var rankingEvent in pendingEvents)
            {
                if (!definitionsByEvent.TryGetValue(rankingEvent.EventType, out var matchingDefinitions)
                    || matchingDefinitions.Count == 0)
                {
                    rankingEvent.Status = RankingEventStatus.Processed;
                    rankingEvent.ProcessedAtUtc = now;
                    continue;
                }

                if (rankingEvent.PlayerAccountId is null)
                {
                    var playerId = await EnsureTelemetryPlayerAccountAsync(rankingEvent.PlayerEmail, cancellationToken);
                    if (playerId is null)
                    {
                        rankingEvent.Status = RankingEventStatus.Rejected;
                        rankingEvent.ModerationReason = "Player account not found for event email.";
                        rankingEvent.ProcessedAtUtc = now;
                        continue;
                    }

                    rankingEvent.PlayerAccountId = playerId;
                }

                foreach (var definition in matchingDefinitions)
                {
                    if (definition.RequiresModeration && rankingEvent.Status != RankingEventStatus.Approved)
                    {
                        rankingEvent.Status = RankingEventStatus.PendingModeration;
                        continue;
                    }

                    var uniquenessKey = BuildUniquenessKey(definition, rankingEvent);
                    if (awardedUniquenessKeysInRun.Contains(uniquenessKey))
                    {
                        continue;
                    }

                    var alreadyExists = await db.MasterRankingRewardRecords
                        .AsNoTracking()
                        .AnyAsync(record => record.UniquenessKey == uniquenessKey, cancellationToken);
                    if (alreadyExists)
                    {
                        continue;
                    }

                    var reward = new MasterRankingRewardRecord
                    {
                        Id = Guid.NewGuid(),
                        PlayerAccountId = rankingEvent.PlayerAccountId.Value,
                        BountyDefinitionId = definition.Id,
                        RankingEventId = rankingEvent.Id,
                        PointsAwarded = definition.RewardPoints,
                        Status = RankingRewardStatus.Awarded,
                        UniquenessKey = uniquenessKey,
                        ServerKey = rankingEvent.ServerKey,
                        EventDateUtc = rankingEvent.OccurredAtUtc.Date,
                        AwardedAtUtc = now,
                        AwardMetadataJson = rankingEvent.PayloadJson,
                    };

                    db.MasterRankingRewardRecords.Add(reward);
                    awardedUniquenessKeysInRun.Add(uniquenessKey);
                    run.RewardRecordsCreated += 1;
                    run.TotalPointsAwarded += definition.RewardPoints;

                    if (!playerSnapshots.TryGetValue(reward.PlayerAccountId, out var snapshot))
                    {
                        snapshot = new MasterRankingPlayerSnapshot
                        {
                            Id = Guid.NewGuid(),
                            PlayerAccountId = reward.PlayerAccountId,
                            TotalPoints = 0m,
                            UpdatedAtUtc = now,
                        };
                        db.MasterRankingPlayerSnapshots.Add(snapshot);
                        playerSnapshots.Add(snapshot.PlayerAccountId, snapshot);
                    }

                    snapshot.TotalPoints += definition.RewardPoints;
                    snapshot.UpdatedAtUtc = now;
                }

                if (rankingEvent.Status != RankingEventStatus.PendingModeration)
                {
                    rankingEvent.Status = RankingEventStatus.Processed;
                    rankingEvent.ProcessedAtUtc = now;
                }

                run.ProcessedEvents += 1;
            }

            await AwardLeaderboardBountiesAsync(definitions, now, cancellationToken);
            await RefreshLeaderboardRanksAsync(now, cancellationToken);

            run.FinishedAtUtc = DateTime.UtcNow;
            run.Notes = BuildEvaluatorNotes(run);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Master ranking hourly evaluator finished: events={Events}, rewards={Rewards}, points={Points}",
                run.ProcessedEvents,
                run.RewardRecordsCreated,
                run.TotalPointsAwarded);

            return run;
        }
        catch (Exception ex)
        {
            run.Status = RankingRunStatus.Failed;
            run.FinishedAtUtc = DateTime.UtcNow;
            run.Notes = $"Hourly evaluator failed: {ex.Message}";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogError(ex, "Master ranking hourly evaluator failed.");
            throw;
        }
    }

    public async Task<MasterRankingEvaluationRun> ApplyDailyDecayAsync(CancellationToken cancellationToken = default)
    {
        var startedAtUtc = DateTime.UtcNow;
        var run = new MasterRankingEvaluationRun
        {
            Id = Guid.NewGuid(),
            RunType = RankingRunType.DailyDecay,
            Status = RankingRunStatus.Succeeded,
            StartedAtUtc = startedAtUtc,
            FinishedAtUtc = startedAtUtc,
            Notes = string.Empty,
        };

        db.MasterRankingEvaluationRuns.Add(run);

        try
        {
            var snapshots = await db.MasterRankingPlayerSnapshots.ToListAsync(cancellationToken);
            run.TotalPointsBeforeDecay = snapshots.Sum(snapshot => snapshot.TotalPoints);

            var now = DateTime.UtcNow;
            foreach (var snapshot in snapshots)
            {
                snapshot.TotalPoints = decimal.Round(snapshot.TotalPoints * DailyDecayFactor, 4, MidpointRounding.ToEven);
                snapshot.LastDailyDecayFactorApplied = DailyDecayFactor;
                snapshot.UpdatedAtUtc = now;
            }

            run.TotalPointsAfterDecay = snapshots.Sum(snapshot => snapshot.TotalPoints);
            await RefreshLeaderboardRanksAsync(now, cancellationToken);

            run.FinishedAtUtc = DateTime.UtcNow;
            run.Notes = $"Daily decay applied with factor {DailyDecayFactor:0.00}.";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Master ranking daily decay finished: totalBefore={Before}, totalAfter={After}",
                run.TotalPointsBeforeDecay,
                run.TotalPointsAfterDecay);

            return run;
        }
        catch (Exception ex)
        {
            run.Status = RankingRunStatus.Failed;
            run.FinishedAtUtc = DateTime.UtcNow;
            run.Notes = $"Daily decay failed: {ex.Message}";
            await db.SaveChangesAsync(cancellationToken);

            logger.LogError(ex, "Master ranking daily decay failed.");
            throw;
        }
    }
}
