using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class TradeRouteQueryTests
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
            new { input = new { email, displayName = "Route Tester", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<(Guid companyId, Guid routeId)> SeedRouteAsync(
        AppDbContext db,
        Guid ownerPlayerId,
        string suffix)
    {
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

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

    private static async Task<(Guid fromBuildingId, Guid toBuildingId, Guid productTypeId)> SeedShippingQuoteAsync(
        AppDbContext db,
        Guid ownerPlayerId)
    {
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var chair = await db.ProductTypes.FirstAsync(p => p.Slug == "wooden-chair");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = ownerPlayerId,
            Name = $"Shipping Quote Co {Guid.NewGuid():N}",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };

        var fromBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = bratislava.Id,
            Type = BuildingType.Factory,
            Name = "Shipping Source",
            Latitude = bratislava.Latitude + 0.02d,
            Longitude = bratislava.Longitude + 0.02d,
            Level = 1,
        };

        var toBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = prague.Id,
            Type = BuildingType.SalesShop,
            Name = "Shipping Destination",
            Latitude = prague.Latitude - 0.02d,
            Longitude = prague.Longitude - 0.02d,
            Level = 1,
        };

        db.Companies.Add(company);
        db.Buildings.AddRange(fromBuilding, toBuilding);
        await db.SaveChangesAsync();

        return (fromBuilding.Id, toBuilding.Id, chair.Id);
    }

    [Fact]
    public async Task MyTradeRoutes_WithoutCompanyId_ReturnsOnlyCurrentPlayersRoutes()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownToken = await RegisterAndGetTokenAsync(client, $"route-owner-{Guid.NewGuid():N}@test.com");
        var otherToken = await RegisterAndGetTokenAsync(client, $"route-other-{Guid.NewGuid():N}@test.com");

        var ownPlayerId = await GetPlayerIdAsync(client, ownToken);
        var otherPlayerId = await GetPlayerIdAsync(client, otherToken);

        Guid ownRouteId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (var _, ownRouteId) = await SeedRouteAsync(db, ownPlayerId, "own");
            await SeedRouteAsync(db, otherPlayerId, "other");
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query { myTradeRoutes { id companyId status } }",
            token: ownToken);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());

        var routes = result.GetProperty("data").GetProperty("myTradeRoutes");
        Assert.Equal(1, routes.GetArrayLength());
        Assert.Equal(ownRouteId.ToString(), routes[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task MyTradeRoutes_WithCompanyId_FiltersToRequestedCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"route-filter-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        Guid companyAId;
        Guid routeAId;
        Guid companyBId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (companyAId, routeAId) = await SeedRouteAsync(db, playerId, "A");
            (companyBId, _) = await SeedRouteAsync(db, playerId, "B");
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query($companyId: UUID) { myTradeRoutes(companyId: $companyId) { id companyId } }",
            new { companyId = companyAId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());

        var routes = result.GetProperty("data").GetProperty("myTradeRoutes");
        Assert.Equal(1, routes.GetArrayLength());
        Assert.Equal(routeAId.ToString(), routes[0].GetProperty("id").GetString());
        Assert.Equal(companyAId.ToString(), routes[0].GetProperty("companyId").GetString());
        Assert.NotEqual(companyBId.ToString(), routes[0].GetProperty("companyId").GetString());
    }

    [Fact]
    public async Task ShippingCostQuote_Authenticated_ReturnsPositiveQuote()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"shipping-quote-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        Guid fromBuildingId;
        Guid toBuildingId;
        Guid productTypeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (fromBuildingId, toBuildingId, productTypeId) = await SeedShippingQuoteAsync(db, playerId);
        }

        var result = await ExecuteGraphQlAsync(
            client,
            @"query ShippingCostQuote($fromBuildingId: UUID!, $toBuildingId: UUID!, $productTypeId: UUID!, $quantity: Decimal!) {
                shippingCostQuote(
                    fromBuildingId: $fromBuildingId
                    toBuildingId: $toBuildingId
                    productTypeId: $productTypeId
                    quantity: $quantity
                ) {
                    distanceKm
                    weightKgPerUnit
                    quantity
                    costPerUnit
                    totalCost
                    currencyCode
                }
            }",
            new { fromBuildingId, toBuildingId, productTypeId, quantity = 5m },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var quote = result.GetProperty("data").GetProperty("shippingCostQuote");

        Assert.True(quote.GetProperty("distanceKm").GetDecimal() > 0m);
        Assert.True(quote.GetProperty("weightKgPerUnit").GetDecimal() > 0m);
        Assert.Equal(5m, quote.GetProperty("quantity").GetDecimal());
        Assert.True(quote.GetProperty("costPerUnit").GetDecimal() > 0m);
        Assert.True(quote.GetProperty("totalCost").GetDecimal() > quote.GetProperty("costPerUnit").GetDecimal());
        Assert.Equal("CZK", quote.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task ShippingCostQuote_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            @"query ShippingCostQuote($fromBuildingId: UUID!, $toBuildingId: UUID!, $productTypeId: UUID!, $quantity: Decimal!) {
                shippingCostQuote(
                    fromBuildingId: $fromBuildingId
                    toBuildingId: $toBuildingId
                    productTypeId: $productTypeId
                    quantity: $quantity
                ) {
                    totalCost
                }
            }",
            new { fromBuildingId = Guid.NewGuid(), toBuildingId = Guid.NewGuid(), productTypeId = Guid.NewGuid(), quantity = 1m });

        Assert.True(result.TryGetProperty("errors", out var errors), result.ToString());
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.StartsWith("AUTH_", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShippingCostQuote_UnknownBuilding_ReturnsBuildingNotFound()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, $"shipping-unknown-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        Guid toBuildingId;
        Guid productTypeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (_, toBuildingId, productTypeId) = await SeedShippingQuoteAsync(db, playerId);
        }

        var result = await ExecuteGraphQlAsync(
            client,
            @"query ShippingCostQuote($fromBuildingId: UUID!, $toBuildingId: UUID!, $productTypeId: UUID!, $quantity: Decimal!) {
                shippingCostQuote(
                    fromBuildingId: $fromBuildingId
                    toBuildingId: $toBuildingId
                    productTypeId: $productTypeId
                    quantity: $quantity
                ) {
                    totalCost
                }
            }",
            new { fromBuildingId = Guid.NewGuid(), toBuildingId, productTypeId, quantity = 1m },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors), result.ToString());
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BUILDING_NOT_FOUND", code);
    }
}
