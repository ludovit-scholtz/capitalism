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
