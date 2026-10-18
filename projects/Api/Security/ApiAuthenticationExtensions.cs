using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Security;
using Api.Utilities;
using Api.Types;
using Capitalism.Shared.Security;
using HotChocolate.AspNetCore;
using HotChocolate.CostAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using static Api.Security.RequestTokenReader;

namespace Api.Security;

/// <summary>Registers local-JWT, Biatec OIDC and the policy scheme that chooses between them, plus the admin authorization policy.</summary>
internal static class ApiAuthenticationExtensions
{
    private static string NormalizeIssuer(string issuer) => issuer.Trim().TrimEnd('/');

    internal static IServiceCollection AddApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        BiatecOidcOptions biatecOidcOptions,
        string[] biatecKnownIssuers)
    {
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = "dynamic-jwt";
                options.DefaultChallengeScheme = "dynamic-jwt";
            })
            .AddPolicyScheme("dynamic-jwt", "Local or Biatec JWT", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    if (!TryReadRequestToken(context, out var token))
                    {
                        return "local-jwt";
                    }

                    try
                    {
                        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
                        if (biatecOidcOptions.Enabled
                            && biatecKnownIssuers.Any(issuer =>
                                string.Equals(NormalizeIssuer(jwt.Issuer), NormalizeIssuer(issuer), StringComparison.OrdinalIgnoreCase)))
                        {
                            return "biatec-oidc";
                        }
                    }
                    catch
                    {
                        // Keep defaulting to local JWT to preserve existing behavior on malformed tokens.
                    }

                    return "local-jwt";
                };
            })
            .AddJwtBearer("local-jwt", options =>
            {
                var localJwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                    ?? new JwtOptions();

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = localJwtOptions.Issuer,
                    ValidAudience = localJwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(localJwtOptions.SigningKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (TryReadRequestToken(context.HttpContext, out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
                        {
                            var synchronizer = context.HttpContext.RequestServices.GetRequiredService<AuthenticatedPlayerClaimsSyncService>();
                            await synchronizer.SyncAsync(context.Principal, identity, context.HttpContext.RequestAborted);

                            if (context.Principal.IsImpersonating() && !TokenBoundaryClaims.HasImpersonationGrant(context.Principal))
                            {
                                context.Fail("Impersonation token is missing an explicit grant.");
                                return;
                            }

                            if (TryResolveJwtSecurityToken(context.SecurityToken, out var jwt))
                            {
                                var revocationService = context.HttpContext.RequestServices.GetRequiredService<IJwtSessionRevocationService>();
                                var isValid = await revocationService.ValidateAndTrackAsync(
                                    context.Principal,
                                    jwt,
                                    context.HttpContext,
                                    context.HttpContext.RequestAborted);
                                if (!isValid)
                                {
                                    context.HttpContext.Items["auth_error_code"] = "session_revoked";
                                    context.Fail("session_revoked");
                                }
                            }
                        }
                    },
                    OnChallenge = async context =>
                    {
                        if (!string.Equals(context.HttpContext.Items["auth_error_code"] as string, "session_revoked", StringComparison.Ordinal))
                        {
                            return;
                        }

                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            error = "session_revoked",
                            message = "Your session has been terminated. Please log in again."
                        }));
                    },
                };
            })
            .AddJwtBearer("biatec-oidc", options =>
            {
                options.Authority = biatecOidcOptions.Authority;
                options.RequireHttpsMetadata = biatecOidcOptions.RequireHttpsMetadata;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = biatecKnownIssuers,
                    ValidateAudience = true,
                    ValidAudience = biatecOidcOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
                options.MapInboundClaims = false;
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        if (TryReadRequestToken(context.HttpContext, out var token))
                        {
                            context.Token = token;
                        }
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
                        {
                            return;
                        }

                        var emailClaim = identity.FindFirst("email")?.Value;
                        if (!string.IsNullOrWhiteSpace(emailClaim) && !identity.HasClaim(claim => claim.Type == ClaimTypes.Email))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Email, emailClaim));
                        }

                        var displayNameClaim = identity.FindFirst("preferred_username")?.Value
                            ?? identity.FindFirst("name")?.Value
                            ?? emailClaim;
                        if (!string.IsNullOrWhiteSpace(displayNameClaim) && !identity.HasClaim(claim => claim.Type == ClaimTypes.Name))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.Name, displayNameClaim));
                        }

                        var subClaim = identity.FindFirst("sub")?.Value;
                        if (!string.IsNullOrWhiteSpace(subClaim) && !identity.HasClaim(claim => claim.Type == ClaimTypes.NameIdentifier))
                        {
                            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subClaim));
                        }

                        var synchronizer = context.HttpContext.RequestServices.GetRequiredService<AuthenticatedPlayerClaimsSyncService>();
                        await synchronizer.SyncAsync(context.Principal, identity, context.HttpContext.RequestAborted);

                        if (TryResolveJwtSecurityToken(context.SecurityToken, out var jwt))
                        {
                            var revocationService = context.HttpContext.RequestServices.GetRequiredService<IJwtSessionRevocationService>();
                            var isValid = await revocationService.ValidateAndTrackAsync(
                                context.Principal,
                                jwt,
                                context.HttpContext,
                                context.HttpContext.RequestAborted);
                            if (!isValid)
                            {
                                context.HttpContext.Items["auth_error_code"] = "session_revoked";
                                context.Fail("session_revoked");
                            }
                        }
                    }
                    ,
                    OnChallenge = async context =>
                    {
                        if (!string.Equals(context.HttpContext.Items["auth_error_code"] as string, "session_revoked", StringComparison.Ordinal))
                        {
                            return;
                        }

                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(JsonSerializer.Serialize(new
                        {
                            error = "session_revoked",
                            message = "Your session has been terminated. Please log in again."
                        }));
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireClaim(ClaimTypes.Role, PlayerRole.Admin));

        return services;
    }
}
