using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Capitalism.Shared.Security;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Api.Security;

public sealed class AuthenticatedPlayerClaimsSyncService(
    AppDbContext db,
    IHttpClientFactory httpClientFactory,
    IHttpContextAccessor httpContextAccessor,
    IOptions<MasterServerRegistrationOptions> masterOptions,
    IMemoryCache cache,
    ILogger<AuthenticatedPlayerClaimsSyncService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task SyncAsync(
        ClaimsPrincipal principal,
        ClaimsIdentity identity,
        CancellationToken cancellationToken = default)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var normalizedEmail = email.ToLowerInvariant();
        var subjectClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var claimedDisplayName = principal.FindFirstValue(ClaimsPrincipalExtensions.EffectivePlayerNameClaimType);
        if (string.IsNullOrWhiteSpace(claimedDisplayName))
        {
            if (TokenBoundaryClaims.IsMasterToken(principal))
            {
                // Master tokens carry the already-provisioned public alias from MasterApi.
                claimedDisplayName = principal.FindFirstValue(ClaimTypes.Name);
            }
            else if (!string.Equals(TokenBoundaryClaims.GetTokenType(principal), TokenBoundaryClaims.TokenTypeGame, StringComparison.OrdinalIgnoreCase))
            {
                claimedDisplayName = await TryGetMasterDisplayNameAsync(normalizedEmail, cancellationToken);
                if (string.IsNullOrWhiteSpace(claimedDisplayName))
                {
                    logger.LogWarning(
                        "Falling back to generated display name alias for player hash {PlayerHash} because master display name could not be resolved.",
                        HashForLog(normalizedEmail));
                }
            }
        }

        var resolvedDisplayName = PlayerDisplayNameProvisioning.ResolveDisplayName(
            claimedDisplayName,
            normalizedEmail,
            subjectClaim);

        var player = await db.Players.FirstOrDefaultAsync(
            candidate => candidate.Email == email || candidate.Email.ToLower() == normalizedEmail,
            cancellationToken);

        var changed = false;
        var createdPlayer = false;
        if (player is null)
        {
            var playerId = Guid.TryParse(subjectClaim, out var parsedId) ? parsedId : Guid.NewGuid();

            player = new Player
            {
                Id = playerId,
                Email = normalizedEmail,
                DisplayName = resolvedDisplayName,
                Role = PlayerRole.Player,
                ActiveAccountType = AccountContextType.Person,
                CreatedAtUtc = DateTime.UtcNow,
            };
            player.PasswordHash = new PasswordHasher<Player>().HashPassword(player, Guid.NewGuid().ToString("N"));

            db.Players.Add(player);
            changed = true;
            createdPlayer = true;
        }
        else
        {
            if (!string.Equals(player.Email, normalizedEmail, StringComparison.Ordinal))
            {
                player.Email = normalizedEmail;
                changed = true;
            }

            if (PlayerDisplayNameProvisioning.ShouldReplaceExistingDisplayName(player.DisplayName, normalizedEmail))
            {
                player.DisplayName = resolvedDisplayName;
                changed = true;
            }
        }

        var settlementAccount = createdPlayer
            ? await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(db, player, 200_000m, cancellationToken)
            : await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
        if (db.Entry(settlementAccount).State == EntityState.Added)
        {
            changed = true;
        }

        if (changed)
        {
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (createdPlayer && IsDuplicatePlayerEmailViolation(exception))
            {
                // Another concurrent request provisioned the same email first. Reload and continue.
                db.ChangeTracker.Clear();
                player = await db.Players.FirstOrDefaultAsync(
                    candidate => candidate.Email == normalizedEmail || candidate.Email.ToLower() == normalizedEmail,
                    cancellationToken)
                    ?? throw new InvalidOperationException("Player email conflict detected but no matching player was found after reload.");

                await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(db, player, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        EnsureClaim(identity, ClaimsPrincipalExtensions.AuthenticatedActorPlayerIdClaimType, player.Id.ToString());
        if (!principal.HasClaim(claim => claim.Type == ClaimsPrincipalExtensions.EffectivePlayerIdClaimType))
        {
            EnsureClaim(identity, ClaimsPrincipalExtensions.EffectivePlayerIdClaimType, player.Id.ToString());
        }

        if (!principal.HasClaim(claim => claim.Type == ClaimsPrincipalExtensions.EffectivePlayerEmailClaimType))
        {
            EnsureClaim(identity, ClaimsPrincipalExtensions.EffectivePlayerEmailClaimType, player.Email);
        }

        if (!principal.HasClaim(claim => claim.Type == ClaimsPrincipalExtensions.EffectivePlayerNameClaimType))
        {
            EnsureClaim(identity, ClaimsPrincipalExtensions.EffectivePlayerNameClaimType, player.DisplayName);
        }

        if (TokenBoundaryClaims.IsMasterToken(principal))
        {
            foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
            {
                identity.RemoveClaim(claim);
            }

            EnsureClaim(identity, ClaimTypes.Role, PlayerRole.Player);
        }
        else if (!principal.HasClaim(claim => claim.Type == ClaimTypes.Role))
        {
            EnsureClaim(identity, ClaimTypes.Role, player.Role);
        }
    }

    private static void EnsureClaim(ClaimsIdentity identity, string type, string value)
    {
        if (!identity.HasClaim(type, value))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

    private static bool IsDuplicatePlayerEmailViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgres.ConstraintName, "IX_Players_Email", StringComparison.Ordinal);

    private async Task<string?> TryGetMasterDisplayNameAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(GetMasterDisplayNameCacheKey(normalizedEmail), out string? cachedDisplayName))
        {
            return cachedDisplayName;
        }

        if (string.IsNullOrWhiteSpace(masterOptions.Value.ApiUrl))
        {
            return null;
        }

        var bearerToken = TryGetBearerTokenFromRequest();
        if (string.IsNullOrWhiteSpace(bearerToken))
        {
            return null;
        }

        try
        {
            var client = httpClientFactory.CreateClient("master-server");
            using var request = new HttpRequestMessage(HttpMethod.Post, masterOptions.Value.ApiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            request.Content = JsonContent.Create(new
            {
                query = "{ me { displayName } }"
            });

            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "Master display name lookup failed for player hash {PlayerHash} with status {StatusCode} via {MasterApiUrl}.",
                    HashForLog(normalizedEmail),
                    (int)response.StatusCode,
                    masterOptions.Value.ApiUrl);
                return null;
            }

            var payload = await response.Content.ReadFromJsonAsync<MasterMeGraphQlResponse>(JsonOptions, cancellationToken);
            if (payload?.Errors is { Count: > 0 })
            {
                logger.LogWarning("Master display name lookup returned GraphQL error: {GraphQlErrorMessage}", payload.Errors[0].Message);
                return null;
            }

            var masterDisplayName = payload?.Data?.Me?.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(masterDisplayName))
            {
                return null;
            }

            cache.Set(GetMasterDisplayNameCacheKey(normalizedEmail), masterDisplayName, TimeSpan.FromMinutes(5));
            return masterDisplayName;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Master display name lookup failed.");
            return null;
        }
    }

    private string? TryGetBearerTokenFromRequest()
    {
        var authorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorizationHeader)
            || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static string GetMasterDisplayNameCacheKey(string normalizedEmail)
        => $"master-display-name:{normalizedEmail}";

    private static string HashForLog(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..6]);
    }

    private sealed class MasterMeGraphQlResponse
    {
        public MasterMePayload? Data { get; init; }

        public List<MasterMeError>? Errors { get; init; }
    }

    private sealed class MasterMePayload
    {
        public MasterMeResult? Me { get; init; }
    }

    private sealed class MasterMeResult
    {
        public string? DisplayName { get; init; }
    }

    private sealed class MasterMeError
    {
        public string Message { get; init; } = string.Empty;
    }
}
