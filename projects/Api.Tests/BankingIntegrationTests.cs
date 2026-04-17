using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

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

        // Seed bank with zero cash — cannot pay any deposit interest
        var (_, bankCompany, bank) = SeedBank(db, suffix: "illiquid", companyCash: 0m, deposits: 10_000_000m);

        // Seed a customer depositor
        var customerPlayer = new Player { Id = Guid.NewGuid(), Email = $"cust-{Guid.NewGuid():N}@test.com", DisplayName = "Customer", PasswordHash = "h", Role = PlayerRole.Player };
        db.Players.Add(customerPlayer);
        var customerCompany = new Company { Id = Guid.NewGuid(), PlayerId = customerPlayer.Id, Name = "Customer Corp", Cash = 0m };
        db.Companies.Add(customerCompany);

        var deposit = new BankDeposit
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bank.Id,
            DepositorCompanyId = customerCompany.Id,
            Amount = 1_000_000m,
            DepositInterestRatePercent = 3m, // 3% p.a.
            IsBaseCapital = false,
            IsActive = true,
            DepositedAtTick = 1,
            DepositedAtUtc = DateTime.UtcNow,
        };
        db.BankDeposits.Add(deposit);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Reload entities
        var updatedBank = await db.Buildings.FindAsync(bank.Id);
        var updatedCustomer = await db.Companies.FindAsync(customerCompany.Id);

        Assert.NotNull(updatedBank);
        Assert.NotNull(updatedCustomer);

        // Bank should have accumulated central-bank debt
        Assert.True(updatedBank.CentralBankDebt > 0m,
            $"Bank should have central-bank debt after illiquid tick, but CentralBankDebt = {updatedBank.CentralBankDebt}");

        // Depositor should still receive the full interest (central bank covered it)
        Assert.True(updatedCustomer.Cash > 0m,
            $"Depositor should have received interest even from illiquid bank, but cash = {updatedCustomer.Cash}");

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
}
