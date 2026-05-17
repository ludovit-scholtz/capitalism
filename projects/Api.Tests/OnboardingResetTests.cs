using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class OnboardingResetTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public OnboardingResetTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ResetOnboardingProgress_RemovesOrphanOnboardingCompany_AndAllowsRestart()
    {
        var email = $"reset-onboarding-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(email, "Resettable Founder", "Password1!");
        var (companyId, factoryLotId, _, startResult) = await StartOnboardingCompanyAsync(token, "Resettable Co");

        Assert.False(startResult.TryGetProperty("errors", out _), "StartOnboardingCompany must succeed before reset.");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.SingleAsync(candidate => candidate.Email == email);

            player.OnboardingCurrentStep = null;
            player.OnboardingIndustry = null;
            player.OnboardingCityId = null;
            player.OnboardingCompanyId = null;
            player.OnboardingFactoryLotId = null;

            await db.SaveChangesAsync();
        }

        var resetResult = await ResetOnboardingProgressAsync(token);

        Assert.False(resetResult.TryGetProperty("errors", out _), "ResetOnboardingProgress should succeed for an orphaned onboarding company.");
        Assert.True(resetResult.GetProperty("data").GetProperty("resetOnboardingProgress").GetBoolean());

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.SingleAsync(candidate => candidate.Email == email);
            var factoryLot = await db.BuildingLots.FindAsync(Guid.Parse(factoryLotId));

            Assert.Equal(AccountContextType.Person, player.ActiveAccountType);
            Assert.Null(player.ActiveCompanyId);
            Assert.Null(player.OnboardingCurrentStep);
            Assert.Null(player.OnboardingCompanyId);
            Assert.Null(player.OnboardingFactoryLotId);
            Assert.Null(player.OnboardingShopBuildingId);
            Assert.False(await db.Companies.AnyAsync(company => company.Id == Guid.Parse(companyId)));
            Assert.False(await db.BankAccounts.AnyAsync(account => account.CompanyId == Guid.Parse(companyId)));
            Assert.NotNull(factoryLot);
            Assert.Null(factoryLot!.OwnerCompanyId);
            Assert.Null(factoryLot.BuildingId);
        }

        var (_, _, _, restartResult) = await StartOnboardingCompanyAsync(token, "Restarted Co");
        Assert.False(restartResult.TryGetProperty("errors", out _), "Player should be able to start onboarding again after reset.");
    }

    [Fact]
    public async Task ResetOnboardingProgress_Unauthenticated_ReturnsAuthorizationError()
    {
        var result = await ResetOnboardingProgressAsync(token: null);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.Contains("authorized", errors[0].GetProperty("message").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetOnboardingProgress_WhenAlreadyCompleted_ReturnsOnboardingAlreadyCompleted()
    {
        var token = await RegisterAndGetTokenAsync($"reset-completed-{Guid.NewGuid():N}@test.com", "Completed Founder", "Password1!");
        var (_, _, cityId, startResult) = await StartOnboardingCompanyAsync(token, "Completed Co");

        Assert.False(startResult.TryGetProperty("errors", out _), "StartOnboardingCompany must succeed before completion.");

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");
        var shopLotId = await GetAvailableLotIdAsync(cityId, "SALES_SHOP");
        var finishResult = await FinishOnboardingAsync(token, productId, shopLotId);

        Assert.False(finishResult.TryGetProperty("errors", out _), "FinishOnboarding must succeed before reset rejection.");

        var resetResult = await ResetOnboardingProgressAsync(token);

        Assert.True(resetResult.TryGetProperty("errors", out var errors));
        Assert.Equal("ONBOARDING_ALREADY_COMPLETED", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    private async Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private async Task<string> RegisterAndGetTokenAsync(string email, string displayName, string password)
    {
        var result = await ExecuteGraphQlAsync(
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private async Task<(string CompanyId, string FactoryLotId, string CityId, JsonElement Result)> StartOnboardingCompanyAsync(string token, string companyName)
    {
        var cityId = await GetCityIdByNameAsync("Bratislava");
        var factoryLotId = await GetAvailableLotIdAsync(cityId, "FACTORY");
        var result = await ExecuteGraphQlAsync(
            """
            mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                nextStep
                company { id name cash }
                factory { id name type }
                factoryLot { id ownerCompanyId buildingId }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId, companyName, factoryLotId } },
            token);

        var companyId = result.GetProperty("data").GetProperty("startOnboardingCompany").GetProperty("company").GetProperty("id").GetString()!;
        return (companyId, factoryLotId, cityId, result);
    }

    private Task<JsonElement> FinishOnboardingAsync(string token, string productId, string shopLotId)
        => ExecuteGraphQlAsync(
            """
            mutation FinishOnboarding($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) {
                company { id }
                salesShop { id }
              }
            }
            """,
            new { input = new { productTypeId = productId, shopLotId } },
            token);

    private Task<JsonElement> ResetOnboardingProgressAsync(string? token)
        => ExecuteGraphQlAsync(
            """
            mutation ResetOnboardingProgress {
              resetOnboardingProgress
            }
            """,
            token: token);

    private async Task<string> GetCityIdByNameAsync(string cityName)
    {
        var result = await ExecuteGraphQlAsync("{ cities { id name } }");
        var city = result.GetProperty("data").GetProperty("cities").EnumerateArray()
            .Single(candidate => candidate.GetProperty("name").GetString() == cityName);

        return city.GetProperty("id").GetString()!;
    }

    private async Task<string> GetAvailableLotIdAsync(string cityId, string buildingType)
    {
        var result = await ExecuteGraphQlAsync(
            "query CityLots($cityId: UUID!) { cityLots(cityId: $cityId) { id suitableTypes ownerCompanyId } }",
            new { cityId });

        var lot = result.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(candidate =>
                candidate.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null
                && candidate.GetProperty("suitableTypes").GetString()!.Contains(buildingType, StringComparison.Ordinal));

        return lot.GetProperty("id").GetString()!;
    }

    private async Task<string> GetStarterProductIdAsync(string industry, string slug)
    {
        var result = await ExecuteGraphQlAsync($"query {{ productTypes(industry: \"{industry}\") {{ id slug }} }}");

        var product = result.GetProperty("data").GetProperty("productTypes").EnumerateArray()
            .Single(candidate => candidate.GetProperty("slug").GetString() == slug);

        return product.GetProperty("id").GetString()!;
    }
}