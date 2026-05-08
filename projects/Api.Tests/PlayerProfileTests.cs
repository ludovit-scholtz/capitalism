using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Tests.Infrastructure;

namespace Api.Tests;

/// <summary>
/// Integration tests for the player public profile feature:
/// playerProfile(playerId) query and updatePlayerBio mutation.
/// </summary>
public sealed class PlayerProfileTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // GraphQL helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
            req.Headers.Authorization = new("Bearer", token);

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAsync(
        HttpClient client, string email, string displayName = "Test User")
    {
        var result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private const string ProfileQuery = """
        query GetProfile($playerId: UUID!) {
          playerProfile(playerId: $playerId) {
            playerId
            displayName
            bio
            createdAtUtc
            joinGameYear
            hasProSubscription
            totalWealthUsd
            totalCompanyEquityUsd
            companyCount
            leaderboardRank
            activeBuildingTypes
            citiesWithBuildings
            totalProductsSold
            hallOfFame {
              highestSingleTickRevenue
              highestSingleTickRevenueTick
              largestBuildingAcquisitionPrice
              largestBuildingAcquisitionName
              highestBrandQuality
              highestBrandQualityName
              accountAgeTicks
            }
          }
        }
        """;

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlayerProfile_NewPlayer_ReturnsPublicProfileData()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "profile-basic@test.com", "Profile Basic Player");
        var playerId = await GetPlayerIdAsync(client, token);

        // Query is public — no token required
        var result = await ExecAsync(client, ProfileQuery, new { playerId });

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");
        var profile = result.GetProperty("data").GetProperty("playerProfile");

        Assert.Equal(playerId.ToString(), profile.GetProperty("playerId").GetString());
        Assert.Equal("Profile Basic Player", profile.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, profile.GetProperty("bio").ValueKind); // no bio yet
        Assert.Equal(0, profile.GetProperty("companyCount").GetInt32());
        Assert.Equal(0m, profile.GetProperty("totalProductsSold").GetDecimal());
        Assert.False(profile.GetProperty("hasProSubscription").GetBoolean());
        Assert.True(profile.GetProperty("totalWealthUsd").GetDecimal() >= 0m);
        Assert.True(profile.GetProperty("leaderboardRank").GetInt32() >= 1);
        Assert.Equal(0, profile.GetProperty("activeBuildingTypes").GetArrayLength());
        Assert.Equal(0, profile.GetProperty("citiesWithBuildings").GetInt32());

        var hof = profile.GetProperty("hallOfFame");
        Assert.Equal(0m, hof.GetProperty("highestSingleTickRevenue").GetDecimal());
        Assert.Equal(0m, hof.GetProperty("largestBuildingAcquisitionPrice").GetDecimal());
        Assert.Equal(0m, hof.GetProperty("highestBrandQuality").GetDecimal());
        Assert.True(hof.GetProperty("accountAgeTicks").GetInt64() >= 0);
    }

    [Fact]
    public async Task PlayerProfile_UnknownPlayerId_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client, ProfileQuery, new { playerId = Guid.NewGuid() });

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");
        var profile = result.GetProperty("data").GetProperty("playerProfile");
        Assert.Equal(JsonValueKind.Null, profile.ValueKind);
    }

    [Fact]
    public async Task PlayerProfile_QueryIsPublic_NoTokenRequired()
    {
        // Profile query must be accessible without authentication
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "public-profile@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        // Intentionally no token — public query
        var result = await ExecAsync(client, ProfileQuery, new { playerId }, token: null);

        Assert.False(result.TryGetProperty("errors", out _), "playerProfile must be a public query.");
        var profile = result.GetProperty("data").GetProperty("playerProfile");
        Assert.Equal(playerId.ToString(), profile.GetProperty("playerId").GetString());
    }

    [Fact]
    public async Task PlayerProfile_WithCompany_ReturnsNonZeroCompanyCount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "profile-company@test.com", "Company Owner");
        var playerId = await GetPlayerIdAsync(client, token);

        // Create a company
        await ExecAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Test Empire Corp" } },
            token);

        var result = await ExecAsync(client, ProfileQuery, new { playerId });
        var profile = result.GetProperty("data").GetProperty("playerProfile");

        Assert.Equal(1, profile.GetProperty("companyCount").GetInt32());
    }

    [Fact]
    public async Task UpdatePlayerBio_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client,
            "mutation UB($bio: String) { updatePlayerBio(input: { bio: $bio }) { playerId bio } }",
            new { bio = "Hello world" },
            token: null);

        Assert.True(result.TryGetProperty("errors", out _), "Expected auth error for unauthenticated updatePlayerBio.");
    }

    [Fact]
    public async Task UpdatePlayerBio_ValidBio_PersistsAndVisibleOnProfile()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "bio-test@test.com", "Bio Test Player");
        var playerId = await GetPlayerIdAsync(client, token);

        const string bio = "Building wealth one tick at a time!";
        var mutResult = await ExecAsync(client,
            "mutation UB($bio: String) { updatePlayerBio(input: { bio: $bio }) { playerId bio } }",
            new { bio },
            token);

        var payload = mutResult.GetProperty("data").GetProperty("updatePlayerBio");
        Assert.Equal(playerId.ToString(), payload.GetProperty("playerId").GetString());
        Assert.Equal(bio, payload.GetProperty("bio").GetString());

        // Verify bio appears on public profile
        var profileResult = await ExecAsync(client, ProfileQuery, new { playerId });
        var returnedBio = profileResult.GetProperty("data").GetProperty("playerProfile").GetProperty("bio").GetString();
        Assert.Equal(bio, returnedBio);
    }

    [Fact]
    public async Task UpdatePlayerBio_TooLong_ReturnsBioTooLongError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "bio-toolong@test.com");

        // Bio is 161 characters (over the 160 limit)
        var tooLongBio = new string('a', 161);
        var result = await ExecAsync(client,
            "mutation UB($bio: String) { updatePlayerBio(input: { bio: $bio }) { bio } }",
            new { bio = tooLongBio },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected error for bio > 160 chars.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BIO_TOO_LONG", code);
    }

    [Fact]
    public async Task UpdatePlayerBio_ClearBio_NullOnProfile()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "bio-clear@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        // Set bio first
        await ExecAsync(client,
            "mutation UB($bio: String) { updatePlayerBio(input: { bio: $bio }) { bio } }",
            new { bio = "First bio" },
            token);

        // Clear it
        var clearResult = await ExecAsync(client,
            "mutation UB($bio: String) { updatePlayerBio(input: { bio: $bio }) { bio } }",
            new { bio = (string?)null },
            token);

        var payload = clearResult.GetProperty("data").GetProperty("updatePlayerBio");
        Assert.Equal(JsonValueKind.Null, payload.GetProperty("bio").ValueKind);

        // Verify profile shows null bio
        var profileResult = await ExecAsync(client, ProfileQuery, new { playerId });
        var bio = profileResult.GetProperty("data").GetProperty("playerProfile").GetProperty("bio");
        Assert.Equal(JsonValueKind.Null, bio.ValueKind);
    }

    [Fact]
    public async Task PlayerProfile_TwoPlayers_LeaderboardRanksDiffer()
    {
        // Both players register; they should appear in the ranking and receive distinct ranks.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token1 = await RegisterAsync(client, "rank-player1@test.com", "Rank Player One");
        var token2 = await RegisterAsync(client, "rank-player2@test.com", "Rank Player Two");

        var id1 = await GetPlayerIdAsync(client, token1);
        var id2 = await GetPlayerIdAsync(client, token2);

        var r1 = await ExecAsync(client, ProfileQuery, new { playerId = id1 });
        var r2 = await ExecAsync(client, ProfileQuery, new { playerId = id2 });

        var rank1 = r1.GetProperty("data").GetProperty("playerProfile").GetProperty("leaderboardRank").GetInt32();
        var rank2 = r2.GetProperty("data").GetProperty("playerProfile").GetProperty("leaderboardRank").GetInt32();

        Assert.True(rank1 >= 1, "Rank must be positive.");
        Assert.True(rank2 >= 1, "Rank must be positive.");
        Assert.NotEqual(rank1, rank2);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // updateDisplayName mutation tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateDisplayName_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { playerId displayName } }",
            new { dn = "New Name" });

        Assert.True(result.TryGetProperty("errors", out _), "Expected auth error for unauthenticated updateDisplayName.");
    }

    [Fact]
    public async Task UpdateDisplayName_ValidName_PersistsAndVisibleOnProfile()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "dn-update@test.com", "Original Name");
        var playerId = await GetPlayerIdAsync(client, token);

        const string NewName = "Aurelius Victor Fontaine";
        var mutResult = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { playerId displayName } }",
            new { dn = NewName },
            token);

        var payload = mutResult.GetProperty("data").GetProperty("updateDisplayName");
        Assert.Equal(NewName, payload.GetProperty("displayName").GetString());

        // Verify profile query reflects the updated name.
        var profileResult = await ExecAsync(client, ProfileQuery, new { playerId });
        var profileName = profileResult.GetProperty("data").GetProperty("playerProfile").GetProperty("displayName").GetString();
        Assert.Equal(NewName, profileName);
    }

    [Fact]
    public async Task UpdateDisplayName_EmptyName_ReturnsDisplayNameRequiredError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "dn-empty@test.com", "Some Name");

        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { displayName } }",
            new { dn = "   " },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected an error for empty display name.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("DISPLAY_NAME_REQUIRED", code);
    }

    [Fact]
    public async Task UpdateDisplayName_TooLong_ReturnsDisplayNameTooLongError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "dn-toolong@test.com", "Short Name");

        var longName = new string('A', 41);
        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { displayName } }",
            new { dn = longName },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected an error for too-long display name.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("DISPLAY_NAME_TOO_LONG", code);
    }

    [Fact]
    public async Task UpdateDisplayName_Exactly40Chars_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "dn-exact40@test.com", "Start Name");

        var exactName = new string('B', 40);
        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { displayName } }",
            new { dn = exactName },
            token);

        var payload = result.GetProperty("data").GetProperty("updateDisplayName");
        Assert.Equal(exactName, payload.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task UpdateDisplayName_PlayerCannotUpdateAnotherPlayersName()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token1 = await RegisterAsync(client, "dn-player1@test.com", "Player One");
        var token2 = await RegisterAsync(client, "dn-player2@test.com", "Player Two");
        var id2 = await GetPlayerIdAsync(client, token2);

        // Player 1 updates their OWN name — this must succeed.
        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { playerId displayName } }",
            new { dn = "New Player One Name" },
            token1);

        var payload = result.GetProperty("data").GetProperty("updateDisplayName");
        Assert.Equal("New Player One Name", payload.GetProperty("displayName").GetString());

        // Verify Player Two's name was NOT changed.
        var profile2 = await ExecAsync(client, ProfileQuery, new { playerId = id2 });
        var name2 = profile2.GetProperty("data").GetProperty("playerProfile").GetProperty("displayName").GetString();
        Assert.Equal("Player Two", name2);
    }

    [Fact]
    public async Task UpdateDisplayName_LeadingTrailingWhitespaceTrimmed()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "dn-trim@test.com", "Untrimmed Name");

        var result = await ExecAsync(client,
            "mutation UDN($dn: String!) { updateDisplayName(input: { displayName: $dn }) { displayName } }",
            new { dn = "  Trimmed Name  " },
            token);

        var payload = result.GetProperty("data").GetProperty("updateDisplayName");
        Assert.Equal("Trimmed Name", payload.GetProperty("displayName").GetString());
    }
}
