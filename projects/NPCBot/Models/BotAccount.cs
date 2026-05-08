using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Models;

/// <summary>Mutable runtime state for a single NPC bot account.</summary>
public sealed class BotAccount
{
    /// <summary>Numeric index (1-based) used to generate a unique bot identity.</summary>
    public int Index { get; init; }

    /// <summary>Bot display name used in-game.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Bot e-mail address used for registration and login.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Strategy profile name (e.g. "Trading", "Industrial").</summary>
    public string Strategy { get; init; } = string.Empty;

    /// <summary>Current bearer token; null when not authenticated.</summary>
    public string? Token { get; set; }

    /// <summary>Token expiry; null when not authenticated.</summary>
    public DateTime? TokenExpiresAtUtc { get; set; }

    /// <summary>Last known player profile fetched from the API.</summary>
    public PlayerProfile? Profile { get; set; }

    /// <summary>Number of consecutive API errors since last successful call.</summary>
    public int ConsecutiveErrors { get; set; }

    /// <summary>Whether this bot has been disabled due to too many errors.</summary>
    public bool IsSkipped { get; set; }

    /// <summary>UTC timestamp of the last successful operation.</summary>
    public DateTime? LastSuccessUtc { get; set; }

    /// <summary>Net worth at the start of tracking (after onboarding).</summary>
    public decimal InitialNetWorth { get; set; }

    /// <summary>Most recently recorded net worth.</summary>
    public decimal CurrentNetWorth { get; set; }

    /// <summary>Game tick at which this bot's tracking started.</summary>
    public long TrackingStartTick { get; set; }

    /// <summary>
    /// Returns true when the auth token is valid.
    /// For API key tokens (prefixed <c>APIKEY:</c>) this is always true — they never expire.
    /// For JWT bearer tokens, uses a 5-minute proactive-refresh buffer (configurable via <c>TokenRefreshBufferMinutes</c>).
    /// </summary>
    public bool IsTokenValid(int bufferMinutes = 5)
    {
        if (string.IsNullOrWhiteSpace(Token))
            return false;

        // API key sentinel: never expires.
        if (Token.StartsWith("APIKEY:", StringComparison.Ordinal))
            return true;

        return TokenExpiresAtUtc.HasValue &&
               DateTime.UtcNow < TokenExpiresAtUtc.Value.AddMinutes(-bufferMinutes);
    }

    /// <summary>
    /// Convenience property using the default 5-minute refresh buffer.
    /// Call <see cref="IsTokenValid(int)"/> when the buffer needs to match configuration.
    /// </summary>
    public bool HasValidToken => IsTokenValid();

    /// <summary>Returns true when onboarding has been completed for this bot.</summary>
    public bool OnboardingCompleted =>
        Profile?.OnboardingCompletedAtUtc is not null;

    /// <summary>Profitability delta: positive means the bot is making money.</summary>
    public decimal ProfitDelta => CurrentNetWorth - InitialNetWorth;

    /// <summary>
    /// The bot's current rank in the global leaderboard, as last fetched by the orchestrator.
    /// Null when rankings have not been fetched yet or the bot's name was not found in the list.
    /// </summary>
    public int? CurrentRank { get; set; }

    /// <summary>
    /// The most recent strategy recommendation produced by <see cref="Services.BotProfitCalculator"/>.
    /// Set each tick by the orchestrator; cleared after every application attempt (success or no-op)
    /// to prevent infinite retry loops when no units are adjustable or all changes are sub-cent.
    /// </summary>
    public StrategyRecommendation? PendingRecommendation { get; set; }

    public override string ToString() =>
        $"[Bot #{Index} {DisplayName} ({Strategy})]";
}
