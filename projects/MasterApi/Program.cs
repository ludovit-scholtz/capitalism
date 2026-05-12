namespace MasterApi;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MasterApi.Configuration;
using MasterApi.Data;
using MasterApi.Data.Entities;
using MasterApi.Security;
using MasterApi.Utilities;
using Capitalism.Shared.Security;
using HotChocolate.AspNetCore;
using HotChocolate.CostAnalysis;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        builder.Services.Configure<GraphQlSecurityOptions>(
            builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();
        var graphQlSecurityOptions = builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName).Get<GraphQlSecurityOptions>()
            ?? new GraphQlSecurityOptions();
        var graphQlMaxDepth = Math.Max(1, graphQlSecurityOptions.MaxDepth);
        var graphQlMaxComplexity = Math.Max(1, graphQlSecurityOptions.MaxComplexity);
        var graphQlMaxPageSize = Math.Max(1, graphQlSecurityOptions.MaxPageSize);
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
                    OnTokenValidated = async context =>
                    {
                        if (context.Principal?.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
                        {
                            var synchronizer = context.HttpContext.RequestServices.GetRequiredService<AuthenticatedMasterPlayerClaimsSyncService>();
                            await synchronizer.SyncAsync(context.Principal, identity, context.HttpContext.RequestAborted);

                            StampMasterPrivilegeEligibility(identity, context.Principal, jwtOptions.Issuer);
                        }
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
                    }
                };
            });

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("master-server");
        builder.Services.AddScoped<AuthenticatedMasterPlayerClaimsSyncService>();
        builder.Services.AddScoped<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();
        builder.Services.AddScoped<MasterRankingService>();
        builder.Services.AddScoped<RankingTelemetryValidator>();
        builder.Services.AddHostedService<MasterRankingSchedulerHostedService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ILoginThrottleService, LoginThrottleService>();

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
    }
}
