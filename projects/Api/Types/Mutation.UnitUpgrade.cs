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
    /// Schedules a level upgrade for a building unit.
    /// Deducts the upgrade cost from the owning company's cash immediately
    /// and creates (or extends) a queued building configuration plan that applies after the required ticks.
    /// Up to <see cref="Engine.GameConstants.MaxConcurrentUnitUpgrades"/> units may be upgrading
    /// simultaneously within the same building.
    /// </summary>
    [Authorize]
    public async Task<BuildingConfigurationPlan> ScheduleUnitUpgrade(
        ScheduleUnitUpgradeInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()
            ?? throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Game state is not initialized.")
                    .SetCode("GAME_STATE_NOT_FOUND")
                    .Build());

        await BuildingConfigurationService.ApplyDuePlansAsync(db, gameState.CurrentTick);

        var unit = await db.BuildingUnits
            .Include(u => u.Building)
            .ThenInclude(b => b.City)
            .Include(u => u.Building)
            .ThenInclude(b => b.BankAccount)
            .Include(u => u.Building)
            .ThenInclude(b => b.Company)
            .Include(u => u.Building)
            .ThenInclude(b => b.Units)
            .Include(u => u.Building)
            .ThenInclude(b => b.PendingConfiguration)
            .ThenInclude(plan => plan!.Units)
            .Include(u => u.Building)
            .ThenInclude(b => b.PendingConfiguration)
            .ThenInclude(plan => plan!.Removals)
            .AsSplitQuery()
            .FirstOrDefaultAsync(u => u.Id == input.UnitId);

        if (unit is null || unit.Building.Company.PlayerId != userId)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Unit not found or you don't own it.")
                    .SetCode("UNIT_NOT_FOUND")
                    .Build());
        }

        if (!Engine.GameConstants.IsUpgradableUnitType(unit.UnitType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Unit type {unit.UnitType} does not support level upgrades.")
                    .SetCode("UNIT_NOT_UPGRADABLE")
                    .Build());
        }

        if (unit.Level >= Engine.GameConstants.MaxUnitLevel)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"This unit is already at maximum level ({Engine.GameConstants.MaxUnitLevel}).")
                    .SetCode("MAX_LEVEL_REACHED")
                    .Build());
        }

        // Apply the city's FX rate so the upgrade cost is expressed in the building's
        // local currency (e.g. CZK for Prague, INR for Delhi).
        var cityCurrencyCode = unit.Building.City?.CurrencyCode ?? "EUR";
        var fxRates = await Utilities.FxRateHelper.BuildEurRatesLookupAsync(db, [cityCurrencyCode]);
        var fxRate = Utilities.FxRateHelper.GetEurRate(fxRates, cityCurrencyCode);
        var upgradeCost = decimal.Round(
            Engine.GameConstants.UnitUpgradeCost(unit.UnitType, unit.Level) * fxRate,
            2, MidpointRounding.AwayFromZero);
        var company = unit.Building.Company;
        var fundingAccount = unit.Building.BankAccount
            ?? await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(
                db,
                unit.Building,
                cityCurrencyCode,
                httpContextAccessor.HttpContext!.RequestAborted);

        // Guard: the assigned account must be in the building's city currency.
        // A currency mismatch would charge the wrong-currency account by the FX-adjusted amount,
        // causing a massive over- or under-charge depending on direction.
        if (!string.Equals(fundingAccount.CurrencyCode, cityCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Building bank account currency ({fundingAccount.CurrencyCode}) must match city currency ({cityCurrencyCode}). Please assign a {cityCurrencyCode} bank account to this building before upgrading.")
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

        var upgradeTicks = Engine.GameConstants.UnitUpgradeTicks(unit.Level);
        var now = DateTime.UtcNow;
        Guid planId;

        var existingPlan = unit.Building.PendingConfiguration;

        if (existingPlan is not null)
        {
            // Check whether the existing plan is upgrade-only (no structural changes, no removals).
            // An upgrade-only plan has no removals and all IsChanged plan units represent level bumps
            // of existing active units (same type, level increased).
            var activeUnitsByPos = unit.Building.Units
                .DistinctBy(u => (u.GridX, u.GridY))
                .ToDictionary(u => (u.GridX, u.GridY));

            var isUpgradeOnlyPlan = existingPlan.Removals.Count == 0
                && existingPlan.Units
                    .Where(pu => pu.IsChanged)
                    .All(pu =>
                    {
                        var active = activeUnitsByPos.GetValueOrDefault((pu.GridX, pu.GridY));
                        return active is not null && pu.UnitType == active.UnitType && pu.Level > active.Level;
                    });

            if (!isUpgradeOnlyPlan)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("This building already has a pending configuration change. Wait for it to complete or cancel it before scheduling an upgrade.")
                        .SetCode("PENDING_CONFIGURATION_EXISTS")
                        .Build());
            }

            // Count current in-progress upgrades.
            var upgradesInProgress = existingPlan.Units.Count(pu => pu.IsChanged);

            if (upgradesInProgress >= Engine.GameConstants.MaxConcurrentUnitUpgrades)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"This building already has {Engine.GameConstants.MaxConcurrentUnitUpgrades} unit upgrades in progress. Wait for one to complete before scheduling another.")
                        .SetCode("MAX_CONCURRENT_UPGRADES")
                        .Build());
            }

            // Check if the target unit is already being upgraded in the existing plan.
            var targetPlanUnit = existingPlan.Units
                .FirstOrDefault(pu => pu.GridX == unit.GridX && pu.GridY == unit.GridY && pu.IsChanged);

            if (targetPlanUnit is not null)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("This unit is already scheduled for an upgrade.")
                        .SetCode("UNIT_ALREADY_UPGRADING")
                        .Build());
            }

            // Add the second unit upgrade to the existing plan.
            var secondPlanUnit = existingPlan.Units
                .FirstOrDefault(pu => pu.GridX == unit.GridX && pu.GridY == unit.GridY);

            if (secondPlanUnit is not null)
            {
                // Update the existing snapshot unit to become an upgrade.
                secondPlanUnit.Level = unit.Level + 1;
                secondPlanUnit.StartedAtTick = gameState.CurrentTick;
                secondPlanUnit.AppliesAtTick = gameState.CurrentTick + upgradeTicks;
                secondPlanUnit.TicksRequired = upgradeTicks;
                secondPlanUnit.IsChanged = true;
            }
            else
            {
                // The target unit was not in the snapshot (edge case: plan was created before unit was added).
                existingPlan.Units.Add(new BuildingConfigurationPlanUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingConfigurationPlanId = existingPlan.Id,
                    UnitType = unit.UnitType,
                    GridX = unit.GridX,
                    GridY = unit.GridY,
                    Level = unit.Level + 1,
                    LinkUp = unit.LinkUp,
                    LinkDown = unit.LinkDown,
                    LinkLeft = unit.LinkLeft,
                    LinkRight = unit.LinkRight,
                    LinkUpLeft = unit.LinkUpLeft,
                    LinkUpRight = unit.LinkUpRight,
                    LinkDownLeft = unit.LinkDownLeft,
                    LinkDownRight = unit.LinkDownRight,
                    StartedAtTick = gameState.CurrentTick,
                    AppliesAtTick = gameState.CurrentTick + upgradeTicks,
                    TicksRequired = upgradeTicks,
                    IsChanged = true,
                    ResourceTypeId = unit.ResourceTypeId,
                    ProductTypeId = unit.ProductTypeId,
                    MinPrice = unit.MinPrice,
                    MaxPrice = unit.MaxPrice,
                    PurchaseSource = unit.PurchaseSource,
                    SaleVisibility = unit.SaleVisibility,
                    Budget = unit.Budget,
                    MediaHouseBuildingId = unit.MediaHouseBuildingId,
                    MinQuality = unit.MinQuality,
                    BrandScope = unit.BrandScope,
                    VendorLockCompanyId = unit.VendorLockCompanyId,
                    LockedCityId = unit.LockedCityId,
                });
            }

            // Refresh the plan's overall AppliesAtTick to be the max across all changed units.
            var maxAppliesAtTick = existingPlan.Units
                .Where(pu => pu.IsChanged)
                .Max(pu => pu.AppliesAtTick);
            existingPlan.AppliesAtTick = maxAppliesAtTick;
            existingPlan.TotalTicksRequired = (int)(maxAppliesAtTick - existingPlan.SubmittedAtTick);

            planId = existingPlan.Id;
        }
        else
        {
            // No existing plan: create a fresh upgrade plan.
            planId = Guid.NewGuid();

            var plan = new BuildingConfigurationPlan
            {
                Id = planId,
                BuildingId = unit.BuildingId,
                SubmittedAtUtc = now,
                SubmittedAtTick = gameState.CurrentTick,
                AppliesAtTick = gameState.CurrentTick + upgradeTicks,
                TotalTicksRequired = upgradeTicks,
            };

            // Snapshot all active units; only the target unit gets a level bump and a timer.
            // DistinctBy is defensive deduplication per coding guidelines; AsSplitQuery() above prevents
            // Cartesian explosion, but we deduplicate by position as a safety net.
            var allActiveUnits = unit.Building.Units.DistinctBy(u => (u.GridX, u.GridY)).ToList();
            foreach (var activeUnit in allActiveUnits)
            {
                bool isTarget = activeUnit.Id == unit.Id;
                plan.Units.Add(new BuildingConfigurationPlanUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingConfigurationPlanId = planId,
                    UnitType = activeUnit.UnitType,
                    GridX = activeUnit.GridX,
                    GridY = activeUnit.GridY,
                    Level = isTarget ? activeUnit.Level + 1 : activeUnit.Level,
                    LinkUp = activeUnit.LinkUp,
                    LinkDown = activeUnit.LinkDown,
                    LinkLeft = activeUnit.LinkLeft,
                    LinkRight = activeUnit.LinkRight,
                    LinkUpLeft = activeUnit.LinkUpLeft,
                    LinkUpRight = activeUnit.LinkUpRight,
                    LinkDownLeft = activeUnit.LinkDownLeft,
                    LinkDownRight = activeUnit.LinkDownRight,
                    StartedAtTick = gameState.CurrentTick,
                    AppliesAtTick = isTarget ? gameState.CurrentTick + upgradeTicks : gameState.CurrentTick,
                    TicksRequired = isTarget ? upgradeTicks : 0,
                    IsChanged = isTarget,
                    ResourceTypeId = activeUnit.ResourceTypeId,
                    ProductTypeId = activeUnit.ProductTypeId,
                    MinPrice = activeUnit.MinPrice,
                    MaxPrice = activeUnit.MaxPrice,
                    PurchaseSource = activeUnit.PurchaseSource,
                    SaleVisibility = activeUnit.SaleVisibility,
                    Budget = activeUnit.Budget,
                    MediaHouseBuildingId = activeUnit.MediaHouseBuildingId,
                    MinQuality = activeUnit.MinQuality,
                    BrandScope = activeUnit.BrandScope,
                    VendorLockCompanyId = activeUnit.VendorLockCompanyId,
                    LockedCityId = activeUnit.LockedCityId,
                });
            }

            db.BuildingConfigurationPlans.Add(plan);
        }

        fundingAccount.Balance -= upgradeCost;

        db.LedgerEntries.Add(new LedgerEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            BuildingId = unit.BuildingId,
            BuildingUnitId = unit.Id,
            Category = LedgerCategory.UnitUpgrade,
            Description = $"Unit upgrade: {unit.UnitType} Lv{unit.Level}→{unit.Level + 1} ({unit.Building.Name})",
            Amount = -upgradeCost,
            RecordedAtTick = gameState.CurrentTick,
            RecordedAtUtc = now,
        });

        await db.SaveChangesAsync();

        return await db.BuildingConfigurationPlans
            .Include(p => p.Units)
            .Include(p => p.Removals)
            .FirstAsync(p => p.Id == planId);
    }
}
