namespace Api;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Security;
using Api.Utilities;
using Api.Types;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
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

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

                if (allowedOrigins.Length == 0)
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
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
        builder.Services.AddScoped<NbsExchangeRateService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("push");
        builder.Services.AddHttpClient("nbs-exchange-rate");

        if (builder.Configuration["MasterServer:ApiUrl"]?.Contains("masterapi") == true)
        {
            // ignore ssl issues in local dev
            builder.Services.AddHttpClient("master-server").ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (m, c, ch, e) => true
            });

        }
        else
        {
            builder.Services.AddHttpClient("master-server");
        }

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
                        }
                    }
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
                    }
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(Policies.Admin, policy => policy.RequireClaim(ClaimTypes.Role, PlayerRole.Admin));

        builder.Services
            .AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<Query>()
            .AddMutationType<Mutation>()
            .AddTypeExtension<CompanyTypeExtensions>()
            .AddTypeExtension<BuildingTypeExtensions>()
            .AddTypeExtension<PlayerTypeExtensions>();

        builder.Services.AddScoped<IMasterGameAdministrationService, MasterGameAdministrationService>();
        builder.Services.AddScoped<GameAdminAuthorizationService>();
        builder.Services.AddScoped<IMasterRankingTelemetryService, MasterRankingTelemetryService>();
        builder.Services.AddMemoryCache();

        // ── Game tick engine ──
        builder.Services.AddScoped<TickProcessor>();
        builder.Services.AddScoped<ITickPhase, WeatherUpdatePhase>();
        builder.Services.AddScoped<ITickPhase, FuelProcurementPhase>();
        builder.Services.AddScoped<ITickPhase, PowerDistributionPhase>();
        builder.Services.AddScoped<ITickPhase, PowerGridEconomicsPhase>();
        builder.Services.AddScoped<ITickPhase, ConstructionPhase>();
        builder.Services.AddScoped<ITickPhase, BuildingUpgradePhase>();
        builder.Services.AddScoped<ITickPhase, LandMarketPhase>();
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
        builder.Services.AddScoped<ITickPhase, LoanRepaymentPhase>();
        builder.Services.AddScoped<ITickPhase, BuildingDestructionPhase>();
        builder.Services.AddScoped<ITickPhase, PlayerAlertPhase>();
        builder.Services.AddScoped<ITickPhase, BankInterestPhase>();
        builder.Services.AddScoped<ITickPhase, TaxPhase>();
        builder.Services.AddScoped<ITickPhase, DividendPhase>();
        builder.Services.AddScoped<ITickPhase, TelemetryBountyPhase>();
        builder.Services.AddScoped<ITickPhase, EconomicReportPhase>();
        builder.Services.AddScoped<ITickPhase, MarketReportPhase>();
        builder.Services.AddScoped<ITickPhase, RankHistoryPhase>();
        builder.Services.AddScoped<ITickPhase, FxRateHistoryPhase>();
        builder.Services.AddHostedService<GameTickHostedService>();
        builder.Services.AddHostedService<MasterServerRegistrationHostedService>();
        builder.Services.AddHostedService<MarketReportPublisherHostedService>();

        var app = builder.Build();

        app.UseCors("frontend");
        app.UseAuthentication();
        app.UseAuthorization();
    app.UseMiddleware<AdminAuditLoggingMiddleware>();

        app.MapGet("/", () => Results.Ok(new
        {
            name = "Capitalism 5 Game API",
            graphql = "/graphql",
            health = "/healthz"
        }));

        app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));
        app.MapGraphQL();

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<AppDbInitializer>();
            await initializer.InitializeAsync();
        }

        await app.RunAsync();
    }
}
