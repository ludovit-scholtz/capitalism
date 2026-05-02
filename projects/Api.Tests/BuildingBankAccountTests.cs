using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

    [Fact]
    public async Task CompanyBankAccounts_ExcludesClosedAccounts()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-company-accounts-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Company Accounts Co",
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var activeAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 1_000m,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };

        var closedAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 0m,
            CreatedAtUtc = DateTime.UtcNow,
            ClosedAtTick = 10,
            ClosedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };

        db.BankAccounts.AddRange(activeAccount, closedAccount);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query CompanyBankAccounts($companyId: UUID!) {
              companyBankAccounts(companyId: $companyId) {
                id
                accountNumber
                currencyCode
                balance
              }
            }
            """,
            new { companyId = company.Id },
            token);

        var accounts = result.GetProperty("data").GetProperty("companyBankAccounts").EnumerateArray().ToList();
        var accountIds = accounts
            .Select(account => account.GetProperty("id").GetString())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet();

        Assert.Contains(activeAccount.Id.ToString(), accountIds);
        Assert.DoesNotContain(closedAccount.Id.ToString(), accountIds);
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
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var buildingAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            Balance = 0m,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };

        var fundingAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.AddRange(buildingAccount, fundingAccount);

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
            BankAccountId = buildingAccount.Id,
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

        // Total company liquidity is unchanged by internal account-to-account transfer.
        Assert.Equal(500_000m, fund.GetProperty("remainingCompanyCash").GetDecimal());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();

        var transferOut = await verifyDb.LedgerEntries
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.CompanyId == company.Id
                && entry.Category == LedgerCategory.BankAccountTransferOut
                && entry.BankAccountId == fundingAccount.Id);

        var transferIn = await verifyDb.LedgerEntries
            .AsNoTracking()
            .SingleAsync(entry =>
                entry.CompanyId == company.Id
                && entry.Category == LedgerCategory.BankAccountTransferIn
                && entry.BankAccountId == buildingAccount.Id);

        Assert.Equal(-10_000m, transferOut.Amount);
        Assert.Equal(10_000m, transferIn.Amount);
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
            new { input = new { companyId = company.Id, currencyCode = "CZK" } },
            token);

        var account = result.GetProperty("data").GetProperty("createCompanyBankAccount").GetProperty("account");
        Assert.Equal("CZK", account.GetProperty("currencyCode").GetString());
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

    [Fact]
    public async Task CreatePersonalBankAccount_WithValidCurrency_CreatesPersonalAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-personal-create-{Guid.NewGuid():N}@test.com");

        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation CreatePersonal($input: CreatePersonalBankAccountInput!) {
                createPersonalBankAccount(input: $input) {
                    account {
                        id
                        accountNumber
                        currencyCode
                        balance
                    }
                }
            }
            """,
            new { input = new { currencyCode = "CZK" } },
            token);

        Assert.False(result.TryGetProperty("errors", out _));
        var account = result.GetProperty("data").GetProperty("createPersonalBankAccount").GetProperty("account");
        Assert.Equal("CZK", account.GetProperty("currencyCode").GetString());
        Assert.Equal(0m, account.GetProperty("balance").GetDecimal());
        Assert.Equal(16, account.GetProperty("accountNumber").GetString()!.Length);
    }

    [Fact]
    public async Task CreatePersonalBankAccount_DuplicateCurrency_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-personal-dup-{Guid.NewGuid():N}@test.com");

        const string mutation = """
            mutation CreatePersonal($input: CreatePersonalBankAccountInput!) {
                createPersonalBankAccount(input: $input) {
                    account { id }
                }
            }
            """;
        var vars = new { input = new { currencyCode = "USD" } };

        await ExecuteGraphQlAsync(client, mutation, vars, token);

        var result = await ExecuteGraphQlAsync(client, mutation, vars, token);
        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("DUPLICATE_BANK_ACCOUNT", code);
    }

    [Fact]
    public async Task PlaceBuilding_AssignsCompanyCurrencyBankAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-place-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Placement Funding Co",
            Cash = 10_000_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation PB($i:PlaceBuildingInput!){placeBuilding(input:$i){id}}",
            new { i = new { companyId = company.Id, cityId = city.Id, type = "FACTORY", name = "Provisioned Factory" } },
            token);

        var buildingId = Guid.Parse(result.GetProperty("data").GetProperty("placeBuilding").GetProperty("id").GetString()!);

        var building = await db.Buildings.AsNoTracking().FirstAsync(candidate => candidate.Id == buildingId);
        Assert.NotNull(building.BankAccountId);

        var account = await db.BankAccounts.AsNoTracking().FirstAsync(candidate => candidate.Id == building.BankAccountId);
        Assert.Equal(company.Id, account.CompanyId);
        Assert.Equal(city.CurrencyCode, account.CurrencyCode);
    }

    [Fact]
    public async Task AppDbInitializer_AssignsSharedCompanyCurrencyAccountToExistingBuildingsWithoutAccounts()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-init-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Initializer Funding Co",
            Cash = 250_000m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };

        var buildingA = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Legacy Factory A",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 5,
            BuiltAtUtc = DateTime.UtcNow,
        };

        var buildingB = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.SalesShop,
            Name = "Legacy Shop B",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 3,
            BuiltAtUtc = DateTime.UtcNow,
        };

        db.Companies.Add(company);
        db.Buildings.AddRange(buildingA, buildingB);
        await db.SaveChangesAsync();

        var initializer = new AppDbInitializer(
            db,
            Options.Create(new SeedDataOptions
            {
                AdminEmail = "admin@building-bank-account-tests.local",
                AdminDisplayName = "Building Bank Account Test Admin",
                AdminPassword = "TestPassword123!"
            }),
            TestHelpers.CreateFallbackNbsService());

        await initializer.InitializeAsync();

        await db.Entry(buildingA).ReloadAsync();
        await db.Entry(buildingB).ReloadAsync();

        Assert.NotNull(buildingA.BankAccountId);
        Assert.Equal(buildingA.BankAccountId, buildingB.BankAccountId);

        var account = await db.BankAccounts.AsNoTracking().FirstAsync(candidate => candidate.Id == buildingA.BankAccountId);
        Assert.Equal(company.Id, account.CompanyId);
        Assert.Equal(city.CurrencyCode, account.CurrencyCode);
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

    // ── Suspension blocking production phases ─────────────────────────────────

    [Fact]
    public async Task SuspendedBuilding_ManufacturingPhase_DoesNotProduceGoods()
    {
        // When a building is suspended for insufficient funds, ManufacturingPhase
        // must skip it so no goods are produced that tick.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-mfg-sus-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Suspended Mfg Co",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        // Zero-balance bank account → building will be suspended this tick.
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "5555666677778888",
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
            Name = "Suspended Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        // Seed a product type with a recipe.
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");
        var chair = await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair");

        // Storage unit (inventory source) with wood.
        var storageUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Storage,
            GridX = 0,
            GridY = 0,
            Level = 1,
            LinkRight = true,  // feeds resources into the manufacturing unit on the right
        };
        db.BuildingUnits.Add(storageUnit);

        var mfgUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Manufacturing,
            ProductTypeId = chair.Id,
            GridX = 1,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(mfgUnit);

        // Seed enough wood so manufacturing could run if not suspended.
        var woodInventory = new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingUnitId = storageUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = 1000m,
            Quality = 0.8m,
        };
        db.Inventories.Add(woodInventory);

        await db.SaveChangesAsync();

        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // Building should be suspended.
        await db.Entry(building).ReloadAsync();
        Assert.True(building.IsSuspendedForFunds, "Building with zero balance should be suspended.");

        // Wood inventory should be UNCHANGED — no manufacturing happened.
        await db.Entry(woodInventory).ReloadAsync();
        Assert.Equal(1000m, woodInventory.Quantity, 2);

        // No chair inventory should have been produced.
        var chairInventory = await db.Inventories
            .Where(inv => inv.BuildingId == building.Id && inv.ProductTypeId == chair.Id)
            .ToListAsync();
        Assert.Empty(chairInventory);
    }

    [Fact]
    public async Task SuspendedBuilding_PurchasingPhase_SkipsPurchaseOrders()
    {
        // When a building is suspended, PurchasingPhase must not process purchasing
        // so no money is debited and no inventory changes occur.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-pur-sus-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Suspended Purchase Co",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        // Zero-balance bank account → building will be suspended.
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "4444333322221111",
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
            Name = "Suspended Purchase Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var purchaseUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Purchase,
            ResourceTypeId = wood.Id,
            MaxPrice = 50m,
            PurchaseSource = "EXCHANGE",
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(purchaseUnit);
        await db.SaveChangesAsync();

        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // Building should be suspended.
        await db.Entry(building).ReloadAsync();
        Assert.True(building.IsSuspendedForFunds, "Building with zero balance should be suspended.");

        // Bank account must remain at 0 (no purchases debited).
        await db.Entry(bankAccount).ReloadAsync();
        Assert.Equal(0m, bankAccount.Balance);

        // No wood inventory should have appeared.
        var woodInventory = await db.Inventories
            .Where(inv => inv.BuildingId == building.Id && inv.ResourceTypeId == wood.Id)
            .ToListAsync();
        Assert.Empty(woodInventory);
    }

    [Fact]
    public async Task SuspendedBuilding_AfterFunding_ManufacturingResumes()
    {
        // After a player funds the suspended building's bank account the building
        // should resume manufacturing in the next tick.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-resume-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Resume Mfg Co",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "8877665544332211",
            CurrencyCode = "EUR",
            Balance = 0m,     // starts at zero → suspended tick 1
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
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");
        var chair = await db.ProductTypes.FirstAsync(p => p.Name == "Wooden Chair");

        var storageUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Storage,
            GridX = 0,
            GridY = 0,
            Level = 1,
            LinkRight = true,  // feeds resources into the manufacturing unit on the right
        };
        db.BuildingUnits.Add(storageUnit);

        var mfgUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Manufacturing,
            ProductTypeId = chair.Id,
            GridX = 1,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(mfgUnit);

        var woodInventory = new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingUnitId = storageUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = 1000m,
            Quality = 0.8m,
        };
        db.Inventories.Add(woodInventory);
        await db.SaveChangesAsync();

        var processor = CreateTickProcessor(scope);

        // Tick 1: building suspended because bank account is empty.
        await processor.ProcessTickAsync();
        await db.Entry(building).ReloadAsync();
        Assert.True(building.IsSuspendedForFunds, "Tick 1: building should be suspended.");
        await db.Entry(woodInventory).ReloadAsync();
        Assert.Equal(1000m, woodInventory.Quantity, 2);

        // Simulate player funding the account.
        bankAccount.Balance = 500_000m;
        await db.SaveChangesAsync();

        // Tick 2: balance now sufficient, building should resume.
        await processor.ProcessTickAsync();
        await db.Entry(building).ReloadAsync();
        Assert.False(building.IsSuspendedForFunds, "Tick 2: building should be active after funding.");
        Assert.True(building.SuspendedReason is null or { Length: 0 } or "MISSING_BANK_ACCOUNT"
            || !building.SuspendedReason!.StartsWith("INSUFFICIENT_FUNDS"),
            $"Tick 2: expected no INSUFFICIENT_FUNDS reason, got: {building.SuspendedReason}");

        // Manufacturing should have run: some chair inventory should have been produced
        // OR wood should have decreased (depending on recipe quantities vs batch size).
        var chairInventory = await db.Inventories
            .Where(inv => inv.BuildingId == building.Id && inv.ProductTypeId == chair.Id)
            .ToListAsync();
        await db.Entry(woodInventory).ReloadAsync();

        // At least one of: wood decreased or chair appeared — proves manufacturing ran.
        Assert.True(
            chairInventory.Count > 0 || woodInventory.Quantity < 1000m,
            "Tick 2: manufacturing should have produced goods after the building was re-funded.");
    }

    [Fact]
    public async Task SuspendedBuilding_LedgerEntryRecorded_ForInsufficientFunds()
    {
        // When a building is suspended, OperatingCostPhase should record a zero-amount
        // ledger entry with category OTHER explaining the block.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"bba-ledger-{Guid.NewGuid():N}@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var playerId = await GetCurrentPlayerIdAsync(client, token);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Ledger Suspension Co",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1212343456567878",
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
            Name = "Ledger Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            PowerConsumption = 2,
            PowerStatus = PowerStatus.Powered,
            BankAccountId = bankAccount.Id,
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

        var gameState = await db.GameStates.SingleAsync();
        var tickBefore = gameState.CurrentTick;

        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        // A suspension ledger entry should have been recorded.
        var suspensionEntry = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id
                     && e.BuildingId == building.Id
                     && e.Category == "OTHER"
                     && e.Description.Contains("suspended"))
            .FirstOrDefaultAsync();

        Assert.NotNull(suspensionEntry);
        Assert.Equal(0m, suspensionEntry.Amount);
        Assert.Equal(tickBefore + 1, suspensionEntry.RecordedAtTick);
        Assert.Contains("insufficient funds", suspensionEntry.Description, StringComparison.OrdinalIgnoreCase);
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
