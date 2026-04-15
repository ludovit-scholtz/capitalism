using System.ComponentModel.DataAnnotations;

namespace Api.Data.Entities;

/// <summary>
/// Represents a cash deposit made by a company into a bank building.
/// Deposits earn interest each tick at the rate configured when the deposit was made.
/// The bank uses these deposits as its lendable capital (subject to the 10% reserve requirement).
/// </summary>
public sealed class BankDeposit
{
    public Guid Id { get; set; }

    /// <summary>The bank building holding this deposit.</summary>
    public Guid BankBuildingId { get; set; }

    /// <summary>Navigation property to the bank building.</summary>
    public Building BankBuilding { get; set; } = null!;

    /// <summary>The company that made the deposit (may be the bank's own company for base capital).</summary>
    public Guid DepositorCompanyId { get; set; }

    /// <summary>Navigation property to the depositor company.</summary>
    public Company DepositorCompany { get; set; } = null!;

    /// <summary>Current balance of the deposit (after any partial withdrawals).</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Annual interest rate (%) snapshotted at deposit time.
    /// Preserved even if the bank changes its deposit rate later.
    /// </summary>
    public decimal DepositInterestRatePercent { get; set; }

    /// <summary>Whether this is the bank's own base-capital deposit (created on bank placement).</summary>
    public bool IsBaseCapital { get; set; }

    /// <summary>Whether the deposit is still active (not fully withdrawn).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Tick when the deposit was created.</summary>
    public long DepositedAtTick { get; set; }

    /// <summary>UTC timestamp when the deposit was created.</summary>
    public DateTime DepositedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Tick when the deposit was fully withdrawn (null if still active).</summary>
    public long? WithdrawnAtTick { get; set; }

    /// <summary>UTC timestamp when the deposit was fully withdrawn.</summary>
    public DateTime? WithdrawnAtUtc { get; set; }

    /// <summary>Total interest paid out to depositor over the life of this deposit.</summary>
    public decimal TotalInterestPaid { get; set; }
}
