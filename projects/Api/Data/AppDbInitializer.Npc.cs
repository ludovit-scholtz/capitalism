using Api.Data.Entities;
using Api.Utilities;
using Capitalism.Shared.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public sealed partial class AppDbInitializer
{
    private static readonly string[] SeedNpcArchetypes =
    [
        NpcArchetype.RawMaterials,
        NpcArchetype.Manufacturer,
        NpcArchetype.Retailer,
    ];

    private async Task EnsureNpcCompaniesSeedAsync()
    {
        if (await dbContext.NpcCompanies.AnyAsync())
        {
            return;
        }

        var cities = await dbContext.Cities
            .AsNoTracking()
            .OrderBy(city => city.Name)
            .ToListAsync();
        if (cities.Count == 0)
        {
            return;
        }

        var gameTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        var starterProduct = await dbContext.ProductTypes
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .FirstOrDefaultAsync();

        foreach (var city in cities)
        {
            foreach (var archetype in SeedNpcArchetypes)
            {
                var slug = archetype.ToLowerInvariant();
                var email = $"npc-{city.Name.ToLowerInvariant().Replace(' ', '-')}-{slug}@npc.local";
                var player = await dbContext.Players.FirstOrDefaultAsync(candidate => candidate.Email == email);
                if (player is null)
                {
                    player = new Player
                    {
                        Id = Guid.NewGuid(),
                        Email = email,
                        DisplayName = $"{city.Name} {archetype.Replace('_', ' ')}",
                        Gender = PlayerGender.Unspecified,
                        Role = PlayerRole.Player,
                        ActiveAccountType = AccountContextType.Company,
                        CreatedAtUtc = DateTime.UtcNow,
                        OnboardingCompletedAtUtc = DateTime.UtcNow,
                    };
                    player.PasswordHash = new PasswordHasher<Player>().HashPassword(player, $"NpcSeed!{city.Name}!{archetype}");
                    dbContext.Players.Add(player);
                }

                var company = new Company
                {
                    Id = Guid.NewGuid(),
                    PlayerId = player.Id,
                    Name = $"{city.Name} {archetype.Replace('_', ' ')} Co.",
                    FoundedAtUtc = DateTime.UtcNow,
                    FoundedAtTick = gameTick,
                };
                dbContext.Companies.Add(company);

                var companyAccount = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(
                    dbContext,
                    company.Id,
                    city.CurrencyCode,
                    cancellationToken: CancellationToken.None);
                companyAccount.Balance += 450_000m;

                player.ActiveCompanyId = company.Id;

                var npc = new NpcCompany
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    HomeCityId = city.Id,
                    Name = company.Name,
                    Archetype = archetype,
                    DifficultyLevel = 2,
                    IsActive = true,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                dbContext.NpcCompanies.Add(npc);

                if (starterProduct is not null)
                {
                    await SeedNpcStarterShopAsync(npc, city, company, companyAccount, starterProduct, gameTick);
                }

                dbContext.NpcDecisionLogs.Add(new NpcDecisionLog
                {
                    Id = Guid.NewGuid(),
                    NpcCompanyId = npc.Id,
                    Tick = gameTick,
                    ActionType = "SEED",
                    Outcome = $"Seeded NPC company in {city.Name}.",
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
        }
    }

    private async Task SeedNpcStarterShopAsync(
        NpcCompany npc,
        City city,
        Company company,
        BankAccount companyAccount,
        ProductType starterProduct,
        long gameTick)
    {
        var salesShopType = BuildingType.SalesShop;
        var lot = await dbContext.BuildingLots
            .Where(candidate => candidate.CityId == city.Id && candidate.OwnerCompanyId == null)
            .Where(candidate =>
                candidate.SuitableTypes == salesShopType
                || candidate.SuitableTypes.StartsWith($"{salesShopType},")
                || candidate.SuitableTypes.EndsWith($",{salesShopType}")
                || candidate.SuitableTypes.Contains($",{salesShopType},"))
            .OrderBy(candidate => candidate.Price)
            .FirstOrDefaultAsync();
        if (lot is null || companyAccount.Balance < lot.Price)
        {
            return;
        }

        companyAccount.Balance -= lot.Price;
        var shop = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.SalesShop,
            Name = $"{company.Name} Shop",
            Latitude = lot.Latitude,
            Longitude = lot.Longitude,
            PowerConsumption = 6m,
            BuiltAtUtc = DateTime.UtcNow,
            Level = 1,
        };
        dbContext.Buildings.Add(shop);
        await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(dbContext, shop, city.CurrencyCode);

        lot.OwnerCompanyId = company.Id;
        lot.BuildingId = shop.Id;
        lot.ConcurrencyToken = Guid.NewGuid();

        var purchaseUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = shop.Id,
            UnitType = UnitType.Purchase,
            GridX = 0,
            GridY = 0,
            Level = 1,
            LinkRight = true,
            ProductTypeId = starterProduct.Id,
            PurchaseSource = "OPTIMAL",
        };
        var salesUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = shop.Id,
            UnitType = UnitType.PublicSales,
            GridX = 1,
            GridY = 0,
            Level = 1,
            ProductTypeId = starterProduct.Id,
            MinPrice = starterProduct.BasePrice,
        };
        dbContext.BuildingUnits.AddRange(purchaseUnit, salesUnit);
        dbContext.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = shop.Id,
            BuildingUnitId = salesUnit.Id,
            ProductTypeId = starterProduct.Id,
            Quantity = 120m,
            Quality = 0.6m,
        });

        dbContext.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = shop.Id,
            BankAccountId = companyAccount.Id,
            Category = LedgerCategory.PropertyPurchase,
            Description = $"NPC seeded starter shop on lot {lot.Name}",
            Amount = -lot.Price,
            RecordedAtTick = gameTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        dbContext.NpcDecisionLogs.Add(new NpcDecisionLog
        {
            Id = Guid.NewGuid(),
            NpcCompanyId = npc.Id,
            Tick = gameTick,
            ActionType = "SEED_SHOP",
            Outcome = $"Created starter shop {shop.Name}.",
            CreatedAtUtc = DateTime.UtcNow,
        });
    }
}
