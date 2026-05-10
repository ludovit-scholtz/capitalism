namespace MasterApi.Data.Entities;

public sealed class RankingTelemetryAuditLog
{
    public Guid Id { get; set; }

    public Guid BatchId { get; set; }

    public string ServerKeyHash { get; set; } = string.Empty;

    public string ServerKeyMasked { get; set; } = string.Empty;

    public string EventType { get; set; } = string.Empty;

    public string PlayerEmail { get; set; } = string.Empty;

    public string? EventNonce { get; set; }

    public string PayloadHash { get; set; } = string.Empty;

    public string ReasonCode { get; set; } = RankingTelemetryAuditReason.Accepted;

    public string RawPayloadJson { get; set; } = "{}";

    public bool IsRejected { get; set; }

    public bool IsQuarantined { get; set; }

    public string? QuarantineReason { get; set; }

    public DateTime? QuarantineUpdatedAtUtc { get; set; }

    public string? QuarantineUpdatedByEmail { get; set; }

    public string? ClearJustification { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public sealed class RankingTelemetryEventSignature
{
    public Guid Id { get; set; }

    public string SignatureHash { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}

public static class RankingTelemetryAuditReason
{
    public const string Accepted = "ACCEPTED";
    public const string UnknownShardKey = "UNKNOWN_SHARD_KEY";
    public const string StaleShardKey = "STALE_SHARD_KEY";
    public const string DuplicateEventSignature = "DUPLICATE_EVENT_SIGNATURE";
}
