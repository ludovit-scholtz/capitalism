using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

public sealed partial class Query
{
    /// <summary>
    /// Lists autonomous NPC companies, optionally scoped to one city.
    /// </summary>
    public async Task<List<NpcCompanySummaryResult>> GetNpcCompanies(
        Guid? cityId,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var query = db.NpcCompanies
            .AsNoTracking()
            .Include(npc => npc.Company)
            .Include(npc => npc.HomeCity)
            .AsQueryable();

        if (cityId.HasValue)
        {
            query = query.Where(npc => npc.HomeCityId == cityId.Value);
        }

        var npcs = await query
            .OrderBy(npc => npc.Name)
            .ToListAsync(cancellationToken);

        var companyIds = npcs.Select(npc => npc.CompanyId).ToList();
        var buildingCounts = await db.Buildings
            .AsNoTracking()
            .Where(building => companyIds.Contains(building.CompanyId) && building.DestroyedAtUtc == null)
            .GroupBy(building => building.CompanyId)
            .Select(group => new { CompanyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CompanyId, item => item.Count, cancellationToken);

        return npcs.Select(npc => new NpcCompanySummaryResult
        {
            Id = npc.Id,
            CompanyId = npc.CompanyId,
            Name = npc.Name,
            Archetype = npc.Archetype,
            DifficultyLevel = npc.DifficultyLevel,
            HomeCityId = npc.HomeCityId,
            HomeCityName = npc.HomeCity.Name,
            IsActive = npc.IsActive,
            CreatedAtUtc = npc.CreatedAtUtc,
            BuildingCount = buildingCounts.GetValueOrDefault(npc.CompanyId),
        }).ToList();
    }

    /// <summary>
    /// Detailed single NPC profile.
    /// </summary>
    public async Task<NpcCompanyDetailResult?> GetNpcCompanyDetail(
        Guid id,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var npc = await db.NpcCompanies
            .AsNoTracking()
            .Include(item => item.HomeCity)
            .Include(item => item.Company)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (npc is null)
        {
            return null;
        }

        var bankBalance = await db.BankAccounts
            .AsNoTracking()
            .Where(account => account.CompanyId == npc.CompanyId && account.ClosedAtUtc == null)
            .SumAsync(account => account.Balance, cancellationToken);
        var buildings = await db.Buildings
            .AsNoTracking()
            .Where(building => building.CompanyId == npc.CompanyId && building.DestroyedAtUtc == null)
            .Select(building => new NpcBuildingSummary
            {
                Id = building.Id,
                Name = building.Name,
                Type = building.Type,
                CityId = building.CityId,
                Latitude = building.Latitude,
                Longitude = building.Longitude,
            })
            .ToListAsync(cancellationToken);

        return new NpcCompanyDetailResult
        {
            Id = npc.Id,
            CompanyId = npc.CompanyId,
            Name = npc.Name,
            Archetype = npc.Archetype,
            DifficultyLevel = npc.DifficultyLevel,
            HomeCityId = npc.HomeCityId,
            HomeCityName = npc.HomeCity.Name,
            IsActive = npc.IsActive,
            CreatedAtUtc = npc.CreatedAtUtc,
            BankBalance = bankBalance,
            Buildings = buildings,
        };
    }

    /// <summary>
    /// City competitor panel rows (NPC and human companies) with recent market metrics.
    /// </summary>
    public async Task<List<CityCompetitorEntry>> GetCityCompetitors(
        Guid cityId,
        int lastNTicks,
        [Service] AppDbContext db,
        CancellationToken cancellationToken)
    {
        var clampedTicks = Math.Clamp(lastNTicks, 1, 200);
        var currentTick = await db.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync(cancellationToken);

        var fromTick = currentTick - clampedTicks;
        var previousFromTick = fromTick - clampedTicks;

        var companyRows = await db.Buildings
            .AsNoTracking()
            .Where(building => building.CityId == cityId && building.DestroyedAtUtc == null)
            .GroupBy(building => new { building.CompanyId, building.Company.Name })
            .Select(group => new { group.Key.CompanyId, CompanyName = group.Key.Name, BuildingCount = group.Count() })
            .ToListAsync(cancellationToken);

        if (companyRows.Count == 0)
        {
            return [];
        }

        var companyIds = companyRows.Select(row => row.CompanyId).ToList();
        var npcMap = await db.NpcCompanies
            .AsNoTracking()
            .Where(npc => companyIds.Contains(npc.CompanyId))
            .ToDictionaryAsync(npc => npc.CompanyId, cancellationToken);

        var currentWindowSales = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(record => record.CityId == cityId
                && companyIds.Contains(record.CompanyId)
                && record.Tick > fromTick)
            .Include(record => record.ProductType)
            .ToListAsync(cancellationToken);
        var previousWindowSales = await db.PublicSalesRecords
            .AsNoTracking()
            .Where(record => record.CityId == cityId
                && companyIds.Contains(record.CompanyId)
                && record.Tick <= fromTick
                && record.Tick > previousFromTick)
            .Include(record => record.ProductType)
            .ToListAsync(cancellationToken);

        var totalCityRevenue = currentWindowSales.Sum(record => record.Revenue);
        var revenueByCompany = currentWindowSales
            .GroupBy(record => record.CompanyId)
            .ToDictionary(group => group.Key, group => group.Sum(record => record.Revenue));
        var previousRevenueByCompany = previousWindowSales
            .GroupBy(record => record.CompanyId)
            .ToDictionary(group => group.Key, group => group.Sum(record => record.Revenue));

        var sharesByCompanyByIndustry = currentWindowSales
            .Where(record => record.ProductTypeId.HasValue && record.ProductType != null)
            .GroupBy(record => new { record.CompanyId, Industry = record.ProductType!.Industry })
            .ToDictionary(
                group => (group.Key.CompanyId, group.Key.Industry),
                group => group.Sum(record => record.Revenue));

        var totalsByIndustry = currentWindowSales
            .Where(record => record.ProductTypeId.HasValue && record.ProductType != null)
            .GroupBy(record => record.ProductType!.Industry)
            .ToDictionary(group => group.Key, group => group.Sum(record => record.Revenue));

        return companyRows
            .OrderByDescending(row => revenueByCompany.GetValueOrDefault(row.CompanyId))
            .ThenBy(row => row.CompanyName)
            .Select(row =>
            {
                var revenue = revenueByCompany.GetValueOrDefault(row.CompanyId);
                var previousRevenue = previousRevenueByCompany.GetValueOrDefault(row.CompanyId);
                var trend = revenue > previousRevenue * 1.05m
                    ? "UP"
                    : revenue < previousRevenue * 0.95m
                        ? "DOWN"
                        : "STABLE";

                var industryShares = totalsByIndustry
                    .Select(item =>
                    {
                        var companyIndustryRevenue = sharesByCompanyByIndustry.GetValueOrDefault((row.CompanyId, item.Key));
                        var share = item.Value <= 0m ? 0m : decimal.Round((companyIndustryRevenue / item.Value) * 100m, 2, MidpointRounding.AwayFromZero);
                        return new CompetitorMarketShareByCategory
                        {
                            Category = item.Key,
                            SharePercent = share,
                        };
                    })
                    .Where(item => item.SharePercent > 0m)
                    .OrderByDescending(item => item.SharePercent)
                    .ToList();

                var overallShare = totalCityRevenue <= 0m ? 0m : decimal.Round((revenue / totalCityRevenue) * 100m, 2, MidpointRounding.AwayFromZero);
                var isNpc = npcMap.TryGetValue(row.CompanyId, out var npc);

                return new CityCompetitorEntry
                {
                    CompanyId = row.CompanyId,
                    CompanyName = row.CompanyName,
                    BuildingCount = row.BuildingCount,
                    EstimatedRevenueLastTicks = revenue,
                    MarketSharePercent = overallShare,
                    MarketShareByCategory = industryShares,
                    Trend = trend,
                    IsNpc = isNpc,
                    NpcCompanyId = npc?.Id,
                    Archetype = npc?.Archetype,
                };
            })
            .ToList();
    }

    [Authorize]
    public async Task<List<NpcDecisionLogResult>> GetNpcDecisionLogs(
        Guid? npcCompanyId,
        int limit,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor,
        [Service] GameAdminAuthorizationService gameAdminAuthorizationService)
    {
        await gameAdminAuthorizationService.RequireAdminDashboardAccessAsync(
            db,
            httpContextAccessor.HttpContext!.User,
            httpContextAccessor.HttpContext.RequestAborted);

        var clampedLimit = Math.Clamp(limit, 1, 200);
        var query = db.NpcDecisionLogs
            .AsNoTracking()
            .Include(log => log.NpcCompany)
            .AsQueryable();
        if (npcCompanyId.HasValue)
        {
            query = query.Where(log => log.NpcCompanyId == npcCompanyId.Value);
        }

        return await query
            .OrderByDescending(log => log.Tick)
            .ThenByDescending(log => log.CreatedAtUtc)
            .Take(clampedLimit)
            .Select(log => new NpcDecisionLogResult
            {
                Id = log.Id,
                NpcCompanyId = log.NpcCompanyId,
                NpcCompanyName = log.NpcCompany.Name,
                Tick = log.Tick,
                ActionType = log.ActionType,
                Outcome = log.Outcome,
                CreatedAtUtc = log.CreatedAtUtc,
            })
            .ToListAsync(httpContextAccessor.HttpContext.RequestAborted);
    }
}

public sealed class NpcCompanySummaryResult
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public Guid HomeCityId { get; set; }
    public string HomeCityName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int BuildingCount { get; set; }
}

public sealed class NpcBuildingSummary
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public Guid CityId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public sealed class NpcCompanyDetailResult
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Archetype { get; set; } = string.Empty;
    public int DifficultyLevel { get; set; }
    public Guid HomeCityId { get; set; }
    public string HomeCityName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public decimal BankBalance { get; set; }
    public List<NpcBuildingSummary> Buildings { get; set; } = [];
}

public sealed class NpcDecisionLogResult
{
    public Guid Id { get; set; }
    public Guid NpcCompanyId { get; set; }
    public string NpcCompanyName { get; set; } = string.Empty;
    public long Tick { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class CompetitorMarketShareByCategory
{
    public string Category { get; set; } = string.Empty;
    public decimal SharePercent { get; set; }
}

public sealed class CityCompetitorEntry
{
    public Guid CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public bool IsNpc { get; set; }
    public Guid? NpcCompanyId { get; set; }
    public string? Archetype { get; set; }
    public int BuildingCount { get; set; }
    public decimal EstimatedRevenueLastTicks { get; set; }
    public decimal MarketSharePercent { get; set; }
    public List<CompetitorMarketShareByCategory> MarketShareByCategory { get; set; } = [];
    public string Trend { get; set; } = "STABLE";
}
