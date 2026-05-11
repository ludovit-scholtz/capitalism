using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the API keys for the currently authenticated player (active and revoked).
    /// </summary>
    [Authorize]
    public async Task<List<ApiKeyResult>> GetMyApiKeys(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        var keys = await db.PlayerApiKeys
            .AsNoTracking()
            .Where(k => k.PlayerId == playerId)
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(ct);

        return keys.Select(MapApiKey).ToList();
    }

    [Authorize]
    public async Task<List<ApiKeyAuditLogResult>> GetMyApiKeyAuditLog(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        int limit = 50,
        Guid? keyId = null)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;
        var sanitizedLimit = Math.Clamp(limit <= 0 ? 50 : limit, 1, 200);

        var auditQuery = db.PlayerApiKeyAuditLogs
            .AsNoTracking()
            .Include(log => log.PlayerApiKey)
            .ThenInclude(key => key!.Player)
            .Where(log => log.PlayerId == playerId);

        if (keyId.HasValue)
        {
            auditQuery = auditQuery.Where(log => log.PlayerApiKeyId == keyId.Value);
        }

        var items = await auditQuery
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(sanitizedLimit)
            .ToListAsync(ct);

        return items.Select(MapAuditLog).ToList();
    }

    [Authorize(Policy = Policies.Admin)]
    public async Task<List<AdminApiKeyResult>> GetAdminApiKeys(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        string? playerEmail = null,
        bool includeRevoked = true,
        int limit = 100)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var sanitizedFilter = playerEmail?.Trim().ToLowerInvariant();
        var sanitizedLimit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);

        var query = db.PlayerApiKeys
            .AsNoTracking()
            .Include(key => key.Player)
            .AsQueryable();

        if (!includeRevoked)
        {
            query = query.Where(key => key.RevokedAtUtc == null);
        }

        if (!string.IsNullOrWhiteSpace(sanitizedFilter))
        {
            query = query.Where(key => key.Player != null && key.Player.Email.ToLower().Contains(sanitizedFilter));
        }

        var keys = await query
            .OrderByDescending(key => key.LastUsedAtUtc ?? key.CreatedAtUtc)
            .Take(sanitizedLimit)
            .ToListAsync(ct);

        return keys.Select(key => new AdminApiKeyResult
        {
            Key = MapApiKey(key),
            PlayerId = key.PlayerId,
            PlayerEmail = key.Player?.Email ?? string.Empty,
            PlayerDisplayName = key.Player?.DisplayName ?? string.Empty,
        }).ToList();
    }

    [Authorize(Policy = Policies.Admin)]
    public async Task<List<ApiKeyAuditLogResult>> GetAdminApiKeyAuditLog(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        string? playerEmail = null,
        Guid? keyId = null,
        int limit = 100)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var sanitizedFilter = playerEmail?.Trim().ToLowerInvariant();
        var sanitizedLimit = Math.Clamp(limit <= 0 ? 100 : limit, 1, 500);

        var query = db.PlayerApiKeyAuditLogs
            .AsNoTracking()
            .Include(log => log.PlayerApiKey)
            .ThenInclude(key => key!.Player)
            .AsQueryable();

        if (keyId.HasValue)
        {
            query = query.Where(log => log.PlayerApiKeyId == keyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(sanitizedFilter))
        {
            query = query.Where(log =>
                log.PlayerApiKey != null
                && log.PlayerApiKey.Player != null
                && log.PlayerApiKey.Player.Email.ToLower().Contains(sanitizedFilter));
        }

        var items = await query
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(sanitizedLimit)
            .ToListAsync(ct);

        return items.Select(MapAuditLog).ToList();
    }

    /// <summary>
    /// Admin: returns API key usage statistics across all players (last-used ordering).
    /// </summary>
    [Authorize(Policy = Policies.Admin)]
    public async Task<List<ApiKeyUsageStat>> GetApiKeyUsageStats(
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;

        var keys = await db.PlayerApiKeys
            .AsNoTracking()
            .Include(k => k.Player)
            .OrderByDescending(k => k.LastUsedAtUtc)
            .Take(500)
            .ToListAsync(ct);

        return keys.Select(k => new ApiKeyUsageStat
        {
            KeyId = k.Id,
            KeyName = k.Name,
            PlayerEmail = k.Player?.Email ?? "unknown",
            PlayerDisplayName = k.Player?.DisplayName ?? "unknown",
            TotalCallCount = k.TotalCallCount,
            LastUsedAtUtc = k.LastUsedAtUtc,
            CreatedAtUtc = k.CreatedAtUtc,
            IsRevoked = k.RevokedAtUtc is not null,
        }).ToList();
    }

    private static ApiKeyResult MapApiKey(Data.Entities.PlayerApiKey key) => new()
    {
        Id = key.Id,
        Name = key.Name,
        CreatedAtUtc = key.CreatedAtUtc,
        LastUsedAtUtc = key.LastUsedAtUtc,
        TotalCallCount = key.TotalCallCount,
        RevokedAtUtc = key.RevokedAtUtc,
        Scopes = key.Scopes,
        CompanyIds = key.CompanyIds,
    };

    private static ApiKeyAuditLogResult MapAuditLog(Data.Entities.PlayerApiKeyAuditLog log) => new()
    {
        Id = log.Id,
        KeyId = log.PlayerApiKeyId,
        KeyName = log.PlayerApiKey?.Name ?? string.Empty,
        PlayerId = log.PlayerId,
        PlayerEmail = log.PlayerApiKey?.Player?.Email ?? string.Empty,
        PlayerDisplayName = log.PlayerApiKey?.Player?.DisplayName ?? string.Empty,
        OperationName = log.OperationName,
        OperationType = log.OperationType,
        ScopeUsed = log.ScopeUsed,
        WasAllowed = log.WasAllowed,
        DenialCode = log.DenialCode,
        DenialReason = log.DenialReason,
        AttemptedObjectId = log.AttemptedObjectId,
        IpAddress = log.IpAddress,
        SessionContext = log.SessionContext,
        OccurredAtUtc = log.OccurredAtUtc,
    };
}

public sealed class ApiKeyUsageStat
{
    public Guid KeyId { get; init; }
    public string KeyName { get; init; } = string.Empty;
    public string PlayerEmail { get; init; } = string.Empty;
    public string PlayerDisplayName { get; init; } = string.Empty;
    public long TotalCallCount { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public bool IsRevoked { get; init; }
}

public sealed class ApiKeyAuditLogResult
{
    public Guid Id { get; init; }
    public Guid KeyId { get; init; }
    public string KeyName { get; init; } = string.Empty;
    public Guid PlayerId { get; init; }
    public string PlayerEmail { get; init; } = string.Empty;
    public string PlayerDisplayName { get; init; } = string.Empty;
    public string OperationName { get; init; } = string.Empty;
    public string OperationType { get; init; } = string.Empty;
    public string ScopeUsed { get; init; } = string.Empty;
    public bool WasAllowed { get; init; }
    public string? DenialCode { get; init; }
    public string? DenialReason { get; init; }
    public string? AttemptedObjectId { get; init; }
    public string? IpAddress { get; init; }
    public string? SessionContext { get; init; }
    public DateTime OccurredAtUtc { get; init; }
}

public sealed class AdminApiKeyResult
{
    public ApiKeyResult Key { get; init; } = new();
    public Guid PlayerId { get; init; }
    public string PlayerEmail { get; init; } = string.Empty;
    public string PlayerDisplayName { get; init; } = string.Empty;
}
