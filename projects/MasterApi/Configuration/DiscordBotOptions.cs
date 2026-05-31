namespace MasterApi.Configuration;

/// <summary>
/// Configuration for the master-server Discord bot. A single Discord server can host
/// both the staging and production master servers side by side because every command
/// name is prefixed with <see cref="CommandPrefix"/> (for example <c>cap5-verify</c> for
/// production and <c>cap5stage-verify</c> for staging).
/// </summary>
public sealed class DiscordBotOptions
{
    public const string SectionName = "DiscordBot";

    /// <summary>Master switch. When false the bot hosted service does not start.</summary>
    public bool Enabled { get; set; }

    /// <summary>Discord bot token. When empty the bot does not start even if enabled.</summary>
    public string BotToken { get; set; } = string.Empty;

    /// <summary>
    /// Prefix applied to every slash command. Use <c>cap5</c> for production and
    /// <c>cap5stage</c> for staging so both can coexist in one Discord server.
    /// </summary>
    public string CommandPrefix { get; set; } = "cap5";

    /// <summary>
    /// Optional Discord guild (server) id. When set, slash commands are registered to this
    /// guild only (updates are near-instant). When 0, commands are registered globally.
    /// </summary>
    public ulong GuildId { get; set; }

    /// <summary>
    /// Id of the Discord channel mirrored with the in-game chat. Messages from linked users
    /// in this channel are relayed into the game, and in-game chat is posted back here.
    /// When 0 the chat bridge is disabled.
    /// </summary>
    public ulong ChatChannelId { get; set; }

    /// <summary>Public master frontend URL shown by the help command.</summary>
    public string MasterFrontendUrl { get; set; } = "https://capitalism5.com";

    /// <summary>Public invite link to the community Discord, shown by the help command.</summary>
    public string DiscordInviteUrl { get; set; } = "https://discord.gg/PhHSxJvDn6";

    /// <summary>Default blockchain network used when a command omits it.</summary>
    public string DefaultNetwork { get; set; } = "ALGORAND";

    /// <summary>Minutes a generated Discord link code stays valid.</summary>
    public int LinkCodeLifetimeMinutes { get; set; } = 30;

    public string NormalizedCommandPrefix()
    {
        var prefix = CommandPrefix?.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(prefix) ? "cap5" : prefix;
    }

    public bool IsConfigured() => Enabled && !string.IsNullOrWhiteSpace(BotToken);
}
