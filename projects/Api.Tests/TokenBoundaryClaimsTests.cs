using System.Security.Claims;
using Capitalism.Shared.Security;

namespace Api.Tests;

public sealed class TokenBoundaryClaimsTests
{
    [Fact]
    public void IsMasterPrivilegeEligible_MasterTokenWithMatchingIssuer_ReturnsTrue()
    {
        var principal = BuildPrincipal(
            new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            new Claim("iss", "https://master.capitalism.local"));

        Assert.True(TokenBoundaryClaims.IsMasterPrivilegeEligible(principal, "https://master.capitalism.local/"));
    }

    [Fact]
    public void IsMasterPrivilegeEligible_GameToken_ReturnsFalse()
    {
        var principal = BuildPrincipal(
            new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeGame),
            new Claim("iss", "https://master.capitalism.local"));

        Assert.False(TokenBoundaryClaims.IsMasterPrivilegeEligible(principal, "https://master.capitalism.local"));
    }

    [Fact]
    public void IsMasterPrivilegeEligible_MissingTokenType_ReturnsFalse()
    {
        var principal = BuildPrincipal(new Claim("iss", "https://master.capitalism.local"));

        Assert.False(TokenBoundaryClaims.IsMasterPrivilegeEligible(principal, "https://master.capitalism.local"));
    }

    [Fact]
    public void IsMasterPrivilegeEligible_TamperedIssuer_ReturnsFalse()
    {
        var principal = BuildPrincipal(
            new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            new Claim("iss", "https://game-shard.capitalism.local"));

        Assert.False(TokenBoundaryClaims.IsMasterPrivilegeEligible(principal, "https://master.capitalism.local"));
    }

    private static ClaimsPrincipal BuildPrincipal(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "test"));
}
