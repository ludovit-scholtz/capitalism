using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using MasterApi.Data;
using MasterApi.Security;
using MasterApi.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Tests;

public sealed class JwtSessionRevocationIntegrationTests(MasterApiWebApplicationFactory factory) : IClassFixture<MasterApiWebApplicationFactory>
{
    private const string JwtIssuer = "Capitalism";
    private const string JwtAudience = "Capitalism";
    private const string JwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Logout_RevokesCurrentJwt_ForSubsequentRequests()
    {
        var token = await RegisterAndGetTokenAsync("master-revocation-a@capitalism.test");

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        var meResponse = await GetSessionsAsync(token);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
        var body = await meResponse.Content.ReadAsStringAsync();
        Assert.Contains("session_revoked", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminRevokeAll_RevokesExistingSessionsForTargetPlayer_Only()
    {
        var targetToken = await RegisterAndGetTokenAsync("master-revocation-target@capitalism.test");
        var unaffectedToken = await RegisterAndGetTokenAsync("master-revocation-unaffected@capitalism.test");
        var targetId = await GetCurrentPlayerIdAsync(targetToken);
        var adminToken = CreateAdminToken("root@example.com");

        var adminRequest = new HttpRequestMessage(HttpMethod.Post, $"/admin/sessions/{targetId}/revoke-all");
        adminRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        var revokeResponse = await _client.SendAsync(adminRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        var targetMe = await GetSessionsAsync(targetToken);
        Assert.Equal(HttpStatusCode.Unauthorized, targetMe.StatusCode);

        var unaffectedMe = await GetSessionsAsync(unaffectedToken);
        Assert.Equal(HttpStatusCode.OK, unaffectedMe.StatusCode);
    }

    [Fact]
    public async Task LogoutAll_RevokesOlderTokens_ButAllowsFreshLogin()
    {
        var oldToken = await RegisterAndGetTokenAsync("master-revocation-refresh@capitalism.test");
        var currentToken = await LoginAndGetTokenAsync("master-revocation-refresh@capitalism.test");

        var logoutAllRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout-all");
        logoutAllRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", currentToken);
        var logoutAllResponse = await _client.SendAsync(logoutAllRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutAllResponse.StatusCode);

        var oldTokenMe = await GetSessionsAsync(oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenMe.StatusCode);

        var currentTokenMe = await GetSessionsAsync(currentToken);
        Assert.Equal(HttpStatusCode.OK, currentTokenMe.StatusCode);
    }

    [Fact]
    public async Task CleanupExpired_RemovesExpiredRevocationRows()
    {
        var token = await RegisterAndGetTokenAsync("master-revocation-cleanup@capitalism.test");
        var jti = new JwtSecurityTokenHandler().ReadJwtToken(token).Id;
        Assert.False(string.IsNullOrWhiteSpace(jti));

        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var logoutResponse = await _client.SendAsync(logoutRequest);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var revocationService = scope.ServiceProvider.GetRequiredService<IJwtSessionRevocationService>();

        var revokedToken = await db.MasterRevokedTokens.FindAsync(jti);
        Assert.NotNull(revokedToken);
        revokedToken!.ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();

        await revocationService.CleanupExpiredAsync(CancellationToken.None);
        var afterCleanup = await db.MasterRevokedTokens.FindAsync(jti);
        Assert.Null(afterCleanup);
    }

    private static StringContent GraphQlPayload(string query, object? variables = null)
        => new(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> ExecuteGraphQlRawAsync(string query, string? token = null, object? variables = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = GraphQlPayload(query, variables),
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await _client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> GetSessionsAsync(string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private async Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
    {
        var response = await ExecuteGraphQlRawAsync(query, token, variables);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email, displayName = "Revocation Test", password = "password123" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private async Task<string> LoginAndGetTokenAsync(string email)
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) {
                token
              }
            }
            """,
            new { input = new { email, password = "password123" } });

        return result.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;
    }

    private async Task<Guid> GetCurrentPlayerIdAsync(string token)
    {
        var result = await ExecuteGraphQlAsync("{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static string CreateAdminToken(string email)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Name, "Root Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
