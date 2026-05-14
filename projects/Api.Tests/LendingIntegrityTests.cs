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

public sealed class LendingIntegrityTests
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
        return (await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email,
        string displayName)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetCurrentPlayerIdAsync(HttpClient client, string token)
    {
        var me = await ExecuteGraphQlAsync(client, "query { me { id } }", token: token);
        return Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static TickProcessor CreateTickProcessor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance);
    }

    [Fact]
    public async Task AcceptLoan_DefaultedOutstandingPrincipal_CountsAgainstLendingCapacity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var lenderToken = await RegisterAndGetTokenAsync(client, $"cap-def-lender-{Guid.NewGuid():N}@test.com", "CapDefLender");
        var borrowerToken = await RegisterAndGetTokenAsync(client, $"cap-def-borrower-{Guid.NewGuid():N}@test.com", "CapDefBorrower");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lenderPlayerId = await GetCurrentPlayerIdAsync(client, lenderToken);
        var borrowerPlayerId = await GetCurrentPlayerIdAsync(client, borrowerToken);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "CapDefLenderCo" };
        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "CapDefBorrowerCo" };
        db.Companies.AddRange(lenderCompany, borrowerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "CapDefBank",
            BaseCapitalDeposited = true,
            TotalDeposits = 2_000m,
            LendingInterestRatePercent = 10m,
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        };
        db.Buildings.Add(bank);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
        });
        var collateral = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "CapDefCollateral",
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
        };
        db.Buildings.Add(collateral);

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 2_000m,
            TotalCapacity = 2_000m,
            UsedCapacity = 1_700m,
            DurationTicks = 1440L,
            IsActive = true,
            CreatedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);
        var gameState = await db.GameStates.FirstDeterministicAsync();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 1_700m,
            RemainingPrincipal = 1_700m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 1440L,
            NextPaymentTick = gameState.CurrentTick + 720L,
            PaymentAmount = 100m,
            PaymentsMade = 0,
            TotalPayments = 2,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            AcceptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Accept($input: AcceptLoanInput!) { acceptLoan(input: $input) { id } }",
            new { input = new { loanOfferId = bank.Id.ToString(), borrowerCompanyId = borrowerCompany.Id.ToString(), principalAmount = 1_200m, collateralBuildingId = collateral.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("INSUFFICIENT_CAPACITY", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AcceptLoan_LenderMustHaveLoanCurrencyBalance_NoCrossCurrencyFallback()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var lenderToken = await RegisterAndGetTokenAsync(client, $"loan-currency-lender-{Guid.NewGuid():N}@test.com", "LoanCurrencyLender");
        var borrowerToken = await RegisterAndGetTokenAsync(client, $"loan-currency-borrower-{Guid.NewGuid():N}@test.com", "LoanCurrencyBorrower");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lenderPlayerId = await GetCurrentPlayerIdAsync(client, lenderToken);
        var borrowerPlayerId = await GetCurrentPlayerIdAsync(client, borrowerToken);
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "LoanCurrencyLenderCo" };
        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "LoanCurrencyBorrowerCo" };
        db.Companies.AddRange(lenderCompany, borrowerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "LoanCurrencyBank",
            BaseCapitalDeposited = true,
            TotalDeposits = 500_000m,
            LendingInterestRatePercent = 8m,
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        };
        db.Buildings.Add(bank);
        // Lender has only CZK account while loan currency is EUR.
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "CZK",
            Balance = 2_000_000m,
        });
        var collateral = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "LoanCurrencyCollateral",
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
        };
        db.Buildings.Add(collateral);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Accept($input: AcceptLoanInput!) { acceptLoan(input: $input) { id } }",
            new { input = new { loanOfferId = bank.Id.ToString(), borrowerCompanyId = borrowerCompany.Id.ToString(), principalAmount = 10_000m, collateralBuildingId = collateral.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("LENDER_INSUFFICIENT_FUNDS", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task LoanRepayment_TickPhase_DoesNotUseForeignCurrencyFallbackForScheduledDebit()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var lenderPlayer = new Player { Id = Guid.NewGuid(), Email = $"tick-lender-{Guid.NewGuid():N}@test.com", DisplayName = "TickLender", PasswordHash = "hash" };
        var borrowerPlayer = new Player { Id = Guid.NewGuid(), Email = $"tick-borrower-{Guid.NewGuid():N}@test.com", DisplayName = "TickBorrower", PasswordHash = "hash" };
        db.Players.AddRange(lenderPlayer, borrowerPlayer);

        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayer.Id, Name = "TickLenderCo" };
        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayer.Id, Name = "TickBorrowerCo" };
        db.Companies.AddRange(lenderCompany, borrowerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "TickBank",
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
            LendingInterestRatePercent = 10m,
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        };
        db.Buildings.Add(bank);

        var lenderEurAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 100_000m,
        };
        var borrowerEurAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 0m,
        };
        var borrowerCzkAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "CZK",
            Balance = 5_000_000m,
        };
        db.BankAccounts.AddRange(lenderEurAccount, borrowerEurAccount, borrowerCzkAccount);

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 10_000m,
            TotalCapacity = 100_000m,
            UsedCapacity = 10_000m,
            DurationTicks = 10L,
            IsActive = false,
            CreatedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);

        var gameState = await db.GameStates.FirstDeterministicAsync();
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BorrowerBankAccountId = borrowerEurAccount.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 10_000m,
            RemainingPrincipal = 10_000m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 10L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 10L,
            NextPaymentTick = gameState.CurrentTick + 1L,
            PaymentAmount = 1_500m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Active,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        // Borrower has only CZK funds available; EUR repayment account is empty.
        var czkBefore = borrowerCzkAccount.Balance;
        gameState.CurrentTick = loan.NextPaymentTick - 1;
        await db.SaveChangesAsync();

        var processor = CreateTickProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(loan).ReloadAsync();
        await db.Entry(borrowerCzkAccount).ReloadAsync();
        Assert.True(loan.Status == LoanStatus.Overdue || loan.Status == LoanStatus.Defaulted);
        Assert.Equal(czkBefore, borrowerCzkAccount.Balance);
    }

    [Fact]
    public async Task RepayLoanDebt_DoesNotUseForeignCurrencyFallback()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var lenderToken = await RegisterAndGetTokenAsync(client, $"manual-lender-{Guid.NewGuid():N}@test.com", "ManualLender");
        var borrowerToken = await RegisterAndGetTokenAsync(client, $"manual-borrower-{Guid.NewGuid():N}@test.com", "ManualBorrower");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var lenderPlayerId = await GetCurrentPlayerIdAsync(client, lenderToken);
        var borrowerPlayerId = await GetCurrentPlayerIdAsync(client, borrowerToken);
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "ManualLenderCo" };
        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "ManualBorrowerCo" };
        db.Companies.AddRange(lenderCompany, borrowerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "ManualBank",
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
            LendingInterestRatePercent = 8m,
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        };
        db.Buildings.Add(bank);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 100_000m,
        });

        var borrowerEurAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "EUR",
            Balance = 0m,
        };
        var borrowerCzkAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = "CZK",
            Balance = 8_000_000m,
        };
        db.BankAccounts.AddRange(borrowerEurAccount, borrowerCzkAccount);

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 8m,
            MaxPrincipalPerLoan = 50_000m,
            TotalCapacity = 100_000m,
            UsedCapacity = 50_000m,
            DurationTicks = 1440L,
            IsActive = false,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BorrowerBankAccountId = borrowerEurAccount.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 50_000m,
            RemainingPrincipal = 50_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 1440L,
            NextPaymentTick = gameState.CurrentTick + 10,
            PaymentAmount = 5_000m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 5,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-2),
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Repay($input: RepayLoanDebtInput!) { repayLoanDebt(input: $input) { id } }",
            new { input = new { loanId = loan.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("INSUFFICIENT_FUNDS", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CloseCompanyBankAccount_DefaultedLoanRepaymentAccount_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var borrowerToken = await RegisterAndGetTokenAsync(client, $"close-defaulted-{Guid.NewGuid():N}@test.com", "CloseDefaulted");
        var lenderToken = await RegisterAndGetTokenAsync(client, $"close-defaulted-lender-{Guid.NewGuid():N}@test.com", "CloseDefaultedLender");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var borrowerPlayerId = await GetCurrentPlayerIdAsync(client, borrowerToken);
        var lenderPlayerId = await GetCurrentPlayerIdAsync(client, lenderToken);
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "CloseDefaultedBorrowerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "CloseDefaultedLenderCo" };
        db.Companies.AddRange(borrowerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "CloseDefaultedBank",
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
            LendingInterestRatePercent = 8m,
            Latitude = city.Latitude,
            Longitude = city.Longitude,
        };
        db.Buildings.Add(bank);

        var repaymentAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            Balance = 0m,
        };
        db.BankAccounts.Add(repaymentAccount);

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 8m,
            MaxPrincipalPerLoan = 10_000m,
            TotalCapacity = 100_000m,
            UsedCapacity = 10_000m,
            DurationTicks = 1440L,
            IsActive = true,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BorrowerBankAccountId = repaymentAccount.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 10_000m,
            RemainingPrincipal = 10_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 1440L,
            NextPaymentTick = gameState.CurrentTick + 10L,
            PaymentAmount = 1_000m,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 2,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            "mutation CloseAcct($input: CloseCompanyBankAccountInput!) { closeCompanyBankAccount(input: $input) { id } }",
            new { input = new { bankAccountId = repaymentAccount.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("REPAYMENT_ACCOUNT_HAS_UNPAID_LOANS", errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
