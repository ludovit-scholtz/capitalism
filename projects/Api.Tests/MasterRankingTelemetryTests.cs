using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests;

/// <summary>
/// Tests for the master ranking telemetry integration.
/// Verifies that bounty events are fired for the expected game activities.
/// Uses a mock <see cref="IMasterRankingTelemetryService"/> so tests stay deterministic without calling MasterApi.
/// </summary>
public sealed class MasterRankingTelemetryTests
{
    #region Mock telemetry service

    /// <summary>Captures telemetry calls for assertions.</summary>
    private sealed class CapturingTelemetryService : IMasterRankingTelemetryService
    {
        public List<(string EventType, string PlayerEmail, string? UniqueScopeKey)> Calls { get; } = [];

        public Task ReportEventAsync(
            string eventType,
            string playerEmail,
            string? uniqueScopeKey = null,
            string? externalEventId = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((eventType, playerEmail, uniqueScopeKey));
            return Task.CompletedTask;
        }
    }

    #endregion

    #region Factory with capturing telemetry

    private sealed class TelemetryAwareFactory(CapturingTelemetryService capturing)
        : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ApplyBaseConfiguration(builder);
            builder.ConfigureServices(services =>
            {
                // Remove the default (live) telemetry service and register the capturing mock.
                var descriptor = services.FirstOrDefault(
                    d => d.ServiceType == typeof(IMasterRankingTelemetryService));
                if (descriptor is not null)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<IMasterRankingTelemetryService>(_ => capturing);
            });
        }
    }

    #endregion

    #region GraphQL helpers

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client, string email = "telemetry-test@example.com",
        string displayName = "Telemetry Tester", string password = "TestPass123!")
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    #endregion

    #region LOGIN_TO_GAME

    [Fact]
    public async Task GetMe_AfterLogin_FiresLoginToGameTelemetry()
    {
        var capturing = new CapturingTelemetryService();
        await using var factory = new TelemetryAwareFactory(capturing);
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "login-telemetry@example.com");

        // Act: call the me query while authenticated.
        await ExecuteGraphQlAsync(client, "{ me { id email } }", token: token);

        // Assert: LOGIN_TO_GAME was fired for the authenticated player's email.
        Assert.Contains(capturing.Calls,
            c => c.EventType == MasterRankingBountyCodes.LoginToGame
              && c.PlayerEmail == "login-telemetry@example.com");
    }

    [Fact]
    public async Task GetMe_UnauthenticatedCall_DoesNotFireLoginTelemetry()
    {
        var capturing = new CapturingTelemetryService();
        await using var factory = new TelemetryAwareFactory(capturing);
        var client = factory.CreateClient();

        // Act: call me without auth — HotChocolate returns an auth error, telemetry should NOT fire.
        await ExecuteGraphQlAsync(client, "{ me { id } }");

        Assert.Empty(capturing.Calls.Where(c => c.EventType == MasterRankingBountyCodes.LoginToGame));
    }

    [Fact]
    public async Task GetMe_ScopeKey_ContainsLoginToGamePrefixAndEmail()
    {
        var capturing = new CapturingTelemetryService();
        await using var factory = new TelemetryAwareFactory(capturing);
        var client = factory.CreateClient();

        const string email = "scope-key-test@example.com";
        var token = await RegisterAndGetTokenAsync(client, email);

        await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);

        var loginCall = capturing.Calls.FirstOrDefault(c => c.EventType == MasterRankingBountyCodes.LoginToGame);
        Assert.NotEqual(default, loginCall);
        Assert.NotNull(loginCall.UniqueScopeKey);
        Assert.StartsWith($"LOGIN_TO_GAME:{email}:", loginCall.UniqueScopeKey);
    }

    #endregion

    #region TelemetryBountyPhase — MANUFACTURER

    [Fact]
    public async Task TelemetryBountyPhase_ManufacturerActivity_FiresManufacturerEvent()
    {
        var capturing = new CapturingTelemetryService();
        await using var isolatedFactory = new TelemetryAwareFactory(capturing);

        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = "manufacturer@example.com",
            DisplayName = "Manufacturer", PasswordHash = "hashed", Role = PlayerRole.Player
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Manufacturer Co" };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Test Factory", Level = 1
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var unitId = Guid.NewGuid();
        var history = new BuildingUnitResourceHistory
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = unitId,
            Tick = gameState.CurrentTick, ProducedQuantity = 5m
        };

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [building.Id] = building },
            BuildingsByType = new Dictionary<string, List<Building>>
                { [BuildingType.Factory] = [building] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };
        context.NewUnitResourceHistories.Add(history);

        var phase = new TelemetryBountyPhase(
            capturing,
            scope.ServiceProvider.GetRequiredService<IOptions<Api.Configuration.MasterServerRegistrationOptions>>());
        await phase.ProcessAsync(context);

        Assert.Contains(capturing.Calls,
            c => c.EventType == MasterRankingBountyCodes.Manufacturer
              && c.PlayerEmail == "manufacturer@example.com");
    }

    [Fact]
    public async Task TelemetryBountyPhase_NoManufacturingOutput_DoesNotFireManufacturerEvent()
    {
        var capturing = new CapturingTelemetryService();
        await using var isolatedFactory = new TelemetryAwareFactory(capturing);

        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = "idle-factory@example.com",
            DisplayName = "Idle", PasswordHash = "hashed", Role = PlayerRole.Player
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Idle Co" };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.Factory, Name = "Idle Factory", Level = 1
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        // History with 0 produced quantity — should NOT fire MANUFACTURER.
        var history = new BuildingUnitResourceHistory
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = Guid.NewGuid(),
            Tick = gameState.CurrentTick, ProducedQuantity = 0m
        };

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [building.Id] = building },
            BuildingsByType = new Dictionary<string, List<Building>>
                { [BuildingType.Factory] = [building] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };
        context.NewUnitResourceHistories.Add(history);

        var phase = new TelemetryBountyPhase(
            capturing,
            scope.ServiceProvider.GetRequiredService<IOptions<Api.Configuration.MasterServerRegistrationOptions>>());
        await phase.ProcessAsync(context);

        Assert.DoesNotContain(capturing.Calls,
            c => c.EventType == MasterRankingBountyCodes.Manufacturer
              && c.PlayerEmail == "idle-factory@example.com");
    }

    #endregion

    #region TelemetryBountyPhase — WHOLESALER

    [Fact]
    public async Task TelemetryBountyPhase_WholesalerActivity_FiresWholesalerEvent()
    {
        var capturing = new CapturingTelemetryService();
        await using var isolatedFactory = new TelemetryAwareFactory(capturing);

        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = "wholesaler@example.com",
            DisplayName = "Wholesaler", PasswordHash = "hashed", Role = PlayerRole.Player
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Wholesaler Co" };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id,
            Type = BuildingType.SalesShop, Name = "Test Shop", Level = 1
        };
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.PublicSales,
            GridX = 0, GridY = 0, Level = 1
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);
        db.BuildingUnits.Add(unit);

        var productType = await db.ProductTypes.FirstAsync();
        var salesRecord = new PublicSalesRecord
        {
            Id = Guid.NewGuid(),
            BuildingUnitId = unit.Id,
            BuildingId = building.Id,
            CompanyId = company.Id,
            CityId = city.Id,
            ProductTypeId = productType.Id,
            Tick = gameState.CurrentTick,
            QuantitySold = 10m,
            PricePerUnit = 50m,
            Revenue = 500m
        };
        db.PublicSalesRecords.Add(salesRecord);
        await db.SaveChangesAsync();

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [building.Id] = building },
            BuildingsByType = new Dictionary<string, List<Building>>
                { [BuildingType.SalesShop] = [building] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };

        var phase = new TelemetryBountyPhase(
            capturing,
            scope.ServiceProvider.GetRequiredService<IOptions<Api.Configuration.MasterServerRegistrationOptions>>());
        await phase.ProcessAsync(context);

        Assert.Contains(capturing.Calls,
            c => c.EventType == MasterRankingBountyCodes.Wholesaler
              && c.PlayerEmail == "wholesaler@example.com");
    }

    #endregion

    #region NoOpTelemetryService (unconfigured master server)

    [Fact]
    public async Task GetMe_WhenMasterServerNotConfigured_TelemetryIsNoOpAndQuerySucceeds()
    {
        // The default factory has MasterServer:RegistrationEnabled=false → IsConfigured()==false.
        // The live MasterRankingTelemetryService skips when not configured — no exceptions should occur.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "noop-test@example.com");

        var result = await ExecuteGraphQlAsync(client, "{ me { id email } }", token: token);
        var me = result.GetProperty("data").GetProperty("me");

        Assert.Equal("noop-test@example.com", me.GetProperty("email").GetString());
    }

    #endregion
}
