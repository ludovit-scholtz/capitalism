namespace MasterApi.Configuration;

public sealed class AccountDeletionOptions
{
    public const string SectionName = "AccountDeletion";

    /// <summary>
    /// Cooldown period, in hours, between when a player marks their account for
    /// deletion and when the deletion may be finalized. During this window the
    /// player can cancel the deletion. Defaults to 24 hours.
    /// </summary>
    public int CooldownHours { get; set; } = 24;

    /// <summary>How often the background worker scans for due deletions, in minutes.</summary>
    public int ProcessingIntervalMinutes { get; set; } = 15;
}
