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

        // Load building counts and cash for all active NPCs in two queries instead of two per NPC.
        var npcCompanyIds = activeNpcs.Select(npc => npc.CompanyId).Distinct().ToList();
        var buildingCounts = (await context.Db.Buildings
                .Where(building => npcCompanyIds.Contains(building.CompanyId) && building.DestroyedAtUtc == null)
                .Select(building => building.CompanyId)
                .ToListAsync())
            .GroupBy(companyId => companyId)
            .ToDictionary(group => group.Key, group => group.Count());
        var cashByCompany = (await context.Db.BankAccounts
                .Where(account => account.CompanyId != null && npcCompanyIds.Contains(account.CompanyId.Value) && account.ClosedAtUtc == null)
                .Select(account => new { CompanyId = account.CompanyId!.Value, account.Balance })
                .ToListAsync())
            .GroupBy(account => account.CompanyId)
            .ToDictionary(group => group.Key, group => group.Sum(account => account.Balance));

        // Shops, units, stock and products come from the tick context, and the market data for every NPC shop is one aggregate query,
        // so the cost no longer grows with the number of NPCs times the number of their shops (#646).
        var shopData = await LoadShopDataAsync(context, npcCompanyIds);

        foreach (var npc in activeNpcs)
        {
            try
            {
                await ProcessNpcAsync(
                    context,
                    npc,
                    buildingCounts.GetValueOrDefault(npc.CompanyId),
                    cashByCompany.GetValueOrDefault(npc.CompanyId),
                    shopData);
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

    private static async Task ProcessNpcAsync(TickContext context, NpcCompany npc, int buildingCount, decimal totalCash, NpcShopData shopData)
    {
        if (buildingCount < MaxBuildingsPerNpcCompany && totalCash >= ExpandCashThreshold)
        {
            await TryExpandAsync(context, npc, buildingCount);
        }

        var shops = shopData.ShopsByCompany.GetValueOrDefault(npc.CompanyId) ?? [];
        EnsureShopConfiguration(context, npc, shops, shopData.StarterProduct);
        ApplyMarketFollowingPricing(context, npc, shops, shopData.MarketByCityAndProduct);
    }

    /// <summary>An NPC sales shop with the units it has so far, including starter units created earlier in this phase.</summary>
    private sealed record NpcShop(Building Building, List<BuildingUnit> Units);

    private sealed record MarketWindow(decimal QuantitySold, decimal Revenue);

    private sealed record NpcShopData(
        Dictionary<Guid, List<NpcShop>> ShopsByCompany,
        ProductType? StarterProduct,
        Dictionary<(Guid CityId, Guid ProductTypeId), MarketWindow> MarketByCityAndProduct);

    private static async Task<NpcShopData> LoadShopDataAsync(TickContext context, List<Guid> npcCompanyIds)
    {
        var npcCompanyIdSet = npcCompanyIds.ToHashSet();
        var shopsByCompany = new Dictionary<Guid, List<NpcShop>>();
        if (context.BuildingsByType.TryGetValue(BuildingType.SalesShop, out var allShops))
        {
            foreach (var building in allShops.Where(building => npcCompanyIdSet.Contains(building.CompanyId) && building.DestroyedAtUtc == null))
            {
                var units = context.UnitsByBuilding.TryGetValue(building.Id, out var existing) ? [.. existing] : new List<BuildingUnit>();
                if (!shopsByCompany.TryGetValue(building.CompanyId, out var list))
                {
                    shopsByCompany[building.CompanyId] = list = [];
                }

                list.Add(new NpcShop(building, units));
            }
        }

        var starterProduct = await context.Db.ProductTypes
            .AsNoTracking()
            .OrderBy(product => product.Name)
            .FirstOrDefaultAsync();

        var shops = shopsByCompany.Values.SelectMany(list => list).ToList();
        var cityIds = shops.Select(shop => shop.Building.CityId).Distinct().ToList();
        var productIds = shops
            .SelectMany(shop => shop.Units)
            .Where(unit => unit.UnitType == UnitType.PublicSales && unit.ProductTypeId.HasValue)
            .Select(unit => unit.ProductTypeId!.Value)
            .Concat(starterProduct is null ? [] : [starterProduct.Id])
            .Distinct()
            .ToList();

        var market = new Dictionary<(Guid CityId, Guid ProductTypeId), MarketWindow>();
        if (cityIds.Count > 0 && productIds.Count > 0)
        {
            var fromTick = context.GameState.CurrentTick - 100;
            var windows = await context.Db.PublicSalesRecords
                .AsNoTracking()
                .Where(record => record.Tick > fromTick
                    && cityIds.Contains(record.CityId)
                    && record.ProductTypeId != null
                    && productIds.Contains(record.ProductTypeId.Value))
                .GroupBy(record => new { record.CityId, record.ProductTypeId })
                .Select(group => new { group.Key.CityId, group.Key.ProductTypeId, Quantity = group.Sum(record => record.QuantitySold), Revenue = group.Sum(record => record.Revenue) })
                .ToListAsync();
            foreach (var window in windows)
            {
                market[(window.CityId, window.ProductTypeId!.Value)] = new MarketWindow(window.Quantity, window.Revenue);
            }
        }

        return new NpcShopData(shopsByCompany, starterProduct, market);
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
        account.ConcurrencyToken = Guid.NewGuid();
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
            BankAccountId = account.Id,
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

    private static void EnsureShopConfiguration(TickContext context, NpcCompany npc, List<NpcShop> shops, ProductType? starterProduct)
    {
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
                    BuildingId = shop.Building.Id,
                    UnitType = UnitType.Purchase,
                    GridX = 0,
                    GridY = 0,
                    Level = 1,
                    LinkRight = true,
                    ProductTypeId = starterProduct.Id,
                    PurchaseSource = "OPTIMAL",
                };
                context.Db.BuildingUnits.Add(purchase);
                shop.Units.Add(purchase);
            }

            if (publicSales is null)
            {
                publicSales = new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = shop.Building.Id,
                    UnitType = UnitType.PublicSales,
                    GridX = 1,
                    GridY = 0,
                    Level = 1,
                    ProductTypeId = starterProduct.Id,
                    MinPrice = starterProduct.BasePrice,
                };
                context.Db.BuildingUnits.Add(publicSales);
                shop.Units.Add(publicSales);
            }

            if (!publicSales.ProductTypeId.HasValue)
            {
                publicSales.ProductTypeId = starterProduct.Id;
            }

            var inventory = context.InventoryByUnit.TryGetValue(publicSales.Id, out var stock)
                ? stock.FirstOrDefault(entry => entry.ProductTypeId == publicSales.ProductTypeId)
                : null;
            if (inventory is null)
            {
                context.Db.Inventories.Add(new Inventory
                {
                    Id = Guid.NewGuid(),
                    BuildingId = shop.Building.Id,
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

    private static void ApplyMarketFollowingPricing(
        TickContext context,
        NpcCompany npc,
        List<NpcShop> shops,
        Dictionary<(Guid CityId, Guid ProductTypeId), MarketWindow> marketByCityAndProduct)
    {
        foreach (var shop in shops)
        {
            foreach (var unit in shop.Units.Where(unit => unit.UnitType == UnitType.PublicSales && unit.ProductTypeId.HasValue))
            {
                var productTypeId = unit.ProductTypeId!.Value;
                marketByCityAndProduct.TryGetValue((shop.Building.CityId, productTypeId), out var window);
                context.ProductTypesById.TryGetValue(productTypeId, out var product);
                var marketAverage = window is { QuantitySold: > 0m }
                    ? window.Revenue / window.QuantitySold
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
