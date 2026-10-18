using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class WeatherServiceTests
{
    /// <summary>Picks cities and starts from an empty forecast table (the host may already have seeded one).</summary>
    private static async Task<List<Guid>> TakeCitiesAsync(AppDbContext db, int count)
    {
        db.CityWeatherForecasts.RemoveRange(await db.CityWeatherForecasts.ToListAsync());
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return await db.Cities.AsNoTracking().OrderBy(c => c.Name).Select(c => c.Id).Take(count).ToListAsync();
    }

    [Fact]
    public async Task AdvanceForecasts_SeedsAWindowForEveryCityOnTheFirstCall()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = await TakeCitiesAsync(db, 3);

        await WeatherService.AdvanceForecastsAsync(db, cities, currentTick: 100);

        foreach (var cityId in cities)
        {
            var rows = await db.CityWeatherForecasts.AsNoTracking().Where(f => f.CityId == cityId).OrderBy(f => f.Tick).ToListAsync();
            Assert.Equal(WeatherService.ForecastWindow, rows.Count);
            Assert.Equal(100, rows[0].Tick);
            Assert.Equal(100 + WeatherService.ForecastWindow - 1, rows[^1].Tick);
        }
    }

    [Fact]
    public async Task AdvanceForecasts_EachTickPrunesThePastAndExtendsByOneRow_MatchingAFreshlySeededWindow()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = await TakeCitiesAsync(db, 3);
        const long start = 200;
        const int advances = 7;

        for (var i = 0; i < advances; i++)
        {
            await WeatherService.AdvanceForecastsAsync(db, cities, start + i);
            db.ChangeTracker.Clear(); // as in production: each tick has its own context
        }

        foreach (var cityId in cities)
        {
            var rows = await db.CityWeatherForecasts.AsNoTracking().Where(f => f.CityId == cityId).OrderBy(f => f.Tick).ToListAsync();
            var current = start + advances - 1;
            Assert.All(rows, row => Assert.True(row.Tick >= current));
            Assert.Equal(rows.Count, rows.Select(r => r.Tick).Distinct().Count());
            Assert.Equal(current + WeatherService.ForecastWindow - 1, rows[^1].Tick);
            // The incremental wind chain equals one long deterministic seed of the same city.
            var expected = WeatherService.SeedForecast(cityId, start, WeatherService.ForecastWindow + advances - 1)
                .ToDictionary(r => r.Tick);
            foreach (var row in rows)
            {
                Assert.Equal(expected[row.Tick].WindPercent, row.WindPercent);
                Assert.Equal(expected[row.Tick].SolarPercent, row.SolarPercent);
            }
        }
    }

    [Fact]
    public async Task AdvanceForecasts_AddsAWindowOnlyForACityThatHasNoRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cities = await TakeCitiesAsync(db, 2);
        await WeatherService.AdvanceForecastsAsync(db, [cities[0]], 300);
        db.ChangeTracker.Clear();

        await WeatherService.AdvanceForecastsAsync(db, cities, 301);

        var first = await db.CityWeatherForecasts.AsNoTracking().Where(f => f.CityId == cities[0]).ToListAsync();
        var second = await db.CityWeatherForecasts.AsNoTracking().Where(f => f.CityId == cities[1]).ToListAsync();
        Assert.Equal(WeatherService.ForecastWindow, first.Count); // pruned one, extended one
        Assert.Equal(301, first.Min(r => r.Tick));
        Assert.Equal(WeatherService.ForecastWindow, second.Count); // freshly seeded from the current tick
        Assert.Equal(301, second.Min(r => r.Tick));
    }
}
