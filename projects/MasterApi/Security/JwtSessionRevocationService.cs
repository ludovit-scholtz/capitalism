using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MasterApi.Security;

public interface IJwtSessionRevocationService
{
    Task<bool> ValidateAndTrackAsync(ClaimsPrincipal principal, JwtSecurityToken token, HttpContext httpContext, CancellationToken cancellationToken);
    Task RevokeCurrentAsync(ClaimsPrincipal principal, JwtSecurityToken token, CancellationToken cancellationToken);
    Task RevokeAllForPlayerAsync(Guid playerAccountId, string reason, CancellationToken cancellationToken);
    Task RevokeOtherSessionsForPlayerAsync(Guid playerAccountId, string currentJti, string reason, CancellationToken cancellationToken);
    Task<List<MasterPlayerSessionSummary>> GetActiveSessionsAsync(Guid playerAccountId, string? currentJti, CancellationToken cancellationToken);
    Task CleanupExpiredAsync(CancellationToken cancellationToken);
}

public sealed class JwtSessionRevocationService(MasterDbContext db) : IJwtSessionRevocationService
{
    private const string DefaultRevocationReason = "SESSION_REVOKED";

    public async Task<bool> ValidateAndTrackAsync(
        ClaimsPrincipal principal,
        JwtSecurityToken token,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        if (!TryReadUserId(principal, out var playerAccountId))
        {
            return true;
        }

        var jti = ReadJti(token);
        if (string.IsNullOrWhiteSpace(jti))
        {
            return true;
        }

        var now = DateTime.UtcNow;
        var player = await db.PlayerAccounts.FirstOrDefaultAsync(account => account.Id == playerAccountId, cancellationToken);
        if (player is null)
        {
            return true;
        }

        if (player.SessionRevokedBeforeUtc.HasValue && token.ValidFrom <= player.SessionRevokedBeforeUtc.Value)
        {
            return false;
        }

        var tokenRevoked = await db.MasterRevokedTokens
            .AsNoTracking()
            .AnyAsync(revoked => revoked.Jti == jti && revoked.ExpiresAtUtc > now, cancellationToken);
        if (tokenRevoked)
        {
            return false;
        }

        var session = await db.MasterPlayerSessions.FirstOrDefaultAsync(candidate => candidate.Jti == jti, cancellationToken);
        if (session?.RevokedAtUtc is not null)
        {
            return false;
        }

        if (session is null)
        {
            session = new MasterPlayerSession
            {
                Jti = jti,
                PlayerAccountId = playerAccountId,
                IssuedAtUtc = token.ValidFrom,
                ExpiresAtUtc = token.ValidTo,
                LastSeenAtUtc = now,
                LastSeenIpAddress = ResolveClientIpAddress(httpContext),
                UserAgent = ResolveUserAgent(httpContext),
            };
            db.MasterPlayerSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        if (session.ExpiresAtUtc <= now)
        {
            return false;
        }

        if ((now - session.LastSeenAtUtc) >= TimeSpan.FromSeconds(30))
        {
            session.LastSeenAtUtc = now;
            session.LastSeenIpAddress = ResolveClientIpAddress(httpContext);
            session.UserAgent = ResolveUserAgent(httpContext);
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task RevokeCurrentAsync(ClaimsPrincipal principal, JwtSecurityToken token, CancellationToken cancellationToken)
    {
        if (!TryReadUserId(principal, out var playerAccountId))
        {
            return;
        }

        await RevokeTokenAsync(playerAccountId, ReadJti(token), token.ValidTo, DefaultRevocationReason, cancellationToken);
    }

    public async Task RevokeAllForPlayerAsync(Guid playerAccountId, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var player = await db.PlayerAccounts.FirstOrDefaultAsync(account => account.Id == playerAccountId, cancellationToken);
        if (player is null)
        {
            return;
        }

        player.SessionRevokedBeforeUtc = now;

        var sessions = await db.MasterPlayerSessions
            .Where(session => session.PlayerAccountId == playerAccountId && session.ExpiresAtUtc > now)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            if (session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = now;
                session.RevokedReason = reason;
            }

            if (!await db.MasterRevokedTokens.AnyAsync(revoked => revoked.Jti == session.Jti, cancellationToken))
            {
                db.MasterRevokedTokens.Add(new MasterRevokedToken
                {
                    Jti = session.Jti,
                    PlayerAccountId = playerAccountId,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                    RevokedAtUtc = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeOtherSessionsForPlayerAsync(Guid playerAccountId, string currentJti, string reason, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await db.MasterPlayerSessions
            .Where(session => session.PlayerAccountId == playerAccountId && session.ExpiresAtUtc > now && session.Jti != currentJti)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            if (session.RevokedAtUtc is null)
            {
                session.RevokedAtUtc = now;
                session.RevokedReason = reason;
            }

            if (!await db.MasterRevokedTokens.AnyAsync(revoked => revoked.Jti == session.Jti, cancellationToken))
            {
                db.MasterRevokedTokens.Add(new MasterRevokedToken
                {
                    Jti = session.Jti,
                    PlayerAccountId = playerAccountId,
                    ExpiresAtUtc = session.ExpiresAtUtc,
                    RevokedAtUtc = now,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<MasterPlayerSessionSummary>> GetActiveSessionsAsync(Guid playerAccountId, string? currentJti, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var sessions = await db.MasterPlayerSessions
            .AsNoTracking()
            .Where(session => session.PlayerAccountId == playerAccountId && session.ExpiresAtUtc > now)
            .OrderByDescending(session => session.LastSeenAtUtc)
            .ToListAsync(cancellationToken);

        return sessions.Select(session => new MasterPlayerSessionSummary
        {
            Jti = session.Jti,
            Device = session.UserAgent ?? "Unknown device",
            IpAddress = session.LastSeenIpAddress ?? "Unknown",
            LastSeenAtUtc = session.LastSeenAtUtc,
            IssuedAtUtc = session.IssuedAtUtc,
            ExpiresAtUtc = session.ExpiresAtUtc,
            IsCurrent = string.Equals(session.Jti, currentJti, StringComparison.Ordinal),
            IsRevoked = session.RevokedAtUtc.HasValue,
        }).ToList();
    }

    public async Task CleanupExpiredAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var revoked = await db.MasterRevokedTokens
            .Where(token => token.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (revoked.Count > 0)
        {
            db.MasterRevokedTokens.RemoveRange(revoked);
        }

        var expiredSessions = await db.MasterPlayerSessions
            .Where(session => session.ExpiresAtUtc <= now)
            .ToListAsync(cancellationToken);
        if (expiredSessions.Count > 0)
        {
            db.MasterPlayerSessions.RemoveRange(expiredSessions);
        }

        if (revoked.Count > 0 || expiredSessions.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task RevokeTokenAsync(
        Guid playerAccountId,
        string jti,
        DateTime expiresAtUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (!await db.MasterRevokedTokens.AnyAsync(revoked => revoked.Jti == jti, cancellationToken))
        {
            db.MasterRevokedTokens.Add(new MasterRevokedToken
            {
                Jti = jti,
                PlayerAccountId = playerAccountId,
                ExpiresAtUtc = expiresAtUtc,
                RevokedAtUtc = now,
            });
        }

        var session = await db.MasterPlayerSessions.FirstOrDefaultAsync(candidate => candidate.Jti == jti, cancellationToken);
        if (session is not null && session.RevokedAtUtc is null)
        {
            session.RevokedAtUtc = now;
            session.RevokedReason = reason;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool TryReadUserId(ClaimsPrincipal principal, out Guid playerAccountId)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out playerAccountId);
    }

    private static string ReadJti(JwtSecurityToken token)
    {
        var claim = token.Claims.FirstOrDefault(candidate => candidate.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (!string.IsNullOrWhiteSpace(claim))
        {
            return claim;
        }

        return token.Id;
    }

    private static string? ResolveClientIpAddress(HttpContext httpContext)
    {
        var forwarded = httpContext.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrWhiteSpace(forwarded))
        {
            return forwarded.Split(',')[0].Trim();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static string? ResolveUserAgent(HttpContext httpContext)
    {
        var userAgent = httpContext.Request.Headers.UserAgent.ToString().Trim();
        return string.IsNullOrWhiteSpace(userAgent) ? null : userAgent;
    }
}

public sealed class MasterPlayerSessionSummary
{
    public string Jti { get; init; } = string.Empty;
    public string Device { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public DateTime LastSeenAtUtc { get; init; }
    public DateTime IssuedAtUtc { get; init; }
    public DateTime ExpiresAtUtc { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsRevoked { get; init; }
}
