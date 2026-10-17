using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Property-style economy invariant suite. Drives the real tick pipeline plus seeded random player actions
/// and asserts money conservation, no negative balances, matched ledger entries and single building owners.
/// Reproduce a failure by re-running the Theory with the logged seed (see README-economy-invariants.md).
/// </summary>
public sealed class EconomyInvariantTests(ITestOutputHelper output)
{
    private const int PlayerCount = 4;
    private const int Steps = 40;

    [Theory]
    [InlineData(101)]
    [InlineData(202)]
    [InlineData(303)]
    public async Task RandomActionsAndTicks_PreserveEconomyInvariants(int seed)
    {
        output.WriteLine($"Economy invariant seed={seed}");
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        var rng = new Random(seed);
        var world = await SeedWorldAsync(factory, client, seed);

        EconomySnapshot baseline;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            baseline = await EconomyInvariantChecker.TakeSnapshotAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        }

        var contracts = new List<(Guid Id, int SellerIdx, int BuyerIdx)>();
        for (var step = 1; step <= Steps; step++)
        {
            switch (rng.Next(5))
            {
                case 0:
                case 1:
                    await RandomTransferAsync(client, world, rng);
                    break;
                case 2:
                    await RandomProposeAsync(client, world, rng, contracts);
                    break;
                case 3:
                    await RandomContractActionAsync(client, world, rng, contracts);
                    break;
                default:
                    await TickAsync(factory);
                    break;
            }

            if (step % 10 == 0)
            {
                await AssertInvariantsAsync(factory, baseline, seed, step);
            }
        }

        await TickAsync(factory);
        await AssertInvariantsAsync(factory, baseline, seed, Steps + 1);
    }

    [Fact]
    public async Task Checker_DetectsNegativeBalance()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.BankAccounts.Add(NewAccount(null, "EUR", -5m));
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckNoNegativeBalancesAsync(db);

        Assert.Contains(violations, v => v.StartsWith("NEGATIVE_BALANCE"));
    }

    [Fact]
    public async Task Checker_DetectsOrphanBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        db.Buildings.Add(new Building
        {
            Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), CityId = city.Id, Type = BuildingType.Factory,
            Name = "Orphan", Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckBuildingOwnershipAsync(db);

        Assert.Contains(violations, v => v.StartsWith("ORPHAN_BUILDING"));
    }

    [Fact]
    public async Task Checker_DetectsUnmatchedLedgerEntry()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(), CompanyId = Guid.NewGuid(), BankAccountId = Guid.NewGuid(),
            Category = LedgerCategory.Other, Description = "dangling", Amount = 1m
        });
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckLedgerMatchedAsync(db);

        Assert.Contains(violations, v => v.StartsWith("UNMATCHED_LEDGER"));
    }

    [Fact]
    public async Task Checker_DetectsMoneyCreatedWithoutLedger()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var account = NewAccount(Guid.NewGuid(), "EUR", 100m);
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();
        var baseline = await EconomyInvariantChecker.TakeSnapshotAsync(db);

        account.Balance += 50m; // minted out of thin air: no ledger row
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckMoneyConservationAsync(db, baseline);

        Assert.Contains(violations, v => v.StartsWith("MONEY_NOT_CONSERVED"));
    }

    private static async Task AssertInvariantsAsync(ApiWebApplicationFactory factory, EconomySnapshot baseline, int seed, int step)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tick = await db.GameStates.AsNoTracking().Select(g => g.CurrentTick).FirstAsync();
        var violations = await EconomyInvariantChecker.CheckAllAsync(db, baseline);
        Assert.True(violations.Count == 0, $"Invariants violated (seed={seed}, step={step}, tick={tick}):\n{string.Join('\n', violations)}");
    }

    private static async Task TickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetRequiredService<IEnumerable<ITickPhase>>();
        await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
    }

    private static async Task RandomTransferAsync(HttpClient client, World world, Random rng)
    {
        var p = world.Players[rng.Next(world.Players.Count)];
        var (from, to) = rng.Next(2) == 0 ? (p.AccountA, p.AccountB) : (p.AccountB, p.AccountA);
        var amount = Math.Round((decimal)rng.NextDouble() * 400_000m, 2) + 1m;
        await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: TransferFundsInput!) { transferFunds(input: $input) { __typename } }",
            new { input = new { fromBankAccountId = from, toBankAccountId = to, amount } },
            p.Token);
    }

    private static async Task RandomProposeAsync(HttpClient client, World world, Random rng, List<(Guid Id, int SellerIdx, int BuyerIdx)> contracts)
    {
        var s = rng.Next(world.Players.Count);
        var b = (s + 1 + rng.Next(world.Players.Count - 1)) % world.Players.Count;
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: ProposeSupplyContractInput!) { proposeSupplyContract(input: $input) { contract { id } } }",
            new
            {
                input = new
                {
                    sellerCompanyId = world.Players[s].CompanyId,
                    buyerCompanyId = world.Players[b].CompanyId,
                    sellerBuildingUnitId = world.Players[s].SalesUnitId,
                    resourceTypeId = world.ResourceTypeId,
                    quantityPerTick = (decimal)(10 + rng.Next(90)),
                    pricePerUnit = (decimal)(5 + rng.Next(20)),
                    durationTicks = 5 + rng.Next(20),
                    penaltyRatePercent = 10m
                }
            },
            world.Players[s].Token);
        if (result.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object
            && data.GetProperty("proposeSupplyContract").ValueKind == JsonValueKind.Object)
        {
            contracts.Add((Guid.Parse(data.GetProperty("proposeSupplyContract").GetProperty("contract").GetProperty("id").GetString()!), s, b));
        }
    }

    private static async Task RandomContractActionAsync(HttpClient client, World world, Random rng, List<(Guid Id, int SellerIdx, int BuyerIdx)> contracts)
    {
        if (contracts.Count == 0) return;
        var (id, s, b) = contracts[rng.Next(contracts.Count)];
        var (action, actor) = rng.Next(3) switch
        {
            0 => ("acceptSupplyContract", b),
            1 => ("rejectSupplyContract", b),
            _ => ("cancelSupplyContract", rng.Next(2) == 0 ? s : b)
        };
        await TestHelpers.ExecuteGraphQlAsync(
            client,
            $"mutation($id: UUID!) {{ {action}(id: $id) {{ success }} }}",
            new { id },
            world.Players[actor].Token);
    }

    private static BankAccount NewAccount(Guid? companyId, string currency, decimal balance) => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        CurrencyCode = currency,
        Balance = balance,
        AccountNumber = string.Concat(Enumerable.Range(0, 16).Select(_ => Random.Shared.Next(10))),
        CreatedAtUtc = DateTime.UtcNow
    };

    private static async Task<World> SeedWorldAsync(ApiWebApplicationFactory factory, HttpClient client, int seed)
    {
        var tokens = new List<(string Token, Guid PlayerId)>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var reg = await TestHelpers.ExecuteGraphQlAsync(
                client,
                "mutation($input: RegisterInput!) { register(input: $input) { token } }",
                new { input = new { email = $"inv-{seed}-{i}-{Guid.NewGuid():N}@example.com", displayName = $"Inv {i}", password = "TestPass123!" } });
            var token = reg.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
            var me = await TestHelpers.ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
            tokens.Add((token, Guid.Parse(me.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!)));
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var resource = await db.ResourceTypes.OrderBy(r => r.Name).FirstAsync();
        var players = new List<PlayerSlot>();
        for (var i = 0; i < PlayerCount; i++)
        {
            var company = new Company { Id = Guid.NewGuid(), PlayerId = tokens[i].PlayerId, Name = $"Inv Co {i}", FoundedAtTick = 0, FoundedAtUtc = DateTime.UtcNow };
            var building = new Building
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = $"Plant {i}",
                Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow
            };
            var sales = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.B2BSales, GridX = 0, GridY = 0, ResourceTypeId = resource.Id, Level = 1 };
            var purchase = new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Purchase, GridX = 1, GridY = 0, ResourceTypeId = resource.Id, Level = 1 };
            var accountA = NewAccount(company.Id, city.CurrencyCode, 1_000_000m);
            var accountB = NewAccount(company.Id, city.CurrencyCode, 500_000m);
            db.Companies.Add(company);
            db.Buildings.Add(building);
            db.BuildingUnits.AddRange(sales, purchase);
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = sales.Id, ResourceTypeId = resource.Id,
                Quantity = 2000m, Quality = 0.7m, SourcingCostTotal = 6000m
            });
            db.BankAccounts.AddRange(accountA, accountB);
            players.Add(new PlayerSlot(tokens[i].Token, company.Id, sales.Id, accountA.Id, accountB.Id));
        }

        await db.SaveChangesAsync();
        return new World(players, resource.Id);
    }

    private sealed record PlayerSlot(string Token, Guid CompanyId, Guid SalesUnitId, Guid AccountA, Guid AccountB);

    private sealed record World(List<PlayerSlot> Players, Guid ResourceTypeId);
}
