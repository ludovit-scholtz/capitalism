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

        var lenderCompany = world.Players[0].CompanyId;
        var borrower = world.Players[1];
        var loanError = await RandomLoanAsync(client, world, rng);
        Assert.True(loanError is null, $"Loan failed: {loanError}");

        Guid collateralBuildingId;
        decimal debt;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var loan = await db.Loans.OrderBy(l => l.StartTick).FirstAsync(l => l.BorrowerCompanyId != lenderCompany);
            loan.Status = LoanStatus.Defaulted; // an Active/Overdue lien locks the building; only a defaulted lien is settled from the sale
            await db.SaveChangesAsync();
            collateralBuildingId = loan.CollateralBuildingId!.Value;
            debt = loan.RemainingPrincipal;
            borrower = world.Players.Single(p => p.CompanyId == loan.BorrowerCompanyId);
        }

        var buyer = world.Players.First(p => p != borrower && p.CompanyId != lenderCompany);
        var price = 900_000m;
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
        var settled = await verifyDb.Loans.AsNoTracking().FirstAsync(l => l.BorrowerCompanyId == borrower.CompanyId);
        output.WriteLine($"Debt {debt}, loan status after sale: {settled.Status}");
        Assert.Equal(LoanStatus.Repaid, settled.Status);
        Assert.Equal(0m, settled.RemainingPrincipal);
        Assert.Null(settled.CollateralBuildingId);
        Assert.Equal(buyer.CompanyId, (await verifyDb.Buildings.AsNoTracking().FirstAsync(b => b.Id == collateralBuildingId)).CompanyId);
        var violations = await EconomyInvariantChecker.CheckAllAsync(verifyDb, baseline);
        Assert.True(violations.Count == 0, $"Invariants violated after a collateral sale:\n{string.Join('\n', violations)}");
    }
}
