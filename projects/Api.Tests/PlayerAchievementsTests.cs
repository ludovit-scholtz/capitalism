using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for:
/// – PlayerAchievementBadge (unlock, duplicate idempotency, notification, admin-only guard)
/// – PlayerRankSnapshot (RankHistoryPhase, query, summary stats)
/// – GenerateStatsExport (CSV/HTML content, auth guard)
/// </summary>
public sealed class PlayerAchievementsTests
{
    // ── GraphQL helpers ──────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
            req.Headers.Authorization = new("Bearer", token);

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<(string token, string playerId)> RegisterAsync(
        HttpClient client, string email, string displayName = "Tester", bool admin = false)
    {
        var result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email, displayName, password = "TestPass123!" } });

        var data = result.GetProperty("data").GetProperty("register");
        var token = data.GetProperty("token").GetString()!;
        var id = data.GetProperty("player").GetProperty("id").GetString()!;

        if (admin)
        {
            // Elevate to admin in the database directly.
            using var scope = ((ApiWebApplicationFactory)null!).Services.CreateScope();
            // We can't easily do this without the factory, so admin is set in individual tests.
        }

        return (token, id);
    }

    // ── Tests: Badge unlock ──────────────────────────────────────────────────

    [Fact]
    public async Task UnlockPlayerBadge_AdminUnlocksValidBadge_CreatesBadgeAndNotification()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register a regular player.
        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "badges-player@test.com", displayName = "Badge Player", password = "TestPass123!" } });
        var playerToken = playerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var playerId = playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;

        // Register an admin and elevate in DB.
        var adminResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "badges-admin@test.com", displayName = "Admin User", password = "TestPass123!" } });
        var adminToken = adminResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var adminId = adminResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;

        // Elevate admin.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FindAsync(Guid.Parse(adminId));
            admin!.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // Unlock a badge as admin.
        var unlockResult = await ExecAsync(client,
            """
            mutation Unlock($i: UnlockPlayerBadgeInput!) {
              unlockPlayerBadge(input: $i) {
                id badgeType rarity unlockedAtTick
              }
            }
            """,
            new { i = new { playerId = Guid.Parse(playerId), badgeType = "FIRST_MILLION" } },
            adminToken);

        var badge = unlockResult.GetProperty("data").GetProperty("unlockPlayerBadge");
        Assert.Equal("FIRST_MILLION", badge.GetProperty("badgeType").GetString());
        Assert.Equal("COMMON", badge.GetProperty("rarity").GetString());

        // Verify badge is in the database.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var dbBadge = await db.PlayerAchievementBadges
                .FirstOrDefaultAsync(b => b.PlayerId == Guid.Parse(playerId) && b.BadgeType == "FIRST_MILLION");
            Assert.NotNull(dbBadge);

            // Verify notification was created.
            var notification = await db.PlayerNotifications
                .FirstOrDefaultAsync(n => n.PlayerId == Guid.Parse(playerId));
            Assert.NotNull(notification);
            Assert.Contains("Achievement Unlocked", notification.Title);
        }
    }

    [Fact]
    public async Task UnlockPlayerBadge_CalledTwice_IsIdempotent()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register player and admin.
        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "idem-player@test.com", displayName = "Idem Player", password = "TestPass123!" } });
        var playerId = playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;

        var adminResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "idem-admin@test.com", displayName = "Idem Admin", password = "TestPass123!" } });
        var adminId = adminResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;
        var adminToken = adminResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FindAsync(Guid.Parse(adminId));
            admin!.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        const string mutation = """
            mutation Unlock($i: UnlockPlayerBadgeInput!) {
              unlockPlayerBadge(input: $i) { id badgeType }
            }
            """;
        var input = new { i = new { playerId = Guid.Parse(playerId), badgeType = "CITY_PIONEER" } };

        // Unlock twice.
        var first = await ExecAsync(client, mutation, input, adminToken);
        var second = await ExecAsync(client, mutation, input, adminToken);

        // Both should succeed.
        Assert.Equal("CITY_PIONEER", first.GetProperty("data").GetProperty("unlockPlayerBadge").GetProperty("badgeType").GetString());
        Assert.Equal("CITY_PIONEER", second.GetProperty("data").GetProperty("unlockPlayerBadge").GetProperty("badgeType").GetString());

        // Only one badge record should exist.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var count = await db.PlayerAchievementBadges.CountAsync(b => b.PlayerId == Guid.Parse(playerId) && b.BadgeType == "CITY_PIONEER");
            Assert.Equal(1, count);
        }
    }

    [Fact]
    public async Task UnlockPlayerBadge_NonAdminPlayer_ReturnsForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "nonadmin-badge@test.com", displayName = "Non-Admin", password = "TestPass123!" } });
        var playerToken = playerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var playerId = playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;

        var result = await ExecAsync(client,
            """
            mutation Unlock($i: UnlockPlayerBadgeInput!) {
              unlockPlayerBadge(input: $i) { id }
            }
            """,
            new { i = new { playerId = Guid.Parse(playerId), badgeType = "FIRST_MILLION" } },
            playerToken);

        // Should have errors.
        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("FORBIDDEN", code);
    }

    [Fact]
    public async Task UnlockPlayerBadge_InvalidBadgeType_ReturnsInvalidBadgeType()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "invalid-badge-player@test.com", displayName = "Test Player", password = "TestPass123!" } });
        var playerId = playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;

        var adminResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "invalid-badge-admin@test.com", displayName = "Admin", password = "TestPass123!" } });
        var adminId = adminResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!;
        var adminToken = adminResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FindAsync(Guid.Parse(adminId));
            admin!.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            mutation Unlock($i: UnlockPlayerBadgeInput!) {
              unlockPlayerBadge(input: $i) { id }
            }
            """,
            new { i = new { playerId = Guid.Parse(playerId), badgeType = "NONEXISTENT_BADGE" } },
            adminToken);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_BADGE_TYPE", code);
    }

    // ── Tests: playerBadges query ────────────────────────────────────────────

    [Fact]
    public async Task PlayerBadges_QueryForPlayerWithBadges_ReturnsAll()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "query-badges@test.com", displayName = "Query Player", password = "TestPass123!" } });
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var adminResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "query-badges-admin@test.com", displayName = "Admin", password = "TestPass123!" } });
        var adminToken = adminResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var adminId = Guid.Parse(adminResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FindAsync(adminId);
            admin!.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        // Seed 3 badges directly.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PlayerAchievementBadges.AddRange(
                new PlayerAchievementBadge { Id = Guid.NewGuid(), PlayerId = playerId, BadgeType = "FIRST_MILLION", UnlockedAtTick = 100 },
                new PlayerAchievementBadge { Id = Guid.NewGuid(), PlayerId = playerId, BadgeType = "CITY_PIONEER", UnlockedAtTick = 200 },
                new PlayerAchievementBadge { Id = Guid.NewGuid(), PlayerId = playerId, BadgeType = "LEGENDARY_TYCOON", UnlockedAtTick = 300 });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            query Q($id: UUID!) {
              playerBadges(playerId: $id) {
                badgeType rarity unlockCondition unlockedAtTick
              }
            }
            """,
            new { id = playerId });

        var badges = result.GetProperty("data").GetProperty("playerBadges");
        Assert.Equal(3, badges.GetArrayLength());

        // Legendary badge should have LEGENDARY rarity.
        var legendary = badges.EnumerateArray().First(b => b.GetProperty("badgeType").GetString() == "LEGENDARY_TYCOON");
        Assert.Equal("LEGENDARY", legendary.GetProperty("rarity").GetString());
        Assert.False(string.IsNullOrEmpty(legendary.GetProperty("unlockCondition").GetString()));
    }

    [Fact]
    public async Task PlayerBadges_QueryForPlayerWithNoBadges_ReturnsEmpty()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "nobadges@test.com", displayName = "No Badges", password = "TestPass123!" } });
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var result = await ExecAsync(client,
            "query Q($id: UUID!) { playerBadges(playerId: $id) { badgeType } }",
            new { id = playerId });

        var badges = result.GetProperty("data").GetProperty("playerBadges");
        Assert.Equal(0, badges.GetArrayLength());
    }

    // ── Tests: playerRankHistory query ───────────────────────────────────────

    [Fact]
    public async Task PlayerRankHistory_WithSeedSnapshots_ReturnsChronologicalOrder()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "rank-history@test.com", displayName = "Rank Player", password = "TestPass123!" } });
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // Seed 3 snapshots.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.PlayerRankSnapshots.AddRange(
                new PlayerRankSnapshot { Id = Guid.NewGuid(), PlayerId = playerId, SnapshotTick = 1008, LeaderboardRank = 5, WealthUsd = 50_000m, PercentileRank = 70m },
                new PlayerRankSnapshot { Id = Guid.NewGuid(), PlayerId = playerId, SnapshotTick = 2016, LeaderboardRank = 3, WealthUsd = 120_000m, PercentileRank = 85m, PositionChange = 2 },
                new PlayerRankSnapshot { Id = Guid.NewGuid(), PlayerId = playerId, SnapshotTick = 3024, LeaderboardRank = 1, WealthUsd = 500_000m, PercentileRank = 100m, PositionChange = 2 });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            query Q($id: UUID!) {
              playerRankHistory(playerId: $id) {
                snapshotTick leaderboardRank wealthUsd percentileRank positionChange
              }
            }
            """,
            new { id = playerId });

        var snapshots = result.GetProperty("data").GetProperty("playerRankHistory");
        Assert.Equal(3, snapshots.GetArrayLength());

        // Should be in chronological order (oldest first).
        var ticks = snapshots.EnumerateArray().Select(s => s.GetProperty("snapshotTick").GetInt64()).ToList();
        Assert.Equal(new[] { 1008L, 2016L, 3024L }, ticks);

        // Best rank is 1 at tick 3024.
        var last = snapshots[2];
        Assert.Equal(1, last.GetProperty("leaderboardRank").GetInt32());
        Assert.Equal(100m, last.GetProperty("percentileRank").GetDecimal());
    }

    [Fact]
    public async Task PlayerRankHistory_LimitParameter_CapsResults()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "rank-limit@test.com", displayName = "Limit Player", password = "TestPass123!" } });
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // Seed 10 snapshots.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 1; i <= 10; i++)
            {
                db.PlayerRankSnapshots.Add(new PlayerRankSnapshot
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    SnapshotTick = i * 1008L,
                    LeaderboardRank = i,
                    WealthUsd = i * 100_000m,
                    PercentileRank = 50m,
                });
            }
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            "query Q($id: UUID!, $limit: Int) { playerRankHistory(playerId: $id, limit: $limit) { snapshotTick } }",
            new { id = playerId, limit = 3 });

        var snapshots = result.GetProperty("data").GetProperty("playerRankHistory");
        // limit=3 → returns latest 3, in chronological order.
        Assert.Equal(3, snapshots.GetArrayLength());
    }

    [Fact]
    public async Task RankHistory_TicksBackFilter_ReturnsOnlyRecentWindow()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "rank-window@test.com", displayName = "Window Player", password = "TestPass123!" } });
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            gs.CurrentTick = 1_000;
            db.PlayerRankSnapshots.AddRange(
                new PlayerRankSnapshot { Id = Guid.NewGuid(), PlayerId = playerId, SnapshotTick = 600, LeaderboardRank = 4, WealthUsd = 10_000m, PercentileRank = 60m },
                new PlayerRankSnapshot { Id = Guid.NewGuid(), PlayerId = playerId, SnapshotTick = 980, LeaderboardRank = 2, WealthUsd = 20_000m, PercentileRank = 80m });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            "query Q($id: UUID!, $ticksBack: Int!) { rankHistory(playerId: $id, ticksBack: $ticksBack) { snapshotTick } }",
            new { id = playerId, ticksBack = 30 });

        var snapshots = result.GetProperty("data").GetProperty("rankHistory");
        Assert.Equal(1, snapshots.GetArrayLength());
        Assert.Equal(980L, snapshots[0].GetProperty("snapshotTick").GetInt64());
    }

    [Fact]
    public void BadgeType_FromBountyCode_MapsKnownCodes()
    {
        Assert.Equal(BadgeType.FirstB2BTrade, BadgeType.FromBountyCode(Capitalism.Shared.Ranking.MasterRankingBountyCodes.Wholesaler));
        Assert.Equal(BadgeType.BankBaron, BadgeType.FromBountyCode(Capitalism.Shared.Ranking.MasterRankingBountyCodes.Banker));
        Assert.Equal(BadgeType.LoanMaster, BadgeType.FromBountyCode(Capitalism.Shared.Ranking.MasterRankingBountyCodes.Lender));
        Assert.Null(BadgeType.FromBountyCode("UNKNOWN"));
    }

    // ── Tests: Statistics export ─────────────────────────────────────────────

    [Fact]
    public async Task GenerateStatsExport_CsvFormat_ContainsSections()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "export-csv@test.com", displayName = "CSV Player", password = "TestPass123!" } });
        var playerToken = playerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        var result = await ExecAsync(client,
            """
            mutation Export($i: GenerateStatsExportInput!) {
              generateStatsExport(input: $i) {
                format fileName contentBase64
              }
            }
            """,
            new { i = new { format = "CSV" } },
            playerToken);

        var export = result.GetProperty("data").GetProperty("generateStatsExport");
        Assert.Equal("CSV", export.GetProperty("format").GetString());
        Assert.Contains("_Stats_", export.GetProperty("fileName").GetString());

        var content = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(export.GetProperty("contentBase64").GetString()!));
        Assert.Contains("=== OverallStats ===", content);
        Assert.Contains("=== CompanyMetrics ===", content);
        Assert.Contains("=== RankHistory ===", content);
        Assert.Contains("=== Badges ===", content);
    }

    [Fact]
    public async Task GenerateStatsExport_HtmlFormat_ContainsPlayerName()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "export-html@test.com", displayName = "HTML Exporter", password = "TestPass123!" } });
        var playerToken = playerResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        var result = await ExecAsync(client,
            """
            mutation Export($i: GenerateStatsExportInput!) {
              generateStatsExport(input: $i) {
                format fileName contentBase64
              }
            }
            """,
            new { i = new { format = "HTML" } },
            playerToken);

        var export = result.GetProperty("data").GetProperty("generateStatsExport");
        Assert.Equal("HTML", export.GetProperty("format").GetString());
        Assert.EndsWith(".html", export.GetProperty("fileName").GetString());

        var html = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(export.GetProperty("contentBase64").GetString()!));
        Assert.Contains("HTML Exporter", html);
        Assert.Contains("Stats Report", html);
        Assert.Contains("<table>", html);
    }

    [Fact]
    public async Task GenerateStatsExport_UnauthenticatedUser_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client,
            """
            mutation Export($i: GenerateStatsExportInput!) {
              generateStatsExport(input: $i) { format }
            }
            """,
            new { i = new { format = "CSV" } }
            // No token
        );

        Assert.True(result.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task GenerateStatsExport_PlayerExportsOtherPlayersStats_ReturnsForbidden()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var player1Result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "forbid-export1@test.com", displayName = "Player 1", password = "TestPass123!" } });
        var player1Token = player1Result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;

        var player2Result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "forbid-export2@test.com", displayName = "Player 2", password = "TestPass123!" } });
        var player2Id = Guid.Parse(player2Result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // Player 1 tries to export Player 2's stats.
        var result = await ExecAsync(client,
            """
            mutation Export($i: GenerateStatsExportInput!) {
              generateStatsExport(input: $i) { format }
            }
            """,
            new { i = new { playerId = player2Id, format = "CSV" } },
            player1Token);

        Assert.True(result.TryGetProperty("errors", out var errors));
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("FORBIDDEN", code);
    }

    [Fact]
    public async Task GenerateStatsExport_AdminExportsOtherPlayersStats_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var playerResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "admin-export-target@test.com", displayName = "Target Player", password = "TestPass123!" } });
        var targetPlayerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var adminResult = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "admin-export-actor@test.com", displayName = "Admin Actor", password = "TestPass123!" } });
        var adminToken = adminResult.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var adminId = Guid.Parse(adminResult.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var admin = await db.Players.FindAsync(adminId);
            admin!.Role = PlayerRole.Admin;
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            mutation Export($i: GenerateStatsExportInput!) {
              generateStatsExport(input: $i) { format fileName }
            }
            """,
            new { i = new { playerId = targetPlayerId, format = "CSV" } },
            adminToken);

        var export = result.GetProperty("data").GetProperty("generateStatsExport");
        Assert.Equal("CSV", export.GetProperty("format").GetString());
        Assert.Contains("Target_Player", export.GetProperty("fileName").GetString());
    }

    // ── Tests: RankHistoryPhase engine ───────────────────────────────────────

    [Fact]
    public async Task RankHistoryPhase_OnIntervalTick_CreatesSnapshotsForAllPlayers()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register two players.
        var p1Result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "rh-p1@test.com", displayName = "Rank 1", password = "TestPass123!" } });
        var p1Id = Guid.Parse(p1Result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var p2Result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "rh-p2@test.com", displayName = "Rank 2", password = "TestPass123!" } });
        var p2Id = Guid.Parse(p2Result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // ProcessTickAsync increments CurrentTick before running phases.
        // Set to tax-cycle boundary on increment.
        const long snapshotTick = 10L;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            gs.TaxCycleTicks = 10;
            gs.CurrentTick = snapshotTick - 1;
            await db.SaveChangesAsync();
        }

        // Run the tick processor (triggers all phases including RankHistoryPhase).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var tickProcessor = scope.ServiceProvider.GetRequiredService<Api.Engine.TickProcessor>();
            await tickProcessor.ProcessTickAsync(CancellationToken.None);
        }

        // Both players should have a snapshot at tax-cycle boundary tick.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var p1Snap = await db.PlayerRankSnapshots.FirstOrDefaultAsync(s => s.PlayerId == p1Id && s.SnapshotTick == snapshotTick);
            var p2Snap = await db.PlayerRankSnapshots.FirstOrDefaultAsync(s => s.PlayerId == p2Id && s.SnapshotTick == snapshotTick);

            Assert.NotNull(p1Snap);
            Assert.NotNull(p2Snap);
            Assert.True(p1Snap.LeaderboardRank >= 1 && p1Snap.LeaderboardRank <= 2);
            Assert.True(p2Snap.LeaderboardRank >= 1 && p2Snap.LeaderboardRank <= 2);
            // Ranks must be distinct (no ties since IDs differ).
            Assert.NotEqual(p1Snap.LeaderboardRank, p2Snap.LeaderboardRank);
        }
    }

    [Fact]
    public async Task RankHistoryPhase_OnNonIntervalTick_DoesNotCreateSnapshots()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var p1Result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token player { id } } }",
            new { i = new { email = "no-snap@test.com", displayName = "No Snap", password = "TestPass123!" } });
        var p1Id = Guid.Parse(p1Result.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // ProcessTickAsync increments CurrentTick before running phases.
        // Set to a non tax-cycle tick after increment.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gs = await db.GameStates.FirstAsync();
            gs.TaxCycleTicks = 10;
            gs.CurrentTick = 8L; // becomes 9 after increment — not divisible by tax cycle
            await db.SaveChangesAsync();
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var tickProcessor = scope.ServiceProvider.GetRequiredService<Api.Engine.TickProcessor>();
            await tickProcessor.ProcessTickAsync(CancellationToken.None);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var snapCount = await db.PlayerRankSnapshots.CountAsync(s => s.PlayerId == p1Id);
            Assert.Equal(0, snapCount);
        }
    }
}
