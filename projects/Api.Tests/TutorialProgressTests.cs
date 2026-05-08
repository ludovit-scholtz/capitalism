using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Types;
using Api.Utilities;
using Capitalism.Shared.Ranking;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the tutorial milestone tracking system:
/// getTutorialProgress query and markTutorialMilestoneComplete mutation.
/// </summary>
public sealed class TutorialProgressTests
{
    private sealed class CapturingTutorialTelemetryService : IMasterRankingTelemetryService
    {
        public List<(string EventType, string Email, string? UniqueScopeKey)> Calls { get; } = [];

        public Task ReportEventAsync(string eventType, string playerEmail, string? uniqueScopeKey = null, string? externalEventId = null, CancellationToken cancellationToken = default)
        {
            Calls.Add((eventType, playerEmail, uniqueScopeKey));
            return Task.CompletedTask;
        }
    }

    private sealed class StubMasterGameAdministrationService : IMasterGameAdministrationService
    {
        public List<TutorialBountyStatusResult> TutorialBounties { get; } = [];

        public Task<MasterGameAdministrationAccessSnapshot> GetGameAdministrationAccessAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult(new MasterGameAdministrationAccessSnapshot(false, false, false));

        public Task<IReadOnlyList<GlobalGameAdminGrantSummary>> GetGlobalGameAdminGrantsAsync(string requesterEmail, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<GlobalGameAdminGrantSummary>>([]);

        public Task<GlobalGameAdminGrantSummary> AssignGlobalGameAdminAsync(string requesterEmail, string targetEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task RemoveGlobalGameAdminAsync(string requesterEmail, string targetEmail, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<GameNewsFeedResult> GetGameNewsFeedAsync(string? playerEmail, bool includeDrafts, string? requesterEmail, CancellationToken cancellationToken = default) => Task.FromResult(new GameNewsFeedResult());

        public Task MarkGameNewsReadAsync(string playerEmail, IReadOnlyCollection<Guid> entryIds, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> MarkAllGameNewsReadAsync(string playerEmail, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<GameNewsEntryResult> UpsertGameNewsEntryAsync(string requesterEmail, Guid? entryId, string entryType, string status, IReadOnlyList<GameNewsLocalizationInput> localizations, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TutorialBountyStatusResult>> GetTutorialBountyStatusesAsync(string playerEmail, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TutorialBountyStatusResult>>(TutorialBounties);
    }

    private sealed class TutorialBountyAwareFactory(
        CapturingTutorialTelemetryService telemetry,
        StubMasterGameAdministrationService masterStub) : ApiWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ApplyBaseConfiguration(builder);
            builder.ConfigureServices(services =>
            {
                var telemetryDescriptor = services.FirstOrDefault(item => item.ServiceType == typeof(IMasterRankingTelemetryService));
                if (telemetryDescriptor is not null)
                {
                    services.Remove(telemetryDescriptor);
                }

                var adminDescriptor = services.FirstOrDefault(item => item.ServiceType == typeof(IMasterGameAdministrationService));
                if (adminDescriptor is not null)
                {
                    services.Remove(adminDescriptor);
                }

                services.AddScoped<IMasterRankingTelemetryService>(_ => telemetry);
                services.AddScoped<IMasterGameAdministrationService>(_ => masterStub);
            });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GraphQL helpers
    // ──────────────────────────────────────────────────────────────────────────

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
        {
            req.Headers.Authorization = new("Bearer", token);
        }

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName = "Test User")
    {
        var result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTutorialProgress_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }");

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected auth error for unauthenticated request.");
    }

    [Fact]
    public async Task GetTutorialProgress_NewPlayer_ReturnsAllMilestonesIncomplete()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tutorial-progress@test.com");

        var result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted completedAtUtc } }", token: token);

        var milestones = result.GetProperty("data").GetProperty("tutorialProgress");
        // 5 tutorial milestones + 2 contextual milestones + 1 dashboard tooltip milestone = 8 total
        Assert.Equal(TutorialMilestone.All.Count, milestones.GetArrayLength());

        foreach (var m in milestones.EnumerateArray())
        {
            Assert.False(m.GetProperty("isCompleted").GetBoolean(), $"Milestone {m.GetProperty("milestone")} should not be completed for a new player.");
            Assert.Equal(JsonValueKind.Null, m.GetProperty("completedAtUtc").ValueKind);
        }
    }

    [Fact]
    public async Task GetTutorialProgress_AllExpectedMilestoneIdsPresent()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tutorial-ids@test.com");

        var result = await ExecAsync(client, "{ tutorialProgress { milestone } }", token: token);

        var returned = result.GetProperty("data")
            .GetProperty("tutorialProgress")
            .EnumerateArray()
            .Select(m => m.GetProperty("milestone").GetString()!)
            .ToHashSet();

        Assert.Contains("FIRST_RESOURCE_SOLD", returned);
        Assert.Contains("FIRST_B2B_TRADE", returned);
        Assert.Contains("FIRST_LOAN_TAKEN", returned);
        Assert.Contains("FIRST_COMPETITOR_OBSERVED", returned);
        Assert.Contains("FIRST_BRAND_ESTABLISHED", returned);
        // Tooltip overlay milestones
        Assert.Contains("TOOLTIP_DASHBOARD_SHOWN", returned);
        Assert.Contains("FIRST_BUILDING_DETAIL_VISIT", returned);
        Assert.Contains("FIRST_GRID_EDITOR_OPEN", returned);
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
            new { i = new { milestone = "FIRST_RESOURCE_SOLD" } });

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected auth error for unauthenticated request.");
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_ValidMilestone_PersistsToDatabase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "mark-milestone@test.com");

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted completedAtUtc } }",
            new { i = new { milestone = "FIRST_RESOURCE_SOLD" } },
            token: token);

        var payload = result.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
        Assert.Equal("FIRST_RESOURCE_SOLD", payload.GetProperty("milestone").GetString());
        Assert.True(payload.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, payload.GetProperty("completedAtUtc").ValueKind);

        // Also verify via the query that it's reflected
        var queryResult = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token);
        var milestones = queryResult.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var soldMilestone = milestones.First(m => m.GetProperty("milestone").GetString() == "FIRST_RESOURCE_SOLD");
        Assert.True(soldMilestone.GetProperty("isCompleted").GetBoolean());

        // Other milestones should still be incomplete
        var b2bMilestone = milestones.First(m => m.GetProperty("milestone").GetString() == "FIRST_B2B_TRADE");
        Assert.False(b2bMilestone.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_CalledTwice_IsIdempotent()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "idempotent-milestone@test.com");

        const string mutationDoc =
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted completedAtUtc } }";
        var vars = new { i = new { milestone = "FIRST_LOAN_TAKEN" } };

        var first = await ExecAsync(client, mutationDoc, vars, token: token);
        var second = await ExecAsync(client, mutationDoc, vars, token: token);

        var firstPayload = first.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
        var secondPayload = second.GetProperty("data").GetProperty("markTutorialMilestoneComplete");

        Assert.True(firstPayload.GetProperty("isCompleted").GetBoolean());
        Assert.True(secondPayload.GetProperty("isCompleted").GetBoolean());

        // Verify the database has exactly one row for this milestone for this player
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerResult = await ExecAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        var rowCount = db.TutorialProgresses.Count(tp => tp.PlayerId == playerId && tp.Milestone == "FIRST_LOAN_TAKEN");
        Assert.Equal(1, rowCount);
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_UnknownMilestone_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "unknown-milestone@test.com");

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
            new { i = new { milestone = "NONEXISTENT_MILESTONE" } },
            token: token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0, "Expected error for unknown milestone.");
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("UNKNOWN_MILESTONE", code);
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_OriginalFiveMilestones_AllPersistCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "all-milestones@test.com");

        var milestones = new[]
        {
            "FIRST_RESOURCE_SOLD",
            "FIRST_B2B_TRADE",
            "FIRST_LOAN_TAKEN",
            "FIRST_COMPETITOR_OBSERVED",
            "FIRST_BRAND_ESTABLISHED",
        };

        foreach (var milestone in milestones)
        {
            var result = await ExecAsync(client,
                "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
                new { i = new { milestone } },
                token: token);

            var payload = result.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
            Assert.Equal(milestone, payload.GetProperty("milestone").GetString());
            Assert.True(payload.GetProperty("isCompleted").GetBoolean());
        }

        // Verify progress query returns all milestones (8 total) and the 5 we completed are marked
        var progressResult = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token);
        var progress = progressResult.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        Assert.Equal(TutorialMilestone.All.Count, progress.Count);

        // All 5 explicitly-completed milestones must be true
        var completedSet = progress
            .Where(m => m.GetProperty("isCompleted").GetBoolean())
            .Select(m => m.GetProperty("milestone").GetString()!)
            .ToHashSet();
        foreach (var m in milestones)
            Assert.Contains(m, completedSet);

        // Context milestones were NOT explicitly marked, so they remain false
        Assert.DoesNotContain("TOOLTIP_DASHBOARD_SHOWN", completedSet);
        Assert.DoesNotContain("FIRST_BUILDING_DETAIL_VISIT", completedSet);
        Assert.DoesNotContain("FIRST_GRID_EDITOR_OPEN", completedSet);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tooltip/context milestone tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task MarkTutorialMilestoneComplete_TooltipDashboardShown_PersistsCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tooltip-dashboard@test.com");

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted completedAtUtc } }",
            new { i = new { milestone = "TOOLTIP_DASHBOARD_SHOWN" } },
            token: token);

        var payload = result.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
        Assert.Equal("TOOLTIP_DASHBOARD_SHOWN", payload.GetProperty("milestone").GetString());
        Assert.True(payload.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, payload.GetProperty("completedAtUtc").ValueKind);

        // Verify it is reflected in the progress query
        var queryResult = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token);
        var milestones = queryResult.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var dashMilestone = milestones.First(m => m.GetProperty("milestone").GetString() == "TOOLTIP_DASHBOARD_SHOWN");
        Assert.True(dashMilestone.GetProperty("isCompleted").GetBoolean());

        // Other tooltip milestones must remain incomplete
        var detailMilestone = milestones.First(m => m.GetProperty("milestone").GetString() == "FIRST_BUILDING_DETAIL_VISIT");
        Assert.False(detailMilestone.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_FirstBuildingDetailVisit_PersistsCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tooltip-building@test.com");

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted completedAtUtc } }",
            new { i = new { milestone = "FIRST_BUILDING_DETAIL_VISIT" } },
            token: token);

        var payload = result.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
        Assert.Equal("FIRST_BUILDING_DETAIL_VISIT", payload.GetProperty("milestone").GetString());
        Assert.True(payload.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, payload.GetProperty("completedAtUtc").ValueKind);
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_FirstGridEditorOpen_PersistsCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tooltip-grid@test.com");

        var result = await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted completedAtUtc } }",
            new { i = new { milestone = "FIRST_GRID_EDITOR_OPEN" } },
            token: token);

        var payload = result.GetProperty("data").GetProperty("markTutorialMilestoneComplete");
        Assert.Equal("FIRST_GRID_EDITOR_OPEN", payload.GetProperty("milestone").GetString());
        Assert.True(payload.GetProperty("isCompleted").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, payload.GetProperty("completedAtUtc").ValueKind);
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_AllTooltipMilestones_AreIdempotent()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tooltip-idempotent@test.com");

        const string mutDoc =
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }";

        var tooltipMilestones = new[]
        {
            "TOOLTIP_DASHBOARD_SHOWN",
            "FIRST_BUILDING_DETAIL_VISIT",
            "FIRST_GRID_EDITOR_OPEN",
        };

        // Mark each twice — must succeed both times without duplicate DB rows
        foreach (var m in tooltipMilestones)
        {
            var vars = new { i = new { milestone = m } };
            var first = await ExecAsync(client, mutDoc, vars, token: token);
            var second = await ExecAsync(client, mutDoc, vars, token: token);

            Assert.True(first.GetProperty("data").GetProperty("markTutorialMilestoneComplete")
                .GetProperty("isCompleted").GetBoolean(), $"First call for {m} must be completed.");
            Assert.True(second.GetProperty("data").GetProperty("markTutorialMilestoneComplete")
                .GetProperty("isCompleted").GetBoolean(), $"Second idempotent call for {m} must be completed.");
        }

        // Verify exactly one DB row per tooltip milestone
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerResult = await ExecAsync(client, "{ me { id } }", token: token);
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        foreach (var m in tooltipMilestones)
        {
            var rowCount = db.TutorialProgresses.Count(tp => tp.PlayerId == playerId && tp.Milestone == m);
            Assert.Equal(1, rowCount);
        }
    }

    [Fact]
    public async Task GetTutorialProgress_TooltipMilestonesIsolatedPerPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token1 = await RegisterAsync(client, "tooltip-player1@test.com");
        var token2 = await RegisterAsync(client, "tooltip-player2@test.com");

        // Player 1 dismisses dashboard tooltip
        await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
            new { i = new { milestone = "TOOLTIP_DASHBOARD_SHOWN" } },
            token: token1);

        // Player 2 should still see dashboard tooltip as incomplete
        var p2Result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token2);
        var p2Progress = p2Result.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var p2Dash = p2Progress.First(m => m.GetProperty("milestone").GetString() == "TOOLTIP_DASHBOARD_SHOWN");
        Assert.False(p2Dash.GetProperty("isCompleted").GetBoolean(), "Player 2 must not see Player 1's tooltip dismissal.");

        // Player 1 should see dashboard tooltip as completed
        var p1Result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token1);
        var p1Progress = p1Result.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var p1Dash = p1Progress.First(m => m.GetProperty("milestone").GetString() == "TOOLTIP_DASHBOARD_SHOWN");
        Assert.True(p1Dash.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task GetTutorialProgress_IsolatedPerPlayer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token1 = await RegisterAsync(client, "player1-tutorial@test.com");
        var token2 = await RegisterAsync(client, "player2-tutorial@test.com");

        // Player 1 completes a milestone
        await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
            new { i = new { milestone = "FIRST_BRAND_ESTABLISHED" } },
            token: token1);

        // Player 2 should still see all incomplete
        var p2Result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token2);
        var p2Progress = p2Result.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        Assert.All(p2Progress, m => Assert.False(m.GetProperty("isCompleted").GetBoolean()));

        // Player 1 should see the completed milestone
        var p1Result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token1);
        var p1Progress = p1Result.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var brandMilestone = p1Progress.First(m => m.GetProperty("milestone").GetString() == "FIRST_BRAND_ESTABLISHED");
        Assert.True(brandMilestone.GetProperty("isCompleted").GetBoolean());
    }

    [Fact]
    public async Task MarkTutorialMilestoneComplete_FirstGridEditorOpen_EmitsTutorialBountyTelemetry()
    {
        var telemetry = new CapturingTutorialTelemetryService();
        var masterStub = new StubMasterGameAdministrationService();
        await using var factory = new TutorialBountyAwareFactory(telemetry, masterStub);
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tutorial-bounty-emit@test.com");

        await ExecAsync(client,
            "mutation M($i: MarkTutorialMilestoneCompleteInput!) { markTutorialMilestoneComplete(input: $i) { milestone isCompleted } }",
            new { i = new { milestone = "FIRST_GRID_EDITOR_OPEN" } },
            token: token);

        Assert.Contains(telemetry.Calls, call => call.EventType == MasterRankingBountyCodes.TutorialFirstGridEditorOpen);
    }

    [Fact]
    public async Task GetTutorialProgress_BountyAwardedMarksMilestoneCompleted()
    {
        var telemetry = new CapturingTutorialTelemetryService();
        var masterStub = new StubMasterGameAdministrationService();
        masterStub.TutorialBounties.Add(new TutorialBountyStatusResult
        {
            Milestone = "FIRST_LOAN_TAKEN",
            BountyCode = MasterRankingBountyCodes.TutorialFirstLoanTaken,
            IsAwarded = true,
            AwardedAtUtc = DateTime.UtcNow,
            RewardPoints = 60m,
        });

        await using var factory = new TutorialBountyAwareFactory(telemetry, masterStub);
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tutorial-bounty-truth@test.com");

        var progressResult = await ExecAsync(client,
            "{ tutorialProgress { milestone isCompleted completedAtUtc bountyAwarded bountyAwardedAtUtc bountyPoints } }",
            token: token);

        var milestones = progressResult.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        var loanMilestone = milestones.First(item => item.GetProperty("milestone").GetString() == "FIRST_LOAN_TAKEN");

        Assert.True(loanMilestone.GetProperty("isCompleted").GetBoolean());
        Assert.True(loanMilestone.GetProperty("bountyAwarded").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, loanMilestone.GetProperty("bountyAwardedAtUtc").ValueKind);
        Assert.Equal(60m, loanMilestone.GetProperty("bountyPoints").GetDecimal());
    }
}
