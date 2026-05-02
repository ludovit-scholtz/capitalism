using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Api.Tests;

/// <summary>
/// Integration tests for the buildingSupplyChain GraphQL query.
/// Covers health scoring, unit status derivation, link tracing, and authorization.
/// </summary>
public sealed class SupplyChainTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    private const string SharedJwtIssuer = "Capitalism";
    private const string SharedJwtAudience = "Capitalism";
    private const string SharedJwtSigningKey = "ChangeThisSigningKeyBeforeProduction123!";

    public SupplyChainTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Helpers

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
        => ExecuteGraphQlAsync(_client, query, variables, token);

    private async Task<(string Token, Guid PlayerId)> RegisterAsync(string email, string displayName = "SC Tester")
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });
        var payload = result.GetProperty("data").GetProperty("register");
        var token = payload.GetProperty("token").GetString()!;
        var playerId = Guid.Parse(payload.GetProperty("player").GetProperty("id").GetString()!);
        return (token, playerId);
    }

    private static string CreateToken(Guid userId, string email, string displayName)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SharedJwtSigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, displayName),
        };
        var jwtToken = new JwtSecurityToken(
            issuer: SharedJwtIssuer,
            audience: SharedJwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(jwtToken);
    }

    private static readonly string SupplyChainQuery = """
        query BuildingSupplyChain($buildingId: UUID!) {
          buildingSupplyChain(buildingId: $buildingId) {
            buildingId
            buildingName
            buildingType
            units {
              buildingUnitId
              unitType
              gridX
              gridY
              level
              status
              idleTicks
              fillPercent
              resourceOrProductName
              estimatedTransitCost
            }
            links {
              fromUnitId
              toUnitId
              direction
              estimatedTransitCost
            }
            healthScore
            healthReason
            criticalUnitIds
            warningUnitIds
          }
        }
        """;

    /// <summary>
    /// Seeds a factory building with specified units for a fresh player and returns key IDs.
    /// The building units are given history entries to simulate idle ticks.
    /// </summary>
    private async Task<(string Token, Guid BuildingId, List<Guid> UnitIds)> SeedFactoryWithUnitsAsync(
        string email,
        IEnumerable<(string UnitType, int GridX, int GridY, bool LinkRight, long IdleTicks)> unitDefs,
        long currentTick = 100L)
    {
        var (token, playerId) = await RegisterAsync(email);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure game state tick is at currentTick
        var gameState = await db.GameStates.FirstOrDefaultAsync();
        if (gameState is not null && gameState.CurrentTick < currentTick)
        {
            gameState.CurrentTick = currentTick;
            await db.SaveChangesAsync();
        }

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var player = await db.Players.FirstAsync(p => p.Id == playerId);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "SC Test Co",
            Cash = 500_000m,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "SC Test Factory",
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
        };
        db.Buildings.Add(building);

        var unitIds = new List<Guid>();
        foreach (var (unitType, gridX, gridY, linkRight, idleTicks) in unitDefs)
        {
            var unit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                UnitType = unitType,
                GridX = gridX,
                GridY = gridY,
                Level = 1,
                LinkRight = linkRight,
            };
            db.BuildingUnits.Add(unit);
            unitIds.Add(unit.Id);

            // Seed history to control idleTicks: lastActiveTick = currentTick - idleTicks
            var lastActiveTick = Math.Max(0, currentTick - idleTicks);
            db.BuildingUnitResourceHistories.Add(new BuildingUnitResourceHistory
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                BuildingUnitId = unit.Id,
                Tick = lastActiveTick,
                InflowQuantity = 1,
                OutflowQuantity = 0,
            });
        }

        await db.SaveChangesAsync();
        return (token, building.Id, unitIds);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task BuildingSupplyChain_WithoutAuth_ReturnsAuthError()
    {
        var result = await ExecuteGraphQlAsync(
            SupplyChainQuery,
            new { buildingId = Guid.NewGuid().ToString() });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BuildingSupplyChain_NonExistentBuilding_ReturnsBuildingNotFound()
    {
        var (token, _) = await RegisterAsync($"sc-auth-notfound-{Guid.NewGuid():N}@test.com");

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = Guid.NewGuid().ToString() }, token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var first = errors.EnumerateArray().First();
        Assert.Contains("BUILDING_NOT_FOUND", first.GetRawText());
    }

    [Fact]
    public async Task BuildingSupplyChain_BuildingOwnedByAnotherPlayer_ReturnsBuildingNotFound()
    {
        // Create a building owned by player A
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        // Register Player A via GraphQL to get proper password hash, then seed their building directly
        var (tokenA, playerAId) = await RegisterAsync($"sc-owner-a-{Guid.NewGuid():N}@test.com", "Owner A");
        var playerA = await db.Players.FirstAsync(p => p.Id == playerAId);
        var company = new Company { Id = Guid.NewGuid(), PlayerId = playerA.Id, Name = "Owner A Co", Cash = 500_000m };
        db.Companies.Add(company);
        var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Owner A Factory", Latitude = city.Latitude + 0.02, Longitude = city.Longitude + 0.02 };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        _ = tokenA; // tokenA not used — building was seeded via direct DB

        // Player B tries to query it
        var (tokenB, _) = await RegisterAsync($"sc-player-b-{Guid.NewGuid():N}@test.com", "Player B");
        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = building.Id.ToString() }, tokenB);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Contains("BUILDING_NOT_FOUND", errors.GetRawText());
    }

    [Fact]
    public async Task BuildingSupplyChain_NonFactoryBuilding_ReturnsInvalidBuildingType()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var email = $"sc-shop-{Guid.NewGuid():N}@test.com";
        var (token, shopPlayerId) = await RegisterAsync(email, "Shop Owner");
        var player = await db.Players.FirstAsync(p => p.Id == shopPlayerId);
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Shop Co", Cash = 500_000m };
        db.Companies.Add(company);
        var shop = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "Test Shop", Latitude = city.Latitude + 0.03, Longitude = city.Longitude + 0.03 };
        db.Buildings.Add(shop);
        await db.SaveChangesAsync();
        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = shop.Id.ToString() }, token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Contains("INVALID_BUILDING_TYPE", errors.GetRawText());
    }

    #endregion

    #region Health Score Tests

    [Fact]
    public async Task BuildingSupplyChain_AllUnitsActive_ReturnsGreenHealth()
    {
        var email = $"sc-green-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, _) = await SeedFactoryWithUnitsAsync(
            email,
            new[]
            {
                ("PURCHASE", 0, 0, true, 0L),
                ("MANUFACTURING", 1, 0, false, 0L),
            });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        Assert.Equal("GREEN", diagram.GetProperty("healthScore").GetString());
        Assert.Empty(diagram.GetProperty("criticalUnitIds").EnumerateArray());
        Assert.Empty(diagram.GetProperty("warningUnitIds").EnumerateArray());
    }

    [Fact]
    public async Task BuildingSupplyChain_UnitIdleMoreThan5Ticks_ReturnsYellowHealth()
    {
        var email = $"sc-yellow-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, unitIds) = await SeedFactoryWithUnitsAsync(
            email,
            new[]
            {
                ("PURCHASE", 0, 0, false, 7L),  // idle > 5 ticks → YELLOW
            });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        Assert.Equal("YELLOW", diagram.GetProperty("healthScore").GetString());
        Assert.NotEmpty(diagram.GetProperty("warningUnitIds").EnumerateArray());
        Assert.Empty(diagram.GetProperty("criticalUnitIds").EnumerateArray());
    }

    [Fact]
    public async Task BuildingSupplyChain_UnitIdleMoreThan20Ticks_ReturnsRedHealth()
    {
        var email = $"sc-red-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, unitIds) = await SeedFactoryWithUnitsAsync(
            email,
            new[]
            {
                ("PURCHASE", 0, 0, false, 25L),  // idle > 20 ticks → RED
            });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        Assert.Equal("RED", diagram.GetProperty("healthScore").GetString());
        Assert.NotEmpty(diagram.GetProperty("criticalUnitIds").EnumerateArray());
    }

    #endregion

    #region Link Tracing Tests

    [Fact]
    public async Task BuildingSupplyChain_LinkRight_TracesCorrectLink()
    {
        var email = $"sc-links-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, unitIds) = await SeedFactoryWithUnitsAsync(
            email,
            new[]
            {
                ("PURCHASE", 0, 0, true, 0L),    // linkRight=true → points to (1,0)
                ("MANUFACTURING", 1, 0, false, 0L),
            });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        var links = diagram.GetProperty("links").EnumerateArray().ToList();

        Assert.Single(links);
        var link = links[0];
        Assert.Equal("RIGHT", link.GetProperty("direction").GetString());
        Assert.Equal(unitIds[0].ToString(), link.GetProperty("fromUnitId").GetString());
        Assert.Equal(unitIds[1].ToString(), link.GetProperty("toUnitId").GetString());
    }

    #endregion

    #region Unit Info Tests

    [Fact]
    public async Task BuildingSupplyChain_EmptyFactory_ReturnsNoUnitsAndGreenHealth()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var email = $"sc-empty-{Guid.NewGuid():N}@test.com";
        var (token, emptyPlayerId) = await RegisterAsync(email, "Empty Factory Tester");
        var player = await db.Players.FirstAsync(p => p.Id == emptyPlayerId);
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Empty Co", Cash = 500_000m };
        db.Companies.Add(company);
        var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Empty Factory", Latitude = city.Latitude + 0.04, Longitude = city.Longitude + 0.04 };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = building.Id.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        Assert.Equal("GREEN", diagram.GetProperty("healthScore").GetString());
        Assert.Equal(0, diagram.GetProperty("units").GetArrayLength());
        Assert.Equal(0, diagram.GetProperty("links").GetArrayLength());
    }

    [Fact]
    public async Task BuildingSupplyChain_ReturnsCorrectBuildingMetadata()
    {
        var email = $"sc-meta-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, _) = await SeedFactoryWithUnitsAsync(
            email,
            new[] { ("PURCHASE", 0, 0, false, 0L) });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");
        Assert.Equal(buildingId.ToString(), diagram.GetProperty("buildingId").GetString());
        Assert.Equal("SC Test Factory", diagram.GetProperty("buildingName").GetString());
        Assert.Equal("FACTORY", diagram.GetProperty("buildingType").GetString());
    }

    [Fact]
    public async Task BuildingSupplyChain_MixedIdleStates_RedTakesPrecedenceOverYellow()
    {
        var email = $"sc-mixed-{Guid.NewGuid():N}@test.com";
        var (token, buildingId, _) = await SeedFactoryWithUnitsAsync(
            email,
            new[]
            {
                ("PURCHASE", 0, 0, false, 7L),   // YELLOW: idle > 5
                ("MANUFACTURING", 1, 0, false, 25L), // RED: idle > 20
            });

        var result = await ExecuteGraphQlAsync(SupplyChainQuery, new { buildingId = buildingId.ToString() }, token);

        Assert.False(result.TryGetProperty("errors", out _), result.GetRawText());
        var diagram = result.GetProperty("data").GetProperty("buildingSupplyChain");

        // RED takes precedence when any unit exceeds 20 ticks
        Assert.Equal("RED", diagram.GetProperty("healthScore").GetString());
        Assert.NotEmpty(diagram.GetProperty("criticalUnitIds").EnumerateArray());
        Assert.NotEmpty(diagram.GetProperty("warningUnitIds").EnumerateArray());
    }

    #endregion
}
