namespace MasterApi.Data.Entities;

public sealed class PlayerAccount
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Gender { get; set; } = Capitalism.Shared.Security.PlayerGender.Unspecified;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? LastLoginAtUtc { get; set; }

    public string PreferredLocale { get; set; } = "en";

    public DateTime? PreferredLocaleUpdatedAtUtc { get; set; }

    public bool HasReceivedRegistrationEmail { get; set; }

    public DateTime? RegistrationEmailSentAtUtc { get; set; }

    public DateTime? LastWeeklyEmailSentAtUtc { get; set; }

    /// <summary>
    /// When true, the player has opted out of the weekly report emails and the
    /// background sender must skip them.
    /// </summary>
    public bool WeeklyReportEmailUnsubscribed { get; set; }

    /// <summary>
    /// Stable opaque token embedded in one-click unsubscribe links so a player can
    /// unsubscribe from the weekly report emails without authenticating.
    /// </summary>
    public Guid EmailUnsubscribeToken { get; set; } = Guid.NewGuid();

    public string? LastAccessedUrl { get; set; }

    /// <summary>
    /// Tokens issued before this UTC time are considered revoked.
    /// </summary>
    public DateTime? SessionRevokedBeforeUtc { get; set; }

    public DateTime? StartupPackClaimedAtUtc { get; set; }

    /// <summary>
    /// UTC time when the player requested deletion of their account. Null when no
    /// deletion is pending. The account is not removed immediately; a cooldown
    /// period gives the player a chance to cancel.
    /// </summary>
    public DateTime? DeletionRequestedAtUtc { get; set; }

    /// <summary>
    /// UTC time after which the pending account deletion may be finalized by the
    /// background deletion worker. Equals <see cref="DeletionRequestedAtUtc"/> plus
    /// the configured cooldown period.
    /// </summary>
    public DateTime? DeletionScheduledAtUtc { get; set; }

    /// <summary>Email of the player who referred this account (null if self-registered).</summary>
    public string? ReferredByEmail { get; set; }

    /// <summary>
    /// Discord user snowflake id linked to this account, or null when no Discord
    /// account is linked. Set when the player runs the Discord verify command with a
    /// valid link code. Used by the Discord bot to resolve a player from a Discord user.
    /// </summary>
    public string? DiscordUserId { get; set; }

    /// <summary>Discord username captured at link time (for display/audit only).</summary>
    public string? DiscordUsername { get; set; }

    /// <summary>
    /// Short, single-use code the player generates in the master frontend and types
    /// into the Discord verify command to link their Discord account. Null once consumed.
    /// </summary>
    public string? DiscordLinkCode { get; set; }

    /// <summary>UTC expiry of <see cref="DiscordLinkCode"/>; codes are rejected after this time.</summary>
    public DateTime? DiscordLinkCodeExpiresAtUtc { get; set; }

    /// <summary>Current gold token balance (grams of gold). Cannot go negative.</summary>
    public decimal GoldTokenBalance { get; set; } = 0m;

    /// <summary>
    /// Optimistic concurrency token refreshed on every gold balance write.
    /// EF Core uses this to detect concurrent modifications and throw
    /// <see cref="Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException"/>
    /// when two adjustments race against the same balance snapshot.
    /// </summary>
    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();

    public ICollection<ProSubscription> Subscriptions { get; set; } = [];

    public ICollection<GoldTokenTransaction> GoldTokenTransactions { get; set; } = [];

    public ICollection<GoldTokenDepositRequest> GoldTokenDepositRequests { get; set; } = [];

    public ICollection<GoldTokenWithdrawalRequest> GoldTokenWithdrawalRequests { get; set; } = [];

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = [];

    public ICollection<MasterRankingRewardRecord> RankingRewardRecords { get; set; } = [];

    public ICollection<MasterRankingEvent> RankingEvents { get; set; } = [];

    public ICollection<MasterPlayerSession> Sessions { get; set; } = [];
}
