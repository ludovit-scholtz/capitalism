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
/// cash debit for it, otherwise bank statements and company balances drift apart (found by the economy invariant suite).
/// </summary>
public sealed class LoanPenaltyLedgerTests
{
    [Fact]
    public async Task MissedPayment_AddsThePenaltyToTheLoanWithoutAnyCashMovementInTheLedger()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        Guid borrowerCompanyId, borrowerAccountId, loanId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var tick = (await db.GameStates.FirstAsync()).CurrentTick;

            var lenderPlayer = new Player { Id = Guid.NewGuid(), Email = $"pen-lender-{Guid.NewGuid():N}@example.com", DisplayName = "Lender", PasswordHash = "x" };
            var borrowerPlayer = new Player { Id = Guid.NewGuid(), Email = $"pen-borrower-{Guid.NewGuid():N}@example.com", DisplayName = "Borrower", PasswordHash = "x" };
            var lender = new Company { Id = Guid.NewGuid(), PlayerId = lenderPlayer.Id, Name = "Penalty Lender" };
            var borrower = new Company { Id = Guid.NewGuid(), PlayerId = borrowerPlayer.Id, Name = "Penalty Borrower" };
            var bank = new Building
            {
                Id = Guid.NewGuid(), CompanyId = lender.Id, CityId = city.Id, Type = BuildingType.Bank, Name = "Penalty Bank",
                BaseCapitalDeposited = true, TotalDeposits = 100_000m, LendingInterestRatePercent = 10m,
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow,
            };
            var lenderAccount = new BankAccount { Id = Guid.NewGuid(), CompanyId = lender.Id, CurrencyCode = city.CurrencyCode, Balance = 50_000m, AccountNumber = "3000000000000001", CreatedAtUtc = DateTime.UtcNow };
            var borrowerAccount = new BankAccount { Id = Guid.NewGuid(), CompanyId = borrower.Id, CurrencyCode = city.CurrencyCode, Balance = 0m, AccountNumber = "3000000000000002", CreatedAtUtc = DateTime.UtcNow };
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
            db.Players.AddRange(lenderPlayer, borrowerPlayer);
            db.Companies.AddRange(lender, borrower);
            db.Buildings.Add(bank);
            db.BankAccounts.AddRange(lenderAccount, borrowerAccount);
            db.LoanOffers.Add(offer);
            db.Loans.Add(loan);
            await db.SaveChangesAsync();
            (borrowerCompanyId, borrowerAccountId, loanId) = (borrower.Id, borrowerAccount.Id, loan.Id);
        }

        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
            await new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance).ProcessTickAsync();
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var loanAfter = await verifyDb.Loans.AsNoTracking().FirstAsync(l => l.Id == loanId);
        var borrowerRows = await verifyDb.LedgerEntries.AsNoTracking()
            .Where(e => e.CompanyId == borrowerCompanyId && e.Category == LedgerCategory.LoanPenalty)
            .ToListAsync();
        var borrowerBalance = (await verifyDb.BankAccounts.AsNoTracking().FirstAsync(a => a.Id == borrowerAccountId)).Balance;

        Assert.Equal(1, loanAfter.MissedPayments);
        Assert.True(loanAfter.AccumulatedPenalty > 0m);
        Assert.Equal(10_000m + loanAfter.AccumulatedPenalty, loanAfter.RemainingPrincipal);
        Assert.Equal(0m, borrowerBalance); // no cash left the account
        Assert.Single(borrowerRows); // the accrual is still visible on the statement as an informational row
        Assert.All(borrowerRows, row => Assert.Equal(0m, row.Amount));
        Assert.Contains(loanAfter.AccumulatedPenalty.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture), borrowerRows[0].Description.Replace(',', '.'));
    }
}
