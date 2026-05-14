using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class PowerPriorityEnergyTests
{
    private static async Task<JsonElement> ExecuteAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement;
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task PowerDistribution_LoadShedding_UsesPowerPriorityOrder()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = $"Priority City {Guid.NewGuid():N}",
            CountryCode = "TS",
            Population = 100_000,
            Latitude = 48.1,
            Longitude = 17.1,
            CurrencyCode = "EUR",
            AverageRentPerSqm = 10m,
            BaseSalaryPerManhour = 10m,
        };
        db.Cities.Add(city);
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"priority-{Guid.NewGuid():N}@test.com",
            DisplayName = "Priority Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Priority Co",
        };
        db.Players.Add(player);
        db.Companies.Add(company);

        var plant = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.PowerPlant,
            Name = "City Plant",
            PowerPlantType = PowerPlantType.Coal,
            PowerOutput = 50m,
        };

        var highPriorityFactory = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "High Priority Factory",
            PowerConsumption = 40m,
            PowerPriority = 10,
        };

        var lowPriorityFactory = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Low Priority Factory",
            PowerConsumption = 40m,
            PowerPriority = 1,
        };

        db.Buildings.AddRange(plant, highPriorityFactory, lowPriorityFactory);
        await db.SaveChangesAsync();

        var context = new TickContext
        {
            Db = db,
            GameState = await db.GameStates.FirstAsync(),
            BuildingsById = db.Buildings.ToDictionary(b => b.Id),
            UnitsByBuilding = db.BuildingUnits.ToList().GroupBy(u => u.BuildingId).ToDictionary(g => g.Key, g => g.ToList()),
            WeatherByCity = new Dictionary<Guid, WeatherSnapshot>(),
        };

        var phase = new PowerDistributionPhase();
        await phase.ProcessAsync(context);

        Assert.Equal(PowerStatus.Powered, highPriorityFactory.PowerStatus);
        Assert.Equal(PowerStatus.Constrained, lowPriorityFactory.PowerStatus);
    }

    [Fact]
    public async Task SetPowerPriority_Mutation_UpdatesOwnerBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var email = $"owner-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAsync(client, email, "Owner");

        Guid buildingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Email == email);
            var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = "Owner Co",
            };
            buildingId = Guid.NewGuid();
            db.Companies.Add(company);
            db.Buildings.Add(new Building
            {
                Id = buildingId,
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Factory",
                PowerConsumption = 20m,
                PowerPriority = 3,
            });
            await db.SaveChangesAsync();
        }

        var result = await ExecuteAsync(
            client,
            "mutation Set($input: SetPowerPriorityInput!) { setPowerPriority(input: $input) { id powerPriority } }",
            new { input = new { buildingId, priority = 9 } },
            token);

        Assert.Equal(9, result.GetProperty("data").GetProperty("setPowerPriority").GetProperty("powerPriority").GetInt32());
    }

    [Fact]
    public async Task SetPowerPriority_Mutation_ForeignBuilding_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, $"owner2-{Guid.NewGuid():N}@test.com", "Owner2");
        var foreignToken = await RegisterAsync(client, $"other-{Guid.NewGuid():N}@test.com", "Other");

        Guid buildingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Players.FirstAsync(p => p.Email.Contains("owner2-"));
            var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
            var company = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "Owner2 Co" };
            buildingId = Guid.NewGuid();
            db.Companies.Add(company);
            db.Buildings.Add(new Building
            {
                Id = buildingId,
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Owner Factory",
                PowerConsumption = 10m,
            });
            await db.SaveChangesAsync();
        }

        // keep owner token referenced to avoid accidental optimization/drop in test setup
        Assert.False(string.IsNullOrWhiteSpace(ownerToken));

        var result = await ExecuteAsync(
            client,
            "mutation Set($input: SetPowerPriorityInput!) { setPowerPriority(input: $input) { id } }",
            new { input = new { buildingId, priority = 8 } },
            foreignToken);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal("FORBIDDEN", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SetPowerPriority_Mutation_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteAsync(
            client,
            "mutation Set($input: SetPowerPriorityInput!) { setPowerPriority(input: $input) { id } }",
            new { input = new { buildingId = Guid.NewGuid(), priority = 8 } });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Equal("AUTH_NOT_AUTHENTICATED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task EnergyGridStatus_ReturnsSupplyDemandAndOfflineBuildings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        Guid cityId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = new City
            {
                Id = Guid.NewGuid(),
                Name = $"Grid City {Guid.NewGuid():N}",
                CountryCode = "TS",
                Population = 100_000,
                Latitude = 48.2,
                Longitude = 17.2,
                CurrencyCode = "EUR",
                AverageRentPerSqm = 10m,
                BaseSalaryPerManhour = 10m,
            };
            db.Cities.Add(city);
            cityId = city.Id;

            var player = new Player
            {
                Id = Guid.NewGuid(),
                Email = $"grid-{Guid.NewGuid():N}@test.com",
                DisplayName = "Grid Tester",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            };
            var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Grid Co" };
            db.Players.Add(player);
            db.Companies.Add(company);

            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = cityId,
                Type = BuildingType.PowerPlant,
                Name = "Grid Plant",
                PowerPlantType = PowerPlantType.Gas,
                PowerOutput = 30m,
                PowerStatus = PowerStatus.Powered,
            });

            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = cityId,
                Type = BuildingType.Factory,
                Name = "Factory A",
                PowerConsumption = 20m,
                PowerStatus = PowerStatus.Powered,
                PowerPriority = 9,
            });

            db.Buildings.Add(new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = cityId,
                Type = BuildingType.SalesShop,
                Name = "Shop B",
                PowerConsumption = 20m,
                PowerStatus = PowerStatus.Offline,
                PowerPriority = 2,
            });

            await db.SaveChangesAsync();
        }

        var result = await ExecuteAsync(
            client,
            "query Grid($cityId: UUID!) { energyGridStatus(cityId: $cityId) { cityId totalSupplyKw totalDemandKw surplusOrDeficitKw offlineBuildings { buildingName powerStatus powerPriority } } }",
            new { cityId });

        var grid = result.GetProperty("data").GetProperty("energyGridStatus");
        Assert.Equal(30000m, grid.GetProperty("totalSupplyKw").GetDecimal());
        Assert.Equal(40000m, grid.GetProperty("totalDemandKw").GetDecimal());
        Assert.Equal(-10000m, grid.GetProperty("surplusOrDeficitKw").GetDecimal());
        Assert.Single(grid.GetProperty("offlineBuildings").EnumerateArray());
    }

    [Fact]
    public async Task BuildingEnergyStatus_Query_RequiresOwnership()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerEmail = $"bes-owner-{Guid.NewGuid():N}@test.com";
        var ownerToken = await RegisterAsync(client, ownerEmail, "Owner");
        var otherToken = await RegisterAsync(client, $"bes-other-{Guid.NewGuid():N}@test.com", "Other");

        Guid buildingId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Players.FirstAsync(p => p.Email == ownerEmail);
            var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
            var company = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "BES Co" };
            buildingId = Guid.NewGuid();
            db.Companies.Add(company);
            db.Buildings.Add(new Building
            {
                Id = buildingId,
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Energy Factory",
                PowerConsumption = 15m,
                PowerPriority = 7,
                PowerStatus = PowerStatus.Powered,
            });
            await db.SaveChangesAsync();
        }

        var ownerResult = await ExecuteAsync(
            client,
            "query Q($buildingId: UUID!) { buildingEnergyStatus(buildingId: $buildingId) { buildingId powerPriority powerStatus powerDemandKw } }",
            new { buildingId },
            ownerToken);

        var ownerStatus = ownerResult.GetProperty("data").GetProperty("buildingEnergyStatus");
        Assert.Equal(7, ownerStatus.GetProperty("powerPriority").GetInt32());
        Assert.Equal(15000m, ownerStatus.GetProperty("powerDemandKw").GetDecimal());

        var foreignResult = await ExecuteAsync(
            client,
            "query Q($buildingId: UUID!) { buildingEnergyStatus(buildingId: $buildingId) { buildingId } }",
            new { buildingId },
            otherToken);

        Assert.True(foreignResult.TryGetProperty("errors", out var foreignErrors));
        Assert.Equal("FORBIDDEN", foreignErrors[0].GetProperty("extensions").GetProperty("code").GetString());

        var unauthResult = await ExecuteAsync(
            client,
            "query Q($buildingId: UUID!) { buildingEnergyStatus(buildingId: $buildingId) { buildingId } }",
            new { buildingId });

        Assert.True(unauthResult.TryGetProperty("errors", out var unauthErrors));
        Assert.Equal("AUTH_NOT_AUTHENTICATED", unauthErrors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
