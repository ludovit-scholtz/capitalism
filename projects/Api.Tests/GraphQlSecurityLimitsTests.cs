using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class GraphQlSecurityLimitsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphQlSecurityLimitsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQl_NamedOperation_SecondRootIntrospectionField_IsBlocked()
    {
        var (_, result) = await ExecuteGraphQlRawAsync(
            _client,
            """
            query MultiRootIntrospection {
              cities { id }
              __type(name: "Query") { name }
            }
            """,
            operationName: "MultiRootIntrospection");

        Assert.True(result.TryGetProperty("errors", out var errors));
        var extensions = errors[0].GetProperty("extensions");
        Assert.Equal("INTROSPECTION_DISABLED", extensions.GetProperty("code").GetString());
        Assert.Equal("__type", extensions.GetProperty("field").GetString());
        Assert.Equal(0, extensions.GetProperty("batchIndex").GetInt32());
    }

    [Fact]
    public async Task GraphQl_BatchRequest_SecondItemIntrospectionField_IsBlocked()
    {
        var batchBody = """
        [
          { "query": "query { cities { id } }" },
          { "query": "query { __schema { queryType { name } } }" }
        ]
        """;

        var (_, result) = await ExecuteGraphQlRawBodyAsync(_client, batchBody);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var extensions = errors[0].GetProperty("extensions");
        Assert.Equal("INTROSPECTION_DISABLED", extensions.GetProperty("code").GetString());
        Assert.Equal(1, extensions.GetProperty("batchIndex").GetInt32());
        Assert.Equal("__schema", extensions.GetProperty("field").GetString());
    }

    [Fact]
    public async Task GraphQl_MaxDepthExceeded_ReturnsQueryTooDeepCodeWithLimits()
    {
        var (_, result) = await ExecuteGraphQlRawAsync(
            _client,
            """
            query {
              productTypes {
                recipes {
                  inputProductType {
                    recipes {
                      inputProductType {
                        recipes {
                          inputProductType {
                            recipes {
                              inputProductType {
                                recipes {
                                  inputProductType { id }
                                }
                              }
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
            """);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var extensions = errors[0].GetProperty("extensions");
        Assert.Equal("QUERY_TOO_DEEP", extensions.GetProperty("code").GetString());
        Assert.True(extensions.GetProperty("actualDepth").GetInt32() > extensions.GetProperty("maxDepth").GetInt32());
    }

    [Fact]
    public async Task GraphQl_MaxComplexityExceeded_ReturnsQueryTooComplexCodeWithBreakdown()
    {
        var cityResult = await ExecuteGraphQlAsync(
            _client,
            """
            query {
              cities { id }
            }
            """);
        var cityId = cityResult.GetProperty("data").GetProperty("cities")[0].GetProperty("id").GetString();

        var (_, result) = await ExecuteGraphQlRawAsync(
            _client,
            """
            query CityLots($cityId: UUID!) {
              a1: cityLots(cityId: $cityId) { id }
              a2: cityLots(cityId: $cityId) { id }
              a3: cityLots(cityId: $cityId) { id }
              a4: cityLots(cityId: $cityId) { id }
              a5: cityLots(cityId: $cityId) { id }
              a6: cityLots(cityId: $cityId) { id }
              a7: cityLots(cityId: $cityId) { id }
              a8: cityLots(cityId: $cityId) { id }
              a9: cityLots(cityId: $cityId) { id }
              a10: cityLots(cityId: $cityId) { id }
              a11: cityLots(cityId: $cityId) { id }
              a12: cityLots(cityId: $cityId) { id }
              a13: cityLots(cityId: $cityId) { id }
              a14: cityLots(cityId: $cityId) { id }
              a15: cityLots(cityId: $cityId) { id }
              a16: cityLots(cityId: $cityId) { id }
              a17: cityLots(cityId: $cityId) { id }
              a18: cityLots(cityId: $cityId) { id }
              a19: cityLots(cityId: $cityId) { id }
              a20: cityLots(cityId: $cityId) { id }
              a21: cityLots(cityId: $cityId) { id }
              a22: cityLots(cityId: $cityId) { id }
              a23: cityLots(cityId: $cityId) { id }
              a24: cityLots(cityId: $cityId) { id }
              a25: cityLots(cityId: $cityId) { id }
              a26: cityLots(cityId: $cityId) { id }
              a27: cityLots(cityId: $cityId) { id }
              a28: cityLots(cityId: $cityId) { id }
              a29: cityLots(cityId: $cityId) { id }
              a30: cityLots(cityId: $cityId) { id }
            }
            """,
            new { cityId });

        Assert.True(result.TryGetProperty("errors", out var errors));
        var extensions = errors[0].GetProperty("extensions");
        Assert.Equal("QUERY_TOO_COMPLEX", extensions.GetProperty("code").GetString());
        Assert.True(extensions.GetProperty("actualComplexity").GetInt32() > extensions.GetProperty("maxComplexity").GetInt32());
        Assert.True(extensions.GetProperty("rootFields").EnumerateObject().Any());
    }

    [Fact]
    public async Task GraphQl_Introspection_DisabledOutsideDevelopment_ReturnsIntrospectionDisabled()
    {
        var (_, result) = await ExecuteGraphQlRawAsync(
            _client,
            """
            query {
              __type(name: "Query") { name }
            }
            """);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal(
            "INTROSPECTION_DISABLED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthRateLimit_MultiFieldMutation_CountsEveryAuthField()
    {
        await using var factory = new SecurityLimitsApiWebApplicationFactory(rateLimitPerMinute: 1);
        using var client = factory.CreateClient();

        var mutationBody = """
        mutation {
          a: register(input: { email: "limit-a@example.com", displayName: "A", password: "TestPass123!" }) { token }
          b: register(input: { email: "limit-b@example.com", displayName: "B", password: "TestPass123!" }) { token }
        }
        """;

        var (statusCode, body) = await ExecuteGraphQlRawAsync(client, mutationBody);
        Assert.Equal(StatusCodes.Status429TooManyRequests, statusCode);
        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthRateLimit_BatchRequest_CountsEveryBatchItem()
    {
        await using var factory = new SecurityLimitsApiWebApplicationFactory(rateLimitPerMinute: 1);
        using var client = factory.CreateClient();

        var batchBody1 = """
        [
          { "query": "mutation { register(input: { email: \"batch-a@example.com\", displayName: \"A\", password: \"TestPass123!\" }) { token } }" },
          { "query": "mutation { register(input: { email: \"batch-b@example.com\", displayName: \"B\", password: \"TestPass123!\" }) { token } }" }
        ]
        """;

        var (status3, body3) = await ExecuteGraphQlRawBodyAsync(client, batchBody1);
        Assert.Equal(StatusCodes.Status429TooManyRequests, status3);
        Assert.Equal(
            "RATE_LIMIT_EXCEEDED",
            body3.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task GraphQlRequestSecurityMiddleware_Development_AllowsIntrospection()
    {
        var nextCalled = false;
        var middleware = new GraphQlRequestSecurityMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            Options.Create(new GraphQlSecurityOptions
            {
                MaxDepth = 8,
                MaxComplexity = 200,
            }),
            NullLogger<GraphQlRequestSecurityMiddleware>.Instance,
            new TestWebHostEnvironment("Development"));

        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/graphql";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""{ "query": "query { __schema { queryType { name } } }" }"""));
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static async Task<(int StatusCode, JsonElement Body)> ExecuteGraphQlRawAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? operationName = null,
        string? token = null)
    {
        var payload = JsonSerializer.Serialize(new { query, variables, operationName });
        return await ExecuteGraphQlRawBodyAsync(client, payload, token);
    }

    private static async Task<(int StatusCode, JsonElement Body)> ExecuteGraphQlRawBodyAsync(
        HttpClient client,
        string payload,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return ((int)response.StatusCode, JsonSerializer.Deserialize<JsonElement>(body));
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var (statusCode, body) = await ExecuteGraphQlRawAsync(client, query, variables, token: token);
        if (statusCode >= 400)
        {
            throw new HttpRequestException($"HTTP {statusCode}: {JsonSerializer.Serialize(body)}");
        }

        return body;
    }

    private sealed class SecurityLimitsApiWebApplicationFactory(int rateLimitPerMinute) : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Auth:EnableRateLimitInTesting"] = "true",
                    ["Auth:RateLimitRequestsPerMinute"] = rateLimitPerMinute.ToString(),
                });
            });
        }
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(TestWebHostEnvironment);
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = environmentName;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
