using Api.Configuration;
using Api.Data;
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
    private static readonly TimeSpan MaxTelemetryDispatchWait = TimeSpan.FromMilliseconds(250);

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
        var playerIdByCompanyId = context.CompaniesById
            .ToDictionary(kv => kv.Key, kv => kv.Value.PlayerId);
        var governmentCompanyIds = context.CompaniesById.Values
            .Where(company => playersById.TryGetValue(company.PlayerId, out var email)
                && string.Equals(email, GovernmentActorConstants.GovernmentEmail, StringComparison.OrdinalIgnoreCase))
            .Select(company => company.Id)
            .ToHashSet();

        var queuedTelemetryByEmail = new Dictionary<string, List<(string BountyCode, string ScopeKey)>>(StringComparer.OrdinalIgnoreCase);
        var queuedScopeKeys = new HashSet<string>(StringComparer.Ordinal);
        var pendingBadgesByPlayer = new Dictionary<Guid, HashSet<string>>();

        bool IsCompetitiveCompany(Guid companyId) => !governmentCompanyIds.Contains(companyId);

        void QueueBounty(string bountyCode, Guid playerId, string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return;
            }

            var scopeKey = $"{bountyCode}:{email}:{today}:{serverKey}";
            if (!queuedScopeKeys.Add(scopeKey))
            {
                return;
            }

            if (!queuedTelemetryByEmail.TryGetValue(email, out var playerTelemetry))
            {
                playerTelemetry = [];
                queuedTelemetryByEmail[email] = playerTelemetry;
            }

            playerTelemetry.Add((bountyCode, scopeKey));

            var badgeType = BadgeType.FromBountyCode(bountyCode);
            if (badgeType is null)
            {
                return;
            }

            if (!pendingBadgesByPlayer.TryGetValue(playerId, out var playerBadges))
            {
                playerBadges = new HashSet<string>(StringComparer.Ordinal);
                pendingBadgesByPlayer[playerId] = playerBadges;
            }
            playerBadges.Add(badgeType);
        }

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
            if (!IsCompetitiveCompany(companyId))
            {
                continue;
            }

            if (playerIdByCompanyId.TryGetValue(companyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(companyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.Manufacturer, playerId, email);
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
            if (!IsCompetitiveCompany(companyId))
            {
                continue;
            }

            if (playerIdByCompanyId.TryGetValue(companyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(companyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.Wholesaler, playerId, email);
            }
        }

        // ── RESEARCHER ──────────────────────────────────────────────────────────
        // Companies with active R&D budgets.
        var researcherCompanyIds = context.ResearchBudgetsByKey.Keys
            .Select(k => k.CompanyId)
            .ToHashSet();

        foreach (var companyId in researcherCompanyIds)
        {
            if (!IsCompetitiveCompany(companyId))
            {
                continue;
            }

            if (playerIdByCompanyId.TryGetValue(companyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(companyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.Researcher, playerId, email);
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
                    && IsCompetitiveCompany(building.CompanyId)
                    && playerIdByCompanyId.TryGetValue(building.CompanyId, out var playerId)
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    QueueBounty(MasterRankingBountyCodes.RealEstateMagnate, playerId, email);
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
                    && IsCompetitiveCompany(building.CompanyId)
                    && playerIdByCompanyId.TryGetValue(building.CompanyId, out var playerId)
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    QueueBounty(MasterRankingBountyCodes.MediaOwner, playerId, email);
                }
            }
        }

        // ── ENERGY_TRADER ───────────────────────────────────────────────────────
        // Companies that generated power via their power plant this tick.
        foreach (var (plantBuildingId, outputMw) in context.PlantEffectiveOutputMwById)
        {
            if (outputMw > 0
                && context.BuildingsById.TryGetValue(plantBuildingId, out var plantBuilding)
                && IsCompetitiveCompany(plantBuilding.CompanyId)
                && playerIdByCompanyId.TryGetValue(plantBuilding.CompanyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(plantBuilding.CompanyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.EnergyTrader, playerId, email);
            }
        }

        // ── BANKER ──────────────────────────────────────────────────────────────
        // Companies that own a bank building with external deposits (accounts from other companies).
        if (context.BuildingsByType.TryGetValue(BuildingType.Bank, out var bankBuildings))
        {
            // External accounts = accounts linked to this bank building but owned by a different company.
            var bankBuildingIds = bankBuildings
                .Select(building => building.Id)
                .ToHashSet();
            var externalDepositBankIds = await context.Db.BankAccounts
                .Where(a => a.BankBuildingId.HasValue
                    && bankBuildingIds.Contains(a.BankBuildingId.Value)
                    && a.Balance > 0
                    && (a.CompanyId.HasValue || a.PlayerId.HasValue))
                .Join(
                    context.Db.Buildings.AsNoTracking(),
                    account => account.BankBuildingId!.Value,
                    bankBuilding => bankBuilding.Id,
                    (account, bankBuilding) => new
                    {
                        BankBuildingId = account.BankBuildingId!.Value,
                        account.CompanyId,
                        account.PlayerId,
                        BankOwnerCompanyId = bankBuilding.CompanyId,
                    })
                .Where(item => item.PlayerId.HasValue
                    || (item.CompanyId.HasValue && item.CompanyId.Value != item.BankOwnerCompanyId))
                .Select(item => item.BankBuildingId)
                .Distinct()
                .ToHashSetAsync();

            foreach (var building in bankBuildings)
            {
                if (externalDepositBankIds.Contains(building.Id)
                    && IsCompetitiveCompany(building.CompanyId)
                    && playerIdByCompanyId.TryGetValue(building.CompanyId, out var playerId)
                    && playerEmailByCompanyId.TryGetValue(building.CompanyId, out var email)
                    && email is not null)
                {
                    QueueBounty(MasterRankingBountyCodes.Banker, playerId, email);
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
                && IsCompetitiveCompany(lenderBuilding.CompanyId)
                && playerIdByCompanyId.TryGetValue(lenderBuilding.CompanyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(lenderBuilding.CompanyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.Lender, playerId, email);
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
            var competitiveEntries = cityGroup
                .Where(x => IsCompetitiveCompany(x.CompanyId))
                .ToList();

            if (competitiveEntries.Count == 0)
            {
                continue;
            }

            var bestEntry = competitiveEntries
                .MaxBy(x => x.Setting.SalaryMultiplier);

            if (bestEntry.Setting.SalaryMultiplier > 1m
                && playerIdByCompanyId.TryGetValue(bestEntry.CompanyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(bestEntry.CompanyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.GoodEmployer, playerId, email);
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
            if (!IsCompetitiveCompany(companyId))
            {
                continue;
            }

            if (playerIdByCompanyId.TryGetValue(companyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(companyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.DividendsMaster, playerId, email);
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
            .Where(x => x.TotalBalance > 0 && IsCompetitiveCompany(x.Company.Id))
            .OrderByDescending(x => x.TotalBalance)
            .Take(10)
            .ToList();

        foreach (var entry in top10Companies)
        {
            if (playerIdByCompanyId.TryGetValue(entry.Company.Id, out var playerId)
                && playerEmailByCompanyId.TryGetValue(entry.Company.Id, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.CompanyMaster, playerId, email);
            }
        }

        if (pendingBadgesByPlayer.Count > 0)
        {
            var playerIds = pendingBadgesByPlayer.Keys.ToList();
            var existingBadges = await context.Db.PlayerAchievementBadges
                .Where(b => playerIds.Contains(b.PlayerId))
                .Select(b => new { b.PlayerId, b.BadgeType })
                .ToListAsync();

            var existingSet = existingBadges
                .ToHashSet();

            foreach (var (playerId, badgeTypes) in pendingBadgesByPlayer)
            {
                foreach (var badgeType in badgeTypes)
                {
                    if (existingSet.Contains(new { PlayerId = playerId, BadgeType = badgeType }))
                    {
                        continue;
                    }

                    context.Db.PlayerAchievementBadges.Add(new PlayerAchievementBadge
                    {
                        Id = Guid.NewGuid(),
                        PlayerId = playerId,
                        BadgeType = badgeType,
                        UnlockedAtUtc = DateTime.UtcNow,
                        UnlockedAtTick = context.CurrentTick,
                    });
                }
            }
        }

        // Bound telemetry dispatch time so master-server latency never stalls the tick.
        if (queuedTelemetryByEmail.Count > 0)
        {
            using var dispatchCancellation = new CancellationTokenSource(MaxTelemetryDispatchWait);
            var dispatchTasks = queuedTelemetryByEmail
                .Select(kvp => DispatchTelemetryForEmailAsync(
                    telemetry,
                    kvp.Key,
                    kvp.Value,
                    dispatchCancellation.Token))
                .ToList();

            await Task.WhenAll(dispatchTasks);
        }
    }

    private static async Task DispatchTelemetryForEmailAsync(
        IMasterRankingTelemetryService telemetry,
        string email,
        IReadOnlyList<(string BountyCode, string ScopeKey)> queuedTelemetry,
        CancellationToken cancellationToken)
    {
        foreach (var (bountyCode, scopeKey) in queuedTelemetry)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await telemetry.ReportEventAsync(
                    bountyCode,
                    email,
                    uniqueScopeKey: scopeKey,
                    cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
