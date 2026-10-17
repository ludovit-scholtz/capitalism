using Api.Data.Entities;
using Api.Engine;

namespace Api.Tests;

public sealed class TickContextAssetValueTests(Xunit.Abstractions.ITestOutputHelper output)
{
    private static TickContext BuildContext(int companies, int buildingsPerCompany)
    {
        var companyMap = new Dictionary<Guid, Company>();
        var buildings = new Dictionary<Guid, Building>();
        var accounts = new Dictionary<Guid, BankAccount>();
        var inventories = new Dictionary<Guid, List<Inventory>>();
        var lots = new Dictionary<Guid, List<BuildingLot>>();
        var resource = new ResourceType { Id = Guid.NewGuid(), Name = "Wood", BasePrice = 4m };
        var product = new ProductType { Id = Guid.NewGuid(), Name = "Chair", BasePrice = 30m };

        for (var c = 0; c < companies; c++)
        {
            var company = new Company { Id = Guid.NewGuid(), PlayerId = Guid.NewGuid(), Name = $"Co {c}" };
            companyMap[company.Id] = company;
            var account = new BankAccount { Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = "EUR", Balance = 1_000m * (c + 1) };
            accounts[account.Id] = account;
            if (c % 2 == 0)
            {
                lots[company.Id] = [new BuildingLot { Id = Guid.NewGuid(), OwnerCompanyId = company.Id, Price = 50_000m * (c + 1) }];
            }

            for (var b = 0; b < buildingsPerCompany; b++)
            {
                var building = new Building { Id = Guid.NewGuid(), CompanyId = company.Id, Type = BuildingType.Factory, Level = 1 + b };
                buildings[building.Id] = building;
                inventories[building.Id] =
                [
                    new Inventory { Id = Guid.NewGuid(), BuildingId = building.Id, ResourceTypeId = resource.Id, Quantity = 10m * (b + 1) },
                    new Inventory { Id = Guid.NewGuid(), BuildingId = building.Id, ProductTypeId = product.Id, Quantity = 3m },
                ];
            }
        }

        return new TickContext
        {
            Db = null!,
            GameState = new GameState { CurrentTick = 1 },
            CompaniesById = companyMap,
            BuildingsById = buildings,
            BankAccountsById = accounts,
            InventoryByBuilding = inventories,
            LotsByCompany = lots,
            ResourceTypesById = new Dictionary<Guid, ResourceType> { [resource.Id] = resource },
            ProductTypesById = new Dictionary<Guid, ProductType> { [product.Id] = product },
        };
    }

    [Fact]
    public void ComputeAllCompanyAssetValues_EqualsThePerCompanyCalculationForEveryCompany()
    {
        var context = BuildContext(companies: 7, buildingsPerCompany: 4);

        var all = context.ComputeAllCompanyAssetValues();

        Assert.Equal(context.CompaniesById.Count, all.Count);
        foreach (var companyId in context.CompaniesById.Keys)
        {
            Assert.Equal(context.GetCompanyAssetValue(companyId), all[companyId]);
        }
    }

    [Fact]
    public void ComputeAllCompanyAssetValues_IncludesCompaniesWithoutBuildings()
    {
        var context = BuildContext(companies: 3, buildingsPerCompany: 0);

        var all = context.ComputeAllCompanyAssetValues();

        Assert.Equal(3, all.Count);
        Assert.All(context.CompaniesById.Keys, id => Assert.Equal(context.GetCompanyAssetValue(id), all[id]));
        Assert.All(all.Values, value => Assert.True(value > 0m)); // bank balance and lots still count
    }

    [Fact]
    public void ComputeAllCompanyAssetValues_OnAnEmptyWorld_IsEmpty()
    {
        Assert.Empty(BuildContext(companies: 0, buildingsPerCompany: 0).ComputeAllCompanyAssetValues());
    }

    [Fact]
    public void BatchSnapshot_AtTheTargetScale_IsFarCheaperThanPerBuildingRecalculation()
    {
        // 1,000 companies x 20 buildings = the 20,000 buildings of the product requirement.
        var context = BuildContext(companies: 1_000, buildingsPerCompany: 20);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var batch = context.ComputeAllCompanyAssetValues();
        var batchMs = sw.ElapsedMilliseconds;

        // What OperatingCostPhase used to do: one full scan per building.
        sw.Restart();
        decimal checksum = 0m;
        foreach (var building in context.BuildingsById.Values.Take(2_000))
        {
            checksum += context.GetCompanyAssetValue(building.CompanyId);
        }

        var perBuildingMsFor2000 = sw.ElapsedMilliseconds;
        output.WriteLine($"batch for 20,000 buildings: {batchMs} ms; old per-building recalculation: {perBuildingMsFor2000} ms for the first 2,000 buildings (about {perBuildingMsFor2000 * 10} ms for all 20,000)");

        Assert.True(checksum > 0m);
        Assert.Equal(1_000, batch.Count);
    }
}
