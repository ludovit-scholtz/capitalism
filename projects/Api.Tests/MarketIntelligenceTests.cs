using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class MarketIntelligenceTests
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
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task MarketIntelligence_ReturnsRankedSellersWithBrandQualityAndWeeklyVolume()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "market-owner@test.com", "Market Owner");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        var ownPlayer = await db.Players.FirstOrDefaultAsync(p => p.Email == "market-owner@test.com");
        Assert.NotNull(ownPlayer);

        var competitorPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = "market-rival@test.com",
            DisplayName = "Rival Owner",
            PasswordHash = "mock",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(competitorPlayer);

        var ownCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownPlayer!.Id,
            Name = "Alpha Industries",
            Cash = 100_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };

        var rivalCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = competitorPlayer.Id,
            Name = "Beta Manufacturing",
            Cash = 100_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };

        db.Companies.AddRange(ownCompany, rivalCompany);

        db.Brands.AddRange(
            new Brand
            {
                Id = Guid.NewGuid(),
                CompanyId = ownCompany.Id,
                Name = "Alpha Chair",
                Scope = BrandScope.Product,
                ProductTypeId = product!.Id,
                Awareness = 0.5m,
                Quality = 0.65m,
                MarketingQuality = 0.45m,
            },
            new Brand
            {
                Id = Guid.NewGuid(),
                CompanyId = rivalCompany.Id,
                Name = "Beta Chair",
                Scope = BrandScope.Product,
                ProductTypeId = product.Id,
                Awareness = 0.4m,
                Quality = 0.4m,
                MarketingQuality = 0.3m,
            });

        var currentTick = 300L;
        var gameState = await db.GameStates.FindAsync(1);
        Assert.NotNull(gameState);
        gameState!.CurrentTick = currentTick;
        gameState.LastTickAtUtc = DateTime.UtcNow;

        db.PublicSalesRecords.AddRange(
            new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = ownCompany.Id,
                CityId = city!.Id,
                ProductTypeId = product.Id,
                Tick = currentTick - 8,
                QuantitySold = 220m,
                PricePerUnit = 46m,
                Revenue = 10_120m,
                Demand = 260m,
                SalesCapacity = 240m,
                TrendFactor = 1.04m,
            },
            new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = ownCompany.Id,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = currentTick - 2,
                QuantitySold = 210m,
                PricePerUnit = 47m,
                Revenue = 9_870m,
                Demand = 250m,
                SalesCapacity = 240m,
                TrendFactor = 1.06m,
            },
            new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = rivalCompany.Id,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = currentTick - 4,
                QuantitySold = 180m,
                PricePerUnit = 43m,
                Revenue = 7_740m,
                Demand = 220m,
                SalesCapacity = 240m,
                TrendFactor = 0.98m,
            });

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query MarketIntelligence($cityId: UUID!) {
              marketIntelligence(cityId: $cityId) {
                cityId
                cityName
                products {
                  productName
                  sellers {
                    rank
                    displayName
                    askingPricePerUnit
                    brandQuality
                    estimatedWeeklySalesVolume
                  }
                }
              }
            }
            """,
            new { cityId = city.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");

        var products = result.GetProperty("data").GetProperty("marketIntelligence").GetProperty("products");
        Assert.True(products.GetArrayLength() > 0, "Expected at least one product row.");

        var firstProduct = products[0];
        Assert.Equal("Wooden Chair", firstProduct.GetProperty("productName").GetString());

        var sellers = firstProduct.GetProperty("sellers");
        Assert.True(sellers.GetArrayLength() >= 2, "Expected ranked sellers.");

        var firstSeller = sellers[0];
        var secondSeller = sellers[1];

        Assert.Equal(1, firstSeller.GetProperty("rank").GetInt32());
        Assert.Equal("Alpha Industries", firstSeller.GetProperty("displayName").GetString());
        Assert.True(firstSeller.GetProperty("estimatedWeeklySalesVolume").GetDecimal() > secondSeller.GetProperty("estimatedWeeklySalesVolume").GetDecimal());
        Assert.Equal(47m, firstSeller.GetProperty("askingPricePerUnit").GetDecimal());
        Assert.True(firstSeller.GetProperty("brandQuality").GetDecimal() > 0m);
    }

    [Fact]
    public async Task MarketIntelligence_NonExistentCity_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"mi-null-{Guid.NewGuid():N}@test.com", "MI Null Test");

        var nonExistentCityId = Guid.NewGuid();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query MarketIntelligence($cityId: UUID!) {
              marketIntelligence(cityId: $cityId) {
                cityId
                products {
                  productName
                }
              }
            }
            """,
            new { cityId = nonExistentCityId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");

        var data = result.GetProperty("data");
        Assert.True(
            data.GetProperty("marketIntelligence").ValueKind == JsonValueKind.Null,
            "Expected null result for non-existent city.");
    }

    [Fact]
    public async Task MarketIntelligence_CityWithNoSalesRecords_ReturnsEmptyProductList()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"mi-empty-{Guid.NewGuid():N}@test.com", "MI Empty Test");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        // No PublicSalesRecords are added for this city, so the query should return empty Products.
        var result = await ExecuteGraphQlAsync(
            client,
            """
            query MarketIntelligence($cityId: UUID!) {
              marketIntelligence(cityId: $cityId) {
                cityId
                cityName
                products {
                  productName
                }
              }
            }
            """,
            new { cityId = city!.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");

        var mi = result.GetProperty("data").GetProperty("marketIntelligence");
        Assert.Equal(city.Id.ToString(), mi.GetProperty("cityId").GetString());
        Assert.Equal("Bratislava", mi.GetProperty("cityName").GetString());
        Assert.Equal(0, mi.GetProperty("products").GetArrayLength());
    }

    [Fact]
    public async Task MarketIntelligence_SellerWithNoBrandData_ReturnsNullBrandQuality()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"mi-nobrand-{Guid.NewGuid():N}@test.com", "MI NoBrand Test");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email == $"mi-nobrand-{Guid.NewGuid():N}@test.com");

        // Create a company without any Brand rows (no brand quality data)
        var brandlessPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"nobrand-seller-{Guid.NewGuid():N}@test.com",
            DisplayName = "Brandless Seller",
            PasswordHash = "mock",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Person,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(brandlessPlayer);

        var brandlessCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = brandlessPlayer.Id,
            Name = "No Brand Corp",
            Cash = 50_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.1m,
        };
        db.Companies.Add(brandlessCompany);

        var currentTick = (await db.GameStates.AsNoTracking().FirstOrDefaultAsync())!.CurrentTick;

        db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(),
            BuildingUnitId = Guid.NewGuid(),
            BuildingId = Guid.NewGuid(),
            CompanyId = brandlessCompany.Id,
            CityId = city!.Id,
            ProductTypeId = product!.Id,
            Tick = currentTick,
            QuantitySold = 50m,
            PricePerUnit = 40m,
            Revenue = 2_000m,
            Demand = 100m,
            SalesCapacity = 100m,
            TrendFactor = 1.0m,
        });

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query MarketIntelligence($cityId: UUID!) {
              marketIntelligence(cityId: $cityId) {
                products {
                  sellers {
                    displayName
                    brandQuality
                  }
                }
              }
            }
            """,
            new { cityId = city.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");

        var products = result.GetProperty("data").GetProperty("marketIntelligence").GetProperty("products");
        Assert.True(products.GetArrayLength() > 0, "Expected at least one product.");

        var sellers = products[0].GetProperty("sellers");
        var noBrandSeller = sellers.EnumerateArray()
            .FirstOrDefault(s => s.GetProperty("displayName").GetString() == "No Brand Corp");

        Assert.False(noBrandSeller.Equals(default(JsonElement)), "Expected 'No Brand Corp' in sellers list.");
        Assert.Equal(JsonValueKind.Null, noBrandSeller.GetProperty("brandQuality").ValueKind);
    }
}
