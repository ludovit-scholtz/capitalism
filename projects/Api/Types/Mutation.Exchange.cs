using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// GraphQL mutations for the global commodity exchange:
/// buying resources from city exchange offers and selling resources back to the exchange.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Purchases resources from the global exchange for a given source city.
    /// Debits the company bank account by (exchange price + transit cost) × quantity,
    /// credits the target building unit's inventory with the purchased resources,
    /// and records two ledger entries (purchasing cost + shipping cost).
    /// </summary>
    [Authorize]
    public async Task<BuyFromExchangeResult> BuyFromExchange(
        BuyFromExchangeInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (input.Quantity <= 0m)
        {
            return BuyFromExchangeResult.Fail("Quantity must be positive.", "INVALID_QUANTITY");
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // ── Load resource type ────────────────────────────────────────────────────
        var resource = await db.ResourceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == input.ResourceTypeId);
        if (resource is null)
        {
            return BuyFromExchangeResult.Fail("Resource type not found.", "RESOURCE_NOT_FOUND");
        }

        // ── Load source city ──────────────────────────────────────────────────────
        var sourceCity = await db.Cities
            .AsNoTracking()
            .Include(c => c.Resources)
            .FirstOrDefaultAsync(c => c.Id == input.SourceCityId);
        if (sourceCity is null)
        {
            return BuyFromExchangeResult.Fail("Source city not found.", "CITY_NOT_FOUND");
        }

        // ── Load target building unit (must belong to player's company) ───────────
        var targetUnit = await db.BuildingUnits
            .Include(u => u.Building)
                .ThenInclude(b => b.City)
            .Include(u => u.Building)
                .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(u => u.Id == input.TargetBuildingUnitId);

        if (targetUnit is null)
        {
            return BuyFromExchangeResult.Fail("Target building unit not found.", "UNIT_NOT_FOUND");
        }

        if (targetUnit.Building.Company.PlayerId != userId)
        {
            return BuyFromExchangeResult.Fail("You do not own this building.", "ACCESS_DENIED");
        }

        // Only STORAGE and PURCHASE units can receive resource deliveries
        if (targetUnit.UnitType != UnitType.Storage && targetUnit.UnitType != UnitType.Purchase)
        {
            return BuyFromExchangeResult.Fail(
                "Only STORAGE or PURCHASE units can receive exchange deliveries.",
                "INVALID_UNIT_TYPE");
        }

        // ── Verify capacity ───────────────────────────────────────────────────────
        var existingInventory = await db.Inventories
            .FirstOrDefaultAsync(i => i.BuildingUnitId == targetUnit.Id && i.ResourceTypeId == input.ResourceTypeId);

        var currentQty = existingInventory?.Quantity ?? 0m;
        var unitCapacity = targetUnit.UnitType == UnitType.Storage
            ? GameConstants.StorageUnitHoldingCapacity(targetUnit.Level)
            : GameConstants.StorageCapacity(targetUnit.Level);

        if (currentQty + input.Quantity > unitCapacity)
        {
            return BuyFromExchangeResult.Fail(
                $"Insufficient unit capacity. Available: {unitCapacity - currentQty:F2}, requested: {input.Quantity:F2}.",
                "INSUFFICIENT_UNIT_CAPACITY");
        }

        // ── Load bank account (must belong to same company) ───────────────────────
        var bankAccount = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == input.BankAccountId && a.ClosedAtUtc == null);

        if (bankAccount is null)
        {
            return BuyFromExchangeResult.Fail("Bank account not found.", "BANK_ACCOUNT_NOT_FOUND");
        }

        if (bankAccount.CompanyId != targetUnit.Building.CompanyId)
        {
            return BuyFromExchangeResult.Fail("Bank account does not belong to this company.", "BANK_ACCOUNT_MISMATCH");
        }

        // ── Compute prices in bank account currency ───────────────────────────────
        var destinationCity = targetUnit.Building.City;
        var bankCurrency = bankAccount.CurrencyCode;

        // FX rate: 1 EUR expressed in bank account currency
        var fxRateEurToBank = await Query.ComputeForexRateAsync(db, "EUR", bankCurrency);
        // FX rate: 1 source currency expressed in bank account currency
        var fxRateSrcToBank = await Query.ComputeForexRateAsync(db, sourceCity.CurrencyCode, bankCurrency);

        var abundanceFocus = sourceCity.Resources
            .FirstOrDefault(r => r.ResourceTypeId == resource.Id)?.Abundance
            ?? GlobalExchangeCalculator.DefaultMissingAbundance;

        // Exchange price is EUR-based internally, convert to bank currency
        var exchangePriceEur = GlobalExchangeCalculator.ComputeExchangePrice(sourceCity, resource, abundanceFocus);
        var exchangePrice = decimal.Round(exchangePriceEur * fxRateEurToBank, 2, MidpointRounding.AwayFromZero);

        var transitCost = GlobalExchangeCalculator.ComputeTransitCostPerUnit(
            sourceCity, destinationCity, resource, fxRateEurToBank, destinationCity.FuelPriceIndex);

        var deliveredPrice = exchangePrice + transitCost;
        var totalCost = decimal.Round(deliveredPrice * input.Quantity, 2, MidpointRounding.AwayFromZero);

        // ── Check sufficient balance ──────────────────────────────────────────────
        if (bankAccount.Balance < totalCost)
        {
            return BuyFromExchangeResult.Fail(
                $"Insufficient funds. Required: {totalCost:F2} {bankCurrency}, available: {bankAccount.Balance:F2} {bankCurrency}.",
                "INSUFFICIENT_FUNDS");
        }

        // ── Compute quality for this purchase ─────────────────────────────────────
        var currentTick = await GetCurrentTickAsync(db);
        var quality = GlobalExchangeCalculator.SampleExchangeQuality(abundanceFocus, currentTick, sourceCity.Id, resource.Id);

        // ── Debit bank account ────────────────────────────────────────────────────
        bankAccount.Balance -= totalCost;

        // ── Update inventory ──────────────────────────────────────────────────────
        var totalCostInv = decimal.Round(exchangePrice * input.Quantity, 2, MidpointRounding.AwayFromZero);
        if (existingInventory is null)
        {
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                BuildingId = targetUnit.BuildingId,
                BuildingUnitId = targetUnit.Id,
                ResourceTypeId = resource.Id,
                Quantity = input.Quantity,
                Quality = quality,
                SourcingCostTotal = totalCostInv,
            });
        }
        else
        {
            // Weighted-average quality
            var combinedQty = existingInventory.Quantity + input.Quantity;
            existingInventory.Quality = decimal.Round(
                ((existingInventory.Quality * existingInventory.Quantity) + (quality * input.Quantity)) / combinedQty,
                4,
                MidpointRounding.AwayFromZero);
            existingInventory.Quantity = combinedQty;
            existingInventory.SourcingCostTotal += totalCostInv;
        }

        // ── Record ledger entries ─────────────────────────────────────────────────
        var company = targetUnit.Building.Company;

        // Purchasing cost (resource cost only)
        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = targetUnit.BuildingId,
            BankAccountId = bankAccount.Id,
            Category = LedgerCategory.PurchasingCost,
            Description = $"Exchange purchase: {input.Quantity:F2}× {resource.Name} from {sourceCity.Name}",
            Amount = -decimal.Round(exchangePrice * input.Quantity, 2, MidpointRounding.AwayFromZero),
            RecordedAtTick = currentTick,
            ResourceTypeId = resource.Id,
        });

        // Shipping cost (transit cost)
        if (transitCost > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = targetUnit.BuildingId,
                BankAccountId = bankAccount.Id,
                Category = LedgerCategory.ShippingCost,
                Description = $"Transit cost: {resource.Name} from {sourceCity.Name} to {destinationCity.Name}",
                Amount = -decimal.Round(transitCost * input.Quantity, 2, MidpointRounding.AwayFromZero),
                RecordedAtTick = currentTick,
                ResourceTypeId = resource.Id,
            });
        }

        await db.SaveChangesAsync();

        return new BuyFromExchangeResult
        {
            Success = true,
            ResourceName = resource.Name,
            QuantityPurchased = input.Quantity,
            ExchangePricePerUnit = exchangePrice,
            TransitCostPerUnit = transitCost,
            DeliveredPricePerUnit = deliveredPrice,
            TotalCost = totalCost,
            QualityDelivered = quality,
            CurrencyCode = bankCurrency,
            NewBankBalance = bankAccount.Balance,
        };
    }

    /// <summary>
    /// Sells resources from a building unit's inventory to the global exchange.
    /// Credits the company bank account by (exchange price - transit cost) × quantity,
    /// removes the inventory, and records a revenue ledger entry.
    /// </summary>
    [Authorize]
    public async Task<SellToExchangeResult> SellToExchange(
        SellToExchangeInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        if (input.Quantity <= 0m)
        {
            return SellToExchangeResult.Fail("Quantity must be positive.", "INVALID_QUANTITY");
        }

        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // ── Load resource type ────────────────────────────────────────────────────
        var resource = await db.ResourceTypes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == input.ResourceTypeId);
        if (resource is null)
        {
            return SellToExchangeResult.Fail("Resource type not found.", "RESOURCE_NOT_FOUND");
        }

        // ── Load source building unit (must belong to player's company) ───────────
        var sourceUnit = await db.BuildingUnits
            .Include(u => u.Building)
                .ThenInclude(b => b.City)
            .Include(u => u.Building)
                .ThenInclude(b => b.Company)
            .FirstOrDefaultAsync(u => u.Id == input.SourceBuildingUnitId);

        if (sourceUnit is null)
        {
            return SellToExchangeResult.Fail("Source building unit not found.", "UNIT_NOT_FOUND");
        }

        if (sourceUnit.Building.Company.PlayerId != userId)
        {
            return SellToExchangeResult.Fail("You do not own this building.", "ACCESS_DENIED");
        }

        // ── Check inventory ───────────────────────────────────────────────────────
        var inventory = await db.Inventories
            .FirstOrDefaultAsync(i => i.BuildingUnitId == sourceUnit.Id && i.ResourceTypeId == input.ResourceTypeId);

        if (inventory is null || inventory.Quantity <= 0m)
        {
            return SellToExchangeResult.Fail("No inventory of this resource in the source unit.", "INSUFFICIENT_INVENTORY");
        }

        var quantityToSell = Math.Min(input.Quantity, inventory.Quantity);
        if (quantityToSell < input.Quantity)
        {
            return SellToExchangeResult.Fail(
                $"Insufficient inventory. Available: {inventory.Quantity:F2}, requested: {input.Quantity:F2}.",
                "INSUFFICIENT_INVENTORY");
        }

        // ── Load bank account (must belong to same company) ───────────────────────
        var bankAccount = await db.BankAccounts
            .FirstOrDefaultAsync(a => a.Id == input.BankAccountId && a.ClosedAtUtc == null);

        if (bankAccount is null)
        {
            return SellToExchangeResult.Fail("Bank account not found.", "BANK_ACCOUNT_NOT_FOUND");
        }

        if (bankAccount.CompanyId != sourceUnit.Building.CompanyId)
        {
            return SellToExchangeResult.Fail("Bank account does not belong to this company.", "BANK_ACCOUNT_MISMATCH");
        }

        // ── Compute prices in bank account currency ───────────────────────────────
        var sourceCity = sourceUnit.Building.City;
        var bankCurrency = bankAccount.CurrencyCode;

        var fxRateEurToBank = await Query.ComputeForexRateAsync(db, "EUR", bankCurrency);

        // For sell: the exchange buys at the local city price (no transit — selling to local exchange)
        var abundanceFocus = await db.CityResources
            .AsNoTracking()
            .Where(cr => cr.CityId == sourceCity.Id && cr.ResourceTypeId == resource.Id)
            .Select(cr => (decimal?)cr.Abundance)
            .FirstOrDefaultAsync()
            ?? GlobalExchangeCalculator.DefaultMissingAbundance;

        var exchangePriceEur = GlobalExchangeCalculator.ComputeExchangePrice(sourceCity, resource, abundanceFocus);
        var exchangePrice = decimal.Round(exchangePriceEur * fxRateEurToBank, 2, MidpointRounding.AwayFromZero);

        // Selling to local exchange: no transit cost since goods are already in the city
        var totalProceeds = decimal.Round(exchangePrice * input.Quantity, 2, MidpointRounding.AwayFromZero);

        // ── Reduce inventory ──────────────────────────────────────────────────────
        var costFraction = inventory.SourcingCostTotal > 0m && inventory.Quantity > 0m
            ? inventory.SourcingCostTotal * (input.Quantity / inventory.Quantity)
            : 0m;

        inventory.Quantity -= input.Quantity;
        inventory.SourcingCostTotal = Math.Max(0m, inventory.SourcingCostTotal - costFraction);

        if (inventory.Quantity <= 0m)
        {
            db.Inventories.Remove(inventory);
        }

        // ── Credit bank account ───────────────────────────────────────────────────
        bankAccount.Balance += totalProceeds;

        // ── Record ledger entries ─────────────────────────────────────────────────
        var currentTick = await GetCurrentTickAsync(db);
        var company = sourceUnit.Building.Company;

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = sourceUnit.BuildingId,
            BankAccountId = bankAccount.Id,
            Category = LedgerCategory.Revenue,
            Description = $"Exchange sale: {input.Quantity:F2}× {resource.Name} at {exchangePrice:F2} {bankCurrency}/unit",
            Amount = totalProceeds,
            RecordedAtTick = currentTick,
            ResourceTypeId = resource.Id,
        });

        await db.SaveChangesAsync();

        return new SellToExchangeResult
        {
            Success = true,
            ResourceName = resource.Name,
            QuantitySold = input.Quantity,
            ExchangePricePerUnit = exchangePrice,
            TotalProceeds = totalProceeds,
            CurrencyCode = bankCurrency,
            NewBankBalance = bankAccount.Balance,
        };
    }
}
