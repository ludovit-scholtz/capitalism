using Microsoft.Extensions.Hosting;

namespace Capitalism.NPCBot.Configuration;

/// <summary>
/// Validates the NPC bot credential configuration at startup and throws a
/// descriptive <see cref="InvalidOperationException"/> when an insecure
/// placeholder value is detected outside the <c>Development</c> environment.
/// </summary>
public static class BotStartupValidator
{
    /// <summary>
    /// Well-known placeholder values that must never be used in a non-Development
    /// environment. Comparison is case-insensitive.
    /// </summary>
    public static readonly string[] KnownPlaceholders =
    [
        "",
        "NpcBot!2025",
        "changeme",
        "default",
        "password",
        "secret",
    ];

    /// <summary>
    /// Validates that the credential configuration is safe for the current
    /// hosting environment.
    /// <list type="bullet">
    ///   <item>In API-key mode (<see cref="BotOptions.ApiKey"/> is set), password validation is skipped entirely.</item>
    ///   <item>In the <c>Development</c> environment, placeholder passwords are allowed (local dev convenience).</item>
    ///   <item>In all other environments the password must be a non-placeholder value, or the application throws.</item>
    /// </list>
    /// </summary>
    /// <param name="options">Resolved <see cref="BotOptions"/> instance.</param>
    /// <param name="environment">Current hosting environment.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the bot is <see cref="BotOptions.Enabled"/> and the password is a
    /// known placeholder value while running outside the <c>Development</c> environment.
    /// </exception>
    public static void Validate(BotOptions options, IHostEnvironment environment)
    {
        // API-key mode: no password required.
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            return;

        // Development environment: allow placeholder passwords for local convenience.
        if (environment.IsDevelopment())
            return;

        // If the bot is disabled, a missing password is harmless — do not block startup.
        if (!options.Enabled)
            return;

        if (IsPlaceholder(options.BotPassword))
        {
            throw new InvalidOperationException(
                "NPC bot credential is set to the default placeholder value. " +
                "Set NpcBot__BotPassword in your environment before starting the bot outside Development, " +
                "or configure NpcBot__ApiKey for API-key authentication mode. " +
                "Example: export NPCBOT_NpcBot__BotPassword=\"$(openssl rand -hex 32)\"");
        }
    }

    /// <summary>
    /// Returns <c>true</c> when <paramref name="value"/> is a known insecure
    /// placeholder (empty, null, or matches a well-known weak credential).
    /// </summary>
    public static bool IsPlaceholder(string? value) =>
        KnownPlaceholders.Contains(value?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);
}
