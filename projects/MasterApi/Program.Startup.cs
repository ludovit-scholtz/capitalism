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
    /// <summary>Values read from configuration once and shared by the startup steps.</summary>
    private sealed record StartupSettings(
        JwtOptions Jwt,
        int GraphQlMaxDepth,
        int GraphQlMaxComplexity,
        int GraphQlMaxPageSize,
        bool ForwardedHeadersEnabled,
        BiatecOidcOptions BiatecOidc,
        string[] KnownBiatecIssuers);

    private static void ConfigureOptionBindings(WebApplicationBuilder builder)
    {
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
        builder.Services.Configure<GoldTokenTransferOptions>(
            builder.Configuration.GetSection(GoldTokenTransferOptions.SectionName));
        builder.Services.Configure<EmailOptions>(
            builder.Configuration.GetSection(EmailOptions.SectionName));
        builder.Services.Configure<AccountDeletionOptions>(
            builder.Configuration.GetSection(AccountDeletionOptions.SectionName));
        builder.Services.Configure<DiscordBotOptions>(
            builder.Configuration.GetSection(DiscordBotOptions.SectionName));
    }

    private static StartupSettings ResolveStartupSettings(WebApplicationBuilder builder)
    {
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

        return new StartupSettings(
            jwtOptions,
            graphQlMaxDepth,
            graphQlMaxComplexity,
            graphQlMaxPageSize,
            forwardedHeadersEnabled,
            biatecOidcOptions,
            knownBiatecIssuers);
    }

    private static string NormalizeIssuer(string issuer) => issuer.Trim().TrimEnd('/');

    private static void EnforceStartupGuards(WebApplicationBuilder builder, StartupSettings settings, ILogger startupLogger)
    {
        var jwtOptions = settings.Jwt;
        var biatecOidcOptions = settings.BiatecOidc;

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
    }
}
