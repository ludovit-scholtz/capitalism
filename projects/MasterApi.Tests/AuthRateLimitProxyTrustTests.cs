using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class AuthRateLimitProxyTrustTests
{
    [Fact]
    public async Task AuthRateLimit_SpoofedXForwardedFor_FromUntrustedProxy_DoesNotBypassLimit()
    {
        await using var factory = new SecurityRateLimitMasterApiFactory(
            rateLimitPerMinute: 1,
            forwardedForHopCount: 1);
        using var client = factory.CreateClient();

        var requestA = CreateRegisterRequest(
            $"master-xforwarded-untrusted-a-{Guid.NewGuid():N}@example.com",
            "1.1.1.1");
        var responseA = await client.SendAsync(requestA);
        Assert.Equal(StatusCodes.Status200OK, (int)responseA.StatusCode);

        var requestB = CreateRegisterRequest(
            $"master-xforwarded-untrusted-b-{Guid.NewGuid():N}@example.com",
            "2.2.2.2");
        var responseB = await client.SendAsync(requestB);
        var bodyB = JsonSerializer.Deserialize<JsonElement>(await responseB.Content.ReadAsStringAsync());
        Assert.Equal(StatusCodes.Status429TooManyRequests, (int)responseB.StatusCode);
        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            bodyB.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    private static HttpRequestMessage CreateRegisterRequest(string email, string forwardedFor)
    {
        var payload = JsonSerializer.Serialize(new
        {
            query = """
                    mutation Register($input: RegisterInput!) {
                      register(input: $input) { token }
                    }
                    """,
            variables = new
            {
                input = new
                {
                    email,
                    displayName = "Forwarded Header Test",
                    password = "TestPass123!"
                }
            }
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
        return request;
    }

    private sealed class SecurityRateLimitMasterApiFactory(
        int rateLimitPerMinute,
        int forwardedForHopCount,
        string[]? trustedProxies = null) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"masterapi-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var entries = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MasterCatalog"] = _databaseName,
                    ["MasterServer:RegistrationKey"] = "test-registration-key",
                    ["MasterServer:ActiveThresholdSeconds"] = "90",
                    ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
                    ["Auth:PasswordAuthEnabled"] = "true",
                    ["Auth:EnableRateLimitInTesting"] = "true",
                    ["Auth:RateLimitRequestsPerMinute"] = rateLimitPerMinute.ToString(),
                    ["ReverseProxy:ForwardedForHopCount"] = forwardedForHopCount.ToString(),
                };

                var trustedProxyEntries = trustedProxies ?? [];
                for (var i = 0; i < trustedProxyEntries.Length; i++)
                {
                    entries[$"ReverseProxy:TrustedProxies:{i}"] = trustedProxyEntries[i];
                }

                config.AddInMemoryCollection(entries);
            });
        }
    }
}
