namespace Api.Data.Entities;

/// <summary>
/// Immutable audit record for a bank deposit interest rate change.
/// Created whenever a bank owner calls <c>UpdateBankDepositRate</c>.
/// Tracks what the previous rate was, what the new rate is, when it becomes effective,
/// and how many deposits were affected.
/// </summary>
public sealed class BankDepositRateHistory
{
    /// <summary>Unique identifier for this rate-change record.</summary>
    public Guid Id { get; set; }

    /// <summary>The bank building whose deposit rate was changed.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Navigation property to the bank building.</summary>
    public Building BankBuilding { get; set; } = null!;

    /// <summary>Annual deposit interest rate (%) before this change.</summary>
    public decimal PreviousRatePercent { get; set; }

    /// <summary>New annual deposit interest rate (%) that becomes effective on <see cref="EffectiveTick"/>.</summary>
    public decimal NewRatePercent { get; set; }

    /// <summary>Game tick at which the new rate is applied to all active deposits at this bank.</summary>
    public long EffectiveTick { get; set; }

    /// <summary>UTC timestamp corresponding to <see cref="EffectiveTick"/>.</summary>
    public DateTime EffectiveUtc { get; set; }

    /// <summary>Game tick when this rate change was scheduled.</summary>
    public long ScheduledAtTick { get; set; }

    /// <summary>UTC timestamp when this rate change was scheduled.</summary>
    public DateTime ScheduledAtUtc { get; set; }

    /// <summary>Number of active deposits that were affected when the rate became effective. 0 until applied.</summary>
    public int AffectedDepositCount { get; set; }

    /// <summary>Player ID of the bank owner who scheduled this rate change.</summary>
    public Guid ChangedByPlayerId { get; set; }

    /// <summary>Navigation property to the player who changed the rate.</summary>
    public Player ChangedByPlayer { get; set; } = null!;

    /// <summary>True when the tick processor has applied this rate change to all active deposits.</summary>
    public bool IsApplied { get; set; }
}
