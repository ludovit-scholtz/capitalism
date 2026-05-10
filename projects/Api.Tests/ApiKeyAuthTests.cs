using System.Net.Http.Headers;
using System.Net;
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
        var (response, body) = await SendWithStatusAsync(client, query, variables, token, apiKey);
        response.EnsureSuccessStatusCode();
        return body;
    }

    private static async Task<(HttpResponseMessage Response, JsonElement Body)> SendWithStatusAsync(
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
        var body = await response.Content.ReadAsStringAsync();
        return (response, JsonSerializer.Deserialize<JsonElement>(body));
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

    private async Task<string> LoginAsync(string email, string password)
    {
        var variables = new { input = new { email, password } };
        var result = await SendAsync(_client,
            "mutation Login($input: LoginInput!) { login(input: $input) { token } }",
            variables);
        return result.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;
    }

    private async Task<(string PlaintextKey, Guid KeyId)> GenerateApiKeyAsync(
        string token,
        string name,
        string[]? scopes = null,
        Guid[]? companyIds = null)
    {
        var variables = new
        {
            input = new
            {
                name,
                scopes = scopes ?? [ApiKeyScopes.ReadOnly],
                companyIds = companyIds ?? Array.Empty<Guid>(),
            }
        };

        var result = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) {
                    plaintextKey
                    apiKey { id }
                }
            }",
            variables,
            token);

        var payload = result.GetProperty("data").GetProperty("generateApiKey");
        return (
            payload.GetProperty("plaintextKey").GetString()!,
            Guid.Parse(payload.GetProperty("apiKey").GetProperty("id").GetString()!));
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
        var variables = new { input = new { name = "My Test Bot", scopes = new[] { ApiKeyScopes.ReadOnly } } };
        var result = await SendAsync(_client,
            @"mutation Gen($input: GenerateApiKeyInput!) {
                generateApiKey(input: $input) {
                    plaintextKey
                    apiKey { id name totalCallCount revokedAtUtc scopes companyIds }
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
        Assert.Equal("read-only", apiKeyObj.GetProperty("scopes").EnumerateArray().Single().GetString());
    }

    [Fact]
    public async Task GenerateApiKey_Unauthenticated_ReturnsError()
    {
        var variables = new { input = new { name = "Bot", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
        var genVars = new { input = new { name = "Auth Test Key", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
        var genVars = new { input = new { name = "To be revoked", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
            var genVars = new { input = new { name = $"Key {i}", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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

        var genVars = new { input = new { name = "Revocable", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
        var genVars = new { input = new { name = "Player1 Key", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
        var variables = new { input = new { name = "   ", scopes = new[] { ApiKeyScopes.ReadOnly } } };
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
    public void ApiKeyScopes_Normalize_KeepsCanonicalCodes()
    {
        Assert.Equal(ApiKeyScopes.ReadOnly, ApiKeyScopes.Normalize("READ-ONLY"));
        Assert.Equal(ApiKeyScopes.BotOnly, ApiKeyScopes.Normalize("bot-only"));
        Assert.Equal(ApiKeyScopes.TradingOnly, ApiKeyScopes.Normalize("Trading-Only"));
        Assert.Equal(ApiKeyScopes.CompanyBound, ApiKeyScopes.Normalize("company-bound"));
        Assert.Null(ApiKeyScopes.Normalize("admin"));
    }

    [Fact]
    public async Task ReadOnlyApiKey_Mutation_ReturnsStructuredForbidden()
    {
        var token = await RegisterAndLoginAsync("ak-readonly@test.com", "AkReadOnly");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Read Only", [ApiKeyScopes.ReadOnly]);

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation CreateCompany($input: CreateCompanyInput!) {
                createCompany(input: $input) { id }
            }",
            new { input = new { name = "Forbidden Co" } },
            apiKey: plaintext);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = body.GetProperty("errors")[0];
        Assert.Equal("API_KEY_SCOPE_FORBIDDEN", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task BotOnlyApiKey_ForexMutation_ReturnsStructuredForbidden()
    {
        var token = await RegisterAndLoginAsync("ak-bot@test.com", "AkBot");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Bot Key", [ApiKeyScopes.BotOnly]);

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation ExecuteForexSwap($input: ExecuteForexSwapInput!) {
                executeForexSwap(input: $input) { fromAmount }
            }",
            new
            {
                input = new
                {
                    fromCurrencyCode = "EUR",
                    toCurrencyCode = "USD",
                    amount = 100m,
                }
            },
            apiKey: plaintext);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "API_KEY_SCOPE_FORBIDDEN",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TradingOnlyApiKey_StockMutation_ReachesBusinessValidation()
    {
        var token = await RegisterAndLoginAsync("ak-trading@test.com", "AkTrading");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Trading Key", [ApiKeyScopes.TradingOnly]);

        var body = await SendAsync(
            _client,
            @"mutation BuyShares($input: BuySharesInput!) {
                buyShares(input: $input) { companyId }
            }",
            new
            {
                input = new
                {
                    companyId = Guid.NewGuid(),
                    shareCount = -1m,
                }
            },
            apiKey: plaintext);

        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.Equal("INVALID_SHARE_COUNT", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TradingOnlyApiKey_BuildingMutation_ReturnsStructuredForbidden()
    {
        var token = await RegisterAndLoginAsync("ak-trade-building@test.com", "AkTradeBuilding");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Trading Key", [ApiKeyScopes.TradingOnly]);

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation PlaceBuilding($input: PlaceBuildingInput!) {
                placeBuilding(input: $input) { id }
            }",
            new
            {
                input = new
                {
                    companyId = Guid.NewGuid(),
                    cityId = Guid.NewGuid(),
                    type = "FACTORY",
                    name = "Denied Factory"
                }
            },
            apiKey: plaintext);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "API_KEY_SCOPE_FORBIDDEN",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TradingOnlyApiKey_LendingMutation_ReturnsStructuredForbidden()
    {
        var token = await RegisterAndLoginAsync("ak-trade-loan@test.com", "AkTradeLoan");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Trading Key", [ApiKeyScopes.TradingOnly]);

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation AcceptLoan($input: AcceptLoanInput!) {
                acceptLoan(input: $input) { id }
            }",
            new
            {
                input = new
                {
                    loanOfferId = Guid.NewGuid(),
                    borrowerCompanyId = Guid.NewGuid(),
                    collateralBuildingId = Guid.NewGuid(),
                    principalAmount = 10_000m,
                    durationTicks = 24L,
                }
            },
            apiKey: plaintext);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "API_KEY_SCOPE_FORBIDDEN",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TradingOnlyApiKey_AdminMutation_ReturnsStructuredForbidden()
    {
        var token = await RegisterAndLoginAsync("ak-trade-admin@test.com", "AkTradeAdmin");
        var (plaintext, _) = await GenerateApiKeyAsync(token, "Trading Key", [ApiKeyScopes.TradingOnly]);

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation SetPlayerInvisibleInChat($input: SetPlayerInvisibleInChatInput!) {
                setPlayerInvisibleInChat(input: $input) { playerId }
            }",
            new
            {
                input = new
                {
                    playerId = Guid.NewGuid(),
                    isInvisible = true,
                }
            },
            apiKey: plaintext);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "API_KEY_SCOPE_FORBIDDEN",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CompanyBoundApiKey_RestrictsMutationsToListedCompanies()
    {
        var token = await RegisterAndLoginAsync("ak-company-bound@test.com", "AkCompanyBound");

        var companyOneResult = await SendAsync(
            _client,
            @"mutation CreateCompany($input: CreateCompanyInput!) {
                createCompany(input: $input) { id name }
            }",
            new { input = new { name = "Allowed Company" } },
            token: token);
        var companyOneId = Guid.Parse(companyOneResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!);

        var companyTwoResult = await SendAsync(
            _client,
            @"mutation CreateCompany($input: CreateCompanyInput!) {
                createCompany(input: $input) { id name }
            }",
            new { input = new { name = "Denied Company" } },
            token: token);
        var companyTwoId = Guid.Parse(companyTwoResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!);

        var (plaintext, _) = await GenerateApiKeyAsync(
            token,
            "Company Bound",
            [ApiKeyScopes.BotOnly, ApiKeyScopes.CompanyBound],
            [companyOneId]);

        var allowedResult = await SendAsync(
            _client,
            @"mutation UpdateCompanySettings($input: UpdateCompanySettingsInput!) {
                updateCompanySettings(input: $input) { id name }
            }",
            new
            {
                input = new
                {
                    companyId = companyOneId,
                    name = "Allowed Company Renamed",
                    citySalarySettings = Array.Empty<object>(),
                }
            },
            apiKey: plaintext);
        Assert.Equal(
            "Allowed Company Renamed",
            allowedResult.GetProperty("data").GetProperty("updateCompanySettings").GetProperty("name").GetString());

        var (response, body) = await SendWithStatusAsync(
            _client,
            @"mutation UpdateCompanySettings($input: UpdateCompanySettingsInput!) {
                updateCompanySettings(input: $input) { id name }
            }",
            new
            {
                input = new
                {
                    companyId = companyTwoId,
                    name = "Denied Rename",
                    citySalarySettings = Array.Empty<object>(),
                }
            },
            apiKey: plaintext);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "API_KEY_SCOPE_FORBIDDEN",
            body.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task ApiKeyUsage_WritesAuditLog()
    {
        var token = await RegisterAndLoginAsync("ak-audit@test.com", "AkAudit");
        var (plaintext, keyId) = await GenerateApiKeyAsync(token, "Audit Key", [ApiKeyScopes.ReadOnly]);

        var meResult = await SendAsync(_client, "query { me { email } }", apiKey: plaintext);
        Assert.Equal("ak-audit@test.com", meResult.GetProperty("data").GetProperty("me").GetProperty("email").GetString());

        var auditResult = await SendAsync(
            _client,
            @"query Audit($limit: Int!, $keyId: UUID) {
                myApiKeyAuditLog(limit: $limit, keyId: $keyId) {
                    keyId
                    operationName
                    operationType
                    scopeUsed
                    wasAllowed
                    denialCode
                }
            }",
            new { limit = 20, keyId },
            token: token);

        var entries = auditResult.GetProperty("data").GetProperty("myApiKeyAuditLog").EnumerateArray().ToList();
        var matchingEntry = entries.First(entry =>
            entry.GetProperty("keyId").GetString() == keyId.ToString()
            && entry.GetProperty("operationName").GetString() == "me");

        Assert.Equal("query", matchingEntry.GetProperty("operationType").GetString());
        Assert.Equal(ApiKeyScopes.ReadOnly, matchingEntry.GetProperty("scopeUsed").GetString());
        Assert.True(matchingEntry.GetProperty("wasAllowed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, matchingEntry.GetProperty("denialCode").ValueKind);
    }

    [Fact]
    public async Task AdminCanViewAndForceRevokePlayerApiKeys()
    {
        var playerToken = await RegisterAndLoginAsync("ak-admin-view@test.com", "AkAdminView");
        var (plaintext, keyId) = await GenerateApiKeyAsync(playerToken, "Admin Visible Key", [ApiKeyScopes.ReadOnly]);
        _ = await SendAsync(_client, "query { me { email } }", apiKey: plaintext);

        var adminToken = await LoginAsync("admin@capitalism.local", "ChangeMe123!");

        var adminList = await SendAsync(
            _client,
            @"query AdminKeys($playerEmail: String!, $limit: Int!) {
                adminApiKeys(playerEmail: $playerEmail, limit: $limit) {
                    playerEmail
                    key {
                        id
                        name
                        scopes
                        revokedAtUtc
                    }
                }
            }",
            new { playerEmail = "ak-admin-view@test.com", limit = 20 },
            token: adminToken);

        var listedKey = adminList.GetProperty("data").GetProperty("adminApiKeys").EnumerateArray().Single();
        Assert.Equal("ak-admin-view@test.com", listedKey.GetProperty("playerEmail").GetString());
        Assert.Equal("Admin Visible Key", listedKey.GetProperty("key").GetProperty("name").GetString());

        var revokeResult = await SendAsync(
            _client,
            @"mutation ForceRevoke($keyId: UUID!) {
                forceRevokeApiKey(keyId: $keyId)
            }",
            new { keyId },
            token: adminToken);
        Assert.True(revokeResult.GetProperty("data").GetProperty("forceRevokeApiKey").GetBoolean());

        var revokedUse = await SendAsync(_client, "query { me { email } }", apiKey: plaintext);
        Assert.True(revokedUse.TryGetProperty("errors", out _));

        var auditResult = await SendAsync(
            _client,
            @"query AdminAudit($playerEmail: String!, $limit: Int!) {
                adminApiKeyAuditLog(playerEmail: $playerEmail, limit: $limit) {
                    playerEmail
                    operationName
                    scopeUsed
                }
            }",
            new { playerEmail = "ak-admin-view@test.com", limit = 20 },
            token: adminToken);

        var auditEntries = auditResult.GetProperty("data").GetProperty("adminApiKeyAuditLog").EnumerateArray().ToList();
        Assert.Contains(auditEntries, entry =>
            entry.GetProperty("playerEmail").GetString() == "ak-admin-view@test.com"
            && entry.GetProperty("operationName").GetString() == "me"
            && entry.GetProperty("scopeUsed").GetString() == ApiKeyScopes.ReadOnly);
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
