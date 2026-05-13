namespace Api.Configuration;

/// <summary>
/// Authentication feature flags controlling which authentication methods are available.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// When <c>false</c> (the default), the <c>login</c> and <c>register</c> GraphQL mutations
    /// are disabled and will return AUTH_PASSWORD_DISABLED. Set to <c>true</c> to allow
    /// traditional email/password authentication alongside OIDC.
    /// </summary>
    public bool PasswordAuthEnabled { get; init; } = false;

    /// <summary>
    /// Number of consecutive failed login attempts before the account is temporarily locked out.
    /// Default is 5.
    /// </summary>
    public int MaxFailedLoginAttempts { get; init; } = 5;

    /// <summary>
    /// How long (in minutes) a locked-out account must wait before it can attempt login again.
    /// Default is 15 minutes.
    /// </summary>
    public int LockoutWindowMinutes { get; init; } = 15;

    /// <summary>
    /// Maximum number of login/register requests per IP address per minute before HTTP 429 is returned.
    /// Applies in non-Development environments only. Default is 10.
    /// </summary>
    public int RateLimitRequestsPerMinute { get; init; } = 10;

    /// <summary>
    /// When enabled, auth rate limiting also runs in Testing environment.
    /// Defaults to false so existing test suites are not affected unless explicitly opted in.
    /// </summary>
    public bool EnableRateLimitInTesting { get; init; } = false;
}
