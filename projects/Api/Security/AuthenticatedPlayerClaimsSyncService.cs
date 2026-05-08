using System.Security.Claims;
using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.AspNetCore.Identity;

namespace Api.Security;

public sealed class AuthenticatedPlayerClaimsSyncService(AppDbContext db)
{
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
        var claimedDisplayName = principal.FindFirstValue(ClaimsPrincipalExtensions.EffectivePlayerNameClaimType)
            ?? principal.FindFirstValue(ClaimTypes.Name)
            ?? normalizedEmail;

        var player = await db.Players.FirstOrDefaultAsync(
            candidate => candidate.Email == email || candidate.Email.ToLower() == normalizedEmail,
            cancellationToken);

        var changed = false;
        var createdPlayer = false;
        if (player is null)
        {
            var actorIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            var playerId = Guid.TryParse(actorIdClaim, out var parsedId) ? parsedId : Guid.NewGuid();
            var displayName = claimedDisplayName.Trim();

            player = new Player
            {
                Id = playerId,
                Email = normalizedEmail,
                DisplayName = displayName,
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

            // Keep an already chosen in-game alias stable across future sign-ins.
            // The player can explicitly change this value via profile settings/onboarding.
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

        if (!principal.HasClaim(claim => claim.Type == ClaimTypes.Role))
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
}
