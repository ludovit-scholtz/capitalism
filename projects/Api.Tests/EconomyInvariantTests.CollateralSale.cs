using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed partial class EconomyInvariantTests
{
    /// <summary>
    /// #607: a borrower whose loan defaulted sells the pledged building. The sale proceeds must settle the lien (lender credited,
    /// loan closed, collateral released) and the seller receives only the remainder, with every economy invariant intact.
    /// </summary>
    [Fact]
    public async Task SellingADefaultedLoansCollateral_SettlesTheLienAndKeepsInvariants()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var rng = new Random(404);
        var world = await SeedWorldAsync(factory, client, 404);
        EconomySnapshot baseline;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            baseline = await EconomyInvariantChecker.TakeSnapshotAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        PlayerSlot borrower;
        var loanError = await RandomLoanAsync(client, world, rng);
        Assert.True(loanError is null, $"Loan failed: {loanError}");

        Guid collateralBuildingId;
        Guid loanId;
        Guid lenderCompany;
        decimal debt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loan = await db.Loans.SingleAsync(); // only the scripted loan exists in this world
            Assert.NotNull(loan.CollateralBuildingId);
            // An Active/Overdue lien locks the building; only a defaulted lien is settled from the sale.
            loan.Status = LoanStatus.Defaulted;
            await db.SaveChangesAsync();
            loanId = loan.Id;
            lenderCompany = loan.LenderCompanyId;
            collateralBuildingId = loan.CollateralBuildingId!.Value;
            debt = loan.RemainingPrincipal;
            borrower = world.Players.Single(p => p.CompanyId == loan.BorrowerCompanyId);
        }

        var buyer = world.Players.First(p => p != borrower && p.CompanyId != lenderCompany);
        var price = 900_000m + debt; // above the sale floor and the debt, so the lien is fully settled with a remainder for the seller
        EconomySnapshot beforeSale;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            beforeSale = await EconomyInvariantChecker.TakeSnapshotAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        var listed = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId = collateralBuildingId, isForSale = true, askingPrice = price } },
            borrower.Token);
        Assert.Null(DescribeError(listed));
        var offer = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = collateralBuildingId, buyerCompanyId = buyer.CompanyId, offeredPrice = price } },
            buyer.Token);
        Assert.Null(DescribeError(offer));
        var offerData = offer.GetProperty("data").GetProperty("makeOfferOnBuilding");
        var accepted = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { __typename } }",
            new { input = new { offerId = offerData.GetProperty("id").GetString(), offerVersion = offerData.GetProperty("offerVersion").GetString() } },
            borrower.Token);
        Assert.Null(DescribeError(accepted));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var afterSale = await EconomyInvariantChecker.TakeSnapshotAsync(verifyDb);
        decimal Delta(Guid company) => afterSale.CompanyBalances.GetValueOrDefault(company) - beforeSale.CompanyBalances.GetValueOrDefault(company);
        Assert.Equal(debt, Delta(lenderCompany));            // the lender is repaid in full from the proceeds
        Assert.Equal(price - debt, Delta(borrower.CompanyId)); // the seller keeps only the remainder
        Assert.Equal(-price, Delta(buyer.CompanyId));
        var settled = await verifyDb.Loans.AsNoTracking().SingleAsync(l => l.Id == loanId);
        output.WriteLine($"Debt {debt}, loan status after sale: {settled.Status}");
        Assert.Equal(LoanStatus.Repaid, settled.Status);
        Assert.Equal(0m, settled.RemainingPrincipal);
        Assert.Null(settled.CollateralBuildingId);
        Assert.Equal(buyer.CompanyId, (await verifyDb.Buildings.AsNoTracking().FirstAsync(b => b.Id == collateralBuildingId)).CompanyId);
        var violations = await EconomyInvariantChecker.CheckAllAsync(verifyDb, baseline);
        Assert.True(violations.Count == 0, $"Invariants violated after a collateral sale:\n{string.Join('\n', violations)}");
    }
}
