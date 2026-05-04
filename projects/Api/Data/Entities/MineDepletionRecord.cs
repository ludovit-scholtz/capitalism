namespace Api.Data.Entities;

/// <summary>
/// Audit record created when a mine lot's <see cref="BuildingLot.MaterialQuantity"/> reaches zero.
/// Used for analytics, depletion event tracking, and future "prospecting" features.
/// </summary>
public sealed class MineDepletionRecord
{
    /// <summary>Unique identifier for this depletion event.</summary>
    public Guid Id { get; set; }

    /// <summary>The lot that was fully depleted.</summary>
    public Guid LotId { get; set; }

    /// <summary>The building (mine) that extracted the last ore from the lot.</summary>
    public Guid BuildingId { get; set; }

    /// <summary>The company that owns the mine building.</summary>
    public Guid CompanyId { get; set; }

    /// <summary>The resource type extracted to depletion.</summary>
    public Guid? ResourceTypeId { get; set; }

    /// <summary>Denormalised resource name at the time of depletion (kept for historical queries even if the resource type is renamed).</summary>
    public string ResourceTypeName { get; set; } = string.Empty;

    /// <summary>The original deposit size before any extraction (in tonnes).</summary>
    public decimal OriginalQuantity { get; set; }

    /// <summary>Game tick at which depletion was recorded.</summary>
    public long DepletedAtTick { get; set; }

    /// <summary>Wall-clock UTC time when depletion was recorded.</summary>
    public DateTime DepletedAtUtc { get; set; } = DateTime.UtcNow;
}
