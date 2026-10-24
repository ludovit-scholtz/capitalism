namespace MasterApi;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Security;
using MasterApi.Utilities;
using MasterApi.Utilities.Discord;
using Capitalism.Shared.Security;
using HotChocolate.AspNetCore;
using HotChocolate.CostAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public partial class Program
{
    private static void ConfigurePipeline(WebApplication app, StartupSettings settings, ILogger startupLogger)
    {
        var forwardedHeadersEnabled = settings.ForwardedHeadersEnabled;

        var appCorsAllowedOrigins = CorsPolicyHelper.ResolveAllowedOrigins(app.Configuration);
        var corsRejectAllCrossOrigin = CorsPolicyHelper.IsNonDevelopmentMisconfigured(app.Environment, appCorsAllowedOrigins);

        if (corsRejectAllCrossOrigin)
        {
            startupLogger.LogWarning(
                "{WarningMessage} Environment={EnvironmentName}",
                CorsPolicyHelper.MisconfiguredWarningMessage,
                app.Environment.EnvironmentName);

            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.ContainsKey("Origin"))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                await next();
            });
        }

        if (forwardedHeadersEnabled)
        {
            app.UseForwardedHeaders();
        }

        app.UseCors("frontend");
        app.UseMiddleware<AuthRateLimitMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<GraphQlRequestSecurityMiddleware>();
    }

    private static void MapEndpoints(WebApplication app)
    {
        app.MapGet("/", () => Results.Ok(new
        {
            name = "Capitalism Master API",
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
            MasterDbContext db,
            IOptions<GameAdministrationOptions> gameAdministrationOptions,
            IJwtSessionRevocationService revocationService) =>
        {
            var email = httpContext.User.FindFirstValue(ClaimTypes.Email)?.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Results.Unauthorized();
            }

            var rootEmails = gameAdministrationOptions.Value.RootAdministratorEmails
                .Select(candidate => candidate?.Trim().ToLowerInvariant())
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
                .Cast<string>()
                .ToHashSet(StringComparer.Ordinal);
            var hasGlobalAdminRole = await db.GlobalGameAdminGrants
                .AsNoTracking()
                .AnyAsync(grant => grant.Email == email, httpContext.RequestAborted);
            if (!hasGlobalAdminRole && !rootEmails.Contains(email))
            {
                return Results.Forbid();
            }

            await revocationService.RevokeAllForPlayerAsync(playerId, "ADMIN_REVOKE_ALL", httpContext.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/auth/forgot-password", async (
            ForgotPasswordRequest request,
            IOptions<AuthOptions> authOptions,
            IPasswordResetThrottleService throttle,
            PasswordResetService passwordResetService,
            IPasswordResetEmailSender emailSender,
            CancellationToken cancellationToken) =>
        {
            if (!authOptions.Value.PasswordAuthEnabled)
            {
                return Results.Json(new
                {
                    message = "Password reset is disabled on this server. Please use the identity provider sign-in flow.",
                    code = "METHOD_NOT_ALLOWED",
                }, statusCode: StatusCodes.Status405MethodNotAllowed);
            }

            var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedEmail))
            {
                return Results.BadRequest(new
                {
                    message = "Email is required.",
                    code = "INVALID_EMAIL",
                });
            }

            if (throttle.IsRateLimited(normalizedEmail))
            {
                return Results.Json(new
                {
                    message = "Too many reset requests. Please wait before trying again.",
                    code = "RATE_LIMIT_EXCEEDED",
                }, statusCode: StatusCodes.Status429TooManyRequests);
            }

            throttle.RecordRequest(normalizedEmail);
            var issueResult = await passwordResetService.IssueResetTokenAsync(normalizedEmail, cancellationToken);

            if (issueResult.AccountExists && issueResult.RawToken is not null && issueResult.RecipientEmail is not null)
            {
                var resetLink = PasswordResetService.BuildResetLink(authOptions.Value.PasswordResetFrontendUrl, issueResult.RawToken);
                await emailSender.SendPasswordResetEmailAsync(
                    issueResult.RecipientEmail,
                    issueResult.RecipientDisplayName ?? "player",
                    resetLink,
                    cancellationToken);
            }

            return Results.Ok(new
            {
                message = "If an account exists, a reset link has been sent.",
            });
        });

        app.MapPost("/auth/reset-password", async (
            ResetPasswordRequest request,
            IOptions<AuthOptions> authOptions,
            PasswordResetService passwordResetService,
            CancellationToken cancellationToken) =>
        {
            if (!authOptions.Value.PasswordAuthEnabled)
            {
                return Results.Json(new
                {
                    message = "Password reset is disabled on this server. Please use the identity provider sign-in flow.",
                    code = "METHOD_NOT_ALLOWED",
                }, statusCode: StatusCodes.Status405MethodNotAllowed);
            }

            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return Results.BadRequest(new
                {
                    message = "Reset token is required.",
                    code = "RESET_TOKEN_REQUIRED",
                });
            }

            try
            {
                await passwordResetService.ResetPasswordAsync(request.Token, request.NewPassword, cancellationToken);
                return Results.Ok(new
                {
                    message = "Password has been reset successfully.",
                });
            }
            catch (PasswordResetException exception)
            {
                return Results.Json(new
                {
                    message = exception.Message,
                    code = exception.Code,
                }, statusCode: exception.StatusCode);
            }
        });
    }

    private static void MapGraphQlEndpoint(WebApplication app)
    {
        app.MapGraphQL().WithOptions(options =>
        {
            options.EnableSchemaRequests = app.Environment.IsDevelopment();
            options.Tool.Enable = app.Environment.IsDevelopment();
        });
    }
}
