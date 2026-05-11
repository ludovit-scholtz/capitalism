using System.Globalization;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Mutation
{
    /// <summary>
    /// Sets the per-tick content spending budget for a player-owned MEDIA_HOUSE building.
    /// Each tick the specified amount is deducted from the company cash and converted to
    /// accumulated content using the level-based efficiency formula
    /// (efficiency = 1 – 1/(level+1); 50% at level 1, 66% at level 2, …).
    /// Pass null or 0 to stop content investment.
    /// </summary>
    [Authorize]
    public async Task<Building> SetMediaHouseContentBudget(
        SetMediaHouseContentBudgetInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (building.Type != BuildingType.MediaHouse)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only media house buildings support content budget configuration.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (building.IsGovernmentOwned)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government-owned media houses cannot be configured by players.")
                    .SetCode("GOVERNMENT_OWNED")
                    .Build());
        }

        if (input.ContentBudgetPerTick.HasValue && input.ContentBudgetPerTick.Value < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Content budget per tick must be non-negative.")
                    .SetCode("INVALID_BUDGET")
                    .Build());
        }

        building.ContentBudgetPerTick = (input.ContentBudgetPerTick is null or <= 0m)
            ? null
            : input.ContentBudgetPerTick;

        await db.SaveChangesAsync();
        return building;
    }

    /// <summary>
    /// Upgrades a player-owned MEDIA_HOUSE building to the next level, improving content delivery
    /// efficiency and power capacity.
    /// Deducts the FX-adjusted upgrade cost from the building's bank account immediately.
    /// A media house can be upgraded up to level <see cref="Engine.GameConstants.MaxMediaHouseLevel"/>.
    /// </summary>
    [Authorize]
    public async Task<Building> UpgradeMediaHouse(
        UpgradeMediaHouseInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .Include(b => b.City)
            .Include(b => b.BankAccount)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (building.Type != BuildingType.MediaHouse)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only media house buildings can be upgraded with this mutation.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (building.IsGovernmentOwned)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Government-owned media houses cannot be upgraded by players.")
                    .SetCode("GOVERNMENT_OWNED")
                    .Build());
        }

        if (building.Level >= Engine.GameConstants.MaxMediaHouseLevel)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"This media house is already at maximum level ({Engine.GameConstants.MaxMediaHouseLevel}).")
                    .SetCode("MAX_LEVEL_REACHED")
                    .Build());
        }

        // FX-adjust the upgrade cost to the building's city currency.
        var cityCurrencyCode = building.City?.CurrencyCode ?? "EUR";
        var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(db, [cityCurrencyCode]);
        var fxRate = FxRateHelper.GetEurRate(fxRates, cityCurrencyCode);
        var upgradeCost = decimal.Round(
            Engine.GameConstants.MediaHouseUpgradeCost(building.Level) * fxRate,
            2, MidpointRounding.AwayFromZero);

        var fundingAccount = building.BankAccount
            ?? await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(
                db,
                building,
                cityCurrencyCode,
                httpContextAccessor.HttpContext!.RequestAborted);

        if (!string.Equals(fundingAccount.CurrencyCode, cityCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Building bank account currency ({fundingAccount.CurrencyCode}) must match city currency ({cityCurrencyCode}). Assign a {cityCurrencyCode} account before upgrading.")
                    .SetCode("CURRENCY_MISMATCH")
                    .Build());
        }

        if (fundingAccount.Balance < upgradeCost)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Insufficient funds. Upgrade costs {upgradeCost.ToString("N0", CultureInfo.InvariantCulture)} {cityCurrencyCode} but bank account {fundingAccount.AccountNumber} only has {fundingAccount.Balance.ToString("N0", CultureInfo.InvariantCulture)} {cityCurrencyCode}.")
                    .SetCode("INSUFFICIENT_FUNDS")
                    .Build());
        }

        var oldLevel = building.Level;
        building.Level += 1;
        fundingAccount.Balance -= upgradeCost;

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync();

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = building.CompanyId,
            BuildingId = building.Id,
            Category = LedgerCategory.UnitUpgrade,
            Description = $"Media house upgrade: {building.MediaType?.ToLowerInvariant() ?? "media"} Lv{oldLevel}→{building.Level} ({building.Name})",
            Amount = -upgradeCost,
            RecordedAtTick = gameState?.CurrentTick ?? 0,
            RecordedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return building;
    }

    /// <summary>
    /// Creates or updates a media-house campaign unit configuration.
    /// The targeted company must be owned by the authenticated player.
    /// </summary>
    [Authorize]
    public async Task<MediaHouseUnit> ConfigureMediaHouseUnit(
        ConfigureMediaHouseUnitInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var building = await db.Buildings
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == input.BuildingId);

        if (building is null || building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage(ObjectAuthorizationService.FriendlyMessage)
                    .SetCode(ObjectAuthorizationService.NotFoundOrNotOwnedCode)
                    .Build());
        }

        if (building.Type != BuildingType.MediaHouse)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Only media house buildings support campaign configuration.")
                    .SetCode("INVALID_BUILDING_TYPE")
                    .Build());
        }

        if (building.DestroyedAtUtc is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Destroyed buildings cannot be configured.")
                    .SetCode("BUILDING_ALREADY_DESTROYED")
                    .Build());
        }

        if (input.CampaignBudgetPerTick < 0m)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Campaign budget per tick must be non-negative.")
                    .SetCode("INVALID_BUDGET")
                    .Build());
        }

        var normalizedMediaType = (input.MediaType ?? string.Empty).Trim().ToUpperInvariant();
        if (!MediaType.All.Contains(normalizedMediaType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Media type must be NEWSPAPER, RADIO, or TV.")
                    .SetCode("INVALID_MEDIA_TYPE")
                    .Build());
        }

        var targetCompany = await db.Companies
            .FirstOrDefaultAsync(c => c.Id == input.TargetCompanyId && c.PlayerId == userId);

        if (targetCompany is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Target company not found or not owned by the current player.")
                    .SetCode("TARGET_COMPANY_NOT_FOUND")
                    .Build());
        }

        MediaHouseUnit? unit = null;
        if (input.UnitId.HasValue)
        {
            unit = await db.MediaHouseUnits
                .FirstOrDefaultAsync(u => u.Id == input.UnitId.Value && u.BuildingId == input.BuildingId);
        }

        if (unit is null)
        {
            unit = new MediaHouseUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = input.BuildingId,
            };
            db.MediaHouseUnits.Add(unit);
        }

        unit.TargetCompanyId = input.TargetCompanyId;
        unit.MediaType = normalizedMediaType;
        unit.CampaignBudgetPerTick = decimal.Round(input.CampaignBudgetPerTick, 2, MidpointRounding.AwayFromZero);
        unit.IsActive = input.IsActive;
        unit.BrandQualityBoostPerTick = decimal.Round(
            unit.CampaignBudgetPerTick
            * Engine.GameConstants.MediaHouseCampaignMultiplier(normalizedMediaType)
            * Math.Max(1, building.Level),
            6,
            MidpointRounding.AwayFromZero);
        unit.LaborCostPerTick = Engine.GameConstants.MediaHouseLaborCostPerTick(unit.CampaignBudgetPerTick);
        unit.EnergyCostPerTick = Engine.GameConstants.MediaHouseEnergyCostPerTick(unit.CampaignBudgetPerTick);

        await db.SaveChangesAsync();
        return unit;
    }
}
