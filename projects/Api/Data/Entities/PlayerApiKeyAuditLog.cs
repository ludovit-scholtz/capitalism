using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Immutable audit record written for each API-key-authenticated GraphQL request.
/// </summary>
public sealed class PlayerApiKeyAuditLog
{
    public Guid Id { get; set; }

    public Guid PlayerApiKeyId { get; set; }

    public Guid PlayerId { get; set; }

    [Required, MaxLength(160)]
    public string OperationName { get; set; } = string.Empty;

    [Required, MaxLength(16)]
    public string OperationType { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string ScopeUsed { get; set; } = string.Empty;

    public bool WasAllowed { get; set; }

    [MaxLength(80)]
    public string? DenialCode { get; set; }

    [MaxLength(40)]
    public string? DenialReason { get; set; }

    [MaxLength(64)]
    public string? AttemptedObjectId { get; set; }

    [MaxLength(64)]
    public string? IpAddress { get; set; }

    [MaxLength(128)]
    public string? SessionContext { get; set; }

    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    public PlayerApiKey? PlayerApiKey { get; set; }
}
