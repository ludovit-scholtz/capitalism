using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for FX rate history snapshots:
/// FxRateHistoryPhase tick phase, GetFxRateHistory GraphQL query, and EurFxRate buy/sell fields.
/// </summary>
public sealed class FxRateHistoryTests
{
    #region FxRateHistoryPhase — tick snapshot creation

    [Fact]
    public async Task FxRateHistoryPhase_CapturesSnapshotsForAllSeededPairs()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Verify we have FX rates seeded (prerequisite).
        var rateCount = await db.FxRates.CountAsync();
        Assert.True(rateCount > 0, "FX rates must be seeded before the phase can run.");

        // Run the phase manually via the TickProcessor helper.
        var phase = new FxRateHistoryPhase();
        var gameState = await db.GameStates.FirstOrDefaultAsync() ?? new GameState { CurrentTick = 1 };
        var context = new TickContext { Db = db, GameState = gameState };

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        // Assert snapshots were created — one per EUR-based pair.
        var histories = await db.FxRateHistories.ToListAsync();
        Assert.True(histories.Count >= rateCount, $"Expected at least {rateCount} history entries, got {histories.Count}.");

        // All snapshots at the current tick.
        Assert.All(histories, h => Assert.Equal(gameState.CurrentTick, h.GameTick));
        Assert.All(histories, h => Assert.Equal("EUR", h.BaseCurrencyCode));
    }

    [Fact]
    public async Task FxRateHistoryPhase_BuyRateIsHigherThanMid_SellRateIsLower()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var phase = new FxRateHistoryPhase();
        var gameState = await db.GameStates.FirstOrDefaultAsync() ?? new GameState { CurrentTick = 5 };
        var context = new TickContext { Db = db, GameState = gameState };

        await phase.ProcessAsync(context);
        await db.SaveChangesAsync();

        var histories = await db.FxRateHistories.ToListAsync();
        Assert.NotEmpty(histories);

        foreach (var h in histories)
        {
            if (h.QuoteCurrencyCode == "EUR") continue; // EUR/EUR is always 1
            Assert.True(h.BuyRate > h.MidRate,
                $"BuyRate ({h.BuyRate}) must be greater than MidRate ({h.MidRate}) for {h.QuoteCurrencyCode}.");
            Assert.True(h.SellRate < h.MidRate,
                $"SellRate ({h.SellRate}) must be less than MidRate ({h.MidRate}) for {h.QuoteCurrencyCode}.");
            Assert.True(h.MidRate > 0, $"MidRate must be positive for {h.QuoteCurrencyCode}.");
        }
    }

    #endregion

    #region GetFxRateHistory — GraphQL query

    [Fact]
    public async Task GetFxRateHistory_WithSeededData_ReturnsSnapshotsOrderedByTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed two snapshots at different ticks for CZK.
        var now = DateTime.UtcNow;
        db.FxRateHistories.AddRange(
            new FxRateHistory { Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "CZK", MidRate = 25.0m, BuyRate = 25.125m, SellRate = 24.875m, GameTick = 10, CapturedAtUtc = now },
            new FxRateHistory { Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "CZK", MidRate = 25.5m, BuyRate = 25.6275m, SellRate = 25.3725m, GameTick = 20, CapturedAtUtc = now.AddMinutes(1) }
        );
        await db.SaveChangesAsync();

        using var client = factory.CreateClient();
        var doc = await ExecuteGraphQlAsync(client,
            """
            query { fxRateHistory(quoteCurrencyCode: "CZK", ticksBack: 100) { baseCurrencyCode quoteCurrencyCode midRate buyRate sellRate gameTick } }
            """);

        Assert.False(doc.TryGetProperty("errors", out _), "fxRateHistory must not return errors.");
        var items = doc.GetProperty("data").GetProperty("fxRateHistory").EnumerateArray().ToList();

        Assert.True(items.Count >= 2, $"Expected at least 2 history items, got {items.Count}.");

        // Verify ordering: oldest first.
        var ticks = items.Select(i => i.GetProperty("gameTick").GetInt64()).ToList();
        for (var i = 0; i < ticks.Count - 1; i++)
        {
            Assert.True(ticks[i] <= ticks[i + 1],
                $"Items must be ordered by tick ascending. Got tick {ticks[i]} before {ticks[i + 1]}.");
        }

        // Verify buy/sell spread in returned data.
        var czk10 = items.FirstOrDefault(i => i.GetProperty("gameTick").GetInt64() == 10);
        Assert.True(czk10.ValueKind != JsonValueKind.Undefined, "Snapshot at tick 10 must be present.");
        Assert.Equal(25.0m, czk10.GetProperty("midRate").GetDecimal());
        Assert.True(czk10.GetProperty("buyRate").GetDecimal() > czk10.GetProperty("midRate").GetDecimal());
        Assert.True(czk10.GetProperty("sellRate").GetDecimal() < czk10.GetProperty("midRate").GetDecimal());
    }

    [Fact]
    public async Task GetFxRateHistory_EmptyForUnknownCurrency_ReturnsEmptyList()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var doc = await ExecuteGraphQlAsync(client,
            """
            query { fxRateHistory(quoteCurrencyCode: "XYZ", ticksBack: 10) { gameTick midRate } }
            """);

        Assert.False(doc.TryGetProperty("errors", out _), "Unknown currency must return empty list, not an error.");
        var items = doc.GetProperty("data").GetProperty("fxRateHistory").EnumerateArray().ToList();
        Assert.Empty(items);
    }

    [Fact]
    public async Task GetFxRateHistory_IsPublicQuery_WorksWithoutAuthentication()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.FxRateHistories.Add(new FxRateHistory
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "USD",
            MidRate = 1.08m, BuyRate = 1.0854m, SellRate = 1.0746m, GameTick = 1, CapturedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        using var client = factory.CreateClient();
        // No auth token — must still work.
        var doc = await ExecuteGraphQlAsync(client,
            """query { fxRateHistory(quoteCurrencyCode: "USD") { gameTick midRate buyRate sellRate } }""",
            token: null);

        Assert.False(doc.TryGetProperty("errors", out _), "fxRateHistory must be publicly accessible.");
        var items = doc.GetProperty("data").GetProperty("fxRateHistory").EnumerateArray().ToList();
        Assert.NotEmpty(items);
    }

    #endregion

    #region EurFxRate buy/sell computed fields

    [Fact]
    public async Task EurFxRates_GraphQL_ReturnsBuyAndSellFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var doc = await ExecuteGraphQlAsync(client,
            "{ eurFxRates { currencyCode rate midRate buyRate sellRate } }");

        Assert.False(doc.TryGetProperty("errors", out _), "eurFxRates must not return errors.");
        var rates = doc.GetProperty("data").GetProperty("eurFxRates").EnumerateArray().ToList();
        Assert.NotEmpty(rates);

        foreach (var rate in rates)
        {
            var code = rate.GetProperty("currencyCode").GetString();
            var mid = rate.GetProperty("midRate").GetDecimal();
            var buy = rate.GetProperty("buyRate").GetDecimal();
            var sell = rate.GetProperty("sellRate").GetDecimal();

            Assert.True(mid > 0, $"Mid rate must be positive for {code}.");
            if (code != "EUR")
            {
                Assert.True(buy > mid, $"BuyRate ({buy}) must exceed MidRate ({mid}) for {code}.");
                Assert.True(sell < mid, $"SellRate ({sell}) must be below MidRate ({mid}) for {code}.");
            }
        }
    }

    #endregion

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { query, variables }),
            System.Text.Encoding.UTF8, "application/json");
        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
        return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(body);
    }
}
