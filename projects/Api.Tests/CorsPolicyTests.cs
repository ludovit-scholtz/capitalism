using System.Net;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Api.Tests;

public sealed class CorsPolicyTests
{
    [Fact]
    public void ResolveAllowedOrigins_TrimsAndDeduplicates()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins:0"] = " https://app.example.com ",
                ["Cors:AllowedOrigins:1"] = "",
                ["Cors:AllowedOrigins:2"] = "https://APP.example.com",
            })
            .Build();

        var origins = CorsPolicyHelper.ResolveAllowedOrigins(configuration);

        Assert.Single(origins);
        Assert.Equal("https://app.example.com", origins[0]);
    }

    [Fact]
    public void IsDevelopmentOpenPolicy_ReturnsTrueInDevelopment()
    {
        var environment = new TestHostEnvironment("Development");
        Assert.True(CorsPolicyHelper.IsDevelopmentOpenPolicy(environment));
    }

    [Fact]
    public async Task NonDevelopment_EmptyAllowedOrigins_RejectsCrossOriginWith403()
    {
        using var factory = CreateFactory("Testing");
        using var client = factory.CreateClient();
        using var request = CreatePreflightRequest("https://evil.com");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task NonDevelopment_ConfiguredOrigin_AllowsConfiguredAndRejectsOther()
    {
        using var factory = CreateFactory("Testing", "https://app.example.com");
        using var client = factory.CreateClient();

        using var allowedRequest = CreatePreflightRequest("https://app.example.com");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.True(allowedResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedValues));
        Assert.Equal("https://app.example.com", allowedValues.Single());

        using var blockedRequest = CreatePreflightRequest("https://evil.com");
        using var blockedResponse = await client.SendAsync(blockedRequest);

        Assert.Equal(HttpStatusCode.NoContent, blockedResponse.StatusCode);
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static ApiWebApplicationFactory CreateFactory(string environmentName, params string[] allowedOrigins)
    {
        return new CorsApiWebApplicationFactory(environmentName, allowedOrigins);
    }

    private sealed class CorsApiWebApplicationFactory(string environmentName, string[] allowedOrigins) : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environmentName);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddInMemoryCollection(BuildConfiguration(allowedOrigins));
            });
        }
    }

    private static Dictionary<string, string?> BuildConfiguration(string[] allowedOrigins)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:GameCatalog"] = $"cors-policy-tests-{Guid.NewGuid():N}",
            ["SeedData:AdminEmail"] = "admin@capitalism.local",
            ["SeedData:AdminDisplayName"] = "Platform Admin",
            ["SeedData:AdminPassword"] = ApiWebApplicationFactory.TestSeedAdminPassword,
            ["Auth:PasswordAuthEnabled"] = "true",
            ["GameEngine:Enabled"] = "false",
            ["MasterServer:RegistrationEnabled"] = "false",
            ["Jwt:Issuer"] = "Capitalism",
            ["Jwt:Audience"] = "Capitalism",
            ["Jwt:SigningKey"] = "ChangeThisSigningKeyBeforeProduction123!",
            ["Jwt:ExpiresMinutes"] = "120",
        };

        for (var i = 0; i < allowedOrigins.Length; i++)
        {
            values[$"Cors:AllowedOrigins:{i}"] = allowedOrigins[i];
        }

        return values;
    }

    private static HttpRequestMessage CreatePreflightRequest(string origin)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/graphql");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        return request;
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
