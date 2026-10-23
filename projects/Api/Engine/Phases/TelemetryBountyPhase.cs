using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Economy;

namespace Api.Engine.Phases;

/// <summary>
/// Fires telemetry bounty events to the MasterApi ranking system at the end of each tick.
/// Detects player activity (manufacturing, sales, research, etc.) and reports events so
/// players earn master ranking points for economic participation.
/// This phase runs after DividendPhase so it can also detect dividend payouts.
///
/// Runs every <see cref="TickCadence.TelemetryBountyTicks"/> ticks (#646). Bounties are one per player per UTC day, so the delay is
/// harmless, and the event-based checks (public sales, dividends) look back over the ticks since the previous run so an event on a skipped
/// tick still counts. Checks of standing state (research budgets, occupied buildings, plants, banks, lenders, employers, rankings) read
/// the current state, which does not depend on the cadence. Manufacturing is read from the current tick's output only: a factory that
/// produces on any run tick of the day earns the bounty.
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
        if (!TickCadence.IsDue(context.CurrentTick, TickCadence.TelemetryBountyTicks))
        {
            return;
        }

        var windowStartTick = context.CurrentTick - TickCadence.TelemetryBountyTicks; // exclusive
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
        foreach (var entry in context.Db.ChangeTracker.TrackedEntriesWithoutDetection<BuildingUnitResourceHistory>())
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
        // Companies that made any public retail sales since the previous run.
        var wholesalerCompanyIds = context.Db.PublicSalesRecords
            .Where(r => r.Tick > windowStartTick && r.Tick <= context.CurrentTick && r.QuantitySold > 0)
            .Select(r => r.CompanyId)
            .Distinct()
            .ToHashSet();

        // PublicSalesPhase writes records in this tick before SaveChanges.
        // Include added/modified tracked entries so wholesaler bounty can trigger immediately.
        foreach (var entry in context.Db.ChangeTracker.TrackedEntriesWithoutDetection<PublicSalesRecord>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var record = entry.Entity;
            if (record.Tick > windowStartTick && record.Tick <= context.CurrentTick && record.QuantitySold > 0)
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
        // The best-paying real employer of each city (one winner per city). "Real" means a building that actually hires there
        // (units plus power) and no strike in the company's buildings in that city; a salary setting alone proves nothing.
        // "Good" means the pay clearly beats the company's rivals and the baseline wage (LaborMarketCalculator.IsGoodEmployer).
        var cityBuildings = context.BuildingsById.Values.GroupBy(building => building.CityId).ToList();
        var employingCompanyIdsByCity = cityBuildings.ToDictionary(
            group => group.Key,
            group =>
            {
                // A company counts as an employer in a city when at least one building really hires there and none of its
                // buildings in that city is on strike.
                var striking = group.Where(context.IsOnStrike).Select(building => building.CompanyId).ToHashSet();
                return group
                    .Where(building => context.EmploysWorkers(building) && !striking.Contains(building.CompanyId))
                    .Select(building => building.CompanyId)
                    .ToHashSet();
            });

        var salaryByCityCompany = context.CitySalarySettingsByCompany
            .SelectMany(kv => kv.Value.Select(s => (CompanyId: kv.Key, Setting: s)))
            .GroupBy(x => x.Setting.CityId)
            .ToList();

        foreach (var cityGroup in salaryByCityCompany)
        {
            employingCompanyIdsByCity.TryGetValue(cityGroup.Key, out var employingCompanyIds);
            var competitiveEntries = cityGroup
                .Where(x => IsCompetitiveCompany(x.CompanyId) && employingCompanyIds?.Contains(x.CompanyId) == true)
                .ToList();

            if (competitiveEntries.Count == 0)
            {
                continue;
            }

            // Highest pay wins; ties break on company id so the winner never depends on enumeration order.
            var bestEntry = competitiveEntries
                .OrderByDescending(x => x.Setting.SalaryMultiplier)
                .ThenBy(x => x.CompanyId)
                .First();

            // The bar is set by the winner's real rivals, not by an average that includes the winner itself.
            var rivals = competitiveEntries.Where(x => x.CompanyId != bestEntry.CompanyId).ToList();
            var rivalsAverage = rivals.Count == 0 ? 1m : rivals.Average(x => x.Setting.SalaryMultiplier);

            if (LaborMarketCalculator.IsGoodEmployer(bestEntry.Setting.SalaryMultiplier, rivalsAverage)
                && playerIdByCompanyId.TryGetValue(bestEntry.CompanyId, out var playerId)
                && playerEmailByCompanyId.TryGetValue(bestEntry.CompanyId, out var email)
                && email is not null)
            {
                QueueBounty(MasterRankingBountyCodes.GoodEmployer, playerId, email);
            }
        }

        // ── DIVIDENDS_MASTER ────────────────────────────────────────────────────
        // Companies that paid dividends since the previous run.
        var dividendCompanyIds = context.Db.LedgerEntries
            .Where(e => e.RecordedAtTick > windowStartTick
                && e.RecordedAtTick <= context.CurrentTick
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
