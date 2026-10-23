namespace Api.Types;

/// <summary>What happened to the caller's companies between a remembered tick and now (#602). Money is in USD.</summary>
public sealed class AwaySummaryResult
{
    /// <summary>The start of the window actually used (the requested tick, clamped to the retained history).</summary>
    public long SinceTick { get; set; }

    public long CurrentTick { get; set; }
    public long TicksAway { get; set; }
    public decimal RevenueUsd { get; set; }

    /// <summary>Running costs as a negative number (labor, energy, purchasing, tax, interest).</summary>
    public decimal CostsUsd { get; set; }

    public decimal NetUsd { get; set; }
    public int LoanPaymentsCount { get; set; }
}
