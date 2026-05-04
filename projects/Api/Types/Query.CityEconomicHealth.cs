using Api.Data;
using Api.Data.Entities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Returns the latest <see cref="CityEconomicReport"/> for the given city,
    /// plus up to <paramref name="cycles"/> historical reports ordered by cycle descending.
    /// </summary>
    [Authorize]
    public async Task<CityEconomicReportResult> GetCityEconomicReport(
        Guid cityId,
        [Service] AppDbContext db,
        int cycles = 10)
    {
        var clampedCycles = Math.Clamp(cycles, 1, 20);

        var reports = await db.CityEconomicReports
            .AsNoTracking()
            .Where(r => r.CityId == cityId)
            .OrderByDescending(r => r.TaxCycleEnd)
            .Take(clampedCycles)
            .ToListAsync();

        return new CityEconomicReportResult(reports.FirstOrDefault(), reports);
    }

    /// <summary>
    /// Returns the last <paramref name="cycles"/> economic reports for the given city,
    /// ordered by cycle ascending (oldest first) for chart/trend use.
    /// </summary>
    [Authorize]
    public async Task<List<CityEconomicReport>> CityEconomicHistory(
        Guid cityId,
        [Service] AppDbContext db,
        int cycles = 10)
    {
        var clampedCycles = Math.Clamp(cycles, 1, 20);

        return await db.CityEconomicReports
            .AsNoTracking()
            .Where(r => r.CityId == cityId)
            .OrderByDescending(r => r.TaxCycleEnd)
            .Take(clampedCycles)
            .OrderBy(r => r.TaxCycleEnd)
            .ToListAsync();
    }
}

/// <summary>GraphQL result type for <see cref="Query.GetCityEconomicReport"/>.</summary>
public record CityEconomicReportResult(
    CityEconomicReport? Latest,
    List<CityEconomicReport> History);
