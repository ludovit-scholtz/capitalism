namespace Capitalism.NPCBot.Configuration;

/// <summary>Top-level configuration section for the NPC bot runner.</summary>
public sealed class BotOptions
{
    public const string SectionName = "NpcBot";

    /// <summary>GraphQL endpoint URL for the game API.</summary>
    public string GraphqlUrl { get; set; } = "https://capitalism.de-4.biatec.io/graphql";

    /// <summary>
    /// Maximum number of NPC accounts to run concurrently. Clamped between 1 and 20
    /// to prevent accidental resource exhaustion; increase this limit if your server
    /// can support more bot connections.
    /// </summary>
    public int BotCount { get; set; } = 3;

    /// <summary>Whether the bot runner is active. Set to false to pause all bots.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Prefix used when generating bot display names and e-mail addresses.</summary>
    public string BotNamePrefix { get; set; } = "NPC";

    /// <summary>
    /// Shared password used for all NPC bot accounts.
    /// Must be at least 8 characters.
    /// In production override via environment variable <c>NPCBOT_NpcBot__BotPassword</c>
    /// or <c>appsettings.Local.json</c> — do not commit real credentials.
    /// </summary>
    public string BotPassword { get; set; } = "NpcBot!2025";

    /// <summary>
    /// E-mail domain used when registering NPC accounts.
    /// Accounts are named like <c>{prefix}_{strategy}_{n}@{domain}</c>.
    /// </summary>
    public string BotEmailDomain { get; set; } = "npcbot.capitalism.local";

    /// <summary>
    /// How often (in seconds) the orchestrator polls the game state for each bot.
    /// Defaults to 60 seconds.
    /// </summary>
    public int PollIntervalSeconds { get; set; } = 60;

    /// <summary>
    /// Minutes before token expiry at which the bot proactively re-authenticates.
    /// Increase for slow-network environments.
    /// </summary>
    public int TokenRefreshBufferMinutes { get; set; } = 5;
    public int MaxConsecutiveErrors { get; set; } = 5;

    /// <summary>
    /// Minimum number of ticks that must have elapsed before the profitability
    /// calculator makes a price-adjustment recommendation.  Prevents premature
    /// strategy changes during the early-game ramp-up period.
    /// </summary>
    public int MinTicksBeforeAdjustment { get; set; } = 5;

    /// <summary>Free-to-use starter industries that NPC bots may join.</summary>
    public string[] AllowedIndustries { get; set; } =
    [
        "FURNITURE",
        "FOOD_PROCESSING",
        "HEALTHCARE",
    ];
}
