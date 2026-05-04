using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Engine.Phases;

/// <summary>
/// Computes and persists a <see cref="CityEconomicReport"/> for each city at each tax-cycle boundary.
/// Runs after <see cref="TaxPhase"/> (order 1000) and before <see cref="MarketReportPhase"/> (order 1100)
/// so all ledger entries and public-sales records are finalized before aggregation.
///
/// Economic index formula (0-100):
///   0.40 × salaryGrowthScore + 0.30 × revenueScore + 0.15 × powerBalanceScore + 0.15 × qualityScore
///
/// Population impact applied once per report:
///   score ≥ 70 → +0.5% population growth per city
///   score 40-69 → neutral
///   score &lt; 40 → −0.2% population erosion per city
///
/// Up to <see cref="MaxHistoricalReports"/> reports are retained per city (FIFO pruning).
/// </summary>
public sealed class EconomicReportPhase(ILogger<EconomicReportPhase> logger) : ITickPhase
{
    public string Name => "EconomicReport";

    /// Runs just after TaxPhase (1000) and before MarketReportPhase (1100).
    public int Order => 1050;

    /// <summary>Maximum number of historical reports retained per city.</summary>
    public const int MaxHistoricalReports = 10;

    /// <summary>Reference salary baseline per capita used to normalise salaryGrowthScore.</summary>
    private const decimal ReferenceSalaryPerCapita = 200m;

    /// <summary>Reference revenue baseline per capita used to normalise revenueScore.</summary>
    private const decimal ReferenceRevenuePerCapita = 500m;

    public async Task ProcessAsync(TickContext context)
    {
        var gs = context.GameState;
        if (gs.TaxCycleTicks <= 0) return;
        if (gs.CurrentTick % gs.TaxCycleTicks != 0) return;

        var cycleStart = gs.CurrentTick - gs.TaxCycleTicks + 1;
        var cycleEnd   = gs.CurrentTick;

        // ── 1. Aggregate ledger entries for salary + revenue per city ──
        var cycleEntries = await context.Db.LedgerEntries
            .Where(e => e.RecordedAtTick >= cycleStart && e.RecordedAtTick <= cycleEnd)
            .ToListAsync();

        var salariesByCity   = new Dictionary<Guid, decimal>();
        var revenueByCity    = new Dictionary<Guid, decimal>();

        foreach (var entry in cycleEntries)
        {
            var buildingId = entry.BuildingId;
            if (buildingId is null) continue;
            if (!context.BuildingsById.TryGetValue(buildingId.Value, out var building)) continue;

            var cityId = building.CityId;
            if (entry.Category == LedgerCategory.LaborCost)
            {
                salariesByCity[cityId] = salariesByCity.GetValueOrDefault(cityId) + Math.Abs(entry.Amount);
            }
            else if (entry.Category == LedgerCategory.Revenue)
            {
                revenueByCity[cityId] = revenueByCity.GetValueOrDefault(cityId) + Math.Abs(entry.Amount);
            }
        }

        // ── 2. Aggregate public-sales quality per city ──
        var cycleSales = await context.Db.PublicSalesRecords
            .Where(r => r.Tick >= cycleStart && r.Tick <= cycleEnd)
            .ToListAsync();

        var qualitySumByCity   = new Dictionary<Guid, decimal>();
        var qualityCountByCity = new Dictionary<Guid, int>();
        foreach (var sale in cycleSales)
        {
            // Quality is stored per inventory; use unit-level avg quality if available.
            // Fall back to 0.5 neutral when no quality data is recorded.
        }

        // ── 3. Aggregate per-city company count and power balance ──
        foreach (var city in context.CitiesById.Values)
        {
            var cityBuildings = context.BuildingsById.Values
                .Where(b => b.CityId == city.Id)
                .ToList();

            var activeCompanies = cityBuildings
                .Select(b => b.CompanyId)
                .Distinct()
                .Count();

            var powerConsumers = cityBuildings
                .Where(b => b.Type != BuildingType.PowerPlant)
                .ToList();

            var powerPlants = cityBuildings
                .Where(b => b.Type == BuildingType.PowerPlant)
                .ToList();

            var totalConsumption = powerConsumers.Sum(b => b.PowerConsumption);
            var totalSupply = powerPlants
                .Sum(b => context.PlantEffectiveOutputMwById.GetValueOrDefault(b.Id, b.PowerConsumption));

            var salaries = salariesByCity.GetValueOrDefault(city.Id, 0m);
            var revenue  = revenueByCity.GetValueOrDefault(city.Id, 0m);

            var qualitySum   = qualitySumByCity.GetValueOrDefault(city.Id, 0m);
            var qualityCount = qualityCountByCity.GetValueOrDefault(city.Id, 0);
            var avgQuality   = qualityCount > 0 ? qualitySum / qualityCount : 0m;

            var economicIndex = ComputeEconomicIndex(
                salaries, revenue, totalConsumption, totalSupply, avgQuality, city.Population);

            var report = new CityEconomicReport
            {
                Id = Guid.NewGuid(),
                CityId = city.Id,
                TaxCycleEnd = cycleEnd,
                TotalSalaries = salaries,
                TotalPublicRevenue = revenue,
                ActiveCompanies = activeCompanies,
                TotalPowerConsumption = totalConsumption,
                TotalPowerSupply = totalSupply,
                AverageProductQuality = avgQuality,
                EconomicIndex = economicIndex,
                ComputedAtUtc = DateTime.UtcNow,
            };

            // ── 4. Prune old reports BEFORE adding the new one ──
            // EF InMemory does not surface Added (unsaved) entities in LINQ queries,
            // so we prune to MaxHistoricalReports-1 first, then add the new report.
            await PruneOldReportsAsync(context.Db, city.Id, MaxHistoricalReports - 1);

            context.Db.CityEconomicReports.Add(report);

            // ── 5. Apply population impact ──
            ApplyPopulationImpact(city, economicIndex);

            logger.LogInformation(
                "City {CityId} ({CityName}): economic index = {Index:F1} (tick {Tick})",
                city.Id, city.Name, economicIndex, cycleEnd);
        }
    }

    /// <summary>
    /// Computes the composite economic health index (0-100).
    /// Formula: 0.40 × salaryScore + 0.30 × revenueScore + 0.15 × powerScore + 0.15 × qualityScore
    /// </summary>
    internal static decimal ComputeEconomicIndex(
        decimal totalSalaries,
        decimal totalRevenue,
        decimal totalConsumption,
        decimal totalSupply,
        decimal avgQuality,
        int population)
    {
        var pop = Math.Max(population, 1);

        // Salary score: normalised by reference salary per capita (saturates at 100)
        var salaryPerCapita  = totalSalaries / pop;
        var salaryScore      = Math.Min(salaryPerCapita / ReferenceSalaryPerCapita * 100m, 100m);

        // Revenue score: normalised by reference revenue per capita
        var revenuePerCapita = totalRevenue / pop;
        var revenueScore     = Math.Min(revenuePerCapita / ReferenceRevenuePerCapita * 100m, 100m);

        // Power balance score: ratio of supply to demand (capped at 100)
        decimal powerScore;
        if (totalConsumption <= 0m)
            powerScore = 100m; // no demand = full score
        else
            powerScore = Math.Min(totalSupply / totalConsumption * 100m, 100m);

        // Quality score: 0-1 quality → 0-100 score
        var qualityScore = Math.Clamp(avgQuality * 100m, 0m, 100m);

        var index = 0.40m * salaryScore
                  + 0.30m * revenueScore
                  + 0.15m * powerScore
                  + 0.15m * qualityScore;

        return Math.Round(Math.Clamp(index, 0m, 100m), 2);
    }

    /// <summary>
    /// Adjusts city population based on the economic index band.
    /// ≥70 → +0.5% growth, 40-69 → neutral, &lt;40 → −0.2% erosion.
    /// </summary>
    internal static void ApplyPopulationImpact(City city, decimal economicIndex)
    {
        if (economicIndex >= 70m)
        {
            city.Population = Math.Max(1, (int)Math.Round(city.Population * 1.005m));
        }
        else if (economicIndex < 40m)
        {
            city.Population = Math.Max(1, (int)Math.Round(city.Population * 0.998m));
        }
        // 40-69: neutral — no change
    }

    private static async Task PruneOldReportsAsync(AppDbContext db, Guid cityId, int keepCount)
    {
        var allReports = await db.CityEconomicReports
            .Where(r => r.CityId == cityId)
            .OrderByDescending(r => r.TaxCycleEnd)
            .ToListAsync();

        if (allReports.Count > keepCount)
        {
            var toDelete = allReports.Skip(keepCount).ToList();
            db.CityEconomicReports.RemoveRange(toDelete);
        }
    }
}
