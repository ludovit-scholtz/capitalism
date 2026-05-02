namespace MasterApi.Data.Entities;

public sealed class MasterRankingBountyDefinition
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public decimal RewardPoints { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool IsVisibleToPlayers { get; set; } = true;

    public bool RequiresModeration { get; set; }

    public string CooldownMode { get; set; } = RankingCooldownMode.UtcDay;

    public string SourceEventType { get; set; } = string.Empty;

    public string ProofRequirement { get; set; } = RankingProofRequirement.None;

    public string ValidationSettingsJson { get; set; } = "{}";

    public string VisibilityScope { get; set; } = RankingVisibilityScope.PlayerHistory;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<MasterRankingRewardRecord> RewardRecords { get; set; } = [];

    public ICollection<MasterRankingBountyAudit> AuditTrail { get; set; } = [];
}

public static class RankingCooldownMode
{
    public const string None = "NONE";
    public const string UtcDay = "UTC_DAY";
    public const string UtcDayPerServer = "UTC_DAY_PER_SERVER";
    public const string Once = "ONCE";
    public const string PerUniqueKey = "PER_UNIQUE_KEY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        UtcDay,
        UtcDayPerServer,
        Once,
        PerUniqueKey,
    };
}

public static class RankingProofRequirement
{
    public const string None = "NONE";
    public const string Url = "URL";
    public const string DiscordHandle = "DISCORD_HANDLE";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        None,
        Url,
        DiscordHandle,
    };
}

public static class RankingVisibilityScope
{
    public const string PlayerHistory = "PLAYER_HISTORY";
    public const string AdminOnly = "ADMIN_ONLY";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        PlayerHistory,
        AdminOnly,
    };
}
