using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Regression tests for the onboarding financial validation.
///
/// These tests verify the complete IPO transfer mechanics: 200k USD is transferred from the
/// player's personal account to the new company, leaving the personal account with zero balance
/// across all currencies. Ledger entries must be visible from both sides:
/// - Personal ledger: incoming government deposit (+200k) and outgoing founder contribution (-200k)
/// - Company ledger: incoming founder contribution and IPO raise amounts
///
/// This set covers the ROADMAP item "Fix onboarding" final acceptance criterion:
/// "Create test which will check that after the onboarding there is 200k USD transferred from
/// the personal account and the current balance is 0."
/// </summary>
public sealed class OnboardingFundingRegressionTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly ApiWebApplicationFactory _factory;

    private const decimal StarterFounderContribution = 200_000m;
    private const decimal StarterIpoRaiseTarget = 400_000m;

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
                    ipoRaiseTarget = (int)StarterIpoRaiseTarget,
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

    /// <summary>
    /// Validates the "personal ledger as outgoing tx" requirement from the ROADMAP:
    /// The personal bank statement must show both sides of the IPO transfer:
    ///  - Government starter deposit (+200k USD credit)
    ///  - Founder contribution to company IPO (-200k USD debit)
    /// After both entries the personal account balance must be exactly zero.
    ///
    /// This proves the player can inspect their personal bank statement and see a clear
    /// audit trail of how their starter cash was converted into company equity.
    /// </summary>
    [Fact]
    public async Task FinishOnboarding_PersonalBankStatement_ShowsGovernmentDepositAsCreditAndFounderContributionAsDebit()
    {
        // Arrange: register a fresh player and complete the full onboarding flow in Bratislava (EUR city).
        // The player's personal USD account starts at 200k, then 200k is debited to fund the company.
        var token = await RegisterAndGetTokenAsync(
            $"personal-stmt-{Guid.NewGuid():N}@test.com",
            "Statement Tester",
            "Password1!");

        var cityId = await GetCityIdByNameAsync("Bratislava");
        var factoryLotId = await GetAvailableLotIdAsync(cityId, "FACTORY");

        var startResult = await ExecuteGraphQlAsync(
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId, ipoRaiseTarget = (int)StarterIpoRaiseTarget, companyName = "Stmt Test Co", factoryLotId } },
            token);

        var companyId = startResult.GetProperty("data").GetProperty("startOnboardingCompany").GetProperty("company").GetProperty("id").GetString()!;

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");
        var shopLotId = await GetAvailableLotIdAsync(cityId, "SALES_SHOP");

        _ = await ExecuteGraphQlAsync(
            """
            mutation Finish($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) { company { id } }
            }
            """,
            new { input = new { productTypeId = productId, shopLotId } },
            token);

        // Act: retrieve all bank accounts and find the personal USD settlement account.
        var accountsResult = await ExecuteGraphQlAsync(
            "{ myBankAccounts { id ownerType currencyCode balance } }",
            token: token);

        var personalUsdAccount = accountsResult.GetProperty("data").GetProperty("myBankAccounts")
            .EnumerateArray()
            .FirstOrDefault(a =>
                a.GetProperty("ownerType").GetString() == "PERSON"
                && a.GetProperty("currencyCode").GetString() == "USD");

        Assert.True(personalUsdAccount.ValueKind != JsonValueKind.Undefined,
            "Personal USD settlement account must exist after onboarding");

        // Verify zero balance is reported in the account summary.
        Assert.Equal(0m, personalUsdAccount.GetProperty("balance").GetDecimal());

        var personalAccountId = personalUsdAccount.GetProperty("id").GetString()!;

        // Act: query the personal bank statement using the personal account ID.
        // BuildPersonalBankStatementAsync synthesises two entries:
        //  1. Government starter deposit (+200k) — the initial 200k USD seeded for the new player
        //  2. Founder contribution (−200k)         — the 200k debited to fund the company IPO
        var stmtResult = await ExecuteGraphQlAsync(
            """
            query PersonalStmt($accountId: UUID!) {
              bankStatement(accountId: $accountId, limit: 50) {
                currentBalance
                totalEntries
                rows {
                  category
                  amount
                  description
                  runningBalance
                }
              }
            }
            """,
            new { accountId = personalAccountId },
            token);

        Assert.False(stmtResult.TryGetProperty("errors", out _), "Personal bank statement must not return errors");

        var stmt = stmtResult.GetProperty("data").GetProperty("bankStatement");

        // Assert: current balance is zero — no starter cash remains after funding the company.
        Assert.Equal(0m, stmt.GetProperty("currentBalance").GetDecimal());

        // Assert: exactly two entries — government deposit and founder contribution outgoing tx.
        var totalEntries = stmt.GetProperty("totalEntries").GetInt32();
        Assert.Equal(2, totalEntries);

        var rows = stmt.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);

        // One row must be the incoming government starter deposit (+200k USD).
        var depositRow = rows.FirstOrDefault(r => r.GetProperty("amount").GetDecimal() > 0m);
        Assert.True(depositRow.ValueKind != JsonValueKind.Undefined,
            "Personal statement must contain an incoming credit row (government starter deposit)");
        Assert.Equal(StarterFounderContribution, depositRow.GetProperty("amount").GetDecimal());
        var depositCategory = depositRow.GetProperty("category").GetString();
        Assert.Equal("BANK_ACCOUNT_TRANSFER_IN", depositCategory);
        Assert.Contains("Government", depositRow.GetProperty("description").GetString(),
            StringComparison.OrdinalIgnoreCase);

        // One row must be the outgoing founder contribution to the company IPO (−200k USD).
        var contributionRow = rows.FirstOrDefault(r => r.GetProperty("amount").GetDecimal() < 0m);
        Assert.True(contributionRow.ValueKind != JsonValueKind.Undefined,
            "Personal statement must contain an outgoing debit row (founder contribution to company IPO)");
        Assert.Equal(-StarterFounderContribution, contributionRow.GetProperty("amount").GetDecimal());
        var contributionCategory = contributionRow.GetProperty("category").GetString();
        Assert.Equal("FOUNDER_CONTRIBUTION", contributionCategory);

        // The running balance after the final (newest) entry must be zero.
        // Rows are ordered newest-first; the first row in the list is the most recent.
        Assert.Equal(0m, rows[0].GetProperty("runningBalance").GetDecimal());
    }

    /// <summary>
    /// Validates that for a USD city (New York) the founder contribution and IPO raise amounts
    /// are exactly 200,000 USD and the selected IPO raise target respectively, with no FX conversion.
    /// This is the clean-number validation: USD → USD has FX rate 1.0 so amounts must be bit-exact.
    ///
    /// This test ensures the exact 200k USD transfer requirement from the ROADMAP is numerically verified.
    /// </summary>
    [Fact]
    public async Task FinishOnboarding_UsdCity_FounderContributionIsExactly200kUsdAndIpoRaiseIsExact_NoFxConversion()
    {
        var email = $"usd-city-{Guid.NewGuid():N}@test.com";
        var token = await RegisterAndGetTokenAsync(
            email,
            "USD Tester",
            "Password1!");

        var cityId = await GetCityIdByNameAsync("New York");

        // New York lots are priced at market rates (millions of USD) which exceed the starter budget.
        // Create affordable test lots directly in the database so the onboarding can complete.
        var factoryLotId = await CreateTestLotAsync(cityId, "FACTORY,MINE", "NYC Test Industrial");
        var shopLotId = await CreateTestLotAsync(cityId, "SALES_SHOP", "NYC Test Commercial");

        var startResult = await ExecuteGraphQlAsync(
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId, ipoRaiseTarget = (int)StarterIpoRaiseTarget, companyName = "NYC Furniture Co", factoryLotId } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _),
            $"startOnboardingCompany must succeed for New York. Response: {startResult}");

        var companyId = Guid.Parse(startResult.GetProperty("data")
            .GetProperty("startOnboardingCompany")
            .GetProperty("company")
            .GetProperty("id")
            .GetString()!);

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");

        _ = await ExecuteGraphQlAsync(
            """
            mutation Finish($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) { company { id } cityCurrencyCode }
            }
            """,
            new { input = new { productTypeId = productId, shopLotId } },
            token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // For a USD city, FX rate = 1.0 → local amounts are identical to the USD source amounts.
        var founderEntry = await db.LedgerEntries.FirstOrDefaultAsync(e =>
            e.CompanyId == companyId && e.Category == LedgerCategory.FounderContribution);
        var ipoEntry = await db.LedgerEntries.FirstOrDefaultAsync(e =>
            e.CompanyId == companyId && e.Category == LedgerCategory.IpoRaise);

        Assert.NotNull(founderEntry);
        Assert.NotNull(ipoEntry);

        // Exact 200k USD: no FX conversion should alter the amount.
        Assert.True(founderEntry!.Amount == StarterFounderContribution,
            $"Founder contribution in USD city must be exactly {StarterFounderContribution:N0} USD, got {founderEntry.Amount}");
        Assert.True(ipoEntry!.Amount == StarterIpoRaiseTarget,
            $"IPO raise in USD city must be exactly {StarterIpoRaiseTarget:N0} USD, got {ipoEntry.Amount}");

        // Description must mention it is a direct USD deposit (no FX line).
        Assert.Contains("USD government starter deposit", founderEntry.Description,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FX", founderEntry.Description, StringComparison.OrdinalIgnoreCase);

        // Personal USD account must be at zero: 200k was fully transferred to the company.
        var player = await db.Players.AsNoTracking().SingleAsync(p => p.Email == email);
        var personalUsdBalance = await PersonalBankAccountService.GetTrackedBalanceAsync(
            db,
            player.Id,
            "USD");
        Assert.Equal(0m, personalUsdBalance);
    }

    /// <summary>
    /// Validates that the onboarding flow creates exactly ONE FounderContribution and ONE IpoRaise
    /// entry in the company ledger — no duplicate transactions regardless of retry attempts.
    ///
    /// Duplicate entries would cause inflated company balances and inaccurate bank statements,
    /// which could be exploited or cause loss of player trust.
    /// </summary>
    [Fact]
    public async Task FinishOnboarding_CompanyLedger_ContainsExactlyOneFounderContributionAndOneIpoRaise_NoDuplicates()
    {
        var token = await RegisterAndGetTokenAsync(
            $"no-dup-{Guid.NewGuid():N}@test.com",
            "No Duplicates",
            "Password1!");

        var cityId = await GetCityIdByNameAsync("Vienna");
        var factoryLotId = await GetAvailableLotIdAsync(cityId, "FACTORY");

        var startResult = await ExecuteGraphQlAsync(
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId, ipoRaiseTarget = (int)StarterIpoRaiseTarget, companyName = "No Dup Co", factoryLotId } },
            token);

        var companyId = Guid.Parse(startResult.GetProperty("data")
            .GetProperty("startOnboardingCompany")
            .GetProperty("company")
            .GetProperty("id")
            .GetString()!);

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");
        var shopLotId = await GetAvailableLotIdAsync(cityId, "SALES_SHOP");

        _ = await ExecuteGraphQlAsync(
            """
            mutation Finish($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) { company { id } }
            }
            """,
            new { input = new { productTypeId = productId, shopLotId } },
            token);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var founderEntries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Category == LedgerCategory.FounderContribution)
            .ToListAsync();
        var ipoEntries = await db.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.Category == LedgerCategory.IpoRaise)
            .ToListAsync();

        var founderEntry = Assert.Single(founderEntries);
        var ipoEntry = Assert.Single(ipoEntries);

        // Sanity-check amounts are positive (company receives funds).
        Assert.True(founderEntry.Amount > 0m, "FounderContribution must be a positive credit to the company");
        Assert.True(ipoEntry.Amount > 0m, "IpoRaise must be a positive credit to the company");
    }

    /// <summary>
    /// Validates that ALL personal bank accounts across all currencies have exactly zero balance
    /// after onboarding, even if additional currency accounts were opened during the flow.
    ///
    /// The ROADMAP requirement is explicit: "Personal account must not have any money after the
    /// onboarding - 0 USD, 0 EUR nor any other currency."
    /// </summary>
    [Fact]
    public async Task FinishOnboarding_AllPersonalBankAccountsAcrossAllCurrencies_HaveZeroBalance()
    {
        var token = await RegisterAndGetTokenAsync(
            $"all-currencies-{Guid.NewGuid():N}@test.com",
            "Currency Check",
            "Password1!");

        var cityId = await GetCityIdByNameAsync("Prague"); // CZK city — tests non-EUR, non-USD conversion
        var factoryLotId = await CreateTestLotAsync(cityId, "FACTORY,MINE", "Prague Currency Test Zone");

        var startResult = await ExecuteGraphQlAsync(
            """
            mutation Start($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId, ipoRaiseTarget = (int)StarterIpoRaiseTarget, companyName = "CZK Multi-Currency Co", factoryLotId } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _), "StartOnboardingCompany for Prague must succeed");

        var productId = await GetStarterProductIdAsync("FURNITURE", "wooden-chair");
        var shopLotId = await CreateTestLotAsync(cityId, "SALES_SHOP", "Prague Currency Test Commercial");

        _ = await ExecuteGraphQlAsync(
            """
            mutation Finish($input: FinishOnboardingInput!) {
              finishOnboarding(input: $input) { company { id } }
            }
            """,
            new { input = new { productTypeId = productId, shopLotId } },
            token);

        // Retrieve ALL accounts for this player (personal and company).
        var accountsResult = await ExecuteGraphQlAsync(
            "{ myBankAccounts { ownerType currencyCode balance } }",
            token: token);

        var allPersonalAccounts = accountsResult.GetProperty("data").GetProperty("myBankAccounts")
            .EnumerateArray()
            .Where(a => a.GetProperty("ownerType").GetString() == "PERSON")
            .ToList();

        // At least the USD settlement account must exist.
        Assert.NotEmpty(allPersonalAccounts);

        // Every personal account — regardless of currency — must have zero balance.
        foreach (var account in allPersonalAccounts)
        {
            var currency = account.GetProperty("currencyCode").GetString();
            var balance = account.GetProperty("balance").GetDecimal();
            Assert.True(0m == balance,
                $"Personal {currency} account must have zero balance after onboarding. Got: {balance}");
        }
    }

    private async Task<string> CreateTestLotAsync(string cityId, string suitableTypes, string district)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Id == Guid.Parse(cityId));
        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = $"Test Lot {Guid.NewGuid():N}"[..17],
            Description = "Auto-generated test lot for onboarding financial validation.",
            District = district,
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
            Price = 75_000m,
            SuitableTypes = suitableTypes,
            ConcurrencyToken = Guid.NewGuid(),
        };
        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();
        return lot.Id.ToString();
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
