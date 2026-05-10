using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Structured security audit log for FX execution rejections.
/// Persists every QUOTE_EXPIRED, QUOTE_ALREADY_USED, and SLIPPAGE_EXCEEDED rejection
/// for fraud investigation and fairness monitoring.
/// </summary>
public sealed class FxSecurityAuditLog
{
    /// <summary>Unique identifier of this audit entry.</summary>
    public Guid Id { get; set; }

    /// <summary>The player whose trade was rejected.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Hex-encoded hash of the nonce UUID (N format, no dashes) for log correlation
    /// without embedding the raw nonce value.
    /// </summary>
    [Required, MaxLength(64)]
    public string NonceHash { get; set; } = string.Empty;

    /// <summary>Rejection reason code: QUOTE_EXPIRED, QUOTE_ALREADY_USED, or SLIPPAGE_EXCEEDED.</summary>
    [Required, MaxLength(64)]
    public string RejectionReason { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the rejection occurred.</summary>
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
