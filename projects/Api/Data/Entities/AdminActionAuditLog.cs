using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

public sealed class AdminActionAuditLog
{
    public Guid Id { get; set; }

    public Guid AdminActorPlayerId { get; set; }

    [Required, MaxLength(256)]
    public string AdminActorEmail { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string AdminActorDisplayName { get; set; } = string.Empty;

    public Guid EffectivePlayerId { get; set; }

    [Required, MaxLength(256)]
    public string EffectivePlayerEmail { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string EffectivePlayerDisplayName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string EffectiveAccountType { get; set; } = AccountContextType.Person;

    public Guid? EffectiveCompanyId { get; set; }

    [MaxLength(200)]
    public string? EffectiveCompanyName { get; set; }

    [MaxLength(160)]
    public string GraphQlOperationName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string MutationSummary { get; set; } = string.Empty;

    public int ResponseStatusCode { get; set; }

    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}