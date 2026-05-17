using System.Net;
using MasterApi.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class CorsPolicyIntegrationTests
{
    [Fact]
    public async Task ConfiguredOrigin_AuthSessionPreflight_AllowsExplicitOriginWithCredentials()
    {
        using var factory = CreateFactory("Testing", "http://localhost:5173");
        using var client = factory.CreateClient();
        using var request = CreatePreflightRequest(
            "http://localhost:5173",
            "/auth/session",
            "authorization,content-type");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedValues));
        Assert.Equal("http://localhost:5173", allowedValues.Single());
        Assert.True(response.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentialValues));
        Assert.Equal("true", credentialValues.Single());
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

    [Fact]
    public async Task NonDevelopment_WildcardStageShardOrigin_AllowsShardSubdomainAndRejectsOther()
    {
        using var factory = CreateFactory("Testing", "https://*.stage.capitalism5.com");
        using var client = factory.CreateClient();

        using var allowedRequest = CreatePreflightRequest("https://inception-of-wealth-2026.stage.capitalism5.com");
        using var allowedResponse = await client.SendAsync(allowedRequest);

        Assert.Equal(HttpStatusCode.NoContent, allowedResponse.StatusCode);
        Assert.True(allowedResponse.Headers.TryGetValues("Access-Control-Allow-Origin", out var allowedValues));
        Assert.Equal("https://inception-of-wealth-2026.stage.capitalism5.com", allowedValues.Single());
        Assert.True(allowedResponse.Headers.TryGetValues("Access-Control-Allow-Credentials", out var credentialValues));
        Assert.Equal("true", credentialValues.Single());

        using var blockedRequest = CreatePreflightRequest("https://evil.capitalism5.com");
        using var blockedResponse = await client.SendAsync(blockedRequest);

        Assert.Equal(HttpStatusCode.NoContent, blockedResponse.StatusCode);
        Assert.False(blockedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    private static WebApplicationFactory<Program> CreateFactory(string environmentName, params string[] allowedOrigins)
    {
        return new MasterApiWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environmentName);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.Sources.Clear();
                configurationBuilder.AddInMemoryCollection(BuildConfiguration(allowedOrigins));
            });
        });
    }

    private static Dictionary<string, string?> BuildConfiguration(string[] allowedOrigins)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:MasterCatalog"] = $"masterapi-cors-policy-tests-{Guid.NewGuid():N}",
            ["MasterServer:RegistrationKey"] = "test-registration-key",
            ["MasterServer:ActiveThresholdSeconds"] = "90",
            ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
            ["Auth:PasswordAuthEnabled"] = "true",
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

    private static HttpRequestMessage CreatePreflightRequest(
        string origin,
        string path = "/graphql",
        string? requestedHeaders = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        if (!string.IsNullOrWhiteSpace(requestedHeaders))
        {
            request.Headers.Add("Access-Control-Request-Headers", requestedHeaders);
        }

        return request;
    }
}
