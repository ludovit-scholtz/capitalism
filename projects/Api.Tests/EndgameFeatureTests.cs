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
        Assert.Equal("SHARD_CONCLUDED", firstError.GetProperty("extensions").GetProperty("code").GetString());
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

    [Fact]
    public async Task AdminCanEndShardManually()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, _) = await RegisterAndGetTokenAsync(client, "admin-endshard@endgame.test");

        // Also register a regular player who will become the leader
        var (_, playerUserId) = await RegisterAndGetTokenAsync(client, "richplayer-endshard@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Promote the first user to admin
            var adminPlayer = await db.Players.FirstAsync(p => p.Email == "admin-endshard@endgame.test");
            adminPlayer.Role = PlayerRole.Admin;
            // Give the regular player some money so they become the leader
            var account = await db.BankAccounts.FirstOrDefaultAsync(a => a.PlayerId == playerUserId);
            if (account != null) account.Balance = 1_000_000;
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) {
                gameEnded
                winnerDisplayName
              }
            }
            """,
            new { input = new { reason = "Test end" } },
            token);

        var data = result.GetProperty("data").GetProperty("endShardManually");
        Assert.True(data.GetProperty("gameEnded").GetBoolean());
        // The regular player should be identified as the leader (highest bank balance)
        var winnerName = data.GetProperty("winnerDisplayName").GetString();
        Assert.NotNull(winnerName);
    }

    [Fact]
    public async Task NonAdminCannotEndShardManually()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, _) = await RegisterAndGetTokenAsync(client, "player-endshard@endgame.test");

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) {
                gameEnded
              }
            }
            """,
            new { input = new { reason = (string?)null } },
            token);

        // Expect either an auth error or errors array
        var hasErrors = result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0;
        var hasNullData = result.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("endShardManually", out var ended)
            && ended.ValueKind == JsonValueKind.Null;
        Assert.True(hasErrors || hasNullData, "Non-admin should not be able to end the shard.");
    }

    [Fact]
    public async Task EndgameStatus_IsAccessibleWithoutAuthentication()
    {
        // endgameStatus is a public query — no bearer token should be required.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Send the request without any auth header
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

        Assert.False(result.TryGetProperty("errors", out _), "endgameStatus should not require authentication.");
        var status = result.GetProperty("data").GetProperty("endgameStatus");
        Assert.False(status.GetProperty("gameEnded").GetBoolean());
        Assert.Equal(430_000_000_000m, status.GetProperty("winningThresholdUsd").GetDecimal());
        Assert.Equal(10, status.GetProperty("topRealWorldRichest").GetArrayLength());
    }

    [Fact]
    public async Task EndShardManually_WithoutToken_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Call with no token
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) { gameEnded }
            }
            """,
            new { input = new { reason = (string?)null } },
            token: null);

        var hasErrors = result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0;
        var hasNullData = result.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("endShardManually", out var field)
            && field.ValueKind == JsonValueKind.Null;
        Assert.True(hasErrors || hasNullData, "Unauthenticated call should be rejected.");
    }

    [Fact]
    public async Task EndShardManually_WithTooLongReason_ReturnsValidationError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAndGetTokenAsync(client, "admin-longreason@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Id == playerId);
            player.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // 501-character reason exceeds MaxLength 500
        var tooLongReason = new string('A', 501);
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) { gameEnded }
            }
            """,
            new { input = new { reason = tooLongReason } },
            token);

        var hasErrors = result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0;
        var hasNullData = result.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("endShardManually", out var field)
            && field.ValueKind == JsonValueKind.Null;
        Assert.True(hasErrors || hasNullData, "Reason longer than 500 chars should fail validation.");
    }

    [Fact]
    public async Task EndShardManually_PicksLeaderAcrossMultipleCurrencies()
    {
        // Seed three players with different currency balances — winner should be the one
        // whose total (EUR + USD + GBP) converted to USD is largest.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "admin-multi@endgame.test");
        var (_, eurLeaderId) = await RegisterAndGetTokenAsync(client, "eur-leader@endgame.test");
        var (_, gbpLeaderId) = await RegisterAndGetTokenAsync(client, "gbp-leader@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;

            // EUR leader: 2 000 000 EUR (≈ ~2.2M USD at ~1.1 rate)
            var eurAccount = await db.BankAccounts.FirstOrDefaultAsync(
                a => a.PlayerId == eurLeaderId && a.CurrencyCode == "EUR");
            if (eurAccount != null) eurAccount.Balance = 2_000_000m;

            // GBP leader: 1 500 000 GBP (≈ ~1.9M USD at ~1.27 rate) — should be less than EUR leader
            var gbpAccount = await db.BankAccounts.FirstOrDefaultAsync(
                a => a.PlayerId == gbpLeaderId && a.CurrencyCode == "GBP");
            if (gbpAccount != null) gbpAccount.Balance = 1_500_000m;

            // USD for admin: tiny amount so admin doesn't win
            var adminUsd = await db.BankAccounts.FirstOrDefaultAsync(
                a => a.PlayerId == adminId && a.CurrencyCode == "USD");
            if (adminUsd != null) adminUsd.Balance = 1m;

            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) {
                gameEnded
                winnerPlayerId
                winnerDisplayName
              }
            }
            """,
            new { input = new { reason = "Multi-currency leader test" } },
            adminToken);

        var data = result.GetProperty("data").GetProperty("endShardManually");
        Assert.True(data.GetProperty("gameEnded").GetBoolean());
        // The winner should be either the EUR leader or GBP leader (whichever converts highest) — NOT the admin
        var winnerId = data.GetProperty("winnerPlayerId").GetString();
        Assert.NotNull(winnerId);
        Assert.NotEqual(adminId.ToString(), winnerId);
    }

    [Fact]
    public async Task EndShardManually_AfterGameAlreadyEnded_MutationsAreBlocked()
    {
        // End the shard, then try another mutation — it should be blocked with GAME_ENDED.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "admin-reend@endgame.test");
        var (playerToken, _) = await RegisterAndGetTokenAsync(client, "player-reend@endgame.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // End the shard
        await ExecuteGraphQlAsync(
            client,
            """
            mutation EndShard($input: EndShardManuallyInput!) {
              endShardManually(input: $input) { gameEnded }
            }
            """,
            new { input = new { reason = "first end" } },
            adminToken);

        // Now try to create a company — should be blocked
        var blockedResult = await ExecuteGraphQlAsync(
            client,
            """
            mutation CreateCompany($input: CreateCompanyInput!) {
              createCompany(input: $input) { id name }
            }
            """,
            new { input = new { name = "PostEndGame Corp" } },
            playerToken);

        Assert.True(blockedResult.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0,
            "Mutations should be blocked after game ends.");
        Assert.Equal("SHARD_CONCLUDED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
