using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class CompanyCityFinancialBreakdownTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<(string Token, string Email)> RegisterAndGetTokenAsync(HttpClient client, string displayName)
    {
        var email = $"city-ledger-{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new
            {
                input = new
                {
                    email,
                    displayName,
                    password,
                },
            });

        var token = result.GetProperty("data").GetProperty("register").GetProperty("token").GetString();
        return (token!, email);
    }

    [Fact]
    public async Task CompanyCityFinancialBreakdown_ReturnsPerCityRevenueCostsAndProfit()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (token, email) = await RegisterAndGetTokenAsync(client, "Ledger Owner");

        Guid companyId;
        Guid bratislavaBuildingId;
        Guid pragueBuildingId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(playerItem => playerItem.Email == email);
            var bratislava = await db.Cities.FirstAsync(city => city.Name == "Bratislava");
            var prague = await db.Cities.FirstAsync(city => city.Name == "Prague");

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = "City Breakdown Co",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
            };
            db.Companies.Add(company);
            companyId = company.Id;

            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                AccountNumber = Guid.NewGuid().ToString("N")[..16],
                CurrencyCode = "EUR",
                Balance = 100_000m,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(account);

            var bratislavaBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = bratislava.Id,
                Type = BuildingType.Factory,
                Name = "Bratislava Factory",
                Latitude = bratislava.Latitude,
                Longitude = bratislava.Longitude,
                BankAccountId = account.Id,
            };
            var pragueBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = prague.Id,
                Type = BuildingType.Factory,
                Name = "Prague Factory",
                Latitude = prague.Latitude,
                Longitude = prague.Longitude,
                BankAccountId = account.Id,
            };
            db.Buildings.AddRange(bratislavaBuilding, pragueBuilding);
            bratislavaBuildingId = bratislavaBuilding.Id;
            pragueBuildingId = pragueBuilding.Id;

            db.LedgerEntries.AddRange(
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = bratislavaBuilding.Id,
                    Category = LedgerCategory.Revenue,
                    Description = "Bratislava sale",
                    Amount = 1_200m,
                    RecordedAtTick = 100,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = bratislavaBuilding.Id,
                    Category = LedgerCategory.ShippingCost,
                    Description = "Bratislava shipping",
                    Amount = -200m,
                    RecordedAtTick = 101,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = pragueBuilding.Id,
                    Category = LedgerCategory.Revenue,
                    Description = "Prague sale",
                    Amount = 900m,
                    RecordedAtTick = 102,
                    RecordedAtUtc = DateTime.UtcNow,
                },
                new LedgerEntry
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    BuildingId = pragueBuilding.Id,
                    Category = LedgerCategory.LaborCost,
                    Description = "Prague labor",
                    Amount = -500m,
                    RecordedAtTick = 103,
                    RecordedAtUtc = DateTime.UtcNow,
                });

            await db.SaveChangesAsync();
        }

        var queryResult = await ExecuteGraphQlAsync(
            client,
            """
            query CityBreakdown($companyId: UUID!) {
              companyCityFinancialBreakdown(companyId: $companyId) {
                cityId
                cityName
                currencyCode
                revenue
                costs
                profit
                revenueTrend {
                  tick
                  revenue
                }
              }
            }
            """,
            new { companyId },
            token);

        var rows = queryResult.GetProperty("data").GetProperty("companyCityFinancialBreakdown");
        Assert.Equal(2, rows.GetArrayLength());

        var byCity = rows.EnumerateArray().ToDictionary(row => row.GetProperty("cityName").GetString()!);
        Assert.Equal(1_200m, byCity["Bratislava"].GetProperty("revenue").GetDecimal());
        Assert.Equal(200m, byCity["Bratislava"].GetProperty("costs").GetDecimal());
        Assert.Equal(1_000m, byCity["Bratislava"].GetProperty("profit").GetDecimal());
        Assert.Equal("EUR", byCity["Bratislava"].GetProperty("currencyCode").GetString());

        Assert.Equal(900m, byCity["Prague"].GetProperty("revenue").GetDecimal());
        Assert.Equal(500m, byCity["Prague"].GetProperty("costs").GetDecimal());
        Assert.Equal(400m, byCity["Prague"].GetProperty("profit").GetDecimal());
        Assert.Equal("CZK", byCity["Prague"].GetProperty("currencyCode").GetString());

        Assert.NotEmpty(byCity["Bratislava"].GetProperty("revenueTrend").EnumerateArray());
        Assert.NotEmpty(byCity["Prague"].GetProperty("revenueTrend").EnumerateArray());

        _ = bratislavaBuildingId;
        _ = pragueBuildingId;
    }

    [Fact]
    public async Task CompanyCityFinancialBreakdown_ForOtherPlayerCompany_ReturnsEmptyList()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var (ownerToken, ownerEmail) = await RegisterAndGetTokenAsync(client, "Owner");
        var (otherToken, _) = await RegisterAndGetTokenAsync(client, "Other");

        Guid companyId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var owner = await db.Players.FirstAsync(player => player.Email == ownerEmail);
            var city = await db.Cities.FirstAsync(cityItem => cityItem.Name == "Bratislava");

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = owner.Id,
                Name = "Owner Co",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = 1,
            };
            db.Companies.Add(company);
            companyId = company.Id;

            var account = new BankAccount
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                AccountNumber = Guid.NewGuid().ToString("N")[..16],
                CurrencyCode = city.CurrencyCode,
                Balance = 10_000m,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(account);

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Owner Factory",
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                BankAccountId = account.Id,
            };
            db.Buildings.Add(building);

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.Revenue,
                Description = "Revenue",
                Amount = 100m,
                RecordedAtTick = 100,
                RecordedAtUtc = DateTime.UtcNow,
            });

            await db.SaveChangesAsync();
        }

        var ownerResult = await ExecuteGraphQlAsync(
            client,
            "query Q($companyId: UUID!) { companyCityFinancialBreakdown(companyId: $companyId) { cityName } }",
            new { companyId },
            ownerToken);
        Assert.Single(ownerResult.GetProperty("data").GetProperty("companyCityFinancialBreakdown").EnumerateArray());

        var otherResult = await ExecuteGraphQlAsync(
            client,
            "query Q($companyId: UUID!) { companyCityFinancialBreakdown(companyId: $companyId) { cityName } }",
            new { companyId },
            otherToken);
        Assert.Empty(otherResult.GetProperty("data").GetProperty("companyCityFinancialBreakdown").EnumerateArray());
    }
}
