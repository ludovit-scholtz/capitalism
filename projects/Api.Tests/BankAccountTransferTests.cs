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
/// Integration tests for the <c>transferFunds</c> mutation, which lets a player move money
/// between two bank accounts they own (same currency only).
/// </summary>
public sealed class BankAccountTransferTests
{
    private const string TransferFundsMutation = """
        mutation Transfer($input: TransferFundsInput!) {
            transferFunds(input: $input) {
                amount
                currencyCode
                fromAccount { id accountNumber currencyCode balance companyId companyName }
                toAccount { id accountNumber currencyCode balance companyId companyName }
            }
        }
        """;

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

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName = "Transfer Tester", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    /// <summary>
    /// Seeds a company plus one bank account in the named currency owned by the given player.
    /// </summary>
    private static async Task<(Company company, BankAccount account)> SeedAccountAsync(
        AppDbContext db,
        Guid playerId,
        string companyName,
        string currencyCode,
        decimal balance)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = companyName,
            Cash = 0m,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = currencyCode,
            Balance = balance,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        return (company, account);
    }

    [Fact]
    public async Task TransferFunds_SameCurrencySufficientFunds_TransfersAndRecordsLedgerEntries()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-ok-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (company, fromAccount) = await SeedAccountAsync(db, playerId, "Source Co", "EUR", 5_000m);
        var toAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = 1_000m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(toAccount);

        var player = await db.Players.FirstAsync(candidate => candidate.Id == playerId);
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = fromAccount.Id,
                    toBankAccountId = toAccount.Id,
                    amount = 1_500m,
                    description = "Operating cash sweep",
                },
            },
            token);

        Assert.False(
            result.TryGetProperty("errors", out _),
            "Expected no errors but got: " + result.ToString());

        var transfer = result.GetProperty("data").GetProperty("transferFunds");
        Assert.Equal(1_500m, transfer.GetProperty("amount").GetDecimal());
        Assert.Equal("EUR", transfer.GetProperty("currencyCode").GetString());
        Assert.Equal(3_500m, transfer.GetProperty("fromAccount").GetProperty("balance").GetDecimal());
        Assert.Equal(2_500m, transfer.GetProperty("toAccount").GetProperty("balance").GetDecimal());

        // Verify persisted balances in the DB.
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbFrom = await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == fromAccount.Id);
        var dbTo = await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == toAccount.Id);
        Assert.Equal(3_500m, dbFrom.Balance);
        Assert.Equal(2_500m, dbTo.Balance);

        // Verify ledger entries on both companies.
        var fromEntry = await verifyDb.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.BankAccountTransferOut)
            .SingleAsync();
        Assert.Equal(-1_500m, fromEntry.Amount);
        Assert.Contains("Operating cash sweep", fromEntry.Description);

        var toEntry = await verifyDb.LedgerEntries
            .AsNoTracking()
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.BankAccountTransferIn)
            .SingleAsync();
        Assert.Equal(1_500m, toEntry.Amount);
        Assert.Contains("Operating cash sweep", toEntry.Description);
    }

    [Fact]
    public async Task TransferFunds_InsufficientFunds_ReturnsInsufficientFundsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-low-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (company, fromAccount) = await SeedAccountAsync(db, playerId, "Tiny Co", "EUR", 100m);
        var toAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = "EUR",
            Balance = 0m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(toAccount);

        var player = await db.Players.FirstAsync(candidate => candidate.Id == playerId);
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = fromAccount.Id,
                    toBankAccountId = toAccount.Id,
                    amount = 500m,
                    description = (string?)null,
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("INSUFFICIENT_FUNDS", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_CurrencyMismatch_ReturnsCurrencyMismatchError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-fx-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (company, eurAccount) = await SeedAccountAsync(db, playerId, "EUR Co", "EUR", 10_000m);
        var czkAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L).ToString("D16"),
            CurrencyCode = "CZK",
            Balance = 0m,
            CompanyId = company.Id,
            IsGovernmentAccount = false,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(czkAccount);

        var player = await db.Players.FirstAsync(candidate => candidate.Id == playerId);
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = company.Id;
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = eurAccount.Id,
                    toBankAccountId = czkAccount.Id,
                    amount = 100m,
                    description = (string?)null,
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("CURRENCY_MISMATCH", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_DestinationOwnedByAnotherPlayer_ReturnsToAccountNotFoundError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAndGetTokenAsync(client, $"xfer-owner-{Guid.NewGuid():N}@test.com");
        var ownerId = await GetPlayerIdAsync(client, ownerToken);

        var strangerToken = await RegisterAndGetTokenAsync(client, $"xfer-stranger-{Guid.NewGuid():N}@test.com");
        var strangerId = await GetPlayerIdAsync(client, strangerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (ownerCompany, ownerAccount) = await SeedAccountAsync(db, ownerId, "Owner Co", "EUR", 5_000m);
        var (_, strangerAccount) = await SeedAccountAsync(db, strangerId, "Stranger Co", "EUR", 0m);

        var ownerPlayer = await db.Players.FirstAsync(candidate => candidate.Id == ownerId);
        ownerPlayer.ActiveAccountType = AccountContextType.Company;
        ownerPlayer.ActiveCompanyId = ownerCompany.Id;
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = ownerAccount.Id,
                    toBankAccountId = strangerAccount.Id,
                    amount = 100m,
                    description = (string?)null,
                },
            },
            ownerToken);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("FORBIDDEN", error.GetProperty("extensions").GetProperty("code").GetString());

        // Verify owner balance untouched.
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbOwner = await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == ownerAccount.Id);
        Assert.Equal(5_000m, dbOwner.Balance);
    }

    [Fact]
    public async Task TransferFunds_SameAccountId_ReturnsSameAccountError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-same-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, account) = await SeedAccountAsync(db, playerId, "Solo Co", "EUR", 1_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = account.Id,
                    toBankAccountId = account.Id,
                    amount = 50m,
                    description = (string?)null,
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("SAME_ACCOUNT", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_NonPositiveAmount_ReturnsInvalidAmountError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-amt-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, fromAccount) = await SeedAccountAsync(db, playerId, "From Co", "EUR", 1_000m);
        var (_, toAccount) = await SeedAccountAsync(db, playerId, "To Co", "EUR", 0m);

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = fromAccount.Id,
                    toBankAccountId = toAccount.Id,
                    amount = 0m,
                    description = (string?)null,
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("INVALID_AMOUNT", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_PersonContext_CompanyAccounts_ReturnsContextMismatch()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-person-context-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, fromAccount) = await SeedAccountAsync(db, playerId, "Company A", "EUR", 1_000m);
        var (_, toAccount) = await SeedAccountAsync(db, playerId, "Company B", "EUR", 1_000m);

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = fromAccount.Id,
                    toBankAccountId = toAccount.Id,
                    amount = 100m,
                    description = "Invalid cross-context transfer",
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("ACCOUNT_CONTEXT_MISMATCH", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_CompanyContext_DifferentCompanyAccounts_ReturnsContextMismatch()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, $"xfer-company-context-{Guid.NewGuid():N}@test.com");
        var playerId = await GetPlayerIdAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (activeCompany, activeAccount) = await SeedAccountAsync(db, playerId, "Active Co", "EUR", 1_000m);
        var (_, otherAccount) = await SeedAccountAsync(db, playerId, "Other Co", "EUR", 1_000m);

        var player = await db.Players.FirstAsync(candidate => candidate.Id == playerId);
        player.ActiveAccountType = AccountContextType.Company;
        player.ActiveCompanyId = activeCompany.Id;
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = activeAccount.Id,
                    toBankAccountId = otherAccount.Id,
                    amount = 100m,
                    description = "Cross-company transfer should fail",
                },
            },
            token);

        var error = result.GetProperty("errors")[0];
        Assert.Equal("ACCOUNT_CONTEXT_MISMATCH", error.GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task TransferFunds_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(
            client,
            TransferFundsMutation,
            new
            {
                input = new
                {
                    fromBankAccountId = Guid.NewGuid(),
                    toBankAccountId = Guid.NewGuid(),
                    amount = 100m,
                    description = (string?)null,
                },
            });

        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0,
            "Expected an authentication error.");
    }
}
