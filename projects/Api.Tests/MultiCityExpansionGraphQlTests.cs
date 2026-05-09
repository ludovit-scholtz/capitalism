using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class MultiCityExpansionGraphQlTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };

        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Expansion Tester", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task GetCrossCityShipments_ReturnsRoutesForOwnedCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"cross-city-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        Guid companyId;
        Guid routeId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (companyId, routeId) = await SeedRouteAsync(db, playerId, "cross-city");
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query($companyId: UUID) { getCrossCityShipments(companyId: $companyId) { id companyId status } }",
            new { companyId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var shipments = result.GetProperty("data").GetProperty("getCrossCityShipments");
        Assert.Single(shipments.EnumerateArray());
        Assert.Equal(routeId.ToString(), shipments[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task GetLogisticsCostEstimate_UsesCeil500KmTransitRule()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"logistics-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        Guid sourceBuildingId;
        Guid destinationCityId;
        Guid resourceTypeId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var bratislava = db.Cities.First(c => c.Name == "Bratislava");
            var destination = db.Cities.First(c => c.Name != bratislava.Name);
            var wood = db.ResourceTypes.First(r => r.Slug == "wood");
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Name = "Logistics Co",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
            };

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = bratislava.Id,
                Type = BuildingType.Factory,
                Name = "Origin",
                Latitude = bratislava.Latitude,
                Longitude = bratislava.Longitude,
                Level = 1,
            };

            db.Companies.Add(company);
            db.Buildings.Add(building);
            await db.SaveChangesAsync();

            sourceBuildingId = building.Id;
            destinationCityId = destination.Id;
            resourceTypeId = wood.Id;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($originBuildingId: UUID!, $destinationCityId: UUID!, $resourceTypeId: UUID!, $quantity: Decimal!) {
              getLogisticsCostEstimate(
                originBuildingId: $originBuildingId
                destinationCityId: $destinationCityId
                resourceTypeId: $resourceTypeId
                quantity: $quantity
              ) {
                distanceKm
                transitTicks
                freightCostPerUnit
                totalFreightCost
                estimatedArrivalTick
              }
            }
            """,
            new { originBuildingId = sourceBuildingId, destinationCityId, resourceTypeId, quantity = 12m },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var estimate = result.GetProperty("data").GetProperty("getLogisticsCostEstimate");
        var distanceKm = estimate.GetProperty("distanceKm").GetDecimal();
        var transitTicks = estimate.GetProperty("transitTicks").GetInt64();
        var expectedTicks = Math.Max(1L, (long)Math.Ceiling((double)distanceKm / 500.0d));
        Assert.Equal(expectedTicks, transitTicks);
        Assert.True(estimate.GetProperty("totalFreightCost").GetDecimal() >= estimate.GetProperty("freightCostPerUnit").GetDecimal());
    }

    [Fact]
    public async Task GetCities_ReturnsUnlockStatusBasedOnOwnedBuildings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"cities-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        string unlockedCityName;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = db.Cities.First(c => c.Name == "Bratislava");
            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = playerId,
                Name = "Unlock Co",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
            };

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "HQ",
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                Level = 1,
            };

            db.Companies.Add(company);
            db.Buildings.Add(building);
            await db.SaveChangesAsync();
            unlockedCityName = city.Name;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query {
              getCities {
                name
                isUnlocked
                availableLandPlots
              }
            }
            """,
            token: token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var cities = result.GetProperty("data").GetProperty("getCities").EnumerateArray().ToList();
        var unlockedCity = cities.First(c => c.GetProperty("name").GetString() == unlockedCityName);
        Assert.True(unlockedCity.GetProperty("isUnlocked").GetBoolean());
        Assert.True(unlockedCity.GetProperty("availableLandPlots").GetInt32() >= 0);
    }

    [Fact]
    public async Task UnlockCity_ReturnsFalseWhenPlayerHasNoBuildingInCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"unlock-city-{Guid.NewGuid():N}@test.com");

        Guid cityId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            cityId = db.Cities.First(c => c.Name == "Prague").Id;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation($cityId: UUID!) {
              unlockCity(cityId: $cityId) {
                isSuccess
                cityId
                isUnlocked
                availableLandPlots
              }
            }
            """,
            new { cityId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var payload = result.GetProperty("data").GetProperty("unlockCity");
        Assert.True(payload.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(cityId.ToString(), payload.GetProperty("cityId").GetString());
        Assert.False(payload.GetProperty("isUnlocked").GetBoolean());
    }

    private static async Task<(Guid companyId, Guid routeId)> SeedRouteAsync(
        AppDbContext db,
        Guid ownerPlayerId,
        string suffix)
    {
        var bratislava = db.Cities.First(c => c.Name == "Bratislava");
        var prague = db.Cities.First(c => c.Name == "Prague");
        var wood = db.ResourceTypes.First(r => r.Slug == "wood");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownerPlayerId,
            Name = $"Route Co {suffix}",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
            Cash = 0m,
        };

        var sourceBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = bratislava.Id,
            Type = BuildingType.Factory,
            Name = $"Source {suffix}",
            Latitude = bratislava.Latitude,
            Longitude = bratislava.Longitude,
            Level = 1,
        };

        var destinationBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = prague.Id,
            Type = BuildingType.Factory,
            Name = $"Destination {suffix}",
            Latitude = prague.Latitude,
            Longitude = prague.Longitude,
            Level = 1,
        };

        var sourceUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = sourceBuilding.Id,
            UnitType = UnitType.B2BSales,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };

        var destinationUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = destinationBuilding.Id,
            UnitType = UnitType.Purchase,
            ResourceTypeId = wood.Id,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };

        var route = new InterCityTradeRoute
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SourceBuildingId = sourceBuilding.Id,
            SourceBuildingUnitId = sourceUnit.Id,
            DestinationBuildingId = destinationBuilding.Id,
            DestinationBuildingUnitId = destinationUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
            SourcingCostTotal = 20m,
            PricePerUnit = 5m,
            ScheduledDepartureTick = 1,
            ExpectedArrivalTick = 2,
            TransitTicks = 1,
            ShippingCostEstimate = 1m,
            ShippingCostActual = 0m,
            Status = TradeRouteStatus.InTransit,
            CreatedAtUtc = DateTime.UtcNow,
            DepartedAtUtc = DateTime.UtcNow,
        };

        db.Companies.Add(company);
        db.Buildings.AddRange(sourceBuilding, destinationBuilding);
        db.BuildingUnits.AddRange(sourceUnit, destinationUnit);
        db.InterCityTradeRoutes.Add(route);
        await db.SaveChangesAsync();

        return (company.Id, route.Id);
    }
}
