using Api.Configuration;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Engine.Phases;

/// <summary>
/// Fires telemetry bounty events to the MasterApi ranking system at the end of each tick.
/// Detects player activity (manufacturing, sales, research, etc.) and reports events so
/// players earn master ranking points for economic participation.
/// This phase runs after DividendPhase so it can also detect dividend payouts.
/// </summary>
public sealed class TelemetryBountyPhase(
    IMasterRankingTelemetryService telemetry,
    IOptions<MasterServerRegistrationOptions> masterOptions) : ITickPhase
{
    public string Name => "TelemetryBounty";
    public int Order => 1050; // After DividendPhase (1010) and before MarketReportPhase

    public async Task ProcessAsync(TickContext context)
    {
        var serverKey = masterOptions.Value.ServerKey ?? string.Empty;
        var today = DateTime.UtcNow.ToString("yyyyMMdd");

        // Load player emails for company ownership lookup.
        var playersById = context.Db.Players
            .ToDictionary(p => p.Id, p => p.Email);

        var playerEmailByCompanyId = context.CompaniesById
            .ToDictionary(
                kv => kv.Key,
                kv => playersById.TryGetValue(kv.Value.PlayerId, out var email) ? email : null);

        var tasks = new List<Task>();

        // ── MANUFACTURER ────────────────────────────────────────────────────────
        // Companies that had any manufacturing output this tick.
        var manufacturerCompanyIds = context.NewUnitResourceHistories
            .Where(h => h.ProducedQuantity > 0)
            .Select(h => context.BuildingsById.TryGetValue(h.BuildingId, out var building) ? building : null)
            .Where(building => building is not null && building.Type == BuildingType.Factory)
            .Select(building => building!.CompanyId)
            .ToHashSet();

        // Include tracked-but-not-yet-saved history entries from this tick.
        foreach (var entry in context.Db.ChangeTracker.Entries<BuildingUnitResourceHistory>())
        {
            var history = entry.Entity;
            if (history.ProducedQuantity <= 0 || history.Tick != context.CurrentTick)
            {
                continue;
            }

            if (context.BuildingsById.TryGetValue(history.BuildingId, out var building)
                && building.Type == BuildingType.Factory)
            {
                manufacturerCompanyIds.Add(building.CompanyId);
            }
        }

        foreach (var companyId in manufacturerCompanyIds)
        {
            if (playerEmailByCompanyId.TryGetValue(companyId, out var email) && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.Manufacturer,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.Manufacturer}:{email}:{today}:{serverKey}"));
            }
        }

        // ── WHOLESALER ──────────────────────────────────────────────────────────
        // Companies that made any public retail sales this tick.
        var wholesalerCompanyIds = context.Db.PublicSalesRecords
            .Where(r => r.Tick == context.CurrentTick && r.QuantitySold > 0)
            .Select(r => r.CompanyId)
            .Distinct()
            .ToHashSet();

        // PublicSalesPhase writes records in this tick before SaveChanges.
        // Include added/modified tracked entries so wholesaler bounty can trigger immediately.
        foreach (var entry in context.Db.ChangeTracker.Entries<PublicSalesRecord>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var record = entry.Entity;
            if (record.Tick == context.CurrentTick && record.QuantitySold > 0)
            {
                wholesalerCompanyIds.Add(record.CompanyId);
            }
        }

        foreach (var companyId in wholesalerCompanyIds)
        {
            if (playerEmailByCompanyId.TryGetValue(companyId, out var email) && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.Wholesaler,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.Wholesaler}:{email}:{today}:{serverKey}"));
            }
        }

        // ── RESEARCHER ──────────────────────────────────────────────────────────
        // Companies with active R&D budgets.
        var researcherCompanyIds = context.ResearchBudgetsByKey.Keys
            .Select(k => k.CompanyId)
            .ToHashSet();

        foreach (var companyId in researcherCompanyIds)
        {
            if (playerEmailByCompanyId.TryGetValue(companyId, out var email) && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.Researcher,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.Researcher}:{email}:{today}:{serverKey}"));
            }
        }

        // ── REAL_ESTATE_MAGNATE ─────────────────────────────────────────────────
        // Companies with occupied apartment or commercial buildings.
        var realEstateTypes = new[] { BuildingType.Apartment, BuildingType.Commercial };
        foreach (var buildingType in realEstateTypes)
        {
            if (!context.BuildingsByType.TryGetValue(buildingType, out var buildings))
            {
                continue;
            }

            foreach (var building in buildings)
            {
                if (building.OccupancyPercent is > 0
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    tasks.Add(telemetry.ReportEventAsync(
                        MasterRankingBountyCodes.RealEstateMagnate,
                        email,
                        uniqueScopeKey: $"{MasterRankingBountyCodes.RealEstateMagnate}:{email}:{today}:{serverKey}"));
                }
            }
        }

        // ── MEDIA_OWNER ─────────────────────────────────────────────────────────
        // Companies operating a media house with active content budget.
        if (context.BuildingsByType.TryGetValue(BuildingType.MediaHouse, out var mediaBuildings))
        {
            foreach (var building in mediaBuildings)
            {
                if (building.ContentBudgetPerTick is > 0
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    tasks.Add(telemetry.ReportEventAsync(
                        MasterRankingBountyCodes.MediaOwner,
                        email,
                        uniqueScopeKey: $"{MasterRankingBountyCodes.MediaOwner}:{email}:{today}:{serverKey}"));
                }
            }
        }

        // ── ENERGY_TRADER ───────────────────────────────────────────────────────
        // Companies that generated power via their power plant this tick.
        foreach (var (plantBuildingId, outputMw) in context.PlantEffectiveOutputMwById)
        {
            if (outputMw > 0
                && context.BuildingsById.TryGetValue(plantBuildingId, out var plantBuilding)
                && playerEmailByCompanyId.TryGetValue(plantBuilding.CompanyId, out var email)
                && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.EnergyTrader,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.EnergyTrader}:{email}:{today}:{serverKey}"));
            }
        }

        // ── BANKER ──────────────────────────────────────────────────────────────
        // Companies that own a bank building with external deposits (accounts from other companies).
        if (context.BuildingsByType.TryGetValue(BuildingType.Bank, out var bankBuildings))
        {
            // External accounts = accounts linked to this bank building but owned by a different company.
            var externalDepositBankIds = context.Db.BankAccounts
                .Where(a => a.BankBuildingId.HasValue
                    && a.Balance > 0
                    && (a.CompanyId.HasValue || a.PlayerId.HasValue))
                .Select(a => a.BankBuildingId!.Value)
                .Distinct()
                .ToHashSet();

            foreach (var building in bankBuildings)
            {
                if (externalDepositBankIds.Contains(building.Id)
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    tasks.Add(telemetry.ReportEventAsync(
                        MasterRankingBountyCodes.Banker,
                        email,
                        uniqueScopeKey: $"{MasterRankingBountyCodes.Banker}:{email}:{today}:{serverKey}"));
                }
            }
        }

        // ── LENDER ──────────────────────────────────────────────────────────────
        // Companies whose bank buildings have active outstanding loans.
        var lenderBuildingIds = context.Db.Loans
            .Where(l => l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue)
            .Select(l => l.BankBuildingId)
            .Distinct()
            .ToHashSet();

        foreach (var buildingId in lenderBuildingIds)
        {
            if (context.BuildingsById.TryGetValue(buildingId, out var lenderBuilding)
                && playerEmailByCompanyId.TryGetValue(lenderBuilding.CompanyId, out var email)
                && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.Lender,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.Lender}:{email}:{today}:{serverKey}"));
            }
        }

        // ── GOOD_EMPLOYER ───────────────────────────────────────────────────────
        // Company with the highest average wage per city (one winner per city).
        var salaryByCityCompany = context.CitySalarySettingsByCompany
            .SelectMany(kv => kv.Value.Select(s => (CompanyId: kv.Key, Setting: s)))
            .GroupBy(x => x.Setting.CityId)
            .ToList();

        foreach (var cityGroup in salaryByCityCompany)
        {
            var bestEntry = cityGroup
                .MaxBy(x => x.Setting.SalaryMultiplier);

            if (bestEntry.Setting.SalaryMultiplier > 1m
                && playerEmailByCompanyId.TryGetValue(bestEntry.CompanyId, out var email)
                && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.GoodEmployer,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.GoodEmployer}:{email}:{today}:{serverKey}"));
            }
        }

        // ── DIVIDENDS_MASTER ────────────────────────────────────────────────────
        // Companies that paid dividends this tick.
        var dividendCompanyIds = context.Db.LedgerEntries
            .Where(e => e.RecordedAtTick == context.CurrentTick
                && e.Category == LedgerCategory.Dividend
                && e.Amount < 0) // Dividend distributions are debits on the company ledger
            .Select(e => e.CompanyId)
            .Distinct()
            .ToList();

        foreach (var companyId in dividendCompanyIds)
        {
            if (playerEmailByCompanyId.TryGetValue(companyId, out var email) && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.DividendsMaster,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.DividendsMaster}:{email}:{today}:{serverKey}"));
            }
        }

        // ── COMPANY_MASTER ──────────────────────────────────────────────────────
        // Top 10 companies by total bank balance (approximate ranking signal).
        var top10Companies = context.CompaniesById.Values
            .Select(c => new
            {
                Company = c,
                TotalBalance = context.BankAccountsById.Values
                    .Where(a => a.CompanyId == c.Id)
                    .Sum(a => a.Balance)
            })
            .Where(x => x.TotalBalance > 0)
            .OrderByDescending(x => x.TotalBalance)
            .Take(10)
            .ToList();

        foreach (var entry in top10Companies)
        {
            if (playerEmailByCompanyId.TryGetValue(entry.Company.Id, out var email) && email is not null)
            {
                tasks.Add(telemetry.ReportEventAsync(
                    MasterRankingBountyCodes.CompanyMaster,
                    email,
                    uniqueScopeKey: $"{MasterRankingBountyCodes.CompanyMaster}:{email}:{today}:{serverKey}"));
            }
        }

        // Fire all telemetry events (failures are swallowed inside the service).
        if (tasks.Count > 0)
        {
            await Task.WhenAll(tasks);
        }
    }
}
