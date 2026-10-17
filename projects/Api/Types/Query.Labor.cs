using Api.Data;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;
using Shared.Economy;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Labor market of every city as computed by the last tick: pool, demand, unemployment, wage pressure and the
    /// demand-weighted average salary. With <paramref name="companyId"/> (must be owned by the caller) each row also
    /// carries the company's own salary multiplier, its pay ratio against the city average and whether that ratio puts
    /// it at risk of strikes. Read-only; no writes.
    /// </summary>
    [Authorize]
    public async Task<List<CityLaborMarketItem>> GetCityLaborMarkets(
        Guid? companyId,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] ObjectAuthorizationService objectAuthorization)
    {
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();
        var ct = httpContextAccessor.HttpContext.RequestAborted;

        Dictionary<Guid, decimal>? salaryByCity = null;
        Dictionary<Guid, List<AffectedBuildingItem>>? affectedByCity = null;
        var currentTick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultAsync(ct);
        if (companyId.HasValue)
        {
            var ownerPlayerId = await db.Companies
                .AsNoTracking()
                .Where(company => company.Id == companyId.Value)
                .Select(company => (Guid?)company.PlayerId)
                .FirstOrDefaultAsync(ct);
            if (ownerPlayerId != userId)
            {
                throw objectAuthorization.Deny(
                    userId,
                    "company",
                    companyId.Value,
                    ownerPlayerId is null ? ObjectAuthorizationService.NotFoundReason : ObjectAuthorizationService.NotOwnedReason);
            }

            affectedByCity = (await db.Buildings
                    .AsNoTracking()
                    .Where(building => building.CompanyId == companyId.Value
                        && building.DestroyedAtUtc == null
                        && (building.StaffingFactor < 1m || building.StrikeUntilTick != null))
                    .Select(building => new { building.Id, building.CityId, building.Name, building.StaffingFactor, building.StrikeUntilTick })
                    .ToListAsync(ct))
                .GroupBy(building => building.CityId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(building => building.Name)
                        .Select(building => new AffectedBuildingItem
                        {
                            BuildingId = building.Id,
                            Name = building.Name,
                            StaffingFactor = building.StaffingFactor,
                            OnStrike = building.StrikeUntilTick != null && currentTick <= building.StrikeUntilTick,
                            StrikeUntilTick = building.StrikeUntilTick,
                        })
                        .ToList());

            salaryByCity = await db.CompanyCitySalarySettings
                .AsNoTracking()
                .Where(setting => setting.CompanyId == companyId.Value)
                .ToDictionaryAsync(setting => setting.CityId, setting => setting.SalaryMultiplier, ct);
        }

        var markets = await db.CityLaborMarkets
            .AsNoTracking()
            .ToDictionaryAsync(market => market.CityId, ct);
        var cityRows = await db.Cities
            .AsNoTracking()
            .OrderBy(city => city.Name)
            .Select(city => new { city.Id, city.Name, city.Population })
            .ToListAsync(ct);
        // Every city is listed; a city the tick has not reached yet is neutral rather than missing.
        var rows = cityRows
            .Select(city =>
            {
                markets.TryGetValue(city.Id, out var market);
                return new
                {
                    CityId = city.Id,
                    CityName = city.Name,
                    city.Population,
                    LaborPool = market?.LaborPool ?? Shared.Economy.LaborMarketCalculator.ComputeLaborPool(city.Population),
                    LaborDemand = market?.LaborDemand ?? 0m,
                    Utilization = market?.Utilization ?? 0m,
                    UnemploymentRate = market?.UnemploymentRate ?? 0m,
                    WageIndex = market?.WageIndex ?? 1m,
                    StaffingFillRate = market?.StaffingFillRate ?? 1m,
                    AverageSalaryMultiplier = market?.AverageSalaryMultiplier ?? 1m,
                    UpdatedAtTick = market?.UpdatedAtTick ?? 0L,
                };
            })
            .ToList();

        return rows
            .Select(row =>
            {
                decimal? yourSalary = null;
                decimal? payRatio = null;
                bool? strikeProne = null;
                if (salaryByCity is not null)
                {
                    yourSalary = Utilities.CompanyEconomyCalculator.ClampSalaryMultiplier(
                        salaryByCity.GetValueOrDefault(row.CityId, Utilities.CompanyEconomyCalculator.DefaultSalaryMultiplier));
                    payRatio = LaborMarketCalculator.ComputePayRatio(yourSalary.Value, row.AverageSalaryMultiplier);
                    strikeProne = LaborMarketCalculator.IsStrikeProne(payRatio.Value);
                }

                return new CityLaborMarketItem
                {
                    CityId = row.CityId,
                    CityName = row.CityName,
                    Population = row.Population,
                    LaborPool = row.LaborPool,
                    LaborDemand = row.LaborDemand,
                    Utilization = row.Utilization,
                    UnemploymentRate = row.UnemploymentRate,
                    WageIndex = row.WageIndex,
                    StaffingFillRate = row.StaffingFillRate,
                    AverageSalaryMultiplier = row.AverageSalaryMultiplier,
                    UpdatedAtTick = row.UpdatedAtTick,
                    YourSalaryMultiplier = yourSalary,
                    PayRatio = payRatio,
                    StrikeProne = strikeProne,
                    AffectedBuildings = affectedByCity?.GetValueOrDefault(row.CityId) ?? [],
                };
            })
            .ToList();
    }
}

/// <summary>One city's labor market row, optionally with the caller's company pay position.</summary>
public sealed class CityLaborMarketItem
{
    public Guid CityId { get; set; }
    public string CityName { get; set; } = string.Empty;
    public int Population { get; set; }
    public decimal LaborPool { get; set; }
    public decimal LaborDemand { get; set; }
    public decimal Utilization { get; set; }
    public decimal UnemploymentRate { get; set; }
    public decimal WageIndex { get; set; }
    public decimal StaffingFillRate { get; set; }
    public decimal AverageSalaryMultiplier { get; set; }
    public long UpdatedAtTick { get; set; }
    public decimal? YourSalaryMultiplier { get; set; }
    public decimal? PayRatio { get; set; }
    public bool? StrikeProne { get; set; }

    /// <summary>The caller company's buildings in this city that are on strike or short of staff (empty without a company).</summary>
    public List<AffectedBuildingItem> AffectedBuildings { get; set; } = [];
}

/// <summary>A building held back by the labor market.</summary>
public sealed class AffectedBuildingItem
{
    public Guid BuildingId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal StaffingFactor { get; set; }
    public bool OnStrike { get; set; }
    public long? StrikeUntilTick { get; set; }
}
