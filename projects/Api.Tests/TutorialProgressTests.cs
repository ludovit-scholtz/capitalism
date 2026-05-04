using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the tutorial milestone tracking system:
/// getTutorialProgress query and markTutorialMilestoneComplete mutation.
/// </summary>
public sealed class TutorialProgressTests
{
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
    public async Task GetTutorialProgress_NewPlayer_ReturnsAllFiveMilestonesIncomplete()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "tutorial-progress@test.com");

        var result = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted completedAtUtc } }", token: token);

        var milestones = result.GetProperty("data").GetProperty("tutorialProgress");
        Assert.Equal(5, milestones.GetArrayLength());

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
    public async Task MarkTutorialMilestoneComplete_AllFiveMilestones_AllPersistCorrectly()
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

        // Verify all are returned as completed in the progress query
        var progressResult = await ExecAsync(client, "{ tutorialProgress { milestone isCompleted } }", token: token);
        var progress = progressResult.GetProperty("data").GetProperty("tutorialProgress").EnumerateArray().ToList();
        Assert.Equal(5, progress.Count);
        Assert.All(progress, m => Assert.True(m.GetProperty("isCompleted").GetBoolean()));
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
}
