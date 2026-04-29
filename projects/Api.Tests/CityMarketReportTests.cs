using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for city market report generation.
/// Uses isolated factories to avoid shared-state interference with tick-advancing tests.
/// </summary>
public sealed class CityMarketReportTests
{
    #region Helpers

    private static async Task<(Guid CityId, Guid CompanyId, Guid ProductTypeId)> SeedSalesDataAsync(
        AppDbContext db,
        string cityName,
        decimal pricePerUnit,
        int tickCount,
        long tickOffset = 0L)
    {
        var city = await db.Cities.FirstAsync(c => c.Name == cityName);
        var product = await db.ProductTypes.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"report-test-{Guid.NewGuid():N}@test.com",
            DisplayName = "Report Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Report Company {Guid.NewGuid():N}",
            Cash = 0m
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            CompanyId = company.Id,
            Type = BuildingType.SalesShop,
            Latitude = 48.1,
            Longitude = 17.1,
            Name = "Test Shop",
            PowerStatus = PowerStatus.Powered,
        };
        db.Buildings.Add(building);

        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.PublicSales,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(unit);

        for (var t = 1L; t <= tickCount; t++)
        {
            db.PublicSalesRecords.Add(new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = unit.Id,
                BuildingId = building.Id,
                CompanyId = company.Id,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = tickOffset + t,
                QuantitySold = 10m,
                PricePerUnit = pricePerUnit,
                Revenue = 10m * pricePerUnit,
                Demand = 20m,
                SalesCapacity = 20m,
                TrendFactor = 1m,
            });
        }

        await db.SaveChangesAsync();
        return (city.Id, company.Id, product.Id);
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query }),
            Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    #endregion

    [Fact]
    public async Task GenerateReports_WithSalesData_CreatesWeeklyReport()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 50m, 10);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db,
            MarketReportType.Weekly,
            tickFrom: 1,
            tickTo: 10);

        Assert.NotEmpty(reports);
        var cityReport = reports.FirstOrDefault(r => r.CityId == cityId);
        Assert.NotNull(cityReport);
        Assert.Equal(MarketReportType.Weekly, cityReport.ReportType);
        Assert.Equal(1, cityReport.TickFrom);
        Assert.Equal(10, cityReport.TickTo);
    }

    [Fact]
    public async Task GenerateReports_WithSalesData_ReportDataJsonContainsTopProducts()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 60m, 5, tickOffset: 10_000L);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db,
            MarketReportType.Weekly,
            tickFrom: 10_001,
            tickTo: 10_005);

        var cityReport = reports.FirstOrDefault(r => r.CityId == cityId);
        Assert.NotNull(cityReport);

        var data = CityMarketReportService.DeserializeData(cityReport);
        Assert.NotNull(data);
        Assert.True(data.TopProducts.Count > 0, "Report should have at least one top product");
        Assert.Equal("Bratislava", data.CityName);
        Assert.Equal(MarketReportType.Weekly, data.ReportType);
        Assert.True(data.TotalRevenue > 0m, "TotalRevenue must be positive");
    }

    [Fact]
    public async Task GenerateReports_Idempotent_SkipsExistingReports()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 40m, 8, tickOffset: 20_000L);

        var reports1 = await CityMarketReportService.GenerateReportsAsync(
            db,
            MarketReportType.Weekly,
            tickFrom: 20_001,
            tickTo: 20_008);

        db.CityMarketReports.AddRange(reports1);
        await db.SaveChangesAsync();

        var reports2 = await CityMarketReportService.GenerateReportsAsync(
            db,
            MarketReportType.Weekly,
            tickFrom: 20_001,
            tickTo: 20_008);

        Assert.DoesNotContain(reports2, r => r.CityId == cityId);
    }

    [Fact]
    public async Task GenerateReports_WeeklyAndMonthly_BothCreatedSeparately()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 30m, 5, tickOffset: 30_000L);

        var weeklyReports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: 30_001, tickTo: 30_005);

        var monthlyReports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Monthly, tickFrom: 30_001, tickTo: 30_005);

        var weeklyCity = weeklyReports.FirstOrDefault(r => r.CityId == cityId);
        var monthlyCity = monthlyReports.FirstOrDefault(r => r.CityId == cityId);

        Assert.NotNull(weeklyCity);
        Assert.NotNull(monthlyCity);
        Assert.Equal(MarketReportType.Weekly, weeklyCity.ReportType);
        Assert.Equal(MarketReportType.Monthly, monthlyCity.ReportType);
    }

    [Fact]
    public async Task GenerateReports_NoCityWithSales_ReturnsEmpty()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db,
            MarketReportType.Weekly,
            tickFrom: 999_990_000,
            tickTo: 999_999_999);

        Assert.Empty(reports);
    }

    [Fact]
    public async Task BuildLocalizations_ProducesAllThreeLocales()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 50m, 3, tickOffset: 40_000L);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: 40_001, tickTo: 40_003);

        var cityReport = reports.FirstOrDefault(r => r.CityId == cityId);
        Assert.NotNull(cityReport);

        var localizations = CityMarketReportService.BuildLocalizations(cityReport);

        Assert.Equal(3, localizations.Count);
        var locales = localizations.Select(l => l.Locale).ToHashSet();
        Assert.Contains("en", locales);
        Assert.Contains("sk", locales);
        Assert.Contains("de", locales);

        foreach (var (locale, title, summary, html) in localizations)
        {
            Assert.False(string.IsNullOrWhiteSpace(title), $"Title should not be empty for locale {locale}");
            Assert.Contains("market-report", html, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task GenerateReports_TopProductsOrderedByRevenue()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var products = await db.ProductTypes.Take(2).ToListAsync();

        if (products.Count < 2)
            return;

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"order-test-{Guid.NewGuid():N}@test.com",
            DisplayName = "Order Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Order Corp {Guid.NewGuid():N}",
            Cash = 0m
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            CompanyId = company.Id,
            Type = BuildingType.SalesShop,
            Latitude = 48.2,
            Longitude = 17.2,
            Name = "Order Shop",
            PowerStatus = PowerStatus.Powered,
        };
        db.Buildings.Add(building);

        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.PublicSales,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(unit);

        var testTick = 50_000L;

        db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(),
            BuildingUnitId = unit.Id,
            BuildingId = building.Id,
            CompanyId = company.Id,
            CityId = city.Id,
            ProductTypeId = products[0].Id,
            Tick = testTick,
            QuantitySold = 100m,
            PricePerUnit = 200m,
            Revenue = 20_000m,
            Demand = 150m,
            SalesCapacity = 150m,
            TrendFactor = 1m,
        });

        db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(),
            BuildingUnitId = unit.Id,
            BuildingId = building.Id,
            CompanyId = company.Id,
            CityId = city.Id,
            ProductTypeId = products[1].Id,
            Tick = testTick,
            QuantitySold = 5m,
            PricePerUnit = 10m,
            Revenue = 50m,
            Demand = 10m,
            SalesCapacity = 10m,
            TrendFactor = 1m,
        });

        await db.SaveChangesAsync();

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: testTick, tickTo: testTick);

        var cityReport = reports.FirstOrDefault(r => r.CityId == city.Id);
        Assert.NotNull(cityReport);

        var data = CityMarketReportService.DeserializeData(cityReport);
        Assert.NotNull(data);
        Assert.True(data.TopProducts.Count >= 2);
        Assert.True(
            data.TopProducts[0].TotalRevenue >= data.TopProducts[1].TotalRevenue,
            "Top products should be ordered by revenue descending");
    }

    [Fact]
    public async Task CityMarketReport_Persisted_SurvivesDbRoundTrip()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (cityId, _, _) = await SeedSalesDataAsync(db, "Bratislava", 45m, 3, tickOffset: 60_000L);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: 60_001, tickTo: 60_003);

        Assert.NotEmpty(reports);

        db.CityMarketReports.AddRange(reports);
        await db.SaveChangesAsync();

        var persisted = await db.CityMarketReports
            .Include(r => r.City)
            .FirstOrDefaultAsync(r => r.CityId == cityId && r.ReportType == MarketReportType.Weekly);

        Assert.NotNull(persisted);
        Assert.Equal(cityId, persisted.CityId);
        Assert.Null(persisted.MasterNewsEntryId);
        Assert.False(string.IsNullOrWhiteSpace(persisted.ReportDataJson));

        var deserialized = CityMarketReportService.DeserializeData(persisted);
        Assert.NotNull(deserialized);
        Assert.Equal("Bratislava", deserialized.CityName);
    }

    [Fact]
    public async Task CityMarketReports_GraphQlQuery_ReturnsPersistedReports()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, _) = await SeedSalesDataAsync(db, "Bratislava", 45m, 3, tickOffset: 70_000L);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: 70_001, tickTo: 70_003);

        db.CityMarketReports.AddRange(reports);
        await db.SaveChangesAsync();

        var gql = """
        query {
          cityMarketReports(cityId: null, reportType: null, limit: 10) {
            id
            cityId
            cityName
            reportType
            tickFrom
            tickTo
            totalRevenue
            uniqueProducts
            topProducts {
              productName
              totalRevenue
              grossMarginPct
              sellerCount
            }
          }
        }
        """;

        var result = await ExecuteGraphQlAsync(client, gql);
        var items = result.GetProperty("data").GetProperty("cityMarketReports");

        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() > 0, "Should return at least one market report");

        var first = items[0];
        Assert.True(first.GetProperty("totalRevenue").GetDecimal() > 0m);
        Assert.Equal("WEEKLY", first.GetProperty("reportType").GetString());
    }

    [Fact]
    public async Task CityMarketReports_GraphQlQuery_FilterByCityId()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bratiCity = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        await SeedSalesDataAsync(db, "Bratislava", 45m, 3, tickOffset: 80_000L);

        var reports = await CityMarketReportService.GenerateReportsAsync(
            db, MarketReportType.Weekly, tickFrom: 80_001, tickTo: 80_003);

        db.CityMarketReports.AddRange(reports);
        await db.SaveChangesAsync();

        var gql = $$"""
        query {
          cityMarketReports(cityId: "{{bratiCity.Id}}", reportType: "WEEKLY", limit: 5) {
            id
            cityId
            cityName
            reportType
          }
        }
        """;

        var result = await ExecuteGraphQlAsync(client, gql);
        var items = result.GetProperty("data").GetProperty("cityMarketReports");

        Assert.Equal(JsonValueKind.Array, items.ValueKind);

        foreach (var item in items.EnumerateArray())
        {
            Assert.Equal(bratiCity.Id.ToString(), item.GetProperty("cityId").GetString());
            Assert.Equal("WEEKLY", item.GetProperty("reportType").GetString());
        }
    }
}
