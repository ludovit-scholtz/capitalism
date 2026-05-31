using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace MasterApi.Tests;

public sealed class AuthRateLimitBypassTests
{
    [Fact]
    public async Task AuthRateLimit_NamedOperation_DoesNotBypassLimit()
    {
        await using var factory = new RateLimitFactory(rateLimitPerMinute: 1);
        using var client = factory.CreateClient();

        // First named-operation register counts against the limit.
        var responseA = await client.SendAsync(CreateNamedRegisterRequest(
            $"master-named-a-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(StatusCodes.Status200OK, (int)responseA.StatusCode);

        // Second one exceeds the per-IP limit even though the operation name is not
        // literally "register" — the AST parser still counts the register root field.
        var responseB = await client.SendAsync(CreateNamedRegisterRequest(
            $"master-named-b-{Guid.NewGuid():N}@example.com"));
        Assert.Equal(StatusCodes.Status429TooManyRequests, (int)responseB.StatusCode);
        var bodyB = JsonSerializer.Deserialize<JsonElement>(await responseB.Content.ReadAsStringAsync());
        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            bodyB.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthRateLimit_BatchedArrayBody_DoesNotBypassLimit()
    {
        await using var factory = new RateLimitFactory(rateLimitPerMinute: 1);
        using var client = factory.CreateClient();

        // A single JSON-array batch containing two register mutations counts as two
        // auth fields, so it must trip the limit of one within a single request.
        var payload = JsonSerializer.Serialize(new[]
        {
            BuildRegisterItem($"master-batch-a-{Guid.NewGuid():N}@example.com"),
            BuildRegisterItem($"master-batch-b-{Guid.NewGuid():N}@example.com"),
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(request);

        Assert.Equal(StatusCodes.Status429TooManyRequests, (int)response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    private static HttpRequestMessage CreateNamedRegisterRequest(string email)
    {
        var payload = JsonSerializer.Serialize(new
        {
            operationName = "Authenticate",
            query = """
                    mutation Authenticate($input: RegisterInput!) {
                      register(input: $input) { token }
                    }
                    """,
            variables = new
            {
                input = new
                {
                    email,
                    displayName = "Named Operation Test",
                    password = "TestPass123!"
                }
            }
        });

        return new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
    }

    private static object BuildRegisterItem(string email) => new
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
                displayName = "Batched Register Test",
                password = "TestPass123!"
            }
        }
    };

    private sealed class RateLimitFactory(int rateLimitPerMinute) : WebApplicationFactory<Program>
    {
        private readonly string _databaseName = $"masterapi-tests-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:MasterCatalog"] = _databaseName,
                    ["MasterServer:RegistrationKey"] = "test-registration-key",
                    ["MasterServer:ActiveThresholdSeconds"] = "90",
                    ["GameAdministration:RootAdministratorEmails:0"] = "root@example.com",
                    ["Auth:PasswordAuthEnabled"] = "true",
                    ["Auth:EnableRateLimitInTesting"] = "true",
                    ["Auth:RateLimitRequestsPerMinute"] = rateLimitPerMinute.ToString(),
                });
            });
        }
    }
}
