using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Capitalism.Shared.Security;
using MasterApi.Tests.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace MasterApi.Tests;

/// <summary>
/// Tests for gold token deposit requests, note text generation, and the automated
/// blockchain scanner matching logic.
/// </summary>
public sealed class GoldTokenDepositTests
{
    private const string SharedJwtIssuer = "Capitalism";
    private const string SharedJwtAudience = "Capitalism";
    private const string SharedJwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";

    private static string CreateUserToken(string email, Guid userId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SharedJwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: SharedJwtIssuer,
            audience: SharedJwtAudience,
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Name, email.Split('@')[0]),
                new Claim(TokenBoundaryClaims.TokenTypeClaimType, TokenBoundaryClaims.TokenTypeMaster),
            ],
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<JsonElement> GraphQlAsync(
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

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static async Task<(string Token, string PlayerId, string Email)> RegisterAsync(
        HttpClient client,
        string email)
    {
        var result = await GraphQlAsync(client, """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id email }
              }
            }
            """,
            new
            {
                input = new
                {
                    email,
                    displayName = email.Split('@')[0],
                    password = "password123",
                }
            });

        if (result.TryGetProperty("errors", out _))
            throw new InvalidOperationException($"Register failed: {result}");

        var register = result.GetProperty("data").GetProperty("register");
        var token = register.GetProperty("token").GetString()!;
        var playerId = register.GetProperty("player").GetProperty("id").GetString()!;
        return (token, playerId, email);
    }

    // ── CreateGoldTokenDepositRequest ──────────────────────────────────────────

    [Fact]
    public async Task CreateGoldTokenDepositRequest_UnauthenticatedUser_ReturnsError()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
                noteText
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "ALGORAND",
                    amount = 1.0m,
                    senderAddress = "TESTSENDER"
                }
            });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray().ToList());
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_SetsNoteText_WithCapPrefix()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
                noteText
                status
                network
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "ALGORAND",
                    amount = 5.0m,
                    senderAddress = "TESTSENDER"
                }
            },
            authToken);

        Assert.False(result.TryGetProperty("errors", out _), $"Unexpected errors: {result}");
        var depositResult = result.GetProperty("data").GetProperty("createGoldTokenDepositRequest");

        var requestId = depositResult.GetProperty("id").GetString()!;
        var noteText = depositResult.GetProperty("noteText").GetString()!;
        var status = depositResult.GetProperty("status").GetString();

        Assert.NotEmpty(requestId);
        Assert.Equal($"CAP-{requestId}", noteText);
        Assert.Equal("PENDING", status);
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_NoteText_MatchesCapPlusGuidPattern()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-pattern-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
                noteText
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "VOI",
                    amount = 10.0m,
                }
            },
            authToken);

        Assert.False(result.TryGetProperty("errors", out _), $"Unexpected errors: {result}");
        var depositResult = result.GetProperty("data").GetProperty("createGoldTokenDepositRequest");

        var requestId = depositResult.GetProperty("id").GetString()!;
        var noteText = depositResult.GetProperty("noteText").GetString()!;

        // Validate the pattern: "CAP-" followed by a valid GUID
        Assert.StartsWith("CAP-", noteText, StringComparison.Ordinal);
        var guidPart = noteText["CAP-".Length..];
        Assert.True(Guid.TryParse(guidPart, out var parsedGuid), $"'{guidPart}' is not a valid GUID");
        Assert.Equal(Guid.Parse(requestId), parsedGuid);
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_Algorand_AssignsCorrectAssetId()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-algo-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
                assetId
                network
                noteText
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "ALGORAND",
                    amount = 2.5m,
                }
            },
            authToken);

        Assert.False(result.TryGetProperty("errors", out _), $"Unexpected errors: {result}");
        var depositResult = result.GetProperty("data").GetProperty("createGoldTokenDepositRequest");

        Assert.Equal("ALGORAND", depositResult.GetProperty("network").GetString());
        Assert.Equal(1241944285L, depositResult.GetProperty("assetId").GetInt64());
        Assert.StartsWith("CAP-", depositResult.GetProperty("noteText").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_Voi_AssignsCorrectAssetId()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-voi-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
                assetId
                network
                noteText
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "VOI",
                    amount = 3.0m,
                }
            },
            authToken);

        Assert.False(result.TryGetProperty("errors", out _), $"Unexpected errors: {result}");
        var depositResult = result.GetProperty("data").GetProperty("createGoldTokenDepositRequest");

        Assert.Equal("VOI", depositResult.GetProperty("network").GetString());
        Assert.Equal(302228L, depositResult.GetProperty("assetId").GetInt64());
        Assert.StartsWith("CAP-", depositResult.GetProperty("noteText").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_ZeroAmount_ReturnsError()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-zero-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "ALGORAND",
                    amount = 0.0m,
                }
            },
            authToken);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected validation error for zero amount");
        var errorList = errors.EnumerateArray().ToList();
        Assert.NotEmpty(errorList);
    }

    [Fact]
    public async Task CreateGoldTokenDepositRequest_NegativeAmount_ReturnsError()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-neg-{Guid.NewGuid():N}@example.com");

        var result = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) {
                id
              }
            }
            """,
            new
            {
                input = new
                {
                    network = "ALGORAND",
                    amount = -1.0m,
                }
            },
            authToken);

        Assert.True(result.TryGetProperty("errors", out var errors), "Expected validation error for negative amount");
        var errorList = errors.EnumerateArray().ToList();
        Assert.NotEmpty(errorList);
    }

    // ── NoteText uniqueness across requests ────────────────────────────────────

    [Fact]
    public async Task CreateGoldTokenDepositRequest_TwoRequests_HaveDifferentNoteTxts()
    {
        await using var factory = new MasterApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var (authToken, _, _) = await RegisterAsync(client, $"golddep-unique-{Guid.NewGuid():N}@example.com");

        var result1 = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) { id noteText }
            }
            """,
            new { input = new { network = "ALGORAND", amount = 1.0m } },
            authToken);

        var result2 = await GraphQlAsync(client, """
            mutation CreateDeposit($input: CreateGoldTokenDepositRequestInput!) {
              createGoldTokenDepositRequest(input: $input) { id noteText }
            }
            """,
            new { input = new { network = "ALGORAND", amount = 2.0m } },
            authToken);

        var note1 = result1.GetProperty("data").GetProperty("createGoldTokenDepositRequest").GetProperty("noteText").GetString()!;
        var note2 = result2.GetProperty("data").GetProperty("createGoldTokenDepositRequest").GetProperty("noteText").GetString()!;

        Assert.NotEqual(note1, note2);
    }
}
