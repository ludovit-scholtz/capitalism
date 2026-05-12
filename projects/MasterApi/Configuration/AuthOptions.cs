namespace MasterApi.Configuration;

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
    /// Maximum number of forgot-password requests per normalized email during the reset window.
    /// Default is 3.
    /// </summary>
    public int ForgotPasswordMaxRequests { get; init; } = 3;

    /// <summary>
    /// Duration of the forgot-password throttling window in minutes. Default is 15.
    /// </summary>
    public int ForgotPasswordWindowMinutes { get; init; } = 15;

    /// <summary>
    /// Password reset token lifetime in minutes. Default is 60.
    /// </summary>
    public int PasswordResetTokenLifetimeMinutes { get; init; } = 60;

    /// <summary>
    /// Frontend base URL used in password reset links.
    /// </summary>
    public string PasswordResetFrontendUrl { get; init; } = "http://localhost:5173";

    /// <summary>
    /// Sender address used for password reset emails.
    /// </summary>
    public string PasswordResetEmailFrom { get; init; } = "no-reply@capitalism.local";

    /// <summary>
    /// Optional sender display name for password reset emails.
    /// </summary>
    public string PasswordResetEmailFromName { get; init; } = "Capitalism";

    /// <summary>
    /// SMTP host for password reset email delivery. Leave empty to disable SMTP sending.
    /// </summary>
    public string PasswordResetSmtpHost { get; init; } = string.Empty;

    /// <summary>
    /// SMTP port for password reset email delivery.
    /// </summary>
    public int PasswordResetSmtpPort { get; init; } = 587;

    /// <summary>
    /// SMTP username for password reset email delivery.
    /// </summary>
    public string PasswordResetSmtpUsername { get; init; } = string.Empty;

    /// <summary>
    /// SMTP password for password reset email delivery.
    /// </summary>
    public string PasswordResetSmtpPassword { get; init; } = string.Empty;

    /// <summary>
    /// Enables TLS when sending password reset emails through SMTP.
    /// </summary>
    public bool PasswordResetSmtpEnableSsl { get; init; } = true;
}
