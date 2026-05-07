using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for the building destruction (loan default foreclosure) workflow.
/// Each test uses an isolated factory to avoid mutating shared game state.
/// Covers: auto-listing on default at 90%, 72-tick foreclosure window,
/// debt/surplus distribution, player notification, lot release, and edge cases.
/// </summary>
public sealed class BuildingDestructionTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static TickProcessor CreateProcessor(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = new NullLogger<TickProcessor>();
        return new TickProcessor(db, phases, logger);
    }

    /// <summary>Seeds a player, company, building, bank account, lender, and loan offer.</summary>
    private static async Task<(AppDbContext Db, Player Player, Company Company, Building Building,
        BankAccount Account, LoanOffer LoanOffer)> SeedBaseAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"destroy-{Guid.NewGuid():N}@test.com",
            DisplayName = "Destruction Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Destroy Corp" };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Foreclosure Factory",
            Latitude = 48.15,
            Longitude = 17.1,
            Level = 1,
        };
        db.Buildings.Add(building);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
            CurrencyCode = city.CurrencyCode,
            Balance = 0m,
            CompanyId = company.Id,
        };
        db.BankAccounts.Add(account);

        var lenderPlayer = new Player { Id = Guid.NewGuid(), Email = $"lender-{Guid.NewGuid():N}@test.com", DisplayName = "Lender", PasswordHash = "h", Role = PlayerRole.Player };
        db.Players.Add(lenderPlayer);
        var lender = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayer.Id, Name = "Lender Corp" };
        db.Companies.Add(lender);
        var lenderAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
            CurrencyCode = city.CurrencyCode,
            Balance = 1_000_000m,
            CompanyId = lender.Id,
        };
        db.BankAccounts.Add(lenderAccount);

        var loanOffer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = building.Id,
            LenderCompanyId = lender.Id,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 600_000m,
            TotalCapacity = 1_000_000m,
            UsedCapacity = 0m,
            DurationTicks = 1440L,
            IsActive = true,
            CreatedAtTick = 1L,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(loanOffer);

        await db.SaveChangesAsync();
        return (db, player, company, building, account, loanOffer);
    }

    /// <summary>Adds a defaulted loan with collateral to the database.</summary>
    private static async Task<Loan> AddDefaultedLoanAsync(
        AppDbContext db, Company borrower, Building building, LoanOffer loanOffer,
        decimal appraisedValue, long defaultedAtTick)
    {
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = borrower.Id,
            BankBuildingId = building.Id,
            LenderCompanyId = loanOffer.LenderCompanyId,
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 500_000m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = defaultedAtTick + 10_000L,  // not due during test tick
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            MissedPayments = 3,
            Status = LoanStatus.Defaulted,
            DefaultedAtTick = defaultedAtTick,
            CollateralBuildingId = building.Id,
            CollateralAppraisedValue = appraisedValue,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-5),
            ClosedAtUtc = DateTime.UtcNow.AddDays(-4),
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return loan;
    }

    // ── auto-listing tests (LoanRepaymentPhase) ──────────────────────────────

    [Fact]
    public async Task LoanMissedPayment_WithCollateral_AutoListsBuildingForSale_AndNotifiesBorrower()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = company.Id,
            BankBuildingId = building.Id,
            LenderCompanyId = loanOffer.LenderCompanyId,
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 500_000m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1L,     // due on tick 1
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            MissedPayments = 0,
            Status = LoanStatus.Active,
            CollateralBuildingId = building.Id,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = 0;   // processor will increment to 1
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        await db.Entry(loan).ReloadAsync();

        Assert.True(building.IsForSale, "Building should be auto-listed immediately after the first missed payment");
        Assert.NotNull(building.AskingPrice);
        // 90% of 1,000,000 = 900,000
        Assert.Equal(900_000m, building.AskingPrice.Value);
        Assert.Equal(LoanStatus.Overdue, loan.Status);
        Assert.NotNull(loan.DefaultedAtTick);

        var notification = await db.PlayerNotifications
            .FirstOrDefaultAsync(n => n.PlayerId == company.PlayerId && n.Type == PlayerNotificationType.LoanPaymentMissed);
        Assert.NotNull(notification);
        Assert.Contains("missed payment at", notification!.Message);
        Assert.Contains("seized in", notification.Message);
    }

    [Fact]
    public async Task LoanDefault_WithCollateral_AutoListPrice_IsNinetyPercentOfAppraisedValue()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var appraisedValue = 2_500_000m;
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = company.Id,
            BankBuildingId = building.Id,
            LenderCompanyId = loanOffer.LenderCompanyId,
            OriginalPrincipal = 300_000m,
            RemainingPrincipal = 300_000m,
            AnnualInterestRatePercent = 5m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 2L,
            PaymentAmount = 5_000m,
            TotalPayments = 10,
            MissedPayments = 2,
            Status = LoanStatus.Overdue,
            CollateralBuildingId = building.Id,
            CollateralAppraisedValue = appraisedValue,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = 1;  // processor will increment to 2
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.True(building.IsForSale);
        Assert.Equal(2_250_000m, building.AskingPrice!.Value);   // 90% of 2,500,000
    }

    [Fact]
    public async Task LoanDefault_WithoutCollateral_DoesNotAutoListAnyBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = company.Id,
            BankBuildingId = building.Id,
            LenderCompanyId = loanOffer.LenderCompanyId,
            OriginalPrincipal = 100_000m,
            RemainingPrincipal = 100_000m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 5L,
            PaymentAmount = 5_000m,
            TotalPayments = 10,
            MissedPayments = 2,
            Status = LoanStatus.Overdue,
            CollateralBuildingId = null,    // unsecured
            CollateralAppraisedValue = null,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = 4;  // processor increments to 5 (payment due)
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.False(building.IsForSale, "Unsecured loan default should NOT list any building for sale");
    }

    [Fact]
    public async Task LoanRepayment_CreatesAccountScopedLedgerEntries_ForBorrowerAndLender()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        account.Balance = 100_000m;

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = loanOffer.Id,
            BorrowerCompanyId = company.Id,
            BankBuildingId = building.Id,
            LenderCompanyId = loanOffer.LenderCompanyId,
            OriginalPrincipal = 50_000m,
            RemainingPrincipal = 50_000m,
            AnnualInterestRatePercent = 12m,
            DurationTicks = 24L,
            StartTick = 0L,
            DueTick = 24L,
            NextPaymentTick = 1L,
            PaymentAmount = 2_100m,
            TotalPayments = 24,
            BorrowerBankAccountId = account.Id,
            Status = LoanStatus.Active,
            AcceptedAtUtc = DateTime.UtcNow,
            CollateralBuildingId = building.Id,
            CollateralAppraisedValue = 1_000_000m,
        };
        db.Loans.Add(loan);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = 0;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var borrowerPrincipalEntry = await db.LedgerEntries.FirstOrDefaultAsync(e =>
            e.CompanyId == company.Id
            && e.Category == LedgerCategory.LoanRepaymentPrincipal
            && e.Amount < 0);
        Assert.NotNull(borrowerPrincipalEntry);
        Assert.Equal(account.Id, borrowerPrincipalEntry!.BankAccountId);

        var borrowerInterestEntry = await db.LedgerEntries.FirstOrDefaultAsync(e =>
            e.CompanyId == company.Id
            && e.Category == LedgerCategory.LoanInterestExpense
            && e.Amount < 0);
        Assert.NotNull(borrowerInterestEntry);
        Assert.Equal(account.Id, borrowerInterestEntry!.BankAccountId);

        var lenderPrincipalEntry = await db.LedgerEntries.FirstOrDefaultAsync(e =>
            e.CompanyId == loanOffer.LenderCompanyId
            && e.Category == LedgerCategory.LoanRepaymentPrincipal
            && e.Amount > 0);
        Assert.NotNull(lenderPrincipalEntry);
        Assert.NotNull(lenderPrincipalEntry!.BankAccountId);
    }

    // ── destruction tests (BuildingDestructionPhase) ─────────────────────────

    [Fact]
    public async Task BuildingDestruction_AfterForeclosureWindow_DestroysBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var defaultedAtTick = 100L;
        building.IsForSale = true;
        building.AskingPrice = 900_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-4);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, 1_000_000m, defaultedAtTick);

        // Set current tick to exactly 1 tick beyond the foreclosure window.
        var gs = await db.GameStates.FindAsync(1);
        // processor increments CurrentTick, so we set it to (defaultedAtTick + ForeclosureWindowTicks) and the phase sees (defaultedAtTick + ForeclosureWindowTicks + 1) as current tick
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.NotNull(building.DestroyedAtUtc);
        Assert.False(building.IsForSale);
        Assert.Null(building.AskingPrice);
    }

    [Fact]
    public async Task BuildingDestruction_PaysDebtToLender_AndReturnsSurplusToBorrower()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var appraisedValue = 1_200_000m;
        var defaultedAtTick = 200L;
        building.IsForSale = true;
        building.AskingPrice = 1_080_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-4);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, appraisedValue, defaultedAtTick);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(account).ReloadAsync();
        var lenderAccount = await db.BankAccounts.FirstAsync(a => a.CompanyId == loanOffer.LenderCompanyId);

        // Appraised 1,200,000 - debt 500,000 = surplus 700,000
        Assert.Equal(700_000m, account.Balance);
        Assert.Equal(1_500_000m, lenderAccount.Balance);

        var ledger = await db.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.BuildingSale && e.Amount > 0)
            .FirstOrDefaultAsync();
        Assert.NotNull(ledger);
        Assert.Equal(700_000m, ledger.Amount);
        Assert.Equal(account.Id, ledger.BankAccountId);

        var lenderLedger = await db.LedgerEntries
            .Where(e => e.CompanyId == loanOffer.LenderCompanyId
                && e.Category == LedgerCategory.LoanRepaymentPrincipal
                && e.Amount > 0)
            .FirstOrDefaultAsync();
        Assert.NotNull(lenderLedger);
        Assert.Equal(lenderAccount.Id, lenderLedger!.BankAccountId);
    }

    [Fact]
    public async Task BuildingDestruction_EmitsPlayerNotification()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, player, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var defaultedAtTick = 300L;
        building.IsForSale = true;
        building.AskingPrice = 900_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-4);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, 1_000_000m, defaultedAtTick);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var notification = await db.PlayerNotifications
            .Where(n => n.PlayerId == player.Id && n.Type == PlayerNotificationType.BuildingDestroyedByDefault)
            .FirstOrDefaultAsync();
        Assert.NotNull(notification);
        Assert.Contains("Foreclosure Factory", notification.Message);
    }

    [Fact]
    public async Task BuildingDestruction_FreesLot_AfterDestruction()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = building.CityId,
            Name = "Test Foreclosure Lot",
            District = "Industrial",
            SuitableTypes = "FACTORY",
            Price = 100_000m,
            BasePrice = 90_000m,
            OwnerCompanyId = company.Id,
            BuildingId = building.Id,
        };
        db.BuildingLots.Add(lot);

        var defaultedAtTick = 400L;
        building.IsForSale = true;
        building.AskingPrice = 900_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-4);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, 1_000_000m, defaultedAtTick);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(lot).ReloadAsync();
        Assert.Null(lot.OwnerCompanyId);
        Assert.Null(lot.BuildingId);
    }

    [Fact]
    public async Task BuildingDestruction_DoesNotDestroyBuilding_BeforeForeclosureWindow()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var defaultedAtTick = 500L;
        building.IsForSale = true;
        building.AskingPrice = 900_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-1);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, 1_000_000m, defaultedAtTick);

        // Set tick to only 50 ticks after default (< 72 tick window).
        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + 49L;  // processor increments to +50
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.Null(building.DestroyedAtUtc);
        Assert.True(building.IsForSale);
    }

    [Fact]
    public async Task BuildingDestruction_DoesNotDestroy_WhenBuildingSoldBeforeWindow()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, account, loanOffer) = await SeedBaseAsync(scope);

        var defaultedAtTick = 600L;
        // Building was sold (not for sale anymore).
        building.IsForSale = false;
        building.AskingPrice = null;
        building.ListedAtUtc = null;

        await AddDefaultedLoanAsync(db, company, building, loanOffer, 1_000_000m, defaultedAtTick);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.Null(building.DestroyedAtUtc);
    }

    [Fact]
    public async Task BuildingDestructionPhase_ForeclosureWindowTicks_IsSeventyTwo()
    {
        // Constant verification — changing this breaks the 3-game-day contract.
        Assert.Equal(72L, BuildingDestructionPhase.ForeclosureWindowTicks);
    }

    [Fact]
    public async Task BuildingDestruction_CreatesDestructionRecord_WithCorrectFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var (db, _, company, building, _, loanOffer) = await SeedBaseAsync(scope);

        var appraisedValue = 500_000m;
        var defaultedAtTick = 300L;
        building.IsForSale = true;
        building.AskingPrice = 450_000m;
        building.ListedAtUtc = DateTime.UtcNow.AddDays(-4);

        await AddDefaultedLoanAsync(db, company, building, loanOffer, appraisedValue, defaultedAtTick);

        var gs = await db.GameStates.FindAsync(1);
        gs!.CurrentTick = defaultedAtTick + BuildingDestructionPhase.ForeclosureWindowTicks;
        await db.SaveChangesAsync();

        var processor = CreateProcessor(scope);
        await processor.ProcessTickAsync();

        var record = await db.BuildingDestructionRecords
            .FirstOrDefaultAsync(r => r.BuildingId == building.Id);
        Assert.NotNull(record);
        Assert.Equal(building.Id, record.BuildingId);
        Assert.Equal(appraisedValue, record.OriginalPropertyValue);
        // Compensation equals borrower surplus after debt payout.
        Assert.Equal(0m, record.CompensationPaid);
        Assert.Equal(BuildingDestructionReason.GracePeriodExpired, record.DestructionReason);
        // Tick count is stored as the tick AFTER the processor increments.
        Assert.True(record.DestructionTickCount > defaultedAtTick);
    }

    [Fact]
    public async Task GameConstants_ForeclosureWindowTicks_MatchesBuildingDestructionPhase()
    {
        // Ensures GameConstants and BuildingDestructionPhase stay in sync.
        Assert.Equal(GameConstants.ForeclosureWindowTicks, BuildingDestructionPhase.ForeclosureWindowTicks);
    }

    [Fact]
    public async Task GameConstants_ForeclosureAutoListDiscount_IsTenPercent()
    {
        Assert.Equal(0.10m, GameConstants.ForeclosureAutoListDiscount);
    }
}
