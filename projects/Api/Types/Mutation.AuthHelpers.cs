using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Types;

public sealed partial class Mutation
{
    private static AuthenticatedSession GenerateToken(
        Player player,
        JwtOptions options,
        AdminImpersonationTokenContext? impersonation = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var issuedAtUtc = DateTime.UtcNow;
        var expires = issuedAtUtc.AddMinutes(options.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Email, player.Email),
            new Claim(ClaimTypes.Name, player.DisplayName),
            new Claim(ClaimTypes.Role, player.Role),
            new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeGame),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new Claim(JwtRegisteredClaimNames.Iat, EpochTime.GetIntDate(issuedAtUtc).ToString(), ClaimValueTypes.Integer64),
        };

        if (impersonation is not null)
        {
            claims.Add(new Claim(ClaimsPrincipalExtensions.EffectivePlayerIdClaimType, impersonation.EffectivePlayer.Id.ToString()));
            claims.Add(new Claim(ClaimsPrincipalExtensions.EffectivePlayerEmailClaimType, impersonation.EffectivePlayer.Email));
            claims.Add(new Claim(ClaimsPrincipalExtensions.EffectivePlayerNameClaimType, impersonation.EffectivePlayer.DisplayName));
            claims.Add(new Claim(ClaimsPrincipalExtensions.EffectiveAccountTypeClaimType, impersonation.EffectiveAccountType));

            if (impersonation.EffectiveCompanyId.HasValue)
            {
                claims.Add(new Claim(ClaimsPrincipalExtensions.EffectiveCompanyIdClaimType, impersonation.EffectiveCompanyId.Value.ToString()));
            }

            if (!string.IsNullOrWhiteSpace(impersonation.EffectiveCompanyName))
            {
                claims.Add(new Claim(ClaimsPrincipalExtensions.EffectiveCompanyNameClaimType, impersonation.EffectiveCompanyName));
            }

            claims.Add(new Claim(TokenBoundaryClaims.ImpersonationGrantClaimType, bool.TrueString));
        }

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: issuedAtUtc,
            expires: expires,
            signingCredentials: credentials);

        return new AuthenticatedSession(
            new JwtSecurityTokenHandler().WriteToken(token),
            expires);
    }

    private sealed record ImpersonationAccountContext(
        string EffectiveAccountType,
        Guid? EffectiveCompanyId,
        string? EffectiveCompanyName);

    private sealed record AdminImpersonationTokenContext(
        Player EffectivePlayer,
        string EffectiveAccountType,
        Guid? EffectiveCompanyId,
        string? EffectiveCompanyName);

    private static async Task TrackIssuedSessionAsync(
        AppDbContext db,
        Guid playerId,
        AuthenticatedSession session,
        HttpContext? httpContext,
        CancellationToken cancellationToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        var jti = jwt.Claims.FirstOrDefault(claim => claim.Type == JwtRegisteredClaimNames.Jti)?.Value ?? jwt.Id;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var current = await db.PlayerSessions.FirstOrDefaultAsync(candidate => candidate.Jti == jti, cancellationToken);
        if (current is not null)
        {
            current.LastSeenAtUtc = DateTime.UtcNow;
            current.ExpiresAtUtc = session.ExpiresAtUtc;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        db.PlayerSessions.Add(new PlayerSession
        {
            Jti = jti,
            PlayerId = playerId,
            IssuedAtUtc = jwt.ValidFrom,
            ExpiresAtUtc = session.ExpiresAtUtc,
            LastSeenAtUtc = DateTime.UtcNow,
            LastSeenIpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            UserAgent = httpContext?.Request.Headers.UserAgent.ToString(),
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
