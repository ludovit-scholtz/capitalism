using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class VictoryCheckPhaseTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<(string Token, Guid PlayerId)> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(client, """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token player { id } }
            }
            """, new { input = new { email, displayName = "Tester", password = "TestPass123!" } });

        var token = result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var playerId = Guid.Parse(result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);
        return (token, playerId);
    }

    private static TickProcessor CreateProcessor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance);
    }

    // -------------------------------------------------------------------
    // BillionaireBenchmarkUsd query

    [Fact]
    public async Task BillionaireBenchmarkUsd_ReturnsConfiguredValue()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client, "{ billionaireBenchmarkUsd }");
        var value = result.GetProperty("data").GetProperty("billionaireBenchmarkUsd").GetDecimal();
        Assert.True(value > 0m, "billionaireBenchmarkUsd should be a positive number.");
        // Default is 200 billion USD
        Assert.Equal(200_000_000_000m, value);
    }

    /// <summary>
    /// billionaireBenchmarkUsd is intentionally a public query — unauthenticated callers
    /// can read the race-to-top target so they know the win condition without logging in.
    /// </summary>
    [Fact]
    public async Task BillionaireBenchmarkUsd_Unauthenticated_ReturnsPublicValue()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // No token — this query is intentionally public (no [Authorize])
        var result = await ExecuteGraphQlAsync(client, "{ billionaireBenchmarkUsd }");

        // Should not return errors
        Assert.False(result.TryGetProperty("errors", out _),
            "billionaireBenchmarkUsd must be accessible without authentication.");

        var value = result.GetProperty("data").GetProperty("billionaireBenchmarkUsd").GetDecimal();
        Assert.True(value > 0m, "Public billionaireBenchmarkUsd should be a positive number.");
    }

    // -------------------------------------------------------------------
    // ShardStatus query

    [Fact]
    public async Task ShardStatus_InitialState_IsActive()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client, """
            {
              shardStatus {
                shardState
                gameEnded
                winnerDisplayName
                winnerCompanyName
                winnerNetWorth
              }
            }
            """);

        var status = result.GetProperty("data").GetProperty("shardStatus");
        Assert.Equal("ACTIVE", status.GetProperty("shardState").GetString());
        Assert.False(status.GetProperty("gameEnded").GetBoolean());
        Assert.True(status.GetProperty("winnerDisplayName").ValueKind == JsonValueKind.Null
            || string.IsNullOrEmpty(status.GetProperty("winnerDisplayName").GetString()));
    }

    // -------------------------------------------------------------------
    // VictoryCheckPhase — Below threshold, no conclusion

    [Fact]
    public async Task VictoryCheckPhase_BelowThreshold_DoesNotConcludeShard()
    {
        await using var factory = new ApiWebApplicationFactory();

        // Set benchmark to 1 trillion so player wealth (near zero) will not trigger conclusion.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 1_000_000_000_000m;
        }

        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-below@victory.test");

        // Give the player a personal bank account with modest balance
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000001",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 1_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None);
        }

        // Shard must still be active
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            Assert.Equal(GameShardState.Active, gs.ShardState);
            Assert.False(gs.GameEnded);
        }
    }

    // -------------------------------------------------------------------
    // VictoryCheckPhase — Above threshold triggers conclusion

    [Fact]
    public async Task VictoryCheckPhase_AboveThreshold_SetsConcluded_AndCreatesNewsletter()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-winner@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Set a very low threshold so the player's seeded balance triggers victory.
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 500m;

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000002",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 1_000m,   // > 500 USD threshold
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            Assert.Equal(GameShardState.Concluded, gs.ShardState);
            Assert.True(gs.GameEnded);
            Assert.Equal(playerId, gs.WinnerPlayerId);
            Assert.NotNull(gs.WinnerNetWorth);
            Assert.True(gs.WinnerNetWorth > 0m);

            var newsletter = await db.VictoryNewsletters.FirstOrDefaultAsync();
            Assert.NotNull(newsletter);
            Assert.Equal(playerId, newsletter.WinnerPlayerId);
            Assert.True(newsletter.WinnerNetWorthUsd > 0m);
            Assert.False(string.IsNullOrWhiteSpace(newsletter.Top10RankingsJson));
        }
    }

    // -------------------------------------------------------------------
    // VictoryCheckPhase — Idempotency

    [Fact]
    public async Task VictoryCheckPhase_Idempotent_WhenAlreadyConcluded()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-idempotent@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 500m;

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000003",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 1_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None); // First tick concludes
            await processor.ProcessTickAsync(CancellationToken.None); // Second tick must be no-op
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            // Exactly one newsletter — second tick should not add another
            var count = await db.VictoryNewsletters.CountAsync();
            Assert.Equal(1, count);
        }
    }

    // -------------------------------------------------------------------
    // VictoryCheckPhase — Winner net worth is stored

    [Fact]
    public async Task VictoryCheckPhase_SetsWinnerNetWorth_OnGameState()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-networth@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 500m;

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000004",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 2_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            Assert.NotNull(gs.WinnerNetWorth);
            Assert.True(gs.WinnerNetWorth >= 500m, "Winner net worth should be at least the balance converted to USD.");
        }
    }

    // -------------------------------------------------------------------
    // VictoryCheckPhase — No players, does not conclude

    [Fact]
    public async Task VictoryCheckPhase_NoRegularPlayers_DoesNotConclude()
    {
        await using var factory = new ApiWebApplicationFactory();
        // We don't register any player — only system/government actors exist.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 1m;  // Extremely low threshold

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            Assert.Equal(GameShardState.Active, gs.ShardState);
            Assert.False(gs.GameEnded);
        }
    }

    // -------------------------------------------------------------------
    // ForceShardConclusion — Unauthenticated returns error

    [Fact]
    public async Task ForceShardConclusion_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client, """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) {
                shardState
              }
            }
            """, new { input = new { reason = "test" } });

        // Unauthenticated: HotChocolate returns an auth error in errors array
        Assert.True(result.TryGetProperty("errors", out var errors), "Should return errors when unauthenticated.");
        Assert.True(errors.GetArrayLength() > 0);
    }

    // -------------------------------------------------------------------
    // ForceShardConclusion — Non-admin player gets rejected

    [Fact]
    public async Task ForceShardConclusion_NonAdmin_ReturnsAuthorizationError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, _) = await RegisterAndGetTokenAsync(client, "victory-nonadmin@victory.test");

        var result = await ExecuteGraphQlAsync(client, """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) {
                shardState
              }
            }
            """, new { input = new { reason = "test" } }, token);

        Assert.True(result.TryGetProperty("errors", out var errors), "Non-admin should get an error.");
        Assert.True(errors.GetArrayLength() > 0);
    }

    // -------------------------------------------------------------------
    // ForceShardConclusion — Admin can force conclusion

    [Fact]
    public async Task ForceShardConclusion_AsAdmin_ConcludesShardAndCreatesNewsletter()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "victory-admin@victory.test");
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-richplayer@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000005",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 10_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(client, """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) {
                shardState
                gameEnded
                winnerDisplayName
              }
            }
            """, new { input = new { reason = "Integration test force conclusion" } }, adminToken);

        var data = result.GetProperty("data").GetProperty("forceShardConclusion");
        Assert.Equal("CONCLUDED", data.GetProperty("shardState").GetString());
        Assert.True(data.GetProperty("gameEnded").GetBoolean());
        Assert.False(string.IsNullOrEmpty(data.GetProperty("winnerDisplayName").GetString()));

        // Verify newsletter was created
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var newsletter = await db.VictoryNewsletters.FirstOrDefaultAsync();
            Assert.NotNull(newsletter);
            Assert.Equal(playerId, newsletter.WinnerPlayerId);
        }
    }

    // -------------------------------------------------------------------
    // VictoryNewsletter query — returns null when not yet concluded

    [Fact]
    public async Task VictoryNewsletter_WhenNotConcluded_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client, """
            {
              victoryNewsletter {
                winnerDisplayName
                winnerNetWorthUsd
                gameDurationTicks
              }
            }
            """);

        var newsletter = result.GetProperty("data").GetProperty("victoryNewsletter");
        Assert.Equal(JsonValueKind.Null, newsletter.ValueKind);
    }

    // -------------------------------------------------------------------
    // VictoryNewsletter query — returns data after conclusion

    [Fact]
    public async Task VictoryNewsletter_AfterConclusion_ReturnsWinnerData()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAndGetTokenAsync(client, "victory-newsletter@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var opts = scope.ServiceProvider.GetRequiredService<IOptions<GameRulesOptions>>();
            opts.Value.BillionaireNetWorthBenchmarkUsd = 500m;

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = "VTEST000000000006",
                PlayerId = playerId,
                CurrencyCode = "EUR",
                Balance = 1_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var processor = CreateProcessor(scope);
            await processor.ProcessTickAsync(CancellationToken.None);
        }

        var result = await ExecuteGraphQlAsync(client, """
            {
              victoryNewsletter {
                winnerDisplayName
                winnerCompanyName
                winnerNetWorthUsd
                gameDurationTicks
                top10RankingsJson
              }
            }
            """);

        var newsletter = result.GetProperty("data").GetProperty("victoryNewsletter");
        Assert.NotEqual(JsonValueKind.Null, newsletter.ValueKind);
        Assert.False(string.IsNullOrEmpty(newsletter.GetProperty("winnerDisplayName").GetString()));
        Assert.True(newsletter.GetProperty("winnerNetWorthUsd").GetDecimal() > 0m);
        Assert.True(newsletter.GetProperty("gameDurationTicks").GetInt64() >= 0);
        Assert.False(string.IsNullOrEmpty(newsletter.GetProperty("top10RankingsJson").GetString()));
    }

    // -------------------------------------------------------------------
    // ShardStatus — public query, accessible without auth

    [Fact]
    public async Task ShardStatus_Unauthenticated_ReturnsPublicActiveState()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // No token — shardStatus must be a public query so unauthenticated players
        // can check whether the shard has concluded without logging in.
        var result = await ExecuteGraphQlAsync(client, """
            {
              shardStatus {
                shardState
                gameEnded
              }
            }
            """);

        Assert.False(result.TryGetProperty("errors", out _),
            "shardStatus must be accessible without authentication.");

        var status = result.GetProperty("data").GetProperty("shardStatus");
        Assert.Equal("ACTIVE", status.GetProperty("shardState").GetString());
        Assert.False(status.GetProperty("gameEnded").GetBoolean());
    }

    // -------------------------------------------------------------------
    // VictoryNewsletter — public query, accessible without auth

    [Fact]
    public async Task VictoryNewsletter_Unauthenticated_ReturnsNullWhenNotConcluded()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // No token — victoryNewsletter must be public so any visitor can read it
        var result = await ExecuteGraphQlAsync(client, """
            {
              victoryNewsletter {
                winnerDisplayName
              }
            }
            """);

        Assert.False(result.TryGetProperty("errors", out _),
            "victoryNewsletter must be accessible without authentication.");

        Assert.Equal(JsonValueKind.Null,
            result.GetProperty("data").GetProperty("victoryNewsletter").ValueKind);
    }

    // -------------------------------------------------------------------
    // ForceShardConclusion — SHARD_ALREADY_CONCLUDED guard

    [Fact]
    public async Task ForceShardConclusion_WhenAlreadyConcluded_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "victory-double-admin@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        const string mutBody = """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) {
                shardState
                gameEnded
              }
            }
            """;
        var input = new { input = new { reason = "First conclusion" } };

        // First call succeeds
        var first = await ExecuteGraphQlAsync(client, mutBody, input, adminToken);
        Assert.Equal("CONCLUDED",
            first.GetProperty("data").GetProperty("forceShardConclusion").GetProperty("shardState").GetString());

        // Second call must be rejected — either by the SHARD_CONCLUDED middleware
        // or by the mutation's own SHARD_ALREADY_CONCLUDED guard
        var second = await ExecuteGraphQlAsync(client, mutBody, new { input = new { reason = "Duplicate" } }, adminToken);
        Assert.True(second.TryGetProperty("errors", out var errors),
            "Second forceShardConclusion must return errors.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.True(code is "SHARD_ALREADY_CONCLUDED" or "SHARD_CONCLUDED",
            $"Expected SHARD_ALREADY_CONCLUDED or SHARD_CONCLUDED but got: {code}");
    }

    // -------------------------------------------------------------------
    // ForceShardConclusion — empty reason is rejected

    [Fact]
    public async Task ForceShardConclusion_EmptyReason_ReturnsReasonRequiredError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "victory-empty-reason@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(client, """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) {
                shardState
              }
            }
            """, new { input = new { reason = "   " } }, adminToken);

        Assert.True(result.TryGetProperty("errors", out var errors),
            "Empty-reason forceShardConclusion must return an error.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("REASON_REQUIRED", code);
    }

    // -------------------------------------------------------------------
    // SHARD_CONCLUDED middleware — mutations blocked after conclusion

    [Fact]
    public async Task ShardConcluded_RegularMutationsAreBlocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (adminToken, adminId) = await RegisterAndGetTokenAsync(client, "victory-block-admin@victory.test");
        var (playerToken, _) = await RegisterAndGetTokenAsync(client, "victory-block-player@victory.test");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Id == adminId);
            admin.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // Conclude the shard
        await ExecuteGraphQlAsync(client, """
            mutation ForceConclusion($input: ForceShardConclusionInput!) {
              forceShardConclusion(input: $input) { shardState }
            }
            """, new { input = new { reason = "Block test conclusion" } }, adminToken);

        // Try to create a company — should be blocked
        var blocked = await ExecuteGraphQlAsync(client, """
            mutation CreateCompany($input: CreateCompanyInput!) {
              createCompany(input: $input) { id name }
            }
            """, new { input = new { name = "ShouldBeBlocked" } }, playerToken);

        Assert.True(blocked.TryGetProperty("errors", out var errors),
            "Regular mutations must be blocked after shard conclusion.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("SHARD_CONCLUDED", code);
    }
}
