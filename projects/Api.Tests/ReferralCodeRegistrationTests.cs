using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the referral code system:
/// - Registration with a valid referral code persists the code on the player record.
/// - Registration without a referral code leaves AppliedReferralCode null.
/// - Invalid/malformed codes are silently ignored (normalised to null).
/// - Backend code length limits: 4–20 alphanumeric characters.
/// </summary>
public sealed class ReferralCodeRegistrationTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static readonly string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            player { id email appliedReferralCode }
          }
        }
        """;

    // -----------------------------------------------------------------------
    // 1. Registration without a referral code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithoutReferralCode_LeavesAppliedReferralCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email = "noref@example.com", displayName = "No Ref", password = "TestPass123!" } });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);

        // Also verify via database
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbPlayer = await db.Players.FirstAsync(p => p.Email == "noref@example.com");
        Assert.Null(dbPlayer.AppliedReferralCode);
    }

    // -----------------------------------------------------------------------
    // 2. Registration WITH a valid referral code
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithValidReferralCode_PersistsNormalizedCodeOnPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "withref@example.com",
                    displayName = "With Ref",
                    password = "TestPass123!",
                    referralCode = "ABC12345"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABC12345", player.GetProperty("appliedReferralCode").GetString());

        // Also verify via database
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbPlayer = await db.Players.FirstAsync(p => p.Email == "withref@example.com");
        Assert.Equal("ABC12345", dbPlayer.AppliedReferralCode);
    }

    // -----------------------------------------------------------------------
    // 3. Code is normalised: lowercase input → uppercase stored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithLowercaseReferralCode_StoresUppercase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "lowref@example.com",
                    displayName = "Low Ref",
                    password = "TestPass123!",
                    referralCode = "abc12345"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABC12345", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 4. Codes with whitespace are trimmed then validated
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithWhitespacePaddedReferralCode_StoresTrimmedUppercase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "trimref@example.com",
                    displayName = "Trim Ref",
                    password = "TestPass123!",
                    referralCode = "  REF99999  "
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("REF99999", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 5. Codes shorter than 4 characters are silently ignored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTooShortReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "shortref@example.com",
                    displayName = "Short Ref",
                    password = "TestPass123!",
                    referralCode = "AB"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 6. Codes with special characters are silently ignored
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithSpecialCharReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "specialref@example.com",
                    displayName = "Special Ref",
                    password = "TestPass123!",
                    referralCode = "BAD!CODE"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 7. Code at exactly 4 characters (lower boundary) is accepted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithFourCharReferralCode_IsAccepted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "fourref@example.com",
                    displayName = "Four Ref",
                    password = "TestPass123!",
                    referralCode = "ABCD"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABCD", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 8. Code at exactly 20 characters (upper boundary) is accepted
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTwentyCharReferralCode_IsAccepted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "twentyref@example.com",
                    displayName = "Twenty Ref",
                    password = "TestPass123!",
                    referralCode = "ABCDEFGHIJ1234567890"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal("ABCDEFGHIJ1234567890", player.GetProperty("appliedReferralCode").GetString());
    }

    // -----------------------------------------------------------------------
    // 9. Code with 21 characters is silently ignored (over max)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Register_WithTwentyOneCharReferralCode_LeavesAppliedCodeNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "longref@example.com",
                    displayName = "Long Ref",
                    password = "TestPass123!",
                    referralCode = "ABCDEFGHIJ12345678901"
                }
            });

        var player = result.GetProperty("data").GetProperty("register").GetProperty("player");
        Assert.Equal(JsonValueKind.Null, player.GetProperty("appliedReferralCode").ValueKind);
    }

    // -----------------------------------------------------------------------
    // 10. Referral code field is returned in the `me` query
    // -----------------------------------------------------------------------

    [Fact]
    public async Task Me_Query_ReturnsAppliedReferralCode()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register with a code
        var regResult = await ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new
            {
                input = new
                {
                    email = "meref@example.com",
                    displayName = "Me Ref",
                    password = "TestPass123!",
                    referralCode = "MYCODE01"
                }
            });

        var token = regResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        // Query `me`
        var meResult = await ExecuteGraphQlAsync(
            client,
            "{ me { id appliedReferralCode } }",
            token: token);

        var mePlayer = meResult.GetProperty("data").GetProperty("me");
        Assert.Equal("MYCODE01", mePlayer.GetProperty("appliedReferralCode").GetString());
    }
}
