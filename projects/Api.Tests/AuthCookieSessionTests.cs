using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

public sealed class AuthCookieSessionTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_Sets_HttpOnly_Strict_Secure_AuthCookie()
    {
        var loginResponse = await SendGraphQlAsync(
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) {
                token
              }
            }
            """,
            new
            {
                input = new
                {
                    email = "admin@capitalism.local",
                    password = ApiWebApplicationFactory.TestSeedAdminPassword
                }
            });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var setCookie = GetSetCookieHeaders(loginResponse);
        var authCookie = Assert.Single(setCookie, value => value.StartsWith("auth_token=", StringComparison.Ordinal));
        Assert.Contains("HttpOnly", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SameSite=Strict", authCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Secure", authCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthenticatedCookie_Allows_Me_Query_Without_Authorization_Header()
    {
        var cookieHeader = await LoginAsAdminAsync();
        var meResponse = await SendGraphQlAsync("{ me { id email } }", cookieHeader: cookieHeader);
        var json = await DeserializeAsync(meResponse);
        var me = json.GetProperty("data").GetProperty("me");
        Assert.Equal("admin@capitalism.local", me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Logout_Clears_AuthCookie_With_MaxAgeZero()
    {
        var cookieHeader = await LoginAsAdminAsync();
        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Add("Cookie", cookieHeader);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var setCookie = GetSetCookieHeaders(logoutResponse);
        var authCookie = Assert.Single(setCookie, value => value.StartsWith("auth_token=", StringComparison.Ordinal));
        Assert.Contains("Max-Age=0", authCookie, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string> LoginAsAdminAsync()
    {
        var response = await SendGraphQlAsync(
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) {
                token
              }
            }
            """,
            new
            {
                input = new
                {
                    email = "admin@capitalism.local",
                    password = ApiWebApplicationFactory.TestSeedAdminPassword
                }
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authCookie = Assert.Single(
            GetSetCookieHeaders(response),
            value => value.StartsWith("auth_token=", StringComparison.Ordinal));
        return authCookie.Split(';', 2, StringSplitOptions.TrimEntries)[0];
    }

    private async Task<HttpResponseMessage> SendGraphQlAsync(string query, object? variables = null, string? cookieHeader = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            request.Headers.Add("Cookie", cookieHeader);
        }
        return await _client.SendAsync(request);
    }

    private static IEnumerable<string> GetSetCookieHeaders(HttpResponseMessage response)
    {
        return response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : [];
    }

    private static async Task<JsonElement> DeserializeAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }
}
