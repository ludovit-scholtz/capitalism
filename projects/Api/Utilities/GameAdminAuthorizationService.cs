using System.Security.Claims;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

public sealed record GameAdminAccessContext(
    Player ActorPlayer,
    bool IsLocalAdmin,
    bool HasGlobalAdminRole,
    bool IsRootAdministrator,
    bool CanAccessAdminDashboard,
    bool IsImpersonating);

public sealed class GameAdminAuthorizationService(IMasterGameAdministrationService masterGameAdministrationService)
{
    public async Task<GameAdminAccessContext> GetAccessContextAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = principal.GetAuthenticatedActorUserId();
        var actorPlayer = await db.Players
            .AsNoTracking()
            .FirstOrDefaultAsync(player => player.Id == actorUserId, cancellationToken)
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());

        var masterAccess = await masterGameAdministrationService.GetGameAdministrationAccessAsync(actorPlayer.Email, cancellationToken);
        var isLocalAdmin = actorPlayer.Role == PlayerRole.Admin;

        return new GameAdminAccessContext(
            actorPlayer,
            isLocalAdmin,
            masterAccess.HasGlobalAdminRole,
            masterAccess.IsRootAdministrator,
            isLocalAdmin || masterAccess.CanAccessEveryGameDashboard,
            principal.IsImpersonating());
    }

    public async Task<GameAdminAccessContext> RequireAdminDashboardAccessAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var accessContext = await GetAccessContextAsync(db, principal, cancellationToken);
        if (!accessContext.CanAccessAdminDashboard)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game administrator access is required.")
                    .SetCode("ADMIN_ACCESS_REQUIRED")
                    .Build());
        }

        return accessContext;
    }

    public async Task<GameAdminAccessContext> RequireRootAccessAsync(
        AppDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken = default)
    {
        var accessContext = await GetAccessContextAsync(db, principal, cancellationToken);
        if (!accessContext.IsRootAdministrator)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Root administrator access is required.")
                    .SetCode("ROOT_ADMIN_REQUIRED")
                    .Build());
        }

        return accessContext;
    }
}