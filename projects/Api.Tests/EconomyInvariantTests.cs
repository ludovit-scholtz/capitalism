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
        await ScriptedAcceptAndBuyerCancelAsync(client, world, rng);
        Assert.Null(await RandomForexSwapAsync(client, world, rng));
        var scriptedSaleError = await BuildingSaleAsync(client, world, rng);
        Assert.True(scriptedSaleError is null, $"Scripted building sale failed (seed={seed}): {scriptedSaleError}");
        var applied = new Dictionary<string, int>();
        for (var step = 1; step <= Steps; step++)
        {
            switch (rng.Next(7))
            {
                case 0:
                case 1:
                    if (await RandomTransferAsync(client, world, rng)) applied["transfer"] = applied.GetValueOrDefault("transfer") + 1;
                    break;
                case 2:
                    if (await RandomProposeAsync(client, world, rng, contracts)) applied["propose"] = applied.GetValueOrDefault("propose") + 1;
                    break;
                case 3:
                    var done = await RandomContractActionAsync(client, world, rng, contracts);
                    if (done is not null) applied[done] = applied.GetValueOrDefault(done) + 1;
                    break;
                case 4:
                    var forexError = await RandomForexSwapAsync(client, world, rng);
                    // Running out of funds is a legitimate outcome of random play; any other failure is a defect.
                    Assert.True(forexError is null or "INSUFFICIENT_FUNDS", $"Unexpected forex failure (seed={seed}, step={step}): {forexError}");
                    if (forexError is null) applied["forex"] = applied.GetValueOrDefault("forex") + 1;
                    break;
                case 5:
                    if (await BuildingSaleAsync(client, world, rng) is null) applied["buildingSale"] = applied.GetValueOrDefault("buildingSale") + 1;
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

        // Guard against a vacuous run: the random generator must have actually moved money.
        output.WriteLine($"Applied actions: {string.Join(", ", applied.Select(kv => $"{kv.Key}={kv.Value}"))}");
        Assert.True(applied.GetValueOrDefault("transfer") > 0, $"No transfer succeeded (seed={seed}).");
        // Contracts, forex swaps and a building sale are guaranteed by the scripted flows at the start (they assert success).
    }

    /// <summary>
    /// Deterministically exercises the buyer early-cancellation fee path (propose, accept, buyer cancels)
    /// so the invariant run always covers money moved by a contract cancellation.
    /// </summary>
    private static async Task ScriptedAcceptAndBuyerCancelAsync(HttpClient client, World world, Random rng)
    {
        var contracts = new List<(Guid Id, int SellerIdx, int BuyerIdx)>();
        Assert.True(await RandomProposeAsync(client, world, rng, contracts), "Scripted proposal failed.");
        var (id, seller, buyer) = contracts[0];
        var accept = await TestHelpers.ExecuteGraphQlAsync(client, "mutation($id: UUID!) { acceptSupplyContract(id: $id) { success } }", new { id }, world.Players[buyer].Token);
        Assert.False(accept.TryGetProperty("errors", out var acceptErrors), $"Scripted accept failed: {acceptErrors}");
        var cancel = await TestHelpers.ExecuteGraphQlAsync(client, "mutation($id: UUID!) { cancelSupplyContract(id: $id) { success } }", new { id }, world.Players[buyer].Token);
        Assert.False(cancel.TryGetProperty("errors", out var cancelErrors), $"Scripted buyer cancel failed: {cancelErrors}");
        Assert.NotEqual(seller, buyer);
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
    public async Task Checker_DetectsBuildingDoubleOwnedViaLots()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        db.Companies.AddRange(
            new Company { Id = owner, PlayerId = Guid.NewGuid(), Name = "A", FoundedAtUtc = DateTime.UtcNow },
            new Company { Id = other, PlayerId = Guid.NewGuid(), Name = "B", FoundedAtUtc = DateTime.UtcNow });
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = owner, CityId = city.Id, Type = BuildingType.Factory,
            Name = "Shared", Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.Add(building);
        // A lot claims the same building but names a different owner company.
        var lot = await db.BuildingLots.FirstAsync(l => l.CityId == city.Id);
        lot.BuildingId = building.Id;
        lot.OwnerCompanyId = other;
        await db.SaveChangesAsync();

        var violations = await EconomyInvariantChecker.CheckBuildingOwnershipAsync(db);

        Assert.Contains(violations, v => v.StartsWith("MULTI_OWNER_BUILDING"));
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

    private static async Task<bool> RandomTransferAsync(HttpClient client, World world, Random rng)
    {
        var p = world.Players[rng.Next(world.Players.Count)];
        var (from, to) = rng.Next(2) == 0 ? (p.AccountA, p.AccountB) : (p.AccountB, p.AccountA);
        var amount = Math.Round((decimal)rng.NextDouble() * 400_000m, 2) + 1m;
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: TransferFundsInput!) { transferFunds(input: $input) { __typename } }",
            new { input = new { fromBankAccountId = from, toBankAccountId = to, amount } },
            p.Token);
        return !result.TryGetProperty("errors", out _);
    }

    private static async Task<bool> RandomProposeAsync(HttpClient client, World world, Random rng, List<(Guid Id, int SellerIdx, int BuyerIdx)> contracts)
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
            return true;
        }

        return false;
    }

    private static async Task<string?> RandomContractActionAsync(HttpClient client, World world, Random rng, List<(Guid Id, int SellerIdx, int BuyerIdx)> contracts)
    {
        if (contracts.Count == 0) return null;
        var (id, s, b) = contracts[rng.Next(contracts.Count)];
        var (action, actor) = rng.Next(3) switch
        {
            0 => ("acceptSupplyContract", b),
            1 => ("rejectSupplyContract", b),
            _ => ("cancelSupplyContract", rng.Next(2) == 0 ? s : b)
        };
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            $"mutation($id: UUID!) {{ {action}(id: $id) {{ success }} }}",
            new { id },
            world.Players[actor].Token);
        return result.TryGetProperty("errors", out _) ? null : action;
    }

    /// <summary>Swaps part of a company's main balance into its second-currency account. Returns null on success, else the error code.</summary>
    private static async Task<string?> RandomForexSwapAsync(HttpClient client, World world, Random rng)
    {
        var p = world.Players[rng.Next(world.Players.Count)];
        var amount = Math.Round((decimal)rng.NextDouble() * 20_000m, 2) + 100m;
        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: ExecuteForexSwapInput!) { executeForexSwap(input: $input) { __typename } }",
            new
            {
                input = new
                {
                    fromCurrencyCode = world.CityCurrency,
                    toCurrencyCode = world.ForeignCurrency,
                    amount,
                    fromBankAccountId = p.AccountA,
                    toBankAccountId = p.AccountC,
                },
            },
            p.Token);
        return DescribeError(result);
    }

    private static string? DescribeError(JsonElement response)
    {
        if (!response.TryGetProperty("errors", out var errors))
        {
            return null;
        }

        var first = errors[0];
        var code = first.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var c) ? c.GetString() : null;
        return code ?? first.GetProperty("message").GetString() ?? "UNKNOWN";
    }

    /// <summary>Lists a building, lets another company bid and the owner accept: the company-to-company sale flow.</summary>
    private static async Task<string?> BuildingSaleAsync(HttpClient client, World world, Random rng)
    {
        var sellers = world.Players.Where(p => p.Buildings.Count > 0).ToList();
        if (sellers.Count == 0) return "NO_SELLER_WITH_A_BUILDING";
        var seller = sellers[rng.Next(sellers.Count)];
        var buyer = world.Players.Where(p => p != seller).ElementAt(rng.Next(world.Players.Count - 1));
        var buildingId = seller.Buildings[rng.Next(seller.Buildings.Count)];
        var price = 900_000m + rng.Next(0, 100) * 1_000m;

        var listed = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = price } },
            seller.Token);
        if (DescribeError(listed) is { } listError) return $"list: {listError}";

        var offer = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId = buyer.CompanyId, offeredPrice = price } },
            buyer.Token);
        if (DescribeError(offer) is { } offerError) return $"offer: {offerError}";
        var offerData = offer.GetProperty("data").GetProperty("makeOfferOnBuilding");

        var accepted = await TestHelpers.ExecuteGraphQlAsync(
            client,
            "mutation($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { __typename } }",
            new { input = new { offerId = offerData.GetProperty("id").GetString(), offerVersion = offerData.GetProperty("offerVersion").GetString() } },
            seller.Token);
        if (DescribeError(accepted) is { } acceptError) return $"accept: {acceptError}";

        // The building changed hands: only its new owner can sell it again.
        seller.Buildings.Remove(buildingId);
        buyer.Buildings.Add(buildingId);
        return null;
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
        var foreignCurrency = string.Equals(city.CurrencyCode, "USD", StringComparison.OrdinalIgnoreCase) ? "EUR" : "USD";
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
            var accountC = NewAccount(company.Id, foreignCurrency, 0m);
            db.Companies.Add(company);
            var player = await db.Players.FirstAsync(pl => pl.Id == tokens[i].PlayerId);
            player.ActiveAccountType = AccountContextType.Company;
            player.ActiveCompanyId = company.Id;
            db.Buildings.Add(building);
            db.BuildingUnits.AddRange(sales, purchase);
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = sales.Id, ResourceTypeId = resource.Id,
                Quantity = 2000m, Quality = 0.7m, SourcingCostTotal = 6000m
            });
            db.BankAccounts.AddRange(accountA, accountB, accountC);
            players.Add(new PlayerSlot(tokens[i].Token, company.Id, sales.Id, accountA.Id, accountB.Id, accountC.Id, [building.Id]));
        }

        await db.SaveChangesAsync();
        return new World(players, resource.Id, city.CurrencyCode, foreignCurrency);
    }

    private sealed record PlayerSlot(string Token, Guid CompanyId, Guid SalesUnitId, Guid AccountA, Guid AccountB, Guid AccountC, List<Guid> Buildings);

    private sealed record World(List<PlayerSlot> Players, Guid ResourceTypeId, string CityCurrency, string ForeignCurrency);
}
