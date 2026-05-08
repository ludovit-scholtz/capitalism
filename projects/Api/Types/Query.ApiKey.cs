using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the API keys for the currently authenticated player (active keys only).
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

        return keys.Select(k => new ApiKeyResult
        {
            Id = k.Id,
            Name = k.Name,
            CreatedAtUtc = k.CreatedAtUtc,
            LastUsedAtUtc = k.LastUsedAtUtc,
            TotalCallCount = k.TotalCallCount,
            RevokedAtUtc = k.RevokedAtUtc,
        }).ToList();
    }

    /// <summary>
    /// Admin: returns API key usage statistics across all players (last 24h and last 7d call counts).
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
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
