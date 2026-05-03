using Api.Data;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Verifies that Berlin and Warsaw are correctly seeded with currency codes,
/// resource abundances, and building lots as part of the multi-city expansion.
/// These are non-archived tests that run in CI.
/// </summary>
public sealed class CitySeederTests
{
    [Fact]
    public async Task BerlinAndWarsaw_AreSeededWithCorrectCurrencyCodes()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cityNames = await db.Cities.Select(c => c.Name).ToListAsync();

        Assert.Contains("Berlin", cityNames);
        Assert.Contains("Warsaw", cityNames);

        var berlin = await db.Cities.FirstAsync(c => c.Name == "Berlin");
        Assert.Equal("EUR", berlin.CurrencyCode);
        Assert.Equal("DE", berlin.CountryCode);
        Assert.True(berlin.Population > 0, "Berlin must have a positive population.");

        var warsaw = await db.Cities.FirstAsync(c => c.Name == "Warsaw");
        Assert.Equal("PLN", warsaw.CurrencyCode);
        Assert.Equal("PL", warsaw.CountryCode);
        Assert.True(warsaw.Population > 0, "Warsaw must have a positive population.");
    }

    [Fact]
    public async Task BerlinAndWarsaw_HaveResourceAbundances()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var berlinResources = await db.CityResources
            .Include(cr => cr.ResourceType)
            .Where(cr => cr.City!.Name == "Berlin")
            .ToListAsync();

        Assert.True(berlinResources.Count >= 3,
            $"Berlin must have at least 3 resource abundances, got {berlinResources.Count}.");
        Assert.Contains(berlinResources, r => r.ResourceType!.Slug == "coal");
        Assert.Contains(berlinResources, r => r.ResourceType!.Slug == "iron-ore");
        Assert.Contains(berlinResources, r => r.ResourceType!.Slug == "silicon");

        var warsawResources = await db.CityResources
            .Include(cr => cr.ResourceType)
            .Where(cr => cr.City!.Name == "Warsaw")
            .ToListAsync();

        Assert.True(warsawResources.Count >= 3,
            $"Warsaw must have at least 3 resource abundances, got {warsawResources.Count}.");
        Assert.Contains(warsawResources, r => r.ResourceType!.Slug == "grain");
        Assert.Contains(warsawResources, r => r.ResourceType!.Slug == "wood");
        Assert.Contains(warsawResources, r => r.ResourceType!.Slug == "coal");
    }

    [Fact]
    public async Task BerlinAndWarsaw_HavePositiveSalaryRates()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var berlin = await db.Cities.FirstAsync(c => c.Name == "Berlin");
        Assert.True(berlin.BaseSalaryPerManhour > 0m,
            $"Berlin BaseSalaryPerManhour must be positive, got {berlin.BaseSalaryPerManhour}.");

        var warsaw = await db.Cities.FirstAsync(c => c.Name == "Warsaw");
        Assert.True(warsaw.BaseSalaryPerManhour > 0m,
            $"Warsaw BaseSalaryPerManhour must be positive, got {warsaw.BaseSalaryPerManhour}.");
    }

    [Fact]
    public async Task BerlinAndWarsaw_HaveBuildingLots()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var berlinLotCount = await db.BuildingLots
            .Where(l => l.City!.Name == "Berlin")
            .CountAsync();

        Assert.True(berlinLotCount >= 5,
            $"Berlin must have at least 5 building lots, got {berlinLotCount}.");

        var warsawLotCount = await db.BuildingLots
            .Where(l => l.City!.Name == "Warsaw")
            .CountAsync();

        Assert.True(warsawLotCount >= 5,
            $"Warsaw must have at least 5 building lots, got {warsawLotCount}.");
    }

    [Fact]
    public async Task TotalCityCount_IsAtLeastNine()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.Cities.CountAsync();
        Assert.True(count >= 9, $"Expected at least 9 cities (including Berlin and Warsaw), got {count}.");
    }
}
