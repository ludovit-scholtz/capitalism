using System.Security.Claims;

namespace Capitalism.Shared.Security;

public static class TokenBoundaryClaims
{
    public const string IssuerClaimType = "iss";
    public const string TokenTypeClaimType = "capitalism/token_type";
    public const string ImpersonationGrantClaimType = "capitalism/impersonation-granted";
    public const string MasterPrivilegeEligibleClaimType = "capitalism/master-privilege-eligible";

    public const string TokenTypeGame = "game";
    public const string TokenTypeMaster = "master";

    public static string? GetTokenType(ClaimsPrincipal principal)
        => principal.FindFirst(TokenTypeClaimType)?.Value;

    public static bool IsMasterToken(ClaimsPrincipal principal)
        => string.Equals(GetTokenType(principal), TokenTypeMaster, StringComparison.OrdinalIgnoreCase);

    public static bool HasImpersonationGrant(ClaimsPrincipal principal)
        => string.Equals(
            principal.FindFirst(ImpersonationGrantClaimType)?.Value,
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsMasterPrivilegeEligible(ClaimsPrincipal principal, string expectedIssuer)
    {
        var tokenType = GetTokenType(principal);
        var issuer = principal.FindFirst(IssuerClaimType)?.Value;

        return string.Equals(tokenType, TokenTypeMaster, StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeIssuer(issuer), NormalizeIssuer(expectedIssuer), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIssuer(string? issuer)
        => issuer?.Trim().TrimEnd('/') ?? string.Empty;
}
