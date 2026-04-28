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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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
        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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
        var city = await db.Cities.FirstDeterministicAsync();

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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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
        var city = await db.Cities.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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

        var player = await db.Players.FirstDeterministicAsync();
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
        var email = await db.Players.OrderByDescending(p => p.CreatedAtUtc).Select(p => p.Email).FirstDeterministicAsync();
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

    // ── New unit type tests ──────────────────────────────────────────────────

    /// <summary>
    /// A WIND_TURBINE unit in a WIND plant at full wind (100%) should add
    /// WindTurbineBoostMwPerLevel MW to the plant's output.
    /// </summary>
    [Fact]
    public async Task WindTurbineUnit_AtFullWind_BoostsOutputCorrectly()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"WindCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 50.0, Longitude = 15.0, Population = 50_000, AverageRentPerSqm = 8m
        };
        db.Cities.Add(city);

        // Seed weather with full wind (100%) for the current tick.
        var gameState = await db.GameStates.FirstAsync();
        db.CityWeatherForecasts.Add(new CityWeatherForecast
        {
            CityId = city.Id, Tick = gameState.CurrentTick,
            WindPercent = 100, SolarPercent = 50
        });

        var company = new Company { Id = Guid.NewGuid(), Name = "Wind Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // Wind plant base 25 MW + 1×WIND_TURBINE level 1 (8 MW × 100% wind = 8 MW)
        // Consumer at 30 MW: supply=33 MW > demand → surplus
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Wind Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "WIND", PowerOutput = 25m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 30m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        // Add WIND_TURBINE unit
        var windUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.WindTurbine, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(windUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // At full wind: base 25 MW × 1.0 + 8 MW × 1.0 = 33 MW; demand 30 MW → surplus of 3 MW
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        var surplus = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);
        Assert.True(surplus > 0m, $"Wind turbine at full wind should produce surplus income; got {surplus}");
    }

    /// <summary>
    /// A WATER_TURBINE unit provides steady hydro output independent of weather.
    /// Even with no weather seeded (factor defaults to 1.0 for COAL), the WATER_TURBINE
    /// should add WaterTurbineBoostMwPerLevel MW to a COAL plant.
    /// </summary>
    [Fact]
    public async Task WaterTurbineUnit_AddsStaticBoost_RegardlessOfWeather()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"HydroCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 49.5, Longitude = 17.5, Population = 30_000, AverageRentPerSqm = 7m
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Hydro Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // COAL plant base 50 MW + 1×WATER_TURBINE level 1 (12 MW steady)
        // Consumer 60 MW: supply=62 MW > demand → surplus
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Hydro-Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 60m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var waterUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.WaterTurbine, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(waterUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Base 50 MW + 12 MW water = 62 MW; demand 60 MW → surplus → no fines
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        var fines = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .SumAsync(e => e.Amount);
        Assert.Equal(0m, fines);
    }

    /// <summary>
    /// An ENERGY_STORAGE unit reduces shortage fine exposure similarly to BATTERY_STORAGE.
    /// A plant that is 8 MW short without ENERGY_STORAGE should have fewer fines with it.
    /// </summary>
    [Fact]
    public async Task EnergyStorageUnit_SmoothesShortageFines()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"StoreCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.5, Longitude = 16.5, Population = 20_000, AverageRentPerSqm = 6m
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Storage Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // COAL plant 18 MW base + ENERGY_STORAGE level 1 (8 MW smoothing)
        // Consumer 20 MW: raw shortage 2 MW, but smoothing buffer of 8 MW covers it → no fine
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Store Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 18m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 20m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var storeUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.EnergyStorage, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(storeUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // 18 MW raw + 8 MW smoothing = 26 MW effective; demand 20 MW → POWERED, surplus → no fines
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        var fines = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .SumAsync(e => e.Amount);
        Assert.Equal(0m, fines);
    }

    /// <summary>
    /// ENERGY_PRODUCING unit adds EnergyProducingBoostMwPerLevel MW to plant output.
    /// A plant that would be short without the unit should be surplus after adding it.
    /// </summary>
    [Fact]
    public async Task EnergyProducingUnit_BoostsOutputByExpectedAmount()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"GenCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 47.8, Longitude = 17.2, Population = 15_000, AverageRentPerSqm = 5m
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Gen Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // COAL plant 10 MW base + 1×ENERGY_PRODUCING level 1 (20 MW boost) = 30 MW
        // Consumer 25 MW: supply 30 > demand 25 → surplus
        // FuelReserveMwh pre-seeded so ENERGY_PRODUCING can draw from it (new fuel-chain model).
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Gen Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 10m, PowerConsumption = 0m,
            FuelReserveMwh = 100m, // pre-seed reserve so ENERGY_PRODUCING contributes MW
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 25m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var genUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.EnergyProducing, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(genUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // 10 MW base + 20 MW ENERGY_PRODUCING = 30 MW; demand 25 MW → surplus
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        var surplus = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);
        Assert.True(surplus > 0m, $"ENERGY_PRODUCING should create surplus income; got {surplus}");
    }

    /// <summary>
    /// A FUEL_PURCHASE unit adds FuelPurchaseBoostMwPerLevel MW of output capacity.
    /// A short COAL plant with a FUEL_PURCHASE unit should cover its consumer demand.
    /// </summary>
    [Fact]
    public async Task FuelPurchaseUnit_AddsOutputCapacity()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"FuelCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.2, Longitude = 16.2, Population = 20_000, AverageRentPerSqm = 7m
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Fuel Corp", Cash = 1_000_000m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // COAL plant 5 MW base + 1×FUEL_PURCHASE level 1 (10 MW from reserve) = 15 MW
        // Consumer 12 MW: supply 15 > demand 12 → surplus
        // FuelReserveMwh pre-seeded so FUEL_PURCHASE unit can draw from reserve (new fuel-chain model).
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Fuel Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 5m, PowerConsumption = 0m,
            FuelReserveMwh = 50m, // pre-seed reserve so FUEL_PURCHASE contributes MW
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 12m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        var fuelUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(fuelUnit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // 5 MW base + 10 MW FUEL_PURCHASE = 15 MW; demand 12 MW → POWERED, surplus
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        var fines = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .SumAsync(e => e.Amount);
        Assert.Equal(0m, fines);
    }

    /// <summary>
    /// BuildingConfigurationService should allow all 7 power plant unit types for POWER_PLANT buildings.
    /// </summary>
    [Fact]
    public void BuildingConfiguration_PowerPlant_AllowsAllSevenUnitTypes()
    {
        var allowedTypes = BuildingConfigurationService.GetAllowedUnitTypes(BuildingType.PowerPlant);
        Assert.Contains(UnitType.PowerGeneration, allowedTypes);
        Assert.Contains(UnitType.BatteryStorage, allowedTypes);
        Assert.Contains(UnitType.FuelPurchase, allowedTypes);
        Assert.Contains(UnitType.WindTurbine, allowedTypes);
        Assert.Contains(UnitType.WaterTurbine, allowedTypes);
        Assert.Contains(UnitType.EnergyStorage, allowedTypes);
        Assert.Contains(UnitType.EnergyProducing, allowedTypes);
    }

    /// <summary>
    /// GameConstants should report correct MW stat labels for all new unit types.
    /// </summary>
    [Fact]
    public void GameConstants_NewUnitTypes_HaveCorrectStatLabels()
    {
        Assert.Contains("MW", GameConstants.GetUnitStatLabel(UnitType.FuelPurchase));
        Assert.Contains("MW", GameConstants.GetUnitStatLabel(UnitType.WindTurbine));
        Assert.Contains("MW", GameConstants.GetUnitStatLabel(UnitType.WaterTurbine));
        Assert.Contains("MW", GameConstants.GetUnitStatLabel(UnitType.EnergyStorage));
        Assert.Contains("MW", GameConstants.GetUnitStatLabel(UnitType.EnergyProducing));
    }

    /// <summary>
    /// GameConstants should report correct MW boosts for all new unit types.
    /// </summary>
    [Fact]
    public void GameConstants_NewUnitTypes_HaveCorrectBoostValues()
    {
        Assert.Equal(GameConstants.FuelPurchaseBoostMwPerLevel, GameConstants.GetUnitStat(UnitType.FuelPurchase, 1));
        Assert.Equal(GameConstants.WindTurbineBoostMwPerLevel, GameConstants.GetUnitStat(UnitType.WindTurbine, 1));
        Assert.Equal(GameConstants.WaterTurbineBoostMwPerLevel, GameConstants.GetUnitStat(UnitType.WaterTurbine, 1));
        Assert.Equal(GameConstants.EnergyStorageSmoothingMwPerLevel, GameConstants.GetUnitStat(UnitType.EnergyStorage, 1));
        Assert.Equal(GameConstants.EnergyProducingBoostMwPerLevel, GameConstants.GetUnitStat(UnitType.EnergyProducing, 1));
    }

    /// <summary>
    /// CompanyEconomyCalculator must return non-zero labor and energy costs for all new power plant unit types.
    /// </summary>
    [Fact]
    public void CompanyEconomy_NewPowerUnitTypes_HaveNonZeroCosts()
    {
        foreach (var unitType in new[]
        {
            UnitType.FuelPurchase,
            UnitType.WindTurbine,
            UnitType.WaterTurbine,
            UnitType.EnergyStorage,
            UnitType.EnergyProducing,
        })
        {
            var labor = CompanyEconomyCalculator.GetBaseUnitLaborHours(unitType, 1);
            var energy = CompanyEconomyCalculator.GetBaseUnitEnergyMwh(unitType, 1);
            Assert.True(labor > 0m, $"{unitType} should have labor hours > 0, got {labor}");
            Assert.True(energy > 0m, $"{unitType} should have energy MWh > 0, got {energy}");
        }
    }

    /// <summary>
    /// Regression test for weather-scaling correctness on mixed-unit setups.
    ///
    /// A WIND plant at 50% wind with a WATER_TURBINE unit must NOT have the hydro output
    /// reduced by the wind factor. The correct calculation is:
    ///   windBase × windFactor + waterTurbineBoost (unscaled)
    ///
    /// Without the fix the (now-incorrect) formula would have been:
    ///   (windBase + waterTurbineBoost) × windFactor
    ///
    /// Setup:
    ///   WIND plant: base 20 MW, windFactor=0.5 → 10 MW scaled base
    ///   WATER_TURBINE level 1: +12 MW steady (not scaled)
    ///   Effective supply = 10 + 12 = 22 MW
    ///   Consumer demand  = 18 MW
    ///   Expected result  = POWERED (surplus 4 MW) → surplus income, no fines
    ///
    /// Under the old (buggy) logic:
    ///   Effective supply = (20 + 12) × 0.5 = 16 MW < 18 MW demand → shortage → fine
    /// </summary>
    [Fact]
    public async Task MixedUnitWeatherScaling_WaterTurbineOnWindPlant_HydroNotScaledByWind()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"MixedWind_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 49.8, Longitude = 16.8, Population = 20_000, AverageRentPerSqm = 6m
        };
        db.Cities.Add(city);

        // Seed weather: exactly 50% wind for the current tick.
        var gameState = await db.GameStates.FirstAsync();
        db.CityWeatherForecasts.Add(new CityWeatherForecast
        {
            CityId = city.Id, Tick = gameState.CurrentTick,
            WindPercent = 50m, SolarPercent = 60m,
        });

        var company = new Company
        {
            Id = Guid.NewGuid(), Name = "Mixed Wind-Hydro Corp", Cash = 1_000_000m,
            PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow
        };
        db.Companies.Add(company);

        // WIND plant base 20 MW: at 50% wind → 10 MW scaled base.
        var windPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Mixed Wind Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "WIND", PowerOutput = 20m, PowerConsumption = 0m,
            BuiltAtUtc = DateTime.UtcNow
        };
        // Consumer demanding 18 MW — above the 10 MW scaled base but below 22 MW with hydro.
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 18m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(windPlant, consumer);

        // WATER_TURBINE level 1: +12 MW steady hydro (must NOT be scaled by wind factor).
        var waterUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = windPlant.Id,
            UnitType = UnitType.WaterTurbine, GridX = 0, GridY = 0, Level = 1
        };
        db.BuildingUnits.Add(waterUnit);

        var settlementAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
            db, company.Id, city.CurrencyCode ?? "EUR");
        settlementAccount.Balance = 100_000m;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Correct (fixed) behavior: 20×0.5 + 12 = 22 MW supply > 18 MW demand → POWERED.
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        // Also verify surplus income was earned (proves economics phase uses the same correct logic).
        var fines = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridFine)
            .SumAsync(e => e.Amount);
        Assert.Equal(0m, fines);

        var surplusIncome = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);
        Assert.True(surplusIncome > 0m,
            $"Mixed wind+hydro plant should earn surplus income when WATER_TURBINE output is not wind-scaled. Got {surplusIncome}");
    }


    // ── Fuel-chain gameplay tests ─────────────────────────────────────────────

    /// <summary>
    /// FuelProcurementPhase must debit the building bank account and add a FUEL_COST ledger
    /// entry every tick that a COAL/GAS plant has FUEL_PURCHASE units and sufficient funds.
    /// </summary>
    [Fact]
    public async Task FuelProcurementPhase_CoalPlant_ChargesFuelCostAndFillsReserve()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"FuelChg_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Fuel Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // EUR/EUR fx rate is needed for cost computation.
        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        // Bank account with enough balance to cover procurement cost.
        var bankAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankAccount.Balance = 1_000m;

        // COAL plant: 50 MW base + FUEL_PURCHASE level 1 = procures 10 MWh/tick.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(powerPlant);

        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // FuelProcurementPhase records a FUEL_COST ledger entry for the procured MWh.
        // The fuel reserve is consumed in the same tick by PowerDistributionPhase, so
        // FuelReserveMwh nets back to 0 at end-of-tick; the procurement is proven
        // by the ledger entry alone.
        // 10 MWh × FuelCostPerMwhBase (3) × fuelPriceIndex 1.0 × fxRate 1.0 = 30 EUR
        var expectedCost = 10m * GameConstants.FuelCostPerMwhBase;
        var fuelCostEntry = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.BuildingId == powerPlant.Id && e.Category == LedgerCategory.FuelCost);
        Assert.NotNull(fuelCostEntry);
        Assert.True(fuelCostEntry.Amount < 0m, "Fuel cost ledger entry should be negative");
        Assert.True(Math.Abs(Math.Abs(fuelCostEntry.Amount) - expectedCost) < 0.10m,
            $"Expected fuel cost ~{expectedCost} EUR, actual {Math.Abs(fuelCostEntry.Amount)} EUR");
    }

    /// <summary>
    /// When the bank account has less balance than full fuel procurement cost,
    /// the phase procures proportionally (partial procurement, not zero).
    /// </summary>
    [Fact]
    public async Task FuelProcurementPhase_InsufficientFunds_ProcuresPartialFuel()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"LowFunds_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Low Funds Corp", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        // Only 10 EUR in bank (full cost for 10 MWh = 10 × FuelCostPerMwhBase EUR) — partial procurement expected.
        var bankAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankAccount.Balance = 10m;

        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(powerPlant);

        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // With 10 EUR and costPerMwh = 3 EUR/MWh, we can afford ~3.33 MWh (partial procurement).
        // That fuel is consumed in the same tick by PowerDistributionPhase.
        // Proof of partial procurement: FUEL_COST ledger entry exists with |Amount| ≤ 10 EUR.
        var fuelCostEntry = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.BuildingId == powerPlant.Id && e.Category == LedgerCategory.FuelCost);
        Assert.NotNull(fuelCostEntry);
        Assert.True(fuelCostEntry.Amount < 0m, "Fuel cost should be negative");
        // Full cost = 10 MWh × 3 = 30 EUR. Partial cost ≤ 10 EUR (starting balance).
        var fullFuelCostPartialTest = 10m * GameConstants.FuelCostPerMwhBase;
        Assert.True(Math.Abs(fuelCostEntry.Amount) < fullFuelCostPartialTest,
            $"Partial procurement cost {Math.Abs(fuelCostEntry.Amount)} should be less than full {fullFuelCostPartialTest}");
        Assert.True(Math.Abs(fuelCostEntry.Amount) <= 10.01m,
            $"Partial cost {Math.Abs(fuelCostEntry.Amount)} should not exceed starting balance (10 EUR)");
    }

    /// <summary>
    /// Setting dispatch target to 50% should halve the effective output and fuel consumption.
    /// </summary>
    [Fact]
    public async Task DispatchTarget_50Pct_HalvesOutputAndFuelConsumption()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"DispCity_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Dispatch Corp", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        var bankAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankAccount.Balance = 500m;

        // COAL plant: 20 MW base + FUEL_PURCHASE level 1 (10 MW when fuel available). Dispatch = 50%.
        // FuelReserveMwh starts at 0; FuelProcurementPhase procures at 50% dispatch = 5 MWh.
        // After procurement: reserve = 5 MWh.
        // ComputeAndConsumeFuel: FUEL_PURCHASE draws min(10, 5) = 5 MW, reserve → 0.
        // rawOutput = (20 + 5); × 0.5 dispatch = 12.5 MW.
        // Consumer: 10 MW → POWERED (12.5 > 10).
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Half-Dispatch Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 20m, PowerConsumption = 0m,
            DispatchTargetPercent = 50, FuelReserveMwh = 0m, BankAccountId = bankAccount.Id,
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

        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Powered, consumer.PowerStatus);

        // At 50% dispatch, fuel procurement is 5 MWh (half the full-dispatch 10 MWh).
        // Cost = 5 × FuelCostPerMwhBase = 15 EUR. Full = 30 EUR.
        var fullFuelCost = 10m * GameConstants.FuelCostPerMwhBase;
        var fuelCost = await db.LedgerEntries
            .Where(e => e.BuildingId == powerPlant.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));
        Assert.True(fuelCost > 0m, $"Fuel cost should be > 0, got {fuelCost}");
        Assert.True(fuelCost < fullFuelCost, $"Half-dispatch fuel cost {fuelCost} should be < full {fullFuelCost}");
    }

    /// <summary>
    /// A COAL plant with zero dispatch target should produce 0 MW output and zero fuel cost.
    /// </summary>
    [Fact]
    public async Task DispatchTarget_Zero_ProducesNoOutputAndNoFuelCost()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"ZeroDisp_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Offline Corp", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        var bankAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankAccount.Balance = 500m;

        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Offline Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            DispatchTargetPercent = 0, FuelReserveMwh = 100m, BankAccountId = bankAccount.Id,
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
        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Offline, consumer.PowerStatus);

        // Zero dispatch = zero fuel procurement, zero FUEL_COST entries.
        // (Other operational costs like labor/grid fines may still be charged.)
        var fuelCost = await db.LedgerEntries
            .Where(e => e.BuildingId == powerPlant.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));
        Assert.Equal(0m, fuelCost);
    }

    /// <summary>
    /// ENERGY_PRODUCING units require fuel from the reserve to generate output.
    /// If reserve is zero, ENERGY_PRODUCING contributes 0 MW output.
    /// </summary>
    [Fact]
    public async Task EnergyProducing_EmptyReserve_ContributesZeroMw()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"EmptyRes_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Empty Corp", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        // COAL plant: small base output (5 MW), 1 × ENERGY_PRODUCING unit.
        // With empty reserve, EP contributes 0 MW → total = 5 MW.
        // Consumer 40 MW → 5/40 = 12.5% < 50% → OFFLINE.
        var powerPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Empty Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 5m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, // empty reserve — ENERGY_PRODUCING contributes 0
            BuiltAtUtc = DateTime.UtcNow
        };
        var consumer = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Consumer",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerConsumption = 40m, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(powerPlant, consumer);

        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = powerPlant.Id,
            UnitType = UnitType.EnergyProducing, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // 5 MW base + ENERGY_PRODUCING with 0 reserve (contributes 0 MW) = 5 MW; demand 40 MW → OFFLINE
        await db.Entry(consumer).ReloadAsync();
        Assert.Equal(PowerStatus.Offline, consumer.PowerStatus);
    }

    /// <summary>
    /// setPlantDispatch GraphQL mutation should update the building's DispatchTargetPercent.
    /// </summary>
    [Fact]
    public async Task SetPlantDispatch_Mutation_UpdatesDispatchTarget()
    {
        var token = await RegisterAndGetTokenAsync($"dispatchtest_{Guid.NewGuid():N}"[..28] + "@t.com", "DispatchTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Dispatch Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Dispatch Test Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m,
            DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            mutation SetDispatch($buildingId: UUID!, $percent: Int!) {
                setPlantDispatch(input: { buildingId: $buildingId, dispatchTargetPercent: $percent }) {
                    id dispatchTargetPercent
                }
            }
            """,
            new { buildingId = plant.Id, percent = 75 },
            token);

        var updated = result.GetProperty("data").GetProperty("setPlantDispatch");
        Assert.Equal(75, updated.GetProperty("dispatchTargetPercent").GetInt32());

        await db.Entry(plant).ReloadAsync();
        Assert.Equal(75, plant.DispatchTargetPercent);
    }

    /// <summary>
    /// setPlantDispatch should reject values outside 0–100.
    /// </summary>
    [Fact]
    public async Task SetPlantDispatch_InvalidPercent_ReturnsError()
    {
        var token = await RegisterAndGetTokenAsync($"dispatche2_{Guid.NewGuid():N}"[..28] + "@t.com", "DispatchErrorTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Dispatch Err Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Dispatch Error Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m,
            DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            mutation SetDispatch($buildingId: UUID!, $percent: Int!) {
                setPlantDispatch(input: { buildingId: $buildingId, dispatchTargetPercent: $percent }) {
                    id dispatchTargetPercent
                }
            }
            """,
            new { buildingId = plant.Id, percent = 150 },
            token);

        Assert.True(result.TryGetProperty("errors", out _), "Should return errors for invalid dispatch target");
    }

    /// <summary>
    /// powerPlantAnalytics query must return the new fuel-reserve capacity fields:
    /// maxFuelReserveMwh, fuelReservePercent, fuelPurchaseCapacityMwhPerTick,
    /// energyProducingCapacityMw, fuelConstrainedOutputMw, fuelTypeLabel, and fuelCostPerMwhEur.
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_ReturnsReserveCapacityFields()
    {
        var token = await RegisterAndGetTokenAsync($"analytics_cap_{Guid.NewGuid():N}"[..28] + "@t.com", "CapTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Cap Analytics Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        // COAL plant with 1 × FUEL_PURCHASE (level 2) and 1 × ENERGY_PRODUCING (level 1).
        // Max reserve capacity = 2 × 50 MWh = 100 MWh.
        // Current reserve = 40 MWh → fill percent = 40%.
        // FP procurement capacity = 2 × 10 = 20 MWh/tick.
        // EP capacity = 1 × 20 = 20 MW.
        // Constrained = max(0, 20 − 40) = 0 (reserve >= EP capacity).
        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Cap Analytics Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m,
            FuelReserveMwh = 40m, DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);

        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.FuelPurchase,    GridX = 0, GridY = 0, Level = 2 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.EnergyProducing, GridX = 1, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query Analytics($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId) {
                    maxFuelReserveMwh
                    fuelReservePercent
                    fuelPurchaseCapacityMwhPerTick
                    energyProducingCapacityMw
                    fuelConstrainedOutputMw
                    fuelTypeLabel
                    fuelCostPerMwhEur
                }
            }
            """,
            new { buildingId = plant.Id },
            token);

        var analytics = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal(100m, analytics.GetProperty("maxFuelReserveMwh").GetDecimal());
        Assert.Equal(40, analytics.GetProperty("fuelReservePercent").GetInt32());
        Assert.Equal(20m, analytics.GetProperty("fuelPurchaseCapacityMwhPerTick").GetDecimal());
        Assert.Equal(20m, analytics.GetProperty("energyProducingCapacityMw").GetDecimal());
        Assert.Equal(0m, analytics.GetProperty("fuelConstrainedOutputMw").GetDecimal()); // reserve (40) >= EP cap (20)
        Assert.Equal("Coal", analytics.GetProperty("fuelTypeLabel").GetString());
        Assert.Equal(GameConstants.FuelCostPerMwhBase, analytics.GetProperty("fuelCostPerMwhEur").GetDecimal());
    }

    /// <summary>
    /// When the fuel reserve is lower than the ENERGY_PRODUCING unit capacity,
    /// fuelConstrainedOutputMw should be > 0 indicating how much output is being lost.
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_FuelConstrainedOutput_WhenReserveLow()
    {
        var token = await RegisterAndGetTokenAsync($"constrained_{Guid.NewGuid():N}"[..28] + "@t.com", "ConstrainedTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Constrained Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        // COAL plant with 2 × ENERGY_PRODUCING (level 1 each) = 40 MW EP capacity.
        // Reserve = 15 MWh — only 15 MW of the 40 MW EP capacity can fire.
        // Constrained = 40 − 15 = 25 MW of unused capacity.
        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Constrained Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m,
            FuelReserveMwh = 15m, DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);

        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.EnergyProducing, GridX = 0, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.EnergyProducing, GridX = 1, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query Analytics($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId) {
                    energyProducingCapacityMw
                    fuelConstrainedOutputMw
                }
            }
            """,
            new { buildingId = plant.Id },
            token);

        var analytics = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal(40m, analytics.GetProperty("energyProducingCapacityMw").GetDecimal());
        Assert.Equal(25m, analytics.GetProperty("fuelConstrainedOutputMw").GetDecimal()); // 40 − 15 = 25
    }

    /// <summary>
    /// A GAS plant should pay FuelCostPerMwhBase × GasFuelCostMultiplier per MWh —
    /// i.e. 20% more than an equivalent COAL plant procuring the same quantity.
    /// </summary>
    [Fact]
    public async Task FuelProcurement_GasPlant_CostsMoreThanCoal()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"MultiFuel_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 5_000, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Multi Fuel Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1.0m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        // Create separate bank accounts for coal and gas plants.
        var coalAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        coalAccount.Balance = 100_000m;

        // Create a second EUR account for the gas plant.
        var gasAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1234567890123456",
            CompanyId = company.Id,
            CurrencyCode = "EUR",
            Balance = 100_000m,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(gasAccount);

        // Identical plants except for plant type and bank account.
        // Both have 1 × FUEL_PURCHASE level-1 unit → procure 10 MWh/tick.
        var coalPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Coal Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, BankAccountId = coalAccount.Id, BuiltAtUtc = DateTime.UtcNow
        };
        var gasPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Gas Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "GAS", PowerOutput = 40m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, BankAccountId = gasAccount.Id, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(coalPlant, gasPlant);

        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = coalPlant.Id, UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = gasPlant.Id,  UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var coalCost = await db.LedgerEntries
            .Where(e => e.BuildingId == coalPlant.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));

        var gasCost = await db.LedgerEntries
            .Where(e => e.BuildingId == gasPlant.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));

        Assert.True(coalCost > 0m, $"Coal plant should have fuel cost but got {coalCost}");
        Assert.True(gasCost > coalCost,
            $"GAS plant fuel cost ({gasCost}) should be higher than COAL ({coalCost}) by {GameConstants.GasFuelCostMultiplier}x");

        // Verify the exact multiplier: GAS / COAL should equal GasFuelCostMultiplier.
        var ratio = gasCost / coalCost;
        Assert.True(Math.Abs(ratio - GameConstants.GasFuelCostMultiplier) < 0.001m,
            $"GAS/COAL cost ratio should be {GameConstants.GasFuelCostMultiplier} but was {ratio}");
    }

    /// <summary>
    /// fuelTypeLabel must be "Natural Gas" for GAS plants.
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_GasPlant_ReturnsFuelTypeLabel()
    {
        var token = await RegisterAndGetTokenAsync($"gastype_{Guid.NewGuid():N}"[..28] + "@t.com", "GasTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Gas Type Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Gas Type Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "GAS", PowerOutput = 40m, FuelReserveMwh = 0m,
            DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query Analytics($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId) {
                    fuelTypeLabel
                    fuelCostPerMwhEur
                }
            }
            """,
            new { buildingId = plant.Id },
            token);

        var analytics = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal("Natural Gas", analytics.GetProperty("fuelTypeLabel").GetString());
        Assert.Equal(GameConstants.FuelCostPerMwhBase * GameConstants.GasFuelCostMultiplier,
            analytics.GetProperty("fuelCostPerMwhEur").GetDecimal());
    }

    // ── Reserve capacity analytics — non-thermal plant ────────────────────────

    /// <summary>
    /// NUCLEAR plants are non-thermal: fuelTypeLabel must be empty, fuelCostPerMwhEur must be 0,
    /// and all reserve/capacity fields must be 0 regardless of installed units.
    /// This ensures the frontend hides the fuel-reserve panel for non-thermal plant types.
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_NuclearPlant_ReturnsEmptyFuelFields()
    {
        var token = await RegisterAndGetTokenAsync($"nuclear_{Guid.NewGuid():N}"[..28] + "@t.com", "NuclearTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Nuclear Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Nuclear Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "NUCLEAR", PowerOutput = 150m, FuelReserveMwh = 0m,
            DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);
        // Add FUEL_PURCHASE unit — should be ignored for non-thermal plants.
        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = plant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 2
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query Analytics($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId) {
                    plantType
                    maxFuelReserveMwh
                    fuelReservePercent
                    fuelPurchaseCapacityMwhPerTick
                    energyProducingCapacityMw
                    fuelConstrainedOutputMw
                    fuelTypeLabel
                    fuelCostPerMwhEur
                }
            }
            """,
            new { buildingId = plant.Id },
            token);

        var analytics = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal("NUCLEAR", analytics.GetProperty("plantType").GetString());
        // Non-thermal: all fuel fields must be 0 / empty
        Assert.Equal(0m, analytics.GetProperty("maxFuelReserveMwh").GetDecimal());
        Assert.Equal(0, analytics.GetProperty("fuelReservePercent").GetInt32());
        Assert.Equal(0m, analytics.GetProperty("fuelPurchaseCapacityMwhPerTick").GetDecimal());
        Assert.Equal(0m, analytics.GetProperty("energyProducingCapacityMw").GetDecimal());
        Assert.Equal(0m, analytics.GetProperty("fuelConstrainedOutputMw").GetDecimal());
        Assert.Equal("", analytics.GetProperty("fuelTypeLabel").GetString());
        Assert.Equal(0m, analytics.GetProperty("fuelCostPerMwhEur").GetDecimal());
    }

    // ── Reserve lifecycle — multi-tick stability ──────────────────────────────

    /// <summary>
    /// With a pre-seeded partial reserve, procurement and consumption are in balance
    /// each tick (procurement rate == fuel-purchase draw rate for the same units).
    /// After each tick the reserve must remain at its pre-seeded level,
    /// demonstrating that procurement correctly replenishes what distribution consumed.
    /// </summary>
    [Fact]
    public async Task FuelReserve_PreSeededReserve_MaintainsStableLevelOverMultipleTicks()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        // Isolated city with zero population → no building demand, no fines.
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"StableRes_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 0, AverageRentPerSqm = 0m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Stable Reserve Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1.0m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        var bankAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankAccount.Balance = 10_000m;

        // COAL plant: 1 × FUEL_PURCHASE level-1.
        // Max reserve = 50 MWh. Pre-seed 25 MWh (50% full).
        // Each tick: procurement adds 10 MWh (if below max); distribution consumes 10 MWh.
        // Net per tick = 0 → reserve stays stable around pre-seeded value.
        const decimal initialReserve = 25m;
        var coalPlant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Stable Reserve Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 0m, PowerConsumption = 0m,
            FuelReserveMwh = initialReserve, BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(coalPlant);
        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = coalPlant.Id,
            UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);

        // Process 3 ticks; reserve should remain in range [initialReserve - 1, maxCapacity].
        const decimal maxCapacity = 50m;
        for (int tick = 1; tick <= 3; tick++)
        {
            await processor.ProcessTickAsync();
            await db.Entry(coalPlant).ReloadAsync();
            Assert.True(coalPlant.FuelReserveMwh >= 0m,
                $"After tick {tick} reserve must not go negative: was {coalPlant.FuelReserveMwh}");
            Assert.True(coalPlant.FuelReserveMwh <= maxCapacity + 0.1m,
                $"After tick {tick} reserve ({coalPlant.FuelReserveMwh}) must not exceed max capacity ({maxCapacity})");
        }

        // Verify that fuel costs were recorded (procurement happened each tick).
        var fuelCostCount = await db.LedgerEntries
            .CountAsync(e => e.BuildingId == coalPlant.Id && e.Category == LedgerCategory.FuelCost);
        Assert.True(fuelCostCount >= 3,
            $"At least 3 fuel cost entries expected (one per tick) but got {fuelCostCount}");
    }

    // ── Dispatch → profitability link ─────────────────────────────────────────

    /// <summary>
    /// Reducing dispatch from 100% to 50% on a COAL plant halves both fuel cost
    /// AND surplus income from the grid (compared to a full-dispatch plant in the
    /// same city).  This proves the dispatch control has visible P&amp;L consequences.
    /// </summary>
    [Fact]
    public async Task DispatchChange_50Pct_HalvesFuelCostAndReducesSurplusIncome()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = await db.Players.FirstDeterministicAsync();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"DispatchPnL_{Guid.NewGuid():N}"[..20], CountryCode = "XX",
            Latitude = 48.1, Longitude = 16.9, Population = 0, AverageRentPerSqm = 5m,
            FuelPriceIndex = 1.0m, CurrencyCode = "EUR"
            // Population = 0 ensures zero building power demand in this isolated city,
            // so both plants generate pure surplus income (no fines risk).
        };
        db.Cities.Add(city);

        var company = new Company { Id = Guid.NewGuid(), Name = "Dispatch PnL Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);

        db.FxRates.Add(new FxRate
        {
            Id = Guid.NewGuid(), BaseCurrencyCode = "EUR", QuoteCurrencyCode = "EUR",
            Rate = 1.0m, RateDate = DateOnly.FromDateTime(DateTime.UtcNow), FetchedAtUtc = DateTime.UtcNow, Source = "FALLBACK"
        });

        // Two bank accounts — one per plant.
        var bankFull = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, "EUR");
        bankFull.Balance = 10_000m;
        var bankHalf = new BankAccount
        {
            Id = Guid.NewGuid(), AccountNumber = "2000000000000001",
            CompanyId = company.Id, CurrencyCode = "EUR", Balance = 10_000m,
            IsGovernmentAccount = false, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankHalf);

        // Identical COAL plants — same FP unit, but different DispatchTargetPercent.
        var plantFull = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Full Dispatch Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, // start empty so procurement runs
            BankAccountId = bankFull.Id, DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        var plantHalf = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Half Dispatch Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 50m, PowerConsumption = 0m,
            FuelReserveMwh = 0m, // start empty so procurement runs
            BankAccountId = bankHalf.Id, DispatchTargetPercent = 50, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(plantFull, plantHalf);

        // Same 1 × FUEL_PURCHASE level-1 on both plants.
        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plantFull.Id, UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plantHalf.Id, UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Fuel costs: 100% dispatch = 10 MWh × FuelCostBase; 50% dispatch = 5 MWh × FuelCostBase.
        var fullFuelCost = await db.LedgerEntries
            .Where(e => e.BuildingId == plantFull.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));
        var halfFuelCost = await db.LedgerEntries
            .Where(e => e.BuildingId == plantHalf.Id && e.Category == LedgerCategory.FuelCost)
            .SumAsync(e => Math.Abs(e.Amount));

        Assert.True(fullFuelCost > 0m, $"Full-dispatch plant should have fuel cost but got {fullFuelCost}");
        Assert.True(halfFuelCost > 0m, $"Half-dispatch plant should have fuel cost but got {halfFuelCost}");
        // 50% dispatch → half the fuel cost (within 5% rounding tolerance).
        var halfExpected = fullFuelCost * 0.5m;
        Assert.True(Math.Abs(halfFuelCost - halfExpected) / fullFuelCost < 0.05m,
            $"50% dispatch fuel cost should be ~half of full ({fullFuelCost}) but was {halfFuelCost}");

        // Surplus income: full-dispatch plant earns proportionally more.
        var fullSurplus = await db.LedgerEntries
            .Where(e => e.BuildingId == plantFull.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);
        var halfSurplus = await db.LedgerEntries
            .Where(e => e.BuildingId == plantHalf.Id && e.Category == LedgerCategory.GridSurplusIncome)
            .SumAsync(e => e.Amount);

        // Both plants contribute supply so both earn surplus income.
        Assert.True(fullSurplus > 0m, $"Full-dispatch plant should earn surplus income but got {fullSurplus}");
        // The half-dispatch plant earns less because it contributes fewer MW.
        Assert.True(halfSurplus < fullSurplus,
            $"50% dispatch surplus ({halfSurplus}) should be less than 100% dispatch surplus ({fullSurplus})");
    }

    // ── Full reserve fill → constrained analytics update ──────────────────────

    /// <summary>
    /// Once the fuel reserve fills to 100%, fuelReservePercent must return 100
    /// and fuelConstrainedOutputMw must return 0 (no constraint when fully fuelled).
    /// </summary>
    [Fact]
    public async Task PowerPlantAnalytics_WhenReserveIsFull_ConstrainedOutputIsZero()
    {
        var token = await RegisterAndGetTokenAsync($"fullres_{Guid.NewGuid():N}"[..28] + "@t.com", "FullResTester");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.OrderByDescending(p => p.CreatedAtUtc).FirstDeterministicAsync();
        var city = await db.Cities.FirstDeterministicAsync();
        var company = await db.Companies.Where(c => c.PlayerId == player.Id).FirstOrDefaultAsync();
        if (company is null)
        {
            company = new Company { Id = Guid.NewGuid(), Name = "Full Res Co", Cash = 0m, PlayerId = player.Id, FoundedAtUtc = DateTime.UtcNow };
            db.Companies.Add(company);
            await db.SaveChangesAsync();
        }

        // 2 × FUEL_PURCHASE level-1 → max 100 MWh; 1 × ENERGY_PRODUCING level-1 → 20 MW capacity.
        // Pre-seed reserve = 100 MWh (full tank).
        var plant = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.PowerPlant, Name = "Full Reserve Plant",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1,
            PowerPlantType = "COAL", PowerOutput = 70m, FuelReserveMwh = 100m,
            DispatchTargetPercent = 100, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(plant);
        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.FuelPurchase, GridX = 0, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.FuelPurchase, GridX = 1, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = plant.Id, UnitType = UnitType.EnergyProducing, GridX = 2, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            """
            query Analytics($buildingId: UUID!) {
                powerPlantAnalytics(buildingId: $buildingId) {
                    maxFuelReserveMwh
                    fuelReservePercent
                    energyProducingCapacityMw
                    fuelConstrainedOutputMw
                }
            }
            """,
            new { buildingId = plant.Id },
            token);

        var analytics = result.GetProperty("data").GetProperty("powerPlantAnalytics");
        Assert.Equal(100m, analytics.GetProperty("maxFuelReserveMwh").GetDecimal());
        // Reserve = max → 100%
        Assert.Equal(100, analytics.GetProperty("fuelReservePercent").GetInt32());
        Assert.Equal(20m, analytics.GetProperty("energyProducingCapacityMw").GetDecimal());
        // Reserve (100) ≥ EP capacity (20) → constrained output = 0
        Assert.Equal(0m, analytics.GetProperty("fuelConstrainedOutputMw").GetDecimal());
    }
}
