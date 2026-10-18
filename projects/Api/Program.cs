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
using Microsoft.AspNetCore.HttpOverrides;
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
        builder.Services.Configure<ReverseProxyOptions>(builder.Configuration.GetSection(ReverseProxyOptions.SectionName));
        builder.Services.Configure<GraphQlSecurityOptions>(builder.Configuration.GetSection(GraphQlSecurityOptions.SectionName));
        builder.Services.Configure<GameRulesOptions>(builder.Configuration.GetSection(GameRulesOptions.SectionName));

        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("JWT configuration is missing.");
        var seedDataOptions = builder.Configuration.GetSection(SeedDataOptions.SectionName).Get<SeedDataOptions>()
            ?? new SeedDataOptions();
        var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>()
            ?? new AuthOptions();
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
        var biatecKnownIssuers = new[]
        {
            biatecOidcOptions.Issuer,
            biatecOidcOptions.Authority,
        }
            .Where(issuer => !string.IsNullOrWhiteSpace(issuer))
            .Select(issuer => issuer!)
            .SelectMany(issuer =>
            {
                var normalized = ApiAuthenticationExtensions.NormalizeIssuer(issuer);
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

        if (RequiredSecretsStartupGuard.TryGetUnsafeOidcHttpsMetadataReason(
                biatecOidcOptions.Enabled,
                biatecOidcOptions.RequireHttpsMetadata,
                biatecOidcOptions.Authority,
                out var unsafeOidcHttpsMetadataReason))
        {
            if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"))
            {
                startupLogger.LogWarning(
                    "Startup with insecure BiatecOidc HTTPS metadata configuration is allowed only in Development/Testing. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeOidcHttpsMetadataReason,
                    "BiatecOidc__RequireHttpsMetadata");
            }
            else
            {
                startupLogger.LogCritical(
                    "Blocking startup because BiatecOidc HTTPS metadata configuration is insecure. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeOidcHttpsMetadataReason,
                    "BiatecOidc__RequireHttpsMetadata");
                throw new InvalidOperationException(
                    "BiatecOidc:RequireHttpsMetadata must be true and BiatecOidc:Authority must be an HTTPS URL while BiatecOidc:Enabled=true. " +
                    "Set BiatecOidc__RequireHttpsMetadata=true (and an HTTPS authority) before starting outside Development. " +
                    $"Validation reason: {unsafeOidcHttpsMetadataReason}");
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

        if (authOptions.PasswordAuthEnabled
            && RequiredSecretsStartupGuard.TryGetUnsafeSeedAdminPasswordReason(seedDataOptions.AdminPassword, out var unsafeSeedAdminPasswordReason))
        {
            if (builder.Environment.IsDevelopment())
            {
                startupLogger.LogWarning(
                    "Startup with insecure SeedData:AdminPassword is allowed only in Development. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeSeedAdminPasswordReason,
                    "SeedData__AdminPassword");
            }
            else
            {
                startupLogger.LogCritical(
                    "Blocking startup because SeedData:AdminPassword is insecure while password auth is enabled. Environment={EnvironmentName} Reason={Reason} OverrideEnvironmentVariable={OverrideEnvironmentVariable}",
                    builder.Environment.EnvironmentName,
                    unsafeSeedAdminPasswordReason,
                    "SeedData__AdminPassword");
                throw new InvalidOperationException(
                    "SeedData:AdminPassword is missing or uses a placeholder value while Auth:PasswordAuthEnabled=true. " +
                    "Set a strong admin seed password via environment variable 'SeedData__AdminPassword' before starting outside Development. " +
                    "See README.md#security-configuration for setup guidance. " +
                    $"Validation reason: {unsafeSeedAdminPasswordReason}");
            }
        }

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var allowedOrigins = CorsPolicyHelper.ResolveEffectiveAllowedOrigins(builder.Configuration, builder.Environment);

                if (allowedOrigins.Length == 0)
                {
                    policy.SetIsOriginAllowed(static _ => false).AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
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
        builder.Services.AddSingleton<ObjectDenialRecorder>().AddScoped<ObjectAuthorizationService>();
        builder.Services.AddScoped<NbsExchangeRateService>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("push");
        builder.Services.AddHttpClient("nbs-exchange-rate");

        MasterServerHttpClientRegistration.Add(builder.Services, builder.Environment);

        builder.Services.AddScoped<WebPush.IWebPushClient>(serviceProvider =>
            new WebPush.WebPushClient(
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("push")));

        builder.Services.AddApiAuthentication(builder.Configuration, biatecOidcOptions, biatecKnownIssuers);

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
            .AddSubscriptionType<Subscription>()
            .AddInMemorySubscriptions()
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

        builder.Services.AddGameTickEngine();

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

        app.UseStaticFiles(new StaticFileOptions
        {
            OnPrepareResponse = context =>
            {
                context.Context.Response.Headers.CacheControl = "public, max-age=86400";
            },
        });

        app.UseCors("frontend");
        app.UseWebSockets();
        app.UseMiddleware<AuthRateLimitMiddleware>();
        app.UseMiddleware<ApiKeyAuthMiddleware>();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseMiddleware<ApiKeyScopeMiddleware>();
        app.UseMiddleware<GameEndedMutationGuardMiddleware>();
        app.UseMiddleware<AdminAuditLoggingMiddleware>();
        app.UseMiddleware<GraphQlRequestSecurityMiddleware>();

        app.MapSessionEndpoints();

        app.MapGraphQL().WithOptions(options =>
        {
            options.EnableSchemaRequests = builder.Environment.IsDevelopment();
            options.Tool.Enable = builder.Environment.IsDevelopment();
        });

        if (app.Configuration.GetValue<bool>("Startup:SkipDatabaseInitialization"))
        {
            startupLogger.LogWarning(
                "Skipping AppDbInitializer.InitializeAsync because Startup:SkipDatabaseInitialization=true. Environment={EnvironmentName}",
                app.Environment.EnvironmentName);
        }
        else
        {
            using var scope = app.Services.CreateScope();
            var initializer = scope.ServiceProvider.GetRequiredService<AppDbInitializer>();
            await initializer.InitializeAsync();
        }

        await app.RunAsync();
    }
}
