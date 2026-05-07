using Api.Data;
using Api.Data.Entities;
using Api.Security;
using HotChocolate.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Api.Types;

/// <summary>
/// GraphQL mutations for the player achievement badge and statistics export features.
/// </summary>
public sealed partial class Mutation
{
    /// <summary>
    /// Unlocks an achievement badge for a player.
    /// Admin-only. Idempotent: calling the same (playerId, badgeType) twice returns the existing badge.
    /// </summary>
    [Authorize]
    public async Task<PlayerBadgeResult> UnlockPlayerBadge(
        UnlockPlayerBadgeInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var currentUser = httpContextAccessor.HttpContext!.User;

        // Only admins may unlock badges directly.
        var actingPlayer = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == currentUser.GetRequiredUserId(), ct);
        if (actingPlayer is null || actingPlayer.Role != PlayerRole.Admin)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Admin role required to unlock badges.")
                    .SetCode("FORBIDDEN")
                    .Build());
        }

        // Validate badge type.
        if (!Data.Entities.BadgeType.All.Contains(input.BadgeType))
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage($"Unknown badge type: {input.BadgeType}")
                    .SetCode("INVALID_BADGE_TYPE")
                    .Build());
        }

        // Validate target player exists.
        var targetPlayer = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == input.PlayerId, ct);
        if (targetPlayer is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        }

        // Idempotent: return existing badge if already unlocked.
        var existing = await db.PlayerAchievementBadges
            .FirstOrDefaultAsync(b => b.PlayerId == input.PlayerId && b.BadgeType == input.BadgeType, ct);

        if (existing is not null)
        {
            return MapBadgeToResult(existing);
        }

        // Load current tick for timestamp.
        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(ct);
        var currentTick = gameState?.CurrentTick ?? 0L;

        var badge = new PlayerAchievementBadge
        {
            Id = Guid.NewGuid(),
            PlayerId = input.PlayerId,
            BadgeType = input.BadgeType,
            UnlockedAtUtc = DateTime.UtcNow,
            UnlockedAtTick = currentTick,
        };

        db.PlayerAchievementBadges.Add(badge);

        // Emit in-game notification so the player sees the badge unlock in the bell panel.
        db.PlayerNotifications.Add(new PlayerNotification
        {
            Id = Guid.NewGuid(),
            PlayerId = input.PlayerId,
            Type = PlayerNotificationType.Generic,
            Title = "Achievement Unlocked!",
            Message = $"You earned the \"{input.BadgeType.Replace('_', ' ')}\" badge. {Data.Entities.BadgeType.GetUnlockCondition(input.BadgeType)}",
            CreatedAtTick = currentTick,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync(ct);
        return MapBadgeToResult(badge);
    }

    /// <summary>
    /// Alias for internal bounty hooks and automation paths.
    /// Keeps badge awarding idempotent through the same unlock workflow.
    /// </summary>
    [Authorize]
    public Task<PlayerBadgeResult> AwardProfileBadge(
        UnlockPlayerBadgeInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor) =>
        UnlockPlayerBadge(input, db, httpContextAccessor);

    /// <summary>
    /// Generates a statistics export for the player (CSV text or HTML report).
    /// Players can only export their own stats; admins can export any player's stats.
    /// </summary>
    [Authorize]
    public async Task<StatsExportResult> GenerateStatsExport(
        GenerateStatsExportInput input,
        [Service] AppDbContext db,
        [Service] IHttpContextAccessor httpContextAccessor)
    {
        var ct = httpContextAccessor.HttpContext!.RequestAborted;
        var userId = httpContextAccessor.HttpContext!.User.GetRequiredUserId();

        // Auth guard: players can only export their own stats.
        var actingPlayer = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == userId, ct);
        var targetPlayerId = input.PlayerId ?? userId;

        if (targetPlayerId != userId)
        {
            if (actingPlayer?.Role != PlayerRole.Admin)
            {
                throw new GraphQLException(
                    ErrorBuilder.New()
                        .SetMessage("You can only export your own stats. Admin role required to export other players' stats.")
                        .SetCode("FORBIDDEN")
                        .Build());
            }
        }

        // Load the target player.
        var targetPlayer = await db.Players.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == targetPlayerId, ct);
        if (targetPlayer is null)
        {
            throw new GraphQLException(
                ErrorBuilder.New()
                    .SetMessage("Player not found.")
                    .SetCode("PLAYER_NOT_FOUND")
                    .Build());
        }

        var gameState = await db.GameStates.AsNoTracking().FirstOrDefaultDeterministicAsync(ct);
        var currentTick = gameState?.CurrentTick ?? 0L;

        // Load rank history.
        var snapshots = await db.PlayerRankSnapshots
            .AsNoTracking()
            .Where(s => s.PlayerId == targetPlayerId)
            .OrderBy(s => s.SnapshotTick)
            .ToListAsync(ct);

        // Load badges.
        var badges = await db.PlayerAchievementBadges
            .AsNoTracking()
            .Where(b => b.PlayerId == targetPlayerId)
            .OrderBy(b => b.UnlockedAtUtc)
            .ToListAsync(ct);

        // Load companies.
        var companies = await db.Companies
            .AsNoTracking()
            .Include(c => c.BankAccounts)
            .Include(c => c.Buildings)
            .Where(c => c.PlayerId == targetPlayerId)
            .AsSplitQuery()
            .ToListAsync(ct);

        var dateStr = DateTime.UtcNow.ToString("yyyy-MM-dd");

        if (input.Format == "CSV")
        {
            var csv = BuildCsvExport(targetPlayer, companies, snapshots, badges, currentTick);
            return new StatsExportResult
            {
                Format = "CSV",
                FileName = $"{SanitizeFileName(targetPlayer.DisplayName)}_Stats_{dateStr}.csv",
                ContentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(csv)),
            };
        }
        else
        {
            var html = BuildHtmlExport(targetPlayer, companies, snapshots, badges, currentTick);
            return new StatsExportResult
            {
                Format = "HTML",
                FileName = $"{SanitizeFileName(targetPlayer.DisplayName)}_Stats_{dateStr}.html",
                ContentBase64 = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(html)),
            };
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static PlayerBadgeResult MapBadgeToResult(PlayerAchievementBadge badge) =>
        new()
        {
            Id = badge.Id,
            PlayerId = badge.PlayerId,
            BadgeType = badge.BadgeType,
            Rarity = Data.Entities.BadgeType.GetRarity(badge.BadgeType),
            UnlockCondition = Data.Entities.BadgeType.GetUnlockCondition(badge.BadgeType),
            UnlockedAtUtc = badge.UnlockedAtUtc,
            UnlockedAtTick = badge.UnlockedAtTick,
        };

    private static string SanitizeFileName(string name) =>
        new string(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_').ToArray());

    private static string BuildCsvExport(
        Player player,
        List<Company> companies,
        List<PlayerRankSnapshot> snapshots,
        List<PlayerAchievementBadge> badges,
        long currentTick)
    {
        var sb = new System.Text.StringBuilder();

        // Sheet 1: OverallStats
        sb.AppendLine("=== OverallStats ===");
        sb.AppendLine("Name,JoinDate,CurrentTick,CompanyCount,BadgeCount");
        sb.AppendLine($"\"{player.DisplayName}\",\"{player.CreatedAtUtc:yyyy-MM-dd}\",{currentTick},{companies.Count},{badges.Count}");
        sb.AppendLine();

        // Sheet 2: CompanyMetrics
        sb.AppendLine("=== CompanyMetrics ===");
        sb.AppendLine("CompanyName,BuildingCount,TotalCashEur");
        foreach (var company in companies)
        {
            var cash = company.BankAccounts.Where(a => a.ClosedAtUtc == null).Sum(a => a.Balance);
            sb.AppendLine($"\"{company.Name}\",{company.Buildings.Count},{cash:F2}");
        }
        sb.AppendLine();

        // Sheet 3: RankHistory
        sb.AppendLine("=== RankHistory ===");
        sb.AppendLine("SnapshotTick,SnapshotDate,LeaderboardRank,WealthUsd,PercentileRank,PositionChange");
        foreach (var s in snapshots)
        {
            sb.AppendLine($"{s.SnapshotTick},{s.SnapshotUtc:yyyy-MM-dd},{s.LeaderboardRank},{s.WealthUsd:F2},{s.PercentileRank:F1},{s.PositionChange?.ToString() ?? ""}");
        }
        sb.AppendLine();

        // Sheet 4: Badges
        sb.AppendLine("=== Badges ===");
        sb.AppendLine("BadgeType,Rarity,UnlockedDate,UnlockedTick");
        foreach (var b in badges)
        {
            sb.AppendLine($"{b.BadgeType},{Data.Entities.BadgeType.GetRarity(b.BadgeType)},{b.UnlockedAtUtc:yyyy-MM-dd},{b.UnlockedAtTick}");
        }

        return sb.ToString();
    }

    private static string BuildHtmlExport(
        Player player,
        List<Company> companies,
        List<PlayerRankSnapshot> snapshots,
        List<PlayerAchievementBadge> badges,
        long currentTick)
    {
        var bestRank = snapshots.Count > 0 ? snapshots.Min(s => s.LeaderboardRank) : 0;
        var currentRank = snapshots.Count > 0 ? snapshots.OrderByDescending(s => s.SnapshotTick).First().LeaderboardRank : 0;
        var avgPercentile = snapshots.Count > 0 ? snapshots.Average(s => (double)s.PercentileRank) : 0.0;

        var companyRows = string.Concat(companies.Select(c =>
        {
            var cash = c.BankAccounts.Where(a => a.ClosedAtUtc == null).Sum(a => a.Balance);
            return $"<tr><td>{System.Web.HttpUtility.HtmlEncode(c.Name)}</td><td>{c.Buildings.Count}</td><td>€{cash:N0}</td></tr>";
        }));

        var badgeRows = string.Concat(badges.Select(b =>
            $"<tr><td>{b.BadgeType}</td><td>{Data.Entities.BadgeType.GetRarity(b.BadgeType)}</td><td>{b.UnlockedAtUtc:yyyy-MM-dd}</td></tr>"));

        var rankRows = string.Concat(snapshots.TakeLast(30).Select(s =>
            $"<tr><td>{s.SnapshotTick}</td><td>#{s.LeaderboardRank}</td><td>${s.WealthUsd:N0}</td><td>{s.PercentileRank:F1}%</td></tr>"));

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Stats Export – {{System.Web.HttpUtility.HtmlEncode(player.DisplayName)}}</title>
  <style>
    body { font-family: sans-serif; color: #111; max-width: 860px; margin: 40px auto; }
    h1 { color: #004aff; }
    h2 { border-bottom: 2px solid #eee; padding-bottom: 6px; }
    table { border-collapse: collapse; width: 100%; margin-bottom: 24px; }
    th { background: #f0f0f0; text-align: left; padding: 8px; }
    td { padding: 8px; border-bottom: 1px solid #eee; }
    .kpi { display: inline-block; background: #f8f8f8; border: 1px solid #ddd; padding: 12px 20px; border-radius: 8px; margin: 8px; text-align: center; }
    .kpi span { font-size: 24px; font-weight: bold; display: block; }
  </style>
</head>
<body>
  <h1>📊 {{System.Web.HttpUtility.HtmlEncode(player.DisplayName)}} — Stats Report</h1>
  <p>Generated: {{DateTime.UtcNow:yyyy-MM-dd HH:mm}} UTC | Game Tick: {{currentTick:N0}}</p>

  <h2>Performance Summary</h2>
  <div>
    <div class="kpi"><span>#{{(bestRank > 0 ? bestRank : "—")}}</span>Best Rank</div>
    <div class="kpi"><span>#{{(currentRank > 0 ? currentRank : "—")}}</span>Current Rank</div>
    <div class="kpi"><span>{{avgPercentile:F1}}%</span>Avg Percentile</div>
    <div class="kpi"><span>{{badges.Count}}</span>Badges Earned</div>
  </div>

  <h2>Companies</h2>
  <table>
    <tr><th>Company</th><th>Buildings</th><th>Cash Balance</th></tr>
    {{(string.IsNullOrEmpty(companyRows) ? "<tr><td colspan='3'>No companies</td></tr>" : companyRows)}}
  </table>

  <h2>Achievement Badges</h2>
  <table>
    <tr><th>Badge</th><th>Rarity</th><th>Unlocked</th></tr>
    {{(string.IsNullOrEmpty(badgeRows) ? "<tr><td colspan='3'>No badges yet</td></tr>" : badgeRows)}}
  </table>

  <h2>Recent Rank History (last 30 snapshots)</h2>
  <table>
    <tr><th>Tick</th><th>Rank</th><th>Wealth (USD)</th><th>Percentile</th></tr>
    {{(string.IsNullOrEmpty(rankRows) ? "<tr><td colspan='4'>No rank history yet</td></tr>" : rankRows)}}
  </table>
</body>
</html>
""";
    }
}

// ── Input / Output types ──────────────────────────────────────────────────────

/// <summary>Input for the unlockPlayerBadge mutation.</summary>
public sealed class UnlockPlayerBadgeInput
{
    /// <summary>The player to award the badge to.</summary>
    public Guid PlayerId { get; set; }

    /// <summary>The badge type to unlock (e.g. FIRST_MILLION).</summary>
    public string BadgeType { get; set; } = string.Empty;
}

/// <summary>Input for the generateStatsExport mutation.</summary>
public sealed class GenerateStatsExportInput
{
    /// <summary>Target player ID. Defaults to the authenticated player.</summary>
    public Guid? PlayerId { get; set; }

    /// <summary>Export format: "CSV" (comma-separated text) or "HTML" (styled HTML report). Defaults to CSV.</summary>
    public string Format { get; set; } = "CSV";
}

/// <summary>Result of the generateStatsExport mutation.</summary>
public sealed class StatsExportResult
{
    /// <summary>The format of the export ("CSV" or "HTML").</summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>Suggested file name for the download.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Base64-encoded content of the exported file.</summary>
    public string ContentBase64 { get; set; } = string.Empty;
}
