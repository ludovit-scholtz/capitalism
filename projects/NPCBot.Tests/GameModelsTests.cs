using Capitalism.NPCBot.Models;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="GameStateSummary"/>, <see cref="PlayerProfile"/>,
/// <see cref="CompanySummary"/>, and <see cref="RankingEntry"/> model defaults
/// and behaviour.
/// </summary>
public sealed class GameModelsTests
{
    // ── PlayerProfile ─────────────────────────────────────────────────────────

    [Fact]
    public void PlayerProfile_DefaultCompaniesCollection_IsEmpty()
    {
        var profile = new PlayerProfile();
        Assert.NotNull(profile.Companies);
        Assert.Empty(profile.Companies);
    }

    [Fact]
    public void PlayerProfile_OnboardingCompletedAtUtc_NullByDefault()
    {
        var profile = new PlayerProfile();
        Assert.Null(profile.OnboardingCompletedAtUtc);
    }

    [Fact]
    public void PlayerProfile_WhenOnboardingComplete_NetWorthCanBeComputed()
    {
        var profile = new PlayerProfile
        {
            OnboardingCompletedAtUtc = DateTime.UtcNow.AddHours(-2),
            Companies =
            [
                new CompanySummary { Cash = 150_000m },
                new CompanySummary { Cash = 50_000m },
            ],
        };

        var netWorth = profile.Companies.Sum(c => c.Cash);
        Assert.Equal(200_000m, netWorth);
    }

    // ── CompanySummary ────────────────────────────────────────────────────────

    [Fact]
    public void CompanySummary_DefaultBuildingsCollection_IsEmpty()
    {
        var company = new CompanySummary();
        Assert.NotNull(company.Buildings);
        Assert.Empty(company.Buildings);
    }

    [Fact]
    public void CompanySummary_CashDefaultIsZero()
    {
        var company = new CompanySummary();
        Assert.Equal(0m, company.Cash);
    }

    // ── GameStateSummary ──────────────────────────────────────────────────────

    [Fact]
    public void GameStateSummary_CanStoreHighTickValues()
    {
        var gs = new GameStateSummary { CurrentTick = 100_000L };
        Assert.Equal(100_000L, gs.CurrentTick);
    }

    [Fact]
    public void GameStateSummary_TickIntervalAndTaxCycleAreReadable()
    {
        var gs = new GameStateSummary
        {
            TickIntervalSeconds = 300,
            TaxCycleTicks = 8760,
        };
        Assert.Equal(300, gs.TickIntervalSeconds);
        Assert.Equal(8760, gs.TaxCycleTicks);
    }

    // ── RankingEntry ──────────────────────────────────────────────────────────

    [Fact]
    public void RankingEntry_CanStoreRankAndNetWorth()
    {
        var entry = new RankingEntry
        {
            Rank = 1,
            DisplayName = "NPC_Trading_01",
            NetWorth = 500_000m,
        };
        Assert.Equal(1, entry.Rank);
        Assert.Equal(500_000m, entry.NetWorth);
    }

    // ── BuildingLotSummary ────────────────────────────────────────────────────

    [Fact]
    public void BuildingLotSummary_BuildingIdIsNullByDefault()
    {
        var lot = new BuildingLotSummary();
        Assert.Null(lot.BuildingId);
    }

    [Fact]
    public void BuildingLotSummary_SuitableTypesDefaultIsEmpty()
    {
        var lot = new BuildingLotSummary();
        Assert.Equal(string.Empty, lot.SuitableTypes);
    }

    // ── ProductTypeSummary ────────────────────────────────────────────────────

    [Fact]
    public void ProductTypeSummary_IsProOnlyDefaultIsFalse()
    {
        var product = new ProductTypeSummary();
        Assert.False(product.IsProOnly);
    }

    [Fact]
    public void ProductTypeSummary_BasePriceDefaultIsZero()
    {
        var product = new ProductTypeSummary();
        Assert.Equal(0m, product.BasePrice);
    }

    // ── CitySummary ───────────────────────────────────────────────────────────

    [Fact]
    public void CitySummary_DefaultCountryCodeIsEmpty()
    {
        var city = new CitySummary();
        Assert.Equal(string.Empty, city.CountryCode);
    }

    [Fact]
    public void CitySummary_DefaultPopulationIsZero()
    {
        var city = new CitySummary();
        Assert.Equal(0, city.Population);
    }

    [Fact]
    public void CitySummary_CanStoreAllFields()
    {
        var city = new CitySummary
        {
            Id = "city-ba",
            Name = "Bratislava",
            CountryCode = "SK",
            Population = 475_000,
        };
        Assert.Equal("Bratislava", city.Name);
        Assert.Equal("SK", city.CountryCode);
        Assert.Equal(475_000, city.Population);
    }

    // ── AuthPayload ───────────────────────────────────────────────────────────

    [Fact]
    public void AuthPayload_DefaultTokenIsEmpty()
    {
        var auth = new AuthPayload();
        Assert.Equal(string.Empty, auth.Token);
    }

    [Fact]
    public void AuthPayload_PlayerIsNullByDefault()
    {
        var auth = new AuthPayload();
        Assert.Null(auth.Player);
    }

    [Fact]
    public void AuthPayload_CanStoreTokenAndExpiry()
    {
        var expiry = DateTime.UtcNow.AddHours(2);
        var auth = new AuthPayload { Token = "jwt-abc", ExpiresAtUtc = expiry };
        Assert.Equal("jwt-abc", auth.Token);
        Assert.Equal(expiry, auth.ExpiresAtUtc);
    }

    // ── BuildingSummary ───────────────────────────────────────────────────────

    [Fact]
    public void BuildingSummary_DefaultTypeIsEmpty()
    {
        var building = new BuildingSummary();
        Assert.Equal(string.Empty, building.Type);
    }

    [Fact]
    public void BuildingSummary_DefaultCityIdIsEmpty()
    {
        var building = new BuildingSummary();
        Assert.Equal(string.Empty, building.CityId);
    }

    [Fact]
    public void BuildingSummary_CanStoreAllFields()
    {
        var building = new BuildingSummary
        {
            Id = "bldg-1",
            Name = "NPC Factory",
            Type = "FACTORY",
            CityId = "city-ba",
        };
        Assert.Equal("FACTORY", building.Type);
        Assert.Equal("city-ba", building.CityId);
    }

    // ── PlayerProfile onboarding fields ──────────────────────────────────────

    [Fact]
    public void PlayerProfile_OnboardingCurrentStepIsNullByDefault()
    {
        var profile = new PlayerProfile();
        Assert.Null(profile.OnboardingCurrentStep);
    }

    [Fact]
    public void PlayerProfile_OnboardingIndustryIsNullByDefault()
    {
        var profile = new PlayerProfile();
        Assert.Null(profile.OnboardingIndustry);
    }

    [Fact]
    public void PlayerProfile_OnboardingCityIdIsNullByDefault()
    {
        var profile = new PlayerProfile();
        Assert.Null(profile.OnboardingCityId);
    }
}
