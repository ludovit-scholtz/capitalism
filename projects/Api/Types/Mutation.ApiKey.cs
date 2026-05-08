using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    private const int MaxApiKeysPerPlayer = 10;

    /// <summary>
    /// Generates a new personal API key for the authenticated player.
    /// Returns the plaintext key exactly once — it cannot be retrieved again.
    /// </summary>
    [Authorize]
    public async Task<GenerateApiKeyPayload> GenerateApiKey(
        GenerateApiKeyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        var trimmedName = (input.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmedName))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("API key name is required.")
                    .SetCode("VALIDATION_ERROR")
                    .Build());
        }

        if (trimmedName.Length > 80)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("API key name must be 80 characters or fewer.")
                    .SetCode("VALIDATION_ERROR")
                    .Build());
        }

        var existingCount = await db.PlayerApiKeys
            .CountAsync(k => k.PlayerId == playerId && k.RevokedAtUtc == null, ct);

        if (existingCount >= MaxApiKeysPerPlayer)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"You can have at most {MaxApiKeysPerPlayer} active API keys.")
                    .SetCode("API_KEY_LIMIT_REACHED")
                    .Build());
        }

        var (plaintext, hash) = ApiKeyAuthMiddleware.GenerateNewKey();

        var apiKey = new PlayerApiKey
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = trimmedName,
            KeyHash = hash,
            CreatedAtUtc = DateTime.UtcNow,
        };

        db.PlayerApiKeys.Add(apiKey);
        await db.SaveChangesAsync(ct);

        return new GenerateApiKeyPayload
        {
            ApiKey = new ApiKeyResult
            {
                Id = apiKey.Id,
                Name = apiKey.Name,
                CreatedAtUtc = apiKey.CreatedAtUtc,
                LastUsedAtUtc = apiKey.LastUsedAtUtc,
                TotalCallCount = apiKey.TotalCallCount,
            },
            // Shown exactly once.
            PlaintextKey = plaintext,
        };
    }

    /// <summary>
    /// Revokes an existing API key so it can no longer be used.
    /// Only the owning player can revoke their own keys.
    /// </summary>
    [Authorize]
    public async Task<bool> RevokeApiKey(
        RevokeApiKeyInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var playerId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        var apiKey = await db.PlayerApiKeys
            .FirstOrDefaultAsync(k => k.Id == input.KeyId && k.PlayerId == playerId, ct);

        if (apiKey is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("API key not found or does not belong to you.")
                    .SetCode("NOT_FOUND")
                    .Build());
        }

        if (apiKey.RevokedAtUtc is not null)
        {
            return true; // Already revoked — idempotent.
        }

        apiKey.RevokedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }
}

// ─── Input / Payload types ───────────────────────────────────────────────────

public sealed record GenerateApiKeyInput(string? Name);

public sealed record RevokeApiKeyInput(Guid KeyId);

public sealed class GenerateApiKeyPayload
{
    /// <summary>Summary of the newly created key (without the secret).</summary>
    public ApiKeyResult ApiKey { get; init; } = null!;

    /// <summary>The actual secret key value. Only returned once, never persisted.</summary>
    public string PlaintextKey { get; init; } = string.Empty;
}

public sealed class ApiKeyResult
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; }
    public DateTime? LastUsedAtUtc { get; init; }
    public long TotalCallCount { get; init; }
    public DateTime? RevokedAtUtc { get; init; }
}
