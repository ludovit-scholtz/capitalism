using System.Text.Json;
using Capitalism.Shared.Security;
using HotChocolate;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MasterApi.Utilities;

public sealed partial class MasterRankingService
{
    public async Task RebuildSnapshotsFromRewardsAsync(CancellationToken cancellationToken = default)
    {
        var rewardTotalsByPlayer = await db.MasterRankingRewardRecords
            .AsNoTracking()
            .GroupBy(record => record.PlayerAccountId)
            .Select(group => new { PlayerAccountId = group.Key, TotalPoints = group.Sum(item => item.PointsAwarded) })
            .ToListAsync(cancellationToken);

        var snapshots = await db.MasterRankingPlayerSnapshots.ToListAsync(cancellationToken);
        var snapshotsByPlayerId = snapshots.ToDictionary(snapshot => snapshot.PlayerAccountId);
        var now = DateTime.UtcNow;

        foreach (var rewardTotal in rewardTotalsByPlayer)
        {
            if (!snapshotsByPlayerId.TryGetValue(rewardTotal.PlayerAccountId, out var snapshot))
            {
                snapshot = new MasterRankingPlayerSnapshot
                {
                    Id = Guid.NewGuid(),
                    PlayerAccountId = rewardTotal.PlayerAccountId,
                };
                db.MasterRankingPlayerSnapshots.Add(snapshot);
                snapshotsByPlayerId.Add(snapshot.PlayerAccountId, snapshot);
            }

            snapshot.TotalPoints = rewardTotal.TotalPoints;
            snapshot.UpdatedAtUtc = now;
        }

        var activePlayerIds = rewardTotalsByPlayer.Select(item => item.PlayerAccountId).ToHashSet();
        var staleSnapshots = snapshots.Where(snapshot => !activePlayerIds.Contains(snapshot.PlayerAccountId)).ToList();
        if (staleSnapshots.Count > 0)
        {
            db.MasterRankingPlayerSnapshots.RemoveRange(staleSnapshots);
        }

        await db.SaveChangesAsync(cancellationToken);

        var ordered = await db.MasterRankingPlayerSnapshots
            .OrderByDescending(snapshot => snapshot.TotalPoints)
            .ThenBy(snapshot => snapshot.PlayerAccountId)
            .ToListAsync(cancellationToken);
        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].PreviousGlobalRank = ordered[index].GlobalRank;
            ordered[index].GlobalRank = index + 1;
            ordered[index].UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task AwardLeaderboardBountiesAsync(
        List<MasterRankingBountyDefinition> definitions,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var topPlayerDefinition = definitions.FirstOrDefault(definition => definition.Code == MasterRankingBountyCodes.TopPlayer && definition.IsEnabled);
        var greatPlayerDefinition = definitions.FirstOrDefault(definition => definition.Code == MasterRankingBountyCodes.GreatPlayer && definition.IsEnabled);
        if (topPlayerDefinition is null && greatPlayerDefinition is null)
        {
            return;
        }

        var snapshots = await db.MasterRankingPlayerSnapshots
            .OrderByDescending(snapshot => snapshot.TotalPoints)
            .ThenBy(snapshot => snapshot.PlayerAccountId)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < snapshots.Count; index++)
        {
            var rank = index + 1;
            if (topPlayerDefinition is not null && rank <= 10)
            {
                await TryCreateSyntheticRewardAsync(topPlayerDefinition, snapshots[index], rank, now, cancellationToken);
            }

            if (greatPlayerDefinition is not null && rank <= 100)
            {
                await TryCreateSyntheticRewardAsync(greatPlayerDefinition, snapshots[index], rank, now, cancellationToken);
            }
        }
    }

    private async Task TryCreateSyntheticRewardAsync(
        MasterRankingBountyDefinition definition,
        MasterRankingPlayerSnapshot snapshot,
        int rank,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var syntheticEvent = new MasterRankingEvent
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = snapshot.PlayerAccountId,
            PlayerEmail = string.Empty,
            EventType = definition.SourceEventType,
            Status = RankingEventStatus.Processed,
            OccurredAtUtc = now,
            CreatedAtUtc = now,
            ProcessedAtUtc = now,
            PayloadJson = JsonSerializer.Serialize(new { rank }),
            UniqueScopeKey = $"rank:{rank}",
        };

        var uniquenessKey = BuildUniquenessKey(definition, syntheticEvent);
        var exists = await db.MasterRankingRewardRecords
            .AsNoTracking()
            .AnyAsync(record => record.UniquenessKey == uniquenessKey, cancellationToken);
        if (exists)
        {
            return;
        }

        db.MasterRankingEvents.Add(syntheticEvent);
        db.MasterRankingRewardRecords.Add(new MasterRankingRewardRecord
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = snapshot.PlayerAccountId,
            BountyDefinitionId = definition.Id,
            RankingEventId = syntheticEvent.Id,
            PointsAwarded = definition.RewardPoints,
            Status = RankingRewardStatus.Awarded,
            UniquenessKey = uniquenessKey,
            EventDateUtc = now.Date,
            AwardedAtUtc = now,
            AwardMetadataJson = syntheticEvent.PayloadJson,
        });

        snapshot.TotalPoints += definition.RewardPoints;
        snapshot.UpdatedAtUtc = now;
    }

    private async Task RefreshLeaderboardRanksAsync(DateTime now, CancellationToken cancellationToken)
    {
        var ordered = await db.MasterRankingPlayerSnapshots
            .OrderByDescending(snapshot => snapshot.TotalPoints)
            .ThenBy(snapshot => snapshot.PlayerAccountId)
            .ToListAsync(cancellationToken);

        for (var index = 0; index < ordered.Count; index++)
        {
            var nextRank = index + 1;
            ordered[index].PreviousGlobalRank = ordered[index].GlobalRank;
            ordered[index].GlobalRank = nextRank;
            ordered[index].UpdatedAtUtc = now;
        }
    }
}
