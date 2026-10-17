using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class CityLaborMarketsQueryTests
{
    private const string Query = """
        query($companyId: UUID) {
          cityLaborMarkets(companyId: $companyId) {
            cityId cityName population laborPool laborDemand utilization unemploymentRate wageIndex staffingFillRate
            averageSalaryMultiplier updatedAtTick yourSalaryMultiplier payRatio strikeProne
            affectedBuildings { buildingId name staffingFactor onStrike strikeUntilTick }
          }
        }
        """;

    private static async Task<(string Token, Guid PlayerId)> RegisterAsync(HttpClient client)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email = $"labor-q-{Guid.NewGuid():N}@example.com", displayName = "Labor Q", password = "TestPass123!" } });
        var token = result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var me = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return (token, Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!));
    }

    private static async Task<Guid> SeedCompanyAsync(ApiWebApplicationFactory factory, Guid playerId, Guid cityId, decimal salaryMultiplier)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var company = new Company { Id = Guid.NewGuid(), PlayerId = playerId, Name = "Q Co", FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);
        db.CompanyCitySalarySettings.Add(new CompanyCitySalarySetting
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = cityId, SalaryMultiplier = salaryMultiplier,
        });
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static async Task<Guid> SeedMarketAsync(ApiWebApplicationFactory factory, decimal averageSalary)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        db.CityLaborMarkets.Add(new CityLaborMarket
        {
            CityId = city.Id, LaborPool = 1000m, LaborDemand = 400m, Utilization = 0.4m, UnemploymentRate = 0.6m,
            WageIndex = 1m, StaffingFillRate = 1m, AverageSalaryMultiplier = averageSalary, UpdatedAtTick = 7,
        });
        await db.SaveChangesAsync();
        return city.Id;
    }

    [Fact]
    public async Task Unauthenticated_IsRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = (Guid?)null });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task AuthenticatedPlayer_SeesCityMarketsWithoutCompanyFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _) = await RegisterAsync(client);
        var cityId = await SeedMarketAsync(factory, averageSalary: 1.1m);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = (Guid?)null }, token);

        var rows = result.GetProperty("data").GetProperty("cityLaborMarkets");
        var row = rows.EnumerateArray().Single(r => r.GetProperty("cityId").GetString() == cityId.ToString());
        Assert.Equal("Bratislava", row.GetProperty("cityName").GetString());
        Assert.Equal(0.6m, row.GetProperty("unemploymentRate").GetDecimal());
        Assert.Equal(1.1m, row.GetProperty("averageSalaryMultiplier").GetDecimal());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("payRatio").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("yourSalaryMultiplier").ValueKind);
    }

    [Fact]
    public async Task CitiesWithoutAStoredRow_AreStillListedWithNeutralValues()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _) = await RegisterAsync(client);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = (Guid?)null }, token);

        var rows = result.GetProperty("data").GetProperty("cityLaborMarkets").EnumerateArray().ToList();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(await db.Cities.CountAsync(), rows.Count);
        Assert.All(rows, row =>
        {
            Assert.Equal(1m, row.GetProperty("wageIndex").GetDecimal());
            Assert.Equal(1m, row.GetProperty("staffingFillRate").GetDecimal());
            Assert.Equal(0L, row.GetProperty("updatedAtTick").GetInt64());
        });
    }

    [Fact]
    public async Task Owner_SeesPayRatioAndStrikeRiskForOwnCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var cityId = await SeedMarketAsync(factory, averageSalary: 1.2m);
        var companyId = await SeedCompanyAsync(factory, playerId, cityId, salaryMultiplier: 0.9m);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId }, token);

        var rows = result.GetProperty("data").GetProperty("cityLaborMarkets");
        var row = rows.EnumerateArray().Single(r => r.GetProperty("cityId").GetString() == cityId.ToString());
        Assert.Equal(0.9m, row.GetProperty("yourSalaryMultiplier").GetDecimal());
        Assert.Equal(0.75m, row.GetProperty("payRatio").GetDecimal());
        Assert.True(row.GetProperty("strikeProne").GetBoolean());
    }

    [Fact]
    public async Task ForeignAndMissingCompany_ReturnIdenticalForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _) = await RegisterAsync(client);
        var (_, otherPlayerId) = await RegisterAsync(client);
        var cityId = await SeedMarketAsync(factory, averageSalary: 1m);
        var foreignCompanyId = await SeedCompanyAsync(factory, otherPlayerId, cityId, salaryMultiplier: 1m);

        var foreign = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = foreignCompanyId }, token);
        var missing = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = Guid.NewGuid() }, token);

        var foreignError = foreign.GetProperty("errors")[0];
        var missingError = missing.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", foreignError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("extensions").GetProperty("code").GetString(), missingError.GetProperty("extensions").GetProperty("code").GetString());
        Assert.Equal(foreignError.GetProperty("message").GetString(), missingError.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Owner_SeesOnlyTheirStrikingOrUnderstaffedBuildingsPerCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var cityId = await SeedMarketAsync(factory, averageSalary: 1m);
        var companyId = await SeedCompanyAsync(factory, playerId, cityId, salaryMultiplier: 1m);
        Guid strikingId, understaffedId, healthyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.FirstAsync(c => c.Id == cityId);
            Building NewBuilding(string name, decimal staffing, long? until) => new()
            {
                Id = Guid.NewGuid(), CompanyId = companyId, CityId = cityId, Type = BuildingType.Factory, Name = name,
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
                StaffingFactor = staffing, StrikeStartedAtTick = until.HasValue ? 3 : null, StrikeUntilTick = until,
            };
            var striking = NewBuilding("Striking", 1m, 90);
            var understaffed = NewBuilding("Understaffed", 0.4m, null);
            var healthy = NewBuilding("Healthy", 1m, null);
            db.Buildings.AddRange(striking, understaffed, healthy);
            await db.SaveChangesAsync();
            (strikingId, understaffedId, healthyId) = (striking.Id, understaffed.Id, healthy.Id);
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId }, token);

        var row = result.GetProperty("data").GetProperty("cityLaborMarkets").EnumerateArray()
            .Single(r => r.GetProperty("cityId").GetString() == cityId.ToString());
        var affected = row.GetProperty("affectedBuildings").EnumerateArray().ToList();
        Assert.Equal(2, affected.Count);
        var striker = affected.Single(a => a.GetProperty("buildingId").GetString() == strikingId.ToString());
        Assert.True(striker.GetProperty("onStrike").GetBoolean());
        Assert.Equal(90L, striker.GetProperty("strikeUntilTick").GetInt64());
        var short_ = affected.Single(a => a.GetProperty("buildingId").GetString() == understaffedId.ToString());
        Assert.False(short_.GetProperty("onStrike").GetBoolean());
        Assert.Equal(0.4m, short_.GetProperty("staffingFactor").GetDecimal());
        Assert.DoesNotContain(affected, a => a.GetProperty("buildingId").GetString() == healthyId.ToString());
    }

    [Fact]
    public async Task WithoutACompany_AffectedBuildingsIsEmpty()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, _) = await RegisterAsync(client);
        await SeedMarketAsync(factory, averageSalary: 1m);

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId = (Guid?)null }, token);

        var rows = result.GetProperty("data").GetProperty("cityLaborMarkets").EnumerateArray().ToList();
        Assert.All(rows, row => Assert.Equal(0, row.GetProperty("affectedBuildings").GetArrayLength()));
    }

    [Fact]
    public async Task FullyStaffedBuildingWithAnExpiredStrikeMarker_IsNotListed()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client);
        var cityId = await SeedMarketAsync(factory, averageSalary: 1m);
        var companyId = await SeedCompanyAsync(factory, playerId, cityId, salaryMultiplier: 1m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.GameStates.FirstAsync()).CurrentTick = 100;
            var city = await db.Cities.FirstAsync(c => c.Id == cityId);
            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(), CompanyId = companyId, CityId = cityId, Type = BuildingType.Factory, Name = "Settled",
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
                StaffingFactor = 1m, StrikeStartedAtTick = 80, StrikeUntilTick = 90, // ended ten ticks ago, not yet cleared
            });
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(client, Query, new { companyId }, token);

        var row = result.GetProperty("data").GetProperty("cityLaborMarkets").EnumerateArray()
            .Single(r => r.GetProperty("cityId").GetString() == cityId.ToString());
        Assert.Equal(0, row.GetProperty("affectedBuildings").GetArrayLength());
    }
}
