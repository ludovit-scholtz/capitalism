using Api.Data;
using Api.Data.Entities;
using Api.Types;
using HotChocolate;
using Microsoft.EntityFrameworkCore;

namespace Api.Utilities;

/// <summary>
/// Handles validation, queueing, and activation of building configuration upgrades.
/// </summary>
public static class BuildingConfigurationService
{
    public const int LinkChangeTicks = 1;
    public const int UnitPlanChangeTicks = 3;

    public static async Task QueueConfigurationAsync(
        AppDbContext db,
        Building building,
        IReadOnlyCollection<BuildingConfigurationUnitInput> submittedUnits,
        long currentTick)
    {
        ValidateUnits(building.Type, submittedUnits);

        var existingPlan = await db.BuildingConfigurationPlans
            .Include(plan => plan.Units)
            .FirstOrDefaultAsync(plan => plan.BuildingId == building.Id);

        if (existingPlan is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("This building already has an upgrade in progress.")
                    .SetCode("BUILDING_UPGRADE_IN_PROGRESS")
                    .Build());
        }

        var currentUnits = building.Units.ToDictionary(unit => (unit.GridX, unit.GridY));
        var planUnits = submittedUnits
            .OrderBy(unit => unit.GridY)
            .ThenBy(unit => unit.GridX)
            .Select(unit => CreatePlanUnit(unit, currentUnits.GetValueOrDefault((unit.GridX, unit.GridY))))
            .ToList();

        var removedUnitTicks = currentUnits.Keys
            .Except(submittedUnits.Select(unit => (unit.GridX, unit.GridY)))
            .Select(_ => UnitPlanChangeTicks);

        var totalTicksRequired = planUnits.Select(unit => unit.TicksRequired)
            .Concat(removedUnitTicks)
            .DefaultIfEmpty(0)
            .Max();

        if (totalTicksRequired == 0)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("No building configuration changes were detected.")
                    .SetCode("NO_BUILDING_CONFIGURATION_CHANGES")
                    .Build());
        }

        var plan = new BuildingConfigurationPlan
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            SubmittedAtUtc = DateTime.UtcNow,
            SubmittedAtTick = currentTick,
            AppliesAtTick = currentTick + totalTicksRequired,
            TotalTicksRequired = totalTicksRequired,
            Units = planUnits
        };

        db.BuildingConfigurationPlans.Add(plan);
    }

    public static async Task ApplyDuePlansAsync(AppDbContext db, long currentTick)
    {
        var duePlans = await db.BuildingConfigurationPlans
            .Include(plan => plan.Units)
            .Include(plan => plan.Building)
            .ThenInclude(building => building.Units)
            .Where(plan => plan.AppliesAtTick <= currentTick)
            .ToListAsync();

        foreach (var plan in duePlans)
        {
            db.BuildingUnits.RemoveRange(plan.Building.Units);

            var nextUnits = plan.Units.Select(unit => new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = plan.BuildingId,
                UnitType = unit.UnitType,
                GridX = unit.GridX,
                GridY = unit.GridY,
                Level = unit.Level,
                LinkUp = unit.LinkUp,
                LinkDown = unit.LinkDown,
                LinkLeft = unit.LinkLeft,
                LinkRight = unit.LinkRight,
                LinkUpLeft = unit.LinkUpLeft,
                LinkUpRight = unit.LinkUpRight,
                LinkDownLeft = unit.LinkDownLeft,
                LinkDownRight = unit.LinkDownRight,
            }).ToList();

            plan.Building.Units = nextUnits;
            db.BuildingUnits.AddRange(nextUnits);
            db.BuildingConfigurationPlanUnits.RemoveRange(plan.Units);
            db.BuildingConfigurationPlans.Remove(plan);
        }
    }

    public static IReadOnlyList<string> GetAllowedUnitTypes(string buildingType)
    {
        return buildingType switch
        {
            BuildingType.Mine => [UnitType.Mining, UnitType.Storage, UnitType.B2BSales],
            BuildingType.Factory => [UnitType.Purchase, UnitType.Manufacturing, UnitType.Branding, UnitType.Storage, UnitType.B2BSales],
            BuildingType.SalesShop => [UnitType.Purchase, UnitType.Marketing, UnitType.PublicSales],
            BuildingType.ResearchDevelopment => [UnitType.ProductQuality, UnitType.BrandQuality],
            _ => []
        };
    }

    private static BuildingConfigurationPlanUnit CreatePlanUnit(
        BuildingConfigurationUnitInput input,
        BuildingUnit? currentUnit)
    {
        var ticksRequired = CalculateTicksRequired(currentUnit, input);

        return new BuildingConfigurationPlanUnit
        {
            Id = Guid.NewGuid(),
            UnitType = input.UnitType,
            GridX = input.GridX,
            GridY = input.GridY,
            Level = currentUnit?.Level ?? 1,
            LinkUp = input.LinkUp,
            LinkDown = input.LinkDown,
            LinkLeft = input.LinkLeft,
            LinkRight = input.LinkRight,
            LinkUpLeft = input.LinkUpLeft,
            LinkUpRight = input.LinkUpRight,
            LinkDownLeft = input.LinkDownLeft,
            LinkDownRight = input.LinkDownRight,
            TicksRequired = ticksRequired,
            IsChanged = ticksRequired > 0,
        };
    }

    private static int CalculateTicksRequired(BuildingUnit? currentUnit, BuildingConfigurationUnitInput input)
    {
        if (currentUnit is null)
        {
            return UnitPlanChangeTicks;
        }

        if (!string.Equals(currentUnit.UnitType, input.UnitType, StringComparison.Ordinal))
        {
            return UnitPlanChangeTicks;
        }

        if (currentUnit.LinkUp != input.LinkUp
            || currentUnit.LinkDown != input.LinkDown
            || currentUnit.LinkLeft != input.LinkLeft
            || currentUnit.LinkRight != input.LinkRight
            || currentUnit.LinkUpLeft != input.LinkUpLeft
            || currentUnit.LinkUpRight != input.LinkUpRight
            || currentUnit.LinkDownLeft != input.LinkDownLeft
            || currentUnit.LinkDownRight != input.LinkDownRight)
        {
            return LinkChangeTicks;
        }

        return 0;
    }

    private static void ValidateUnits(string buildingType, IReadOnlyCollection<BuildingConfigurationUnitInput> submittedUnits)
    {
        if (submittedUnits.Count > 16)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("A building configuration can contain at most 16 units.")
                    .SetCode("TOO_MANY_BUILDING_UNITS")
                    .Build());
        }

        var allowedUnitTypes = GetAllowedUnitTypes(buildingType);
        var duplicatePositions = submittedUnits
            .GroupBy(unit => (unit.GridX, unit.GridY))
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicatePositions is not null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Each building grid position can contain only one unit.")
                    .SetCode("DUPLICATE_BUILDING_UNIT_POSITION")
                    .Build());
        }

        foreach (var unit in submittedUnits)
        {
            if (unit.GridX < 0 || unit.GridX > 3 || unit.GridY < 0 || unit.GridY > 3)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("Building unit positions must stay within the 4x4 grid.")
                        .SetCode("INVALID_BUILDING_UNIT_POSITION")
                        .Build());
            }

            if (!allowedUnitTypes.Contains(unit.UnitType))
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage($"Unit type {unit.UnitType} is not allowed for building type {buildingType}.")
                        .SetCode("INVALID_BUILDING_UNIT_TYPE")
                        .Build());
            }
        }
    }
}
