using System.Security.Claims;
using MasterApi.Data;
using MasterApi.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

        var displayName = principal.FindFirstValue(ClaimTypes.Name)?.Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = email;
        }

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
        }

        await db.SaveChangesAsync(cancellationToken);

        if (!identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()));
        }
    }
}
