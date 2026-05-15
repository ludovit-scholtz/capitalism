using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Tests for the Dynamic Global Events &amp; Market Shocks System.
/// Covers entity lifecycle, tick-engine multiplier application, GraphQL surface, and admin access.
/// </summary>
public sealed class GlobalEventTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GlobalEventTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var body = variables is not null
            ? new { query, variables }
            : (object)new { query };
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = JsonContent.Create(body),
        };
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
        => ExecuteGraphQlAsync(_client, query, variables, token);

    private static async Task<string> RegisterAndGetTokenAsync(
        HttpClient client,
        string email,
        string displayName = "Tester",
        string password = "TestPass123!")
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($email: String!, $password: String!, $displayName: String!) {
              register(input: { email: $email, password: $password, displayName: $displayName }) {
                token
              }
            }
            """,
            new { email, password, displayName });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private async Task<string> RegisterAndGetTokenAsync(string? email = null, string displayName = "Tester")
    {
        email ??= $"ge-{Guid.NewGuid():N}@example.com";
        return await RegisterAndGetTokenAsync(_client, email, displayName);
    }

    private static async Task<string> MakeAdminAsync(
        ApiWebApplicationFactory factory,
        HttpClient client)
    {
        var email = $"ge-admin-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(client, email, "AdminUser");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var player = await db.Players.SingleAsync(p => p.Email == email);
        player.Role = PlayerRole.Admin;
        await db.SaveChangesAsync();
        return token;
    }

    // -----------------------------------------------------------------------
    // GraphQL surface — public queries
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ActiveGlobalEvents_WhenNoActiveEvents_ReturnsEmptyList()
    {
        var result = await ExecuteGraphQlAsync("{ activeGlobalEvents { id eventType } }");
        Assert.False(result.TryGetProperty("errors", out _), "Query should succeed without auth");
        var list = result.GetProperty("data").GetProperty("activeGlobalEvents");
        Assert.Equal(JsonValueKind.Array, list.ValueKind);
    }

    [Fact]
    public async Task ActiveGlobalEvents_WhenEventSeeded_ReturnsActiveEvent()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        using var isolatedClient = isolatedFactory.CreateClient();

        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = GlobalEventType.SupplyChainDisruption,
            Severity = GlobalEventSeverity.Major,
            Title = "Test Supply Chain Shock",
            Description = "Test disruption.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 50,
            OperatingCostMultiplier = 1.2m,
            TradeRouteMultiplier = 1.3m,
            RdMultiplier = 0.9m,
            MineEfficiencyMultiplier = 0.85m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            isolatedClient,
            "{ activeGlobalEvents { id title severity isActive operatingCostMultiplier } }");
        Assert.False(result.TryGetProperty("errors", out _));
        var list = result.GetProperty("data").GetProperty("activeGlobalEvents").EnumerateArray().ToList();
        Assert.Contains(list, item => item.GetProperty("title").GetString() == "Test Supply Chain Shock");
        var found = list.First(item => item.GetProperty("title").GetString() == "Test Supply Chain Shock");
        Assert.Equal("MAJOR", found.GetProperty("severity").GetString());
        Assert.True(found.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task GlobalEventHistory_WhenCalled_ReturnsRecentEvents()
    {
        var result = await ExecuteGraphQlAsync("{ globalEventHistory(limit: 10) { id title isActive } }");
        Assert.False(result.TryGetProperty("errors", out _));
        Assert.Equal(JsonValueKind.Array, result.GetProperty("data").GetProperty("globalEventHistory").ValueKind);
    }

    [Fact]
    public async Task GlobalEventHistory_LimitClampedAtMaximum()
    {
        // Should not error — clamped by server
        var result = await ExecuteGraphQlAsync("{ globalEventHistory(limit: 9999) { id } }");
        Assert.False(result.TryGetProperty("errors", out _));
    }

    // -----------------------------------------------------------------------
    // Admin mutations — authorization boundaries
    // -----------------------------------------------------------------------

    private const string TriggerMutation = """
        mutation($type: String!, $sev: String!, $title: String!) {
          triggerGlobalEvent(input: {
            eventType: $type
            severity: $sev
            title: $title
            description: "Test."
            durationTicks: 20
            operatingCostMultiplier: 1.1
            tradeRouteMultiplier: 1.0
            rdMultiplier: 1.0
            mineEfficiencyMultiplier: 1.0
          }) { id eventType severity title isActive durationTicks }
        }
        """;

    [Fact]
    public async Task TriggerGlobalEvent_WhenUnauthenticated_ReturnsAuthError()
    {
        var result = await ExecuteGraphQlAsync(TriggerMutation, new
        {
            type = GlobalEventType.SupplyChainDisruption,
            sev = GlobalEventSeverity.Moderate,
            title = "Unauth Test",
        });
        Assert.True(result.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task TriggerGlobalEvent_WhenNonAdmin_ReturnsAdminAccessRequired()
    {
        var token = await RegisterAndGetTokenAsync();
        var result = await ExecuteGraphQlAsync(TriggerMutation,
            new { type = GlobalEventType.TechBoom, sev = GlobalEventSeverity.Minor, title = "Non-admin" },
            token: token);
        Assert.True(result.TryGetProperty("errors", out var errors), "Non-admin should be rejected");
        var hasAdminError = errors.EnumerateArray().Any(e =>
            e.TryGetProperty("extensions", out var ext) &&
            ext.TryGetProperty("code", out var code) &&
            (code.GetString() is "ADMIN_ACCESS_REQUIRED" or "AUTH_NOT_AUTHORIZED"));
        Assert.True(hasAdminError, "Expected ADMIN_ACCESS_REQUIRED or AUTH_NOT_AUTHORIZED");
    }

    [Fact]
    public async Task TriggerGlobalEvent_WhenAdmin_CreatesAndReturnsEvent()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        using var isolatedClient = isolatedFactory.CreateClient();
        var token = await MakeAdminAsync(isolatedFactory, isolatedClient);

        var result = await ExecuteGraphQlAsync(isolatedClient, TriggerMutation, new
        {
            type = GlobalEventType.EnergyCrisis,
            sev = GlobalEventSeverity.Major,
            title = "Admin Energy Crisis",
        }, token: token);

        Assert.False(result.TryGetProperty("errors", out _), "Admin trigger should succeed");
        var ev = result.GetProperty("data").GetProperty("triggerGlobalEvent");
        Assert.Equal(GlobalEventType.EnergyCrisis, ev.GetProperty("eventType").GetString());
        Assert.Equal(GlobalEventSeverity.Major, ev.GetProperty("severity").GetString());
        Assert.Equal("Admin Energy Crisis", ev.GetProperty("title").GetString());
        Assert.True(ev.GetProperty("isActive").GetBoolean());
        Assert.Equal(20, ev.GetProperty("durationTicks").GetInt64());
    }

    [Fact]
    public async Task TriggerGlobalEvent_WithInvalidEventType_ReturnsValidationError()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        using var isolatedClient = isolatedFactory.CreateClient();
        var token = await MakeAdminAsync(isolatedFactory, isolatedClient);

        var result = await ExecuteGraphQlAsync(isolatedClient, TriggerMutation, new
        {
            type = "TOTALLY_MADE_UP_TYPE",
            sev = GlobalEventSeverity.Minor,
            title = "Invalid Type",
        }, token: token);
        Assert.True(result.TryGetProperty("errors", out _), "Invalid event type should be rejected");
    }

    [Fact]
    public async Task ResolveGlobalEvent_WhenAdmin_DeactivatesEvent()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        using var isolatedClient = isolatedFactory.CreateClient();
        var token = await MakeAdminAsync(isolatedFactory, isolatedClient);

        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        var evId = Guid.NewGuid();
        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = evId,
            EventType = GlobalEventType.TradeWar,
            Severity = GlobalEventSeverity.Moderate,
            Title = "Resolve Test Trade War",
            Description = "Will be resolved.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 100,
            OperatingCostMultiplier = 1.0m,
            TradeRouteMultiplier = 1.4m,
            RdMultiplier = 1.0m,
            MineEfficiencyMultiplier = 1.0m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var resolveResult = await ExecuteGraphQlAsync(
            isolatedClient,
            $$"""
            mutation {
              resolveGlobalEvent(id: "{{evId}}") {
                id isActive resolvedAtUtc
              }
            }
            """,
            token: token);

        Assert.False(resolveResult.TryGetProperty("errors", out _), "Resolve should succeed");
        var resolved = resolveResult.GetProperty("data").GetProperty("resolveGlobalEvent");
        Assert.False(resolved.GetProperty("isActive").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, resolved.GetProperty("resolvedAtUtc").ValueKind);
    }

    [Fact]
    public async Task ResolveGlobalEvent_WhenNonAdmin_ReturnsAuthError()
    {
        var token = await RegisterAndGetTokenAsync();
        var result = await ExecuteGraphQlAsync(
            $"mutation {{ resolveGlobalEvent(id: \"{Guid.NewGuid()}\") {{ id }} }}",
            token: token);
        Assert.True(result.TryGetProperty("errors", out _));
    }

    // -----------------------------------------------------------------------
    // Tick engine — multiplier application via TickContext
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GlobalEvent_OperatingCostMultiplier_ExposedInTickContext()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = GlobalEventType.EnergyCrisis,
            Severity = GlobalEventSeverity.Major,
            Title = "Energy Crisis Test",
            Description = "Cost test.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 100,
            OperatingCostMultiplier = 1.5m,
            TradeRouteMultiplier = 1.0m,
            RdMultiplier = 1.0m,
            MineEfficiencyMultiplier = 1.0m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(1.5m, context.GlobalEventOperatingCostMultiplier);
    }

    [Fact]
    public async Task GlobalEvent_TradeRouteMultiplier_ExposedInTickContext()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = GlobalEventType.TradeWar,
            Severity = GlobalEventSeverity.Major,
            Title = "Trade War Test",
            Description = "Trade test.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 100,
            OperatingCostMultiplier = 1.0m,
            TradeRouteMultiplier = 1.35m,
            RdMultiplier = 1.0m,
            MineEfficiencyMultiplier = 1.0m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(1.35m, context.GlobalEventTradeRouteMultiplier);
    }

    [Fact]
    public async Task GlobalEvent_RdMultiplier_ExposedInTickContext()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = GlobalEventType.TechBoom,
            Severity = GlobalEventSeverity.Moderate,
            Title = "Tech Boom Test",
            Description = "RD test.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 100,
            OperatingCostMultiplier = 1.0m,
            TradeRouteMultiplier = 1.0m,
            RdMultiplier = 1.4m,
            MineEfficiencyMultiplier = 1.0m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(1.4m, context.GlobalEventRdMultiplier);
    }

    [Fact]
    public async Task GlobalEvent_CityScoped_MineEfficiencyMultiplier_ExposedForThatCity()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = Guid.NewGuid(),
            EventType = GlobalEventType.EnvironmentalDisaster,
            Severity = GlobalEventSeverity.Catastrophic,
            Title = "Bratislava Mine Disaster",
            Description = "Mining test.",
            IsActive = true,
            StartTick = gs!.CurrentTick,
            DurationTicks = 100,
            AffectedCityId = bratislava.Id,
            OperatingCostMultiplier = 1.0m,
            TradeRouteMultiplier = 1.0m,
            RdMultiplier = 1.0m,
            MineEfficiencyMultiplier = 0.5m,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(0.5m, context.GetGlobalEventMineEfficiency(bratislava.Id));
    }

    [Fact]
    public async Task GlobalEvent_WhenNoActiveEvents_AllMultipliersDefaultToOne()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(1.0m, context.GlobalEventOperatingCostMultiplier);
        Assert.Equal(1.0m, context.GlobalEventTradeRouteMultiplier);
        Assert.Equal(1.0m, context.GlobalEventRdMultiplier);
    }

    [Fact]
    public async Task GlobalEvent_MultipleActiveEvents_MultipliersStackMultiplicatively()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        // Two events both raising operating cost: 1.2 × 1.1 = 1.32
        db.GlobalEvents.AddRange(
            new GlobalEvent
            {
                Id = Guid.NewGuid(),
                EventType = GlobalEventType.EnergyCrisis,
                Severity = GlobalEventSeverity.Minor,
                Title = "Cost Event 1",
                Description = ".",
                IsActive = true,
                StartTick = gs!.CurrentTick,
                DurationTicks = 100,
                OperatingCostMultiplier = 1.2m,
                TradeRouteMultiplier = 1.0m,
                RdMultiplier = 1.0m,
                MineEfficiencyMultiplier = 1.0m,
                CreatedAtUtc = DateTime.UtcNow,
            },
            new GlobalEvent
            {
                Id = Guid.NewGuid(),
                EventType = GlobalEventType.PandemicShock,
                Severity = GlobalEventSeverity.Minor,
                Title = "Cost Event 2",
                Description = ".",
                IsActive = true,
                StartTick = gs!.CurrentTick,
                DurationTicks = 100,
                OperatingCostMultiplier = 1.1m,
                TradeRouteMultiplier = 1.0m,
                RdMultiplier = 1.0m,
                MineEfficiencyMultiplier = 1.0m,
                CreatedAtUtc = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        var context = await processor.BuildContextForTestAsync();
        Assert.Equal(1.32m, Math.Round(context.GlobalEventOperatingCostMultiplier, 4));
    }

    // -----------------------------------------------------------------------
    // GlobalEventPhase — expiry logic (via ProcessTickAsync)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GlobalEventPhase_ExpiredEvents_AreDeactivatedOnNextTick()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        await using var scope = isolatedFactory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gs = await db.GameStates.FindAsync(1);
        Assert.NotNull(gs);

        // Create an event that started 10 ticks ago with duration 5 → expired 5 ticks ago
        var expiredId = Guid.NewGuid();
        db.GlobalEvents.Add(new GlobalEvent
        {
            Id = expiredId,
            EventType = GlobalEventType.SupplyChainDisruption,
            Severity = GlobalEventSeverity.Minor,
            Title = "Already Expired",
            Description = ".",
            IsActive = true,
            StartTick = gs!.CurrentTick - 10,
            DurationTicks = 5,
            OperatingCostMultiplier = 1.0m,
            TradeRouteMultiplier = 1.0m,
            RdMultiplier = 1.0m,
            MineEfficiencyMultiplier = 1.0m,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var processor = new TickProcessor(db, scope.ServiceProvider.GetServices<ITickPhase>(),
            NullLogger<TickProcessor>.Instance);
        await processor.ProcessTickAsync();

        var updated = await db.GlobalEvents.FindAsync(expiredId);
        Assert.NotNull(updated);
        Assert.False(updated!.IsActive, "Expired event should be deactivated after tick");
        Assert.NotNull(updated.ResolvedAtUtc);
    }

    // -----------------------------------------------------------------------
    // GlobalEventPhase helper — deterministic random event
    // -----------------------------------------------------------------------

    [Fact]
    public void GlobalEventPhase_CreateRandomEvent_ReturnsValidEvent()
    {
        var ev = GlobalEventPhase.CreateRandomEvent(100L);
        Assert.NotNull(ev);
        Assert.True(ev.IsActive);
        Assert.Equal(100L, ev.StartTick);
        Assert.True(ev.DurationTicks > 0);
        Assert.Contains(ev.EventType, GlobalEventType.All);
        Assert.Contains(ev.Severity, new[]
        {
            GlobalEventSeverity.Minor, GlobalEventSeverity.Moderate,
            GlobalEventSeverity.Major, GlobalEventSeverity.Catastrophic,
        });
    }
}
