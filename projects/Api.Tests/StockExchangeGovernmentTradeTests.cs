using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class StockExchangeGovernmentTradeTests
{
    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "Password1!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetCurrentPlayerIdAsync(HttpClient client, string token)
    {
        var result = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<Guid> GetGovernmentCompanyIdAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Companies
            .AsNoTracking()
            .Where(company => company.Player.Email == GovernmentActorConstants.GovernmentEmail)
            .Select(company => company.Id)
            .FirstAsync();
    }

    private static async Task<Guid> SeedPublicCompanyAsync(ApiWebApplicationFactory factory, Guid controllerPlayerId, string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = controllerPlayerId,
            Name = name,
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = currentTick,
        };

        db.Companies.Add(company);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = (Math.Abs(Guid.NewGuid().GetHashCode()) % 100_000_000L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = 100_000m,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        });
        db.Shareholdings.Add(new Shareholding
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            OwnerPlayerId = controllerPlayerId,
            ShareCount = 5_000m,
        });

        await db.SaveChangesAsync();
        return company.Id;
    }

    [Fact]
    public async Task StockExchangeListings_ExcludesGovernmentCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"gov-list-owner-{Guid.NewGuid():N}@test.com", "Gov List Owner");
        var ownerId = await GetCurrentPlayerIdAsync(client, ownerToken);
        var publicCompanyId = await SeedPublicCompanyAsync(factory, ownerId, "Regular Listed Co");
        var governmentCompanyId = await GetGovernmentCompanyIdAsync(factory);

        var investorToken = await RegisterAndGetTokenAsync(client, $"gov-list-investor-{Guid.NewGuid():N}@test.com", "Gov List Investor");
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "{ stockExchangeListings { companyId companyName } }",
            token: investorToken);

        var listings = result.GetProperty("data").GetProperty("stockExchangeListings").EnumerateArray().ToList();
        Assert.Contains(listings, listing => listing.GetProperty("companyId").GetString() == publicCompanyId.ToString());
        Assert.DoesNotContain(listings, listing => listing.GetProperty("companyId").GetString() == governmentCompanyId.ToString());
        Assert.DoesNotContain(listings, listing => listing.GetProperty("companyName").GetString() == "Government");
    }

    [Fact]
    public async Task BuyShares_GovernmentCompany_ReturnsTradeForbiddenError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var governmentCompanyId = await GetGovernmentCompanyIdAsync(factory);
        var investorToken = await RegisterAndGetTokenAsync(client, $"gov-buy-{Guid.NewGuid():N}@test.com", "Gov Buy Investor");

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation BuyShares($input: BuySharesInput!) {
              buyShares(input: $input) { shareCount }
            }
            """,
            new { input = new { companyId = governmentCompanyId, shareCount = 1m } },
            investorToken);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("Government company shares cannot be traded on the stock exchange.", error.GetProperty("message").GetString());
        Assert.Equal("GOVERNMENT_SHARES_NOT_TRADEABLE", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task SellShares_GovernmentCompany_ReturnsTradeForbiddenError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var governmentCompanyId = await GetGovernmentCompanyIdAsync(factory);
        var investorToken = await RegisterAndGetTokenAsync(client, $"gov-sell-{Guid.NewGuid():N}@test.com", "Gov Sell Investor");
        var investorId = await GetCurrentPlayerIdAsync(client, investorToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = governmentCompanyId,
                OwnerPlayerId = investorId,
                ShareCount = 5m,
            });
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            mutation SellShares($input: SellSharesInput!) {
              sellShares(input: $input) { shareCount }
            }
            """,
            new { input = new { companyId = governmentCompanyId, shareCount = 1m } },
            investorToken);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("Government company shares cannot be traded on the stock exchange.", error.GetProperty("message").GetString());
        Assert.Equal("GOVERNMENT_SHARES_NOT_TRADEABLE", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task PersonAccount_ExcludesGovernmentShareholdings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var governmentCompanyId = await GetGovernmentCompanyIdAsync(factory);
        var investorToken = await RegisterAndGetTokenAsync(client, $"gov-portfolio-{Guid.NewGuid():N}@test.com", "Gov Portfolio Investor");
        var investorId = await GetCurrentPlayerIdAsync(client, investorToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = governmentCompanyId,
                OwnerPlayerId = investorId,
                ShareCount = 15m,
            });
            await db.SaveChangesAsync();
        }

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "{ personAccount { shareholdings { companyId companyName } } }",
            token: investorToken);

        var holdings = result.GetProperty("data").GetProperty("personAccount").GetProperty("shareholdings").EnumerateArray().ToList();
        Assert.DoesNotContain(holdings, holding => holding.GetProperty("companyId").GetString() == governmentCompanyId.ToString());
        Assert.DoesNotContain(holdings, holding => holding.GetProperty("companyName").GetString() == "Government");
    }

    [Fact]
    public async Task CompanyShareholders_GovernmentCompany_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var governmentCompanyId = await GetGovernmentCompanyIdAsync(factory);
        var investorToken = await RegisterAndGetTokenAsync(client, $"gov-shareholders-{Guid.NewGuid():N}@test.com", "Gov Shareholders Investor");

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            """
            query Shareholders($companyId: UUID!) {
              companyShareholders(companyId: $companyId) { companyId }
            }
            """,
            new { companyId = governmentCompanyId },
            investorToken);

        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").GetProperty("companyShareholders").ValueKind);
    }
}
