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
/// Regression tests guarding the dashboard performance optimization:
/// <list type="bullet">
///   <item>
///     <description>
///       <c>GetMyCompanies</c> must be read-only — it must NOT invoke
///       <c>BuildingConfigurationService.ApplyDuePlansAsync</c>.  That write
///       path is the exclusive responsibility of the tick engine's
///       <c>BuildingUpgradePhase</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       A plan whose <c>AppliesAtTick</c> equals the current tick must remain
///       pending after a bare <c>myCompanies</c> query, and must only be cleared
///       after the tick engine runs.
///     </description>
///   </item>
/// </list>
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

    // ── tests ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Core regression: a building configuration plan that is due at <c>currentTick</c>
    /// must NOT be applied by a bare <c>myCompanies</c> GraphQL query.
    /// Only the tick engine (<c>ProcessTickAsync</c>) may apply due plans.
    /// </summary>
    [Fact]
    public async Task GetMyCompanies_WithDuePlan_DoesNotApplyPlan_PlanRemainsUntilTickEngineRuns()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        // ── 1. Register the user. ──
        var userToken = await RegisterAndGetTokenAsync(
            client, "dash-perf-user@test.com", "Perf User");

        // ── 2. Seed a company, building, and a due plan directly in the DB. ──
        Guid buildingId;
        long currentTick;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userPlayer = await db.Players.FirstAsync(
                p => p.Email == "dash-perf-user@test.com");
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            currentTick = await db.GameStates.AsNoTracking()
                .Select(gs => gs.CurrentTick)
                .FirstOrDefaultDeterministicAsync();

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = userPlayer.Id,
                Name = "DashPerfCorp",
                FoundedAtUtc = DateTime.UtcNow,
                FoundedAtTick = currentTick,
            };
            db.Companies.Add(company);

            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                // Use ticks-based number: unique within a test run, no external state needed.
                AccountNumber = (DateTime.UtcNow.Ticks % 1_000_000_000_000_0000L)
                    .ToString("D16"),
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
                Name = "DashPerfFactory",
                Level = 1,
            };
            db.Buildings.Add(building);
            buildingId = building.Id;

            // Plan that is due at the CURRENT tick (would be applied by old eager path).
            db.BuildingConfigurationPlans.Add(new BuildingConfigurationPlan
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                AppliesAtTick = currentTick,
                SubmittedAtTick = currentTick,
                SubmittedAtUtc = DateTime.UtcNow,
                TotalTicksRequired = 0,
            });

            await db.SaveChangesAsync();
        }

        // ── 3. Call myCompanies — must NOT apply the due plan (read-only). ──
        var readResult = await ExecuteGraphQlAsync(
            client,
            """
            {
                myCompanies {
                    buildings {
                        id
                        pendingConfiguration { id appliesAtTick }
                    }
                }
            }
            """,
            token: userToken);

        var buildings = readResult
            .GetProperty("data")
            .GetProperty("myCompanies")
            .EnumerateArray()
            .SelectMany(c => c.GetProperty("buildings").EnumerateArray())
            .ToList();

        var dashBuilding = buildings.FirstOrDefault(
            b => b.GetProperty("id").GetString() == buildingId.ToString());
        Assert.NotEqual(JsonValueKind.Undefined, dashBuilding.ValueKind);

        // ── Assertion A: plan visible in GraphQL response (not eagerly wiped). ──
        var pendingAfterRead = dashBuilding.GetProperty("pendingConfiguration");
        Assert.NotEqual(JsonValueKind.Null, pendingAfterRead.ValueKind);
        Assert.Equal(currentTick, pendingAfterRead.GetProperty("appliesAtTick").GetInt64());

        // ── Assertion B: plan is STILL IN THE DATABASE after the read call. ──
        // The read must not have triggered any SaveChangesAsync / plan deletion.
        await using (var checkScope = factory.Services.CreateAsyncScope())
        {
            var db2 = checkScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var planStillExists = await db2.BuildingConfigurationPlans
                .AnyAsync(p => p.BuildingId == buildingId);
            Assert.True(
                planStillExists,
                "GetMyCompanies must not delete (apply) the due plan; " +
                "plan must remain in the DB after a read-only query.");
        }

        // ── 4. Run the tick engine — the ONLY legitimate applier. ──
        await using (var tickScope = factory.Services.CreateAsyncScope())
        {
            var db3 = tickScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = tickScope.ServiceProvider.GetServices<ITickPhase>();
            var processor = new TickProcessor(
                db3, phases, new NullLogger<TickProcessor>());
            await processor.ProcessTickAsync();
        }

        // ── Assertion C: plan is GONE after the tick engine ran. ──
        await using (var finalScope = factory.Services.CreateAsyncScope())
        {
            var db4 = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var planGoneAfterTick = !await db4.BuildingConfigurationPlans
                .AnyAsync(p => p.BuildingId == buildingId);
            Assert.True(
                planGoneAfterTick,
                "BuildingUpgradePhase must apply and remove the due plan after ProcessTickAsync.");
        }
    }
}
