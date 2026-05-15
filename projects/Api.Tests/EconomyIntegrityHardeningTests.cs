using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Regression tests for economy integrity hardening:
/// building offer floor enforcement, defaulted-collateral lender-safe sales,
/// currency-scoped loan lifecycle, repayment-account closure guard,
/// pledged-building edit freeze, offer escrow, and defaulted-principal
/// lending-capacity inclusion.
/// </summary>
public sealed class EconomyIntegrityHardeningTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // GraphQL helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
            req.Headers.Authorization = new("Bearer", token);
        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAsync(
        HttpClient client, string email, string displayName = "Test User")
    {
        var result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(HttpClient client, string token)
    {
        var me = await ExecAsync(client, "query { me { id } }", token: token);
        return Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private static string NewAccountNumber() => Guid.NewGuid().ToString("N")[..16];

    // ──────────────────────────────────────────────────────────────────────────
    // 1. AcceptBuildingOffer_BelowFloor_ReturnsError
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AcceptBuildingOffer_BelowFloor_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-seller-floor-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-buyer-floor-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihSellerFloorCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihBuyerFloorCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihFloorFactory",
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
            Level = 1,
        };
        db.Buildings.Add(building);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000_000m,
            CompanyId = buyerCompany.Id,
        });
        await db.SaveChangesAsync();

        // Compute minimum sale floor
        var valuation = await BuildingMarketValuationCalculator.CalculateAsync(db, building);
        var minimumFloor = valuation.MinimumSalePrice;

        // List building for sale above floor
        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId = building.Id, isForSale = true, askingPrice = minimumFloor } },
            sellerToken);

        // Buyer makes an offer below the floor
        var belowFloor = decimal.Round(minimumFloor * 0.85m, 2, MidpointRounding.AwayFromZero);
        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = building.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = belowFloor } },
            buyerToken);

        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        // Seller tries to accept — must be rejected at the floor check
        var acceptResult = await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        var errors = acceptResult.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("OFFER_BELOW_FLOOR", code);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 2. DefaultedCollateralSale_FullLenderRecovery
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DefaultedCollateralSale_FullLenderRecovery()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-def-seller-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-def-buyer-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-def-lender-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihDefSellerCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihDefBuyerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihDefLenderCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihDefBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        // Collateral building owned by seller
        var collateralBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihDefCollateral",
            Latitude = city.Latitude + 0.02,
            Longitude = city.Longitude + 0.02,
            Level = 1,
            IsForSale = true,
            AskingPrice = 500_000m,
        };
        db.Buildings.Add(collateralBuilding);

        var lenderAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
            CompanyId = lenderCompany.Id,
        };
        db.BankAccounts.Add(lenderAccount);

        var buyerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 2_000_000m,
            CompanyId = buyerCompany.Id,
        };
        db.BankAccounts.Add(buyerAccount);

        // Set up a defaulted loan against the collateral building
        const decimal outstandingPrincipal = 100_000m;
        var loanOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = outstandingPrincipal,
            TotalCapacity = outstandingPrincipal,
            UsedCapacity = outstandingPrincipal,
            DurationTicks = 1440L,
            IsActive = false,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(loanOffer);

        var defaultedLoan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = sellerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = outstandingPrincipal,
            RemainingPrincipal = outstandingPrincipal,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick - 100,
            DueTick = gameState.CurrentTick + 1000,
            NextPaymentTick = gameState.CurrentTick + 10,
            PaymentAmount = 10_000m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 5,
            CollateralBuildingId = collateralBuilding.Id,
            CollateralAppraisedValue = 200_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-5),
        };
        db.Loans.Add(defaultedLoan);
        await db.SaveChangesAsync();

        var lenderBalanceBefore = lenderAccount.Balance;

        // Buyer makes an offer above the outstanding lien
        const decimal salePrice = 500_000m;
        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = collateralBuilding.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = salePrice } },
            buyerToken);

        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        // Seller accepts the offer
        var acceptResult = await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id companyId } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        Assert.False(acceptResult.TryGetProperty("errors", out _),
            $"Accept failed: {acceptResult}");

        // Reload lender account to verify full recovery
        await db.Entry(lenderAccount).ReloadAsync();
        await db.Entry(defaultedLoan).ReloadAsync();

        Assert.Equal(LoanStatus.Repaid, defaultedLoan.Status);
        Assert.Equal(0m, defaultedLoan.RemainingPrincipal);
        // Lender should have received the full outstanding principal
        Assert.Equal(lenderBalanceBefore + outstandingPrincipal, lenderAccount.Balance);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 3. FriendlyRepurchaseBelowLien_Blocked
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FriendlyRepurchaseBelowLien_Blocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-lien-seller-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-lien-buyer-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-lien-lender-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihLienSellerCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihLienBuyerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihLienLenderCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihLienBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        var collateralBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihLienCollateral",
            Latitude = city.Latitude + 0.03,
            Longitude = city.Longitude + 0.03,
            Level = 1,
            IsForSale = true,
            AskingPrice = 50_000m, // below lien
        };
        db.Buildings.Add(collateralBuilding);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 2_000_000m,
            CompanyId = buyerCompany.Id,
        });

        // Outstanding defaulted loan of 200,000 EUR
        const decimal outstandingLien = 200_000m;
        var loanOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = outstandingLien,
            TotalCapacity = outstandingLien,
            UsedCapacity = outstandingLien,
            DurationTicks = 1440L,
            IsActive = false,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(loanOffer);
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = sellerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = outstandingLien,
            RemainingPrincipal = outstandingLien,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick - 50,
            DueTick = gameState.CurrentTick + 1000,
            NextPaymentTick = gameState.CurrentTick + 5,
            PaymentAmount = 20_000m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 3,
            CollateralBuildingId = collateralBuilding.Id,
            CollateralAppraisedValue = 300_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-3),
        });
        await db.SaveChangesAsync();

        // Make offer below the lien
        const decimal belowLien = 50_000m;
        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = collateralBuilding.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = belowLien } },
            buyerToken);

        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        // Accept should fail: sale proceeds below the outstanding lien
        var acceptResult = await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        var errors = acceptResult.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("COLLATERAL_LIEN_UNDERFUNDED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 4. LoanRepayment_WrongCurrency_ReturnsMismatchError
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoanRepayment_WrongCurrency_ReturnsMismatchError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var borrowerToken = await RegisterAsync(client, $"eih-wc-borrower-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-wc-lender-{Guid.NewGuid():N}@test.com");

        var borrowerPlayerId = await GetPlayerIdAsync(client, borrowerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava"); // EUR city
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "EihWcBorrowerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihWcLenderCo" };
        db.Companies.AddRange(borrowerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihWcBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        // Borrower has ONLY a CZK account (wrong currency vs EUR loan)
        var wrongCurrencyAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = "CZK",
            Balance = 5_000_000m,
            CompanyId = borrowerCompany.Id,
        };
        db.BankAccounts.Add(wrongCurrencyAccount);

        var loanOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 8m,
            MaxPrincipalPerLoan = 10_000m,
            TotalCapacity = 100_000m,
            UsedCapacity = 10_000m,
            DurationTicks = 1440L,
            IsActive = false,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(loanOffer);

        // EUR loan (bank city = Bratislava = EUR)
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 10_000m,
            RemainingPrincipal = 10_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick - 10,
            DueTick = gameState.CurrentTick + 1440,
            NextPaymentTick = gameState.CurrentTick + 5,
            PaymentAmount = 1_000m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 2,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-2),
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        // Try to repay using the wrong-currency (CZK) account explicitly
        var result = await ExecAsync(client,
            "mutation R($input: RepayLoanDebtInput!) { repayLoanDebt(input: $input) { id } }",
            new { input = new { loanId = loan.Id.ToString(), repaymentBankAccountId = wrongCurrencyAccount.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("CURRENCY_MISMATCH",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 5. RepaymentAccountClosure_WithActiveLoan_Blocked
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RepaymentAccountClosure_WithActiveLoan_Blocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var borrowerToken = await RegisterAsync(client, $"eih-active-loan-borrower-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-active-loan-lender-{Guid.NewGuid():N}@test.com");

        var borrowerPlayerId = await GetPlayerIdAsync(client, borrowerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "EihActiveLoanBorrowerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihActiveLoanLenderCo" };
        db.Companies.AddRange(borrowerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihActiveLoanBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        var repaymentAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 0m, // zero so closure would otherwise proceed
            CompanyId = borrowerCompany.Id,
        };
        db.BankAccounts.Add(repaymentAccount);

        var loanOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 8m,
            MaxPrincipalPerLoan = 5_000m,
            TotalCapacity = 50_000m,
            UsedCapacity = 5_000m,
            DurationTicks = 1440L,
            IsActive = true,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(loanOffer);

        // ACTIVE loan against the repayment account
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BorrowerBankAccountId = repaymentAccount.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 5_000m,
            RemainingPrincipal = 5_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 1440,
            NextPaymentTick = gameState.CurrentTick + 10,
            PaymentAmount = 500m,
            TotalPayments = 10,
            Status = LoanStatus.Active,
            AcceptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Attempt to close the repayment account
        var result = await ExecAsync(client,
            "mutation C($input: CloseCompanyBankAccountInput!) { closeCompanyBankAccount(input: $input) { id } }",
            new { input = new { bankAccountId = repaymentAccount.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("REPAYMENT_ACCOUNT_HAS_UNPAID_LOANS",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 6. RepaymentAccountClosure_WithDefaultedLoan_Blocked
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RepaymentAccountClosure_WithDefaultedLoan_Blocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var borrowerToken = await RegisterAsync(client, $"eih-def-loan-borrower-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-def-loan-lender-{Guid.NewGuid():N}@test.com");

        var borrowerPlayerId = await GetPlayerIdAsync(client, borrowerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "EihDefLoanBorrowerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihDefLoanLenderCo" };
        db.Companies.AddRange(borrowerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihDefLoanBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        var repaymentAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 0m, // zero balance — would normally allow closure
            CompanyId = borrowerCompany.Id,
        };
        db.BankAccounts.Add(repaymentAccount);

        var loanOffer = new LoanOffer
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
        db.LoanOffers.Add(loanOffer);

        // DEFAULTED loan against the repayment account
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BorrowerBankAccountId = repaymentAccount.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 10_000m,
            RemainingPrincipal = 10_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick - 20,
            DueTick = gameState.CurrentTick + 1000,
            NextPaymentTick = gameState.CurrentTick + 5,
            PaymentAmount = 1_000m,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            MissedPayments = 3,
            DefaultedAtTick = gameState.CurrentTick - 3,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-2),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation C($input: CloseCompanyBankAccountInput!) { closeCompanyBankAccount(input: $input) { id } }",
            new { input = new { bankAccountId = repaymentAccount.Id.ToString() } },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("REPAYMENT_ACCOUNT_HAS_UNPAID_LOANS",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 7. PledgedBuildingEdit_Blocked
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PledgedBuildingEdit_Blocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, $"eih-pledged-owner-{Guid.NewGuid():N}@test.com");
        var lenderToken = await RegisterAsync(client, $"eih-pledged-lender-{Guid.NewGuid():N}@test.com");

        var ownerPlayerId = await GetPlayerIdAsync(client, ownerToken);
        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var ownerCompany = new Company { Id = Guid.NewGuid(), PlayerId = ownerPlayerId, Name = "EihPledgedOwnerCo" };
        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihPledgedLenderCo" };
        db.Companies.AddRange(ownerCompany, lenderCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihPledgedBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 1_000_000m,
        };
        db.Buildings.Add(bank);

        // Factory building pledged as collateral
        var pledgedBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = ownerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihPledgedFactory",
            Latitude = city.Latitude + 0.05,
            Longitude = city.Longitude + 0.05,
            Level = 1,
        };
        db.Buildings.Add(pledgedBuilding);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
            CompanyId = ownerCompany.Id,
        });

        var loanOffer = new LoanOffer
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
        db.LoanOffers.Add(loanOffer);

        // Active loan with the building as collateral
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = ownerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 50_000m,
            RemainingPrincipal = 50_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 1440,
            NextPaymentTick = gameState.CurrentTick + 10,
            PaymentAmount = 5_000m,
            TotalPayments = 10,
            Status = LoanStatus.Active,
            CollateralBuildingId = pledgedBuilding.Id,
            CollateralAppraisedValue = 100_000m,
            AcceptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Try to store a building configuration (edit the pledged building)
        // Using an empty units list to trigger the collateral check before any validation
        var result = await ExecAsync(client,
            """
            mutation SC($input: StoreBuildingConfigurationInput!) {
                storeBuildingConfiguration(input: $input) { id }
            }
            """,
            new { input = new { buildingId = pledgedBuilding.Id, units = Array.Empty<object>() } },
            ownerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("BUILDING_IS_PLEDGED_COLLATERAL",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 8. MakeOffer_InsufficientFunds_Blocked
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MakeOffer_InsufficientFunds_Blocked()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-insuf-seller-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-insuf-buyer-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihInsufSellerCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihInsufBuyerCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihInsufFactory",
            Latitude = city.Latitude + 0.06,
            Longitude = city.Longitude + 0.06,
            Level = 1,
            IsForSale = true,
            AskingPrice = 1_000_000m,
        };
        db.Buildings.Add(building);

        // Buyer has only 100 EUR — not enough
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 100m,
            CompanyId = buyerCompany.Id,
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId = building.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = 500_000m } },
            buyerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("INSUFFICIENT_FUNDS",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 9. MakeOffer_EscrowDeducted_BalanceReduced
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MakeOffer_EscrowDeducted_BalanceReduced()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-esc-seller-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-esc-buyer-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihEscSellerCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihEscBuyerCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihEscFactory",
            Latitude = city.Latitude + 0.07,
            Longitude = city.Longitude + 0.07,
            Level = 1,
            IsForSale = true,
            AskingPrice = 300_000m,
        };
        db.Buildings.Add(building);

        const decimal initialBalance = 1_000_000m;
        var buyerAccountId = Guid.NewGuid();
        db.BankAccounts.Add(new BankAccount
        {
            Id = buyerAccountId,
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = initialBalance,
            CompanyId = buyerCompany.Id,
        });
        await db.SaveChangesAsync();

        const decimal offerAmount = 200_000m;
        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id escrowAmount } }",
            new { input = new { buildingId = building.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = offerAmount } },
            buyerToken);

        Assert.False(offerResult.TryGetProperty("errors", out _), $"Offer failed: {offerResult}");
        var escrowAmount = offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("escrowAmount").GetDecimal();
        Assert.Equal(offerAmount, escrowAmount);

        // Verify buyer's account balance was reduced by the escrowed amount
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buyerAccount = await verifyDb.BankAccounts.FindAsync(buyerAccountId);
        Assert.NotNull(buyerAccount);
        Assert.Equal(initialBalance - offerAmount, buyerAccount.Balance);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 10. CancelOffer_EscrowReleased
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOffer_EscrowReleased()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, $"eih-cancel-seller-{Guid.NewGuid():N}@test.com");
        var buyerToken = await RegisterAsync(client, $"eih-cancel-buyer-{Guid.NewGuid():N}@test.com");

        var sellerPlayerId = await GetPlayerIdAsync(client, sellerToken);
        var buyerPlayerId = await GetPlayerIdAsync(client, buyerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "EihCancelSellerCo" };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "EihCancelBuyerCo" };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihCancelFactory",
            Latitude = city.Latitude + 0.08,
            Longitude = city.Longitude + 0.08,
            Level = 1,
            IsForSale = true,
            AskingPrice = 400_000m,
        };
        db.Buildings.Add(building);

        const decimal initialBalance = 800_000m;
        var buyerAccountId = Guid.NewGuid();
        db.BankAccounts.Add(new BankAccount
        {
            Id = buyerAccountId,
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = initialBalance,
            CompanyId = buyerCompany.Id,
        });
        await db.SaveChangesAsync();

        // Make an offer — this escrows the funds
        const decimal offerAmount = 250_000m;
        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = building.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = offerAmount } },
            buyerToken);

        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        // Verify escrow was deducted
        await using (var midScope = factory.Services.CreateAsyncScope())
        {
            var midDb = midScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var buyerAcctMid = await midDb.BankAccounts.FindAsync(buyerAccountId);
            Assert.Equal(initialBalance - offerAmount, buyerAcctMid!.Balance);
        }

        // Seller cancels the offer
        var cancelResult = await ExecAsync(client,
            "mutation C($input: CancelBuildingOfferInput!) { cancelBuildingOffer(input: $input) { id status } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        Assert.False(cancelResult.TryGetProperty("errors", out _), $"Cancel failed: {cancelResult}");
        Assert.Equal("REJECTED",
            cancelResult.GetProperty("data").GetProperty("cancelBuildingOffer").GetProperty("status").GetString());

        // Verify escrow was released back to buyer
        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buyerAcctFinal = await finalDb.BankAccounts.FindAsync(buyerAccountId);
        Assert.NotNull(buyerAcctFinal);
        Assert.Equal(initialBalance, buyerAcctFinal.Balance);

        var cancelledOffer = await finalDb.BuildingSaleOffers.FindAsync(offerId);
        Assert.NotNull(cancelledOffer);
        Assert.Equal(0m, cancelledOffer.EscrowAmount);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // 11. LendingCapacity_IncludesDefaultedPrincipal
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LendingCapacity_IncludesDefaultedPrincipal()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var lenderToken = await RegisterAsync(client, $"eih-cap-lender-{Guid.NewGuid():N}@test.com");
        var borrowerToken = await RegisterAsync(client, $"eih-cap-borrower-{Guid.NewGuid():N}@test.com");

        var lenderPlayerId = await GetPlayerIdAsync(client, lenderToken);
        var borrowerPlayerId = await GetPlayerIdAsync(client, borrowerToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstDeterministicAsync();

        var lenderCompany = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayerId, Name = "EihCapLenderCo" };
        var borrowerCompany = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId, Name = "EihCapBorrowerCo" };
        db.Companies.AddRange(lenderCompany, borrowerCompany);

        // Bank with TotalDeposits = 2000; 90% = 1800 max lending capacity
        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "EihCapBank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 2_000m,
            LendingInterestRatePercent = 10m,
        };
        db.Buildings.Add(bank);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
            CompanyId = lenderCompany.Id,
        });

        var collateral = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = borrowerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "EihCapCollateral",
            Latitude = city.Latitude + 0.09,
            Longitude = city.Longitude + 0.09,
        };
        db.Buildings.Add(collateral);

        var borrowerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 500_000m,
            CompanyId = borrowerCompany.Id,
        };
        db.BankAccounts.Add(borrowerAccount);

        // Existing DEFAULTED loan that consumes 1700 of the 1800 capacity
        var existingOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 1_700m,
            TotalCapacity = 2_000m,
            UsedCapacity = 1_700m,
            DurationTicks = 1440L,
            IsActive = true,
            CreatedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(existingOffer);

        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = existingOffer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BankBuildingId = bank.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 1_700m,
            RemainingPrincipal = 1_700m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = gameState.CurrentTick - 100,
            DueTick = gameState.CurrentTick + 1000,
            NextPaymentTick = gameState.CurrentTick + 10,
            PaymentAmount = 170m,
            PaymentsMade = 0,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted, // <-- DEFAULTED: must count against lending capacity
            MissedPayments = 3,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();

        // Only 100 EUR of capacity remains (1800 - 1700 = 100).
        // Trying to borrow 1200 EUR must fail with INSUFFICIENT_CAPACITY.
        var result = await ExecAsync(client,
            "mutation AcceptLoan($input: AcceptLoanInput!) { acceptLoan(input: $input) { id } }",
            new
            {
                input = new
                {
                    loanOfferId = bank.Id.ToString(),
                    borrowerCompanyId = borrowerCompany.Id.ToString(),
                    principalAmount = 1_200m,
                    collateralBuildingId = collateral.Id.ToString(),
                }
            },
            borrowerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("INSUFFICIENT_CAPACITY",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }
}
