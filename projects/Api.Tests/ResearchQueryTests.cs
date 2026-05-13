using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the R&amp;D research GraphQL queries:
/// productQualityProfile, brandQualityOverview, buildingResearchProgress.
///
/// Coverage includes:
/// - Auth-negative (unauthenticated) rejection
/// - Owner-success (authenticated owner gets data)
/// - Foreign-company isolation (other player's company returns null / empty)
/// - Quality price premium constant alignment
/// </summary>
public sealed class ResearchQueryTests
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

    // ──────────────────────────────────────────────────────────
    // productQualityProfile tests
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ProductQualityProfile_UnauthenticatedRequest_Fails()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!, $productTypeId: UUID!) {
              productQualityProfile(companyId: $companyId, productTypeId: $productTypeId) {
                qualityLevel
              }
            }
            """,
            new { companyId = Guid.NewGuid(), productTypeId = Guid.NewGuid() });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task ProductQualityProfile_ForeignCompany_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var tokenA = await RegisterAndGetTokenAsync(client, $"pqp-a-{Guid.NewGuid()}@test.com", "PQP A");
        var tokenB = await RegisterAndGetTokenAsync(client, $"pqp-b-{Guid.NewGuid()}@test.com", "PQP B");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerA = await db.Players.FirstOrDefaultAsync(p => p.Email.Contains("pqp-a-"));
        Assert.NotNull(playerA);

        var companyA = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerA.Id,
            Name = "PQP Test Corp A",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(companyA);
        await db.SaveChangesAsync();

        var product = await db.ProductTypes.FirstAsync();

        // Player B queries Player A's company — should return null
        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!, $productTypeId: UUID!) {
              productQualityProfile(companyId: $companyId, productTypeId: $productTypeId) {
                qualityLevel
              }
            }
            """,
            new { companyId = companyA.Id, productTypeId = product.Id },
            tokenB);

        Assert.False(result.TryGetProperty("errors", out _), "Foreign company query must not return errors (just null data).");
        var profile = result.GetProperty("data").GetProperty("productQualityProfile");
        Assert.Equal(JsonValueKind.Null, profile.ValueKind);
    }

    [Fact]
    public async Task ProductQualityProfile_OwnCompany_ReturnsProfileShape()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"pqp-own-{Guid.NewGuid()}@test.com", "PQP Own");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email.Contains("pqp-own-"));
        Assert.NotNull(player);

        var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "PQP Research Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(company);

        // Seed a brand with quality
        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "Quality Chair Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Awareness = 0.6m,
            Quality = 0.5m,
            MarketingQuality = 0.0m,
            MarketingEfficiencyMultiplier = 1.2m,
        });

        // Seed a research budget
        db.ProductResearchBudgets.Add(new ProductResearchBudget
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ProductTypeId = product.Id,
            AccumulatedBudget = 50_000m,
        });

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!, $productTypeId: UUID!) {
              productQualityProfile(companyId: $companyId, productTypeId: $productTypeId) {
                companyId
                productTypeId
                productName
                industry
                rdQuality
                marketingQuality
                combinedQuality
                qualityLevel
                rdQualityLevel
                accumulatedResearchBudgetUsd
                baseResearchBudgetUsd
                maxCompetitorBudgetUsd
                marketingEfficiencyMultiplier
                qualityPricePremiumPct
                ticksToNextLevel
              }
            }
            """,
            new { companyId = company.Id, productTypeId = product.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Authenticated owner profile query must succeed.");
        var profile = result.GetProperty("data").GetProperty("productQualityProfile");
        Assert.NotEqual(JsonValueKind.Null, profile.ValueKind);

        // qualityLevel = combinedQuality × 10; at Quality=0.5, MarketingQuality=0 → combinedQuality=0.5 → level=5
        var qualityLevel = profile.GetProperty("qualityLevel").GetDecimal();
        Assert.Equal(5.0m, qualityLevel);

        // qualityPricePremiumPct at combinedQuality=0.5 → 0.5 × 0.5 × 100 = 25%
        var premiumPct = profile.GetProperty("qualityPricePremiumPct").GetDecimal();
        Assert.Equal(25.0m, premiumPct);

        // marketingEfficiencyMultiplier
        var mktEff = profile.GetProperty("marketingEfficiencyMultiplier").GetDecimal();
        Assert.Equal(1.2m, mktEff);
    }

    // ──────────────────────────────────────────────────────────
    // brandQualityOverview tests
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BrandQualityOverview_UnauthenticatedRequest_Fails()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!) {
              brandQualityOverview(companyId: $companyId) {
                companyId
                totalResearchBudgetUsd
              }
            }
            """,
            new { companyId = Guid.NewGuid() });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BrandQualityOverview_ForeignCompany_ReturnsEmptyBrands()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var tokenA = await RegisterAndGetTokenAsync(client, $"bqo-a-{Guid.NewGuid()}@test.com", "BQO A");
        var tokenB = await RegisterAndGetTokenAsync(client, $"bqo-b-{Guid.NewGuid()}@test.com", "BQO B");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerA = await db.Players.FirstOrDefaultAsync(p => p.Email.Contains("bqo-a-"));
        Assert.NotNull(playerA);

        var companyA = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerA.Id,
            Name = "BQO Corp A",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(companyA);
        await db.SaveChangesAsync();

        // Player B queries Player A's brand overview — should return empty
        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!) {
              brandQualityOverview(companyId: $companyId) {
                companyId
                totalResearchBudgetUsd
                brands { id scope }
              }
            }
            """,
            new { companyId = companyA.Id },
            tokenB);

        Assert.False(result.TryGetProperty("errors", out _), "Foreign company overview must not throw errors.");
        var overview = result.GetProperty("data").GetProperty("brandQualityOverview");
        Assert.NotEqual(JsonValueKind.Null, overview.ValueKind);
        var brands = overview.GetProperty("brands").EnumerateArray().ToList();
        Assert.Empty(brands);
        Assert.Equal(0m, overview.GetProperty("totalResearchBudgetUsd").GetDecimal());
    }

    [Fact]
    public async Task BrandQualityOverview_OwnCompany_ReturnsBrandsWithQualityLevels()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"bqo-own-{Guid.NewGuid()}@test.com", "BQO Own");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email.Contains("bqo-own-"));
        Assert.NotNull(player);

        var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "BQO Research Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(company);

        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "BQO Chair Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Awareness = 0.4m,
            Quality = 0.3m,
            MarketingQuality = 0.2m,
            MarketingEfficiencyMultiplier = 1.1m,
        });

        db.ProductResearchBudgets.Add(new ProductResearchBudget
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ProductTypeId = product.Id,
            AccumulatedBudget = 30_000m,
        });

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($companyId: UUID!) {
              brandQualityOverview(companyId: $companyId) {
                companyId
                totalResearchBudgetUsd
                brands {
                  id
                  scope
                  productTypeId
                  productName
                  quality
                  marketingQuality
                  combinedBrandQuality
                  accumulatedResearchBudget
                }
              }
            }
            """,
            new { companyId = company.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Authenticated owner overview must succeed.");
        var overview = result.GetProperty("data").GetProperty("brandQualityOverview");
        Assert.NotEqual(JsonValueKind.Null, overview.ValueKind);

        var brands = overview.GetProperty("brands").EnumerateArray().ToList();
        Assert.Single(brands);

        var brand = brands[0];
        Assert.Equal("PRODUCT", brand.GetProperty("scope").GetString());
        Assert.Equal(0.3m, brand.GetProperty("quality").GetDecimal());
        Assert.Equal(0.2m, brand.GetProperty("marketingQuality").GetDecimal());

        // combinedBrandQuality = 1 - (1 - 0.3) × (1 - 0.2) = 1 - 0.7 × 0.8 = 1 - 0.56 = 0.44
        var combined = brand.GetProperty("combinedBrandQuality").GetDecimal();
        Assert.InRange(combined, 0.43m, 0.45m);

        // totalResearchBudgetUsd should include the 30 000 seeded budget
        Assert.True(overview.GetProperty("totalResearchBudgetUsd").GetDecimal() > 0m);
    }

    // ──────────────────────────────────────────────────────────
    // buildingResearchProgress tests
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task BuildingResearchProgress_UnauthenticatedRequest_Fails()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($buildingId: UUID!) {
              buildingResearchProgress(buildingId: $buildingId) {
                unitId currentQualityLevel
              }
            }
            """,
            new { buildingId = Guid.NewGuid() });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BuildingResearchProgress_OwnRdBuilding_ReturnsProgressPerUnit()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"brp-own-{Guid.NewGuid()}@test.com", "BRP Own");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstOrDefaultAsync(p => p.Email.Contains("brp-own-"));
        Assert.NotNull(player);

        var city = await db.Cities.FirstOrDefaultAsync(c => c.Name == "Bratislava");
        Assert.NotNull(city);

        var product = await db.ProductTypes.FirstOrDefaultAsync(p => p.Slug == "wooden-chair");
        Assert.NotNull(product);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "BRP Research Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Name = "R&D Lab",
            Type = BuildingType.ResearchDevelopment,
            Latitude = 48.1,
            Longitude = 17.1,
        };
        db.Buildings.Add(building);

        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.ProductQuality,
            GridX = 0,
            GridY = 0,
            Level = 1,
            ProductTypeId = product.Id,
        };
        db.BuildingUnits.Add(unit);

        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = "BRP Chair Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Awareness = 0.2m,
            Quality = 0.4m,
            MarketingQuality = 0.0m,
        });

        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            query Q($buildingId: UUID!) {
              buildingResearchProgress(buildingId: $buildingId) {
                unitId
                unitType
                gridX
                gridY
                level
                productTypeId
                productName
                currentQualityLevel
                combinedQualityLevel
                progressToNextLevelPct
                qualityPricePremiumPct
              }
            }
            """,
            new { buildingId = building.Id },
            token);

        Assert.False(result.TryGetProperty("errors", out _), "Own building research progress must succeed.");
        var items = result.GetProperty("data").GetProperty("buildingResearchProgress")
            .EnumerateArray().ToList();

        Assert.Single(items);
        var item = items[0];
        Assert.Equal(unit.Id.ToString(), item.GetProperty("unitId").GetString());
        Assert.Equal("PRODUCT_QUALITY", item.GetProperty("unitType").GetString());
        Assert.Equal(product.Id.ToString(), item.GetProperty("productTypeId").GetString());

        // quality=0.4 → currentQualityLevel = 4.0
        Assert.Equal(4.0m, item.GetProperty("currentQualityLevel").GetDecimal());

        // qualityPricePremiumPct at combined=0.4 → 0.5 × 0.4 × 100 = 20%
        Assert.Equal(20.0m, item.GetProperty("qualityPricePremiumPct").GetDecimal());
    }

    // ──────────────────────────────────────────────────────────
    // Quality price premium constant validation
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void QualityPricePremium_AtQualityLevel5_Is25Percent()
    {
        // Acceptance Criterion: a product at qualityLevel=5 (brandQuality=0.5)
        // has a 25% price premium (qualityAdjustedBasePrice = 1.25 × localBasePrice).
        const decimal brandQuality = 0.5m; // qualityLevel = 5 on 0–10 scale
        var premiumMultiplier = 1m + Api.Engine.GameConstants.QualityPricePremiumRate * brandQuality;
        Assert.Equal(1.25m, premiumMultiplier);
    }

    [Fact]
    public void QualityPricePremium_AtQualityLevel10_Is50Percent()
    {
        // At maximum quality level 10 (brandQuality=1.0): 50% price premium.
        const decimal brandQuality = 1.0m;
        var premiumMultiplier = 1m + Api.Engine.GameConstants.QualityPricePremiumRate * brandQuality;
        Assert.Equal(1.50m, premiumMultiplier);
    }

    [Fact]
    public void QualityPricePremium_AtQualityLevel0_IsZeroPercent()
    {
        // At zero quality: no premium, reference price unchanged.
        const decimal brandQuality = 0m;
        var premiumMultiplier = 1m + Api.Engine.GameConstants.QualityPricePremiumRate * brandQuality;
        Assert.Equal(1.0m, premiumMultiplier);
    }
}
