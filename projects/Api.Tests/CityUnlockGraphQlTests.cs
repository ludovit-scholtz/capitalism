using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class CityUnlockGraphQlTests
{
    [Fact]
    public async Task CityUnlockStatus_ReturnsLocked_WhenCompanyNetWorthIsBelowThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client, $"city-lock-low-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedCompanyAsync(factory, playerId, companyName: "Starter GmbH", fundingBalance: 120_000m);
        var berlinId = await GetCityIdAsync(factory, "Berlin");

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!) {
              cityUnlockStatus(cityId: $cityId) {
                cityId
                cityName
                isUnlocked
                requiredNetWorth
                currentNetWorth
                currency
                progressPercent
                companyId
              }
            }
            """,
            new { cityId = berlinId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var status = result.GetProperty("data").GetProperty("cityUnlockStatus");
        Assert.Equal(berlinId.ToString(), status.GetProperty("cityId").GetString());
        Assert.Equal(companyId.ToString(), status.GetProperty("companyId").GetString());
        Assert.Equal("Berlin", status.GetProperty("cityName").GetString());
        Assert.False(status.GetProperty("isUnlocked").GetBoolean());
        Assert.Equal("EUR", status.GetProperty("currency").GetString());
        Assert.True(status.GetProperty("requiredNetWorth").GetDecimal() > 0m);
        Assert.True(status.GetProperty("currentNetWorth").GetDecimal() < status.GetProperty("requiredNetWorth").GetDecimal());
        Assert.InRange(status.GetProperty("progressPercent").GetInt32(), 0, 99);
    }

    [Fact]
    public async Task CityUnlockStatus_ReturnsUnlocked_WhenCompanyNetWorthMeetsThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client, $"city-lock-high-{Guid.NewGuid():N}@test.com");
        var berlinId = await GetCityIdAsync(factory, "Berlin");
        await SeedCompanyAsync(factory, playerId, companyName: "Growth GmbH", fundingBalance: 700_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query($cityId: UUID!) {
              cityUnlockStatus(cityId: $cityId) {
                isUnlocked
                requiredNetWorth
                currentNetWorth
                currency
                progressPercent
              }
            }
            """,
            new { cityId = berlinId },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var status = result.GetProperty("data").GetProperty("cityUnlockStatus");
        Assert.True(status.GetProperty("isUnlocked").GetBoolean());
        Assert.Equal("EUR", status.GetProperty("currency").GetString());
        Assert.True(status.GetProperty("currentNetWorth").GetDecimal() >= status.GetProperty("requiredNetWorth").GetDecimal());
        Assert.Equal(100, status.GetProperty("progressPercent").GetInt32());
    }

    [Fact]
    public async Task PurchaseLot_Berlin_ReturnsCityLocked_ForNewCompanyBelowThreshold()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client, $"purchase-lock-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedCompanyAsync(factory, playerId, companyName: "Expansion Blocked", fundingBalance: 200_000m);
        var lotId = await CreateTestLotAsync(factory, "Berlin", "Berlin Affordable Factory Lot", 12_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                building { id }
              }
            }
            """,
            new
            {
                input = new
                {
                    companyId,
                    lotId,
                    buildingType = BuildingType.Factory,
                    buildingName = "Berlin Factory"
                }
            },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors), result.ToString());
        Assert.Contains(errors.EnumerateArray(), error => error.GetProperty("extensions").GetProperty("code").GetString() == "CITY_LOCKED");
    }

    [Fact]
    public async Task PurchaseLot_Berlin_Succeeds_AfterThresholdIsMet()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, playerId) = await RegisterAsync(client, $"purchase-open-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedCompanyAsync(factory, playerId, companyName: "Expansion Ready", fundingBalance: 900_000m);
        var lotId = await CreateTestLotAsync(factory, "Berlin", "Berlin Affordable Factory Lot", 15_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                lot { id ownerCompanyId cityId }
                building { id name type cityId }
              }
            }
            """,
            new
            {
                input = new
                {
                    companyId,
                    lotId,
                    buildingType = BuildingType.Factory,
                    buildingName = "Berlin Factory"
                }
            },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var payload = result.GetProperty("data").GetProperty("purchaseLot");
        Assert.Equal(lotId.ToString(), payload.GetProperty("lot").GetProperty("id").GetString());
        Assert.Equal(companyId.ToString(), payload.GetProperty("lot").GetProperty("ownerCompanyId").GetString());
        Assert.Equal(BuildingType.Factory, payload.GetProperty("building").GetProperty("type").GetString());
    }

    [Fact]
    public async Task TickProcessor_WhenThresholdCrossed_CreatesUnlockRowAndNotification()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (_, playerId) = await RegisterAsync(client, $"unlock-phase-{Guid.NewGuid():N}@test.com");
        var companyId = await SeedCompanyAsync(factory, playerId, companyName: "Threshold Runner", fundingBalance: 650_000m);
        var berlinId = await GetCityIdAsync(factory, "Berlin");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();

        Assert.False(await db.CompanyCityUnlocks.AnyAsync(unlock => unlock.CompanyId == companyId && unlock.CityId == berlinId));
        Assert.False(await db.PlayerNotifications.AnyAsync(notification =>
            notification.PlayerId == playerId
            && notification.Type == PlayerNotificationType.CityExpansionUnlocked
            && notification.RelatedEntityId == berlinId));

        await tickProcessor.ProcessTickAsync();

        Assert.True(await db.CompanyCityUnlocks.AnyAsync(unlock => unlock.CompanyId == companyId && unlock.CityId == berlinId));
        var notification = await db.PlayerNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(item =>
                item.PlayerId == playerId
                && item.Type == PlayerNotificationType.CityExpansionUnlocked
                && item.RelatedEntityId == berlinId);

        Assert.NotNull(notification);
        Assert.Contains("Berlin", notification!.Title);
    }

    [Fact]
    public async Task AppDbInitializer_CityUnlockRequirementsRemainIdempotent()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<AppDbInitializer>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var beforeCount = await db.CityUnlockRequirements.CountAsync();
        await initializer.InitializeAsync();
        var afterCount = await db.CityUnlockRequirements.CountAsync();

        Assert.Equal(beforeCount, afterCount);

        var berlinRequirement = await db.CityUnlockRequirements
            .Include(requirement => requirement.City)
            .SingleAsync(requirement => requirement.City.Name == "Berlin");
        var warsawRequirement = await db.CityUnlockRequirements
            .Include(requirement => requirement.City)
            .SingleAsync(requirement => requirement.City.Name == "Warsaw");

        Assert.Equal(500_000m, berlinRequirement.RequiredNetWorthUsd);
        Assert.Equal(300_000m, warsawRequirement.RequiredNetWorthUsd);
    }

    [Fact]
    public async Task StartOnboardingCompany_Warsaw_IsAcceptedAsStartingCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, _) = await RegisterAsync(client, $"onboarding-warsaw-{Guid.NewGuid():N}@test.com");
        var warsawId = await GetCityIdAsync(factory, "Warsaw");
        var lotId = await CreateTestLotAsync(factory, "Warsaw", "Warsaw Starter Factory Lot", 50_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id name }
                factory { id cityId }
                nextStep
              }
            }
            """,
            new
            {
                input = new
                {
                    industry = "FURNITURE",
                    cityId = warsawId,
                    companyName = "Warsaw Starter",
                    factoryLotId = lotId,
                    ipoRaiseTarget = 200000
                }
            },
            token);

        Assert.False(result.TryGetProperty("errors", out _), result.ToString());
        var payload = result.GetProperty("data").GetProperty("startOnboardingCompany");
        Assert.Equal("Warsaw Starter", payload.GetProperty("company").GetProperty("name").GetString());
        Assert.Equal(warsawId.ToString(), payload.GetProperty("factory").GetProperty("cityId").GetString());
        Assert.Equal("SHOP_SELECTION", payload.GetProperty("nextStep").GetString());
    }

    private static async Task<(string Token, Guid PlayerId)> RegisterAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
                player { id }
              }
            }
            """,
            new { input = new { email, password = "Password1!", displayName = "City Unlock Tester" } });

        var register = result.GetProperty("data").GetProperty("register");
        return (
            register.GetProperty("token").GetString()!,
            Guid.Parse(register.GetProperty("player").GetProperty("id").GetString()!));
    }

    private static async Task<Guid> SeedCompanyAsync(
        ApiWebApplicationFactory factory,
        Guid playerId,
        string companyName,
        decimal fundingBalance)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.FirstAsync(candidate => candidate.Id == playerId);
        var bratislava = await db.Cities.FirstAsync(city => city.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = companyName,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync();

        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;

        var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, company.Id, bratislava.CurrencyCode);
        fundingAccount.Balance = fundingBalance;
        await db.SaveChangesAsync();
        return company.Id;
    }

    private static async Task<Guid> CreateTestLotAsync(
        ApiWebApplicationFactory factory,
        string cityName,
        string lotName,
        decimal price)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(candidate => candidate.Name == cityName);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = lotName,
            Description = $"Test starter lot in {cityName}.",
            District = "Industrial Zone",
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
            PopulationIndex = 0.65m,
            BasePrice = price,
            Price = price,
            SuitableTypes = "FACTORY,POWER_PLANT",
            ConcurrencyToken = Guid.NewGuid(),
        };

        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();
        return lot.Id;
    }

    private static async Task<Guid> GetCityIdAsync(ApiWebApplicationFactory factory, string cityName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Cities
            .Where(city => city.Name == cityName)
            .Select(city => city.Id)
            .SingleAsync();
    }

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
                "application/json")
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
}
