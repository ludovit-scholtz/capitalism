using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class StockTakeoverTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public StockTakeoverTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json");

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    }

    private Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
        => ExecuteGraphQlAsync(_client, query, variables, token);

    private async Task<string> RegisterAndGetTokenAsync(string email, string displayName)
    {
        var result = await ExecuteGraphQlAsync(
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) {
                token
              }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private async Task<Guid> GetCurrentPlayerIdAsync(string token)
    {
        var result = await ExecuteGraphQlAsync("{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private async Task<Guid> SeedPublicCompanyAsync(Guid controllerPlayerId, string name, decimal founderShares)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstAsync();

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = controllerPlayerId,
            Name = name,
            Cash = 0m,
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
            ShareCount = founderShares,
        });

        await db.SaveChangesAsync();
        return company.Id;
    }

    [Fact]
    public async Task ReplaceCEO_UsesControlledOwnershipToTakeControl()
    {
        var targetOwnerToken = await RegisterAndGetTokenAsync($"replace-ceo-target-{Guid.NewGuid():N}@test.com", "Target Owner");
        var targetOwnerId = await GetCurrentPlayerIdAsync(targetOwnerToken);
        var targetCompanyId = await SeedPublicCompanyAsync(targetOwnerId, "Replace CEO Target", 5_000m);

        var acquirerToken = await RegisterAndGetTokenAsync($"replace-ceo-acquirer-{Guid.NewGuid():N}@test.com", "Acquirer");
        var acquirerId = await GetCurrentPlayerIdAsync(acquirerToken);

        await ExecuteGraphQlAsync(
            """
            mutation BuyShares($input: BuySharesInput!) {
              buyShares(input: $input) { ownedShareCount }
            }
            """,
            new { input = new { companyId = targetCompanyId, shareCount = 5_000m } },
            acquirerToken);

        var replaceResult = await ExecuteGraphQlAsync(
            """
            mutation ReplaceCEO($input: ReplaceCeoInput!) {
              replaceCEO(input: $input) {
                companyId
                companyName
                newCeoPlayerId
                newCeoDisplayName
              }
            }
            """,
            new { input = new { companyId = targetCompanyId, newCeoPlayerId = acquirerId } },
            acquirerToken);

        var replaced = replaceResult.GetProperty("data").GetProperty("replaceCEO");
        Assert.Equal(targetCompanyId.ToString(), replaced.GetProperty("companyId").GetString());
        Assert.Equal("Replace CEO Target", replaced.GetProperty("companyName").GetString());
        Assert.Equal(acquirerId.ToString(), replaced.GetProperty("newCeoPlayerId").GetString());

        var meResult = await ExecuteGraphQlAsync("{ me { activeAccountType activeCompanyId companies { id } } }", token: acquirerToken);
        var me = meResult.GetProperty("data").GetProperty("me");
        Assert.Equal("COMPANY", me.GetProperty("activeAccountType").GetString());
        Assert.Equal(targetCompanyId.ToString(), me.GetProperty("activeCompanyId").GetString());
        Assert.Contains(me.GetProperty("companies").EnumerateArray(), company => company.GetProperty("id").GetString() == targetCompanyId.ToString());
    }

    [Fact]
    public async Task ReplaceCEO_BelowFiftyPercentOwnership_ReturnsCompanyControlRequired()
    {
        var investorToken = await RegisterAndGetTokenAsync($"replace-ceo-low-{Guid.NewGuid():N}@test.com", "Low Ownership Investor");
        var investorId = await GetCurrentPlayerIdAsync(investorToken);

        var founderToken = await RegisterAndGetTokenAsync($"replace-ceo-founder-{Guid.NewGuid():N}@test.com", "Replace Founder");
        var founderId = await GetCurrentPlayerIdAsync(founderToken);
        var companyId = await SeedPublicCompanyAsync(founderId, "Replace CEO Threshold", 8_000m);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Shareholdings.Add(new Shareholding
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                OwnerPlayerId = investorId,
                ShareCount = 1_000m,
            });
            await db.SaveChangesAsync();
        }

        var replaceResult = await ExecuteGraphQlAsync(
            """
            mutation ReplaceCEO($input: ReplaceCeoInput!) {
              replaceCEO(input: $input) { companyId }
            }
            """,
            new { input = new { companyId, newCeoPlayerId = investorId } },
            investorToken);

        var errors = replaceResult.GetProperty("errors").EnumerateArray().ToList();
        Assert.NotEmpty(errors);
        Assert.Contains(errors, error =>
        {
            var extensions = error.GetProperty("extensions");
            return extensions.TryGetProperty("code", out var code)
                   && code.GetString() == "COMPANY_CONTROL_REQUIRED";
        });
    }
}
