using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class OperationsDashboardProductAnalyticsTests
{
    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task PromoteToAdminAsync(ApiWebApplicationFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(p => p.Email == email);
        player.Role = PlayerRole.Admin;
        await db.SaveChangesAsync();
    }

    private static async Task<JsonElement> QueryAnalyticsAsync(HttpClient client, string token, object? input = null)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query ProductAnalytics($input: AdminProductAnalyticsInput) {
              adminProductAnalytics(input: $input) {
                windowTicks
                rows {
                  productTypeId
                  companyId
                  companyName
                  totalProduced
                  totalSold
                  totalRevenue
                  marketSize
                  marketSaturation
                  avgMarketPrice
                  totalMaterialCost
                  totalLaborCost
                  totalEnergyCost
                  totalResearchSpend
                  activeCityCount
                }
              }
            }
            """,
            variables: new { input },
            token: token);

        Assert.False(result.TryGetProperty("errors", out _));
        return result.GetProperty("data").GetProperty("adminProductAnalytics");
    }

    [Fact]
    public async Task AdminProductAnalytics_FilterByCompany_ReturnsOnlySelectedCompanyTotals()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        Guid productId;
        Guid targetCompanyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var gameState = await db.GameStates.FirstAsync();
            gameState.CurrentTick = 500;
            productId = (await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair")).Id;

            var companyA = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "Company A" };
            var companyB = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "Company B" };
            targetCompanyId = companyA.Id;
            db.Companies.AddRange(companyA, companyB);

            var unitA = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = Guid.NewGuid(), UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, ProductTypeId = productId };
            var unitB = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = Guid.NewGuid(), UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, ProductTypeId = productId };
            var buildingA = new Building { Id = unitA.BuildingId, CompanyId = companyA.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "A Shop" };
            var buildingB = new Building { Id = unitB.BuildingId, CompanyId = companyB.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "B Shop" };
            db.Buildings.AddRange(buildingA, buildingB);
            db.BuildingUnits.AddRange(unitA, unitB);

            db.PublicSalesRecords.AddRange(
                new PublicSalesRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingUnitId = unitA.Id,
                    BuildingId = buildingA.Id,
                    CompanyId = companyA.Id,
                    CityId = city.Id,
                    ProductTypeId = productId,
                    Tick = 499,
                    QuantitySold = 10m,
                    Demand = 20m,
                    PricePerUnit = 40m,
                    Revenue = 400m,
                },
                new PublicSalesRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingUnitId = unitB.Id,
                    BuildingId = buildingB.Id,
                    CompanyId = companyB.Id,
                    CityId = city.Id,
                    ProductTypeId = productId,
                    Tick = 499,
                    QuantitySold = 5m,
                    Demand = 8m,
                    PricePerUnit = 35m,
                    Revenue = 175m,
                });

            await db.SaveChangesAsync();
        }

        var analytics = await QueryAnalyticsAsync(client, token, new { companyId = targetCompanyId });
        var row = analytics.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("productTypeId").GetString() == productId.ToString());

        Assert.Equal(targetCompanyId.ToString(), row.GetProperty("companyId").GetString());
        Assert.Equal("Company A", row.GetProperty("companyName").GetString());
        Assert.Equal(10m, row.GetProperty("totalSold").GetDecimal());
        Assert.Equal(400m, row.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(20m, row.GetProperty("marketSize").GetDecimal());
    }

    [Fact]
    public async Task AdminProductAnalytics_FilterByProductType_ReturnsSingleRequestedProduct()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        Guid selectedProductId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var chair = await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair");
            selectedProductId = chair.Id;

            var analytics = await QueryAnalyticsAsync(client, token, new { productTypeId = selectedProductId });
            var rows = analytics.GetProperty("rows").EnumerateArray().ToList();

            Assert.Single(rows);
            Assert.Equal(chair.Id.ToString(), rows[0].GetProperty("productTypeId").GetString());
        }
    }

    [Fact]
    public async Task AdminProductAnalytics_FilterByCity_ReturnsOnlySelectedCityAggregations()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        Guid cityId;
        Guid productId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
            cityId = bratislava.Id;
            productId = (await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair")).Id;

            var company = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "City Filter Corp" };
            db.Companies.Add(company);

            var unitA = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = Guid.NewGuid(), UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, ProductTypeId = productId };
            var unitB = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = Guid.NewGuid(), UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, ProductTypeId = productId };
            db.Buildings.AddRange(
                new Building { Id = unitA.BuildingId, CompanyId = company.Id, CityId = bratislava.Id, Type = BuildingType.SalesShop, Name = "BA Shop" },
                new Building { Id = unitB.BuildingId, CompanyId = company.Id, CityId = prague.Id, Type = BuildingType.SalesShop, Name = "PRG Shop" });
            db.BuildingUnits.AddRange(unitA, unitB);

            db.PublicSalesRecords.AddRange(
                new PublicSalesRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingUnitId = unitA.Id,
                    BuildingId = unitA.BuildingId,
                    CompanyId = company.Id,
                    CityId = bratislava.Id,
                    ProductTypeId = productId,
                    Tick = 10,
                    QuantitySold = 12m,
                    Demand = 20m,
                    PricePerUnit = 45m,
                    Revenue = 540m,
                },
                new PublicSalesRecord
                {
                    Id = Guid.NewGuid(),
                    BuildingUnitId = unitB.Id,
                    BuildingId = unitB.BuildingId,
                    CompanyId = company.Id,
                    CityId = prague.Id,
                    ProductTypeId = productId,
                    Tick = 10,
                    QuantitySold = 30m,
                    Demand = 35m,
                    PricePerUnit = 45m,
                    Revenue = 1_350m,
                });

            await db.SaveChangesAsync();
        }

        var analytics = await QueryAnalyticsAsync(client, token, new { cityId });
        var row = analytics.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("productTypeId").GetString() == productId.ToString());

        Assert.Equal(12m, row.GetProperty("totalSold").GetDecimal());
        Assert.Equal(540m, row.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(20m, row.GetProperty("marketSize").GetDecimal());
        Assert.Equal(1, row.GetProperty("activeCityCount").GetInt32());
    }

    [Fact]
    public async Task AdminProductAnalytics_FilterByWindowTicks_ExcludesOlderRecords()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        Guid productId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var gameState = await db.GameStates.FirstAsync();
            gameState.CurrentTick = 500;
            productId = (await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair")).Id;

            var company = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "Window Corp" };
            var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Factory" };
            var unit = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Manufacturing, GridX = 0, GridY = 0, ProductTypeId = productId };
            db.Companies.Add(company);
            db.Buildings.Add(building);
            db.BuildingUnits.Add(unit);
            db.BuildingUnitResourceHistories.AddRange(
                new BuildingUnitResourceHistory
                {
                    Id = Guid.NewGuid(),
                    BuildingId = building.Id,
                    BuildingUnitId = unit.Id,
                    ProductTypeId = productId,
                    Tick = 495,
                    ProducedQuantity = 7m,
                },
                new BuildingUnitResourceHistory
                {
                    Id = Guid.NewGuid(),
                    BuildingId = building.Id,
                    BuildingUnitId = unit.Id,
                    ProductTypeId = productId,
                    Tick = 300,
                    ProducedQuantity = 100m,
                });

            await db.SaveChangesAsync();
        }

        var analytics = await QueryAnalyticsAsync(client, token, new { windowTicks = 10, productTypeId = productId });
        Assert.Equal(10, analytics.GetProperty("windowTicks").GetInt32());
        var row = analytics.GetProperty("rows").EnumerateArray().Single();
        Assert.Equal(7m, row.GetProperty("totalProduced").GetDecimal());
    }

    [Fact]
    public async Task AdminProductAnalytics_ReturnsMarketMetricsAndResearchSpend()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        Guid productId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            productId = (await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair")).Id;

            var company = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "Metrics Corp" };
            var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "Shop" };
            var salesUnit = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, ProductTypeId = productId };
            db.Companies.Add(company);
            db.Buildings.Add(building);
            db.BuildingUnits.Add(salesUnit);

            db.PublicSalesRecords.Add(new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = salesUnit.Id,
                BuildingId = building.Id,
                CompanyId = company.Id,
                CityId = city.Id,
                ProductTypeId = productId,
                Tick = 20,
                QuantitySold = 10m,
                Demand = 40m,
                PricePerUnit = 50m,
                Revenue = 500m,
            });

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                ProductTypeId = productId,
                Category = LedgerCategory.UnitUpgrade,
                Amount = -70m,
                RecordedAtTick = 20,
                RecordedAtUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        var analytics = await QueryAnalyticsAsync(client, token, new { productTypeId = productId });
        var row = analytics.GetProperty("rows").EnumerateArray().Single();
        Assert.Equal(40m, row.GetProperty("marketSize").GetDecimal());
        Assert.Equal(25m, row.GetProperty("marketSaturation").GetDecimal());
        Assert.Equal(50m, row.GetProperty("avgMarketPrice").GetDecimal());
        Assert.Equal(70m, row.GetProperty("totalResearchSpend").GetDecimal());
    }
}
