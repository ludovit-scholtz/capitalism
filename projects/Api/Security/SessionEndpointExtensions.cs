using System.Security.Claims;
using Api.Data;
using Api.Utilities;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using static Api.Security.RequestTokenReader;

namespace Api.Security;

/// <summary>Root, health and session endpoints (list, logout, logout-all, session probe, admin revoke-all).</summary>
internal static class SessionEndpointExtensions
{
    internal static WebApplication MapSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            name = "Capitalism 5 Game API",
            graphql = "/graphql",
            health = "/healthz"
        }));

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
        app.MapGet("/auth/sessions", async (HttpContext httpContext, IJwtSessionRevocationService revocationService) =>
        {
            if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId))
            {
                return Results.Unauthorized();
            }

            var currentJti = httpContext.User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti);
            var sessions = await revocationService.GetActiveSessionsAsync(playerId, currentJti, httpContext.RequestAborted);
            return Results.Ok(new { sessions });
        }).RequireAuthorization();

        app.MapPost("/auth/logout", async (HttpContext httpContext, IJwtSessionRevocationService revocationService) =>
        {
            if (!TryReadRequestJwt(httpContext, out var jwt))
            {
                return Results.Unauthorized();
            }

            await revocationService.RevokeCurrentAsync(httpContext.User, jwt, httpContext.RequestAborted);
            var hostEnvironment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            AuthSessionCookieService.ClearSessionCookies(httpContext, hostEnvironment);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/auth/logout-all", async (HttpContext httpContext, IJwtSessionRevocationService revocationService) =>
        {
            if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId))
            {
                return Results.Unauthorized();
            }

            if (!TryReadRequestJwt(httpContext, out var jwt))
            {
                return Results.Unauthorized();
            }

            var currentJti = jwt.Claims.FirstOrDefault(claim => claim.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value ?? jwt.Id;
            if (string.IsNullOrWhiteSpace(currentJti))
            {
                return Results.Unauthorized();
            }

            await revocationService.RevokeOtherSessionsForPlayerAsync(playerId, currentJti, "PLAYER_LOGOUT_ALL", httpContext.RequestAborted);
            var hostEnvironment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            AuthSessionCookieService.ClearSessionCookies(httpContext, hostEnvironment);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/auth/session", (HttpContext httpContext) =>
        {
            if (!TryReadRequestJwt(httpContext, out var jwt))
            {
                return Results.Unauthorized();
            }

            var hostEnvironment = httpContext.RequestServices.GetRequiredService<IHostEnvironment>();
            AuthSessionCookieService.SetSessionCookies(httpContext, hostEnvironment, jwt.RawData, jwt.ValidTo);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/admin/sessions/{playerId:guid}/revoke-all", async (
            Guid playerId,
            HttpContext httpContext,
            IJwtSessionRevocationService revocationService) =>
        {
            await revocationService.RevokeAllForPlayerAsync(playerId, "ADMIN_REVOKE_ALL", httpContext.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization(Policies.Admin);

        return app;
    }
}
