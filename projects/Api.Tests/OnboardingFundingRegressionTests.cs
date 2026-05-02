using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class OnboardingFundingRegressionTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    public OnboardingFundingRegressionTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FinishOnboarding_ConsumesUsdStarterCash_AndRecordsCompanyFundingLedgerEntries()
    {
        const string email = "onboarding-funding@test.com";
        var token = await RegisterAndGetTokenAsync(email, "Funding Tester", "Password1!");

        var cityId = await GetCityIdByNameAsync("Bratislava");
        var factoryLotId = await GetAvailableLotIdAsync(cityId, "FACTORY");

        var startResult = await ExecuteGraphQlAsync(
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id }
                nextStep
              }
            }
            """,
            new
            {
                input = new
                {
                    industry = "FURNITURE",
                    cityId,
                    ipoRaiseTarget = 400000,
                    companyName = "Funding Regression Co",
                    factoryLotId,
                },
            },
            token);

        var companyId = Guid.Parse(
            startResult.GetProperty("data")
                .GetProperty("startOnboardingCompany")
                .GetProperty("company")
                .GetProperty("id")
                .GetString()!);

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");
        var shopLotId = await GetAvailableLotIdAsync(cityId, "SALES_SHOP");

        _ = await ExecuteGraphQlAsync(
            """
            mutation Finish($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) {
                company { id }
                cityCurrencyCode
              }
            }
            """,
            new
            {
                input = new
                {
                    productTypeId = productId,
                    shopLotId,
                },
            },
            token);

        var personAccountResult = await ExecuteGraphQlAsync(
            "{ personAccount { personalCash availableCash taxReserve } }",
            token: token);

        var personAccount = personAccountResult.GetProperty("data").GetProperty("personAccount");
        Assert.Equal(0m, personAccount.GetProperty("personalCash").GetDecimal());
        Assert.Equal(0m, personAccount.GetProperty("availableCash").GetDecimal());
        Assert.Equal(0m, personAccount.GetProperty("taxReserve").GetDecimal());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.SingleAsync(candidate => candidate.Email == email);

        var personalAccounts = await db.BankAccounts
            .Where(account => account.PlayerId == player.Id)
            .ToListAsync();

        Assert.NotEmpty(personalAccounts);
        Assert.All(personalAccounts, account => Assert.Equal(0m, account.Balance));

        var founderEntry = await db.LedgerEntries.FirstOrDefaultAsync(entry =>
            entry.CompanyId == companyId
            && entry.Category == LedgerCategory.FounderContribution);
        var ipoEntry = await db.LedgerEntries.FirstOrDefaultAsync(entry =>
            entry.CompanyId == companyId
            && entry.Category == LedgerCategory.IpoRaise);

        Assert.NotNull(founderEntry);
        Assert.NotNull(ipoEntry);
        Assert.True(founderEntry!.Amount > 0m);
        Assert.True(ipoEntry!.Amount > 0m);
    }

    private async Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(new { query, variables }),
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await _client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"HTTP {(int)response.StatusCode}: {body}");
        }

        var result = JsonSerializer.Deserialize<JsonElement>(body);
        if (result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0)
        {
            throw new InvalidOperationException($"GraphQL errors: {errors}");
        }

        return result;
    }

    private async Task<string> RegisterAndGetTokenAsync(string email, string displayName, string password)
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private async Task<string> GetCityIdByNameAsync(string cityName)
    {
        var result = await ExecuteGraphQlAsync("{ cities { id name } }");
        var city = result.GetProperty("data")
            .GetProperty("cities")
            .EnumerateArray()
            .First(candidate => string.Equals(candidate.GetProperty("name").GetString(), cityName, StringComparison.Ordinal));

        return city.GetProperty("id").GetString()!;
    }

    private async Task<string> GetAvailableLotIdAsync(string cityId, string buildingType)
    {
        var result = await ExecuteGraphQlAsync(
            """
            query CityLots($cityId: UUID!) {
              cityLots(cityId: $cityId) {
                id
                ownerCompanyId
                suitableTypes
              }
            }
            """,
            new { cityId });

        var lot = result.GetProperty("data")
            .GetProperty("cityLots")
            .EnumerateArray()
            .First(candidate =>
                candidate.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null
                && candidate.GetProperty("suitableTypes").GetString()!.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Contains(buildingType));

        return lot.GetProperty("id").GetString()!;
    }

    private async Task<string> GetStarterProductIdAsync(string industry, string slug)
    {
        var result = await ExecuteGraphQlAsync(
            """
            query StarterProducts($industry: String) {
              productTypes(industry: $industry) {
                id
                slug
              }
            }
            """,
            new { industry });

        var product = result.GetProperty("data")
            .GetProperty("productTypes")
            .EnumerateArray()
            .First(candidate => string.Equals(candidate.GetProperty("slug").GetString(), slug, StringComparison.Ordinal));

        return product.GetProperty("id").GetString()!;
    }
}
