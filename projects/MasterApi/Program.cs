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
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureOptionBindings(builder);
        var settings = ResolveStartupSettings(builder);

        using var startupLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            logging.AddConsole();
        });
        var startupLogger = startupLoggerFactory.CreateLogger("JwtSigningKeyStartupGuard");

        EnforceStartupGuards(builder, settings, startupLogger);
        ConfigureCorsAndDatabase(builder);
        ConfigureAuthentication(builder, settings);
        RegisterServices(builder, settings);

        var app = builder.Build();
        ConfigurePipeline(app, settings, startupLogger);
        MapEndpoints(app);
        MapGraphQlEndpoint(app);

        using (var scope = app.Services.CreateScope())
        {
            var initializer = scope.ServiceProvider.GetRequiredService<MasterDbInitializer>();
            await initializer.InitializeAsync();
        }

        await app.RunAsync();
    }

    private sealed record ForgotPasswordRequest(
        [property: JsonPropertyName("email")] string Email);

    private sealed record ResetPasswordRequest(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("newPassword")] string NewPassword);
}
