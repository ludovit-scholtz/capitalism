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

    public async Task<MasterRankingEvent?> FindReplayEventAsync(
        string eventType,
        string playerEmail,
        string? serverKey,
        string? externalEventId,
        string? uniqueScopeKey,
        string? idempotencyKey,
        string payloadJson,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdempotencyKey = NormalizeIdempotencyKey(idempotencyKey);
        if (normalizedIdempotencyKey is null)
        {
            return null;
        }

        var idempotentEvent = await FindIdempotentEventAsync(
            eventType,
            playerEmail,
            serverKey,
            normalizedIdempotencyKey,
            cancellationToken);
        if (idempotentEvent is not null)
        {
            return idempotentEvent;
        }

        if (string.IsNullOrWhiteSpace(serverKey))
        {
            return null;
        }

        var normalizedEmail = playerEmail.Trim().ToLowerInvariant();
        var normalizedEventType = eventType.Trim().ToUpperInvariant();
        var normalizedServerKey = serverKey.Trim();
        var signatureHash = RankingTelemetryValidator.ComputeSignatureHash(
            normalizedServerKey,
            externalEventId,
            uniqueScopeKey,
            payloadJson);

        return await db.MasterRankingEvents
            .AsNoTracking()
            .Where(entry =>
                entry.PlayerEmail == normalizedEmail
                && entry.EventType == normalizedEventType
                && entry.ServerKey == normalizedServerKey
                && entry.TelemetrySignatureHash == signatureHash)
            .OrderBy(entry => entry.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
