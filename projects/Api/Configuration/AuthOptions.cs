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
}
