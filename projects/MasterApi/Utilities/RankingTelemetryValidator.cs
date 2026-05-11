using System.Text.Json;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Utilities;

public sealed class RankingTelemetryValidationResult
{
    public required Guid BatchId { get; init; }

    public required string ServerKeyHash { get; init; }

    public required string TelemetryNonce { get; init; }

    public required string PayloadHash { get; init; }

    public required string SignatureHash { get; init; }

    public bool IsSuspicious { get; init; }

    public string AuditReasonCode { get; init; } = RankingTelemetryAuditReason.Accepted;

    public string? QuarantineReason { get; init; }
}

public sealed class RankingTelemetryValidator(MasterDbContext db)
{
    private static readonly TimeSpan SignatureTtl = TimeSpan.FromHours(24);
    private const int BurstThresholdPerMinute = 8;
    private const decimal NetWorthRegressionToleranceUsd = 0.01m;

    public async Task<RankingTelemetryValidationResult> ValidateAndTrackAsync(
        string serverKey,
        string eventType,
        string playerEmail,
        string? externalEventId,
        string? uniqueScopeKey,
        string payloadJson,
        DateTime occurredAtUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedServerKey = serverKey.Trim();
        var serverKeyHash = ShardKeyProtector.ComputeHash(normalizedServerKey);
        var maskedServerKey = ShardKeyProtector.Mask(normalizedServerKey);
        var normalizedPayload = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson;
        var payloadHash = ShardKeyProtector.ComputeHash(normalizedPayload);
        var nonce = ResolveNonce(externalEventId, uniqueScopeKey);
        var signatureHash = ShardKeyProtector.ComputeHash($"{serverKeyHash}|{nonce}|{payloadHash}");
        var now = DateTime.UtcNow;
        var batchId = Guid.NewGuid();

        var server = await db.GameServers.FirstOrDefaultAsync(
            candidate => candidate.ServerKeyHash == serverKeyHash || candidate.ServerKey == normalizedServerKey,
            cancellationToken);
        if (server is null)
        {
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.UnknownShardKey, true, false, now);
            await db.SaveChangesAsync(cancellationToken);
            throw BuildForbiddenError("Unknown shard key.", RankingTelemetryAuditReason.UnknownShardKey);
        }

        if (!server.IsActive || server.ExpiresAtUtc <= now)
        {
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.StaleShardKey, true, false, now);
            await db.SaveChangesAsync(cancellationToken);
            throw BuildForbiddenError("Shard key is stale or revoked.", RankingTelemetryAuditReason.StaleShardKey);
        }

        var expiredSignatures = await db.RankingTelemetryEventSignatures
            .Where(signature => signature.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (expiredSignatures.Count > 0)
        {
            db.RankingTelemetryEventSignatures.RemoveRange(expiredSignatures);
        }

        var isDuplicate = await db.RankingTelemetryEventSignatures
            .AsNoTracking()
            .AnyAsync(signature => signature.SignatureHash == signatureHash, cancellationToken);
        if (isDuplicate)
        {
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.DuplicateEventSignature, true, false, now);
            await db.SaveChangesAsync(cancellationToken);
            throw BuildForbiddenError("Duplicate telemetry signature detected.", RankingTelemetryAuditReason.DuplicateEventSignature);
        }

        db.RankingTelemetryEventSignatures.Add(new RankingTelemetryEventSignature
        {
            Id = Guid.NewGuid(),
            SignatureHash = signatureHash,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(SignatureTtl),
        });

        var suspiciousReasonCode = await ResolveSuspiciousReasonCodeAsync(
            playerEmail,
            serverKeyHash,
            nonce,
            normalizedPayload,
            occurredAtUtc,
            now,
            cancellationToken);
        var isSuspicious = suspiciousReasonCode is not null;

        TrackAudit(
            batchId,
            serverKeyHash,
            maskedServerKey,
            eventType,
            playerEmail,
            nonce,
            payloadHash,
            normalizedPayload,
            suspiciousReasonCode ?? RankingTelemetryAuditReason.Accepted,
            false,
            isSuspicious,
            now);

        return new RankingTelemetryValidationResult
        {
            BatchId = batchId,
            ServerKeyHash = serverKeyHash,
            TelemetryNonce = nonce,
            PayloadHash = payloadHash,
            SignatureHash = signatureHash,
            IsSuspicious = isSuspicious,
            AuditReasonCode = suspiciousReasonCode ?? RankingTelemetryAuditReason.Accepted,
            QuarantineReason = isSuspicious
                ? $"Telemetry flagged for moderation: {suspiciousReasonCode}"
                : null,
        };
    }

    private void TrackAudit(
        Guid batchId,
        string serverKeyHash,
        string maskedServerKey,
        string eventType,
        string playerEmail,
        string nonce,
        string payloadHash,
        string payloadJson,
        string reasonCode,
        bool isRejected,
        bool isQuarantined,
        DateTime now)
    {
        db.RankingTelemetryAuditLogs.Add(new RankingTelemetryAuditLog
        {
            Id = Guid.NewGuid(),
            BatchId = batchId,
            ServerKeyHash = serverKeyHash,
            ServerKeyMasked = maskedServerKey,
            EventType = eventType,
            PlayerEmail = playerEmail,
            EventNonce = nonce,
            PayloadHash = payloadHash,
            ReasonCode = reasonCode,
            RawPayloadJson = payloadJson,
            IsRejected = isRejected,
            IsQuarantined = isQuarantined,
            CreatedAtUtc = now,
        });
    }

    private async Task<string?> ResolveSuspiciousReasonCodeAsync(
        string playerEmail,
        string serverKeyHash,
        string nonce,
        string payloadJson,
        DateTime occurredAtUtc,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var minuteStart = new DateTime(occurredAtUtc.Year, occurredAtUtc.Month, occurredAtUtc.Day, occurredAtUtc.Hour, occurredAtUtc.Minute, 0, DateTimeKind.Utc);
        var minuteEnd = minuteStart.AddMinutes(1);
        var burstCount = await db.MasterRankingEvents
            .AsNoTracking()
            .CountAsync(
                entry => entry.PlayerEmail == playerEmail
                    && entry.OccurredAtUtc >= minuteStart
                    && entry.OccurredAtUtc < minuteEnd,
                cancellationToken);
        if (burstCount >= BurstThresholdPerMinute)
        {
            return RankingTelemetryAuditReason.BurstSubmissionPattern;
        }

        var hasMismatchedShard = await db.MasterRankingEvents
            .AsNoTracking()
            .AnyAsync(
                entry => entry.TelemetryNonce == nonce
                    && entry.ServerKeyHash != null
                    && entry.ServerKeyHash != serverKeyHash
                    && entry.CreatedAtUtc >= now.AddMinutes(-10),
                cancellationToken);
        if (hasMismatchedShard)
        {
            return RankingTelemetryAuditReason.MismatchedShardKey;
        }

        if (!TryExtractNetWorthUsd(payloadJson, out var currentNetWorthUsd))
        {
            return null;
        }

        var priorPayloads = await db.MasterRankingEvents
            .AsNoTracking()
            .Where(entry => entry.PlayerEmail == playerEmail && entry.PayloadJson != null)
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .Take(25)
            .Select(entry => entry.PayloadJson)
            .ToListAsync(cancellationToken);
        foreach (var priorPayload in priorPayloads)
        {
            if (!TryExtractNetWorthUsd(priorPayload ?? "{}", out var priorNetWorthUsd))
            {
                continue;
            }

            if (currentNetWorthUsd + NetWorthRegressionToleranceUsd < priorNetWorthUsd)
            {
                return RankingTelemetryAuditReason.NonMonotonicNetWorth;
            }

            break;
        }

        return null;
    }

    private static bool TryExtractNetWorthUsd(string payloadJson, out decimal netWorthUsd)
    {
        netWorthUsd = 0m;

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!doc.RootElement.TryGetProperty("netWorthUsd", out var netWorthEl))
            {
                return false;
            }

            return netWorthEl.ValueKind switch
            {
                JsonValueKind.Number => netWorthEl.TryGetDecimal(out netWorthUsd),
                JsonValueKind.String => decimal.TryParse(netWorthEl.GetString(), out netWorthUsd),
                _ => false,
            };
        }
        catch
        {
            return false;
        }
    }

    private static string ResolveNonce(string? externalEventId, string? uniqueScopeKey)
    {
        if (!string.IsNullOrWhiteSpace(externalEventId))
        {
            return externalEventId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(uniqueScopeKey))
        {
            return uniqueScopeKey.Trim();
        }

        return "NO_NONCE";
    }

    private static GraphQLException BuildForbiddenError(string message, string code)
    {
        return new GraphQLException(
            ErrorBuilder.New()
                .SetMessage(message)
                .SetCode(code)
                .SetExtension("httpStatus", 403)
                .Build());
    }
}
