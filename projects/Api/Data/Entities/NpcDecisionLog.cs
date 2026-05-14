using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Per-tick decision audit trail for autonomous NPC companies.
/// </summary>
public sealed class NpcDecisionLog
{
    public Guid Id { get; set; }
    public Guid NpcCompanyId { get; set; }
    public NpcCompany NpcCompany { get; set; } = null!;

    public long Tick { get; set; }

    [Required, MaxLength(50)]
    public string ActionType { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Outcome { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

