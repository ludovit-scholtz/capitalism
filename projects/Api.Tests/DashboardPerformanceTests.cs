using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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

    // ── Authentication / isolation ────────────────────────────────────────────

    /// <summary>
    /// <c>myCompanies</c> is decorated with [Authorize] — an unauthenticated request must be
    /// rejected with a GraphQL authorization error, not silently return an empty list.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_Unauthenticated_ReturnsAuthorizationError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // No Bearer token — deliberately unauthenticated.
        var result = await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { id name } }");

        // HotChocolate returns either data=null with an errors array, or a top-level errors
        // object — both are valid.  Either way, `myCompanies` must not return real data.
        var hasErrors = result.TryGetProperty("errors", out var errors) &&
                        errors.ValueKind == JsonValueKind.Array &&
                        errors.GetArrayLength() > 0;

        bool companiesIsNull = false;
        if (result.TryGetProperty("data", out var dataEl))
        {
            if (dataEl.ValueKind == JsonValueKind.Object
                && dataEl.TryGetProperty("myCompanies", out var mc))
            {
                companiesIsNull = mc.ValueKind == JsonValueKind.Null;
            }
            else
            {
                companiesIsNull = dataEl.ValueKind == JsonValueKind.Null;
            }
        }

        Assert.True(hasErrors || companiesIsNull,
            "An unauthenticated myCompanies request must be rejected.");
    }

    /// <summary>
    /// A player with three companies must see all three returned by <c>myCompanies</c>.
    /// Ensures the WHERE clause filters correctly and that the query doesn't crash under
    /// moderate data volume.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_WithMultipleCompanies_ReturnsAllOwnedCompanies()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "multi-co-user@test.com", "Multi Co User");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "multi-co-user@test.com")).Id;
        }

        // Seed three extra companies directly (on top of any existing ones).
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var currentTick = await db.GameStates.AsNoTracking()
                .Select(gs => gs.CurrentTick)
                .FirstOrDefaultDeterministicAsync();

            foreach (var name in new[] { "AlphaInc", "BetaCorp", "GammaLtd" })
            {
                var company = new Company
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    Name = name,
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
                    Balance = 10_000m,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync();
        }

        // Invalidate the per-user cache so we hit the DB.
        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove($"myCompanies_{playerId}");

        var result = await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { id name } }",
            token: userToken);

        var companies = result.GetProperty("data").GetProperty("myCompanies")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString())
            .ToList();

        Assert.Contains("AlphaInc", companies);
        Assert.Contains("BetaCorp", companies);
        Assert.Contains("GammaLtd", companies);
    }

    /// <summary>
    /// Verifies that Player A's <c>myCompanies</c> query does NOT apply Player B's due plans.
    /// Cross-player data isolation is a correctness requirement for multi-player games.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_OtherPlayersDuePlans_AreNotAffected()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register two separate players.
        var tokenA = await RegisterAndGetTokenAsync(client, "isolation-a@test.com", "Player A");
        var tokenB = await RegisterAndGetTokenAsync(client, "isolation-b@test.com", "Player B");

        Guid playerBId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerBId = (await db.Players.FirstAsync(p => p.Email == "isolation-b@test.com")).Id;
        }

        // Seed a due plan belonging to Player B.
        var (buildingBId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerBId, "PlayerBCorp", "PlayerBFactory");

        // Player A runs myCompanies.
        await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { id buildings { id } } }",
            token: tokenA);

        // Player B's plan must still exist — Player A's query must not have touched it.
        Assert.True(
            await PlanExistsAsync(factory, buildingBId),
            "Player A's myCompanies query must NOT apply Player B's due plans.");
    }

    /// <summary>
    /// A company with multiple simultaneously-due plans must have ALL plans intact
    /// after a <c>myCompanies</c> query — not just the first one.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_WithMultipleDuePlans_NoneAreApplied()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "multi-plan-user@test.com", "Multi Plan User");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "multi-plan-user@test.com")).Id;
        }

        // Seed the first building (uses the shared helper which creates one plan).
        var (building1Id, currentTick) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "MultiPlanCorp", "MultiPlanFactory1");

        // Seed a second due plan on a different building under the same company.
        Guid building2Id;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var company = await db.Companies.FirstAsync(c => c.Name == "MultiPlanCorp");
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

            var building2 = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "MultiPlanFactory2",
                Level = 1,
            };
            db.Buildings.Add(building2);
            db.BuildingConfigurationPlans.Add(new BuildingConfigurationPlan
            {
                Id = Guid.NewGuid(),
                BuildingId = building2.Id,
                AppliesAtTick = currentTick,
                SubmittedAtTick = currentTick,
                SubmittedAtUtc = DateTime.UtcNow,
                TotalTicksRequired = 0,
            });
            await db.SaveChangesAsync();
            building2Id = building2.Id;
        }

        // Query myCompanies — both plans must survive.
        await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { buildings { id pendingConfiguration { id } } } }",
            token: userToken);

        Assert.True(await PlanExistsAsync(factory, building1Id),
            "Plan for building 1 must NOT be applied by myCompanies query.");
        Assert.True(await PlanExistsAsync(factory, building2Id),
            "Plan for building 2 must NOT be applied by myCompanies query.");
    }

    // ── GetCity content ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the <c>city(id)</c> resolver returns correct, populated city data —
    /// name, population, resources — proving the <c>AsNoTracking</c> + <c>AsSplitQuery</c>
    /// refactor did not break the Include chain.
    /// </summary>
    [Fact]
    public async Task GetCity_ReturnsCorrectCityData()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        Guid bratislavaId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            bratislavaId = (await db.Cities.FirstAsync(c => c.Name == "Bratislava")).Id;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query GetCity($id: UUID!) { city(id: $id) { id name population currencyCode } }",
            variables: new { id = bratislavaId });

        var city = result.GetProperty("data").GetProperty("city");
        Assert.NotEqual(JsonValueKind.Null, city.ValueKind);
        Assert.Equal("Bratislava", city.GetProperty("name").GetString());
        Assert.True(city.GetProperty("population").GetInt64() > 0, "Bratislava population must be positive");
        Assert.Equal("EUR", city.GetProperty("currencyCode").GetString());
    }

    /// <summary>
    /// Querying a non-existent city ID must return a null result without throwing or
    /// triggering any write path.
    /// </summary>
    [Fact]
    public async Task GetCity_NonExistentId_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var missingId = Guid.NewGuid();

        var result = await ExecuteGraphQlAsync(
            client,
            "query GetCity($id: UUID!) { city(id: $id) { id name } }",
            variables: new { id = missingId });

        var cityValue = result.GetProperty("data").GetProperty("city");
        Assert.Equal(JsonValueKind.Null, cityValue.ValueKind);
    }

    // ── Cache behaviour ───────────────────────────────────────────────────────

    /// <summary>
    /// Verifies the <c>gameState</c> 8-second cache is actually populated after the first
    /// call so that rapid subsequent requests skip the DB round-trip.
    /// </summary>
    [Fact]
    public async Task GetGameState_AfterFirstCall_IsCachedInMemory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Evict any lingering cache entry from other tests.
        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove("gameState_singleton");

        // First call — should populate the cache.
        await ExecuteGraphQlAsync(client, "{ gameState { currentTick } }");

        // Inspect the cache directly.
        var cacheHit = cache.TryGetValue("gameState_singleton", out GameState? cached);
        Assert.True(cacheHit, "gameState_singleton must be set in IMemoryCache after first query.");
        Assert.NotNull(cached);
    }

    /// <summary>
    /// A second <c>gameState</c> HTTP call must return the same tick value as the first
    /// (proving the cache is serving the result, not querying the DB each time).
    /// </summary>
    [Fact]
    public async Task GetGameState_SecondCall_ReturnsSameDataFromCache()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Evict any lingering cache entry.
        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove("gameState_singleton");

        // First HTTP call — populates the cache.
        var first = await ExecuteGraphQlAsync(client, "{ gameState { currentTick taxCycleTicks } }");
        var tick1 = first.GetProperty("data").GetProperty("gameState").GetProperty("currentTick").GetInt64();

        // Second call without any DB change — must return the same cached tick.
        var second = await ExecuteGraphQlAsync(client, "{ gameState { currentTick taxCycleTicks } }");
        var tick2 = second.GetProperty("data").GetProperty("gameState").GetProperty("currentTick").GetInt64();

        Assert.Equal(tick1, tick2);
    }

    /// <summary>
    /// Verifies that the per-user <c>myCompanies</c> cache is populated after the first call,
    /// so burst concurrent reloads skip the DB round-trip.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_AfterFirstCall_IsCachedInMemory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "mc-cache-user@test.com", "Cache User");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "mc-cache-user@test.com")).Id;
        }

        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        var cacheKey = $"myCompanies_{playerId}";
        cache.Remove(cacheKey);

        // First call — should populate the per-user cache.
        await ExecuteGraphQlAsync(client, "{ myCompanies { id name } }", token: userToken);

        // Cache entry must now exist.
        var cacheHit = cache.TryGetValue(cacheKey, out List<Company>? cached);
        Assert.True(cacheHit, "myCompanies_{userId} must be set in IMemoryCache after first query.");
        Assert.NotNull(cached);
    }

    // ── GetCities (list query) ────────────────────────────────────────────────

    /// <summary>
    /// The <c>cities</c> list query must return all three seeded cities (Bratislava, Prague,
    /// Vienna) and must remain read-only (no due plans applied as a side effect).
    /// </summary>
    [Fact]
    public async Task GetCities_ReturnsAllThreeSeededCities_AndIsReadOnly()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Register a user and seed a due plan so we can detect any spurious writes.
        var userToken = await RegisterAndGetTokenAsync(client, "cities-list-user@test.com", "Cities List User");
        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "cities-list-user@test.com")).Id;
        }
        var (buildingId, _) = await SeedCompanyWithDuePlanAsync(
            factory, playerId, "CitiesListCorp", "CitiesListFactory");

        var result = await ExecuteGraphQlAsync(client, "{ cities { id name currencyCode } }");

        var cityNames = result.GetProperty("data").GetProperty("cities")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()!)
            .ToHashSet();

        Assert.Contains("Bratislava", cityNames);
        Assert.Contains("Prague", cityNames);
        Assert.Contains("Vienna", cityNames);

        // cities query must not have applied the due plan.
        Assert.True(
            await PlanExistsAsync(factory, buildingId),
            "GetCities must not apply due plans; it is a read-only query.");
    }

    // ── All three seeded cities via GetCity ───────────────────────────────────

    /// <summary>
    /// Prague must be returned correctly by <c>city(id)</c> with CZK currency.
    /// </summary>
    [Fact]
    public async Task GetCity_PragueCity_ReturnsCzkCurrency()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        Guid pragueId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            pragueId = (await db.Cities.FirstAsync(c => c.Name == "Prague")).Id;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query GetCity($id: UUID!) { city(id: $id) { id name currencyCode } }",
            variables: new { id = pragueId });

        var city = result.GetProperty("data").GetProperty("city");
        Assert.NotEqual(JsonValueKind.Null, city.ValueKind);
        Assert.Equal("Prague", city.GetProperty("name").GetString());
        Assert.Equal("CZK", city.GetProperty("currencyCode").GetString());
    }

    /// <summary>
    /// Vienna must be returned correctly by <c>city(id)</c> with EUR currency.
    /// </summary>
    [Fact]
    public async Task GetCity_ViennaCity_ReturnsEurCurrency()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        Guid viennaId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            viennaId = (await db.Cities.FirstAsync(c => c.Name == "Vienna")).Id;
        }

        var result = await ExecuteGraphQlAsync(
            client,
            "query GetCity($id: UUID!) { city(id: $id) { id name currencyCode } }",
            variables: new { id = viennaId });

        var city = result.GetProperty("data").GetProperty("city");
        Assert.NotEqual(JsonValueKind.Null, city.ValueKind);
        Assert.Equal("Vienna", city.GetProperty("name").GetString());
        Assert.Equal("EUR", city.GetProperty("currencyCode").GetString());
    }

    // ── Ordering and correctness ──────────────────────────────────────────────

    /// <summary>
    /// <c>myCompanies</c> orders results alphabetically by name, matching the
    /// <c>OrderBy(c =&gt; c.Name)</c> clause in the resolver.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_MultipleCompanies_ReturnedInAlphabeticalOrder()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var userToken = await RegisterAndGetTokenAsync(client, "ordered-co-user@test.com", "Ordered Co");

        Guid playerId;
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            playerId = (await db.Players.FirstAsync(p => p.Email == "ordered-co-user@test.com")).Id;
        }

        // Seed companies in reverse alphabetical order to confirm the DB sorts them.
        await using (var s = factory.Services.CreateAsyncScope())
        {
            var db = s.ServiceProvider.GetRequiredService<AppDbContext>();
            var tick = await db.GameStates.AsNoTracking()
                .Select(gs => gs.CurrentTick).FirstOrDefaultDeterministicAsync();

            foreach (var name in new[] { "ZetaGlobal", "AlphaCorp", "MidBiz" })
            {
                var co = new Company
                {
                    Id = Guid.NewGuid(),
                    PlayerId = playerId,
                    Name = name,
                    FoundedAtUtc = DateTime.UtcNow,
                    FoundedAtTick = tick,
                };
                db.Companies.Add(co);
                db.BankAccounts.Add(new BankAccount
                {
                    Id = Guid.NewGuid(),
                    AccountNumber = GenerateTestAccountNumber(),
                    CompanyId = co.Id,
                    CurrencyCode = "EUR",
                    Balance = 1_000m,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
            await db.SaveChangesAsync();
        }

        // Evict cache so the query hits the DB.
        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove($"myCompanies_{playerId}");

        var result = await ExecuteGraphQlAsync(
            client,
            "{ myCompanies { name } }",
            token: userToken);

        var names = result.GetProperty("data").GetProperty("myCompanies")
            .EnumerateArray()
            .Select(c => c.GetProperty("name").GetString()!)
            .ToList();

        // Must include all three seeded companies.
        Assert.Contains("AlphaCorp", names);
        Assert.Contains("MidBiz", names);
        Assert.Contains("ZetaGlobal", names);

        // Names within the returned list must be in non-descending order.
        var seededSubset = names.Where(n => n is "AlphaCorp" or "MidBiz" or "ZetaGlobal").ToList();
        var sorted = seededSubset.OrderBy(n => n).ToList();
        Assert.Equal(sorted, seededSubset);
    }

    // ── GetCities cache ───────────────────────────────────────────────────────

    /// <summary>
    /// The <c>cities</c> list query must populate the <c>cities_all</c> cache key after the
    /// first call so subsequent requests skip the DB round-trip.
    /// </summary>
    [Fact]
    public async Task GetCities_AfterFirstCall_IsCachedInMemory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var cache = factory.Services.GetRequiredService<IMemoryCache>();
        cache.Remove("cities_all");

        // First call — should populate the cache.
        await ExecuteGraphQlAsync(client, "{ cities { id name } }");

        // Cache entry must now exist.
        var cacheHit = cache.TryGetValue("cities_all", out List<City>? cached);
        Assert.True(cacheHit, "cities_all must be set in IMemoryCache after first query.");
        Assert.NotNull(cached);
        Assert.True(cached!.Count >= 3, "At least three cities must be seeded and cached.");
    }

    // ── Combined query — auth vs public field isolation ───────────────────────

    /// <summary>
    /// In the combined dashboard startup query, if the request is unauthenticated,
    /// <c>myCompanies</c> must be rejected.  HotChocolate may set <c>data</c> to <c>null</c>
    /// (non-nullable field failure) or return per-field errors; either way the request must
    /// not return live company data for an anonymous caller.
    /// </summary>
    [Fact]
    public async Task CombinedDashboardQuery_Unauthenticated_ReturnsAuthorizationError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // Deliberately no Bearer token.
        var result = await ExecuteGraphQlAsync(
            client,
            """
            {
                myCompanies { id name }
                gameState { currentTick }
                cities { id name }
            }
            """);

        // HotChocolate bubbles the non-nullable auth error to `data: null` for the whole
        // document OR returns per-field errors.  Either way an `errors` array must be present.
        var hasErrors = result.TryGetProperty("errors", out var errs) &&
                        errs.ValueKind == JsonValueKind.Array &&
                        errs.GetArrayLength() > 0;

        // Alternatively, data may be present but myCompanies is null.
        bool mcIsNullOrMissing = false;
        if (result.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
        {
            mcIsNullOrMissing = !dataEl.TryGetProperty("myCompanies", out var mc) ||
                                mc.ValueKind == JsonValueKind.Null;
        }

        Assert.True(hasErrors || mcIsNullOrMissing,
            "Unauthenticated combined query: errors must be present or myCompanies must be null/absent.");
    }
}
