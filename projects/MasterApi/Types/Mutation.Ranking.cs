using System.Security.Claims;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace MasterApi.Types;

public sealed partial class Mutation
{

    public async Task<RankingEventModerationItem> IngestRankingEvent(
        IngestRankingEventInput input,
        [Service] MasterDbContext db,
        [Service] MasterRankingService rankingService,
        [Service] RankingTelemetryValidator telemetryValidator,
        [Service] IOptions<MasterServerOptions> masterServerOptions)
    {
        Query.EnsureServiceAccess(input, masterServerOptions, requireRegistrationKey: true, requireServerKey: true);

        var eventType = input.EventType.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Event type is required.")
                    .SetCode("EVENT_TYPE_REQUIRED")
                    .Build());
        }

        var playerEmail = Query.NormalizeEmail(input.PlayerEmail, "INVALID_PLAYER_EMAIL");
        var occurredAtUtc = input.OccurredAtUtc == default ? DateTime.UtcNow : input.OccurredAtUtc;
        var replayedEvent = await rankingService.FindReplayEventAsync(
            eventType,
            playerEmail,
            input.ServerKey,
            input.ExternalEventId,
            input.UniqueScopeKey,
            input.IdempotencyKey,
            input.PayloadJson,
            CancellationToken.None);
        if (replayedEvent is not null)
        {
            return ToModerationItem(replayedEvent);
        }

        await using IDbContextTransaction? tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync()
            : null;

        try
        {
            var telemetryValidation = await telemetryValidator.ValidateAndTrackAsync(
                input.ServerKey,
                eventType,
                playerEmail,
                input.ExternalEventId,
                input.UniqueScopeKey,
                input.PayloadJson,
                occurredAtUtc,
                CancellationToken.None);

            var rankingEvent = await rankingService.IngestEventAsync(
                eventType,
                playerEmail,
                input.ServerKey,
                input.ExternalEventId,
                input.UniqueScopeKey,
                input.IdempotencyKey,
                input.ProofReference,
                input.PayloadJson,
                occurredAtUtc,
                telemetryValidation,
                CancellationToken.None);

            if (tx is not null)
            {
                await tx.CommitAsync();
            }

            return ToModerationItem(rankingEvent);
        }
        catch (GraphQLException ex) when (RankingTelemetryValidator.IsDuplicateSignatureError(ex))
        {
            if (tx is not null)
            {
                await tx.RollbackAsync();
            }

            db.ChangeTracker.Clear();

            replayedEvent = await rankingService.FindReplayEventAsync(
                eventType,
                playerEmail,
                input.ServerKey,
                input.ExternalEventId,
                input.UniqueScopeKey,
                input.IdempotencyKey,
                input.PayloadJson,
                CancellationToken.None);
            if (replayedEvent is not null)
            {
                return ToModerationItem(replayedEvent);
            }

            if (tx is not null)
            {
                await telemetryValidator.RecordDuplicateSignatureAuditAsync(
                    input.ServerKey,
                    eventType,
                    playerEmail,
                    input.ExternalEventId,
                    input.UniqueScopeKey,
                    input.PayloadJson,
                    CancellationToken.None);
            }

            throw;
        }
        catch (GraphQLException ex) when (RankingTelemetryValidator.IsShardValidationAuditOnlyError(ex))
        {
            if (tx is not null)
            {
                await tx.CommitAsync();
            }

            throw;
        }
        catch
        {
            if (tx is not null)
            {
                await tx.RollbackAsync();
            }

            throw;
        }
    }

    private static RankingEventModerationItem ToModerationItem(MasterRankingEvent rankingEvent)
    {
        return new RankingEventModerationItem
        {
            Id = rankingEvent.Id,
            EventType = rankingEvent.EventType,
            PlayerEmail = rankingEvent.PlayerEmail,
            ServerKey = rankingEvent.ServerKey,
            ProofReference = rankingEvent.ProofReference,
            PayloadJson = rankingEvent.PayloadJson,
            Status = rankingEvent.Status,
            OccurredAtUtc = rankingEvent.OccurredAtUtc,
            CreatedAtUtc = rankingEvent.CreatedAtUtc,
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingTelemetryBatchInfo> QuarantineTelemetryBatch(
        Guid batchId,
        string reason,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions,
        [Service] MasterRankingService rankingService)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking telemetry quarantine requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var trimmedReason = reason.Trim();
        if (string.IsNullOrWhiteSpace(trimmedReason))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Quarantine reason is required.")
                    .SetCode("QUARANTINE_REASON_REQUIRED")
                    .Build());
        }

        var now = DateTime.UtcNow;
        var events = await db.MasterRankingEvents
            .Where(entry => entry.TelemetryBatchId == batchId)
            .ToListAsync();
        if (events.Count == 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Telemetry batch not found.")
                    .SetCode("TELEMETRY_BATCH_NOT_FOUND")
                    .Build());
        }

        foreach (var entry in events)
        {
            entry.IsQuarantined = true;
            entry.QuarantineReason = trimmedReason;
            entry.QuarantinedByEmail = callerEmail;
            entry.QuarantinedAtUtc = now;
            entry.Status = RankingEventStatus.Rejected;
            entry.ProcessedAtUtc = now;
        }

        var auditRows = await db.RankingTelemetryAuditLogs
            .Where(item => item.BatchId == batchId)
            .ToListAsync();
        foreach (var audit in auditRows)
        {
            audit.IsQuarantined = true;
            audit.QuarantineReason = trimmedReason;
            audit.QuarantineUpdatedAtUtc = now;
            audit.QuarantineUpdatedByEmail = callerEmail;
            audit.ClearJustification = null;
        }

        var eventIds = events.Select(item => item.Id).ToList();
        var rewardRows = await db.MasterRankingRewardRecords
            .Where(item => item.RankingEventId.HasValue && eventIds.Contains(item.RankingEventId.Value))
            .ToListAsync();
        if (rewardRows.Count > 0)
        {
            db.MasterRankingRewardRecords.RemoveRange(rewardRows);
        }

        await db.SaveChangesAsync();
        await rankingService.RebuildSnapshotsFromRewardsAsync();

        return await Query.BuildTelemetryBatchInfoAsync(db, batchId, includeOnlyQuarantined: false);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingTelemetryBatchInfo> ClearQuarantine(
        Guid batchId,
        string justification,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Clearing quarantine requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var trimmedJustification = justification.Trim();
        if (string.IsNullOrWhiteSpace(trimmedJustification))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A justification note is required.")
                    .SetCode("CLEAR_QUARANTINE_JUSTIFICATION_REQUIRED")
                    .Build());
        }

        var now = DateTime.UtcNow;
        var events = await db.MasterRankingEvents
            .Where(entry => entry.TelemetryBatchId == batchId)
            .ToListAsync();
        if (events.Count == 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Telemetry batch not found.")
                    .SetCode("TELEMETRY_BATCH_NOT_FOUND")
                    .Build());
        }

        foreach (var entry in events)
        {
            entry.IsQuarantined = false;
            entry.QuarantineClearJustification = trimmedJustification;
            entry.QuarantineClearedByEmail = callerEmail;
            entry.QuarantineClearedAtUtc = now;
            entry.Status = RankingEventStatus.Pending;
            entry.ProcessedAtUtc = null;
        }

        var auditRows = await db.RankingTelemetryAuditLogs
            .Where(item => item.BatchId == batchId)
            .ToListAsync();
        foreach (var audit in auditRows)
        {
            audit.IsQuarantined = false;
            audit.QuarantineUpdatedAtUtc = now;
            audit.QuarantineUpdatedByEmail = callerEmail;
            audit.ClearJustification = trimmedJustification;
        }

        await db.SaveChangesAsync();
        return await Query.BuildTelemetryBatchInfoAsync(db, batchId, includeOnlyQuarantined: false);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingEventModerationItem> SubmitRankingProofEvent(
        string bountyCode,
        string proofReference,
        string? uniqueScopeKey,
        string? idempotencyKey,
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

        var normalizedBountyCode = bountyCode.Trim().ToUpperInvariant();
        var definition = await db.MasterRankingBountyDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Code == normalizedBountyCode);
        if (definition is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Unknown bounty code.")
                    .SetCode("UNKNOWN_BOUNTY_CODE")
                    .Build());
        }

        var existingIdempotentEvent = await rankingService.FindIdempotentEventAsync(
            definition.SourceEventType,
            player.Email,
            null,
            idempotencyKey,
            CancellationToken.None);
        if (existingIdempotentEvent is not null)
        {
            return new RankingEventModerationItem
            {
                Id = existingIdempotentEvent.Id,
                EventType = existingIdempotentEvent.EventType,
                PlayerEmail = existingIdempotentEvent.PlayerEmail,
                ServerKey = existingIdempotentEvent.ServerKey,
                ProofReference = null,
                PayloadJson = "{}",
                Status = existingIdempotentEvent.Status,
                OccurredAtUtc = existingIdempotentEvent.OccurredAtUtc,
                CreatedAtUtc = existingIdempotentEvent.CreatedAtUtc,
            };
        }

        var rankingEvent = await rankingService.IngestEventAsync(
            definition.SourceEventType,
            player.Email,
            null,
            null,
            uniqueScopeKey,
            idempotencyKey,
            proofReference,
            JsonSerializer.Serialize(new { bountyCode = normalizedBountyCode }),
            DateTime.UtcNow,
            cancellationToken: CancellationToken.None);

        if (definition.RequiresModeration)
        {
            rankingEvent.Status = RankingEventStatus.PendingModeration;
            await db.SaveChangesAsync();
        }

        return new RankingEventModerationItem
        {
            Id = rankingEvent.Id,
            EventType = rankingEvent.EventType,
            PlayerEmail = rankingEvent.PlayerEmail,
            ServerKey = rankingEvent.ServerKey,
            ProofReference = null,
            PayloadJson = "{}",
            Status = rankingEvent.Status,
            OccurredAtUtc = rankingEvent.OccurredAtUtc,
            CreatedAtUtc = rankingEvent.CreatedAtUtc,
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingEventModerationItem> ModerateRankingEvent(
        ModerateRankingEventInput input,
        ClaimsPrincipal claimsPrincipal,
        [Service] MasterDbContext db,
        [Service] IOptions<GameAdministrationOptions> gameAdministrationOptions)
    {
        var callerEmail = Query.GetEmailFromClaims(claimsPrincipal);
        var access = await Query.BuildGameAdministrationAccessAsync(db, gameAdministrationOptions.Value, callerEmail);
        if (!access.CanAccessEveryGameDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking moderation requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var rankingEvent = await db.MasterRankingEvents
            .FirstOrDefaultAsync(entry => entry.Id == input.EventId)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Ranking event not found.")
                    .SetCode("RANKING_EVENT_NOT_FOUND")
                    .Build());

        var now = DateTime.UtcNow;
        rankingEvent.Status = input.Approve ? RankingEventStatus.Approved : RankingEventStatus.Rejected;
        rankingEvent.ModeratedByEmail = callerEmail;
        rankingEvent.ModeratedAtUtc = now;
        rankingEvent.ModerationReason = string.IsNullOrWhiteSpace(input.Reason)
            ? (input.Approve ? "Approved by administrator." : "Rejected by administrator.")
            : input.Reason.Trim();
        if (input.Approve)
        {
            rankingEvent.IsQuarantined = false;
            rankingEvent.QuarantineClearedByEmail = callerEmail;
            rankingEvent.QuarantineClearedAtUtc = now;
            rankingEvent.QuarantineClearJustification = rankingEvent.ModerationReason;
            rankingEvent.QuarantineReason = null;
        }
        else
        {
            rankingEvent.IsQuarantined = true;
            rankingEvent.QuarantinedByEmail = callerEmail;
            rankingEvent.QuarantinedAtUtc = now;
            rankingEvent.QuarantineReason = rankingEvent.ModerationReason;
        }

        if (rankingEvent.TelemetryBatchId.HasValue)
        {
            var auditRows = await db.RankingTelemetryAuditLogs
                .Where(item => item.BatchId == rankingEvent.TelemetryBatchId.Value)
                .ToListAsync();
            foreach (var audit in auditRows)
            {
                audit.IsQuarantined = !input.Approve;
                audit.QuarantineUpdatedAtUtc = now;
                audit.QuarantineUpdatedByEmail = callerEmail;
                audit.QuarantineReason = input.Approve ? null : rankingEvent.ModerationReason;
                audit.ClearJustification = input.Approve ? rankingEvent.ModerationReason : null;
            }
        }

        await db.SaveChangesAsync();

        return new RankingEventModerationItem
        {
            Id = rankingEvent.Id,
            EventType = rankingEvent.EventType,
            PlayerEmail = rankingEvent.PlayerEmail,
            ServerKey = rankingEvent.ServerKey,
            ProofReference = rankingEvent.ProofReference,
            PayloadJson = rankingEvent.PayloadJson,
            Status = rankingEvent.Status,
            OccurredAtUtc = rankingEvent.OccurredAtUtc,
            CreatedAtUtc = rankingEvent.CreatedAtUtc,
        };
    }
}
