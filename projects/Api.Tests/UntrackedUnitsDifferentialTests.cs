using System.Reflection;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Loading the 50,000 (or 500,000) building units tracked costs most of the context load and a share of change detection (#646). The
/// tick can load them untracked and persist only the units that changed; this runs the same world through both modes and requires
/// the persisted result to be identical, including units changed by a due configuration plan, by the NPC phase and by the
/// low-inventory alert.
/// </summary>
public sealed class UntrackedUnitsDifferentialTests
{
    private const int Seed = 77;
    private const int Ticks = 3;
    private static readonly TickBenchmarkScale Scale = new(Players: 4, BuildingsPerPlayer: 20, UnitsPerBuilding: 8);

    // Random phases cannot be compared between two runs.
    private static readonly HashSet<string> NondeterministicPhases = ["GlobalEvents", "ResourceReplenishment"];

    private static Guid Fixed(string kind, int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(Seed).CopyTo(bytes, 0);
        BitConverter.GetBytes(index).CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes(kind.PadRight(8)[..8]).CopyTo(bytes, 8);
        return new Guid(bytes);
    }

    /// <summary>The world generator and this test derive ids from the seed; ids the application generates itself differ between runs.</summary>
    private static bool IsDeterministic(Guid id) => BitConverter.ToInt32(id.ToByteArray(), 0) == Seed;

    private static string Key(Guid? id) => id is { } value && IsDeterministic(value) ? value.ToString() : "?";

    private static async Task<ApiWebApplicationFactory> BuildWorldAsync()
    {
        var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient(); // boots the host and seeds the reference data
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await TickBenchmarkWorld.SeedAsync(db, Scale, Seed);

        var factory0 = await db.Buildings.AsNoTracking().Where(b => b.Type == BuildingType.Factory).OrderBy(b => b.Id).FirstAsync();
        var shop = await db.Buildings.AsNoTracking().Where(b => b.Type == BuildingType.SalesShop).OrderBy(b => b.Id).FirstAsync();
        var factoryUnits = await db.BuildingUnits.AsNoTracking().Where(u => u.BuildingId == factory0.Id).OrderBy(u => u.GridY).ThenBy(u => u.GridX).ToListAsync();

        // 1. A due configuration plan: one unit upgraded, one removed, one new.
        var plan = new BuildingConfigurationPlan { Id = Fixed("plan", 1), BuildingId = factory0.Id, SubmittedAtTick = 0, AppliesAtTick = 0, TotalTicksRequired = 0 };
        var upgraded = factoryUnits[0];
        var removed = factoryUnits[1];
        db.BuildingConfigurationPlans.Add(plan);
        db.BuildingConfigurationPlanUnits.Add(new BuildingConfigurationPlanUnit
        {
            Id = Fixed("planunit", 1), BuildingConfigurationPlanId = plan.Id, UnitType = upgraded.UnitType, GridX = upgraded.GridX, GridY = upgraded.GridY, Level = 2,
            ResourceTypeId = upgraded.ResourceTypeId, ProductTypeId = upgraded.ProductTypeId, IsChanged = true, AppliesAtTick = 0, TicksRequired = 0,
        });
        db.BuildingConfigurationPlanUnits.Add(new BuildingConfigurationPlanUnit
        {
            Id = Fixed("planunit", 2), BuildingConfigurationPlanId = plan.Id, UnitType = UnitType.Storage, GridX = 0, GridY = 3, Level = 1,
            IsChanged = true, AppliesAtTick = 0, TicksRequired = 0,
        });
        db.BuildingConfigurationPlanRemovals.Add(new BuildingConfigurationPlanRemoval
        {
            Id = Fixed("planrem", 1), BuildingConfigurationPlanId = plan.Id, GridX = removed.GridX, GridY = removed.GridY, AppliesAtTick = 0, TicksRequired = 0,
        });

        // 2. A sales unit below its low-inventory alert threshold.
        var salesUnit = await db.BuildingUnits.Where(u => u.BuildingId == shop.Id && u.UnitType == UnitType.PublicSales).OrderBy(u => u.Id).FirstAsync();
        salesUnit.LowInventoryAlertThreshold = 10m;
        db.Inventories.Add(new Inventory
        {
            Id = Fixed("inv", 1), BuildingId = shop.Id, BuildingUnitId = salesUnit.Id, ProductTypeId = salesUnit.ProductTypeId, Quantity = 3m, Quality = 0.5m,
        });

        // 3. An NPC retailer whose shops have no units yet (the NPC phase creates them and sets prices).
        var city = await db.Cities.AsNoTracking().OrderBy(c => c.Name).FirstAsync();
        var npcPlayer = new Player { Id = Fixed("npcplyr", 1), Email = "npc-differential@test.local", DisplayName = "NPC", PasswordHash = "x", Role = PlayerRole.Player, OnboardingCompletedAtUtc = DateTime.UtcNow };
        var npcCompany = new Company { Id = Fixed("npccomp", 1), PlayerId = npcPlayer.Id, Name = "NPC Differential", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        db.Players.Add(npcPlayer);
        db.Companies.Add(npcCompany);
        db.BankAccounts.Add(new BankAccount { Id = Fixed("npcacct", 1), CompanyId = npcCompany.Id, CurrencyCode = city.CurrencyCode, Balance = 1_000m, AccountNumber = "9000000000000001", CreatedAtUtc = DateTime.UtcNow });
        db.NpcCompanies.Add(new NpcCompany { Id = Fixed("npc", 1), CompanyId = npcCompany.Id, HomeCityId = city.Id, Name = npcCompany.Name, Archetype = NpcArchetype.Retailer, IsActive = true, CreatedAtUtc = DateTime.UtcNow });
        for (var i = 0; i < 2; i++)
        {
            db.Buildings.Add(new Building
            {
                Id = Fixed("npcshop", i), CompanyId = npcCompany.Id, CityId = city.Id, Type = BuildingType.SalesShop, Name = $"NPC Shop {i}", Latitude = city.Latitude,
                Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, Level = 1,
            });
        }

        // The NPC companies the application seeds pick lots at random; only the deterministic one above takes part.
        foreach (var other in await db.NpcCompanies.Where(npc => npc.Id != Fixed("npc", 1)).ToListAsync())
        {
            other.IsActive = false;
        }

        await db.SaveChangesAsync();
        return factory;
    }

    private static async Task RunTicksAsync(ApiWebApplicationFactory factory, bool trackBuildingUnits)
    {
        for (var i = 0; i < Ticks; i++)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = scope.ServiceProvider.GetServices<ITickPhase>().Where(phase => !NondeterministicPhases.Contains(phase.Name)).ToList();
            var processor = new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance, trackBuildingUnits: trackBuildingUnits);
            _ = await processor.ProcessTickAsync();
        }
    }

    /// <summary>All scalar values of an entity except timestamps and navigation properties, as text, for comparing two runs.</summary>
    private static Dictionary<string, string> Scalars(object entity)
        => entity.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && IsScalar(property.PropertyType) && property.Name != "ConcurrencyToken") // a fresh random value per write
            .Where(property => property.PropertyType != typeof(DateTime) && property.PropertyType != typeof(DateTime?)
                && property.PropertyType != typeof(DateTimeOffset) && property.PropertyType != typeof(DateTimeOffset?))
            .ToDictionary(property => property.Name, property => Convert.ToString(property.GetValue(entity), System.Globalization.CultureInfo.InvariantCulture) ?? "∅");

    private static bool IsScalar(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        return type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal) || type == typeof(Guid);
    }

    private sealed record Outcome(
        SortedDictionary<Guid, Dictionary<string, string>> SeededUnits,
        List<string> NewUnits,
        SortedDictionary<string, decimal> StockByUnit,
        List<string> Ledger,
        SortedDictionary<Guid, decimal> Balances,
        SortedDictionary<Guid, Dictionary<string, string>> Buildings,
        List<string> Notifications,
        List<string> PlanState);

    private static async Task<Outcome> ReadAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var units = await db.BuildingUnits.AsNoTracking().ToListAsync();
        var ledger = (await db.LedgerEntries.AsNoTracking().Where(e => e.RecordedAtTick >= 1).ToListAsync()) // tick 0 rows come from the application's random start-up seeding
            .Select(e => $"{Key(e.CompanyId)}|{e.Category}|{decimal.Round(e.Amount, 4)}|{e.RecordedAtTick}").Order().ToList();
        var stock = (await db.Inventories.AsNoTracking().Where(i => i.BuildingUnitId != null).ToListAsync())
            .GroupBy(i => Key(i.BuildingUnitId))
            .ToDictionary(g => g.Key, g => decimal.Round(g.Sum(i => i.Quantity), 4));

        return new Outcome(
            new SortedDictionary<Guid, Dictionary<string, string>>(units.Where(u => IsDeterministic(u.Id)).ToDictionary(u => u.Id, u => Scalars(u))),
            units.Where(u => !IsDeterministic(u.Id)).Select(u => string.Join(",", Scalars(u).Where(kv => kv.Key is not ("Id" or "BuildingId")).OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"))).Order().ToList(),
            new SortedDictionary<string, decimal>(stock),
            ledger,
            new SortedDictionary<Guid, decimal>((await db.BankAccounts.AsNoTracking().ToListAsync()).Where(a => IsDeterministic(a.Id)).ToDictionary(a => a.Id, a => decimal.Round(a.Balance, 4))),
            new SortedDictionary<Guid, Dictionary<string, string>>((await db.Buildings.AsNoTracking().ToListAsync()).Where(b => IsDeterministic(b.Id)).ToDictionary(b => b.Id, b => Scalars(b))),
            (await db.PlayerNotifications.AsNoTracking().ToListAsync()).Select(n => $"{Key(n.PlayerId)}|{n.Type}|{Key(n.BuildingId)}|{n.CreatedAtTick}").Order().ToList(),
            (await db.BuildingConfigurationPlans.AsNoTracking().Include(p => p.Units).Include(p => p.Removals).ToListAsync())
                .Select(p => $"{Key(p.Id)}|{p.Units.Count}|{p.Removals.Count}").Order().ToList());
    }

    private static void AssertSame(Outcome expected, Outcome actual)
    {
        Assert.Equal(expected.SeededUnits.Keys, actual.SeededUnits.Keys);
        foreach (var (id, scalars) in expected.SeededUnits)
        {
            Assert.True(scalars.SequenceEqual(actual.SeededUnits[id]), $"Unit {id} differs:\n{string.Join("\n", scalars.Where(kv => actual.SeededUnits[id][kv.Key] != kv.Value).Select(kv => $"  {kv.Key}: {kv.Value} vs {actual.SeededUnits[id][kv.Key]}"))}");
        }

        Assert.Equal(expected.NewUnits, actual.NewUnits);
        Assert.Equal(expected.StockByUnit, actual.StockByUnit);
        Assert.Equal(expected.Ledger, actual.Ledger);
        Assert.Equal(expected.Balances, actual.Balances);
        Assert.Equal(expected.Notifications, actual.Notifications);
        Assert.Equal(expected.PlanState, actual.PlanState);
        foreach (var (id, scalars) in expected.Buildings)
        {
            Assert.True(scalars.SequenceEqual(actual.Buildings[id]), $"Building {id} differs:\n{string.Join("\n", scalars.Where(kv => actual.Buildings[id][kv.Key] != kv.Value).Select(kv => $"  {kv.Key}: {kv.Value} vs {actual.Buildings[id][kv.Key]}"))}");
        }
    }

    [Fact]
    public async Task TheHarnessItself_IsDeterministic_TrackedRunsAgree()
    {
        await using var first = await BuildWorldAsync();
        await using var second = await BuildWorldAsync();
        await RunTicksAsync(first, trackBuildingUnits: true);
        await RunTicksAsync(second, trackBuildingUnits: true);

        AssertSame(await ReadAsync(first), await ReadAsync(second));
    }

    [Fact]
    public async Task UntrackedUnitLoading_PersistsExactlyWhatTrackedLoadingPersists()
    {
        await using var tracked = await BuildWorldAsync();
        await using var untracked = await BuildWorldAsync();
        await RunTicksAsync(tracked, trackBuildingUnits: true);
        await RunTicksAsync(untracked, trackBuildingUnits: false);

        var expected = await ReadAsync(tracked);
        var actual = await ReadAsync(untracked);

        // Sanity: the scenarios really happened, otherwise equality would prove nothing.
        Assert.Contains(expected.SeededUnits.Values, unit => unit["IsLowInventoryAlertActive"] == "True");
        Assert.Contains(expected.SeededUnits.Values, unit => unit["Level"] == "2");
        Assert.NotEmpty(expected.NewUnits);
        Assert.Contains(expected.Notifications, n => n.Contains(PlayerNotificationType.BuildingUpgradeApplied));
        AssertSame(expected, actual);
    }
}
