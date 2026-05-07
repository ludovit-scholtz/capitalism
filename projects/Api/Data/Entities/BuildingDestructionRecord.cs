using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Audit record created when a building is automatically destroyed after a loan default
/// and the <see cref="GameConstants.ForeclosureWindowTicks"/> grace period expires.
/// </summary>
public sealed class BuildingDestructionRecord
{
    /// <summary>Unique identifier for this destruction event.</summary>
    public Guid Id { get; set; }

    /// <summary>The building that was destroyed.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>Denormalised building name at the time of destruction (kept for historical queries).</summary>
    [MaxLength(200)]
    public string BuildingName { get; set; } = string.Empty;

    /// <summary>The defaulted loan that triggered the destruction.</summary>
    public Guid? LoanId { get; set; }

    /// <summary>The city where the building was located (for audit and geographic queries).</summary>
    public Guid CityId { get; set; }

    /// <summary>The company that owned the building at the time of destruction.</summary>
    public Guid OwnerCompanyId { get; set; }

    /// <summary>
    /// Appraised value of the building at the time the loan was accepted (from
    /// <see cref="Loan.CollateralAppraisedValue"/>).
    /// </summary>
    public decimal OriginalPropertyValue { get; set; }

    /// <summary>
    /// Cash compensation paid to the owner: <see cref="GameConstants.ForeclosureRefundFraction"/>
    /// of <see cref="OriginalPropertyValue"/>.
    /// </summary>
    public decimal CompensationPaid { get; set; }

    /// <summary>Game tick at which the building was destroyed.</summary>
    public long DestructionTickCount { get; set; }

    /// <summary>Human-readable reason for the destruction (e.g., "DefaultedLoan", "GracePeriodExpired").</summary>
    [MaxLength(50)]
    public string DestructionReason { get; set; } = BuildingDestructionReason.DefaultedLoan;

    /// <summary>Wall-clock UTC time when the destruction was recorded.</summary>
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Reason codes for building destruction.</summary>
public static class BuildingDestructionReason
{
    /// <summary>Building destroyed because a secured loan defaulted.</summary>
    public const string DefaultedLoan = "DefaultedLoan";

    /// <summary>Building destroyed because the foreclosure grace period expired with no buyer.</summary>
    public const string GracePeriodExpired = "GracePeriodExpired";

    /// <summary>Building destroyed for another reason.</summary>
    public const string Other = "Other";
}
