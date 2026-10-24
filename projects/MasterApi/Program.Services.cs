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
    private static void ConfigureCorsAndDatabase(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("frontend", policy =>
            {
                var allowedOrigins = CorsPolicyHelper.ResolveAllowedOrigins(builder.Configuration);

                if (allowedOrigins.Length == 0)
                {
                    policy.SetIsOriginAllowed(static _ => false).AllowAnyHeader().AllowAnyMethod();
                    return;
                }

                policy.WithOrigins(allowedOrigins)
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
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
    }

    private static void RegisterServices(WebApplicationBuilder builder, StartupSettings settings)
    {
        var graphQlMaxDepth = settings.GraphQlMaxDepth;
        var graphQlMaxComplexity = settings.GraphQlMaxComplexity;
        var graphQlMaxPageSize = settings.GraphQlMaxPageSize;

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddHttpClient("master-server");
        builder.Services.AddHttpClient<PersonalAccountNamePropagationService>();
        builder.Services.AddHttpClient<IGameServerAccountPurgeService, GameServerAccountPurgeService>();
        builder.Services.AddScoped<AuthenticatedMasterPlayerClaimsSyncService>();
        builder.Services.AddScoped<IPasswordHasher<PlayerAccount>, PasswordHasher<PlayerAccount>>();
        builder.Services.AddScoped<MasterRankingService>();
        builder.Services.AddScoped<RankingTelemetryValidator>();
        builder.Services.AddScoped<PasswordResetService>();
        builder.Services.AddScoped<IPasswordResetEmailSender, PasswordResetEmailSender>();
        builder.Services.AddScoped<IEmailTemplateRenderer, HandlebarsEmailTemplateRenderer>();
        builder.Services.AddScoped<IEmailSender, AzureCommunicationEmailSender>();
        builder.Services.AddScoped<IMasterEmailService, MasterEmailService>();
        builder.Services.AddScoped<IWeeklyEmailReportService, WeeklyEmailReportService>();
        builder.Services.AddScoped<IAccountDeletionService, AccountDeletionService>();
        builder.Services.AddHostedService<AccountDeletionHostedService>();
        builder.Services.AddHostedService<MasterRankingSchedulerHostedService>();
        builder.Services.AddHostedService<GameServerEvictionHostedService>();
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<ILoginThrottleService, LoginThrottleService>();
        builder.Services.AddSingleton<IPasswordResetThrottleService, PasswordResetThrottleService>();
        builder.Services.AddScoped<IJwtSessionRevocationService, JwtSessionRevocationService>();
        builder.Services.AddHostedService<JwtSessionCleanupHostedService>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<WeeklyEmailReportHostedService>();

        builder.Services.AddHttpClient<BlockchainDepositScannerHostedService>();
        builder.Services.AddHostedService<BlockchainDepositScannerHostedService>();

        builder.Services.AddScoped<DiscordCommandService>();
        builder.Services.AddHttpClient<DiscordGameChatBridge>();
        builder.Services.AddSingleton<DiscordChatRelay>();
        builder.Services.AddHostedService<DiscordBotHostedService>();

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
    }
}
