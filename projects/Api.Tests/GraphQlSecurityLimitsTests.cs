using System.Text;
using System.Text.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

public sealed class GraphQlSecurityLimitsTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GraphQlSecurityLimitsTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GraphQl_MaxDepthExceeded_ReturnsStructuredErrorCode()
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
                                  inputProductType {
                                    id
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
            }
            """);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var resultJson = JsonSerializer.Serialize(result);
        Assert.NotNull(resultJson);
        Assert.True(
            (errors[0].GetProperty("extensions").GetProperty("code").GetString() ?? string.Empty).Contains("MAX_DEPTH_EXCEEDED", StringComparison.Ordinal),
            resultJson);
    }

    [Fact]
    public async Task GraphQl_MaxComplexityExceeded_ReturnsStructuredErrorCode()
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
        var resultJson = JsonSerializer.Serialize(result);
        Assert.NotNull(resultJson);
        Assert.True(
            (errors[0].GetProperty("extensions").GetProperty("code").GetString() ?? string.Empty).Contains("MAX_COMPLEXITY_EXCEEDED", StringComparison.Ordinal),
            resultJson);
    }

    [Fact]
    public async Task GraphQl_Introspection_DisabledOutsideDevelopment_ReturnsForbidden()
    {
        var (_, result) = await ExecuteGraphQlRawAsync(
            _client,
            """
            query {
              __type(name: "Query") { name }
            }
            """);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var resultJson = JsonSerializer.Serialize(result);
        Assert.NotNull(resultJson);
        Assert.True(
            (errors[0].GetProperty("extensions").GetProperty("code").GetString() ?? string.Empty).Contains("FORBIDDEN", StringComparison.Ordinal),
            resultJson);
    }

    private static async Task<(int StatusCode, JsonElement Body)> ExecuteGraphQlRawAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

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
        var (statusCode, body) = await ExecuteGraphQlRawAsync(client, query, variables, token);
        if (statusCode >= 400)
        {
            throw new HttpRequestException($"HTTP {statusCode}: {JsonSerializer.Serialize(body)}");
        }

        return body;
    }
}
