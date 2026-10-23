using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

/// <summary>Size of a generated benchmark world.</summary>
internal sealed record TickBenchmarkScale(int Players, int BuildingsPerPlayer, int UnitsPerBuilding)
{
    public int Buildings => Players * BuildingsPerPlayer;

    public int Units => Buildings * UnitsPerBuilding;

    /// <summary>The product requirement: 1,000 users, 20,000 buildings, 500,000 units.</summary>
    public static TickBenchmarkScale Full { get; } = new(1_000, 20, 25);

    /// <summary>A tenth of <see cref="Full"/> (2,000 buildings): large enough to expose quadratic behaviour, still seedable in seconds.</summary>
    public static TickBenchmarkScale Medium { get; } = new(100, 20, 25);

    /// <summary>A 1/50 slice of <see cref="Full"/> that is cheap enough to run on every CI build.</summary>
    public static TickBenchmarkScale Ci { get; } = new(20, 20, 25);
}

/// <summary>
/// Deterministic generator for tick performance benchmarks. The same seed and scale always produce the same world
/// (ids included), so timings are comparable between runs and machines.
/// </summary>
internal static class TickBenchmarkWorld
{
    private const int SaveBatchBuildings = 500;

    public static async Task SeedAsync(AppDbContext db, TickBenchmarkScale scale, int seed)
    {
        var rng = new Random(seed);
        var cities = await db.Cities.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
        var resources = await db.ResourceTypes.AsNoTracking().OrderBy(r => r.Name).Take(8).ToListAsync();
        var product = await db.ProductTypes.AsNoTracking().OrderBy(p => p.Name).FirstAsync();
        db.ChangeTracker.AutoDetectChangesEnabled = false;

        var pending = 0;
        for (var p = 0; p < scale.Players; p++)
        {
            var city = cities[p % cities.Count];
            var player = new Player
            {
                Id = DeterministicGuid(seed, "player", p),
                Email = $"bench-{seed}-{p}@example.com",
                DisplayName = $"Bench {p}",
                PasswordHash = "x",
                OnboardingCompletedAtUtc = DateTime.UtcNow,
            };
            var company = new Company
            {
                Id = DeterministicGuid(seed, "company", p),
                PlayerId = player.Id,
                Name = $"Bench Co {p}",
                FoundedAtUtc = DateTime.UtcNow,
            };
            db.Players.Add(player);
            db.Companies.Add(company);
            db.BankAccounts.Add(new BankAccount
            {
                Id = DeterministicGuid(seed, "account", p),
                CompanyId = company.Id,
                CurrencyCode = city.CurrencyCode,
                Balance = 5_000_000m,
                AccountNumber = (1_000_000_000_000_000L + p).ToString(),
                CreatedAtUtc = DateTime.UtcNow,
            });

            for (var b = 0; b < scale.BuildingsPerPlayer; b++)
            {
                // Every kind of building that has a phase of its own is represented, because a phase the world never exercises cannot show
                // up in the benchmark (the rent phase once cost 29 s per tick unnoticed): per player a mine, a solar plant and an R&D lab,
                // an apartment block in every ten buildings, shops in every fourth, factories for the rest.
                var kind = b switch
                {
                    0 => BenchKind.Mine,
                    1 => BenchKind.PowerPlant,
                    2 => BenchKind.Research,
                    _ when b % 10 == 9 => BenchKind.Apartment,
                    _ when b % 4 == 3 => BenchKind.Shop,
                    _ => BenchKind.Factory,
                };
                var isShop = kind == BenchKind.Shop;
                var isApartment = kind == BenchKind.Apartment;
                var building = new Building
                {
                    Id = DeterministicGuid(seed, "building", p * scale.BuildingsPerPlayer + b),
                    CompanyId = company.Id,
                    CityId = city.Id,
                    Type = BuildingTypeOf(kind),
                    Name = $"Bench {p}-{b}",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    BuiltAtUtc = DateTime.UtcNow,
                    PricePerSqm = isApartment ? city.AverageRentPerSqm : null,
                    TotalAreaSqm = isApartment ? 400m : null,
                    OccupancyPercent = isApartment ? 70m : null,
                    PowerPlantType = kind == BenchKind.PowerPlant ? PowerPlantType.Solar : null,
                    PowerOutput = kind == BenchKind.PowerPlant ? 5m : null,
                };
                db.Buildings.Add(building);

                var mineResource = resources[rng.Next(resources.Count)];
                if (kind == BenchKind.Mine)
                {
                    db.BuildingLots.Add(new BuildingLot
                    {
                        Id = DeterministicGuid(seed, "lot", p * scale.BuildingsPerPlayer + b),
                        CityId = city.Id,
                        Name = $"Bench lot {p}-{b}",
                        Description = "Benchmark mine lot",
                        District = "Industrial",
                        Latitude = city.Latitude,
                        Longitude = city.Longitude,
                        PopulationIndex = 1m,
                        BasePrice = 1_000m,
                        Price = 1_000m,
                        SuitableTypes = BuildingType.Mine,
                        OwnerCompanyId = company.Id,
                        BuildingId = building.Id,
                        ResourceTypeId = mineResource.Id,
                        MaterialQuality = 0.7m,
                        MaterialQuantity = 1_000_000m,
                        OriginalMaterialQuantity = 1_000_000m,
                    });
                }

                for (var u = 0; u < UnitCountOf(kind, scale.UnitsPerBuilding); u++)
                {
                    var resource = kind == BenchKind.Mine ? mineResource : resources[rng.Next(resources.Count)];
                    db.BuildingUnits.Add(new BuildingUnit
                    {
                        Id = DeterministicGuid(seed, "unit", (p * scale.BuildingsPerPlayer + b) * scale.UnitsPerBuilding + u),
                        BuildingId = building.Id,
                        UnitType = UnitTypeFor(kind, u),
                        GridX = u % 5,
                        GridY = u / 5,
                        Level = 1,
                        ResourceTypeId = isShop || kind == BenchKind.Research ? null : resource.Id,
                        ProductTypeId = isShop || kind == BenchKind.Research ? product.Id : null,
                    });
                }

                if (++pending >= SaveBatchBuildings)
                {
                    await FlushAsync(db);
                    pending = 0;
                }
            }
        }

        await FlushAsync(db);
        db.ChangeTracker.AutoDetectChangesEnabled = true;
    }

    private static async Task FlushAsync(AppDbContext db)
    {
        db.ChangeTracker.DetectChanges();
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }

    private enum BenchKind { Factory, Shop, Apartment, Mine, PowerPlant, Research }

    private static string BuildingTypeOf(BenchKind kind) => kind switch
    {
        BenchKind.Shop => BuildingType.SalesShop,
        BenchKind.Apartment => BuildingType.Apartment,
        BenchKind.Mine => BuildingType.Mine,
        BenchKind.PowerPlant => BuildingType.PowerPlant,
        BenchKind.Research => BuildingType.ResearchDevelopment,
        _ => BuildingType.Factory,
    };

    /// <summary>Apartment blocks and power plants carry no grid units.</summary>
    private static int UnitCountOf(BenchKind kind, int unitsPerBuilding)
        => kind is BenchKind.Apartment or BenchKind.PowerPlant ? 0 : unitsPerBuilding;

    private static string UnitTypeFor(BenchKind kind, int index)
    {
        string[] factory = [UnitType.Purchase, UnitType.Manufacturing, UnitType.Storage, UnitType.B2BSales, UnitType.Branding];
        string[] shop = [UnitType.Purchase, UnitType.PublicSales, UnitType.Marketing, UnitType.Storage];
        string[] mine = [UnitType.Mining, UnitType.Storage, UnitType.Mining, UnitType.B2BSales];
        string[] research = [UnitType.ProductQuality, UnitType.BrandQuality, UnitType.Storage];
        var set = kind switch
        {
            BenchKind.Shop => shop,
            BenchKind.Mine => mine,
            BenchKind.Research => research,
            _ => factory,
        };
        return set[index % set.Length];
    }

    private static Guid DeterministicGuid(int seed, string kind, int index)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(seed).CopyTo(bytes, 0);
        BitConverter.GetBytes(index).CopyTo(bytes, 4);
        System.Text.Encoding.ASCII.GetBytes(kind.PadRight(8)[..8]).CopyTo(bytes, 8);
        return new Guid(bytes);
    }
}
