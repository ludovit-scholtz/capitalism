using System.Text.Json;
using Capitalism.Shared.Security;
using HotChocolate;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MasterApi.Utilities;

public sealed class MasterRankingService(MasterDbContext db, ILogger<MasterRankingService> logger)
{
    private const decimal DailyDecayFactor = 0.99m;
    private const string ShadowPasswordHashPrefix = "__SHADOW__:";

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

    public async Task<MasterRankingEvent> IngestEventAsync(
        string eventType,
        string playerEmail,
        string? serverKey,
        string? externalEventId,
        string? uniqueScopeKey,
        string? idempotencyKey,
        string? proofReference,
        string payloadJson,
        DateTime occurredAtUtc,
        RankingTelemetryValidationResult? telemetryValidation = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = playerEmail.Trim().ToLowerInvariant();
        var playerId = await EnsureTelemetryPlayerAccountAsync(normalizedEmail, cancellationToken);
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        var normalizedServerKey = string.IsNullOrWhiteSpace(serverKey) ? null : serverKey.Trim();
        var normalizedProofReference = string.IsNullOrWhiteSpace(proofReference) ? null : proofReference.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedProofReference))
        {
            var duplicateProofEvent = await db.MasterRankingEvents
                .AsNoTracking()
                .FirstOrDefaultAsync(entry => entry.ProofReference == normalizedProofReference, cancellationToken);
            if (duplicateProofEvent is not null)
            {
                logger.LogWarning(
                    "Ranking proof reference duplicate detected. proofReference={ProofReference}, existingEventId={ExistingEventId}",
                    normalizedProofReference,
                    duplicateProofEvent.Id);
                throw BuildProofReferenceConflictError();
            }
        }

        var rankingEvent = new MasterRankingEvent
        {
            Id = Guid.NewGuid(),
            PlayerAccountId = playerId,
            PlayerEmail = normalizedEmail,
            EventType = eventType,
            ServerKey = normalizedServerKey,
            ServerKeyHash = telemetryValidation?.ServerKeyHash,
            ExternalEventId = string.IsNullOrWhiteSpace(externalEventId) ? null : externalEventId.Trim(),
            UniqueScopeKey = string.IsNullOrWhiteSpace(uniqueScopeKey) ? null : uniqueScopeKey.Trim(),
            IdempotencyKey = normalizedIdempotencyKey,
            TelemetryNonce = telemetryValidation?.TelemetryNonce,
            PayloadHash = telemetryValidation?.PayloadHash,
            TelemetrySignatureHash = telemetryValidation?.SignatureHash,
            TelemetryBatchId = telemetryValidation?.BatchId,
            ProofReference = normalizedProofReference,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            Status = telemetryValidation?.IsSuspicious == true
                ? RankingEventStatus.PendingModeration
                : RankingEventStatus.Pending,
            IsQuarantined = telemetryValidation?.IsSuspicious == true,
            QuarantineReason = telemetryValidation?.QuarantineReason,
            QuarantinedByEmail = telemetryValidation?.IsSuspicious == true ? "telemetry-system" : null,
            QuarantinedAtUtc = telemetryValidation?.IsSuspicious == true ? DateTime.UtcNow : null,
            OccurredAtUtc = occurredAtUtc,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.MasterRankingEvents.Add(rankingEvent);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsProofReferenceUniqueConstraintViolation(ex))
        {
            logger.LogWarning(
                ex,
                "Ranking proof reference duplicate detected during persistence. proofReference={ProofReference}",
                normalizedProofReference);
            throw BuildProofReferenceConflictError();
        }

        return rankingEvent;
    }

    public async Task<MasterRankingEvent?> FindIdempotentEventAsync(
        string eventType,
        string playerEmail,
        string? serverKey,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        if (normalizedIdempotencyKey is null)
        {
            return null;
        }

        var normalizedEmail = playerEmail.Trim().ToLowerInvariant();
        var normalizedEventType = eventType.Trim().ToUpperInvariant();
        var normalizedServerKey = string.IsNullOrWhiteSpace(serverKey) ? null : serverKey.Trim();

        return await db.MasterRankingEvents
            .AsNoTracking()
            .Where(entry =>
                entry.IdempotencyKey == normalizedIdempotencyKey
                && entry.PlayerEmail == normalizedEmail
                && entry.EventType == normalizedEventType
                && entry.ServerKey == normalizedServerKey)
            .OrderBy(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

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

    private static string BuildUniquenessKey(MasterRankingBountyDefinition definition, MasterRankingEvent rankingEvent)
    {
        var dateKey = rankingEvent.OccurredAtUtc.Date.ToString("yyyyMMdd");
        var playerKey = rankingEvent.PlayerAccountId?.ToString() ?? rankingEvent.PlayerEmail;
        return definition.CooldownMode switch
        {
            RankingCooldownMode.None => $"{definition.Code}:{rankingEvent.Id}",
            RankingCooldownMode.UtcDay => $"{definition.Code}:{playerKey}:{dateKey}",
            RankingCooldownMode.UtcDayPerServer => $"{definition.Code}:{playerKey}:{rankingEvent.ServerKey ?? "global"}:{dateKey}",
            RankingCooldownMode.Once => $"{definition.Code}:{playerKey}:once",
            RankingCooldownMode.PerUniqueKey => $"{definition.Code}:{playerKey}:{rankingEvent.UniqueScopeKey ?? rankingEvent.ExternalEventId ?? rankingEvent.Id.ToString()}",
            _ => $"{definition.Code}:{playerKey}:{dateKey}:{rankingEvent.Id}",
        };
    }

    private async Task<Guid?> EnsureTelemetryPlayerAccountAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        var existingPlayerId = await db.PlayerAccounts
            .AsNoTracking()
            .Where(player => player.Email == normalizedEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (existingPlayerId.HasValue)
        {
            return existingPlayerId.Value;
        }

        var now = DateTime.UtcNow;
        var fallbackDisplayName = PlayerDisplayNameProvisioning.ResolveDisplayName(
            claimedDisplayName: null,
            normalizedEmail: normalizedEmail,
            subjectClaim: null);

        var shadowAccount = new PlayerAccount
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = fallbackDisplayName,
            PasswordHash = BuildShadowPasswordHash(normalizedEmail),
            CreatedAtUtc = now,
        };

        db.PlayerAccounts.Add(shadowAccount);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return shadowAccount.Id;
        }
        catch (DbUpdateException)
        {
            db.Entry(shadowAccount).State = EntityState.Detached;
        }

        return await db.PlayerAccounts
            .Where(player => player.Email == normalizedEmail)
            .Select(player => (Guid?)player.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string BuildEvaluatorNotes(MasterRankingEvaluationRun run)
    {
        if (run.RewardRecordsCreated > 5000)
        {
            return "Abnormal reward spike detected: created more than 5000 reward records in a single run.";
        }

        return "Hourly ranking evaluation completed successfully.";
    }

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        var normalized = idempotencyKey?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static GraphQLException BuildProofReferenceConflictError()
    {
        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage("Proof reference already used.")
                .SetCode("PROOF_REFERENCE_CONFLICT")
                .SetExtension("httpStatus", 409)
                .Build());
    }

    private static bool IsProofReferenceUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException postgres)
        {
            return false;
        }

        return postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgres.ConstraintName, "IX_MasterRankingEvents_ProofReference", StringComparison.Ordinal);
    }
}
