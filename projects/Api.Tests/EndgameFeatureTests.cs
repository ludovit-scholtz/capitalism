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
    public async Task EndgameStatus_ReturnsTopTenBenchmarks()
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
                topRealWorldRichest { rank name wealthUsd }
              }
            }
            """);

        var status = result.GetProperty("data").GetProperty("endgameStatus");
        Assert.False(status.GetProperty("gameEnded").GetBoolean());
        var list = status.GetProperty("topRealWorldRichest");
        Assert.Equal(10, list.GetArrayLength());
        Assert.Equal(1, list[0].GetProperty("rank").GetInt32());
        Assert.Equal(10, list[9].GetProperty("rank").GetInt32());
        Assert.True(list[0].GetProperty("wealthUsd").GetDecimal() >= list[1].GetProperty("wealthUsd").GetDecimal());
        Assert.Equal("Elon Musk", list[0].GetProperty("name").GetString());
        Assert.Equal(430_000_000_000m, status.GetProperty("winningThresholdUsd").GetDecimal());
    }

    [Fact]
    public async Task TickProcessor_WhenWinnerBelowRichestThreshold_DoesNotEndGame()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "winner@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.BankAccounts.FirstAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");
            account.Balance = 179_000_000_000m;
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
            Assert.False(gameState.GameEnded);
            Assert.Null(gameState.WinnerPlayerId);
        }
    }

    [Fact]
    public async Task TickProcessor_WhenWinnerAtOrAboveRichestThreshold_MarksGameEnded()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "winner-threshold@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.BankAccounts.FirstAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");
            account.Balance = 430_000_000_000m;
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
    public async Task TickProcessor_WhenWinnerBeatsOldFifthRichestButNotRichest_DoesNotEndGame()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "winner-fifth@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var account = await db.BankAccounts.FirstAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");
            account.Balance = 190_000_000_000m;
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
            Assert.False(gameState.GameEnded);
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

    [Fact]
    public async Task AdminCanUpdateRealWorldBillionaireBenchmarks()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAndGetTokenAsync(client, "admin-benchmark@endgame.test");

        Guid benchmarkId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Role = PlayerRole.Admin;
            benchmarkId = await db.RealWorldBillionaires
                .AsNoTracking()
                .Where(item => item.Rank == 1)
                .Select(item => item.Id)
                .FirstAsync();
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation UpdateBenchmark($input: UpdateRealWorldBillionaireInput!) {
              updateRealWorldBillionaire(input: $input) {
                id
                rank
                name
                wealthUsd
              }
            }
            """,
            new
            {
                input = new
                {
                    id = benchmarkId,
                    rank = 1,
                    name = "Elon Musk Updated",
                    wealthUsd = 431000000000m
                }
            },
            token);

        var updated = result.GetProperty("data").GetProperty("updateRealWorldBillionaire");
        Assert.Equal("Elon Musk Updated", updated.GetProperty("name").GetString());
        Assert.Equal(431_000_000_000m, updated.GetProperty("wealthUsd").GetDecimal());
    }
}
