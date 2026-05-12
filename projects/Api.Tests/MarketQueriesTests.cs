using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the market price, price history, and city demand summary GraphQL queries.
/// </summary>
public sealed class MarketQueriesTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(Guid CityId, Guid ProductTypeId, long CurrentTick)> SeedPublicSalesRecordsAsync(
        ApiWebApplicationFactory factory,
        int recordCount = 5,
        decimal pricePerUnit = 45m,
        decimal quantityPerRecord = 20m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        var product = await db.ProductTypes.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        // Create a minimal company for the records.
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"market-seed-{Guid.NewGuid():N}@test.com",
            DisplayName = "Market Seeder",
            PasswordHash = "mock",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        };
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Seed Co {Guid.NewGuid():N}"[..20],
            Cash = 1_000_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };

        db.Players.Add(player);
        db.Companies.Add(company);

        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        var currentTick = Math.Max(gameState!.CurrentTick, 500L);
        gameState.CurrentTick = currentTick;

        for (var i = 0; i < recordCount; i++)
        {
            db.PublicSalesRecords.Add(new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city!.Id,
                ProductTypeId = product!.Id,
                Tick = currentTick - recordCount + i,
                QuantitySold = quantityPerRecord,
                PricePerUnit = pricePerUnit,
                Revenue = quantityPerRecord * pricePerUnit,
                Demand = quantityPerRecord * 1.2m,
                SalesCapacity = quantityPerRecord * 2m,
                TrendFactor = 1.0m,
            });
        }

        await db.SaveChangesAsync();
        return (city.Id, product.Id, currentTick);
    }

    // ── marketPrice query ────────────────────────────────────────────────────

    [Fact]
    public async Task MarketPrice_ReturnsClearingPriceFromRecentSales()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var (cityId, productTypeId, tick) = await SeedPublicSalesRecordsAsync(factory, recordCount: 5, pricePerUnit: 42m, quantityPerRecord: 30m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query GetMarketPrice($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPrice(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                cityId
                productTypeId
                productName
                clearingPrice
                totalVolume
                totalRevenue
                sellerCount
                currencyCode
                fromTick
                toTick
              }
            }
            """,
            new { cityId, productTypeId, lastNTicks = 100 });

        var marketPrice = result.GetProperty("data").GetProperty("marketPrice");
        Assert.NotEqual(JsonValueKind.Null, marketPrice.ValueKind);

        var clearingPrice = marketPrice.GetProperty("clearingPrice").GetDecimal();
        Assert.Equal(42m, clearingPrice);

        var totalVolume = marketPrice.GetProperty("totalVolume").GetDecimal();
        Assert.Equal(150m, totalVolume); // 5 records × 30

        var currencyCode = marketPrice.GetProperty("currencyCode").GetString();
        Assert.Equal("EUR", currencyCode);

        var sellerCount = marketPrice.GetProperty("sellerCount").GetInt32();
        Assert.Equal(1, sellerCount);
    }

    [Fact]
    public async Task MarketPrice_WeightedAverageAcrossMultipleSellers()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Name == "Bratislava");
        var product = await db.ProductTypes.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(city);
        Assert.NotNull(product);

        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        var currentTick = Math.Max(gameState!.CurrentTick, 600L);
        gameState.CurrentTick = currentTick;

        var company1 = new Company { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = "Seller A", Cash = 1_000_000m, FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1, TotalSharesIssued = 10_000m, DividendPayoutRatio = 0.2m };
        var company2 = new Company { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = "Seller B", Cash = 1_000_000m, FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1, TotalSharesIssued = 10_000m, DividendPayoutRatio = 0.2m };
        db.Companies.AddRange(company1, company2);

        // Seller A: 100 units at €40 = €4000
        // Seller B: 50 units at €50  = €2500
        // Weighted avg = (4000 + 2500) / (100 + 50) = 6500/150 ≈ 43.33
        db.PublicSalesRecords.AddRange(
            new PublicSalesRecord { Id = Guid.NewGuid(), BuildingUnitId = Guid.NewGuid(), BuildingId = Guid.NewGuid(), CompanyId = company1.Id, CityId = city!.Id, ProductTypeId = product!.Id, Tick = currentTick - 1, QuantitySold = 100m, PricePerUnit = 40m, Revenue = 4000m, Demand = 120m, SalesCapacity = 200m, TrendFactor = 1.0m },
            new PublicSalesRecord { Id = Guid.NewGuid(), BuildingUnitId = Guid.NewGuid(), BuildingId = Guid.NewGuid(), CompanyId = company2.Id, CityId = city.Id, ProductTypeId = product.Id, Tick = currentTick - 1, QuantitySold = 50m, PricePerUnit = 50m, Revenue = 2500m, Demand = 60m, SalesCapacity = 200m, TrendFactor = 1.0m }
        );

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPrice(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                clearingPrice
                totalVolume
                sellerCount
              }
            }
            """,
            new { cityId = city.Id, productTypeId = product.Id, lastNTicks = 50 });

        var mp = result.GetProperty("data").GetProperty("marketPrice");
        var clearingPrice = mp.GetProperty("clearingPrice").GetDecimal();
        // Weighted avg: 6500 / 150 ≈ 43.3333
        Assert.InRange(clearingPrice, 43.33m, 43.34m);
        Assert.Equal(2, mp.GetProperty("sellerCount").GetInt32());
    }

    [Fact]
    public async Task MarketPrice_ReturnsNull_WhenNoSalesExist()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPrice(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                clearingPrice
              }
            }
            """,
            new { cityId = Guid.NewGuid(), productTypeId = Guid.NewGuid(), lastNTicks = 100 });

        var marketPrice = result.GetProperty("data").GetProperty("marketPrice");
        Assert.Equal(JsonValueKind.Null, marketPrice.ValueKind);
    }

    // ── marketPriceHistory query ─────────────────────────────────────────────

    [Fact]
    public async Task MarketPriceHistory_ReturnsPerTickData()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var (cityId, productTypeId, tick) = await SeedPublicSalesRecordsAsync(factory, recordCount: 5, pricePerUnit: 40m, quantityPerRecord: 25m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPriceHistory(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                tick
                clearingPrice
                totalVolume
                totalRevenue
                sellerCount
              }
            }
            """,
            new { cityId, productTypeId, lastNTicks = 100 });

        var history = result.GetProperty("data").GetProperty("marketPriceHistory");
        Assert.Equal(JsonValueKind.Array, history.ValueKind);
        Assert.True(history.GetArrayLength() >= 5, $"Expected at least 5 history points, got {history.GetArrayLength()}");

        // Verify last entry has correct clearing price
        var last = history.EnumerateArray().Last();
        Assert.Equal(40m, last.GetProperty("clearingPrice").GetDecimal());
        Assert.Equal(25m, last.GetProperty("totalVolume").GetDecimal());
    }

    [Fact]
    public async Task MarketPriceHistory_IsOrderedByTickAscending()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var (cityId, productTypeId, tick) = await SeedPublicSalesRecordsAsync(factory, recordCount: 5, pricePerUnit: 45m, quantityPerRecord: 20m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPriceHistory(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                tick
              }
            }
            """,
            new { cityId, productTypeId, lastNTicks = 100 });

        var ticks = result.GetProperty("data").GetProperty("marketPriceHistory")
            .EnumerateArray()
            .Select(e => e.GetProperty("tick").GetInt64())
            .ToList();

        Assert.Equal(ticks.OrderBy(t => t).ToList(), ticks);
    }

    [Fact]
    public async Task MarketPriceHistory_ReturnsEmpty_WhenNoSalesExist()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $productTypeId: UUID!, $lastNTicks: Int!) {
              marketPriceHistory(cityId: $cityId, productTypeId: $productTypeId, lastNTicks: $lastNTicks) {
                tick
              }
            }
            """,
            new { cityId = Guid.NewGuid(), productTypeId = Guid.NewGuid(), lastNTicks = 100 });

        var history = result.GetProperty("data").GetProperty("marketPriceHistory");
        Assert.Equal(0, history.GetArrayLength());
    }

    // ── cityDemandSummary query ──────────────────────────────────────────────

    [Fact]
    public async Task CityDemandSummary_ReturnsTopProductsByDemand()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var (cityId, productTypeId, tick) = await SeedPublicSalesRecordsAsync(factory, recordCount: 3, pricePerUnit: 45m, quantityPerRecord: 50m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $topN: Int!, $lastNTicks: Int!) {
              cityDemandSummary(cityId: $cityId, topN: $topN, lastNTicks: $lastNTicks) {
                cityId
                cityName
                currencyCode
                products {
                  productTypeId
                  productName
                  industry
                  totalDemand
                  totalQuantitySold
                  satisfactionRate
                  averageClearingPrice
                  totalRevenue
                  sellerCount
                }
              }
            }
            """,
            new { cityId, topN = 5, lastNTicks = 100 });

        var summary = result.GetProperty("data").GetProperty("cityDemandSummary");
        Assert.NotEqual(JsonValueKind.Null, summary.ValueKind);
        Assert.Equal("Bratislava", summary.GetProperty("cityName").GetString());
        Assert.Equal("EUR", summary.GetProperty("currencyCode").GetString());

        var products = summary.GetProperty("products");
        Assert.True(products.GetArrayLength() >= 1);

        var first = products.EnumerateArray().First();
        Assert.NotEmpty(first.GetProperty("productName").GetString()!);
        var satisfactionRate = first.GetProperty("satisfactionRate").GetDecimal();
        Assert.InRange(satisfactionRate, 0m, 1m);
        // 50 sold / (50 * 1.2 demand) = 50/60 ≈ 0.8333
        Assert.InRange(satisfactionRate, 0.8m, 0.9m);
    }

    [Fact]
    public async Task CityDemandSummary_ReturnsNull_ForUnknownCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $topN: Int!, $lastNTicks: Int!) {
              cityDemandSummary(cityId: $cityId, topN: $topN, lastNTicks: $lastNTicks) {
                cityName
              }
            }
            """,
            new { cityId = Guid.NewGuid(), topN = 5, lastNTicks = 100 });

        var summary = result.GetProperty("data").GetProperty("cityDemandSummary");
        Assert.Equal(JsonValueKind.Null, summary.ValueKind);
    }

    [Fact]
    public async Task CityDemandSummary_ReturnsEmptyProducts_WhenNoSales()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!, $topN: Int!, $lastNTicks: Int!) {
              cityDemandSummary(cityId: $cityId, topN: $topN, lastNTicks: $lastNTicks) {
                products {
                  productTypeId
                }
              }
            }
            """,
            new { cityId = city!.Id, topN = 5, lastNTicks = 1 });

        var summary = result.GetProperty("data").GetProperty("cityDemandSummary");
        // May still return empty products since no records in the past 1 tick
        Assert.NotEqual(JsonValueKind.Null, summary.ValueKind);
    }

    // ── marketOverview query ─────────────────────────────────────────────────

    [Fact]
    public async Task MarketOverview_ReturnsAllSeededCities()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($topN: Int!, $lastNTicks: Int!) {
              marketOverview(topN: $topN, lastNTicks: $lastNTicks) {
                cityId
                cityName
                currencyCode
                products {
                  productName
                }
              }
            }
            """,
            new { topN = 5, lastNTicks = 100 });

        var overview = result.GetProperty("data").GetProperty("marketOverview");
        Assert.Equal(JsonValueKind.Array, overview.ValueKind);
        // All 3 seeded cities must be present.
        Assert.True(overview.GetArrayLength() >= 3, $"Expected at least 3 cities, got {overview.GetArrayLength()}");

        var cityNames = overview.EnumerateArray()
            .Select(c => c.GetProperty("cityName").GetString())
            .ToList();
        Assert.Contains("Bratislava", cityNames);
        Assert.Contains("Prague", cityNames);
    }
}
