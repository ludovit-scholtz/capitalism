namespace Api;

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
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
        builder.Services.Configure<BiatecOidcOptions>(builder.Configuration.GetSection(BiatecOidcOptions.SectionName));
        builder.Services.Configure<SeedDataOptions>(builder.Configuration.GetSection(SeedDataOptions.SectionName));
        builder.Services.Configure<VapidOptions>(builder.Configuration.GetSection("Vapid"));
        builder.Services.Configure<GameEngineOptions>(builder.Configuration.GetSection(GameEngineOptions.SectionName));
        builder.Services.Configure<MasterServerRegistrationOptions>(builder.Configuration.GetSection(MasterServerRegistrationOptions.SectionName));
        builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
        builder.Services.Configure<GraphQlSecurityOptions>(builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        var graphQlSecurityOptions = builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName).Get<GraphQlSecurityOptions>()
            ?? new GraphQlSecurityOptions();
        var graphQlMaxDepth = Math.Max(1, graphQlSecurityOptions.MaxDepth);
        var graphQlMaxComplexity = Math.Max(1, graphQlSecurityOptions.MaxComplexity);
        var graphQlMaxPageSize = Math.Max(1, graphQlSecurityOptions.MaxPageSize);
        var biatecOidcOptions = builder.Configuration.GetSection(BiatecOidcOptions.SectionName).Get<BiatecOidcOptions>()
            ?? new BiatecOidcOptions();
        static string NormalizeIssuer(string issuer) => issuer.Trim().TrimEnd('/');
        var biatecKnownIssuers = new[]
        {
            biatecOidcOptions.Issuer,
            biatecOidcOptions.Authority,
        }
            .Where(issuer => !string.IsNullOrWhiteSpace(issuer))
            .Select(issuer => issuer!)
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
            var gameCatalogConnectionString = builder.Configuration.GetConnectionString("GameCatalog");
            if (RequiredSecretsStartupGuard.TryGetUnsafeConnectionStringReason(gameCatalogConnectionString, out var unsafeConnectionReason))
            {
                startupLogger.LogCritical(
                    "Blocking startup because ConnectionStrings:GameCatalog is insecure. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeConnectionReason,
                    "ConnectionStrings__GameCatalog");
                throw new InvalidOperationException(
                    "ConnectionStrings:GameCatalog is missing or uses a placeholder value. " +
                    "Set a real PostgreSQL connection string via environment variable 'ConnectionStrings__GameCatalog' before starting outside Development. " +
                    $"Validation reason: {unsafeConnectionReason}");
            }
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

        builder.Services.AddDbContext<AppDbContext>(options =>
        {
            if (builder.Environment.IsEnvironment("Testing"))
            {
                var testDatabaseName = builder.Configuration.GetConnectionString("GameCatalog") ?? "TestDb";
                options.UseInMemoryDatabase(testDatabaseName);
            }
            else
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("GameCatalog")
                    ?? throw new InvalidOperationException("Connection string 'GameCatalog' is missing."));
            }
        });
        builder.Services.AddScoped<AppDbInitializer>();
        builder.Services.AddScoped<AuthenticatedPlayerClaimsSyncService>();
        builder.Services.AddScoped<BotOwnershipGuard>();
        builder.Services.AddScoped<ObjectAuthorizationService>();
        builder.Services.AddScoped<NbsExchangeRateService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("push");
        builder.Services.AddHttpClient("nbs-exchange-rate");

        MasterServerHttpClientRegistration.Add(builder.Services, builder.Environment);

        builder.Services.AddScoped<WebPush.IWebPushClient>(serviceProvider =>
            new WebPush.WebPushClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("push")));

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
                    var authorization = context.Request.Headers.Authorization.ToString();
                    if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return "local-jwt";
                    }

                    var token = authorization["Bearer ".Length..].Trim();
                    if (string.IsNullOrWhiteSpace(token))
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
                var localJwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
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

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireClaim(ClaimTypes.Role, PlayerRole.Admin));

        builder.Services
            .AddGraphQLServer()
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
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddTypeExtension<CompanyTypeExtensions>()
            .AddTypeExtension<BuildingConfigurationPlanTypeExtensions>()
            .AddTypeExtension<BuildingTypeExtensions>()
            .AddTypeExtension<PlayerTypeExtensions>();

        builder.Services.AddScoped<IMasterGameAdministrationService, MasterGameAdministrationService>();
        builder.Services.AddScoped<GameAdminAuthorizationService>();
        builder.Services.AddScoped<IMasterRankingTelemetryService, MasterRankingTelemetryService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ILoginThrottleService, LoginThrottleService>();
        builder.Services.AddSingleton<IChatRateLimitService, ChatRateLimitService>();
        builder.Services.AddScoped<IJwtSessionRevocationService, JwtSessionRevocationService>();

        // ── Game tick engine ──
        builder.Services.AddScoped<TickProcessor>();
        builder.Services.AddScoped<ITickPhase, WeatherUpdatePhase>();
        builder.Services.AddScoped<ITickPhase, FuelProcurementPhase>();
        builder.Services.AddScoped<ITickPhase, PowerDistributionPhase>();
        builder.Services.AddScoped<ITickPhase, EnergySpotMarketPhase>();
        builder.Services.AddScoped<ITickPhase, PowerGridEconomicsPhase>();
        builder.Services.AddScoped<ITickPhase, ConstructionPhase>();
        builder.Services.AddScoped<ITickPhase, BuildingUpgradePhase>();
        builder.Services.AddScoped<ITickPhase, LandMarketPhase>();
        builder.Services.AddScoped<ITickPhase, MediaHousePhase>();
        builder.Services.AddScoped<ITickPhase, Api.Engine.Phases.EconomicCyclePhase>();
        builder.Services.AddScoped<ITickPhase, PublicSalesPhase>();
        builder.Services.AddScoped<ITickPhase, ResourceMovementPhase>();
        builder.Services.AddScoped<ITickPhase, ManufacturingPhase>();
        builder.Services.AddScoped<ITickPhase, OperatingCostPhase>();
        builder.Services.AddScoped<ITickPhase, MiningPhase>();
        builder.Services.AddScoped<ITickPhase, ResourceReplenishmentPhase>();
        builder.Services.AddScoped<ITickPhase, PurchasingPhase>();
        builder.Services.AddScoped<ITickPhase, TradeRoutePhase>();
        builder.Services.AddScoped<ITickPhase, MediaHouseContentPhase>();
        builder.Services.AddScoped<ITickPhase, MarketingPhase>();
        builder.Services.AddScoped<ITickPhase, ResearchPhase>();
        builder.Services.AddScoped<ITickPhase, RentPhase>();
        builder.Services.AddScoped<ITickPhase, StockLimitOrderMatchingPhase>();
        builder.Services.AddScoped<ITickPhase, LoanRepaymentPhase>();
        builder.Services.AddScoped<ITickPhase, BuildingDestructionPhase>();
        builder.Services.AddScoped<ITickPhase, PlayerAlertPhase>();
        builder.Services.AddScoped<ITickPhase, BankInterestPhase>();
        builder.Services.AddScoped<ITickPhase, TaxPhase>();
        builder.Services.AddScoped<ITickPhase, DividendGovernanceSettlementPhase>();
        builder.Services.AddScoped<ITickPhase, DividendPhase>();
        builder.Services.AddScoped<ITickPhase, TelemetryBountyPhase>();
        builder.Services.AddScoped<ITickPhase, EconomicReportPhase>();
        builder.Services.AddScoped<ITickPhase, MarketReportPhase>();
        builder.Services.AddScoped<ITickPhase, RankHistoryPhase>();
        builder.Services.AddScoped<ITickPhase, EndgamePhase>();
        builder.Services.AddScoped<ITickPhase, FxRateHistoryPhase>();
        builder.Services.AddHostedService<GameTickHostedService>();
        builder.Services.AddHostedService<MasterServerRegistrationHostedService>();
        builder.Services.AddHostedService<MarketReportPublisherHostedService>();
        builder.Services.AddHostedService<JwtSessionCleanupHostedService>();

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

        app.UseCors("frontend");
        app.UseMiddleware<AuthRateLimitMiddleware>();
        app.UseMiddleware<ApiKeyAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ApiKeyScopeMiddleware>();
        app.UseMiddleware<GameEndedMutationGuardMiddleware>();
        app.UseMiddleware<AdminAuditLoggingMiddleware>();
        app.UseMiddleware<GraphQlRequestSecurityMiddleware>();

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
            if (!TryReadBearerJwt(httpContext, out var jwt))
            {
                return Results.Unauthorized();
            }

            await revocationService.RevokeCurrentAsync(httpContext.User, jwt, httpContext.RequestAborted);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/auth/logout-all", async (HttpContext httpContext, IJwtSessionRevocationService revocationService) =>
        {
            if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var playerId))
            {
                return Results.Unauthorized();
            }

            if (!TryReadBearerJwt(httpContext, out var jwt))
            {
                return Results.Unauthorized();
            }

            var currentJti = jwt.Claims.FirstOrDefault(claim => claim.Type == System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Jti)?.Value ?? jwt.Id;
            if (string.IsNullOrWhiteSpace(currentJti))
            {
                return Results.Unauthorized();
            }

            await revocationService.RevokeOtherSessionsForPlayerAsync(playerId, currentJti, "PLAYER_LOGOUT_ALL", httpContext.RequestAborted);
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
            var initializer = scope.ServiceProvider.GetRequiredService<AppDbInitializer>();
            await initializer.InitializeAsync();
        }

        await app.RunAsync();

        static bool TryReadBearerJwt(HttpContext httpContext, out JwtSecurityToken token)
        {
            token = null!;
            var authorization = httpContext.Request.Headers.Authorization.ToString();
            if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rawToken = authorization["Bearer ".Length..].Trim();
            if (string.IsNullOrWhiteSpace(rawToken))
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
}
