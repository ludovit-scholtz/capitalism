using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class OperationsDashboardQueriesTests
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

    [Fact]
    public async Task OperationsStatistics_ReturnsLedgerAndFxFeeAggregations()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"ops-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Ops Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var gameState = await db.GameStates.FirstAsync();
            gameState.CurrentTick = 240;

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = admin.Id,
                Name = "Ops Admin Corp",
            };
            db.Companies.Add(company);

            db.LedgerEntries.AddRange(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Category = LedgerCategory.Revenue,
                    Amount = 500m,
                    RecordedAtTick = 200,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Category = LedgerCategory.RentIncome,
                    Amount = 120m,
                    RecordedAtTick = 210,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Category = LedgerCategory.LaborCost,
                    Amount = -90m,
                    RecordedAtTick = 220,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Category = LedgerCategory.UnitUpgrade,
                    Amount = -30m,
                    RecordedAtTick = 220,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Category = LedgerCategory.Revenue,
                    Amount = 999m,
                    RecordedAtTick = 50,
                    RecordedAtUtc = DateTime.UtcNow,
                });

            db.ForexTradeRecords.Add(new ForexTradeRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = admin.Id,
                FromCurrencyCode = "EUR",
                ToCurrencyCode = "USD",
                FromAmount = 100m,
                ToAmount = 110m,
                FeeAmount = 7.5m,
                Rate = 1.11m,
                ExecutedAtTick = 205,
                ExecutedAtUtc = DateTime.UtcNow,
            });

            var pool = new GoldAmmPool
            {
                Id = Guid.NewGuid(),
                CurrencyCode = "EUR",
                FiatReserve = 10_000m,
                GoldReserve = 100m,
                TotalLiquidityShares = 1_000m,
            };
            db.GoldAmmPools.Add(pool);
            db.GoldAmmTradeRecords.Add(new GoldAmmTradeRecord
            {
                Id = Guid.NewGuid(),
                PlayerId = admin.Id,
                PoolId = pool.Id,
                Direction = "FIAT_TO_GOLD",
                CurrencyCode = city.CurrencyCode,
                InputAmount = 50m,
                OutputAmount = 0.4m,
                FeeAmount = 2.5m,
                ImpliedPrice = 125m,
                ExecutedAtTick = 206,
                ExecutedAtUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query OperationsStats {
              operationsStatistics {
                inflowItems { category amount }
                outflowItems { category amount entryCount }
              }
            }
            """,
            token: token);

        Assert.False(result.TryGetProperty("errors", out _));

        var stats = result.GetProperty("data").GetProperty("operationsStatistics");
        var inflows = stats.GetProperty("inflowItems").EnumerateArray()
            .ToDictionary(i => i.GetProperty("category").GetString()!, i => i.GetProperty("amount").GetDecimal());
        var outflows = stats.GetProperty("outflowItems").EnumerateArray()
            .ToDictionary(i => i.GetProperty("category").GetString()!, i => i);

        Assert.Equal(500m, inflows["PUBLIC_SALES"]);
        Assert.Equal(120m, inflows["RENT_INCOME"]);
        Assert.Equal(90m, outflows["LABOR"].GetProperty("amount").GetDecimal());
        Assert.Equal(30m, outflows["RESEARCH"].GetProperty("amount").GetDecimal());
        Assert.Equal(10m, outflows["FX_FEES"].GetProperty("amount").GetDecimal());
        Assert.Equal(2, outflows["FX_FEES"].GetProperty("entryCount").GetInt32());
    }

    [Fact]
    public async Task AdminProductAnalytics_ReturnsAggregatedProductionSalesAndCosts()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var adminEmail = $"analytics-admin-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(client, adminEmail, "Analytics Admin");
        await PromoteToAdminAsync(factory, adminEmail);

        ProductType product;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FirstAsync(p => p.Email == adminEmail);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            product = await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair");
            var gameState = await db.GameStates.FirstAsync();
            gameState.CurrentTick = 250;

            var company = new Company { Id = Guid.NewGuid(), PlayerId = admin.Id, Name = "Analytics Corp" };
            db.Companies.Add(company);

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Factory",
            };
            db.Buildings.Add(building);

            var manufacturingUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                UnitType = UnitType.Manufacturing,
                GridX = 0,
                GridY = 0,
                ProductTypeId = product.Id,
            };
            var salesUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                UnitType = UnitType.PublicSales,
                GridX = 1,
                GridY = 0,
                ProductTypeId = product.Id,
            };
            db.BuildingUnits.AddRange(manufacturingUnit, salesUnit);

            db.BuildingUnitResourceHistories.Add(new BuildingUnitResourceHistory
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                BuildingUnitId = manufacturingUnit.Id,
                ProductTypeId = product.Id,
                Tick = 230,
                ProducedQuantity = 40m,
            });

            db.PublicSalesRecords.Add(new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = salesUnit.Id,
                BuildingId = building.Id,
                CompanyId = company.Id,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = 232,
                QuantitySold = 25m,
                PricePerUnit = 50m,
                Revenue = 1_250m,
                Demand = 30m,
                SalesCapacity = 40m,
            });

            db.LedgerEntries.AddRange(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    ProductTypeId = product.Id,
                    Category = LedgerCategory.PurchasingCost,
                    Amount = -100m,
                    RecordedAtTick = 233,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    ProductTypeId = product.Id,
                    Category = LedgerCategory.LaborCost,
                    Amount = -30m,
                    RecordedAtTick = 233,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    ProductTypeId = product.Id,
                    Category = LedgerCategory.EnergyCost,
                    Amount = -20m,
                    RecordedAtTick = 233,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    ProductTypeId = product.Id,
                    Category = LedgerCategory.Marketing,
                    Amount = -10m,
                    RecordedAtTick = 233,
                    RecordedAtUtc = DateTime.UtcNow,
                });

            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query ProductAnalytics {
              adminProductAnalytics {
                rows {
                  productTypeId
                  productName
                  industry
                  totalProduced
                  totalSold
                  totalRevenue
                  activeSellerCount
                  activeCityCount
                  totalMaterialCost
                  totalLaborCost
                  totalEnergyCost
                  totalCost
                  totalMarketingSpend
                }
              }
            }
            """,
            token: token);

        Assert.False(result.TryGetProperty("errors", out _));

        var rows = result.GetProperty("data").GetProperty("adminProductAnalytics").GetProperty("rows")
            .EnumerateArray()
            .ToList();
        var ordered = rows
            .Select(r => (Industry: r.GetProperty("industry").GetString()!, Name: r.GetProperty("productName").GetString()!))
            .ToList();
        Assert.Equal(ordered.OrderBy(x => x.Industry).ThenBy(x => x.Name).ToList(), ordered);

        var row = rows.First(r => r.GetProperty("productTypeId").GetString() == product.Id.ToString());
        Assert.Equal(40m, row.GetProperty("totalProduced").GetDecimal());
        Assert.Equal(25m, row.GetProperty("totalSold").GetDecimal());
        Assert.Equal(1_250m, row.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(1, row.GetProperty("activeSellerCount").GetInt32());
        Assert.Equal(1, row.GetProperty("activeCityCount").GetInt32());
        Assert.Equal(100m, row.GetProperty("totalMaterialCost").GetDecimal());
        Assert.Equal(30m, row.GetProperty("totalLaborCost").GetDecimal());
        Assert.Equal(20m, row.GetProperty("totalEnergyCost").GetDecimal());
        Assert.Equal(150m, row.GetProperty("totalCost").GetDecimal());
        Assert.Equal(10m, row.GetProperty("totalMarketingSpend").GetDecimal());
    }

    [Fact]
    public async Task OperationsDashboardQueries_UnauthorizedUser_GetsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"non-admin-{Guid.NewGuid():N}@test.com", "Non Admin");

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query OpsUnauthorized {
              operationsStatistics { currentTick }
              adminProductAnalytics { currentTick }
            }
            """,
            token: token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }
}
