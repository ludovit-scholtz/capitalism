using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class BankInterestVisibilityTests
{
    private static async Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return await Task.FromResult(new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance));
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
                "application/json"),
        };

        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static BankAccount CreateCompanyBankAccount(Guid companyId, string currencyCode, decimal balance)
    {
        return new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = currencyCode,
            Balance = balance,
            CompanyId = companyId,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task BankInterestTick_CreatesDedicatedBankStatementRowsPerTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var bankToken = await RegisterAndGetTokenAsync(client, $"bank-owner-{Guid.NewGuid():N}@test.com", "Bank Owner");
        var depositorToken = await RegisterAndGetTokenAsync(client, $"depositor-{Guid.NewGuid():N}@test.com", "Depositor");
        var bankPlayerId = await GetPlayerIdAsync(client, bankToken);
        var depositorPlayerId = await GetPlayerIdAsync(client, depositorToken);

        Guid depositorCompanyId;
        Guid depositorFundingAccountId;
        string bankName;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = await CreateProcessorAsync(scope);

            var city = await db.Cities
                .AsNoTracking()
                .FirstAsync(c => c.CurrencyCode == "EUR");

            var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = bankPlayerId, Name = "Visibility Bank Co", Cash = 0m };
            var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositorPlayerId, Name = "Depositor Co", Cash = 0m };
            db.Companies.AddRange(bankCompany, depositorCompany);

            var bankBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = bankCompany.Id,
                CityId = city.Id,
                Type = BuildingType.Bank,
                Name = "Visibility Bank",
                BaseCapitalDeposited = true,
                DepositInterestRatePercent = 12m,
                LendingInterestRatePercent = 8m,
                TotalDeposits = 100_000m,
            };
            db.Buildings.Add(bankBuilding);
            bankName = bankBuilding.Name;

            var bankFunding = CreateCompanyBankAccount(bankCompany.Id, city.CurrencyCode, 2_000_000m);
            var depositorFunding = CreateCompanyBankAccount(depositorCompany.Id, city.CurrencyCode, 50_000m);
            db.BankAccounts.AddRange(bankFunding, depositorFunding);

            var depositAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
                CurrencyCode = city.CurrencyCode,
                Balance = 100_000m,
                CompanyId = depositorCompany.Id,
                BankBuildingId = bankBuilding.Id,
                DepositInterestRatePercent = 12m,
                IsGovernmentAccount = false,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(depositAccount);
            await db.SaveChangesAsync();

            await processor.ProcessTickAsync();
            await processor.ProcessTickAsync();
            await db.SaveChangesAsync();

            depositorCompanyId = depositorCompany.Id;
            depositorFundingAccountId = depositorFunding.Id;
        }

        var statementResult = await ExecuteGraphQlAsync(
            client,
            """
            query BankStatement($companyId: UUID!, $accountId: UUID!, $limit: Int!) {
              bankStatement(companyId: $companyId, accountId: $accountId, limit: $limit) {
                rows {
                  category
                  description
                  amount
                  runningBalance
                  recordedAtTick
                }
              }
            }
            """,
            new { companyId = depositorCompanyId, accountId = depositorFundingAccountId, limit = 50 },
            depositorToken);

        var rows = statementResult.GetProperty("data").GetProperty("bankStatement").GetProperty("rows")
            .EnumerateArray()
            .ToList();

        var interestRows = rows
            .Where(row => row.GetProperty("category").GetString() == LedgerCategory.DepositInterestReceived)
            .ToList();

        Assert.Equal(2, interestRows.Count);
        Assert.All(interestRows, row =>
        {
            Assert.Contains(bankName, row.GetProperty("description").GetString() ?? string.Empty);
            Assert.True(row.GetProperty("amount").GetDecimal() > 0m);
            Assert.True(row.GetProperty("runningBalance").GetDecimal() > 0m);
        });
        Assert.Equal(
            2,
            interestRows
                .Select(row => row.GetProperty("recordedAtTick").GetInt64())
                .Distinct()
                .Count());
    }

    [Fact]
    public async Task PersonAccount_IncludesDepositInterestPaymentsWithTickAndCurrency()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var bankToken = await RegisterAndGetTokenAsync(client, $"bank-owner-{Guid.NewGuid():N}@test.com", "Bank Owner");
        var depositorToken = await RegisterAndGetTokenAsync(client, $"depositor-{Guid.NewGuid():N}@test.com", "Depositor");
        var bankPlayerId = await GetPlayerIdAsync(client, bankToken);
        var depositorPlayerId = await GetPlayerIdAsync(client, depositorToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var processor = await CreateProcessorAsync(scope);

            var city = await db.Cities
                .AsNoTracking()
                .FirstAsync(c => c.CurrencyCode == "EUR");

            var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = bankPlayerId, Name = "Income Bank Co", Cash = 0m };
            var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositorPlayerId, Name = "Income Depositor Co", Cash = 0m };
            db.Companies.AddRange(bankCompany, depositorCompany);

            var bankBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = bankCompany.Id,
                CityId = city.Id,
                Type = BuildingType.Bank,
                Name = "Income Bank",
                BaseCapitalDeposited = true,
                DepositInterestRatePercent = 10m,
                LendingInterestRatePercent = 8m,
                TotalDeposits = 80_000m,
            };
            db.Buildings.Add(bankBuilding);

            db.BankAccounts.Add(CreateCompanyBankAccount(bankCompany.Id, city.CurrencyCode, 1_000_000m));
            db.BankAccounts.Add(CreateCompanyBankAccount(depositorCompany.Id, city.CurrencyCode, 10_000m));
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
                CurrencyCode = city.CurrencyCode,
                Balance = 80_000m,
                CompanyId = depositorCompany.Id,
                BankBuildingId = bankBuilding.Id,
                DepositInterestRatePercent = 10m,
                IsGovernmentAccount = false,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            await processor.ProcessTickAsync();
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query PersonAccountWithInterest {
              personAccount {
                interestPayments {
                  companyName
                  bankBuildingName
                  amount
                  recordedAtTick
                  currencyCode
                  description
                }
              }
            }
            """,
            token: depositorToken);

        var interestPayments = result
            .GetProperty("data")
            .GetProperty("personAccount")
            .GetProperty("interestPayments")
            .EnumerateArray()
            .ToList();

        Assert.NotEmpty(interestPayments);
        var first = interestPayments[0];
        Assert.Equal("EUR", first.GetProperty("currencyCode").GetString());
        Assert.True(first.GetProperty("amount").GetDecimal() > 0m);
        Assert.True(first.GetProperty("recordedAtTick").GetInt64() > 0);
        Assert.Contains("Deposit interest", first.GetProperty("description").GetString() ?? string.Empty);
    }
}
