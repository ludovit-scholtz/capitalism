namespace MasterApi.Types;

public sealed class RegisterInput
{
    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Optional email of the existing player who referred this registration.</summary>
    public string? ReferralEmail { get; set; }

    public string? Locale { get; set; }

    public string? CurrentUrl { get; set; }
}

public sealed class LoginInput
{
    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string? Locale { get; set; }

    public string? CurrentUrl { get; set; }
}

public sealed class ProlongSubscriptionInput
{
    /// <summary>Number of months to extend the subscription (1–12).</summary>
    public int Months { get; set; } = 1;
}

public sealed class RegisterGameServerInput
{
    public string RegistrationKey { get; set; } = string.Empty;

    public string ServerKey { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Region { get; set; } = string.Empty;

    public string Environment { get; set; } = string.Empty;

    public string BackendUrl { get; set; } = string.Empty;

    public string GraphqlUrl { get; set; } = string.Empty;

    public string FrontendUrl { get; set; } = string.Empty;

    public string Version { get; set; } = string.Empty;

    public int PlayerCount { get; set; }

    public int CompanyCount { get; set; }

    public long CurrentTick { get; set; }
}

public sealed class GameServerSummary
{
    public Guid Id { get; init; }

    public string ServerKey { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string Region { get; init; } = string.Empty;

    public string Environment { get; init; } = string.Empty;

    public string BackendUrl { get; init; } = string.Empty;

    public string GraphqlUrl { get; init; } = string.Empty;

    public string FrontendUrl { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public int PlayerCount { get; init; }

    public int CompanyCount { get; init; }

    public long CurrentTick { get; init; }

    public DateTime RegisteredAtUtc { get; init; }

    public DateTime LastHeartbeatAtUtc { get; init; }

    public bool IsOnline { get; init; }

    public bool IsActive { get; init; }

    public DateTime ExpiresAtUtc { get; init; }

    public string KeyStatus { get; init; } = "ACTIVE";
}

public sealed class MasterAuthPayload
{
    public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; set; }

    public MasterPlayerProfile Player { get; set; } = null!;
}

public sealed class MasterPlayerProfile
{
    public Guid Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PersonalAccountName { get; set; } = string.Empty;

    public string Gender { get; set; } = "UNSPECIFIED";

    public string PreferredLocale { get; set; } = "en";

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? StartupPackClaimedAtUtc { get; set; }

    public bool CanClaimStartupPack { get; set; }

    public bool HasReferralDiscount { get; set; }

    /// <summary>UTC time when the player requested account deletion, or null when none is pending.</summary>
    public DateTime? DeletionRequestedAtUtc { get; set; }

    /// <summary>UTC time after which a pending account deletion will be finalized, or null when none is pending.</summary>
    public DateTime? DeletionScheduledAtUtc { get; set; }

    /// <summary>True when the account is marked for deletion and still inside the cancellable cooldown window.</summary>
    public bool IsPendingDeletion { get; set; }

    /// <summary>True when the player currently receives the weekly report emails (i.e. not unsubscribed).</summary>
    public bool WeeklyReportEmailSubscribed { get; set; } = true;
}

public sealed class UpdatePersonalAccountNameInput
{
    public string PersonalAccountName { get; set; } = string.Empty;

    public string? Gender { get; set; }
}

public sealed class UpdatePersonalAccountNamePayload
{
    public Guid PlayerId { get; set; }

    public string PersonalAccountName { get; set; } = string.Empty;

    public string Gender { get; set; } = "UNSPECIFIED";
}

public sealed class SubscriptionInfo
{
    public string Tier { get; set; } = "FREE";

    public string Status { get; set; } = "NONE";

    /// <summary>Null if the player has no active subscription.</summary>
    public DateTime? ExpiresAtUtc { get; set; }

    /// <summary>Null if the player has no active subscription.</summary>
    public DateTime? StartsAtUtc { get; set; }

    public bool IsActive { get; set; }

    /// <summary>Days remaining until expiry; null if no active subscription.</summary>
    public int? DaysRemaining { get; set; }

    /// <summary>Whether the player is eligible to prolong/renew.</summary>
    public bool CanProlong { get; set; }
}
