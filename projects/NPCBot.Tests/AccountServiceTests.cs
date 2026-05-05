using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="AccountService"/> using an in-process
/// <see cref="FakeHttpHandler"/> so no real HTTP calls are made.
///
/// <para>
/// <b>RegisterOrLoginAsync</b> — registration success, DUPLICATE_EMAIL fallback to login,
/// and non-recoverable GraphQL errors are all covered.
/// </para>
/// <para>
/// <b>LoginAsync</b> — happy path, HTTP 500, and GraphQL LOGIN_FAILED error.
/// </para>
/// <para>
/// <b>FetchProfileAsync</b> — deserialises the PlayerProfile from the <c>me</c> query,
/// including companies and nested unit summaries.
/// </para>
/// <para>
/// <b>FetchGameStateAsync</b> — deserialises the GameStateSummary from the
/// <c>gameState</c> query.
/// </para>
/// <para>
/// <b>FetchRankingsAsync</b> — returns a populated list of RankingEntry values.
/// </para>
/// <para>
/// <b>UpdatePublicSalesPriceAsync</b> — happy path returns the updated UnitSummary;
/// GraphQL UNIT_NOT_FOUND error propagates as GraphQLException.
/// </para>
/// </summary>
public sealed class AccountServiceTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __)
        {
            CallCount++;
            return Task.FromResult(factory());
        }
    }

    private static (AccountService service, FakeHttpHandler handler) CreateService(
        Func<HttpResponseMessage> factory)
    {
        var handler = new FakeHttpHandler(factory);
        var options = Options.Create(new BotOptions
        {
            BotPassword = "test-password",
            GraphqlUrl = "https://test.example/graphql",
        });
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var svc = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return (svc, handler);
    }

    private static HttpResponseMessage OkResponse(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    // ── RegisterOrLoginAsync — registration success ───────────────────────────

    [Fact]
    public async Task RegisterOrLoginAsync_RegistrationSucceeds_ReturnsToken()
    {
        const string json = """
            {"data":{"register":{"token":"tok-abc","expiresAtUtc":"2099-01-01T00:00:00Z",
            "player":{"id":"p1","displayName":"NPC_01","email":"npc01@test.example",
            "onboardingCompletedAtUtc":null}}}}
            """;

        var (svc, handler) = CreateService(() => OkResponse(json));

        var bot = new BotAccount { Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example", Strategy = "FURNITURE" };
        var (token, expires) = await svc.RegisterOrLoginAsync(bot, CancellationToken.None);

        Assert.Equal("tok-abc", token);
        Assert.Equal(1, handler.CallCount); // one HTTP call for register
        Assert.True(expires > DateTime.UtcNow);
    }

    [Fact]
    public async Task RegisterOrLoginAsync_DuplicateEmail_FallsBackToLogin()
    {
        // First call → DUPLICATE_EMAIL; second call → login success
        int callCount = 0;
        const string errorJson = """
            {"errors":[{"message":"Email already taken.","extensions":{"code":"DUPLICATE_EMAIL"}}]}
            """;
        const string loginJson = """
            {"data":{"login":{"token":"tok-login","expiresAtUtc":"2099-06-01T00:00:00Z",
            "player":{"id":"p2","displayName":"NPC_02","email":"npc02@test.example",
            "onboardingCompletedAtUtc":null}}}}
            """;

        var (svc, _) = CreateService(() =>
        {
            callCount++;
            return OkResponse(callCount == 1 ? errorJson : loginJson);
        });

        var bot = new BotAccount { Index = 2, DisplayName = "NPC_02", Email = "npc02@test.example", Strategy = "HEALTHCARE" };
        var (token, _) = await svc.RegisterOrLoginAsync(bot, CancellationToken.None);

        Assert.Equal("tok-login", token);
        Assert.Equal(2, callCount); // register + login
    }

    [Fact]
    public async Task RegisterOrLoginAsync_NonDuplicateGraphQLError_Throws()
    {
        const string json = """
            {"errors":[{"message":"Server error.","extensions":{"code":"INTERNAL_ERROR"}}]}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));

        var bot = new BotAccount { Index = 3, DisplayName = "NPC_03", Email = "npc03@test.example", Strategy = "FURNITURE" };

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.RegisterOrLoginAsync(bot, CancellationToken.None));

        Assert.Equal("INTERNAL_ERROR", ex.Code);
    }

    // ── LoginAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoginAsync_Success_ReturnsTokenAndExpiry()
    {
        const string json = """
            {"data":{"login":{"token":"tok-xyz","expiresAtUtc":"2099-12-31T00:00:00Z",
            "player":{"id":"p4","displayName":"NPC_04","email":"npc04@test.example",
            "onboardingCompletedAtUtc":"2024-01-01T00:00:00Z"}}}}
            """;

        var (svc, handler) = CreateService(() => OkResponse(json));

        var bot = new BotAccount { Index = 4, DisplayName = "NPC_04", Email = "npc04@test.example", Strategy = "FURNITURE" };
        var (token, expires) = await svc.LoginAsync(bot, CancellationToken.None);

        Assert.Equal("tok-xyz", token);
        Assert.Equal(1, handler.CallCount);
        Assert.True(expires > DateTime.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_Http500_Throws()
    {
        var (svc, _) = CreateService(() =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
            });

        var bot = new BotAccount { Index = 5, DisplayName = "NPC_05", Email = "npc05@test.example", Strategy = "FURNITURE" };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.LoginAsync(bot, CancellationToken.None));
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsGraphQLException()
    {
        const string json = """
            {"errors":[{"message":"Invalid credentials.","extensions":{"code":"LOGIN_FAILED"}}]}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));

        var bot = new BotAccount { Index = 6, DisplayName = "NPC_06", Email = "npc06@test.example", Strategy = "FURNITURE" };

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.LoginAsync(bot, CancellationToken.None));

        Assert.Equal("LOGIN_FAILED", ex.Code);
    }

    // ── FetchProfileAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task FetchProfileAsync_ReturnsDisplayNameAndEmail()
    {
        const string json = """
            {"data":{"me":{"id":"p7","displayName":"NPC_07","email":"npc07@test.example",
            "onboardingCompletedAtUtc":"2024-03-01T00:00:00Z",
            "onboardingCurrentStep":null,"onboardingIndustry":null,
            "onboardingCityId":null,"onboardingCompanyId":null,
            "onboardingFactoryLotId":null,"onboardingShopBuildingId":null,
            "companies":[]}}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var profile = await svc.FetchProfileAsync("tok-valid", CancellationToken.None);

        Assert.Equal("NPC_07", profile.DisplayName);
        Assert.Equal("npc07@test.example", profile.Email);
        Assert.NotNull(profile.OnboardingCompletedAtUtc);
        Assert.Empty(profile.Companies);
    }

    [Fact]
    public async Task FetchProfileAsync_WithCompaniesAndUnits_DeserializesNested()
    {
        const string json = """
            {"data":{"me":{"id":"p8","displayName":"NPC_08","email":"npc08@test.example",
            "onboardingCompletedAtUtc":"2024-04-01T00:00:00Z",
            "onboardingCurrentStep":null,"onboardingIndustry":"FURNITURE",
            "onboardingCityId":"city-bts","onboardingCompanyId":"co-1",
            "onboardingFactoryLotId":"lot-f1","onboardingShopBuildingId":"bld-s1",
            "companies":[{"id":"co-1","name":"Bot Corp","cash":5000.00,
              "buildings":[{"id":"bld-s1","name":"Shop A","type":"SALES_SHOP","cityId":"city-bts",
                "units":[{"id":"u1","unitType":"PUBLIC_SALES","minPrice":99.99}]}]}]}}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var profile = await svc.FetchProfileAsync("tok-valid", CancellationToken.None);

        Assert.Single(profile.Companies);
        var company = profile.Companies[0];
        Assert.Equal("Bot Corp", company.Name);
        Assert.Single(company.Buildings);
        var building = company.Buildings[0];
        Assert.Equal("SALES_SHOP", building.Type);
        Assert.Single(building.Units);
        var unit = building.Units[0];
        Assert.Equal("PUBLIC_SALES", unit.UnitType);
        Assert.Equal(99.99m, unit.MinPrice);
    }

    [Fact]
    public async Task FetchProfileAsync_OnboardingNotComplete_NullCompletedDate()
    {
        const string json = """
            {"data":{"me":{"id":"p9","displayName":"NPC_09","email":"npc09@test.example",
            "onboardingCompletedAtUtc":null,
            "onboardingCurrentStep":null,"onboardingIndustry":null,
            "onboardingCityId":null,"onboardingCompanyId":null,
            "onboardingFactoryLotId":null,"onboardingShopBuildingId":null,
            "companies":[]}}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var profile = await svc.FetchProfileAsync("tok-valid", CancellationToken.None);

        Assert.Null(profile.OnboardingCompletedAtUtc);
    }

    // ── FetchGameStateAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task FetchGameStateAsync_ReturnsTickAndInterval()
    {
        const string json = """
            {"data":{"gameState":{"currentTick":9120,"tickIntervalSeconds":30,"taxCycleTicks":8760}}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var gs = await svc.FetchGameStateAsync(CancellationToken.None);

        Assert.Equal(9120L, gs.CurrentTick);
        Assert.Equal(30, gs.TickIntervalSeconds);
        Assert.Equal(8760, gs.TaxCycleTicks);
    }

    [Fact]
    public async Task FetchGameStateAsync_ZeroTick_ReturnsZero()
    {
        const string json = """
            {"data":{"gameState":{"currentTick":0,"tickIntervalSeconds":60,"taxCycleTicks":8760}}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var gs = await svc.FetchGameStateAsync(CancellationToken.None);

        Assert.Equal(0L, gs.CurrentTick);
        Assert.Equal(60, gs.TickIntervalSeconds);
    }

    // ── FetchRankingsAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task FetchRankingsAsync_ReturnsRankedList()
    {
        const string json = """
            {"data":{"rankings":[
              {"rank":1,"displayName":"Top Player","netWorth":999999.99},
              {"rank":2,"displayName":"NPC_01","netWorth":12345.00}
            ]}}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));
        var rankings = await svc.FetchRankingsAsync(CancellationToken.None);

        Assert.Equal(2, rankings.Count);
        Assert.Equal(1, rankings[0].Rank);
        Assert.Equal("Top Player", rankings[0].DisplayName);
        Assert.Equal(999999.99m, rankings[0].NetWorth);
        Assert.Equal(2, rankings[1].Rank);
    }

    [Fact]
    public async Task FetchRankingsAsync_EmptyLeaderboard_ReturnsEmptyList()
    {
        const string json = """{"data":{"rankings":[]}}""";

        var (svc, _) = CreateService(() => OkResponse(json));
        var rankings = await svc.FetchRankingsAsync(CancellationToken.None);

        Assert.Empty(rankings);
    }

    // ── UpdatePublicSalesPriceAsync ───────────────────────────────────────────

    [Fact]
    public async Task UpdatePublicSalesPriceAsync_Success_ReturnsUpdatedUnit()
    {
        const string json = """
            {"data":{"updatePublicSalesPrice":{"id":"unit-42","unitType":"PUBLIC_SALES","minPrice":87.50}}}
            """;

        var (svc, handler) = CreateService(() => OkResponse(json));
        var unit = await svc.UpdatePublicSalesPriceAsync("unit-42", 87.50m, "tok-valid", CancellationToken.None);

        Assert.Equal("unit-42", unit.Id);
        Assert.Equal("PUBLIC_SALES", unit.UnitType);
        Assert.Equal(87.50m, unit.MinPrice);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task UpdatePublicSalesPriceAsync_UnitNotFound_ThrowsGraphQLException()
    {
        const string json = """
            {"errors":[{"message":"Unit not found.","extensions":{"code":"UNIT_NOT_FOUND"}}]}
            """;

        var (svc, _) = CreateService(() => OkResponse(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.UpdatePublicSalesPriceAsync("bad-id", 50m, "tok-valid", CancellationToken.None));

        Assert.Equal("UNIT_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task UpdatePublicSalesPriceAsync_SendsBearerToken()
    {
        string? capturedAuth = null;
        const string json = """
            {"data":{"updatePublicSalesPrice":{"id":"u99","unitType":"PUBLIC_SALES","minPrice":10.0}}}
            """;

        var handler = new CapturingFakeHttpHandler(_ => OkResponse(json));
        handler.OnRequest += req =>
            capturedAuth = req.Headers.Authorization?.Parameter;

        var options = Options.Create(new BotOptions { GraphqlUrl = "https://test.example/graphql" });
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var svc = new AccountService(api, options, NullLogger<AccountService>.Instance);

        await svc.UpdatePublicSalesPriceAsync("u99", 10m, "my-bearer-token", CancellationToken.None);

        Assert.Equal("my-bearer-token", capturedAuth);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed class CapturingFakeHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage> factory) : HttpMessageHandler
    {
        public event Action<HttpRequestMessage>? OnRequest;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken _)
        {
            OnRequest?.Invoke(request);
            return Task.FromResult(factory(request));
        }
    }
}
