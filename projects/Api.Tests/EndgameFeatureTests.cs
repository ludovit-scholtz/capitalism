using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class EndgameFeatureTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<(string Token, Guid PlayerId)> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var register = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email, displayName = "Winner", password = "TestPass123!" } });

        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var playerId = Guid.Parse(register.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);
        return (token, playerId);
    }

    [Fact]
    public async Task EndgameStatus_ReturnsTopFiveBenchmarks()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            {
              endgameStatus {
                gameEnded
                winningThresholdUsd
                topRealWorldRichest { name wealthUsd }
              }
            }
            """);

        var status = result.GetProperty("data").GetProperty("endgameStatus");
        Assert.False(status.GetProperty("gameEnded").GetBoolean());
        var list = status.GetProperty("topRealWorldRichest");
        Assert.Equal(5, list.GetArrayLength());
        Assert.True(status.GetProperty("winningThresholdUsd").GetDecimal() > 0m);
    }

    [Fact]
    public async Task TickProcessor_WhenWinnerSurpassesThreshold_MarksGameEnded()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "winner@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.BankAccounts.FirstAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");
            account.Balance = 180_000_000_000m;
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = scope.ServiceProvider.GetServices<ITickPhase>();
            var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
            await processor.ProcessTickAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameState = await db.GameStates.FirstDeterministicAsync();
            Assert.True(gameState.GameEnded);
            Assert.Equal(playerId, gameState.WinnerPlayerId);
            Assert.False(string.IsNullOrWhiteSpace(gameState.WinnerDisplayName));
            Assert.NotNull(gameState.GameEndedAtUtc);
        }
    }

    [Fact]
    public async Task TickProcessor_WhenGameAlreadyEnded_DoesNotAdvanceTick()
    {
        await using var factory = new ApiWebApplicationFactory();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameState = await db.GameStates.FirstDeterministicAsync();
            gameState.GameEnded = true;
            gameState.CurrentTick = 123;
            gameState.WinnerDisplayName = "Winner";
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = scope.ServiceProvider.GetServices<ITickPhase>();
            var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
            await processor.ProcessTickAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameState = await db.GameStates.AsNoTracking().FirstDeterministicAsync();
            Assert.Equal(123, gameState.CurrentTick);
        }
    }

    [Fact]
    public async Task GraphQlMutations_WhenGameEnded_AreBlockedWithGameEndedError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, _) = await RegisterAndGetTokenAsync(client, "blocked@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameState = await db.GameStates.FirstDeterministicAsync();
            gameState.GameEnded = true;
            gameState.WinnerDisplayName = "Alice";
            await db.SaveChangesAsync();
        }

        var response = await ExecuteGraphQlAsync(
            client,
            """
            mutation CreateCompany($input: CreateCompanyInput!) {
              createCompany(input: $input) { id name }
            }
            """,
            new { input = new { name = "CannotCreate" } },
            token);

        var errors = response.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var firstError = errors[0];
        Assert.Equal("GAME_ENDED", firstError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Contains("Alice has won", firstError.GetProperty("message").GetString(), StringComparison.Ordinal);
    }
}
