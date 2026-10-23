using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shared.Economy;

namespace Api.Tests;

/// <summary>Moves the shared game state onto a tick on which the telemetry phase runs (it only runs every few ticks, #646).</summary>
internal static class TickCadenceTestExtensions
{
    public static async Task<GameState> AlignedToTelemetryCadenceAsync(this AppDbContext db)
    {
        var gameState = await db.GameStates.FirstAsync();
        gameState.CurrentTick = TickCadence.TelemetryBountyTicks * 10;
        return gameState;
    }
}

/// <summary>
/// Cadence of the expensive tick phases, persisted timings and the telemetry catch-up window (#646). A cadence may change when a phase
/// runs, never what it decides.
/// </summary>
public sealed class TickCadenceTests
{
    [Theory]
    [InlineData(0, 6, true)]
    [InlineData(5, 6, false)]
    [InlineData(6, 6, true)]
    [InlineData(7, 6, false)]
    [InlineData(12, 6, true)]
    [InlineData(7, 1, true)]
    [InlineData(7, 0, true)]
    public void IsDue_RunsOnlyOnMultiplesOfTheCadence(long tick, int cadence, bool expected)
        => Assert.Equal(expected, TickCadence.IsDue(tick, cadence));

    [Fact]
    public void CityExpansionUnlockPhase_UsesTheSharedCadence()
        => Assert.Equal(TickCadence.CityExpansionUnlockTicks, CityExpansionUnlockPhase.CadenceTicks);

    // ── Weather ───────────────────────────────────────────────────────────────

    private static async Task<List<Guid>> TakeEmptyForecastCitiesAsync(AppDbContext db, int count)
    {
        db.CityWeatherForecasts.RemoveRange(await db.CityWeatherForecasts.ToListAsync());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await db.Cities.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Id).Take(count).ToListAsync();
    }

    [Fact]
    public async Task AdvanceForecasts_OnACadence_KeepsTheFullHorizonOnEveryTickAndMatchesAPerTickRoll()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = await TakeEmptyForecastCitiesAsync(db, 3);
        const long start = 300;
        var cadence = TickCadence.WeatherUpdateTicks;

        for (var tick = start; tick < start + cadence * 4; tick++)
        {
            if (TickCadence.IsDue(tick - start, cadence))
            {
                await WeatherService.AdvanceForecastsAsync(db, cities, tick, cadence);
                db.ChangeTracker.Clear();
            }

            foreach (var cityId in cities)
            {
                var ahead = await db.CityWeatherForecasts.AsNoTracking().CountAsync(f => f.CityId == cityId && f.Tick >= tick);
                Assert.True(ahead >= WeatherService.ForecastWindow, $"tick {tick}: only {ahead} forecast ticks ahead");
                Assert.True(await db.CityWeatherForecasts.AnyAsync(f => f.CityId == cityId && f.Tick == tick), $"tick {tick}: no row for the current tick");
            }
        }

        foreach (var cityId in cities)
        {
            var rows = await db.CityWeatherForecasts.AsNoTracking().Where(f => f.CityId == cityId).ToListAsync();
            Assert.Equal(rows.Count, rows.Select(r => r.Tick).Distinct().Count());
            var expected = WeatherService.SeedForecast(cityId, start, (int)(rows.Max(r => r.Tick) - start + 1)).ToDictionary(r => r.Tick);
            Assert.All(rows, row =>
            {
                Assert.Equal(expected[row.Tick].WindPercent, row.WindPercent);
                Assert.Equal(expected[row.Tick].SolarPercent, row.SolarPercent);
            });
        }
    }

    [Fact]
    public async Task WeatherUpdatePhase_OnASkippedTick_LeavesTheForecastTableUntouched()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = await TakeEmptyForecastCitiesAsync(db, 2);
        var gameState = await db.GameStates.FirstAsync();
        var cityMap = await db.Cities.AsNoTracking().Where(c => cities.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        gameState.CurrentTick = TickCadence.WeatherUpdateTicks * 5 + 1;
        await new WeatherUpdatePhase().ProcessAsync(new TickContext { Db = db, GameState = gameState, CitiesById = cityMap });
        Assert.Empty(await db.CityWeatherForecasts.AsNoTracking().ToListAsync());

        gameState.CurrentTick = TickCadence.WeatherUpdateTicks * 5;
        await new WeatherUpdatePhase().ProcessAsync(new TickContext { Db = db, GameState = gameState, CitiesById = cityMap });
        Assert.NotEmpty(await db.CityWeatherForecasts.AsNoTracking().ToListAsync());
    }

    // ── Telemetry ─────────────────────────────────────────────────────────────

    private sealed class CapturingTelemetry : IMasterRankingTelemetryService
    {
        public List<string> EventTypes { get; } = [];

        public Task ReportEventAsync(string eventType, string playerEmail, string? uniqueScopeKey = null, string? externalEventId = null, CancellationToken cancellationToken = default)
        {
            EventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }

    private sealed class TelemetryFactory(CapturingTelemetry capturing) : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ApplyBaseConfiguration(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IMasterRankingTelemetryService>();
                services.AddScoped<IMasterRankingTelemetryService>(_ => capturing);
            });
        }
    }

    /// <summary>Runs the telemetry phase at <paramref name="currentTick"/> for a shop that sold at <paramref name="saleTick"/>.</summary>
    private static async Task<List<string>> RunWholesalerScenarioAsync(long currentTick, long saleTick)
    {
        var capturing = new CapturingTelemetry();
        await using var factory = new TelemetryFactory(capturing);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player { Id = Guid.NewGuid(), Email = "cadence@example.com", DisplayName = "Cadence", PasswordHash = "hashed", Role = PlayerRole.Player };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Cadence Co" };
        var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = "Shop", Level = 1 };
        var unit = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.PublicSales, GridX = 0, GridY = 0, Level = 1 };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);
        db.BuildingUnits.Add(unit);
        db.PublicSalesRecords.Add(new PublicSalesRecord
        {
            Id = Guid.NewGuid(), BuildingUnitId = unit.Id, BuildingId = building.Id, CompanyId = company.Id, CityId = city.Id,
            ProductTypeId = (await db.ProductTypes.FirstAsync()).Id, Tick = saleTick, QuantitySold = 10m, PricePerUnit = 50m, Revenue = 500m,
        });
        await db.SaveChangesAsync();

        gameState.CurrentTick = currentTick;
        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [building.Id] = building },
            BuildingsByType = new Dictionary<string, List<Building>> { [BuildingType.SalesShop] = [building] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };
        await new TelemetryBountyPhase(capturing, scope.ServiceProvider.GetRequiredService<IOptions<MasterServerRegistrationOptions>>()).ProcessAsync(context);
        return capturing.EventTypes;
    }

    [Fact]
    public async Task TelemetryBountyPhase_OnASkippedTick_ReportsNothing()
        => Assert.Empty(await RunWholesalerScenarioAsync(currentTick: 61, saleTick: 61));

    [Fact]
    public async Task TelemetryBountyPhase_OnARunTick_SeesASaleFromASkippedTickInTheWindow()
    {
        // Run tick 66 covers ticks 61..66 (the previous run was tick 60); a sale on skipped tick 63 still counts.
        Assert.Contains(MasterRankingBountyCodes.Wholesaler, await RunWholesalerScenarioAsync(currentTick: 66, saleTick: 63));
        Assert.Contains(MasterRankingBountyCodes.Wholesaler, await RunWholesalerScenarioAsync(currentTick: 66, saleTick: 61));
        Assert.Contains(MasterRankingBountyCodes.Wholesaler, await RunWholesalerScenarioAsync(currentTick: 66, saleTick: 66));
    }

    [Fact]
    public async Task TelemetryBountyPhase_IgnoresASaleThatAnEarlierRunAlreadyCovered()
        => Assert.DoesNotContain(MasterRankingBountyCodes.Wholesaler, await RunWholesalerScenarioAsync(currentTick: 66, saleTick: 60));

    // ── Persisted timings ─────────────────────────────────────────────────────

    private static readonly IReadOnlyDictionary<string, long> SamplePhases = new Dictionary<string, long> { ["PublicSales"] = 120, ["(save changes)"] = 40 };

    [Fact]
    public async Task TickTimingStore_KeepsOnlyTheLastWindowAndRoundTripsThePhases()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var store = new TickTimingStore(db);

        for (var tick = 1; tick <= TickTimingStore.RetainedTicks + 5; tick++)
        {
            await store.SaveAsync(tick, 100 + tick, SamplePhases);
        }

        var loaded = await store.LoadRecentAsync();
        Assert.Equal(TickTimingStore.RetainedTicks, loaded.Count);
        Assert.Equal(6, loaded[0].Tick);
        Assert.Equal(TickTimingStore.RetainedTicks + 5, loaded[^1].Tick);
        Assert.Equal(100 + TickTimingStore.RetainedTicks + 5, loaded[^1].TotalMs);
        Assert.Equal(120, loaded[^1].PhaseMs["PublicSales"]);
    }

    [Fact]
    public async Task TickTimingStore_SavingTheSameTickTwice_UpdatesTheRow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var store = new TickTimingStore(scope.ServiceProvider.GetRequiredService<AppDbContext>());

        await store.SaveAsync(900, 10, SamplePhases);
        await store.SaveAsync(900, 25, SamplePhases);

        var loaded = await store.LoadRecentAsync();
        Assert.Equal(25, Assert.Single(loaded, r => r.Tick == 900).TotalMs);
    }

    [Fact]
    public void TickTimingRecorder_Restore_PutsPersistedHistoryBeforeTicksRecordedSinceStartup()
    {
        var recorder = new TickTimingRecorder(capacity: 5);
        recorder.Record(10, 50, SamplePhases);

        recorder.Restore(
        [
            (7, 70, SamplePhases), (8, 80, SamplePhases), (9, 90, SamplePhases), (10, 999, SamplePhases),
        ]);

        var summary = recorder.Summarize();
        Assert.Equal(4, summary.SampledTicks);
        Assert.Equal(10, summary.LastTick);
        Assert.Equal(50, summary.LastTotalMs); // the live sample wins over a stale persisted one for the same tick
        Assert.Equal(90, summary.MaxTotalMs);
    }

    [Fact]
    public async Task TickProcessor_PersistsTheTimingsOfEachTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var recorder = new TickTimingRecorder();
        var processor = new TickProcessor(db, [], NullLogger<TickProcessor>.Instance, recorder, new TickTimingStore(db));

        await processor.ProcessTickAsync();

        var tick = (await db.GameStates.AsNoTracking().FirstAsync()).CurrentTick;
        var stored = await db.TickTimingRecords.AsNoTracking().SingleAsync(r => r.Tick == tick);
        Assert.Contains(TickProcessor.ContextLoadEntryName, stored.PhaseMsJson);
        Assert.Contains(TickProcessor.SaveEntryName, stored.PhaseMsJson);
        Assert.Equal(tick, recorder.Summarize().LastTick);
    }
}
