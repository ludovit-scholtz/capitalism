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
}

public sealed class RankingTelemetryValidator(MasterDbContext db)
{
    private static readonly TimeSpan SignatureTtl = TimeSpan.FromHours(24);

    public async Task<RankingTelemetryValidationResult> ValidateAndTrackAsync(
        string serverKey,
        string eventType,
        string playerEmail,
        string? externalEventId,
        string? uniqueScopeKey,
        string payloadJson,
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
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.UnknownShardKey, true, now);
            await db.SaveChangesAsync(cancellationToken);
            throw BuildForbiddenError("Unknown shard key.", RankingTelemetryAuditReason.UnknownShardKey);
        }

        if (!server.IsActive || server.ExpiresAtUtc <= now)
        {
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.StaleShardKey, true, now);
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
            TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.DuplicateEventSignature, true, now);
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

        TrackAudit(batchId, serverKeyHash, maskedServerKey, eventType, playerEmail, nonce, payloadHash, normalizedPayload, RankingTelemetryAuditReason.Accepted, false, now);

        return new RankingTelemetryValidationResult
        {
            BatchId = batchId,
            ServerKeyHash = serverKeyHash,
            TelemetryNonce = nonce,
            PayloadHash = payloadHash,
            SignatureHash = signatureHash,
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
            CreatedAtUtc = now,
        });
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
