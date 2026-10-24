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
    private static void ConfigureAuthentication(WebApplicationBuilder builder, StartupSettings settings)
    {
        var jwtOptions = settings.Jwt;
        var biatecOidcOptions = settings.BiatecOidc;
        var knownBiatecIssuers = settings.KnownBiatecIssuers;

        builder.Services
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
                            && knownBiatecIssuers.Any(issuer =>
                                string.Equals(NormalizeIssuer(jwt.Issuer), NormalizeIssuer(issuer), StringComparison.OrdinalIgnoreCase)))
                        {
                            return "biatec-oidc";
                        }
                    }
                    catch
                    {
                        // Keep existing behavior and let local validation fail for malformed tokens.
                    }

                    return "local-jwt";
                };
            })
            .AddJwtBearer("local-jwt", options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
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
                            var synchronizer = context.HttpContext.RequestServices.GetRequiredService<AuthenticatedMasterPlayerClaimsSyncService>();
                            await synchronizer.SyncAsync(context.Principal, identity, context.HttpContext.RequestAborted);

                            StampMasterPrivilegeEligibility(identity, context.Principal, jwtOptions.Issuer);

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
                    }
                };
            })
            .AddJwtBearer("biatec-oidc", options =>
            {
                options.Authority = biatecOidcOptions.Authority;
                options.RequireHttpsMetadata = biatecOidcOptions.RequireHttpsMetadata;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuers = knownBiatecIssuers,
                    ValidateAudience = true,
                    ValidAudience = biatecOidcOptions.Audience,
                    ValidateLifetime = true,
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

                        var synchronizer = context.HttpContext.RequestServices.GetRequiredService<AuthenticatedMasterPlayerClaimsSyncService>();
                        await synchronizer.SyncAsync(context.Principal, identity, context.HttpContext.RequestAborted);

                        StampMasterPrivilegeEligibility(identity, context.Principal, jwtOptions.Issuer);

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
    }

    private static void StampMasterPrivilegeEligibility(ClaimsIdentity identity, ClaimsPrincipal principal, string expectedIssuer)
    {
        foreach (var claim in identity.FindAll(TokenBoundaryClaims.MasterPrivilegeEligibleClaimType).ToList())
        {
            identity.RemoveClaim(claim);
        }

        if (TokenBoundaryClaims.IsMasterPrivilegeEligible(principal, expectedIssuer))
        {
            identity.AddClaim(new Claim(TokenBoundaryClaims.MasterPrivilegeEligibleClaimType, bool.TrueString));
        }
    }

    private static bool TryReadRequestJwt(HttpContext httpContext, out JwtSecurityToken token)
    {
        token = null!;
        if (!TryReadRequestToken(httpContext, out var rawToken))
        {
            return false;
        }

        try
        {
            token = new JwtSecurityTokenHandler().ReadJwtToken(rawToken);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadRequestToken(HttpContext httpContext, out string token)
    {
        token = string.Empty;

        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var rawToken = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(rawToken))
            {
                token = rawToken;
                return true;
            }
        }

        if (httpContext.Request.Cookies.TryGetValue(AuthSessionCookieService.AccessTokenCookieName, out var cookieToken)
            && !string.IsNullOrWhiteSpace(cookieToken))
        {
            token = cookieToken.Trim();
            return true;
        }

        return false;
    }

    private static bool TryResolveJwtSecurityToken(SecurityToken? token, out JwtSecurityToken jwt)
    {
        jwt = null!;
        if (token is JwtSecurityToken concreteJwt)
        {
            jwt = concreteJwt;
            return true;
        }

        if (token is JsonWebToken jsonWebToken)
        {
            jwt = new JwtSecurityToken(jsonWebToken.EncodedToken);
            return true;
        }

        return false;
    }
}
