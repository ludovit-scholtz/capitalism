using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the competitorQualityIntelligence GraphQL query.
/// </summary>
public sealed class CompetitorIntelligenceTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
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

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private const string CompetitorIntelligenceQuery = """
        query Q($cityId: UUID!, $productTypeId: UUID!) {
          competitorQualityIntelligence(cityId: $cityId, productTypeId: $productTypeId) {
            companyId
            companyName
            qualityLevel
            pricePremiumPct
            isOwnCompany
          }
        }
        """;

    [Fact]
    public async Task CompetitorIntelligence_Unauthenticated_Fails()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            CompetitorIntelligenceQuery,
            new { cityId = Guid.NewGuid(), productTypeId = Guid.NewGuid() });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task CompetitorIntelligence_NoCompetitors_ReturnsEmptyList()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"ci-none-{Guid.NewGuid():N}@test.com", "CI None");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var product = await db.ProductTypes.FirstAsync();

        var result = await ExecuteGraphQlAsync(client,
            CompetitorIntelligenceQuery,
            new { cityId = city.Id, productTypeId = product.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Should return data without errors.");
        var entries = result.GetProperty("data").GetProperty("competitorQualityIntelligence");
        Assert.Equal(0, entries.GetArrayLength());
    }

    [Fact]
    public async Task CompetitorIntelligence_IsOwnCompanyFlag_Set()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var emailOwner = $"ci-owner-{Guid.NewGuid():N}@test.com";
        var emailCompetitor = $"ci-comp-{Guid.NewGuid():N}@test.com";

        var ownerToken = await RegisterAndGetTokenAsync(client, emailOwner, "CI Owner");
        await RegisterAndGetTokenAsync(client, emailCompetitor, "CI Competitor");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var product = await db.ProductTypes.FirstAsync();

        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == emailOwner);
        var competitorPlayer = await db.Players.FirstAsync(p => p.Email == emailCompetitor);

        var ownerCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownerPlayer.Id,
            Name = "CI Owner Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        var competitorCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = competitorPlayer.Id,
            Name = "CI Competitor Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.AddRange(ownerCompany, competitorCompany);

        // Both companies need a building in the city
        var ownerBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = ownerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.SalesShop,
            Name = "Owner Shop",
            Level = 1,
        };
        var competitorBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = competitorCompany.Id,
            CityId = city.Id,
            Type = BuildingType.SalesShop,
            Name = "Competitor Shop",
            Level = 1,
        };
        db.Buildings.AddRange(ownerBuilding, competitorBuilding);

        // Both have PRODUCT-scoped brands
        var ownerBrand = new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = ownerCompany.Id,
            Name = "Owner Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Quality = 0.5m,
            Awareness = 0.5m,
        };
        var competitorBrand = new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = competitorCompany.Id,
            Name = "Competitor Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Quality = 0.8m,
            Awareness = 0.5m,
        };
        db.Brands.AddRange(ownerBrand, competitorBrand);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            CompetitorIntelligenceQuery,
            new { cityId = city.Id, productTypeId = product.Id },
            ownerToken);

        Assert.False(result.TryGetProperty("errors", out _), "Should not return errors.");
        var entries = result.GetProperty("data").GetProperty("competitorQualityIntelligence");
        Assert.Equal(2, entries.GetArrayLength());

        // Find own company entry
        var ownEntry = entries.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("companyId").GetString() == ownerCompany.Id.ToString());
        Assert.True(ownEntry.ValueKind != JsonValueKind.Undefined, "Own company entry should exist.");
        Assert.True(ownEntry.GetProperty("isOwnCompany").GetBoolean(), "isOwnCompany should be true for owner.");

        var competitorEntry = entries.EnumerateArray()
            .FirstOrDefault(e => e.GetProperty("companyId").GetString() == competitorCompany.Id.ToString());
        Assert.True(competitorEntry.ValueKind != JsonValueKind.Undefined, "Competitor entry should exist.");
        Assert.False(competitorEntry.GetProperty("isOwnCompany").GetBoolean(), "isOwnCompany should be false for competitor.");
    }

    [Fact]
    public async Task CompetitorIntelligence_SortedByQualityDescending()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerEmail = $"ci-sort-{Guid.NewGuid():N}@test.com";
        var ownerToken = await RegisterAndGetTokenAsync(client, ownerEmail, "CI Sort Owner");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var product = await db.ProductTypes.OrderBy(p => p.Name).FirstAsync();

        var ownerPlayer = await db.Players.FirstAsync(p => p.Email == ownerEmail);

        // Create 3 companies with different quality levels
        var qualities = new[] { 0.3m, 0.9m, 0.6m };
        var companies = new List<Company>();
        var buildings = new List<Building>();
        var brands = new List<Brand>();

        for (var i = 0; i < 3; i++)
        {
            var player = new Player
            {
                Id = Guid.NewGuid(),
                Email = $"ci-sort-p{i}-{Guid.NewGuid():N}@test.com",
                DisplayName = $"Sort Player {i}",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            };
            db.Players.Add(player);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = $"Sort Corp {i}",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
                TotalSharesIssued = 10_000m,
                DividendPayoutRatio = 0.2m,
            };
            companies.Add(company);

            buildings.Add(new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.SalesShop,
                Name = $"Sort Shop {i}",
                Level = 1,
            });

            brands.Add(new Brand
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                Name = $"Sort Brand {i}",
                Scope = BrandScope.Product,
                ProductTypeId = product.Id,
                Quality = qualities[i],
                Awareness = 0.5m,
            });
        }

        db.Companies.AddRange(companies);
        db.Buildings.AddRange(buildings);
        db.Brands.AddRange(brands);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            CompetitorIntelligenceQuery,
            new { cityId = city.Id, productTypeId = product.Id },
            ownerToken);

        Assert.False(result.TryGetProperty("errors", out _), "Should not return errors.");
        var entries = result.GetProperty("data").GetProperty("competitorQualityIntelligence").EnumerateArray().ToList();

        // Find our 3 sorted entries (there may be others from other tests in the shared DB, so filter by company name)
        var sortedEntries = entries
            .Where(e => companies.Any(c => c.Id.ToString() == e.GetProperty("companyId").GetString()))
            .Select(e => e.GetProperty("qualityLevel").GetDecimal())
            .ToList();

        Assert.Equal(3, sortedEntries.Count);
        Assert.True(sortedEntries[0] >= sortedEntries[1], "Results must be sorted descending by quality.");
        Assert.True(sortedEntries[1] >= sortedEntries[2], "Results must be sorted descending by quality.");
    }
}
