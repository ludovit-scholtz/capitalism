using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Api.Tests;

/// <summary>
/// Integration tests for API key generation, usage, revocation, and security boundaries.
/// </summary>
public sealed class ApiKeyAuthTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiKeyAuthTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ─── Helper ─────────────────────────────────────────────────────────────

    private static async Task<JsonElement> SendAsync(
        HttpClient client, string query, object? variables = null, string? token = null,
        string? apiKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8, "application/json");

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (apiKey is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("ApiKey", apiKey);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> RegisterAndLoginAsync(string email = "apikey-user@test.com", string name = "ApiKeyUser")
    {
        var variables = new
        {
            input = new
            {
                email,
                password = "Test1234!",
                displayName = name,
                referralCode = (string?)null
            }
        };
        var result = await SendAsync(_client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            variables);
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    // ─── Unit-style tests for ComputeHash and GenerateNewKey ────────────────

    [Fact]
    public void ComputeHash_IsDeterministic()
    {
        var hash1 = ApiKeyAuthMiddleware.ComputeHash("test-key-abc");
        var hash2 = ApiKeyAuthMiddleware.ComputeHash("test-key-abc");
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentInputs_ProduceDifferentHashes()
    {
        var hash1 = ApiKeyAuthMiddleware.ComputeHash("key-a");
        var hash2 = ApiKeyAuthMiddleware.ComputeHash("key-b");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_IsLowercaseHex64Chars()
    {
        var hash = ApiKeyAuthMiddleware.ComputeHash("some-key");
        Assert.Equal(64, hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash);
    }

    [Fact]
    public void GenerateNewKey_PlaintextIsUrlSafe()
    {
        var (plaintext, hash) = ApiKeyAuthMiddleware.GenerateNewKey();
        Assert.NotEmpty(plaintext);
        // Must not contain + or / or = (base64url without padding)
        Assert.DoesNotContain('+', plaintext);
        Assert.DoesNotContain('/', plaintext);
        Assert.DoesNotContain('=', plaintext);
        // Hash of the generated plaintext must match the returned hash.
        Assert.Equal(ApiKeyAuthMiddleware.ComputeHash(plaintext), hash);
    }

    [Fact]
    public void GenerateNewKey_EachCallProducesUniqueKey()
    {
        var (key1, _) = ApiKeyAuthMiddleware.GenerateNewKey();
        var (key2, _) = ApiKeyAuthMiddleware.GenerateNewKey();
        Assert.NotEqual(key1, key2);
    }

    // ─── Integration: generate and use ──────────────────────────────────────

    [Fact]
    public async Task GenerateApiKey_Authenticated_ReturnsPlaintextKeyAndRecord()
    {
        var token = await RegisterAndLoginAsync("ak-gen@test.com", "AkGenUser");
        var variables = new { input = new { name = "My Test Bot" } };
        var result = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) {
                    plaintextKey
                    apiKey { id name totalCallCount revokedAtUtc }
                }
            }",
            variables, token);

        var payload = result.GetProperty("data").GetProperty("generateApiKey");
        var plaintext = payload.GetProperty("plaintextKey").GetString();
        var apiKeyObj = payload.GetProperty("apiKey");

        Assert.NotNull(plaintext);
        Assert.NotEmpty(plaintext);
        Assert.Equal("My Test Bot", apiKeyObj.GetProperty("name").GetString());
        Assert.Equal(0, apiKeyObj.GetProperty("totalCallCount").GetInt64());
        Assert.Equal(JsonValueKind.Null, apiKeyObj.GetProperty("revokedAtUtc").ValueKind);
    }

    [Fact]
    public async Task GenerateApiKey_Unauthenticated_ReturnsError()
    {
        var variables = new { input = new { name = "Bot" } };
        var result = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { plaintextKey }
            }",
            variables); // no token

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray().ToList());
    }

    [Fact]
    public async Task ApiKeyAuth_ValidKey_AuthenticatesAndResolvesPlayer()
    {
        var token = await RegisterAndLoginAsync("ak-auth@test.com", "AkAuthUser");

        // Generate a key.
        var genVars = new { input = new { name = "Auth Test Key" } };
        var genResult = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { plaintextKey }
            }",
            genVars, token);
        var plaintext = genResult.GetProperty("data").GetProperty("generateApiKey")
            .GetProperty("plaintextKey").GetString()!;

        // Use the key to call `me` query.
        var meResult = await SendAsync(_client,
            "query { me { email } }",
            apiKey: plaintext);

        var email = meResult.GetProperty("data").GetProperty("me").GetProperty("email").GetString();
        Assert.Equal("ak-auth@test.com", email);
    }

    [Fact]
    public async Task ApiKeyAuth_InvalidKey_ReturnsAuthError()
    {
        var result = await SendAsync(_client,
            "query { me { email } }",
            apiKey: "invalid-fake-key-that-doesnt-exist");

        // me is [Authorize] so should return an auth error.
        Assert.True(result.TryGetProperty("errors", out var errors) ||
                    result.GetProperty("data").GetProperty("me").ValueKind == JsonValueKind.Null,
            "Expected auth error for invalid API key");
    }

    [Fact]
    public async Task ApiKeyAuth_RevokedKey_CannotAuthenticate()
    {
        var token = await RegisterAndLoginAsync("ak-revoke@test.com", "AkRevokeUser");

        // Generate a key.
        var genVars = new { input = new { name = "To be revoked" } };
        var genResult = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { plaintextKey apiKey { id } }
            }",
            genVars, token);
        var gp = genResult.GetProperty("data").GetProperty("generateApiKey");
        var plaintext = gp.GetProperty("plaintextKey").GetString()!;
        var keyId = gp.GetProperty("apiKey").GetProperty("id").GetString()!;

        // Revoke it.
        var revokeVars = new { input = new { keyId = Guid.Parse(keyId) } };
        await SendAsync(_client,
            "mutation Revoke($input: RevokeApiKeyInput!) { revokeApiKey(input: $input) }",
            revokeVars, token);

        // Try to use the revoked key.
        var result = await SendAsync(_client,
            "query { me { email } }",
            apiKey: plaintext);

        Assert.True(result.TryGetProperty("errors", out _) ||
                    result.GetProperty("data").GetProperty("me").ValueKind == JsonValueKind.Null,
            "Expected auth error after key revocation");
    }

    [Fact]
    public async Task MyApiKeys_ReturnsOwnKeys()
    {
        var token = await RegisterAndLoginAsync("ak-list@test.com", "AkListUser");

        // Generate two keys.
        for (var i = 1; i <= 2; i++)
        {
            var genVars = new { input = new { name = $"Key {i}" } };
            await SendAsync(_client,
                @"mutation Gen($input: GenerateApiKeyInput!) {
                    generateApiKey(input: $input) { plaintextKey }
                }",
                genVars, token);
        }

        var listResult = await SendAsync(_client,
            @"query { myApiKeys { id name totalCallCount revokedAtUtc } }",
            token: token);

        var keys = listResult.GetProperty("data").GetProperty("myApiKeys").EnumerateArray().ToList();
        Assert.True(keys.Count >= 2);
        Assert.Contains(keys, k => k.GetProperty("name").GetString() == "Key 1");
        Assert.Contains(keys, k => k.GetProperty("name").GetString() == "Key 2");
    }

    [Fact]
    public async Task RevokeApiKey_OwnKey_Succeeds()
    {
        var token = await RegisterAndLoginAsync("ak-rv2@test.com", "AkRv2User");

        var genVars = new { input = new { name = "Revocable" } };
        var genResult = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { apiKey { id } }
            }",
            genVars, token);
        var keyId = genResult.GetProperty("data").GetProperty("generateApiKey")
            .GetProperty("apiKey").GetProperty("id").GetString()!;

        var revokeVars = new { input = new { keyId = Guid.Parse(keyId) } };
        var revokeResult = await SendAsync(_client,
            "mutation Revoke($input: RevokeApiKeyInput!) { revokeApiKey(input: $input) }",
            revokeVars, token);
        Assert.True(revokeResult.GetProperty("data").GetProperty("revokeApiKey").GetBoolean());

        // Key should now appear as revoked in the list.
        var listResult = await SendAsync(_client,
            @"query { myApiKeys { id revokedAtUtc } }",
            token: token);
        var keys = listResult.GetProperty("data").GetProperty("myApiKeys").EnumerateArray().ToList();
        var revokedKey = keys.FirstOrDefault(k => k.GetProperty("id").GetString() == keyId);
        Assert.NotEqual(default, revokedKey);
        Assert.NotEqual(JsonValueKind.Null, revokedKey.GetProperty("revokedAtUtc").ValueKind);
    }

    [Fact]
    public async Task RevokeApiKey_OtherPlayerKey_ReturnsError()
    {
        // Create two players.
        var token1 = await RegisterAndLoginAsync("ak-own1@test.com", "AkOwn1");
        var token2 = await RegisterAndLoginAsync("ak-own2@test.com", "AkOwn2");

        // Player 1 generates a key.
        var genVars = new { input = new { name = "Player1 Key" } };
        var genResult = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { apiKey { id } }
            }",
            genVars, token1);
        var keyId = genResult.GetProperty("data").GetProperty("generateApiKey")
            .GetProperty("apiKey").GetProperty("id").GetString()!;

        // Player 2 tries to revoke Player 1's key.
        var revokeVars = new { input = new { keyId = Guid.Parse(keyId) } };
        var revokeResult = await SendAsync(_client,
            "mutation Revoke($input: RevokeApiKeyInput!) { revokeApiKey(input: $input) }",
            revokeVars, token2);

        Assert.True(revokeResult.TryGetProperty("errors", out var errors));
        var codes = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .ToList();
        Assert.Contains("NOT_FOUND", codes);
    }

    [Fact]
    public async Task GenerateApiKey_EmptyName_ReturnsValidationError()
    {
        var token = await RegisterAndLoginAsync("ak-emptyname@test.com", "AkEmptyName");
        var variables = new { input = new { name = "   " } };
        var result = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) { plaintextKey }
            }",
            variables, token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var codes = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .ToList();
        Assert.Contains("VALIDATION_ERROR", codes);
    }

    [Fact]
    public async Task AuthFeatureFlag_PasswordDisabled_RegisterReturnsDisabledError()
    {
        await using var isolatedFactory = new PasswordDisabledApiWebApplicationFactory();
        var client = isolatedFactory.CreateClient();
        var variables = new
        {
            input = new
            {
                email = "flag-test@test.com",
                password = "Test1234!",
                displayName = "FlagUser",
                referralCode = (string?)null
            }
        };
        var result = await TestHelpers.ExecuteGraphQlAsync(client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            variables);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var codes = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .ToList();
        Assert.Contains("AUTH_PASSWORD_DISABLED", codes);
    }

    [Fact]
    public async Task AuthFeatureFlag_PasswordDisabled_LoginReturnsDisabledError()
    {
        await using var isolatedFactory = new PasswordDisabledApiWebApplicationFactory();
        var client = isolatedFactory.CreateClient();
        var variables = new { input = new { email = "x@test.com", password = "pass" } };
        var result = await TestHelpers.ExecuteGraphQlAsync(client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            variables);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var codes = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .ToList();
        Assert.Contains("AUTH_PASSWORD_DISABLED", codes);
    }
}

/// <summary>
/// Factory that disables password auth (mimicking the production default).
/// </summary>
file sealed class PasswordDisabledApiWebApplicationFactory : ApiWebApplicationFactory
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        base.ApplyBaseConfiguration(builder);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:PasswordAuthEnabled"] = "false"
            });
        });
    }
}
