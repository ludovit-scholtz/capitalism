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

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<MasterServerOptions>(
            builder.Configuration.GetSection(MasterServerOptions.SectionName));

        builder.Services.Configure<GameAdministrationOptions>(
            builder.Configuration.GetSection(GameAdministrationOptions.SectionName));

        builder.Services.Configure<JwtOptions>(
            builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<BiatecOidcOptions>(
            builder.Configuration.GetSection(BiatecOidcOptions.SectionName));
        builder.Services.Configure<AuthOptions>(
            builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.Configure<ReverseProxyOptions>(
            builder.Configuration.GetSection(ReverseProxyOptions.SectionName));
        builder.Services.Configure<GraphQlSecurityOptions>(
            builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();
        var graphQlSecurityOptions = builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName).Get<GraphQlSecurityOptions>()
            ?? new GraphQlSecurityOptions();
        var reverseProxyOptions = builder.Configuration.GetSection(ReverseProxyOptions.SectionName).Get<ReverseProxyOptions>()
            ?? new ReverseProxyOptions();
        var graphQlMaxDepth = Math.Max(1, graphQlSecurityOptions.MaxDepth);
        var graphQlMaxComplexity = Math.Max(1, graphQlSecurityOptions.MaxComplexity);
        var graphQlMaxPageSize = Math.Max(1, graphQlSecurityOptions.MaxPageSize);
        var forwardedHeadersEnabled = ForwardedHeadersConfiguration.TryBuild(reverseProxyOptions, out var forwardedHeadersOptions);
        if (forwardedHeadersEnabled)
        {
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = forwardedHeadersOptions.ForwardedHeaders;
                options.ForwardLimit = forwardedHeadersOptions.ForwardLimit;
                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();
                foreach (var proxy in forwardedHeadersOptions.KnownProxies)
                {
                    options.KnownProxies.Add(proxy);
                }

                foreach (var network in forwardedHeadersOptions.KnownIPNetworks)
                {
                    options.KnownIPNetworks.Add(network);
                }
            });
        }
        var biatecOidcOptions = builder.Configuration.GetSection(BiatecOidcOptions.SectionName).Get<BiatecOidcOptions>()
            ?? new BiatecOidcOptions();
        static string NormalizeIssuer(string issuer) => issuer.Trim().TrimEnd('/');
        var knownBiatecIssuers = new[]
        {
            biatecOidcOptions.Issuer,
            biatecOidcOptions.Authority,
        }
            .Where(issuer => !string.IsNullOrWhiteSpace(issuer))
            .SelectMany(issuer =>
            {
                var normalized = NormalizeIssuer(issuer);
                return new[] { normalized, normalized + "/" };
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var startupLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            logging.AddConsole();
        });
        var startupLogger = startupLoggerFactory.CreateLogger("JwtSigningKeyStartupGuard");

        if (JwtSigningKeyStartupGuard.TryGetUnsafeReason(
                jwtOptions.SigningKey,
                [JwtOptions.DefaultSigningKey],
                out var unsafeSigningKeyReason))
        {
            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                startupLogger.LogWarning(
                    "Startup with insecure Jwt signing key is allowed only in Development/Testing. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeSigningKeyReason,
                    "Jwt__SigningKey");
            }
            else
            {
                startupLogger.LogCritical(
                    "Blocking startup because Jwt signing key is insecure. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeSigningKeyReason,
                    "Jwt__SigningKey");
                throw new InvalidOperationException(
                    "Jwt:SigningKey is set to a placeholder or insecure value. " +
                    "Set a strong secret via environment variable 'Jwt__SigningKey' before starting outside Development. " +
                    $"Validation reason: {unsafeSigningKeyReason}");
            }
        }

        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing"))
        {
            var masterCatalogConnectionString = builder.Configuration.GetConnectionString("MasterCatalog");
            if (RequiredSecretsStartupGuard.TryGetUnsafeConnectionStringReason(masterCatalogConnectionString, out var unsafeMasterConnectionReason))
            {
                startupLogger.LogCritical(
                    "Blocking startup because ConnectionStrings:MasterCatalog is insecure. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeMasterConnectionReason,
                    "ConnectionStrings__MasterCatalog");
                throw new InvalidOperationException(
                    "ConnectionStrings:MasterCatalog is missing or uses a placeholder value. " +
                    "Set a real PostgreSQL connection string via environment variable 'ConnectionStrings__MasterCatalog' before starting outside Development. " +
                    $"Validation reason: {unsafeMasterConnectionReason}");
            }
        }

        if (!builder.Environment.IsDevelopment() && !builder.Environment.IsEnvironment("Testing")
            && RequiredSecretsStartupGuard.TryGetUnsafeRootAdministratorEmailsReason(
                (builder.Configuration.GetSection(GameAdministrationOptions.SectionName).Get<GameAdministrationOptions>()
                    ?? new GameAdministrationOptions()).RootAdministratorEmails,
                out var unsafeRootAdministratorReason))
        {
            startupLogger.LogCritical(
                "Blocking startup because GameAdministration:RootAdministratorEmails is insecure. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariablePattern={OverrideEnvironmentVariablePattern}",
                builder.Environment.EnvironmentName,
                unsafeRootAdministratorReason,
                "GameAdministration__RootAdministratorEmails__0");
            throw new InvalidOperationException(
                "GameAdministration:RootAdministratorEmails is missing or uses placeholder values. " +
                "Set root admin emails via environment variables like 'GameAdministration__RootAdministratorEmails__0' before starting outside Development. " +
                $"Validation reason: {unsafeRootAdministratorReason}");
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var allowedOrigins = CorsPolicyHelper.ResolveAllowedOrigins(builder.Configuration);

                if (CorsPolicyHelper.IsDevelopmentOpenPolicy(builder.Environment))
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                if (allowedOrigins.Length == 0)
                {
                    policy.SetIsOriginAllowed(static _ => false).AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            });
        });

        builder.Services.AddDbContext<MasterDbContext>(options =>
        {
            if (builder.Environment.IsEnvironment("Testing"))
            {
                options.UseInMemoryDatabase(builder.Configuration.GetConnectionString("MasterCatalog")
                    ?? $"masterapi-tests-{Guid.NewGuid():N}");
            }
            else
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("MasterCatalog")
                    ?? throw new InvalidOperationException("Connection string 'MasterCatalog' is missing."));
            }
        });

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

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("master-server");
        builder.Services.AddHttpClient<PersonalAccountNamePropagationService>();
        builder.Services.AddScoped<AuthenticatedMasterPlayerClaimsSyncService>();
        builder.Services.AddScoped<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();
        builder.Services.AddScoped<MasterRankingService>();
        builder.Services.AddScoped<RankingTelemetryValidator>();
        builder.Services.AddScoped<PasswordResetService>();
        builder.Services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
        builder.Services.AddHostedService<MasterRankingSchedulerHostedService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ILoginThrottleService, LoginThrottleService>();
        builder.Services.AddSingleton<IPasswordResetThrottleService, PasswordResetThrottleService>();
        builder.Services.AddScoped<IJwtSessionRevocationService, JwtSessionRevocationService>();
        builder.Services.AddHostedService<JwtSessionCleanupHostedService>();

        builder.Services
            .AddGraphQLServer()
            .ModifyRequestOptions(options =>
            {
                options.IncludeExceptionDetails = builder.Environment.IsDevelopment()
                    || builder.Environment.IsEnvironment("Testing");
            })
            .AddMaxExecutionDepthRule(
                graphQlMaxDepth,
                skipIntrospectionFields: false,
                allowRequestOverrides: false,
                (_, _) => true)
            .AddCostAnalyzer()
            .ModifyCostOptions(options =>
            {
                options.MaxFieldCost = graphQlMaxComplexity;
                options.MaxTypeCost = graphQlMaxComplexity;
                options.EnforceCostLimits = true;
                options.ApplyCostDefaults = true;
                options.SkipAnalyzer = false;
            })
            .ModifyPagingOptions(options =>
            {
                options.MaxPageSize = graphQlMaxPageSize;
            })
            .AddAuthorization()
            .AddQueryType<MasterApi.Types.Query>()
            .AddMutationType<MasterApi.Types.Mutation>();

        builder.Services.AddScoped<MasterDbInitializer>();

        var app = builder.Build();
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

        app.MapGraphQL().WithOptions(new GraphQLServerOptions
        {
            EnableSchemaRequests = builder.Environment.IsDevelopment(),
            Tool =
            {
                Enable = builder.Environment.IsDevelopment(),
            },
        });

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<MasterDbInitializer>();
            await initializer.InitializeAsync();
        }

        await app.RunAsync();

        static void StampMasterPrivilegeEligibility(ClaimsIdentity identity, ClaimsPrincipal principal, string expectedIssuer)
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

        static bool TryReadRequestJwt(HttpContext httpContext, out JwtSecurityToken token)
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

        static bool TryReadRequestToken(HttpContext httpContext, out string token)
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

        static bool TryResolveJwtSecurityToken(SecurityToken? token, out JwtSecurityToken jwt)
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

    private sealed record ForgotPasswordRequest(
        [property: JsonPropertyName("email")] string Email);

    private sealed record ResetPasswordRequest(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("newPassword")] string NewPassword);
}
