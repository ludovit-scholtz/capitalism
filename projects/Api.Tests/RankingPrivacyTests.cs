using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Tests.Infrastructure;
using Capitalism.Shared.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

/// <summary>
/// Privacy regression tests ensuring that Algorand wallet identifiers and other
/// JWT-derived raw identifiers are NEVER exposed in the public ranking query.
///
/// ROADMAP requirement: "Do not use the jwt name anywhere. Generated user personal
/// account name is used in the game ranking in the game server."
/// </summary>
public sealed class RankingPrivacyTests
{
    // ── JWT helper constants ──────────────────────────────────────────────────
    // These must match appsettings.json so the game API validates the test tokens.
    private const string JwtIssuer = "Capitalism";
    private const string JwtAudience = "Capitalism";
    private const string JwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";

    private static string CreateMasterToken(string userId, string email, string jwtName, params Claim[] extra)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, jwtName),
            new(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
        };
        claims.AddRange(extra);

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateExternalToken(string userId, string email, string jwtName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, jwtName),
        };

        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(
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
            request.Headers.Authorization = new("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Algorand 58-char address
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A raw 58-character base32 Algorand wallet address used as the JWT <c>name</c>
    /// claim must be replaced by a generated alias before being stored and must never
    /// appear in the public <c>rankings</c> query.
    /// </summary>
    [Fact]
    public async Task MeAndRankings_AlgorandAddressJwtName_UsesGeneratedAlias()
    {
        // A realistic-looking 58-char Algorand address with varied base32 characters (A-Z, 2-7 only).
        // Real Algorand addresses use the full base32 alphabet; this is structurally valid.
        const string algorandAddress = "ZKCRG4MKYDWCR3BSFB6MZTQDLF7NXHEUVPA2JOIS5YWKGR3ABEQ6UBY543";
        Assert.Equal(58, algorandAddress.Length);
        Assert.True(algorandAddress.All(c => c is >= 'A' and <= 'Z' or >= '2' and <= '7'),
            "Test address must use valid Algorand base32 characters only");

        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"algo-addr-{Guid.NewGuid():N}@example.com";
        var token = CreateMasterToken(Guid.NewGuid().ToString(), email, algorandAddress);

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id email displayName } }", token: token);
        Assert.False(meResult.TryGetProperty("errors", out _), "me query should succeed");

        var me = meResult.GetProperty("data").GetProperty("me");
        var generatedAlias = me.GetProperty("displayName").GetString()!;

        // The raw Algorand address must NOT be stored
        Assert.NotEqual(algorandAddress, generatedAlias);
        // Generated alias must not contain the Algorand address
        Assert.DoesNotContain(algorandAddress, generatedAlias, StringComparison.OrdinalIgnoreCase);
        // Generated aliases are "Adjective Noun NNN" — exactly 3 words
        Assert.Equal(3, generatedAlias.Trim().Split(' ').Length);

        // Verify the generated alias is stored in the DB, not the raw address
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.Players.SingleAsync(p => p.Email == email);
        Assert.NotEqual(algorandAddress, stored.DisplayName);
        Assert.Equal(generatedAlias, stored.DisplayName);

        // Verify rankings do not expose the raw Algorand address
        var rankingsResult = await ExecuteGraphQlAsync(client, "{ rankings { playerId displayName personalAccountName } }");
        Assert.False(rankingsResult.TryGetProperty("errors", out _));
        var rankings = rankingsResult.GetProperty("data").GetProperty("rankings").EnumerateArray().ToList();
        var playerId = me.GetProperty("id").GetString();
        var entry = rankings.FirstOrDefault(e =>
            string.Equals(e.GetProperty("playerId").GetString(), playerId, StringComparison.Ordinal));

        Assert.True(entry.ValueKind != JsonValueKind.Undefined, "Player must appear in rankings.");
        Assert.NotEqual(algorandAddress, entry.GetProperty("displayName").GetString());
        Assert.NotEqual(algorandAddress, entry.GetProperty("personalAccountName").GetString());
        Assert.Equal(generatedAlias, entry.GetProperty("displayName").GetString());
        Assert.Equal(generatedAlias, entry.GetProperty("personalAccountName").GetString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Algorand NFD (.algo domain)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// An Algorand NFD (Non-Fungible Domain) name ending in <c>.algo</c> must be
    /// treated as a sensitive identifier and replaced by a generated alias.
    /// NFDs link a wallet address to a human-readable name and must not be
    /// exposed to other players.
    /// </summary>
    [Fact]
    public async Task MeAndRankings_AlgorandNfdDomainJwtName_UsesGeneratedAlias()
    {
        const string nfdName = "alice.algo";

        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"nfd-{Guid.NewGuid():N}@example.com";
        var token = CreateMasterToken(Guid.NewGuid().ToString(), email, nfdName);

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id email displayName } }", token: token);
        Assert.False(meResult.TryGetProperty("errors", out _), "me query should succeed");

        var me = meResult.GetProperty("data").GetProperty("me");
        var generatedAlias = me.GetProperty("displayName").GetString()!;

        Assert.NotEqual(nfdName, generatedAlias);
        Assert.DoesNotContain(".algo", generatedAlias, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(3, generatedAlias.Trim().Split(' ').Length);

        // Verify rankings do not expose the NFD name
        var rankingsResult = await ExecuteGraphQlAsync(client, "{ rankings { playerId displayName personalAccountName } }");
        Assert.False(rankingsResult.TryGetProperty("errors", out _));
        var rankings = rankingsResult.GetProperty("data").GetProperty("rankings").EnumerateArray().ToList();
        var playerId = me.GetProperty("id").GetString();
        var entry = rankings.FirstOrDefault(e =>
            string.Equals(e.GetProperty("playerId").GetString(), playerId, StringComparison.Ordinal));

        Assert.True(entry.ValueKind != JsonValueKind.Undefined, "Player must appear in rankings.");
        Assert.NotEqual(nfdName, entry.GetProperty("displayName").GetString());
        Assert.NotEqual(nfdName, entry.GetProperty("personalAccountName").GetString());
        Assert.DoesNotContain(".algo",
            entry.GetProperty("displayName").GetString() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Security regression: multiple sensitive patterns in a single test
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Security regression: the public <c>rankings</c> query must never return any
    /// entry whose <c>displayName</c> or <c>personalAccountName</c> contains an email
    /// address, a raw Algorand wallet address, or an Algorand NFD domain.
    /// </summary>
    [Fact]
    public async Task Rankings_SecurityRegression_NoRawIdentifierExposedInPublicOutput()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Provision players with various forms of sensitive JWT name claims
        var scenarios = new[]
        {
            ($"sec-email-{Guid.NewGuid():N}@example.com",
                $"sec-email-{Guid.NewGuid():N}@example.com"),     // email-as-name
            ($"sec-algo-{Guid.NewGuid():N}@example.com",
                "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB33"),   // Algorand address
            ($"sec-nfd-{Guid.NewGuid():N}@example.com",
                "bob.algo"),                                        // NFD domain
        };

        var playerIds = new List<string>();
        foreach (var (email, jwtName) in scenarios)
        {
            var tok = CreateMasterToken(Guid.NewGuid().ToString(), email, jwtName);
            var r = await ExecuteGraphQlAsync(client, "{ me { id } }", token: tok);
            playerIds.Add(r.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
        }

        // The rankings query is public — no auth required
        var rankingsResult = await ExecuteGraphQlAsync(client, "{ rankings { playerId displayName personalAccountName } }");
        Assert.False(rankingsResult.TryGetProperty("errors", out _));
        var rankings = rankingsResult.GetProperty("data").GetProperty("rankings").EnumerateArray().ToList();

        foreach (var pid in playerIds)
        {
            var entry = rankings.FirstOrDefault(e =>
                string.Equals(e.GetProperty("playerId").GetString(), pid, StringComparison.Ordinal));
            Assert.True(entry.ValueKind != JsonValueKind.Undefined, $"Player {pid} must appear in rankings.");

            var displayName = entry.GetProperty("displayName").GetString() ?? string.Empty;
            var personalAccountName = entry.GetProperty("personalAccountName").GetString() ?? string.Empty;

            foreach (var name in new[] { displayName, personalAccountName })
            {
                // No email addresses
                Assert.DoesNotContain("@", name, StringComparison.Ordinal);
                // No Algorand NFD domains
                Assert.False(name.EndsWith(".algo", StringComparison.OrdinalIgnoreCase),
                    $"NFD '.algo' domain must not appear in rankings: '{name}'");
                // No raw 58-char Algorand addresses (uppercase base32 A-Z, 2-7)
                Assert.False(
                    name.Length == 58 && name.All(c => c is >= 'A' and <= 'Z' or '2' or '3' or '4' or '5' or '6' or '7'),
                    $"Algorand address must not appear in rankings: '{name}'");
                // Generated alias is "Adjective Noun NNN" — exactly 3 words
                Assert.Equal(3, name.Trim().Split(' ').Length);
            }
        }
    }

    [Fact]
    public async Task MeAndRankings_ExternalJwtName_DoesNotUseJwtIdentityName()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"external-name-{Guid.NewGuid():N}@example.com";
        const string jwtIdentityName = "wallet-handle-987";
        var token = CreateExternalToken(Guid.NewGuid().ToString(), email, jwtIdentityName);

        var meResult = await ExecuteGraphQlAsync(client, "{ me { id displayName } }", token: token);
        Assert.False(meResult.TryGetProperty("errors", out _), "me query should succeed");

        var me = meResult.GetProperty("data").GetProperty("me");
        var resolvedDisplayName = me.GetProperty("displayName").GetString();

        Assert.False(string.IsNullOrWhiteSpace(resolvedDisplayName));
        Assert.NotEqual(jwtIdentityName, resolvedDisplayName);
        Assert.Equal(3, resolvedDisplayName!.Trim().Split(' ').Length);

        var rankingsResult = await ExecuteGraphQlAsync(client, "{ rankings { playerId displayName personalAccountName } }");
        Assert.False(rankingsResult.TryGetProperty("errors", out _));

        var rankings = rankingsResult.GetProperty("data").GetProperty("rankings").EnumerateArray().ToList();
        var playerId = me.GetProperty("id").GetString();
        var entry = rankings.FirstOrDefault(e =>
            string.Equals(e.GetProperty("playerId").GetString(), playerId, StringComparison.Ordinal));

        Assert.True(entry.ValueKind != JsonValueKind.Undefined, "Player must appear in rankings.");
        Assert.NotEqual(jwtIdentityName, entry.GetProperty("displayName").GetString());
        Assert.NotEqual(jwtIdentityName, entry.GetProperty("personalAccountName").GetString());
    }
}
