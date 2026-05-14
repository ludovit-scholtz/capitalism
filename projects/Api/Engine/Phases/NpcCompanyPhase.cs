using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;

namespace Api.Engine.Phases;

/// <summary>
/// Tick-driven autonomous NPC competitor behavior.
/// </summary>
public sealed class NpcCompanyPhase : ITickPhase
{
    private const decimal ExpandCashThreshold = 220_000m;
    private const int MaxBuildingsPerNpcCompany = 6;

    public string Name => "NpcCompany";
    public int Order => 900;

    public async Task ProcessAsync(TickContext context)
    {
        var activeNpcs = await context.Db.NpcCompanies
            .Include(npc => npc.Company)
            .ThenInclude(company => company.BankAccounts)
            .Where(npc => npc.IsActive)
            .ToListAsync();

        if (activeNpcs.Count == 0)
        {
            return;
        }

        foreach (var npc in activeNpcs)
        {
            try
            {
                await ProcessNpcAsync(context, npc);
            }
            catch (Exception ex)
            {
                context.Db.NpcDecisionLogs.Add(new NpcDecisionLog
                {
                    Id = Guid.NewGuid(),
                    NpcCompanyId = npc.Id,
                    Tick = context.GameState.CurrentTick,
                    ActionType = "ERROR",
                    Outcome = $"NPC phase exception: {ex.Message}",
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
        }
    }

    private static async Task ProcessNpcAsync(TickContext context, NpcCompany npc)
    {
        var companyId = npc.CompanyId;
        var companyBuildings = await context.Db.Buildings
            .Where(building => building.CompanyId == companyId && building.DestroyedAtUtc == null)
            .ToListAsync();

        var totalCash = await context.Db.BankAccounts
            .Where(account => account.CompanyId == companyId && account.ClosedAtUtc == null)
            .SumAsync(account => account.Balance);

        if (companyBuildings.Count < MaxBuildingsPerNpcCompany && totalCash >= ExpandCashThreshold)
        {
            await TryExpandAsync(context, npc, companyBuildings.Count);
        }

        await EnsureShopConfigurationAsync(context, npc);
        await ApplyMarketFollowingPricingAsync(context, npc);
    }

    private static async Task TryExpandAsync(TickContext context, NpcCompany npc, int currentBuildingCount)
    {
        var preferredType = SelectPreferredBuildingType(npc.Archetype, context.GameState.CurrentTick);
        var availableLots = await context.Db.BuildingLots
            .Include(lot => lot.City)
            .Where(lot => lot.CityId == npc.HomeCityId)
            .Where(lot => lot.OwnerCompanyId == null)
            .OrderBy(lot => lot.Price)
            .ToListAsync();

        var account = await CompanyBankingService.EnsurePreferredAccountAsync(context.Db, npc.CompanyId, availableLots.FirstOrDefault()?.City.CurrencyCode ?? "EUR");
        var matchingLot = availableLots.FirstOrDefault(lot =>
            lot.SuitableTypes
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(preferredType, StringComparer.OrdinalIgnoreCase)
            && (preferredType != BuildingType.Mine || lot.ResourceTypeId.HasValue)
            && lot.Price <= account.Balance);

        if (matchingLot is null)
        {
            context.Db.NpcDecisionLogs.Add(new NpcDecisionLog
            {
                Id = Guid.NewGuid(),
                NpcCompanyId = npc.Id,
                Tick = context.GameState.CurrentTick,
                ActionType = "EXPAND_SKIP",
                Outcome = $"No affordable suitable lot found for {preferredType} in home city.",
                CreatedAtUtc = DateTime.UtcNow,
            });
            return;
        }

        account.Balance -= matchingLot.Price;
        var newBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = npc.CompanyId,
            CityId = matchingLot.CityId,
            Type = preferredType,
            Name = $"{npc.Name} {preferredType.Replace('_', ' ')} #{currentBuildingCount + 1}",
            Latitude = matchingLot.Latitude,
            Longitude = matchingLot.Longitude,
            Level = 1,
            BuiltAtUtc = DateTime.UtcNow,
            PowerConsumption = preferredType == BuildingType.SalesShop ? 6m : 8m,
        };

        context.Db.Buildings.Add(newBuilding);
        await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(context.Db, newBuilding, matchingLot.City.CurrencyCode);

        matchingLot.OwnerCompanyId = npc.CompanyId;
        matchingLot.BuildingId = newBuilding.Id;
        matchingLot.ConcurrencyToken = Guid.NewGuid();

        context.Db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = npc.CompanyId,
            BuildingId = newBuilding.Id,
            Category = LedgerCategory.PropertyPurchase,
            Description = $"NPC lot purchase ({preferredType})",
            Amount = -matchingLot.Price,
            RecordedAtTick = context.GameState.CurrentTick,
            RecordedAtUtc = DateTime.UtcNow,
        });

        context.Db.NpcDecisionLogs.Add(new NpcDecisionLog
        {
            Id = Guid.NewGuid(),
            NpcCompanyId = npc.Id,
            Tick = context.GameState.CurrentTick,
            ActionType = "EXPAND",
            Outcome = $"Purchased lot {matchingLot.Name} and built {preferredType}.",
            CreatedAtUtc = DateTime.UtcNow,
        });
    }

    private static async Task EnsureShopConfigurationAsync(TickContext context, NpcCompany npc)
    {
        var shops = await context.Db.Buildings
            .Include(building => building.Units)
            .Where(building => building.CompanyId == npc.CompanyId
                && building.Type == BuildingType.SalesShop
                && building.DestroyedAtUtc == null)
            .ToListAsync();

        var starterProduct = await context.Db.ProductTypes
            .OrderBy(product => product.Name)
            .FirstOrDefaultAsync();

        if (starterProduct is null)
        {
            return;
        }

        foreach (var shop in shops)
        {
            var purchase = shop.Units.FirstOrDefault(unit => unit.UnitType == UnitType.Purchase);
            var publicSales = shop.Units.FirstOrDefault(unit => unit.UnitType == UnitType.PublicSales);

            if (purchase is null)
            {
                purchase = new BuildingUnit
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
                context.Db.BuildingUnits.Add(purchase);
            }

            if (publicSales is null)
            {
                publicSales = new BuildingUnit
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
                context.Db.BuildingUnits.Add(publicSales);
            }

            if (!publicSales.ProductTypeId.HasValue)
            {
                publicSales.ProductTypeId = starterProduct.Id;
            }

            var inventory = await context.Db.Inventories
                .FirstOrDefaultAsync(entry => entry.BuildingUnitId == publicSales.Id && entry.ProductTypeId == publicSales.ProductTypeId);
            if (inventory is null)
            {
                context.Db.Inventories.Add(new Inventory
                {
                    Id = Guid.NewGuid(),
                    BuildingId = shop.Id,
                    BuildingUnitId = publicSales.Id,
                    ProductTypeId = publicSales.ProductTypeId,
                    Quantity = 80m,
                    Quality = 0.55m,
                });
            }
            else if (inventory.Quantity < 40m)
            {
                inventory.Quantity += 40m;
            }
        }
    }

    private static async Task ApplyMarketFollowingPricingAsync(TickContext context, NpcCompany npc)
    {
        var shops = await context.Db.Buildings
            .Include(building => building.Units)
            .Where(building => building.CompanyId == npc.CompanyId
                && building.Type == BuildingType.SalesShop
                && building.DestroyedAtUtc == null)
            .ToListAsync();

        if (shops.Count == 0)
        {
            return;
        }

        foreach (var shop in shops)
        {
            foreach (var unit in shop.Units.Where(unit => unit.UnitType == UnitType.PublicSales && unit.ProductTypeId.HasValue))
            {
                var fromTick = context.GameState.CurrentTick - 100;
                var salesWindow = await context.Db.PublicSalesRecords
                    .Where(record => record.CityId == shop.CityId
                        && record.ProductTypeId == unit.ProductTypeId
                        && record.Tick > fromTick)
                    .ToListAsync();

                var product = await context.Db.ProductTypes.FirstOrDefaultAsync(item => item.Id == unit.ProductTypeId!.Value);
                var marketAverage = salesWindow.Sum(record => record.QuantitySold) > 0m
                    ? salesWindow.Sum(record => record.Revenue) / salesWindow.Sum(record => record.QuantitySold)
                    : (product?.BasePrice ?? 10m);
                var target = marketAverage * (1m + ArchetypePriceModifier(npc.Archetype));
                var clamped = decimal.Clamp(target, marketAverage * 0.8m, marketAverage * 1.2m);
                unit.MinPrice = decimal.Round(clamped, 2, MidpointRounding.AwayFromZero);

                context.Db.NpcDecisionLogs.Add(new NpcDecisionLog
                {
                    Id = Guid.NewGuid(),
                    NpcCompanyId = npc.Id,
                    Tick = context.GameState.CurrentTick,
                    ActionType = "PRICE_SET",
                    Outcome = $"Set {product?.Name ?? "product"} price to {unit.MinPrice:N2}.",
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
        }
    }

    private static string SelectPreferredBuildingType(string archetype, long currentTick)
    {
        return archetype switch
        {
            NpcArchetype.RawMaterials => BuildingType.Mine,
            NpcArchetype.Manufacturer => BuildingType.Factory,
            NpcArchetype.Retailer => BuildingType.SalesShop,
            NpcArchetype.Financier => BuildingType.Bank,
            NpcArchetype.Conglomerate => currentTick % 3 == 0 ? BuildingType.Mine : currentTick % 3 == 1 ? BuildingType.Factory : BuildingType.SalesShop,
            _ => BuildingType.Factory,
        };
    }

    private static decimal ArchetypePriceModifier(string archetype) => archetype switch
    {
        NpcArchetype.RawMaterials => -0.05m,
        NpcArchetype.Manufacturer => 0m,
        NpcArchetype.Retailer => -0.08m,
        NpcArchetype.Financier => 0.12m,
        NpcArchetype.Conglomerate => 0.03m,
        _ => 0m,
    };
}
