namespace Capitalism.NPCBot.Models;

/// <summary>Classifies how profitable a bot currently is.</summary>
public enum ProfitabilityStatus
{
    /// <summary>Net worth has grown since tracking started.</summary>
    Profitable,

    /// <summary>Net worth is within a small threshold of the baseline (±2%).</summary>
    Neutral,

    /// <summary>Net worth has declined since tracking started.</summary>
    Unprofitable,

    /// <summary>Tracking has just started — not enough data yet.</summary>
    Unknown,
}
