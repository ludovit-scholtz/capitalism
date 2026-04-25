using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Integration tests for the city power-grid simulation.
/// These tests run tick-processing operations and are isolated in their own
/// <see cref="ApiWebApplicationFactory"/> fixture to avoid shared-database interference
/// with other test classes.
/// </summary>
public sealed class PowerGridIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public PowerGridIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private async Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> RegisterAndGetTokenAsync(string email, string displayName = "Tester", string password = "TestPass123!")
    {
        var result = await ExecuteGraphQlAsync(
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return Task.FromResult(new TickProcessor(db, phases, logger));
    }

    // ── Unit tests (no DB) ───────────────────────────────────────────────────

    [Fact]
    public void PowerPlant_DefaultOutput_CorrectPerType()
    {
        Assert.Equal(50m, GameConstants.DefaultPowerOutputMw("COAL"));
        Assert.Equal(40m, GameConstants.DefaultPowerOutputMw("GAS"));
        Assert.Equal(20m, GameConstants.DefaultPowerOutputMw("SOLAR"));
        Assert.Equal(25m, GameConstants.DefaultPowerOutputMw("WIND"));
        Assert.Equal(200m, GameConstants.DefaultPowerOutputMw("NUCLEAR"));
    }

    [Fact]
    public void PowerDemand_CorrectValues_PerBuildingType()
    {
        Assert.Equal(5m, GameConstants.PowerDemandMw(BuildingType.Factory, 1));
        Assert.Equal(10m, GameConstants.PowerDemandMw(BuildingType.Factory, 2));
        Assert.Equal(2m, GameConstants.PowerDemandMw(BuildingType.Mine, 1));
        Assert.Equal(1m, GameConstants.PowerDemandMw(BuildingType.SalesShop, 1));
        Assert.Equal(0m, GameConstants.PowerDemandMw(BuildingType.PowerPlant, 1));
    }

    // ── Tick-engine tests ────────────────────────────────────────────────────

    [Fact]
    public async Task PowerDistribution_WithEnoughSupply_AllBuildingsPowered()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"PoweredCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 49.0, Longitude = 14.0, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Powered Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 50 MW coal plant supplies 5 MW factory → POWERED.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factory = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Test Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, factory);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(powerPlant).ReloadAsync();
        await db.Entry(factory).ReloadAsync();

        Assert.True(powerPlant.PowerStatus == PowerStatus.Powered,
            "Power plant should always be POWERED");
        Assert.True(factory.PowerStatus == PowerStatus.Powered,
            "Factory should be POWERED when supply (50 MW) >= demand (5 MW)");
    }

    [Fact]
    public async Task PowerDistribution_WithShortage_BuildingsConstrained()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"ShortCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 49.1, Longitude = 14.1, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Underpowered Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 8 MW supply / 15 MW demand (3 × 5 MW factories) = 53% → CONSTRAINED.
        // Use COAL here so this threshold test stays deterministic and does not depend on
        // weather-scaled renewable output.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Small Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 8m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factories = Enumerable.Range(1, 3).Select(i => new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = $"Factory {i}",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            BuiltAtUtc = DateTime.UtcNow
        }).ToList();
        db.Buildings.Add(powerPlant);
        db.Buildings.AddRange(factories);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(powerPlant).ReloadAsync();
        foreach (var f in factories) await db.Entry(f).ReloadAsync();

        Assert.True(powerPlant.PowerStatus == PowerStatus.Powered, "Power plant is always POWERED");
        foreach (var f in factories)
        {
            Assert.True(f.PowerStatus == PowerStatus.Constrained,
                $"{f.Name} should be CONSTRAINED (53% supply), but was {f.PowerStatus}");
        }
    }

    [Fact]
    public async Task PowerDistribution_RenewableOutput_UsesCurrentWeatherFactor()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var gameState = await db.GameStates.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"SolarCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 49.2, Longitude = 14.2, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Solar Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.CityWeatherForecasts.Add(new CityWeatherForecast
        {
            CityId = city.Id,
            Tick = gameState.CurrentTick + 1,
            WindPercent = 50m,
            SolarPercent = 40m,
        });

        var solarPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Weather Solar Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "SOLAR", PowerOutput = 20m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factories = Enumerable.Range(1, 3).Select(i => new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = $"Solar Factory {i}",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            BuiltAtUtc = DateTime.UtcNow
        }).ToList();
        db.Buildings.Add(solarPlant);
        db.Buildings.AddRange(factories);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(solarPlant).ReloadAsync();
        foreach (var factory in factories) await db.Entry(factory).ReloadAsync();

        Assert.Equal(PowerStatus.Powered, solarPlant.PowerStatus);
        foreach (var factory in factories)
        {
            Assert.Equal(
                PowerStatus.Constrained,
                factory.PowerStatus);
        }
    }

    [Fact]
    public async Task PowerDistribution_WithNoPowerPlants_BuildingsPoweredByLegacyGrid()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"LegacyCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.0, Longitude = 15.0, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Legacy Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        var factory = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Legacy Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = GameConstants.PowerDemandMw(BuildingType.Factory, 1),
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(factory);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(factory).ReloadAsync();

        Assert.True(factory.PowerStatus == PowerStatus.Powered,
            $"Factory should remain POWERED in city without power plants (legacy grid), but was {factory.PowerStatus}");
    }

    [Fact]
    public async Task PowerDistribution_ManufacturingSkipped_WhenBuildingOffline()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // 1 MW supply / 5 MW demand = 20% → OFFLINE (below 50%).
        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"OfflineCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.3, Longitude = 15.3, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Offline Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Tiny Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "SOLAR", PowerOutput = 1m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };

        var product = await db.ProductTypes.Include(p => p.Recipes).FirstAsync(p => p.Slug == "wooden-chair");
        var woodResource = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var factory = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Offline Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, factory);

        var manufacturingUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = factory.Id, UnitType = UnitType.Manufacturing,
            GridX = 0, GridY = 0, Level = 1, ProductTypeId = product.Id
        };
        db.BuildingUnits.Add(manufacturingUnit);

        var woodInventory = new Inventory
        {
            Id = Guid.NewGuid(), BuildingId = factory.Id, BuildingUnitId = manufacturingUnit.Id,
            ResourceTypeId = woodResource.Id, Quantity = 50m, SourcingCostTotal = 500m, Quality = 0.8m
        };
        db.Inventories.Add(woodInventory);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(factory).ReloadAsync();
        Assert.True(factory.PowerStatus == PowerStatus.Offline,
            $"Factory should be OFFLINE when supply is below 50% of demand, but was {factory.PowerStatus}");

        await db.Entry(woodInventory).ReloadAsync();
        Assert.True(woodInventory.Quantity == 50m,
            $"Manufacturing should not consume inputs when OFFLINE. Quantity was {woodInventory.Quantity}");
    }

    // ── GraphQL API tests ────────────────────────────────────────────────────

    [Fact]
    public async Task PowerPlant_WhenPurchased_HasOutputAndPlantType()
    {
        var token = await RegisterAndGetTokenAsync($"pp_{Guid.NewGuid():N}@test.com");

        var companyResult = await ExecuteGraphQlAsync(
            "mutation CreateCompany($input: CreateCompanyInput!) { createCompany(input: $input) { id } }",
            new { input = new { name = "Energy Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = city.Id, Name = "Power Test Lot", Description = "Test lot",
            District = "Energy Zone", Latitude = city.Latitude + 0.02, Longitude = city.Longitude + 0.02,
            Price = 80_000m, SuitableTypes = "POWER_PLANT", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                building { id type powerOutput powerPlantType powerConsumption powerStatus }
              }
            }
            """,
            new { input = new { companyId, lotId = lot.Id.ToString(), buildingType = "POWER_PLANT", buildingName = "Test Coal Plant", powerPlantType = "COAL" } },
            token);

        var building = result.GetProperty("data").GetProperty("purchaseLot").GetProperty("building");
        Assert.Equal("POWER_PLANT", building.GetProperty("type").GetString());
        Assert.Equal("COAL", building.GetProperty("powerPlantType").GetString());
        Assert.True(building.GetProperty("powerOutput").GetDecimal() > 0m,
            "Coal power plant should have positive power output");
        Assert.True(building.GetProperty("powerConsumption").GetDecimal() == 0m,
            "Power plants do not consume power");
    }

    [Fact]
    public async Task PurchasePowerPlantLot_WithGasType_SetsOutputCorrectly()
    {
        var token = await RegisterAndGetTokenAsync($"gas_{Guid.NewGuid():N}@test.com");

        var companyResult = await ExecuteGraphQlAsync(
            "mutation CreateCompany($input: CreateCompanyInput!) { createCompany(input: $input) { id } }",
            new { input = new { name = "Gas Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = city.Id, Name = "Gas Test Lot", Description = "Test lot",
            District = "Energy Zone", Latitude = city.Latitude + 0.03, Longitude = city.Longitude + 0.03,
            Price = 80_000m, SuitableTypes = "POWER_PLANT", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                building { powerOutput powerPlantType }
              }
            }
            """,
            new { input = new { companyId, lotId = lot.Id.ToString(), buildingType = "POWER_PLANT", buildingName = "Gas Station", powerPlantType = "GAS" } },
            token);

        var building = result.GetProperty("data").GetProperty("purchaseLot").GetProperty("building");
        Assert.Equal("GAS", building.GetProperty("powerPlantType").GetString());
        Assert.Equal(40m, building.GetProperty("powerOutput").GetDecimal());
    }

    [Fact]
    public async Task CityPowerBalance_Query_ReturnsCorrectBalance()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"BalCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.1, Longitude = 15.1, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Balance Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Balance Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factory = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Balance Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, factory);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query CityPowerBalance($cityId: UUID!) {
              cityPowerBalance(cityId: $cityId) {
                cityId totalSupplyMw totalDemandMw reserveMw reservePercent status
                powerPlantCount consumerBuildingCount
                powerPlants { buildingId buildingName plantType outputMw powerStatus }
              }
            }
            """,
            new { cityId = city.Id.ToString() });

        var balance = result.GetProperty("data").GetProperty("cityPowerBalance");
        Assert.Equal(city.Id.ToString(), balance.GetProperty("cityId").GetString());
        Assert.Equal(50m, balance.GetProperty("totalSupplyMw").GetDecimal());
        Assert.Equal(5m, balance.GetProperty("totalDemandMw").GetDecimal());
        Assert.Equal(45m, balance.GetProperty("reserveMw").GetDecimal());
        Assert.Equal("BALANCED", balance.GetProperty("status").GetString());
        Assert.Equal(1, balance.GetProperty("powerPlantCount").GetInt32());
        Assert.Equal(1, balance.GetProperty("consumerBuildingCount").GetInt32());

        var plants = balance.GetProperty("powerPlants").EnumerateArray().ToList();
        Assert.Single(plants);
        Assert.Equal("COAL", plants[0].GetProperty("plantType").GetString());
        Assert.Equal(50m, plants[0].GetProperty("outputMw").GetDecimal());
    }

    [Fact]
    public async Task CityPowerBalance_Query_ShowsShortageStatus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"ShortCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.2, Longitude = 15.2, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Shortage Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 8 MW supply / 15 MW demand = 53% → CONSTRAINED.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Partial Solar Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "SOLAR", PowerOutput = 8m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factories = Enumerable.Range(1, 3).Select(i => new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = $"Factory {i}",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        }).ToList();
        db.Buildings.Add(powerPlant);
        db.Buildings.AddRange(factories);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            "query CityPowerBalance($cityId: UUID!) { cityPowerBalance(cityId: $cityId) { totalSupplyMw totalDemandMw reserveMw status } }",
            new { cityId = city.Id.ToString() });

        var balance = result.GetProperty("data").GetProperty("cityPowerBalance");
        Assert.Equal(8m, balance.GetProperty("totalSupplyMw").GetDecimal());
        Assert.Equal(15m, balance.GetProperty("totalDemandMw").GetDecimal());
        Assert.Equal(-7m, balance.GetProperty("reserveMw").GetDecimal());
        Assert.Equal("CONSTRAINED", balance.GetProperty("status").GetString());
    }

    [Fact]
    public async Task CityPowerBalance_Query_IsPublic_NoAuthRequired()
    {
        // cityPowerBalance is a public query — no token should be required.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync();

        // Execute WITHOUT a bearer token.
        var result = await ExecuteGraphQlAsync(
            "query CityPowerBalance($cityId: UUID!) { cityPowerBalance(cityId: $cityId) { cityId totalSupplyMw totalDemandMw status } }",
            new { cityId = city.Id.ToString() }); // no token argument

        Assert.False(result.TryGetProperty("errors", out _), "cityPowerBalance should be accessible without auth");
        var balance = result.GetProperty("data").GetProperty("cityPowerBalance");
        Assert.Equal(city.Id.ToString(), balance.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task CityPowerBalance_Query_ShowsCriticalStatus_WhenSupplyBelow50Percent()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"CritCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.4, Longitude = 15.4, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Critical Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 2 MW supply / 15 MW demand = 13% → CRITICAL (below 50%).
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Tiny Wind Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "WIND", PowerOutput = 2m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factories = Enumerable.Range(1, 3).Select(i => new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = $"Critical Factory {i}",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        }).ToList();
        db.Buildings.Add(powerPlant);
        db.Buildings.AddRange(factories);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            "query CityPowerBalance($cityId: UUID!) { cityPowerBalance(cityId: $cityId) { totalSupplyMw totalDemandMw status } }",
            new { cityId = city.Id.ToString() });

        var balance = result.GetProperty("data").GetProperty("cityPowerBalance");
        Assert.Equal("CRITICAL", balance.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PowerDistribution_WithCriticalShortage_BuildingsGoOffline()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"CritOff_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.5, Longitude = 15.5, Population = 100_000, AverageRentPerSqm = 10m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Critical Offline Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 2 MW supply / 20 MW demand = 10% → all consumers OFFLINE.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Nano Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "SOLAR", PowerOutput = 2m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var factories = Enumerable.Range(1, 4).Select(i => new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = $"OffFactory {i}",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        }).ToList();
        db.Buildings.Add(powerPlant);
        db.Buildings.AddRange(factories);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        foreach (var f in factories) await db.Entry(f).ReloadAsync();
        foreach (var f in factories)
        {
            Assert.True(f.PowerStatus == PowerStatus.Offline,
                $"{f.Name} should be OFFLINE when supply is below 50% of demand, but was {f.PowerStatus}");
        }
    }

    [Fact]
    public async Task PurchasePowerPlantLot_WithNuclearType_SetsHighOutput()
    {
        var token = await RegisterAndGetTokenAsync($"nuc_{Guid.NewGuid():N}@test.com");

        var companyResult = await ExecuteGraphQlAsync(
            "mutation CreateCompany($input: CreateCompanyInput!) { createCompany(input: $input) { id } }",
            new { input = new { name = "Nuclear Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = city.Id, Name = "Nuclear Test Lot", Description = "Test lot",
            District = "Energy Zone", Latitude = city.Latitude + 0.04, Longitude = city.Longitude + 0.04,
            Price = 80_000m, SuitableTypes = "POWER_PLANT", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                building { powerOutput powerPlantType }
              }
            }
            """,
            new { input = new { companyId, lotId = lot.Id.ToString(), buildingType = "POWER_PLANT", buildingName = "Nuclear Station", powerPlantType = "NUCLEAR" } },
            token);

        var building = result.GetProperty("data").GetProperty("purchaseLot").GetProperty("building");
        Assert.Equal("NUCLEAR", building.GetProperty("powerPlantType").GetString());
        Assert.Equal(200m, building.GetProperty("powerOutput").GetDecimal());
    }

    [Fact]
    public async Task Building_PowerStatus_IsReturnedInBuildingQuery()
    {
        // Verify that powerStatus is part of the building GraphQL type and returned via myCompanies.
        var email = $"ps_{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(email);

        // Create company via mutation (ensures the right player owns it).
        var companyResult = await ExecuteGraphQlAsync(
            "mutation CreateCompany($input: CreateCompanyInput!) { createCompany(input: $input) { id } }",
            new { input = new { name = "Status Corp" } }, token);
        var companyId = Guid.Parse(companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!);

        // Add a building directly to the DB so we can assert on powerStatus.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync();
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = companyId, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Status Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, PowerStatus = PowerStatus.Powered, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            "query { myCompanies { buildings { id powerStatus } } }",
            token: token);

        var companies = result.GetProperty("data").GetProperty("myCompanies").EnumerateArray().ToList();
        Assert.NotEmpty(companies);
        var buildings = companies
            .SelectMany(c => c.GetProperty("buildings").EnumerateArray())
            .ToList();
        var testBuilding = buildings.FirstOrDefault(b => b.GetProperty("id").GetString() == building.Id.ToString());
        Assert.True(testBuilding.ValueKind != JsonValueKind.Undefined, "Test building should appear in myCompanies result");
        Assert.Equal("POWERED", testBuilding.GetProperty("powerStatus").GetString());
    }

    // ── cityWeatherForecast query tests ──────────────────────────────────────

    [Fact]
    public async Task CityWeatherForecast_Query_ReturnsRollingForecastData()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"WeatherCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 51.0, Longitude = 16.0, Population = 50_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);

        // Seed 5 forecast rows for this city.
        for (var i = 0; i < 5; i++)
        {
            db.CityWeatherForecasts.Add(new CityWeatherForecast
            {
                CityId = city.Id,
                Tick = 100 + i,
                WindPercent = 60m + i,
                SolarPercent = 70m - (i * 2),
            });
        }
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query CityWeatherForecast($cityId: UUID!) {
              cityWeatherForecast(cityId: $cityId) {
                cityId currentWindPercent currentSolarPercent
                forecast { tick windPercent solarPercent }
              }
            }
            """,
            new { cityId = city.Id.ToString() });

        Assert.False(result.TryGetProperty("errors", out _), "cityWeatherForecast should not return errors");

        var forecast = result.GetProperty("data").GetProperty("cityWeatherForecast");
        Assert.Equal(city.Id.ToString(), forecast.GetProperty("cityId").GetString());

        // currentWindPercent/currentSolarPercent = values from the first (lowest-tick) row.
        Assert.Equal(60m, forecast.GetProperty("currentWindPercent").GetDecimal());
        Assert.Equal(70m, forecast.GetProperty("currentSolarPercent").GetDecimal());

        var forecastTicks = forecast.GetProperty("forecast").EnumerateArray().ToList();
        Assert.Equal(5, forecastTicks.Count);
        Assert.Equal(100, forecastTicks[0].GetProperty("tick").GetInt64());
        Assert.Equal(60m, forecastTicks[0].GetProperty("windPercent").GetDecimal());
        Assert.Equal(70m, forecastTicks[0].GetProperty("solarPercent").GetDecimal());
    }

    [Fact]
    public async Task CityWeatherForecast_Query_IsPublic_NoAuthRequired()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"PubWeather_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 51.5, Longitude = 16.5, Population = 40_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        db.CityWeatherForecasts.Add(new CityWeatherForecast
        {
            CityId = city.Id, Tick = 200, WindPercent = 55m, SolarPercent = 45m,
        });
        await db.SaveChangesAsync();

        // Execute WITHOUT a bearer token — query must be public.
        var result = await ExecuteGraphQlAsync(
            "query CityWeatherForecast($cityId: UUID!) { cityWeatherForecast(cityId: $cityId) { cityId currentWindPercent currentSolarPercent } }",
            new { cityId = city.Id.ToString() }); // no token argument

        Assert.False(result.TryGetProperty("errors", out _), "cityWeatherForecast should be accessible without auth");
        var data = result.GetProperty("data").GetProperty("cityWeatherForecast");
        Assert.Equal(city.Id.ToString(), data.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task CityWeatherForecast_Query_ReturnsNullForCityWithNoForecastData()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"NoWeather_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.0, Longitude = 17.0, Population = 30_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        // No forecast rows seeded for this city — query should return null.
        var result = await ExecuteGraphQlAsync(
            "query CityWeatherForecast($cityId: UUID!) { cityWeatherForecast(cityId: $cityId) { cityId } }",
            new { cityId = city.Id.ToString() });

        Assert.False(result.TryGetProperty("errors", out _), "cityWeatherForecast with no data should not error");
        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").GetProperty("cityWeatherForecast").ValueKind);
    }

    // ── PowerGridEconomicsPhase tests ────────────────────────────────────────

    /// <summary>
    /// When city supply &gt; demand: plant operator should receive GRID_SURPLUS_INCOME
    /// at GridSurplusIncomePerMwTick × surplusMw per tick.
    /// </summary>
    [Fact]
    public async Task PowerGridEconomics_SurplusSupply_PlantEarnsSurplusIncome()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"SurplusCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.1, Longitude = 17.1, Population = 20_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Surplus Energy Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 50 MW supply, 5 MW demand → 45 MW surplus
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Big Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Coal, PowerOutput = 50m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Small Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);
        var settlementAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
            db,
            company.Id,
            city.CurrencyCode ?? "EUR");
        await db.SaveChangesAsync();

        var balanceBefore = settlementAccount.Balance;

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(settlementAccount).ReloadAsync();
        var surplusLedger = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .ToListAsync();

        Assert.True(surplusLedger.Count >= 1, "Should have at least one GRID_SURPLUS_INCOME entry");

        // surplusMw = 45, capacityShare = 1.0, income = 45 × 5 = 225
        var expectedIncome = 45m * GameConstants.GridSurplusIncomePerMwTick;
        Assert.Equal(expectedIncome, surplusLedger.First().Amount);
        Assert.True(
            settlementAccount.Balance >= balanceBefore + expectedIncome,
            $"Settlement account should have increased by surplus income; before={balanceBefore}, after={settlementAccount.Balance}");
    }

    /// <summary>
    /// When city supply &lt; demand: plant operator should be charged GRID_FINE
    /// at GridFinePerMwTick × shortageMw per tick.
    /// </summary>
    [Fact]
    public async Task PowerGridEconomics_ShortageSupply_PlantPaysFine()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"ShortageCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.2, Longitude = 17.2, Population = 20_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Tiny Energy Co", Cash = 100_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 5 MW supply, 50 MW demand → 45 MW shortage
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Tiny Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Gas, PowerOutput = 5m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Big Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 50m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);
        var settlementAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
            db,
            company.Id,
            city.CurrencyCode ?? "EUR");
        settlementAccount.Balance = 100_000m;
        await db.SaveChangesAsync();

        var balanceBefore = settlementAccount.Balance;
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(settlementAccount).ReloadAsync();
        var fineLedger = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .ToListAsync();

        Assert.True(fineLedger.Count >= 1, "Should have at least one GRID_FINE entry");

        // shortageMw = 45, capacityShare = 1.0, fine = 45 × 8 = 360
        var expectedFine = 45m * GameConstants.GridFinePerMwTick;
        Assert.Equal(-expectedFine, fineLedger.First().Amount);
        Assert.True(
            settlementAccount.Balance <= balanceBefore - expectedFine,
            $"Settlement account should have decreased by fine; before={balanceBefore}, after={settlementAccount.Balance}");
    }

    /// <summary>
    /// With multiple power plants, each earns/pays proportional to its output share.
    /// </summary>
    [Fact]
    public async Task PowerGridEconomics_MultiplePlants_SplitByCapacityShare()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"SplitCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.3, Longitude = 17.3, Population = 20_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        // Two companies each with one plant
        var company1 = new Company { Id = Guid.NewGuid(), Name = "Plant Corp A", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        var company2 = new Company { Id = Guid.NewGuid(), Name = "Plant Corp B", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.AddRange(company1, company2);

        // Plant A: 60 MW, Plant B: 40 MW → total 100 MW supply
        // Consumer: 0 MW demand → 100 MW surplus
        // Expected split: A gets 60%, B gets 40%
        var plantA = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company1.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Plant A",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Coal, PowerOutput = 60m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var plantB = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company2.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Plant B",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Gas, PowerOutput = 40m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        // Consumer with small demand so there's a surplus
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company1.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Small Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 10m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(plantA, plantB, consumer);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var surplusA = await db.LedgerEntries
            .Where(e => e.CompanyId == company1.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);
        var surplusB = await db.LedgerEntries
            .Where(e => e.CompanyId == company2.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);

        // surplusMw = 90 MW, A share = 60/100 = 60%, B share = 40/100 = 40%
        var expectedA = 90m * GameConstants.GridSurplusIncomePerMwTick * 0.6m;
        var expectedB = 90m * GameConstants.GridSurplusIncomePerMwTick * 0.4m;
        Assert.Equal(expectedA, surplusA);
        Assert.Equal(expectedB, surplusB);
    }

    /// <summary>
    /// A POWER_GENERATION unit boosts the plant's rated output before the economics calculation.
    /// Plant rated at 30 MW + level-2 POWER_GENERATION unit (+20 MW) → 50 MW effective.
    /// </summary>
    [Fact]
    public async Task PowerGridEconomics_PowerGenerationUnit_BoostsOutputForEconomics()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"BoostCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.4, Longitude = 17.4, Population = 20_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Boosted Plant Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // Rated 30 MW + L2 POWER_GENERATION = 30 + 20 = 50 MW
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Boosted Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Coal, PowerOutput = 30m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var genUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.PowerGeneration, GridX = 0, GridY = 0, Level = 2
        };
        db.BuildingUnits.Add(genUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var surplusIncome = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);

        // Effective supply = 30 + (10 × 2) = 50 MW; demand = 5 MW; surplus = 45 MW
        var expectedIncome = 45m * GameConstants.GridSurplusIncomePerMwTick;
        Assert.Equal(expectedIncome, surplusIncome);
    }

    /// <summary>
    /// A BATTERY_STORAGE unit adds smoothing buffer that prevents a shortage-status scenario.
    /// Without battery: 6 MW supply / 10 MW demand → shortage fine.
    /// With L1 BATTERY_STORAGE (+5 MW buffer): effective 11 MW / 10 MW demand → surplus income.
    /// </summary>
    [Fact]
    public async Task PowerGridEconomics_BatteryStorageUnit_ReducesShortageExposure()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"BattCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.5, Longitude = 17.5, Population = 20_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Battery Plant Co", Cash = 100_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // 6 MW rated + L1 BATTERY_STORAGE (+5 MW smoothing) = 11 MW effective; demand = 10 MW → surplus
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Battery Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Coal, PowerOutput = 6m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 10m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var battUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.BatteryStorage, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(battUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var fines = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .SumAsync(e => e.Amount);
        var surplusIncome = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);

        // Battery pushes effective supply to 11 MW; demand 10 MW → 1 MW surplus, no fines
        Assert.Equal(0m, fines);
        Assert.True(surplusIncome > 0m, $"Battery smoothing should convert shortage to surplus income, got {surplusIncome}");

        // Also verify power status is POWERED (not CONSTRAINED / OFFLINE)
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);
    }

    /// <summary>
    /// powerPlantAnalytics query returns accurate aggregated P&amp;L data for a plant owner.
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_ReturnsCorrectPandL()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var token = await RegisterAndGetTokenAsync($"ppa_{Guid.NewGuid():N}@test.com");

        // Resolve newly registered player
        var email = await db.Players.OrderByDescending(p => p.CreatedAtUtc).Select(p => p.Email).FirstAsync();
        var player = await db.Players.FirstAsync(p => p.Email == email);

        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"PpaCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 52.6, Longitude = 17.6, Population = 20_000, AverageRentPerSqm = 5m,
            CurrencyCode = "EUR"
        };
        db.Cities.Add(city);
        var company = new Company { Id = Guid.NewGuid(), Name = "Analytics Plant Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Analytics Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = PowerPlantType.Coal, PowerOutput = 50m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 5m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var result = await ExecuteGraphQlAsync(
            @"query PPA($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId, limit: 100) {
                    buildingId
                    plantType
                    currentOutputMw
                    totalSurplusIncome
                    totalGridFines
                    totalNetProfit
                    timeline {
                        tick
                        surplusIncome
                        gridFine
                        netProfit
                    }
                }
            }",
            new { buildingId = powerPlant.Id.ToString() },
            token);

        Assert.False(result.TryGetProperty("errors", out var errs), $"powerPlantAnalytics query should not error: {errs}");
        var ppa = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal("COAL", ppa.GetProperty("plantType").GetString());
        Assert.Equal(50.0, ppa.GetProperty("currentOutputMw").GetDouble(), 0);
        Assert.True(ppa.GetProperty("totalSurplusIncome").GetDecimal() > 0m, "Should have surplus income after tick");
        Assert.Equal(0m, ppa.GetProperty("totalGridFines").GetDecimal());
        Assert.True(ppa.GetProperty("totalNetProfit").GetDecimal() > 0m, "Net profit should be positive with surplus");
        Assert.True(ppa.GetProperty("timeline").GetArrayLength() > 0, "Timeline should have entries");
    }
}
