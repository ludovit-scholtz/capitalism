using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Regression tests guarding the dashboard performance optimization.
/// Every public read query on the dashboard path (<c>myCompanies</c>, <c>gameState</c>,
/// <c>city(id)</c>) must be read-only.  Calling
/// <c>BuildingConfigurationService.ApplyDuePlansAsync</c> inside a read resolver
/// triggers a full-table write scan across ALL players on every page load, which is
/// the root cause of multi-second dashboard load times with 50+ active players.
/// </summary>
public sealed class DashboardPerformanceTests
{
    // ── shared helpers ────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
            request.Headers.Authorization = new("Bearer", token);

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email,
        string displayName = "Test User")
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation Register($input: RegisterInput!) { register(input: $input) { token } }",
            new { input = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    /// <summary>
    /// Generates a deterministic-style 16-digit account number from the current timestamp.
    /// Uses modulo 10^15 so the raw value is always ≤ 15 digits; D16 left-pads to exactly 16.
    /// </summary>
    private static string GenerateTestAccountNumber() =>
        (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L).ToString("D16");

    /// <summary>
    /// Seed helper: creates a company, building, and an already-due plan
    /// directly in the database without going through the GraphQL mutation path.
    /// Returns the building ID and current tick.
    /// </summary>
    private static async Task<(Guid buildingId, long currentTick)> SeedCompanyWithDuePlanAsync(
        ApiWebApplicationFactory factory,
        Guid playerId,
        string companyName,
        string buildingName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var currentTick = await db.GameStates.AsNoTracking()
            .Select(gs => gs.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = companyName,
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = currentTick,
        };
        db.Companies.Add(company);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = GenerateTestAccountNumber(),
            CompanyId = company.Id,
            CurrencyCode = "EUR",
            Balance = 500_000m,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = buildingName,
            Level = 1,
        };
        db.Buildings.Add(building);

        // Plan that is due at the CURRENT tick — the old eager path would apply it immediately.
        db.BuildingConfigurationPlans.Add(new BuildingConfigurationPlan
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            AppliesAtTick = currentTick,
            SubmittedAtTick = currentTick,
            SubmittedAtUtc = DateTime.UtcNow,
            TotalTicksRequired = 0,
        });

        await db.SaveChangesAsync();
        return (building.Id, currentTick);
    }

    private static async Task<bool> PlanExistsAsync(ApiWebApplicationFactory factory, Guid buildingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BuildingConfigurationPlans.AnyAsync(p => p.BuildingId == buildingId);
    }

    // ── GetMyCompanies ────────────────────────────────────────────────────────

    /// <summary>
    /// Core regression: a building configuration plan due at <c>currentTick</c> must NOT be
    /// applied by a bare <c>myCompanies</c> GraphQL query.
    /// Only <c>BuildingUpgradePhase</c> in the tick engine may apply due plans.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_WithDuePlan_DoesNotApplyPlan_PlanRemainsUntilTickEngineRuns()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "mc-perf-user@test.com", "MC Perf User");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "mc-perf-user@test.com")).Id;
        }

        var (buildingId, currentTick) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "MCPerfCorp", "MCPerfFactory");

        // ── Query myCompanies — must be read-only. ──
        var readResult = await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { buildings { id pendingConfiguration { id appliesAtTick } } } }",
            token: userToken);

        var buildings = readResult
            .GetProperty("data").GetProperty("myCompanies")
            .EnumerateArray()
            .SelectMany(c => c.GetProperty("buildings").EnumerateArray())
            .ToList();

        var dashBuilding = buildings.FirstOrDefault(b => b.GetProperty("id").GetString() == buildingId.ToString());
        Assert.NotEqual(JsonValueKind.Undefined, dashBuilding.ValueKind);

        // Assertion A: plan is visible in the GraphQL response (not eagerly applied/wiped).
        var pending = dashBuilding.GetProperty("pendingConfiguration");
        Assert.NotEqual(JsonValueKind.Null, pending.ValueKind);
        Assert.Equal(currentTick, pending.GetProperty("appliesAtTick").GetInt64());

        // Assertion B: plan is still present in the database (no write path was triggered).
        Assert.True(
            await PlanExistsAsync(factory, buildingId),
            "GetMyCompanies must not delete (apply) the due plan; plan must remain in DB.");

        // ── Run tick engine — the ONLY legitimate applier. ──
        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db3 = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetServices<ITickPhase>();
            await new TickProcessor(db3, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
        }

        // Assertion C: plan is gone after tick engine ran.
        Assert.False(
            await PlanExistsAsync(factory, buildingId),
            "BuildingUpgradePhase must apply and remove the due plan after ProcessTickAsync.");
    }

    // ── GetGameState ──────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>gameState</c> query is also fetched on every dashboard load (as part of the
    /// combined startup query).  It must NOT apply due plans — that write path was the
    /// second remaining bottleneck after the <c>myCompanies</c> fix.
    /// </summary>
    [Fact]
    public async Task GetGameState_WithDuePlan_DoesNotApplyPlan_PlanRemainsInDatabase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "gs-perf-user@test.com", "GS Perf User");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "gs-perf-user@test.com")).Id;
        }

        var (buildingId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "GSPerfCorp", "GSPerfFactory");

        // Query gameState — must be read-only.
        var result = await ExecuteGraphQlAsync(
            client,
            "{ gameState { currentTick taxCycleTicks taxRate } }");

        var gs = result.GetProperty("data").GetProperty("gameState");
        Assert.NotEqual(JsonValueKind.Null, gs.ValueKind);
        Assert.True(gs.GetProperty("currentTick").GetInt64() >= 0);

        // Plan must still be in the DB — gameState query must not have applied it.
        Assert.True(
            await PlanExistsAsync(factory, buildingId),
            "GetGameState must not delete (apply) due plans; it is a read-only query.");
    }

    /// <summary>
    /// The combined dashboard startup query (<c>myCompanies + gameState + cities</c>)
    /// must not apply any due plans.  Both resolvers execute within the same GraphQL request.
    /// </summary>
    [Fact]
    public async Task CombinedDashboardQuery_WithDuePlan_DoesNotApplyPlan()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "combined-perf-user@test.com", "Combined Perf");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "combined-perf-user@test.com")).Id;
        }

        var (buildingId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "CombinedPerfCorp", "CombinedPerfFactory");

        // Fire the exact same combined query DashboardView.vue sends on mount.
        var result = await ExecuteGraphQlAsync(
            client,
            """
            {
                myCompanies { id name buildings { id pendingConfiguration { id } } }
                gameState { currentTick taxRate }
                cities { id name }
            }
            """,
            token: userToken);

        var data = result.GetProperty("data");
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("myCompanies").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("gameState").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, data.GetProperty("cities").ValueKind);

        // Neither myCompanies nor gameState may have applied the due plan.
        Assert.True(
            await PlanExistsAsync(factory, buildingId),
            "Combined dashboard query must not apply due plans in any resolver.");
    }

    // ── GetCity ───────────────────────────────────────────────────────────────

    /// <summary>
    /// The <c>city(id)</c> query (city map page) also used to apply due plans.
    /// It must be read-only as well — it is not part of the tick engine.
    /// </summary>
    [Fact]
    public async Task GetCity_WithDuePlan_DoesNotApplyPlan_PlanRemainsInDatabase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "city-perf-user@test.com", "City Perf User");

        Guid playerId;
        Guid bratislavaId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "city-perf-user@test.com")).Id;
            bratislavaId = (await db.Cities.FirstAsync(c => c.Name == "Bratislava")).Id;
        }

        var (buildingId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "CityPerfCorp", "CityPerfFactory");

        // Query city — must be read-only.
        var result = await ExecuteGraphQlAsync(
            client,
            "query GetCity($id: UUID!) { city(id: $id) { id name } }",
            variables: new { id = bratislavaId });

        var city = result.GetProperty("data").GetProperty("city");
        Assert.NotEqual(JsonValueKind.Null, city.ValueKind);
        Assert.Equal(bratislavaId.ToString(), city.GetProperty("id").GetString());

        // Plan must still be in the DB — city query must not have applied it.
        Assert.True(
            await PlanExistsAsync(factory, buildingId),
            "GetCity must not delete (apply) due plans; it is a read-only query.");
    }

    // ── GameState content ─────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that the <c>gameState</c> query returns correct, non-null data
    /// (tick, tax cycle, tax rate) for basic regression protection.
    /// </summary>
    [Fact]
    public async Task GetGameState_ReturnsCorrectGameData()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            "{ gameState { currentTick taxCycleTicks taxRate } }");

        var gs = result.GetProperty("data").GetProperty("gameState");
        Assert.NotEqual(JsonValueKind.Null, gs.ValueKind);
        Assert.True(gs.GetProperty("currentTick").GetInt64() >= 0, "currentTick must be non-negative");
        Assert.True(gs.GetProperty("taxCycleTicks").GetInt64() > 0, "taxCycleTicks must be positive");
        Assert.True(gs.GetProperty("taxRate").GetDecimal() > 0m, "taxRate must be positive");
    }

    /// <summary>
    /// After the tick engine advances the game state by one tick,
    /// a subsequent <c>gameState</c> query must reflect the new (higher) tick.
    /// The 8-second cache is cleared explicitly to verify the DB-backed value,
    /// not the in-flight cached snapshot.
    /// </summary>
    [Fact]
    public async Task GetGameState_AfterTickEngineRun_ReflectsNewTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Record the tick before via direct DB access so we do not warm the 8s cache.
        long tickBefore;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            tickBefore = await db.GameStates.AsNoTracking()
                .Select(gs => gs.CurrentTick)
                .FirstOrDefaultDeterministicAsync();
        }

        // Advance one tick.
        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetServices<ITickPhase>();
            await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
        }

        // Evict the singleton cache entry so the HTTP query hits the DB.
        var cache = factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
        cache.Remove("gameState_singleton");

        // Now the HTTP query must return the new tick value.
        var after = await ExecuteGraphQlAsync(client, "{ gameState { currentTick } }");
        var tickAfter = after.GetProperty("data").GetProperty("gameState")
            .GetProperty("currentTick").GetInt64();

        Assert.True(tickAfter > tickBefore,
            $"gameState.currentTick must increase after a tick engine run (was {tickBefore}, got {tickAfter}).");
    }

    // ── Tick engine is the sole applier ───────────────────────────────────────

    /// <summary>
    /// Verifies that the tick engine itself (via <c>BuildingUpgradePhase</c>) DOES apply
    /// due plans when explicitly run — proving the test infrastructure is correct and
    /// the engine is functioning as the sole applier.
    /// </summary>
    [Fact]
    public async Task TickEngine_WithDuePlan_AppliesPlanAndRemovesItFromDatabase()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "tick-applies-user@test.com", "Tick Applies");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "tick-applies-user@test.com")).Id;
        }

        var (buildingId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "TickAppliesCorp", "TickAppliesFactory");

        // Confirm plan is there before the tick.
        Assert.True(await PlanExistsAsync(factory, buildingId));

        // Run tick engine.
        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetServices<ITickPhase>();
            await new TickProcessor(db, phases, new NullLogger<TickProcessor>()).ProcessTickAsync();
        }

        // Confirm plan is gone after the tick.
        Assert.False(
            await PlanExistsAsync(factory, buildingId),
            "BuildingUpgradePhase in the tick engine must apply and remove due plans.");
    }
}
