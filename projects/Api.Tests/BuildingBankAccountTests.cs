using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for building bank account management:
/// - FundBuildingBankAccount mutation
/// - AssignBuildingBankAccount mutation
/// - CreateCompanyBankAccount mutation
/// - buildingBankAccount query
/// - OperatingCostPhase suspension when bank account has insufficient funds
/// </summary>
public sealed class BuildingBankAccountTests
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

        if (token is not null)
        {
            request.Headers.Authorization = new("Bearer", token);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body;
    }

    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email = "bba-test@example.com",
        string password = "TestPass123!")
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "BBA Tester", password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    // ── buildingBankAccount query ─────────────────────────────────────────────

    [Fact]
    public async Task BuildingBankAccount_WithNoAccount_ReturnsMissingAccountAdvisory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-noact-{Guid.NewGuid():N}@test.com");

        // Create a company with a building directly in the database.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "No-Account Co",
            Cash = 100_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Test Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            BuiltAtUtc = DateTime.UtcNow,
            // No BankAccountId
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query BuildingBankAccount($buildingId: UUID!) {
                buildingBankAccount(buildingId: $buildingId) {
                    hasBankAccount
                    bankAccountId
                    accountNumber
                    balance
                    isSuspendedForFunds
                    suspendedReason
                    currencyCode
                }
            }
            """,
            new { buildingId = building.Id },
            token);

        var info = result.GetProperty("data").GetProperty("buildingBankAccount");
        Assert.False(info.GetProperty("hasBankAccount").GetBoolean());
        Assert.Equal(JsonValueKind.Null, info.GetProperty("bankAccountId").ValueKind);
        Assert.Equal(JsonValueKind.Null, info.GetProperty("balance").ValueKind);
        Assert.Equal("EUR", info.GetProperty("currencyCode").GetString());
    }

    // ── fundBuildingBankAccount mutation ──────────────────────────────────────

    [Fact]
    public async Task FundBuildingBankAccount_WithSufficientCash_CreatesAccountAndTransfersFunds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-fund-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Fund Test Co",
            Cash = 500_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Fund Test Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Fund($input: FundBuildingBankAccountInput!) {
                fundBuildingBankAccount(input: $input) {
                    bankAccount {
                        hasBankAccount
                        accountNumber
                        balance
                        isSuspendedForFunds
                        suspendedReason
                    }
                    remainingCompanyCash
                }
            }
            """,
            new { input = new { buildingId = building.Id, amount = 10_000m } },
            token);

        var fund = result.GetProperty("data").GetProperty("fundBuildingBankAccount");
        var bankAccount = fund.GetProperty("bankAccount");

        Assert.True(bankAccount.GetProperty("hasBankAccount").GetBoolean());
        Assert.Equal(10_000m, bankAccount.GetProperty("balance").GetDecimal());
        Assert.False(bankAccount.GetProperty("isSuspendedForFunds").GetBoolean());

        // Company cash reduced.
        Assert.Equal(490_000m, fund.GetProperty("remainingCompanyCash").GetDecimal());
    }

    [Fact]
    public async Task FundBuildingBankAccount_WithInsufficientCash_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-nofunds-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Broke Co",
            Cash = 5_000m, // less than requested
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Broke Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Fund($input: FundBuildingBankAccountInput!) {
                fundBuildingBankAccount(input: $input) {
                    bankAccount { balance }
                    remainingCompanyCash
                }
            }
            """,
            new { input = new { buildingId = building.Id, amount = 50_000m } },
            token);

        // Should return a GraphQL error, not data.
        Assert.True(result.TryGetProperty("errors", out var errors));
        var errorCode = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INSUFFICIENT_COMPANY_CASH", errorCode);
    }

    [Fact]
    public async Task FundBuildingBankAccount_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Fund($input: FundBuildingBankAccountInput!) {
                fundBuildingBankAccount(input: $input) {
                    bankAccount { balance }
                }
            }
            """,
            new { input = new { buildingId = Guid.NewGuid(), amount = 1000m } });

        Assert.True(result.TryGetProperty("errors", out _));
    }

    // ── createCompanyBankAccount mutation ─────────────────────────────────────

    [Fact]
    public async Task CreateCompanyBankAccount_WithValidCurrency_CreatesAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-create-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Account Creator Co",
            Cash = 100_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation CreateAccount($input: CreateCompanyBankAccountInput!) {
                createCompanyBankAccount(input: $input) {
                    account {
                        id
                        accountNumber
                        currencyCode
                        balance
                    }
                }
            }
            """,
            new { input = new { companyId = company.Id, currencyCode = "EUR" } },
            token);

        var account = result.GetProperty("data").GetProperty("createCompanyBankAccount").GetProperty("account");
        Assert.Equal("EUR", account.GetProperty("currencyCode").GetString());
        Assert.Equal(0m, account.GetProperty("balance").GetDecimal());
        Assert.Equal(16, account.GetProperty("accountNumber").GetString()!.Length);
    }

    [Fact]
    public async Task CreateCompanyBankAccount_DuplicateCurrency_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-dup-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Duplicate Acc Co",
            Cash = 100_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        const string mutation = """
            mutation CreateAccount($input: CreateCompanyBankAccountInput!) {
                createCompanyBankAccount(input: $input) {
                    account { id }
                }
            }
            """;
        var vars = new { input = new { companyId = company.Id, currencyCode = "EUR" } };

        // First creation should succeed.
        await ExecuteGraphQlAsync(client, mutation, vars, token);

        // Second creation of same currency should fail.
        var result = await ExecuteGraphQlAsync(client, mutation, vars, token);
        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("DUPLICATE_BANK_ACCOUNT", code);
    }

    // ── Operating cost suspension ─────────────────────────────────────────────

    [Fact]
    public async Task OperatingCostPhase_WithSufficientBankAccount_DeductsAndKeepsActive()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-opok-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Funded Ops Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1234567890123456",
            CurrencyCode = "EUR",
            Balance = 100_000m, // plenty of funds
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankAccount);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Well-Funded Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        // Add a manufacturing unit so operating costs are > 0.
        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Manufacturing,
            GridX = 0,
            GridY = 0,
            Level = 1,
        });
        await db.SaveChangesAsync();

        // Run one tick.
        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // Reload the building and bank account.
        await db.Entry(building).ReloadAsync();
        await db.Entry(bankAccount).ReloadAsync();

        // Building should NOT be suspended.
        Assert.False(building.IsSuspendedForFunds);
        Assert.True(building.SuspendedReason is null or "MISSING_BANK_ACCOUNT" or { Length: 0 }
            || !building.SuspendedReason!.StartsWith("INSUFFICIENT_FUNDS"),
            $"Expected no suspension but got: {building.SuspendedReason}");

        // Bank account balance should have decreased.
        Assert.True(bankAccount.Balance < 100_000m, $"Expected balance to decrease from 100 000 but got {bankAccount.Balance}");
    }

    [Fact]
    public async Task OperatingCostPhase_WithInsufficientBankAccount_SuspendsBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-opsus-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Broke Ops Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "9876543210987654",
            CurrencyCode = "EUR",
            Balance = 0m, // zero — will be insufficient
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankAccount);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Empty-Account Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        // Add a manufacturing unit so operating costs are > 0.
        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Manufacturing,
            GridX = 0,
            GridY = 0,
            Level = 1,
        });
        await db.SaveChangesAsync();

        // Run one tick.
        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // Reload the building.
        await db.Entry(building).ReloadAsync();

        // Building must be suspended.
        Assert.True(building.IsSuspendedForFunds, "Building should be suspended when bank account balance is 0");
        Assert.NotNull(building.SuspendedReason);
        Assert.StartsWith("INSUFFICIENT_FUNDS:", building.SuspendedReason);

        // Bank account balance must remain 0 (no debit occurred).
        await db.Entry(bankAccount).ReloadAsync();
        Assert.Equal(0m, bankAccount.Balance);
    }

    [Fact]
    public async Task OperatingCostPhase_WithNoBankAccount_UsesCompanyCashLegacyPath()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-legacy-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Legacy Cash Co",
            Cash = 500_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Legacy Cash Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            PowerStatus = PowerStatus.Powered,
            // No BankAccountId
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        db.BuildingUnits.Add(new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Manufacturing,
            GridX = 0,
            GridY = 0,
            Level = 1,
        });
        await db.SaveChangesAsync();

        var initialCash = company.Cash;

        // Run one tick.
        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // Reload company and building.
        await db.Entry(company).ReloadAsync();
        await db.Entry(building).ReloadAsync();

        // Building should NOT be hard-suspended (legacy path).
        Assert.False(building.IsSuspendedForFunds);
        // Advisory warning set for UI.
        Assert.Equal("MISSING_BANK_ACCOUNT", building.SuspendedReason);

        // Company cash should have decreased (costs deducted from company cash in legacy path).
        Assert.True(company.Cash < initialCash, $"Expected company cash to decrease from legacy operating costs but got {company.Cash}");
    }

    [Fact]
    public async Task FundBuildingBankAccount_ClearsInsufficientFundsSuspension()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-clear-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Resume Co",
            Cash = 200_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1111222233334444",
            CurrencyCode = "EUR",
            Balance = 0m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankAccount);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Resume Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 10,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            IsSuspendedForFunds = true,
            SuspendedReason = "INSUFFICIENT_FUNDS:150.00",
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        // Fund the account via the mutation.
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Fund($input: FundBuildingBankAccountInput!) {
                fundBuildingBankAccount(input: $input) {
                    bankAccount {
                        isSuspendedForFunds
                        suspendedReason
                        balance
                    }
                }
            }
            """,
            new { input = new { buildingId = building.Id, amount = 10_000m } },
            token);

        var bankAccountResult = result.GetProperty("data").GetProperty("fundBuildingBankAccount").GetProperty("bankAccount");

        // Suspension should be cleared after funding.
        Assert.False(bankAccountResult.GetProperty("isSuspendedForFunds").GetBoolean());
        Assert.Equal(JsonValueKind.Null, bankAccountResult.GetProperty("suspendedReason").ValueKind);
        Assert.Equal(10_000m, bankAccountResult.GetProperty("balance").GetDecimal());
    }

    // ── AssignBuildingBankAccount mutation ────────────────────────────────────

    [Fact]
    public async Task AssignBuildingBankAccount_WithMatchingCurrency_AssignsAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-assign-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Assign Test Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "9999888877776666",
            CurrencyCode = "EUR",
            Balance = 50_000m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Assign Target Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BuiltAtUtc = DateTime.UtcNow,
            // No bank account initially.
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Assign($input: AssignBuildingBankAccountInput!) {
                assignBuildingBankAccount(input: $input) {
                    bankAccount {
                        hasBankAccount
                        bankAccountId
                        accountNumber
                        balance
                    }
                }
            }
            """,
            new { input = new { buildingId = building.Id, bankAccountId = account.Id } },
            token);

        var bankAccount = result.GetProperty("data").GetProperty("assignBuildingBankAccount").GetProperty("bankAccount");
        Assert.True(bankAccount.GetProperty("hasBankAccount").GetBoolean());
        Assert.Equal(account.Id.ToString(), bankAccount.GetProperty("bankAccountId").GetString());
        Assert.Equal("9999888877776666", bankAccount.GetProperty("accountNumber").GetString());
        Assert.Equal(50_000m, bankAccount.GetProperty("balance").GetDecimal());

        // Verify DB state.
        await db.Entry(building).ReloadAsync();
        Assert.Equal(account.Id, building.BankAccountId);
    }

    [Fact]
    public async Task AssignBuildingBankAccount_WithWrongCurrency_ReturnsCurrencyMismatchError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-currency-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var cityEur = await db.Cities.FirstAsync(c => c.Name == "Bratislava"); // EUR
        var cityCzk = await db.Cities.FirstAsync(c => c.CurrencyCode == "CZK");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Currency Mismatch Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        // EUR account.
        var eurAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1234000056780000",
            CurrencyCode = "EUR",
            Balance = 10_000m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(eurAccount);

        // Building in CZK city — assigning EUR account should fail.
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = cityCzk.Id,
            Type = BuildingType.Factory,
            Name = "CZK Factory",
            Latitude = cityCzk.Latitude,
            Longitude = cityCzk.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Assign($input: AssignBuildingBankAccountInput!) {
                assignBuildingBankAccount(input: $input) {
                    bankAccount { hasBankAccount }
                }
            }
            """,
            new { input = new { buildingId = building.Id, bankAccountId = eurAccount.Id } },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("CURRENCY_MISMATCH", code);
    }

    [Fact]
    public async Task AssignBuildingBankAccount_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Assign($input: AssignBuildingBankAccountInput!) {
                assignBuildingBankAccount(input: $input) {
                    bankAccount { hasBankAccount }
                }
            }
            """,
            new { input = new { buildingId = Guid.NewGuid(), bankAccountId = Guid.NewGuid() } });

        Assert.True(result.TryGetProperty("errors", out _));
    }

    // ── buildingBankAccount query auth ────────────────────────────────────────

    [Fact]
    public async Task BuildingBankAccount_Query_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query BBA($buildingId: UUID!) {
                buildingBankAccount(buildingId: $buildingId) {
                    hasBankAccount
                }
            }
            """,
            new { buildingId = Guid.NewGuid() });

        Assert.True(result.TryGetProperty("errors", out _));
    }

    // ── Multi-building isolation ───────────────────────────────────────────────

    [Fact]
    public async Task OperatingCostPhase_OneBuildingInsufficient_OnlySuspendsThatBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-multi-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Multi Building Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        // Well-funded account.
        var richAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1111111111111111",
            CurrencyCode = "EUR",
            Balance = 100_000m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        // Empty account.
        var brokeAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "2222222222222222",
            CurrencyCode = "EUR",
            Balance = 0m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.AddRange(richAccount, brokeAccount);

        var buildingOk = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Rich Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = richAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        var buildingBroke = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Broke Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = brokeAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.AddRange(buildingOk, buildingBroke);

        // Both buildings have a manufacturing unit to incur operating costs.
        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingOk.Id, UnitType = UnitType.Manufacturing, GridX = 0, GridY = 0, Level = 1 },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingBroke.Id, UnitType = UnitType.Manufacturing, GridX = 0, GridY = 0, Level = 1 }
        );
        await db.SaveChangesAsync();

        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(buildingOk).ReloadAsync();
        await db.Entry(buildingBroke).ReloadAsync();

        // The well-funded building should NOT be suspended.
        Assert.False(buildingOk.IsSuspendedForFunds, "Rich factory should not be suspended.");

        // The empty-account building SHOULD be suspended.
        Assert.True(buildingBroke.IsSuspendedForFunds, "Broke factory should be suspended.");
        Assert.NotNull(buildingBroke.SuspendedReason);
        Assert.StartsWith("INSUFFICIENT_FUNDS:", buildingBroke.SuspendedReason);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static async Task<Guid> GetCurrentPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static Api.Engine.TickProcessor CreateTickProcessor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<Api.Engine.ITickPhase>>();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<Api.Engine.TickProcessor>.Instance;
        return new Api.Engine.TickProcessor(db, phases, logger);
    }
}
