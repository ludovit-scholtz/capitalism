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

/// <summary>
/// A missed loan payment adds a penalty to the loan balance (a debt); no cash moves. The ledger must therefore never show a
/// cash debit at that moment (otherwise bank statements and company balances drift apart, as the economy invariant suite found),
/// and the penalty must reach the books exactly when it is actually paid, as a real expense for the borrower and income for
/// the lender, without changing the total cash that moves.
/// </summary>
public sealed class LoanPenaltyLedgerTests
{
    private sealed record Fixture(Guid LenderCompanyId, Guid BorrowerCompanyId, Guid LenderAccountId, Guid BorrowerAccountId, Guid LoanId);

    private static async Task<Fixture> SeedLoanAsync(ApiWebApplicationFactory factory, Guid? borrowerPlayerId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var tick = (await db.GameStates.FirstAsync()).CurrentTick;

        var lenderPlayer = new Player { Id = Guid.NewGuid(), Email = $"pen-lender-{Guid.NewGuid():N}@example.com", DisplayName = "Lender", PasswordHash = "x" };
        var lender = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayer.Id, Name = "Penalty Lender" };
        db.Players.Add(lenderPlayer);
        if (borrowerPlayerId is null)
        {
            var borrowerPlayer = new Player { Id = Guid.NewGuid(), Email = $"pen-borrower-{Guid.NewGuid():N}@example.com", DisplayName = "Borrower", PasswordHash = "x" };
            db.Players.Add(borrowerPlayer);
            borrowerPlayerId = borrowerPlayer.Id;
        }

        var borrower = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayerId.Value, Name = "Penalty Borrower" };
        var bank = new Building
        {
            Id = Guid.NewGuid(), CompanyId = lender.Id, CityId = city.Id, Type = BuildingType.Bank, Name = "Penalty Bank",
            BaseCapitalDeposited = true, TotalDeposits = 100_000m, LendingInterestRatePercent = 10m,
            Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
        };
        var lenderAccount = new BankAccount { Id = Guid.NewGuid(), CompanyId = lender.Id, CurrencyCode = city.CurrencyCode, Balance = 50_000m, AccountNumber = Guid.NewGuid().ToString("N")[..16], CreatedAtUtc = DateTime.UtcNow };
        var borrowerAccount = new BankAccount { Id = Guid.NewGuid(), CompanyId = borrower.Id, CurrencyCode = city.CurrencyCode, Balance = 0m, AccountNumber = Guid.NewGuid().ToString("N")[..16], CreatedAtUtc = DateTime.UtcNow };
        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(), BankBuildingId = bank.Id, LenderCompanyId = lender.Id, AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 10_000m, TotalCapacity = 10_000m, UsedCapacity = 10_000m, DurationTicks = 100, IsActive = true,
            CreatedAtTick = 1, CreatedAtUtc = DateTime.UtcNow,
        };
        var loan = new Loan
        {
            Id = Guid.NewGuid(), LoanOfferId = offer.Id, BorrowerCompanyId = borrower.Id, BorrowerBankAccountId = borrowerAccount.Id,
            BankBuildingId = bank.Id, LenderCompanyId = lender.Id, OriginalPrincipal = 10_000m, RemainingPrincipal = 10_000m,
            AnnualInterestRatePercent = 10m, DurationTicks = 100, StartTick = tick, DueTick = tick + 100, NextPaymentTick = tick + 1,
            PaymentAmount = 1_000m, PaymentsMade = 0, TotalPayments = 10, Status = LoanStatus.Active, AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Companies.AddRange(lender, borrower);
        db.Buildings.Add(bank);
        db.BankAccounts.AddRange(lenderAccount, borrowerAccount);
        db.LoanOffers.Add(offer);
        db.Loans.Add(loan);
        await db.SaveChangesAsync();
        return new Fixture(lender.Id, borrower.Id, lenderAccount.Id, borrowerAccount.Id, loan.Id);
    }

    private static async Task TickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance).ProcessTickAsync();
    }

    private static async Task<decimal> BalanceAsync(ApiWebApplicationFactory factory, Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<AppDbContext>().BankAccounts.AsNoTracking().FirstAsync(a => a.Id == accountId)).Balance;
    }

    private static async Task<List<LedgerEntry>> RowsAsync(ApiWebApplicationFactory factory, Guid companyId, long fromTickInclusive = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().LedgerEntries.AsNoTracking()
            .Where(e => e.CompanyId == companyId && e.RecordedAtTick >= fromTickInclusive)
            .ToListAsync();
    }

    private static async Task<Loan> LoanAsync(ApiWebApplicationFactory factory, Guid loanId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Loans.AsNoTracking().FirstAsync(l => l.Id == loanId);
    }

    private static async Task SetBalanceAsync(ApiWebApplicationFactory factory, Guid accountId, decimal balance)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankAccounts.FirstAsync(a => a.Id == accountId)).Balance = balance;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task MissedPayment_AddsThePenaltyToTheLoanWithoutAnyCashMovementInTheLedger()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var f = await SeedLoanAsync(factory);

        await TickAsync(factory);

        var loan = await LoanAsync(factory, f.LoanId);
        var penaltyRows = (await RowsAsync(factory, f.BorrowerCompanyId)).Where(r => r.Category == LedgerCategory.LoanPenalty).ToList();
        Assert.Equal(1, loan.MissedPayments);
        Assert.True(loan.AccumulatedPenalty > 0m);
        Assert.Equal(10_000m + loan.AccumulatedPenalty, loan.RemainingPrincipal);
        Assert.Equal(0m, await BalanceAsync(factory, f.BorrowerAccountId)); // no cash left the account
        var row = Assert.Single(penaltyRows); // the accrual is still visible as an informational row
        Assert.Equal(0m, row.Amount);
        Assert.Contains("penalty", row.Description);
    }

    [Fact]
    public async Task ScheduledPaymentAfterAMiss_BooksThePenaltyShareAsExpenseAndIncomeWithoutChangingCash()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var f = await SeedLoanAsync(factory);
        await TickAsync(factory); // tick 1: the borrower cannot pay -> penalty accrues
        var afterMiss = await LoanAsync(factory, f.LoanId);

        // The borrower recovers and the next scheduled payment (tick 2) succeeds.
        await SetBalanceAsync(factory, f.BorrowerAccountId, 50_000m);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Loans.FirstAsync(l => l.Id == f.LoanId)).NextPaymentTick = 2;
            await db.SaveChangesAsync();
        }

        var borrowerBefore = await BalanceAsync(factory, f.BorrowerAccountId);
        var lenderBefore = await BalanceAsync(factory, f.LenderAccountId);
        await TickAsync(factory);

        var after = await LoanAsync(factory, f.LoanId);
        var borrowerRows = await RowsAsync(factory, f.BorrowerCompanyId, fromTickInclusive: 2);
        var lenderRows = await RowsAsync(factory, f.LenderCompanyId, fromTickInclusive: 2);
        var penaltyPaid = afterMiss.AccumulatedPenalty - after.AccumulatedPenalty;

        Assert.Equal(1, after.PaymentsMade);
        Assert.True(penaltyPaid > 0m, "part of the accumulated penalty must be settled by the payment");
        Assert.True(after.AccumulatedPenalty >= 0m);
        Assert.Equal(-penaltyPaid, borrowerRows.Where(r => r.Category == LedgerCategory.LoanPenalty).Sum(r => r.Amount));
        Assert.Contains(lenderRows, r => r.Category == LedgerCategory.LoanInterestIncome && r.Description.Contains("penalty") && r.Amount == penaltyPaid);
        // Cash is unchanged by the split: both ledgers still reconcile exactly with the balances.
        Assert.Equal(borrowerBefore + borrowerRows.Sum(r => r.Amount), await BalanceAsync(factory, f.BorrowerAccountId));
        Assert.Equal(lenderBefore + lenderRows.Sum(r => r.Amount), await BalanceAsync(factory, f.LenderAccountId));
    }

    [Fact]
    public async Task ManualPayoff_BooksThePenaltyAsExpenseAndIncomeAndPrincipalForTheRest()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email = $"pen-payoff-{Guid.NewGuid():N}@example.com", displayName = "Payoff", password = "TestPass123!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var me = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        var f = await SeedLoanAsync(factory, Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!));
        await TickAsync(factory); // miss -> penalty accrues
        var afterMiss = await LoanAsync(factory, f.LoanId);
        await SetBalanceAsync(factory, f.BorrowerAccountId, 50_000m);
        var borrowerBefore = await BalanceAsync(factory, f.BorrowerAccountId);
        var lenderBefore = await BalanceAsync(factory, f.LenderAccountId);

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: RepayLoanDebtInput!) { repayLoanDebt(input: $input) { id status } }",
            new { input = new { loanId = f.LoanId } },
            token);

        Assert.False(result.TryGetProperty("errors", out var errors), $"repayLoanDebt failed: {errors}");
        var borrowerRows = await RowsAsync(factory, f.BorrowerCompanyId, fromTickInclusive: 1);
        var lenderRows = await RowsAsync(factory, f.LenderCompanyId, fromTickInclusive: 1);
        var payoff = afterMiss.RemainingPrincipal;
        var penalty = afterMiss.AccumulatedPenalty;
        Assert.Equal(-penalty, borrowerRows.Where(r => r.Category == LedgerCategory.LoanPenalty && r.Amount < 0m).Sum(r => r.Amount), 2);
        Assert.Equal(-(payoff - penalty), borrowerRows.Where(r => r.Category == LedgerCategory.LoanRepaymentPrincipal).Sum(r => r.Amount), 2);
        Assert.Equal(penalty, lenderRows.Where(r => r.Category == LedgerCategory.LoanInterestIncome && r.Description.Contains("penalty")).Sum(r => r.Amount), 2);
        Assert.Equal(borrowerBefore + borrowerRows.Sum(r => r.Amount), await BalanceAsync(factory, f.BorrowerAccountId), 2);
        Assert.Equal(lenderBefore + lenderRows.Sum(r => r.Amount), await BalanceAsync(factory, f.LenderAccountId), 2);
        Assert.Equal(0m, (await LoanAsync(factory, f.LoanId)).AccumulatedPenalty);
    }
}
