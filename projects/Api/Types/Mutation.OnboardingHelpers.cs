using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Security;
using Api.Utilities;
using Capitalism.Shared.Referrals;
using HotChocolate.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Api.Types;

public sealed partial class Mutation
{
    private static bool IsStarterOnboardingProduct(ProductType product)
    {
        return StarterOnboardingProductsByIndustry.TryGetValue(product.Industry, out var starterSlugs)
            && starterSlugs.Contains(product.Slug, StringComparer.Ordinal);
    }

    private static void ClearOnboardingProgress(Player player)
    {
        player.OnboardingCurrentStep = null;
        player.OnboardingIndustry = null;
        player.OnboardingCityId = null;
        player.OnboardingCompanyId = null;
        player.OnboardingFactoryLotId = null;
    }

    /// <summary>
    /// Returns a human-readable display name for a building type constant.
    /// Used when auto-generating building names.
    /// </summary>
    private static string BuildingTypeDisplayName(string buildingType) => buildingType switch
    {
        BuildingType.Mine => "Mine",
        BuildingType.Factory => "Factory",
        BuildingType.SalesShop => "Sales Shop",
        BuildingType.ResearchDevelopment => "R&D Lab",
        BuildingType.Apartment => "Apartment",
        BuildingType.Commercial => "Office",
        BuildingType.MediaHouse => "Media House",
        BuildingType.Bank => "Bank",
        BuildingType.Exchange => "Exchange",
        BuildingType.PowerPlant => "Power Plant",
        _ => "Building"
    };

    private sealed record StarterIpoSelection(decimal RaiseTarget, decimal FounderOwnershipRatio)
    {
        public decimal FounderShareCount => decimal.Round(DefaultCompanyShareCount * FounderOwnershipRatio, 4, MidpointRounding.AwayFromZero);
    }

    private static void AddStarterFactoryShell(AppDbContext db, Guid buildingId)
    {
        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.Purchase, GridX = 0, GridY = 0, Level = 1, LinkRight = true, PurchaseSource = "OPTIMAL" },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.Manufacturing, GridX = 1, GridY = 0, Level = 1, LinkRight = true },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.Storage, GridX = 2, GridY = 0, Level = 1, LinkRight = true },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.B2BSales, GridX = 3, GridY = 0, Level = 1, SaleVisibility = "COMPANY" }
        );
    }

    private static void ConfigureStarterFactory(AppDbContext db, Building factory, ProductType product, Guid starterResourceId, decimal fxRate = 1m)
    {
        db.BuildingUnits.RemoveRange(factory.Units);
        db.BuildingUnits.AddRange(
            // MaxPrice is intentionally left null so the starter factory can always purchase
            // raw materials from the global exchange regardless of the product's base price.
            // Example: Bread (base price 3 coins) requires Grain whose exchange price is ~6 coins
            // in Bratislava — capping MaxPrice to product.BasePrice would permanently block the
            // purchase unit from buying any input material for that industry.
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factory.Id, UnitType = UnitType.Purchase, GridX = 0, GridY = 0, Level = 1, LinkRight = true, ResourceTypeId = starterResourceId, PurchaseSource = "OPTIMAL" },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factory.Id, UnitType = UnitType.Manufacturing, GridX = 1, GridY = 0, Level = 1, LinkRight = true, ProductTypeId = product.Id },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factory.Id, UnitType = UnitType.Storage, GridX = 2, GridY = 0, Level = 1, LinkRight = true },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = factory.Id, UnitType = UnitType.B2BSales, GridX = 3, GridY = 0, Level = 1, ProductTypeId = product.Id, MinPrice = decimal.Round(product.BasePrice * fxRate, 2), SaleVisibility = "COMPANY" }
        );
    }

    private static void AddStarterShop(AppDbContext db, Guid companyId, Guid buildingId, ProductType product, decimal fxRate = 1m)
    {
        db.BuildingUnits.AddRange(
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.Purchase, GridX = 0, GridY = 0, Level = 1, LinkRight = true, ProductTypeId = product.Id, PurchaseSource = "LOCAL", MaxPrice = decimal.Round(product.BasePrice * fxRate * 1.1m, 2), VendorLockCompanyId = companyId },
            new BuildingUnit { Id = Guid.NewGuid(), BuildingId = buildingId, UnitType = UnitType.PublicSales, GridX = 1, GridY = 0, Level = 1, ProductTypeId = product.Id, MinPrice = decimal.Round(product.BasePrice * fxRate * 1.5m, 2) }
        );
    }

    private static async Task<(BuildingLot Lot, Building Building)> PrepareLotPurchaseAsync(
        AppDbContext db,
        Company company,
        Guid lotId,
        string buildingType,
        string? buildingName,
        decimal powerConsumption,
        DateTime builtAtUtc,
        Guid? expectedCityId = null,
        string? powerPlantType = null,
        bool applyConstructionDelay = false,
        bool validateDestinationCurrency = false,
        decimal purchaseDiscountRate = 0m,
        bool skipCityUnlockValidation = false)
    {
        var lot = await db.BuildingLots
            .Include(candidate => candidate.City)
            .Include(candidate => candidate.ResourceType)
            .FirstOrDefaultAsync(candidate => candidate.Id == lotId);

        if (lot is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Building lot not found.")
                    .SetCode("LOT_NOT_FOUND")
                    .Build());
        }

        if (lot.OwnerCompanyId is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This lot has already been purchased.")
                    .SetCode("LOT_ALREADY_OWNED")
                    .Build());
        }

        if (expectedCityId.HasValue && lot.CityId != expectedCityId.Value)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This lot does not belong to the selected onboarding city.")
                    .SetCode("LOT_CITY_MISMATCH")
                    .Build());
        }

        if (!BuildingType.All.Contains(buildingType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Invalid building type: {buildingType}")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (!skipCityUnlockValidation)
        {
            var cityUnlockStatus = await CityUnlockService.GetStatusForCityAsync(db, lot.CityId, company.Id);
            if (cityUnlockStatus is not null && !cityUnlockStatus.IsUnlocked)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"City {lot.City?.Name ?? "Unknown"} is locked for this company.")
                        .SetCode("CITY_LOCKED")
                        .SetExtension("cityId", lot.CityId)
                        .SetExtension("requiredNetWorth", cityUnlockStatus.RequiredNetWorth)
                        .SetExtension("currentNetWorth", cityUnlockStatus.CurrentNetWorth)
                        .SetExtension("currency", cityUnlockStatus.Currency)
                        .Build());
            }
        }

        var suitableTypes = lot.SuitableTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (!suitableTypes.Contains(buildingType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Building type {buildingType} is not suitable for this lot. Suitable types: {lot.SuitableTypes}")
                    .SetCode("UNSUITABLE_BUILDING_TYPE")
                    .Build());
        }

        // Mines can only be placed on lots that have a raw material deposit.
        if (buildingType == BuildingType.Mine && lot.ResourceTypeId is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A Mine can only be placed on a lot with a raw material deposit. This lot has no resource data.")
                    .SetCode("MINE_REQUIRES_RESOURCE_DEPOSIT")
                    .Build());
        }

        var currentTick = (await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync())?.CurrentTick ?? 0;
        var constructionCost = applyConstructionDelay ? Engine.GameConstants.ConstructionCost(buildingType) : 0m;
        var effectiveDiscountRate = decimal.Clamp(purchaseDiscountRate, 0m, ReferralProgramConstants.PurchaseDiscountRate);
        var discountedLotPrice = decimal.Round(lot.Price * (1m - effectiveDiscountRate), 2, MidpointRounding.AwayFromZero);
        var discountedConstructionCost = decimal.Round(constructionCost * (1m - effectiveDiscountRate), 2, MidpointRounding.AwayFromZero);
        var totalCost = discountedLotPrice + discountedConstructionCost;

        var cityCurrencyCode = lot.City?.CurrencyCode ?? "EUR";
        var fundingAccount = await CompanyBankingService.EnsurePreferredAccountAsync(db, company.Id, cityCurrencyCode);

        if (fundingAccount.Balance < totalCost)
        {
            var errorCode = validateDestinationCurrency && !string.Equals(cityCurrencyCode, "EUR", StringComparison.OrdinalIgnoreCase)
                ? "INSUFFICIENT_LOCAL_CURRENCY_FUNDS"
                : "INSUFFICIENT_FUNDS";

            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient {cityCurrencyCode} company funds. This purchase requires {totalCost.ToString("N0", CultureInfo.InvariantCulture)} {cityCurrencyCode}, but company account {fundingAccount.AccountNumber} only has {fundingAccount.Balance.ToString("N0", CultureInfo.InvariantCulture)} {cityCurrencyCode}.")
                    .SetCode(errorCode)
                    .Build());
        }

        fundingAccount.Balance -= totalCost;

        var constructionTicks = applyConstructionDelay ? Engine.GameConstants.ConstructionTicks(buildingType) : 0;

        // Auto-generate a natural building name when not provided.
        if (string.IsNullOrWhiteSpace(buildingName))
        {
            var existingCount = await db.Buildings
                .CountAsync(b => b.CompanyId == company.Id && b.Type == buildingType);
            var typeLabel = BuildingTypeDisplayName(buildingType);
            buildingName = $"{typeLabel} #{existingCount + 1}";
        }

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = lot.CityId,
            Type = buildingType,
            Name = buildingName,
            Latitude = lot.Latitude,
            Longitude = lot.Longitude,
            Level = 1,
            PowerConsumption = powerConsumption,
            PowerPlantType = buildingType == BuildingType.PowerPlant ? (powerPlantType ?? Data.Entities.PowerPlantType.Coal) : null,
            PowerOutput = buildingType == BuildingType.PowerPlant
                ? Engine.GameConstants.DefaultPowerOutputMw(powerPlantType ?? Data.Entities.PowerPlantType.Coal)
                : null,
            BuiltAtUtc = builtAtUtc,
            IsUnderConstruction = applyConstructionDelay,
            ConstructionCompletesAtTick = applyConstructionDelay ? currentTick + constructionTicks : null,
            ConstructionCost = constructionCost,
        };

        // Property buildings must always start with numeric occupancy and a known area.
        if (buildingType == BuildingType.Apartment || buildingType == BuildingType.Commercial)
        {
            building.TotalAreaSqm = Engine.GameConstants.DefaultPropertyAreaSqm(buildingType);
            building.OccupancyPercent = Engine.GameConstants.PropertyInitialOccupancyPercent;
        }

        db.Buildings.Add(building);
        await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(db, building, lot.City?.CurrencyCode);
        lot.OwnerCompanyId = company.Id;
        lot.BuildingId = building.Id;
        lot.ConcurrencyToken = Guid.NewGuid();

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = building.Id,
            Category = LedgerCategory.PropertyPurchase,
            Description = $"Purchased lot: {lot.Name}",
            Amount = -discountedLotPrice,
            RecordedAtTick = currentTick,
            RecordedAtUtc = builtAtUtc,
        });

        if (applyConstructionDelay && discountedConstructionCost > 0m)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.ConstructionCost,
                Description = $"Construction order: {buildingName} ({buildingType})",
                Amount = -discountedConstructionCost,
                RecordedAtTick = currentTick,
                RecordedAtUtc = builtAtUtc,
            });
        }

        return (lot, building);
    }

    private static async Task<decimal> GetReferralDiscountRateIfRegisteredAsync(
        AppDbContext db,
        Guid playerId,
        CancellationToken cancellationToken)
    {
        var hasActiveRegistration = await db.ReferralRegistrations
            .AsNoTracking()
            .AnyAsync(registration => registration.ReferredPlayerId == playerId, cancellationToken);
        return hasActiveRegistration ? ReferralProgramConstants.PurchaseDiscountRate : 0m;
    }

    private static async Task<decimal> EnsureReferralRegistrationAndGetDiscountRateAsync(
        AppDbContext db,
        Player player,
        CancellationToken cancellationToken)
    {
        var normalizedCode = player.AppliedReferralCode?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return 0m;
        }

        var existingRegistration = await db.ReferralRegistrations
            .AsNoTracking()
            .AnyAsync(registration => registration.ReferredPlayerId == player.Id, cancellationToken);
        if (existingRegistration)
        {
            return ReferralProgramConstants.PurchaseDiscountRate;
        }

        var referralCode = await db.ReferralCodes
            .FirstOrDefaultAsync(code => code.Code == normalizedCode, cancellationToken);
        if (referralCode is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("The referral code on your account is not valid.")
                    .SetCode("REFERRAL_CODE_INVALID")
                    .Build());
        }

        if (referralCode.CreatorPlayerId == player.Id)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("You cannot use your own referral code.")
                    .SetCode("REFERRAL_SELF_REFERRAL_NOT_ALLOWED")
                    .Build());
        }

        db.ReferralRegistrations.Add(new ReferralRegistration
        {
            Id = Guid.NewGuid(),
            ReferralCodeId = referralCode.Id,
            ReferredPlayerId = player.Id,
            RegisteredAtUtc = DateTime.UtcNow,
        });
        referralCode.UsageCount += 1;
        return ReferralProgramConstants.PurchaseDiscountRate;
    }

    private static async Task<Guid> FindCompatibleAvailableLotIdAsync(
        AppDbContext db,
        Guid cityId,
        string buildingType)
    {
        var lots = await db.BuildingLots
            .Where(lot => lot.CityId == cityId && lot.OwnerCompanyId == null)
            .ToListAsync();

        // MINE buildings require a raw material deposit — skip generated lots without one.
        var lotId = lots
            .OrderBy(lot => lot.Price)
            .FirstOrDefault(lot =>
                lot.SuitableTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(buildingType, StringComparer.OrdinalIgnoreCase)
                && (buildingType != BuildingType.Mine || lot.ResourceTypeId is not null))?
            .Id;

        if (lotId is not Guid matchingLotId || matchingLotId == Guid.Empty)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("No suitable land is currently available for this building type in the selected city.")
                    .SetCode("NO_SUITABLE_LOT_AVAILABLE")
                    .Build());
        }

        return matchingLotId;
    }
}
