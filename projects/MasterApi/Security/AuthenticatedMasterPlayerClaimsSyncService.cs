using System.Security.Claims;
using Capitalism.Shared.Security;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MasterApi.Security;

public sealed class AuthenticatedMasterPlayerClaimsSyncService(
    MasterDbContext db,
    IPasswordHasher<PlayerAccount> passwordHasher)
{
    public async Task SyncAsync(ClaimsPrincipal principal, ClaimsIdentity identity, CancellationToken cancellationToken = default)
    {
        var email = principal.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var subjectClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var displayName = PlayerDisplayNameProvisioning.ResolveDisplayName(
            principal.FindFirstValue(ClaimTypes.Name),
            email,
            subjectClaim);

        var now = DateTime.UtcNow;

        var player = await db.PlayerAccounts
            .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken);

        if (player is null)
        {
            player = new PlayerAccount
            {
                Id = Guid.NewGuid(),
                Email = email,
                DisplayName = displayName,
                CreatedAtUtc = now,
                LastLoginAtUtc = now,
            };
            player.PasswordHash = passwordHasher.HashPassword(player, Guid.NewGuid().ToString("N"));
            db.PlayerAccounts.Add(player);
        }
        else
        {
            player.LastLoginAtUtc = now;
            if (PlayerDisplayNameProvisioning.ShouldReplaceExistingDisplayName(player.DisplayName, email))
            {
                player.DisplayName = displayName;
            }
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (player.Id != Guid.Empty && IsDuplicatePlayerEmailViolation(exception))
        {
            db.ChangeTracker.Clear();

            player = await db.PlayerAccounts
                .FirstOrDefaultAsync(candidate => candidate.Email.ToLower() == email, cancellationToken)
                ?? throw new InvalidOperationException("Master player email conflict detected but no matching player was found after reload.");

            player.LastLoginAtUtc = now;
            if (PlayerDisplayNameProvisioning.ShouldReplaceExistingDisplayName(player.DisplayName, email))
            {
                player.DisplayName = displayName;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        if (!identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()));
        }
    }

    private static bool IsDuplicatePlayerEmailViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException postgres
            && postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgres.ConstraintName, "IX_PlayerAccounts_Email", StringComparison.Ordinal);
}
