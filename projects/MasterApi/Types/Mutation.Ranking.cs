using System.Security.Claims;
using System.Text.Json;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Utilities;
using Microsoft.EntityFrameworkCore;
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
        var telemetryValidation = await telemetryValidator.ValidateAndTrackAsync(
            input.ServerKey,
            eventType,
            playerEmail,
            input.ExternalEventId,
            input.UniqueScopeKey,
            input.PayloadJson,
            CancellationToken.None);

        var rankingEvent = await rankingService.IngestEventAsync(
            eventType,
            playerEmail,
            input.ServerKey,
            input.ExternalEventId,
            input.UniqueScopeKey,
            input.ProofReference,
            input.PayloadJson,
            occurredAtUtc,
            telemetryValidation,
            CancellationToken.None);

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

        var rankingEvent = await rankingService.IngestEventAsync(
            definition.SourceEventType,
            player.Email,
            null,
            null,
            uniqueScopeKey,
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

        rankingEvent.Status = input.Approve ? RankingEventStatus.Approved : RankingEventStatus.Rejected;
        rankingEvent.ModeratedByEmail = callerEmail;
        rankingEvent.ModeratedAtUtc = DateTime.UtcNow;
        rankingEvent.ModerationReason = string.IsNullOrWhiteSpace(input.Reason)
            ? (input.Approve ? "Approved by administrator." : "Rejected by administrator.")
            : input.Reason.Trim();

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

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingBountyDefinitionInfo> UpsertRankingBountyDefinition(
        UpsertRankingBountyDefinitionInput input,
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
                    .SetMessage("Ranking bounty configuration requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var code = input.Code.Trim().ToUpperInvariant();
        if (!MasterRankingBountyCodes.All.Contains(code))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Unknown bounty code.")
                    .SetCode("UNKNOWN_BOUNTY_CODE")
                    .Build());
        }

        if (!RankingCooldownMode.All.Contains(input.CooldownMode))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid cooldown mode.")
                    .SetCode("INVALID_COOLDOWN_MODE")
                    .Build());
        }

        if (!RankingProofRequirement.All.Contains(input.ProofRequirement))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid proof requirement.")
                    .SetCode("INVALID_PROOF_REQUIREMENT")
                    .Build());
        }

        if (!RankingVisibilityScope.All.Contains(input.VisibilityScope))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Invalid visibility scope.")
                    .SetCode("INVALID_VISIBILITY_SCOPE")
                    .Build());
        }

        var now = DateTime.UtcNow;
        var existing = input.Id.HasValue
            ? await db.MasterRankingBountyDefinitions.FirstOrDefaultAsync(definition => definition.Id == input.Id.Value)
            : await db.MasterRankingBountyDefinitions.FirstOrDefaultAsync(definition => definition.Code == code);

        var previousValueJson = existing is null
            ? "{}"
            : JsonSerializer.Serialize(existing);

        if (existing is null)
        {
            existing = new MasterRankingBountyDefinition
            {
                Id = Guid.NewGuid(),
                Code = code,
                CreatedAtUtc = now,
            };
            db.MasterRankingBountyDefinitions.Add(existing);
        }

        existing.DisplayName = input.DisplayName.Trim();
        existing.Description = input.Description.Trim();
        existing.RewardPoints = input.RewardPoints;
        existing.IsEnabled = input.IsEnabled;
        existing.IsVisibleToPlayers = input.IsVisibleToPlayers;
        existing.RequiresModeration = input.RequiresModeration;
        existing.CooldownMode = input.CooldownMode;
        existing.SourceEventType = input.SourceEventType.Trim().ToUpperInvariant();
        existing.ProofRequirement = input.ProofRequirement;
        existing.VisibilityScope = input.VisibilityScope;
        existing.ValidationSettingsJson = string.IsNullOrWhiteSpace(input.ValidationSettingsJson) ? "{}" : input.ValidationSettingsJson;
        existing.UpdatedAtUtc = now;

        db.MasterRankingBountyAudits.Add(new MasterRankingBountyAudit
        {
            Id = Guid.NewGuid(),
            BountyDefinitionId = existing.Id,
            ChangedByEmail = callerEmail,
            ChangeType = input.Id.HasValue ? "UPDATED" : "CREATED",
            PreviousValueJson = previousValueJson,
            NewValueJson = JsonSerializer.Serialize(existing),
            CreatedAtUtc = now,
        });

        await db.SaveChangesAsync();

        return new RankingBountyDefinitionInfo
        {
            Id = existing.Id,
            Code = existing.Code,
            DisplayName = existing.DisplayName,
            Description = existing.Description,
            RewardPoints = existing.RewardPoints,
            IsEnabled = existing.IsEnabled,
            IsVisibleToPlayers = existing.IsVisibleToPlayers,
            RequiresModeration = existing.RequiresModeration,
            CooldownMode = existing.CooldownMode,
            SourceEventType = existing.SourceEventType,
            ProofRequirement = existing.ProofRequirement,
            VisibilityScope = existing.VisibilityScope,
            ValidationSettingsJson = existing.ValidationSettingsJson,
            UpdatedAtUtc = existing.UpdatedAtUtc,
        };
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingRunInfo> RunRankingEvaluationNow(
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
                    .SetMessage("Ranking evaluator execution requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var run = await rankingService.EvaluateHourlyAsync();
        return ToRankingRunInfo(run);
    }

    [HotChocolate.Authorization.Authorize]
    public async Task<RankingRunInfo> RunRankingDailyDecayNow(
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
                    .SetMessage("Ranking decay execution requires global admin access.")
                    .SetCode("GLOBAL_ADMIN_REQUIRED")
                    .Build());
        }

        var run = await rankingService.ApplyDailyDecayAsync();
        return ToRankingRunInfo(run);
    }

    private static RankingRunInfo ToRankingRunInfo(MasterRankingEvaluationRun run)
    {
        return new RankingRunInfo
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
        };
    }
}
