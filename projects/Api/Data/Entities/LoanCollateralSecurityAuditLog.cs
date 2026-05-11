using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Structured audit log for collateral/foreclosure commit-time conflict rejections.
/// </summary>
public sealed class LoanCollateralSecurityAuditLog
{
    public Guid Id { get; set; }
    public Guid? LoanId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid PlayerId { get; set; }

    [Required, MaxLength(40)]
    public string Action { get; set; } = string.Empty;

    [Required, MaxLength(80)]
    public string RejectionReason { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Detail { get; set; }

    public bool IsDeadLetter { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;
}
