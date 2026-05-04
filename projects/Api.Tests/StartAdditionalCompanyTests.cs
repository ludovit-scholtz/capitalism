using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the startAdditionalCompany GraphQL mutation.
/// Covers: prerequisites validation (age, profitability, balance, cap), happy path with all tiers,
/// ledger entry auditing, and context-switcher visibility of the new company.
/// Each test uses an isolated factory to avoid shared-state interference.
/// </summary>
public sealed class StartAdditionalCompanyTests
{
    // ── Shared GQL fragments ────────────────────────────────────────────────────

    private const string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            player { id }
          }
        }
        """;

    private const string StartAdditionalCompanyMutation = """
        mutation StartAdditionalCompany($input: StartAdditionalCompanyInput!) {
          startAdditionalCompany(input: $input) {
            id
            name
          }
        }
        """;

    private const string MyCompaniesQuery = """
        {
          myCompanies {
            id
            name
          }
        }
        """;

    private const string PrerequisitesQuery = """
        {
          additionalCompanyPrerequisites {
            companyCount
            underMaxCap
            hasExistingCompany
            companyAgeRequirementMet
            ticksUntilAgeRequirementMet
            profitabilityRequirementMet
            netIncomeInWindow
            balanceRequirementMet
            personalBalanceUsd
            allRequirementsMet
          }
        }
        """;

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteAsync(
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

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName = "Tester")
    {
        var result = await ExecuteAsync(client, RegisterMutation,
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    /// <summary>Seeds a company for the player that is already <paramref name="ageTicks"/> old.</summary>
    private static async Task<Guid> SeedCompanyAsync(
        ApiWebApplicationFactory factory,
        Guid playerId,
        long ageTicks,
        decimal fundingBalance = 600_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gameState = await db.GameStates.FirstOrDefaultAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        var companyId = Guid.NewGuid();
        var company = new Company
        {
            Id = companyId,
            PlayerId = playerId,
            Name = "First Corp",
            TotalSharesIssued = 10_000m,
            DividendPayoutRatio = 0.2m,
            FoundedAtUtc = DateTime.UtcNow.AddDays(-1),
            FoundedAtTick = currentTick - ageTicks,
        };
        db.Companies.Add(company);

        // Provision a funding bank account for the company.
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"1234{companyId:N}".Substring(0, 16),
            CompanyId = companyId,
            CurrencyCode = city.CurrencyCode,
            Balance = fundingBalance,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);

        await db.SaveChangesAsync();
        return companyId;
    }

    /// <summary>Seeds positive ledger entries so the company appears profitable over the last 365 ticks.</summary>
    private static async Task SeedProfitableLedgerAsync(
        ApiWebApplicationFactory factory,
        Guid companyId,
        decimal netAmount = 50_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gameState = await db.GameStates.FirstOrDefaultAsync();
        var currentTick = gameState?.CurrentTick ?? 0L;

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Category = LedgerCategory.Revenue,
            Description = "Test seed revenue",
            Amount = netAmount,
            RecordedAtTick = currentTick - 10,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a personal USD settlement account for the player with the specified balance.</summary>
    private static async Task SeedPersonalBalanceAsync(
        ApiWebApplicationFactory factory,
        Guid playerId,
        decimal balanceUsd)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var existing = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");

        if (existing is not null)
        {
            existing.Balance = balanceUsd;
        }
        else
        {
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = $"USD{playerId:N}".Substring(0, 16),
                PlayerId = playerId,
                CurrencyCode = "USD",
                Balance = balanceUsd,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static async Task<Guid> GetBratislavaCityIdAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Cities.FirstAsync(c => c.Name == "Bratislava")).Id;
    }

    // ── Tests ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StartAdditionalCompany_WithNoExistingCompany_ReturnsNoExistingCompanyError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "no-company@test.com");
        var cityId = await GetBratislavaCityIdAsync(factory);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Second Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("NO_EXISTING_COMPANY", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_WithFirstCompanyTooYoung_ReturnsCompanyTooYoungError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "young-company@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        // Company is only 100 ticks old — well under the 8 760-tick minimum.
        await SeedCompanyAsync(factory, playerId, ageTicks: 100);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Second Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("COMPANY_TOO_YOUNG", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_WithUnprofitableFirstCompany_ReturnsNotProfitableError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "unprofitable@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        // Company is old enough but has NOT generated profit in last 365 ticks.
        await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        // Do NOT seed profitable ledger entries.

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Second Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("COMPANY_NOT_PROFITABLE", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_WithInsufficientPersonalBalance_ReturnsInsufficientFundsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "broke@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        // Set balance to $50 000 — well under the $200 000 requirement.
        await SeedPersonalBalanceAsync(factory, playerId, 50_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Second Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INSUFFICIENT_PERSONAL_FUNDS", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_AtFiveCompanyCap_ReturnsMaxCompaniesReachedError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "maxcap@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        // Seed 5 companies so the player is at the cap.
        for (var i = 0; i < 5; i++)
        {
            await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        }

        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Sixth Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("MAX_COMPANIES_REACHED", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_WithInvalidName_ReturnsInvalidCompanyNameError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "badname@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            // Name is only 2 chars — below the 3-char minimum.
            new { input = new { companyName = "AB", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_COMPANY_NAME", code);
    }

    [Fact]
    public async Task StartAdditionalCompany_WithValidPrerequisites_DefaultTier_CreatesCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "happy-path@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Second Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var data = result.GetProperty("data").GetProperty("startAdditionalCompany");
        Assert.Equal("Second Corp", data.GetProperty("name").GetString());
    }

    [Fact]
    public async Task StartAdditionalCompany_Tier400k_CreatesCompanyWithHigherCapital()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tier400@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Tier400 Corp", cityId, ipoRaiseTarget = 400_000m } },
            token);

        var data = result.GetProperty("data").GetProperty("startAdditionalCompany");
        Assert.Equal("Tier400 Corp", data.GetProperty("name").GetString());

        // Verify the new company has a higher balance (founder 200k + IPO 400k = 600k EUR).
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newCompanyId = Guid.Parse(data.GetProperty("id").GetString()!);
        var balance = await db.BankAccounts
            .Where(a => a.CompanyId == newCompanyId)
            .SumAsync(a => a.Balance);
        Assert.True(balance >= 500_000m, $"Expected ≥ 500 000 but got {balance}");
    }

    [Fact]
    public async Task StartAdditionalCompany_Tier600k_CreatesCompanyWithMaxCapital()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tier600@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Tier600 Corp", cityId, ipoRaiseTarget = 600_000m } },
            token);

        var data = result.GetProperty("data").GetProperty("startAdditionalCompany");
        Assert.Equal("Tier600 Corp", data.GetProperty("name").GetString());

        // Verify balance: founder 200k + IPO 600k = 800k EUR.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var newCompanyId = Guid.Parse(data.GetProperty("id").GetString()!);
        var balance = await db.BankAccounts
            .Where(a => a.CompanyId == newCompanyId)
            .SumAsync(a => a.Balance);
        Assert.True(balance >= 700_000m, $"Expected ≥ 700 000 but got {balance}");
    }

    [Fact]
    public async Task StartAdditionalCompany_ValidPrerequisites_DebitsPersonalAccountAndCreatesLedgerEntries()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "ledger-check@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Ledger Check Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        var data = result.GetProperty("data").GetProperty("startAdditionalCompany");
        var newCompanyId = Guid.Parse(data.GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Personal balance should be reduced by $200 000.
        var personalAccount = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.PlayerId == playerId && a.CurrencyCode == "USD");
        Assert.NotNull(personalAccount);
        Assert.True(personalAccount!.Balance <= 300_001m, $"Personal balance should have been reduced; got {personalAccount.Balance}");

        // Two opening ledger entries: FounderContribution + IpoRaise.
        var entries = await db.LedgerEntries
            .Where(e => e.CompanyId == newCompanyId)
            .ToListAsync();
        Assert.Contains(entries, e => e.Category == LedgerCategory.FounderContribution && e.Amount > 0);
        Assert.Contains(entries, e => e.Category == LedgerCategory.IpoRaise && e.Amount > 0);
    }

    [Fact]
    public async Task StartAdditionalCompany_NewCompanyAppearsInMyCompanies()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "context-switcher@test.com");
        var playerId = await GetPlayerIdAsync(client, token);
        var cityId = await GetBratislavaCityIdAsync(factory);

        var firstCompanyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, firstCompanyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        await ExecuteAsync(client,
            StartAdditionalCompanyMutation,
            new { input = new { companyName = "Switcher Corp", cityId, ipoRaiseTarget = 200_000m } },
            token);

        // Query myCompanies — the new company must appear in the list.
        var myCompaniesResult = await ExecuteAsync(client, MyCompaniesQuery, token: token);
        var companies = myCompaniesResult.GetProperty("data").GetProperty("myCompanies");
        var names = Enumerable.Range(0, companies.GetArrayLength())
            .Select(i => companies[i].GetProperty("name").GetString())
            .ToList();
        Assert.Contains("Switcher Corp", names);
    }

    [Fact]
    public async Task GetAdditionalCompanyPrerequisites_AllMet_ReturnsAllRequirementsMet()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "prereq-all@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        var companyId = await SeedCompanyAsync(factory, playerId, ageTicks: 10_000);
        await SeedProfitableLedgerAsync(factory, companyId);
        await SeedPersonalBalanceAsync(factory, playerId, 500_000m);

        var result = await ExecuteAsync(client, PrerequisitesQuery, token: token);
        var prereqs = result.GetProperty("data").GetProperty("additionalCompanyPrerequisites");

        Assert.True(prereqs.GetProperty("allRequirementsMet").GetBoolean());
        Assert.True(prereqs.GetProperty("companyAgeRequirementMet").GetBoolean());
        Assert.True(prereqs.GetProperty("profitabilityRequirementMet").GetBoolean());
        Assert.True(prereqs.GetProperty("balanceRequirementMet").GetBoolean());
        Assert.Equal(0L, prereqs.GetProperty("ticksUntilAgeRequirementMet").GetInt64());
    }

    [Fact]
    public async Task GetAdditionalCompanyPrerequisites_NoCompany_ReturnsUnmet()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "prereq-none@test.com");

        var result = await ExecuteAsync(client, PrerequisitesQuery, token: token);
        var prereqs = result.GetProperty("data").GetProperty("additionalCompanyPrerequisites");

        Assert.False(prereqs.GetProperty("allRequirementsMet").GetBoolean());
        Assert.False(prereqs.GetProperty("hasExistingCompany").GetBoolean());
    }
}
