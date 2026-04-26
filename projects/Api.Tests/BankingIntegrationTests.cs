using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Integration tests for bank capitalization, liquidity pressure, and central-bank borrowing mechanics.
/// Each test uses an isolated factory to avoid shared-state interference.
/// </summary>
public sealed class BankingIntegrationTests
{
    #region Helpers

    private static async Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return await Task.FromResult(new TickProcessor(db, phases, logger));
    }

    private static (Player player, Company company, Building bank) SeedBank(
        AppDbContext db,
        string suffix,
        decimal companyCash,
        decimal deposits,
        decimal centralBankDebt = 0m)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"bank-{suffix}@test.com",
            DisplayName = $"Banker {suffix}",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Bank Corp {suffix}",
            Cash = companyCash,
        };
        db.Companies.Add(company);

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = $"Test City {suffix}",
            CountryCode = "TC",
            Population = 50_000,
            BaseSalaryPerManhour = 20m,
            Latitude = 48.0,
            Longitude = 17.0,
        };
        db.Cities.Add(city);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = $"Test Bank {suffix}",
            BaseCapitalDeposited = true,
            TotalDeposits = deposits,
            CentralBankDebt = centralBankDebt,
            DepositInterestRatePercent = 3m,
            LendingInterestRatePercent = 8m,
        };
        db.Buildings.Add(bank);

        return (player, company, bank);
    }

    #endregion

    // ── Bank capitalization enforcement ───────────────────────────────────────

    /// <summary>
    /// Verifies that Building.BaseCapitalDeposited defaults to false for new bank entities
    /// and that the undercapitalized bank is blocked from accepting customer deposits.
    /// (The actual GraphQL enforcement is tested in GraphQlIntegrationTests.)
    /// </summary>
    [Fact]
    public async Task Bank_UndercapitalizedBank_HasBaseCapitalDepositedFalse()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"undercap-{Guid.NewGuid():N}@test.com",
            DisplayName = "Undercap",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Undercap Corp", Cash = 5_000_000m };
        db.Companies.Add(company);

        var city = await db.Cities.FirstAsync();
        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "Undercap Bank",
            BaseCapitalDeposited = false, // Not yet capitalized
            TotalDeposits = 0m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        // Verify the undercapitalized state is persisted
        var loaded = await db.Buildings.FindAsync(bank.Id);
        Assert.NotNull(loaded);
        Assert.False(loaded.BaseCapitalDeposited);
        Assert.Equal(0m, loaded.TotalDeposits);
    }

    /// <summary>
    /// Verifies that BankInfoSummary.LiquidityStatus is HEALTHY for a well-capitalized bank
    /// with no central-bank debt and adequate cash reserves.
    /// </summary>
    [Fact]
    public async Task BankInfo_HealthyBank_ReturnsHealthyLiquidityStatus()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, company, bank) = SeedBank(db, suffix: "healthy", companyCash: 5_000_000m, deposits: 10_000_000m);
        await db.SaveChangesAsync();

        // Reload with navigation properties
        var bankWithNav = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstAsync(b => b.Id == bank.Id);

        var summary = await Mutation.BuildBankInfoAsync(db, bankWithNav);

        Assert.Equal(BankLiquidityStatus.Healthy, summary.LiquidityStatus);
        Assert.Equal(0m, summary.CentralBankDebt);
        Assert.Equal(1_000_000m, summary.ReserveRequirement); // 10% of 10M deposits
        Assert.Equal(5_000_000m, summary.AvailableCash);
        Assert.Equal(0m, summary.ReserveShortfall); // cash (5M) > reserve (1M)
    }

    /// <summary>
    /// Verifies that a bank with central-bank debt but sufficient cash is classified as PRESSURED.
    /// </summary>
    [Fact]
    public async Task BankInfo_BankWithCentralBankDebt_ReturnsPressuredStatus()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, company, bank) = SeedBank(
            db, suffix: "pressured",
            companyCash: 2_000_000m,
            deposits: 10_000_000m,
            centralBankDebt: 500_000m);
        await db.SaveChangesAsync();

        var bankWithNav = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstAsync(b => b.Id == bank.Id);

        var summary = await Mutation.BuildBankInfoAsync(db, bankWithNav);

        Assert.Equal(BankLiquidityStatus.Pressured, summary.LiquidityStatus);
        Assert.Equal(500_000m, summary.CentralBankDebt);
        Assert.Equal(1_000_000m, summary.ReserveRequirement);
        Assert.Equal(2_000_000m, summary.AvailableCash);
        Assert.Equal(0m, summary.ReserveShortfall); // cash (2M) > reserve (1M), so no shortfall
    }

    /// <summary>
    /// Verifies that a bank with central-bank debt AND a reserve shortfall is classified as CRITICAL.
    /// </summary>
    [Fact]
    public async Task BankInfo_BankWithDebtAndShortfall_ReturnsCriticalStatus()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, company, bank) = SeedBank(
            db, suffix: "critical",
            companyCash: 500_000m,      // below reserve requirement of 1M
            deposits: 10_000_000m,
            centralBankDebt: 1_000_000m);
        await db.SaveChangesAsync();

        var bankWithNav = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstAsync(b => b.Id == bank.Id);

        var summary = await Mutation.BuildBankInfoAsync(db, bankWithNav);

        Assert.Equal(BankLiquidityStatus.Critical, summary.LiquidityStatus);
        Assert.Equal(1_000_000m, summary.CentralBankDebt);
        Assert.Equal(1_000_000m, summary.ReserveRequirement);
        Assert.Equal(500_000m, summary.AvailableCash);
        Assert.Equal(500_000m, summary.ReserveShortfall); // reserve (1M) - cash (0.5M) = 0.5M
    }

    // ── Central-bank borrowing via BankInterestPhase ──────────────────────────

    /// <summary>
    /// Verifies that when a bank cannot pay deposit interest from its own cash,
    /// the BankInterestPhase records the debt on Building.CentralBankDebt
    /// and the depositor still receives the full interest amount.
    /// </summary>
    [Fact]
    public async Task BankInterestPhase_IlliquidBank_AccumulatesCentralBankDebt()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed bank with zero liquidity account balance — cannot pay any deposit interest.
        var (_, bankCompany, bank) = SeedBank(db, suffix: "illiquid", companyCash: 0m, deposits: 10_000_000m);
        await db.SaveChangesAsync();

        var bankCity = await db.Cities.FirstAsync(c => c.Id == bank.CityId);
        SeedCompanyBankAccount(db, bankCompany.Id, bankCity.CurrencyCode, 0m);

        // Seed a customer depositor
        var customerPlayer = new Player { Id = Guid.NewGuid(), Email = $"cust-{Guid.NewGuid():N}@test.com", DisplayName = "Customer", PasswordHash = "h", Role = PlayerRole.Player };
        db.Players.Add(customerPlayer);
        var customerCompany = new Company { Id = Guid.NewGuid(), PlayerId = customerPlayer.Id, Name = "Customer Corp", Cash = 0m };
        db.Companies.Add(customerCompany);

        var deposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = bankCity.CurrencyCode,
            CompanyId = customerCompany.Id,
            BankBuildingId = bank.Id,
            Balance = 1_000_000m,
            DepositInterestRatePercent = 3m, // 3% p.a.
            IsBaseCapitalDeposit = false,
            DepositedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.Add(deposit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Reload entities
        var updatedBank = await db.Buildings.FindAsync(bank.Id);
        var updatedDeposit = await db.BankAccounts.FindAsync(deposit.Id);

        Assert.NotNull(updatedBank);
        Assert.NotNull(updatedDeposit);

        // Bank should have accumulated central-bank debt
        Assert.True(updatedBank.CentralBankDebt > 0m,
            $"Bank should have central-bank debt after illiquid tick, but CentralBankDebt = {updatedBank.CentralBankDebt}");

        // Depositor should still receive the full interest in the deposit balance
        Assert.True(updatedDeposit.Balance > 1_000_000m,
            $"Depositor deposit balance should include accrued interest even from illiquid bank, but balance = {updatedDeposit.Balance}");

        // A CentralBankBorrow ledger entry should exist
        var cbBorrow = await db.LedgerEntries
            .Where(l => l.CompanyId == bankCompany.Id && l.Category == LedgerCategory.CentralBankBorrow)
            .ToListAsync();
        Assert.NotEmpty(cbBorrow);
    }

    /// <summary>
    /// Verifies that a bank with existing central-bank debt is charged interest on it each tick,
    /// and the debt grows when the bank has no cash to pay the interest.
    /// </summary>
    [Fact]
    public async Task BankInterestPhase_CentralBankDebt_AccruesToInterest()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const decimal initialDebt = 1_000_000m;
        // Bank with zero cash — debt interest will compound
        var (_, bankCompany, bank) = SeedBank(
            db, suffix: "debtinterest",
            companyCash: 0m,
            deposits: 0m,  // no deposits → no deposit interest to pay
            centralBankDebt: initialDebt);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var updatedBank = await db.Buildings.FindAsync(bank.Id);
        Assert.NotNull(updatedBank);

        // Debt should have grown because the bank couldn't pay the interest
        Assert.True(updatedBank.CentralBankDebt > initialDebt,
            $"Central bank debt should grow when bank can't pay interest. Initial={initialDebt}, After={updatedBank.CentralBankDebt}");
    }

    /// <summary>
    /// Verifies that a solvent bank with surplus cash automatically repays central-bank debt
    /// during BankInterestPhase when its cash exceeds the reserve requirement.
    /// </summary>
    [Fact]
    public async Task BankInterestPhase_SolventBank_AutoRepaysCentralBankDebt()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        const decimal deposits = 1_000_000m;
        const decimal initialDebt = 100_000m;
        // Bank with large surplus cash: reserve is 10% of 1M = 100K; bank has 5M
        var (_, bankCompany, bank) = SeedBank(
            db, suffix: "repay",
            companyCash: 5_000_000m,
            deposits: deposits,
            centralBankDebt: initialDebt);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var updatedBank = await db.Buildings.FindAsync(bank.Id);
        Assert.NotNull(updatedBank);

        // Debt should have decreased (surplus cash used to repay)
        Assert.True(updatedBank.CentralBankDebt < initialDebt,
            $"Central bank debt should decrease when bank has surplus cash. Initial={initialDebt}, After={updatedBank.CentralBankDebt}");
    }

    // ── Central-bank rate computation ─────────────────────────────────────────

    /// <summary>
    /// Verifies that ComputeCentralBankRate returns 2% when no banks are borrowing.
    /// </summary>
    [Fact]
    public async Task ComputeCentralBankRate_NoBorrowers_ReturnsMinimumRate()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Ensure no banks have central-bank debt in this isolated DB
        var rate = Mutation.ComputeCentralBankRate(db);
        Assert.Equal(2m, rate); // minimum rate when no banks are borrowing
    }

    /// <summary>
    /// Verifies that ComputeCentralBankRate increases above 2% when banks are borrowing.
    /// </summary>
    [Fact]
    public async Task ComputeCentralBankRate_WithBorrowers_ExceedsMinimumRate()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed several banks with central-bank debt
        for (int i = 0; i < 3; i++)
        {
            SeedBank(db, suffix: $"rate-{i}", companyCash: 0m, deposits: 1_000_000m, centralBankDebt: 100_000m);
        }
        await db.SaveChangesAsync();

        var rate = Mutation.ComputeCentralBankRate(db);

        // With 3 borrowing banks: rate = 2 + (5-2) * (3/5) = 2 + 1.8 = 3.8%
        Assert.True(rate > 2m, $"Rate should be above minimum when banks are borrowing. Got {rate}%");
        Assert.True(rate <= 5m, $"Rate must not exceed maximum 5%. Got {rate}%");
        Assert.Equal(3.8m, rate); // locks in the interpolation formula
    }

    // ── OpenBankAccount reserve-preserving repayment ──────────────────────────

    /// <summary>
    /// Proves that OpenBankAccount only repays central-bank debt from surplus cash ABOVE the
    /// reserve requirement, never draining the bank below its required reserves.
    ///
    /// Scenario:
    ///   Bank: $1M deposits → reserve required = $100K; bank cash = $80K (below reserve = CRITICAL)
    ///   CB debt = $500K
    ///   Customer deposits $200K → bank cash becomes $280K; deposits become $1.2M → reserve = $120K
    ///   Surplus above reserve = $280K - $120K = $160K
    ///   Expected repayment = $160K (only the surplus); bank cash after = $120K (exactly at reserve)
    ///   Old buggy behaviour: repayment = min($500K, $280K) = $280K → bank cash drops to $0 (below reserve!)
    /// </summary>
    [Fact]
    public async Task OpenBankAccount_UnderPressureBank_OnlyRepaysCbDebtFromSurplusCash()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Register bank owner and depositor
        var bankOwnerToken = await RegisterAsync(client, $"cd-res-owner-{Guid.NewGuid():N}@test.com", "BankOwner");
        var depositorToken = await RegisterAsync(client, $"cd-res-dep-{Guid.NewGuid():N}@test.com", "Depositor");

        var bankOwnerEmail  = (await ExecuteAsync(client, "{ me { email } }", token: bankOwnerToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;
        var depositorEmail = (await ExecuteAsync(client, "{ me { email } }", token: depositorToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;

        var bankOwner = await db.Players.FirstAsync(p => p.Email == bankOwnerEmail);
        var depositor = await db.Players.FirstAsync(p => p.Email == depositorEmail);
        var city = await db.Cities.FirstAsync();

        // Bank starts under reserve: $80K liquidity, $1M deposits, $500K CB debt
        var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = bankOwner.Id, Name = "PressuredBankCo", Cash = 80_000m };
        var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositor.Id, Name = "NewDepositorCo", Cash = 500_000m };
        db.Companies.AddRange(bankCompany, depositorCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "PressuredBank", Level = 1,
            DepositInterestRatePercent = 4m, LendingInterestRatePercent = 9m,
            TotalDeposits = 1_000_000m, BaseCapitalDeposited = true,
            CentralBankDebt = 500_000m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var bankLiquidity = SeedCompanyBankAccount(db, bankCompany.Id, city.CurrencyCode, 80_000m);
        SeedCompanyBankAccount(db, depositorCompany.Id, city.CurrencyCode, 500_000m);
        await db.SaveChangesAsync();

        var bankLiquidityBeforeDeposit = await db.BankAccounts
            .Where(account => account.CompanyId == bankCompany.Id && account.ClosedAtUtc == null)
            .SumAsync(account => account.Balance);

        // Customer deposits $200K
        var result = await ExecuteAsync(client,
            """
                        mutation CD($input: OpenBankAccountInput!) {
                            openBankAccount(input: $input) { id amount }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), depositorCompanyId = depositorCompany.Id.ToString(), amount = 200_000m } },
            token: depositorToken);

        var depositData = result.GetProperty("data").GetProperty("openBankAccount");
        Assert.Equal(200_000m, depositData.GetProperty("amount").GetDecimal());

        await db.Entry(bank).ReloadAsync();
        await db.Entry(bankLiquidity).ReloadAsync();

        // After deposit: total bank-company liquidity increases by the incoming amount.
        // Auto-repayment must only use surplus above reserve and never push liquidity below reserve.
        const decimal expectedDeposits = 1_200_000m;
        const decimal expectedReserve = expectedDeposits * 0.10m; // = 120,000
        var liquidityAfterIncomingDeposit = bankLiquidityBeforeDeposit + 200_000m;
        var expectedRepayment = Math.Min(500_000m, Math.Max(0m, liquidityAfterIncomingDeposit - expectedReserve));
        var expectedCbDebt = 500_000m - expectedRepayment;
        var expectedLiquidity = liquidityAfterIncomingDeposit - expectedRepayment;

        var totalBankLiquidity = await db.BankAccounts
            .Where(account => account.CompanyId == bankCompany.Id && account.ClosedAtUtc == null)
            .SumAsync(account => account.Balance);

        Assert.Equal(expectedDeposits, bank.TotalDeposits);
        Assert.Equal(expectedLiquidity, totalBankLiquidity);
        // CB debt should decrease exactly by the surplus available above reserve.
        Assert.Equal(expectedCbDebt, bank.CentralBankDebt);

        // Bank liquidity must be >= reserve requirement (the key invariant)
        var reserveNeeded = bank.TotalDeposits * 0.10m;
        Assert.True(totalBankLiquidity >= reserveNeeded,
            $"Bank liquidity ({totalBankLiquidity:C}) must not fall below reserve requirement ({reserveNeeded:C}) after OpenBankAccount.");
    }

    /// <summary>
    /// Verifies that the bankInfo query returns a liquidity summary consistent with actual DB state
    /// after a deposit arrives at an under-pressure bank. Specifically:
    ///   - ReserveShortfall should be zero or reduced after the deposit
    ///   - CentralBankDebt should have decreased by the surplus (not the full deposit)
    ///   - LiquidityStatus should remain PRESSURED (not flip to HEALTHY if CB debt remains)
    /// </summary>
    [Fact]
    public async Task BankInfo_AfterDepositOnUnderPressureBank_LiquiditySummaryIsConsistentWithDbState()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var bankOwnerToken = await RegisterAsync(client, $"bi-cons-owner-{Guid.NewGuid():N}@test.com", "BiOwner");
        var depositorToken = await RegisterAsync(client, $"bi-cons-dep-{Guid.NewGuid():N}@test.com", "BiDepositor");

        var bankOwnerEmail  = (await ExecuteAsync(client, "{ me { email } }", token: bankOwnerToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;
        var depositorEmail = (await ExecuteAsync(client, "{ me { email } }", token: depositorToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;

        var bankOwner = await db.Players.FirstAsync(p => p.Email == bankOwnerEmail);
        var depositor = await db.Players.FirstAsync(p => p.Email == depositorEmail);
        var city = await db.Cities.FirstAsync();

        // Bank: 5M deposits, cash=400K (below reserve of 500K), CB debt=1M → CRITICAL
        var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = bankOwner.Id, Name = "ConsistencyBankCo", Cash = 400_000m };
        var depositorCompany = new Company { Id = Guid.NewGuid(), PlayerId = depositor.Id, Name = "ConsDepositorCo", Cash = 1_000_000m };
        db.Companies.AddRange(bankCompany, depositorCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "ConsistencyBank", Level = 1,
            DepositInterestRatePercent = 4m, LendingInterestRatePercent = 9m,
            TotalDeposits = 5_000_000m, BaseCapitalDeposited = true,
            CentralBankDebt = 1_000_000m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        // Customer deposits $500K → bank cash: 400K+500K=900K; deposits: 5.5M; reserve: 550K
        // Surplus = 900K - 550K = 350K → repayment = 350K; CB debt after = 650K
        await ExecuteAsync(client,
            """
                        mutation CD($input: OpenBankAccountInput!) {
                            openBankAccount(input: $input) { id }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), depositorCompanyId = depositorCompany.Id.ToString(), amount = 500_000m } },
            token: depositorToken);

        // Now query bankInfo to get the server-computed liquidity summary
        var bankInfoResult = await ExecuteAsync(client,
            """
            query BI($bankBuildingId: UUID!) {
              bankInfo(bankBuildingId: $bankBuildingId) {
                availableCash
                reserveRequirement
                reserveShortfall
                centralBankDebt
                liquidityStatus
              }
            }
            """,
            new { bankBuildingId = bank.Id.ToString() },
            token: bankOwnerToken);

        var info = bankInfoResult.GetProperty("data").GetProperty("bankInfo");

        var availableCash       = info.GetProperty("availableCash").GetDecimal();
        var reserveRequirement  = info.GetProperty("reserveRequirement").GetDecimal();
        var reserveShortfall    = info.GetProperty("reserveShortfall").GetDecimal();
        var centralBankDebt     = info.GetProperty("centralBankDebt").GetDecimal();
        var liquidityStatus     = info.GetProperty("liquidityStatus").GetString();

        // After deposit: deposits = 5.5M → reserve = 550K; cash = 900K - 350K repaid = 550K
        Assert.Equal(5_500_000m * 0.10m, reserveRequirement);   // 550,000
        // Reserve shortfall should be 0 after deposit brings bank above reserve
        Assert.Equal(0m, reserveShortfall);
        Assert.True(availableCash >= reserveRequirement,
            $"Bank cash ({availableCash:C}) must be >= reserve ({reserveRequirement:C}) after reserve-preserving repayment.");
        // CB debt should have decreased from 1M after surplus repayment
        Assert.True(centralBankDebt < 1_000_000m,
            $"CB debt should have decreased from 1M after surplus repayment. Got {centralBankDebt:C}");
        // CB debt should be 650K after 350K surplus repaid
        Assert.Equal(650_000m, centralBankDebt);

        // Bank still has CB debt → status must be PRESSURED (not HEALTHY)
        Assert.Equal("PRESSURED", liquidityStatus);
    }

    // ── SetBankRates tests ────────────────────────────────────────────────────

    /// <summary>
    /// Bank owner can update lending and deposit rates via setBankRates.
    /// </summary>
    [Fact]
    public async Task SetBankRates_BankOwner_UpdatesRatesSuccessfully()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "rateowner@test.com", "Rate Owner");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync();

        var ownerUser = await db.Players.FirstAsync(p => p.Email == "rateowner@test.com");
        var ownerCompany = new Company { Id = Guid.NewGuid(), PlayerId = ownerUser.Id, Name = "Rate Bank Co", Cash = 50_000_000m };
        db.Companies.Add(ownerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = ownerCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Rate Test Bank", Level = 1,
            DepositInterestRatePercent = 3m, LendingInterestRatePercent = 8m,
            TotalDeposits = 10_000_000m, BaseCapitalDeposited = true,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation SBR($input: SetBankRatesInput!) {
              setBankRates(input: $input) {
                depositInterestRatePercent
                lendingInterestRatePercent
              }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), depositInterestRatePercent = 4.5m, lendingInterestRatePercent = 10.5m } },
            token: ownerToken);

        var updated = result.GetProperty("data").GetProperty("setBankRates");
        Assert.Equal(4.5m, updated.GetProperty("depositInterestRatePercent").GetDecimal());
        Assert.Equal(10.5m, updated.GetProperty("lendingInterestRatePercent").GetDecimal());
    }

    /// <summary>
    /// Non-owner cannot update bank rates — should return an error.
    /// </summary>
    [Fact]
    public async Task SetBankRates_NonOwner_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, "rateowner2@test.com", "Rate Owner 2");
        var otherToken = await RegisterAsync(client, "rateother2@test.com", "Rate Other 2");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync();

        var ownerUser = await db.Players.FirstAsync(p => p.Email == "rateowner2@test.com");
        var ownerCompany = new Company { Id = Guid.NewGuid(), PlayerId = ownerUser.Id, Name = "Rate Bank Co 2", Cash = 50_000_000m };
        db.Companies.Add(ownerCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = ownerCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "Rate Test Bank 2", Level = 1,
            DepositInterestRatePercent = 3m, LendingInterestRatePercent = 8m,
            TotalDeposits = 10_000_000m, BaseCapitalDeposited = true,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation SBR($input: SetBankRatesInput!) {
              setBankRates(input: $input) {
                depositInterestRatePercent
              }
            }
            """,
            new { input = new { bankBuildingId = bank.Id.ToString(), depositInterestRatePercent = 5m, lendingInterestRatePercent = 12m } },
            token: otherToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected an error when non-owner tries to set rates.");
    }

    // ── Self-interest exclusion tests ─────────────────────────────────────────

    /// <summary>
    /// During BankInterestPhase, the bank's own founder/owner company does not receive
    /// deposit interest from its own bank — only external depositors earn interest.
    /// </summary>
    [Fact]
    public async Task BankInterestPhase_FounderDeposit_DoesNotReceiveInterest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = await CreateProcessorAsync(scope);
        var city = await db.Cities.FirstAsync();
        var gs = await db.GameStates.FirstAsync();

        // Bank owner
        var bankOwner = new Player { Id = Guid.NewGuid(), Email = "selfint@test.com", DisplayName = "Self Int", PasswordHash = "x", Role = PlayerRole.Player };
        db.Players.Add(bankOwner);

        // External depositor
        var externalDepositor = new Player { Id = Guid.NewGuid(), Email = "extint@test.com", DisplayName = "External", PasswordHash = "x", Role = PlayerRole.Player };
        db.Players.Add(externalDepositor);

        var bankCompany = new Company { Id = Guid.NewGuid(), PlayerId = bankOwner.Id, Name = "SelfInt Bank Co", Cash = 10_000_000m };
        var externalCompany = new Company { Id = Guid.NewGuid(), PlayerId = externalDepositor.Id, Name = "SelfInt External Co", Cash = 5_000_000m };
        db.Companies.AddRange(bankCompany, externalCompany);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = bankCompany.Id, CityId = city.Id,
            Type = BuildingType.Bank, Name = "SelfInt Bank", Level = 1,
            DepositInterestRatePercent = 10m, // high rate to make assertion easy
            LendingInterestRatePercent = 20m,
            TotalDeposits = 0m, BaseCapitalDeposited = false,
        };
        db.Buildings.Add(bank);

        // Founder deposit (base capital) — this deposit must NOT earn interest
        var founderDeposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            CompanyId = bankCompany.Id,
            BankBuildingId = bank.Id,
            Balance = 10_000_000m,
            TotalInterestPaid = 0m,
            IsBaseCapitalDeposit = true,
            DepositInterestRatePercent = bank.DepositInterestRatePercent ?? 10m,
            DepositedAtTick = gs.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };
        // External deposit — this one SHOULD earn interest
        var externalDeposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            CompanyId = externalCompany.Id,
            BankBuildingId = bank.Id,
            Balance = 1_000_000m,
            TotalInterestPaid = 0m,
            IsBaseCapitalDeposit = false,
            DepositInterestRatePercent = bank.DepositInterestRatePercent ?? 10m,
            DepositedAtTick = gs.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.AddRange(founderDeposit, externalDeposit);

        bank.TotalDeposits = founderDeposit.Balance + externalDeposit.Balance;
        bank.BaseCapitalDeposited = true;
        await db.SaveChangesAsync();

        var founderCashBefore = bankCompany.Cash;
        var externalCashBefore = externalCompany.Cash;

        await processor.ProcessTickAsync();
        await db.Entry(bankCompany).ReloadAsync();
        await db.Entry(externalCompany).ReloadAsync();

        // External depositor must have received interest
        Assert.True(externalCompany.Cash > externalCashBefore,
            $"External depositor should have received deposit interest. Before: {externalCashBefore:C}, After: {externalCompany.Cash:C}");

        // Bank owner company must NOT have received deposit interest from its own base capital
        // (The bank company pays out interest to others; it may gain via lending interest income,
        // but the founder deposit must not generate an inbound interest LedgerEntry for the bank company.)
        var founderInterestEntries = await db.LedgerEntries
            .Where(e => e.CompanyId == bankCompany.Id && e.Category == LedgerCategory.DepositInterestReceived)
            .ToListAsync();
        Assert.Empty(founderInterestEntries);
    }

    // ── Currency-aware banking tests ──────────────────────────────────────────

    /// <summary>
    /// GetBaseCapitalRequirement returns 240M CZK for Czech Republic (Prague),
    /// and 10M EUR for Slovakia (Bratislava) and Austria (Vienna).
    /// </summary>
    [Fact]
    public void GetBaseCapitalRequirement_ReturnsCorrectAmountPerCurrency()
    {
        Assert.Equal(240_000_000m, Mutation.GetBaseCapitalRequirement("CZK"));
        Assert.Equal(10_000_000m, Mutation.GetBaseCapitalRequirement("EUR"));
        Assert.Equal(10_000_000m, Mutation.GetBaseCapitalRequirement("USD"));
        Assert.Equal(10_000_000m, Mutation.GetBaseCapitalRequirement("XXX")); // fallback
    }

    /// <summary>
    /// GetCurrencySymbol returns the correct display symbol for known currencies.
    /// </summary>
    [Fact]
    public void GetCurrencySymbol_ReturnsCorrectSymbol()
    {
        Assert.Equal("Kč", Mutation.GetCurrencySymbol("CZK"));
        Assert.Equal("€", Mutation.GetCurrencySymbol("EUR"));
        Assert.Equal("$", Mutation.GetCurrencySymbol("USD"));
        Assert.Equal("£", Mutation.GetCurrencySymbol("GBP"));
        Assert.Equal("XYZ", Mutation.GetCurrencySymbol("XYZ")); // fallback: code itself
    }

    /// <summary>
    /// BankInfoSummary includes CityCurrencyCode and CityCurrencySymbol for a EUR city.
    /// </summary>
    [Fact]
    public async Task BankInfo_EurCity_ReturnsCurrencyCodeAndSymbol()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Use a EUR city (seeded Bratislava or Vienna)
        var eurCity = new City
        {
            Id = Guid.NewGuid(),
            Name = "Test EUR City",
            CountryCode = "SK",
            CurrencyCode = "EUR",
            Population = 100_000,
            BaseSalaryPerManhour = 20m,
            Latitude = 48.15,
            Longitude = 17.11,
        };
        db.Cities.Add(eurCity);

        var (_, _, bank) = SeedBankInCity(db, "eur-info", eurCity, companyCash: 5_000_000m, deposits: 10_000_000m);
        await db.SaveChangesAsync();

        var bankWithNav = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstAsync(b => b.Id == bank.Id);

        var summary = await Mutation.BuildBankInfoAsync(db, bankWithNav);

        Assert.Equal("EUR", summary.CityCurrencyCode);
        Assert.Equal("€", summary.CityCurrencySymbol);
        Assert.Equal(10_000_000m, summary.BaseCapitalRequirement);
    }

    /// <summary>
    /// BankInfoSummary includes CityCurrencyCode = CZK and BaseCapitalRequirement = 240M for Prague.
    /// </summary>
    [Fact]
    public async Task BankInfo_CzkCity_ReturnsCzkCurrencyAndHigherBaseCapital()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var czkCity = new City
        {
            Id = Guid.NewGuid(),
            Name = "Test CZK City",
            CountryCode = "CZ",
            CurrencyCode = "CZK",
            Population = 1_000_000,
            BaseSalaryPerManhour = 22m,
            Latitude = 50.08,
            Longitude = 14.44,
        };
        db.Cities.Add(czkCity);

        var (_, _, bank) = SeedBankInCity(db, "czk-info", czkCity, companyCash: 120_000_000m, deposits: 240_000_000m);
        await db.SaveChangesAsync();

        var bankWithNav = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .FirstAsync(b => b.Id == bank.Id);

        var summary = await Mutation.BuildBankInfoAsync(db, bankWithNav);

        Assert.Equal("CZK", summary.CityCurrencyCode);
        Assert.Equal("Kč", summary.CityCurrencySymbol);
        Assert.Equal(240_000_000m, summary.BaseCapitalRequirement);
    }

    /// <summary>
    /// InitiateBaseDeposit on a Prague (CZK) bank requires 240M CZK, not 10M EUR.
    /// A company with only 10M cash is rejected; a company with 240M passes.
    /// </summary>
    [Fact]
    public async Task InitiateBaseDeposit_CzkCity_Requires240MCzk()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, $"czk-owner-{Guid.NewGuid():N}@test.com", "CZK Owner");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ownerEmail = (await ExecuteAsync(client, "{ me { email } }", token: ownerToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;
        var owner = await db.Players.FirstAsync(p => p.Email == ownerEmail);

        // CZK city
        var czkCity = new City
        {
            Id = Guid.NewGuid(),
            Name = "Prague Test",
            CountryCode = "CZ",
            CurrencyCode = "CZK",
            Population = 1_000_000,
            BaseSalaryPerManhour = 22m,
            Latitude = 50.08,
            Longitude = 14.44,
        };
        db.Cities.Add(czkCity);

        // Company with only 10M cash — not enough for 240M CZK requirement
        var insufficientCompany = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "CZK Insufficient Co", Cash = 10_000_000m };
        db.Companies.Add(insufficientCompany);
        SeedCompanyBankAccount(db, insufficientCompany.Id, czkCity.CurrencyCode, 10_000_000m);

        var bankInsufficient = new Building
        {
            Id = Guid.NewGuid(), CompanyId = insufficientCompany.Id, CityId = czkCity.Id,
            Type = BuildingType.Bank, Name = "CZK Insufficient Bank", Level = 1,
            BaseCapitalDeposited = false, TotalDeposits = 0m,
        };
        db.Buildings.Add(bankInsufficient);
        await db.SaveChangesAsync();

        // Should fail with INSUFFICIENT_FUNDS because 10M < 240M CZK
        var failResult = await ExecuteAsync(client,
            "mutation IB($id: UUID!) { initiateBaseDeposit(bankBuildingId: $id) { baseCapitalDeposited } }",
            new { id = bankInsufficient.Id.ToString() },
            token: ownerToken);

        var errors = failResult.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Should fail with insufficient funds for CZK bank.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INSUFFICIENT_FUNDS", code);

        // Company with 240M cash — enough
        var sufficientCompany = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "CZK Sufficient Co", Cash = 240_000_000m };
        db.Companies.Add(sufficientCompany);
        SeedCompanyBankAccount(db, sufficientCompany.Id, czkCity.CurrencyCode, 240_000_000m);

        var bankSufficient = new Building
        {
            Id = Guid.NewGuid(), CompanyId = sufficientCompany.Id, CityId = czkCity.Id,
            Type = BuildingType.Bank, Name = "CZK Sufficient Bank", Level = 1,
            BaseCapitalDeposited = false, TotalDeposits = 0m,
        };
        db.Buildings.Add(bankSufficient);
        await db.SaveChangesAsync();

        var successResult = await ExecuteAsync(client,
            """
            mutation IB($id: UUID!) {
              initiateBaseDeposit(bankBuildingId: $id) {
                baseCapitalDeposited
                totalDeposits
                cityCurrencyCode
                baseCapitalRequirement
              }
            }
            """,
            new { id = bankSufficient.Id.ToString() },
            token: ownerToken);

        Assert.False(successResult.TryGetProperty("errors", out _), "Sufficient CZK funding account should allow base deposit initiation.");
        var data = successResult.GetProperty("data").GetProperty("initiateBaseDeposit");
        Assert.True(data.GetProperty("baseCapitalDeposited").GetBoolean());
        Assert.Equal(240_000_000m, data.GetProperty("totalDeposits").GetDecimal());
        Assert.Equal("CZK", data.GetProperty("cityCurrencyCode").GetString());
        Assert.Equal(240_000_000m, data.GetProperty("baseCapitalRequirement").GetDecimal());
    }

    /// <summary>
    /// For a EUR city (Bratislava/Vienna), the base capital requirement remains 10M EUR.
    /// </summary>
    [Fact]
    public async Task InitiateBaseDeposit_EurCity_Requires10mEur()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerToken = await RegisterAsync(client, $"eur-owner-{Guid.NewGuid():N}@test.com", "EUR Owner");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ownerEmail = (await ExecuteAsync(client, "{ me { email } }", token: ownerToken))
            .GetProperty("data").GetProperty("me").GetProperty("email").GetString()!;
        var owner = await db.Players.FirstAsync(p => p.Email == ownerEmail);

        var eurCity = new City
        {
            Id = Guid.NewGuid(),
            Name = "Bratislava Test",
            CountryCode = "SK",
            CurrencyCode = "EUR",
            Population = 475_000,
            BaseSalaryPerManhour = 18m,
            Latitude = 48.15,
            Longitude = 17.11,
        };
        db.Cities.Add(eurCity);

        var company = new Company { Id = Guid.NewGuid(), PlayerId = owner.Id, Name = "EUR Bank Co", Cash = 10_000_000m };
        db.Companies.Add(company);

        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = eurCity.Id,
            Type = BuildingType.Bank, Name = "EUR Bank", Level = 1,
            BaseCapitalDeposited = false, TotalDeposits = 0m,
        };
        db.Buildings.Add(bank);
        await db.SaveChangesAsync();

        var result = await ExecuteAsync(client,
            """
            mutation IB($id: UUID!) {
              initiateBaseDeposit(bankBuildingId: $id) {
                baseCapitalDeposited
                totalDeposits
                cityCurrencyCode
                cityCurrencySymbol
                baseCapitalRequirement
              }
            }
            """,
            new { id = bank.Id.ToString() },
            token: ownerToken);

        var data = result.GetProperty("data").GetProperty("initiateBaseDeposit");
        Assert.True(data.GetProperty("baseCapitalDeposited").GetBoolean());
        Assert.Equal(10_000_000m, data.GetProperty("totalDeposits").GetDecimal());
        Assert.Equal("EUR", data.GetProperty("cityCurrencyCode").GetString());
        Assert.Equal("€", data.GetProperty("cityCurrencySymbol").GetString());
        Assert.Equal(10_000_000m, data.GetProperty("baseCapitalRequirement").GetDecimal());
    }

    // ── Static helpers for HTTP-level tests ───────────────────────────────────

    /// <summary>Variant of SeedBank that accepts an explicit city entity.</summary>
    private static (Player player, Company company, Building bank) SeedBankInCity(
        AppDbContext db,
        string suffix,
        City city,
        decimal companyCash,
        decimal deposits,
        decimal centralBankDebt = 0m)
    {
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"bank-{suffix}@test.com",
            DisplayName = $"Banker {suffix}",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"Bank Corp {suffix}",
            Cash = companyCash,
        };
        db.Companies.Add(company);

        var bank = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = $"Test Bank {suffix}",
            BaseCapitalDeposited = true,
            TotalDeposits = deposits,
            CentralBankDebt = centralBankDebt,
            DepositInterestRatePercent = 3m,
            LendingInterestRatePercent = 8m,
        };
        db.Buildings.Add(bank);

        return (player, company, bank);
    }

    private static BankAccount SeedCompanyBankAccount(
        AppDbContext db,
        Guid companyId,
        string currencyCode,
        decimal balance)
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = currencyCode,
            CompanyId = companyId,
            Balance = balance,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };

        db.BankAccounts.Add(account);
        return account;
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName)
    {
        var result = await ExecuteAsync(client,
            """
            mutation R($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> ExecuteAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8, "application/json"),
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }
}
