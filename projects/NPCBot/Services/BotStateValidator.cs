using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Services;

/// <summary>Result returned by <see cref="BotStateValidator"/>.</summary>
public sealed class BotStateValidationResult
{
    /// <summary>Whether the bot is ready for normal operation.</summary>
    public bool IsValid { get; init; }

    /// <summary>Human-readable summary of the current state.</summary>
    public string Summary { get; init; } = string.Empty;

    /// <summary>Specific issues found; empty when <see cref="IsValid"/> is true.</summary>
    public IReadOnlyList<string> Issues { get; init; } = [];
}

/// <summary>
/// Validates the runtime state of a <see cref="BotAccount"/> before the orchestrator
/// schedules any operation on it.  All methods are pure (no I/O) so they are easy to test.
/// </summary>
public static class BotStateValidator
{
    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Validates all aspects of the bot's current state.
    /// Returns a <see cref="BotStateValidationResult"/> whose <c>IsValid</c> flag is true
    /// only when the bot is authenticated, onboarded, not skipped, and not stale.
    /// </summary>
    /// <param name="bot">The bot to validate.</param>
    /// <param name="staleAfterMinutes">
    /// If the bot has not had a successful operation in this many minutes it is considered stale.
    /// Defaults to 10 minutes (≈ one poll interval buffer).
    /// </param>
    public static BotStateValidationResult Validate(BotAccount bot, int staleAfterMinutes = 10)
    {
        var issues = new List<string>();

        if (bot.IsSkipped)
            issues.Add("Bot has been skipped due to too many consecutive errors.");

        if (!bot.HasValidToken)
            issues.Add("Token is missing or expired.");

        if (!bot.OnboardingCompleted)
            issues.Add("Onboarding has not been completed.");

        if (IsStale(bot, staleAfterMinutes))
            issues.Add($"No successful operation in the last {staleAfterMinutes} minutes.");

        return new BotStateValidationResult
        {
            IsValid = issues.Count == 0,
            Summary = issues.Count == 0
                ? "Bot is ready for operation."
                : string.Join(" ", issues),
            Issues = issues,
        };
    }

    /// <summary>
    /// Returns true when the bot is ready for normal game operations:
    /// not skipped, has a valid token, and has completed onboarding.
    /// </summary>
    public static bool IsReadyForOperation(BotAccount bot) =>
        !bot.IsSkipped && bot.HasValidToken && bot.OnboardingCompleted;

    /// <summary>Returns true when the bot has had no successful call for longer than the threshold.</summary>
    public static bool IsStale(BotAccount bot, int staleAfterMinutes = 10)
    {
        if (bot.LastSuccessUtc is null)
            return false; // has not started yet — not stale, just uninitialised

        return DateTime.UtcNow - bot.LastSuccessUtc.Value > TimeSpan.FromMinutes(staleAfterMinutes);
    }

    /// <summary>
    /// Returns true when the bot is approaching its error limit and should be considered at-risk.
    /// </summary>
    /// <param name="bot">The bot to check.</param>
    /// <param name="maxConsecutiveErrors">The configured limit.</param>
    public static bool IsAtRisk(BotAccount bot, int maxConsecutiveErrors) =>
        !bot.IsSkipped && bot.ConsecutiveErrors > 0 &&
        (double)bot.ConsecutiveErrors / maxConsecutiveErrors >= 0.5;
}
