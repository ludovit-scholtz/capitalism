using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Api.Data;
using Api.Security;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Http.Features.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Api.Security;

/// <summary>
/// ASP.NET Core middleware that checks for an API key in the
/// <c>Authorization: ApiKey &lt;key&gt;</c> header (or <c>X-Api-Key</c>) and, when found,
/// resolves the associated player and injects their claims so that downstream
/// HotChocolate resolvers and <c>[Authorize]</c> attributes work identically to JWT auth.
/// </summary>
public sealed class ApiKeyAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var rawKey = ExtractApiKey(context);
        if (rawKey is not null)
        {
            await AuthenticateWithApiKeyAsync(context, db, rawKey);
        }

        await next(context);
    }

    private static string? ExtractApiKey(HttpContext context)
    {
        // Authorization: ApiKey <key>
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (authHeader is not null && authHeader.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
        {
            return authHeader["ApiKey ".Length..].Trim();
        }

        // X-Api-Key: <key>
        var xApiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(xApiKey))
        {
            return xApiKey.Trim();
        }

        return null;
    }

    private static async Task AuthenticateWithApiKeyAsync(HttpContext context, AppDbContext db, string rawKey)
    {
        var hash = ComputeHash(rawKey);

        var apiKey = await db.PlayerApiKeys
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.KeyHash == hash && k.RevokedAtUtc == null,
                context.RequestAborted);

        if (apiKey is null)
        {
            return;
        }

        var player = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == apiKey.PlayerId, context.RequestAborted);

        if (player is null)
        {
            return;
        }

        context.Items[ApiKeyRequestContext.HttpContextItemKey] = new ApiKeyRequestContext(
            apiKey.Id,
            apiKey.PlayerId,
            apiKey.Scopes ?? [],
            apiKey.CompanyIds ?? []);

        // Fire-and-forget usage tracking (best effort, non-blocking).
        _ = Task.Run(async () =>
        {
            try
            {
                await using var scope = context.RequestServices
                    .GetRequiredService<IServiceScopeFactory>()
                    .CreateAsyncScope();
                var trackDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await trackDb.PlayerApiKeys
                    .Where(k => k.Id == apiKey.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(k => k.LastUsedAtUtc, DateTime.UtcNow)
                        .SetProperty(k => k.TotalCallCount, k => k.TotalCallCount + 1),
                        CancellationToken.None);
            }
            catch
            {
                // Usage tracking is best-effort; swallow all errors here.
            }
        });

        var claims = new List<Claim>
        {
            new(ClaimsIdentity.DefaultNameClaimType, player.Id.ToString()),
            new(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new(ClaimTypes.Email, player.Email),
            new(ClaimTypes.Role, player.Role),
            new(ClaimsPrincipalExtensions.AuthenticatedActorPlayerIdClaimType, player.Id.ToString()),
            new(ClaimsPrincipalExtensions.EffectivePlayerIdClaimType, player.Id.ToString()),
            new(ClaimsPrincipalExtensions.EffectivePlayerEmailClaimType, player.Email),
            new(ClaimsPrincipalExtensions.EffectivePlayerNameClaimType, player.DisplayName),
            new(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeGame),
        };

        var identity = new ClaimsIdentity(claims, "ApiKey");
        var principal = new ClaimsPrincipal(identity);
        var authenticationFeature = context.Features.Get<IHttpAuthenticationFeature>();
        if (authenticationFeature is null)
        {
            authenticationFeature = new ApiKeyAuthenticationFeature();
            context.Features.Set(authenticationFeature);
        }

        authenticationFeature.User = principal;
    }

    /// <summary>
    /// Computes the lowercase hex-encoded SHA-256 hash of a plaintext API key.
    /// This is the same algorithm used when generating and storing the key.
    /// </summary>
    public static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a new cryptographically random API key and returns both the
    /// plaintext (shown once to the user) and the stored hash.
    /// </summary>
    public static (string PlaintextKey, string KeyHash) GenerateNewKey()
    {
        var randomBytes = new byte[32];
        RandomNumberGenerator.Fill(randomBytes);
        var plaintext = Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
        var hash = ComputeHash(plaintext);
        return (plaintext, hash);
    }

    private sealed class ApiKeyAuthenticationFeature : IHttpAuthenticationFeature
    {
        public ClaimsPrincipal? User { get; set; } = new(new ClaimsIdentity());
    }
}
