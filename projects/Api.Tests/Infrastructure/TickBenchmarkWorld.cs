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
                var isShop = b % 4 == 3;
                var building = new Building
                {
                    Id = DeterministicGuid(seed, "building", p * scale.BuildingsPerPlayer + b),
                    CompanyId = company.Id,
                    CityId = city.Id,
                    Type = isShop ? BuildingType.SalesShop : BuildingType.Factory,
                    Name = $"Bench {p}-{b}",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    BuiltAtUtc = DateTime.UtcNow,
                };
                db.Buildings.Add(building);

                for (var u = 0; u < scale.UnitsPerBuilding; u++)
                {
                    var resource = resources[rng.Next(resources.Count)];
                    db.BuildingUnits.Add(new BuildingUnit
                    {
                        Id = DeterministicGuid(seed, "unit", (p * scale.BuildingsPerPlayer + b) * scale.UnitsPerBuilding + u),
                        BuildingId = building.Id,
                        UnitType = UnitTypeFor(isShop, u),
                        GridX = u % 5,
                        GridY = u / 5,
                        Level = 1,
                        ResourceTypeId = isShop ? null : resource.Id,
                        ProductTypeId = isShop ? product.Id : null,
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

    private static string UnitTypeFor(bool isShop, int index)
    {
        string[] factory = [UnitType.Purchase, UnitType.Manufacturing, UnitType.Storage, UnitType.B2BSales, UnitType.Branding];
        string[] shop = [UnitType.Purchase, UnitType.PublicSales, UnitType.Marketing, UnitType.Storage];
        var set = isShop ? shop : factory;
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
