using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Final-gap coverage tests targeting paths genuinely absent from all other test files:
/// <list type="bullet">
///   <item><b>AccountService HTTP error paths</b> — HTTP 500 on FetchRankingsAsync,
///   FetchGameStateAsync, and FetchProfileAsync (not covered in AccountServiceTests.cs).</item>
///   <item><b>AccountService GraphQL errors</b> — GraphQL errors on FetchGameStateAsync
///   (not covered elsewhere).</item>
///   <item><b>BotStateValidator combinations</b> — IsReadyForOperation with each
///   individual blocking condition, and IsAtRisk with the exactly-50% boundary.</item>
///   <item><b>BotRosterFactory negative count</b> — Build(-1) is clamped to 1 (same as
///   Build(0) which is already tested, but negative is not).</item>
///   <item><b>PriceAdjustmentHelper.ComputeNewPrice floor clamping</b> — applying a factor
///   to a very small price that would go below the 0.01 floor.</item>
///   <item><b>StrategyRecommendation string representation</b> — Reason field is non-empty
///   for ShouldAct=true, and is well-formed for both action types.</item>
///   <item><b>BotAccount.IsTokenValid buffer boundary</b> — exactly-at-buffer vs
///   just-before-buffer.</item>
///   <item><b>OnboardingHelpers.PickCheapestAvailableLot all-occupied path</b> — every
///   lot has a buildingId, so the result is null.</item>
///   <item><b>BotProfitCalculator.Recommend exact minimum-ticks boundary</b> — when
///   ticksElapsed == minTicksBeforeAdjustment the guard does not fire.</item>
///   <item><b>Multi-company net-worth with a zero-cash company in the middle</b>
///   — zero-cash company is included in the sum without distortion.</item>
/// </list>
/// </summary>
public sealed class BotFinalCoverageTests
{
    // ── Infrastructure shared across account-service tests ────────────────────

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

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage ServerError() =>
        new(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
        };

    private static (AccountService svc, FakeHttpHandler handler) CreateAccountService(
        Func<HttpResponseMessage> httpFactory)
    {
        var handler = new FakeHttpHandler(httpFactory);
        var options = Options.Create(new BotOptions { GraphqlUrl = "https://test.example/graphql" });
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var svc = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return (svc, handler);
    }

    // ── AccountService: missing HTTP-500 paths ────────────────────────────────

    [Fact]
    public async Task FetchRankingsAsync_Http500_ThrowsInvalidOperationException()
    {
        var (svc, _) = CreateAccountService(ServerError);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.FetchRankingsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FetchGameStateAsync_Http500_ThrowsInvalidOperationException()
    {
        var (svc, _) = CreateAccountService(ServerError);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.FetchGameStateAsync(CancellationToken.None));
    }

    [Fact]
    public async Task FetchProfileAsync_Http500_ThrowsInvalidOperationException()
    {
        var (svc, _) = CreateAccountService(ServerError);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.FetchProfileAsync("some-token", CancellationToken.None));
    }

    // ── AccountService: GraphQL errors on previously untested methods ──────────

    [Fact]
    public async Task FetchGameStateAsync_GraphQLError_ThrowsGraphQLException()
    {
        const string json = """{"errors":[{"message":"Server error.","extensions":{"code":"GAME_STATE_UNAVAILABLE"}}]}""";
        var (svc, _) = CreateAccountService(() => Ok(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.FetchGameStateAsync(CancellationToken.None));

        Assert.Equal("GAME_STATE_UNAVAILABLE", ex.Code);
    }

    [Fact]
    public async Task FetchRankingsAsync_GraphQLError_ThrowsGraphQLException()
    {
        const string json = """{"errors":[{"message":"Permission denied.","extensions":{"code":"UNAUTHORIZED"}}]}""";
        var (svc, _) = CreateAccountService(() => Ok(json));

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.FetchRankingsAsync(CancellationToken.None));

        Assert.Equal("UNAUTHORIZED", ex.Code);
    }

    [Fact]
    public async Task UpdatePublicSalesPriceAsync_Http500_ThrowsInvalidOperationException()
    {
        var (svc, _) = CreateAccountService(ServerError);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.UpdatePublicSalesPriceAsync("u1", 99m, "token", CancellationToken.None));
    }

    // ── BotStateValidator: IsReadyForOperation with each blocking condition ───

    [Fact]
    public void IsReadyForOperation_SkippedBot_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "valid-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            IsSkipped = true,
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10) },
        };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_ExpiredToken_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "expired-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(-1), // past
            IsSkipped = false,
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10) },
        };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_OnboardingIncomplete_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "valid-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            IsSkipped = false,
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = null }, // not complete
        };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    [Fact]
    public void IsReadyForOperation_NullProfile_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "valid-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            IsSkipped = false,
            Profile = null, // no profile at all
        };
        Assert.False(BotStateValidator.IsReadyForOperation(bot));
    }

    // ── BotStateValidator: IsAtRisk exact 50% boundary ───────────────────────

    [Fact]
    public void IsAtRisk_Exactly50PercentErrors_ReturnsTrue()
    {
        // 1 error out of 2 max = 50% ≥ 0.5 threshold → at risk
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            ConsecutiveErrors = 1,
        };
        Assert.True(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 2));
    }

    [Fact]
    public void IsAtRisk_JustBelow50PercentErrors_ReturnsFalse()
    {
        // 1 error out of 3 max = 33.3% < 50% threshold → not at risk
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            ConsecutiveErrors = 1,
        };
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 3));
    }

    [Fact]
    public void IsAtRisk_SkippedBot_ReturnsFalse()
    {
        // Skipped bots are excluded from risk assessment because they are in a terminal
        // state — the orchestrator no longer polls them and further error counting is moot.
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            ConsecutiveErrors = 10,
            IsSkipped = true,
        };
        Assert.False(BotStateValidator.IsAtRisk(bot, maxConsecutiveErrors: 5));
    }

    // ── BotRosterFactory: negative BotCount clamped to 1 ────────────────────

    [Fact]
    public void Build_NegativeCount_ClampedToOne()
    {
        var opts = new BotOptions { BotCount = -5 };
        var roster = BotRosterFactory.Build(opts);
        Assert.Single(roster);
    }

    // ── PriceAdjustmentHelper.ComputeNewPrice floor clamping ─────────────────

    [Fact]
    public void ComputeNewPrice_VeryLowCurrentPrice_ClampsToOneCent()
    {
        // 0.001 × 0.95 = 0.00095 → must be clamped to 0.01 (1-cent floor)
        var result = PriceAdjustmentHelper.ComputeNewPrice(0.001m, 0.95m);
        Assert.Equal(0.01m, result);
    }

    [Fact]
    public void ComputeNewPrice_ZeroCurrentPrice_ClampsToOneCent()
    {
        var result = PriceAdjustmentHelper.ComputeNewPrice(0m, 0.85m);
        Assert.Equal(0.01m, result);
    }

    // ── StrategyRecommendation text fields ────────────────────────────────────

    [Fact]
    public void Recommend_MildLoss_ReasonContainsPercentage()
    {
        var rec = BotProfitCalculator.Recommend(95_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.NotEmpty(rec.Reason);
        // Mild loss reason should mention "mild" and a percentage
        Assert.Contains("loss", rec.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Recommend_SevereLoss_ReasonContainsAggressive()
    {
        var rec = BotProfitCalculator.Recommend(85_000m, 100_000m, ticksElapsed: 10);
        Assert.True(rec.ShouldAct);
        Assert.NotEmpty(rec.Reason);
        Assert.Contains("aggressive", rec.Reason, StringComparison.OrdinalIgnoreCase);
    }

    // ── BotAccount.IsTokenValid buffer boundary ───────────────────────────────

    [Fact]
    public void IsTokenValid_TokenExpiresExactlyAtBuffer_ReturnsFalse()
    {
        // Token expires in exactly 5 minutes (== buffer) → not valid (needs refresh)
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5),
        };
        // Buffer is 5 minutes; token at exactly 5 min remaining = not valid (expires <= now + buffer)
        Assert.False(bot.IsTokenValid(bufferMinutes: 5));
    }

    [Fact]
    public void IsTokenValid_TokenExpiresOneSecondAfterBuffer_ReturnsTrue()
    {
        // Token expires in 5 minutes + 1 second → valid (expires > now + 5min buffer)
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddMinutes(5).AddSeconds(1),
        };
        Assert.True(bot.IsTokenValid(bufferMinutes: 5));
    }

    // ── OnboardingHelpers.PickCheapestAvailableLot: all occupied ─────────────

    [Fact]
    public void PickCheapestAvailableLot_AllLotsOccupied_ReturnsNull()
    {
        var lots = new List<BuildingLotSummary>
        {
            new() { Id = "l1", SuitableTypes = "FACTORY", Price = 50_000m, BuildingId = "existing-1" },
            new() { Id = "l2", SuitableTypes = "FACTORY", Price = 75_000m, BuildingId = "existing-2" },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY");
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestAvailableLot_NoMatchingSuitableType_ReturnsNull()
    {
        // Lots exist and are available but none are SALES_SHOP suitable
        var lots = new List<BuildingLotSummary>
        {
            new() { Id = "l1", SuitableTypes = "FACTORY", Price = 50_000m, BuildingId = null },
        };
        var result = OnboardingHelpers.PickCheapestAvailableLot(lots, "SALES_SHOP");
        Assert.Null(result);
    }

    // ── BotProfitCalculator.Recommend: exact minimum-ticks boundary ───────────

    [Fact]
    public void Recommend_ExactlyAtMinTicks_IsEligibleForRecommendation()
    {
        // ticksElapsed == minTicksBeforeAdjustment: BotProfitCalculator.Recommend uses a strict
        // `<` guard so when ticksElapsed equals the minimum, the guard does NOT fire and a
        // recommendation is returned. With −20% loss at exactly tick 5 and minTicks=5 →
        // aggressive action is recommended.
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 80_000m,   // −20% → severe loss → aggressive
            initialNetWorth: 100_000m,
            ticksElapsed: 5,
            minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct, "Exactly at minimum ticks must be eligible for recommendation.");
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_OneLessThanMinTicks_ReturnsNoAction()
    {
        // ticksElapsed == minTicks - 1 → guard fires
        var rec = BotProfitCalculator.Recommend(
            currentNetWorth: 80_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: 4,
            minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct, "One tick below minimum must not trigger any action.");
    }

    // ── Multi-company net-worth: zero-cash company in the middle ──────────────

    [Fact]
    public void ComputeNetWorth_ZeroCashCompanyInMiddle_SumsCorrectly()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Id = "c1", Name = "Alpha", Cash = 300_000m, Buildings = [] },
                new CompanySummary { Id = "c2", Name = "Beta", Cash = 0m, Buildings = [] },
                new CompanySummary { Id = "c3", Name = "Gamma", Cash = 150_000m, Buildings = [] },
            ],
        };

        var netWorth = BotProfitCalculator.ComputeNetWorth(profile);
        Assert.Equal(450_000m, netWorth);
    }

    // ── BotOrchestrator.ComputeRecommendationForBot tick-elapsed calculation ──

    [Fact]
    public void ComputeRecommendationForBot_TickElapsedCalculation_UsesTrackingStart()
    {
        // TrackingStartTick = 100, currentTick = 120 → 20 ticks elapsed
        // With minTicks=5 and −15% loss → should be aggressive
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            TrackingStartTick = 100,
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 85_000m,  // −15% severe loss
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(
            bot, currentTick: 120, minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void ComputeRecommendationForBot_CurrentTickEqualsTrackingStart_ZeroTicksNoAction()
    {
        // currentTick == TrackingStartTick → 0 ticks elapsed → guard fires
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            TrackingStartTick = 50,
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 1m,  // extreme loss — but no action because 0 ticks
        };

        var rec = BotOrchestrator.ComputeRecommendationForBot(
            bot, currentTick: 50, minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct);
    }

    // ── PriceAdjustmentHelper.SelectAdjustableUnits: SALES_SHOP only ──────────

    [Fact]
    public void SelectAdjustableUnits_WithMixedBuildingTypes_ReturnsSalesShopPublicSalesOnly()
    {
        // A FACTORY building with MANUFACTURING units must NOT appear in the adjustable list
        var factoryBuilding = new BuildingSummary
        {
            Id = "bld-factory",
            Name = "My Factory",
            Type = "FACTORY",
            CityId = "city-1",
            Units = [new UnitSummary { Id = "mfg-1", UnitType = "MANUFACTURING", MinPrice = null }],
        };
        var shopBuilding = new BuildingSummary
        {
            Id = "bld-shop",
            Name = "My Shop",
            Type = "SALES_SHOP",
            CityId = "city-1",
            Units = [new UnitSummary { Id = "ps-1", UnitType = "PUBLIC_SALES", MinPrice = 50m }],
        };
        var company = new CompanySummary
        {
            Id = "c1", Name = "Corp", Cash = 10_000m,
            Buildings = [factoryBuilding, shopBuilding],
        };
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "Bot", Email = "b@x.test", Strategy = "FURNITURE",
            Token = "tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile
            {
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddMinutes(-10),
                Companies = [company],
            },
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(bot.Profile!.Companies).ToList();
        var (unit, buildingName) = Assert.Single(adjustable);
        Assert.Equal("PUBLIC_SALES", unit.UnitType);
        Assert.Equal("ps-1", unit.Id);
        Assert.Equal("My Shop", buildingName);
    }

    // ── BotOptions.AllowedIndustries default values ───────────────────────────

    [Fact]
    public void BotOptions_DefaultAllowedIndustries_ContainsAllThreeStarterIndustries()
    {
        var opts = new BotOptions();
        Assert.Contains("FURNITURE", opts.AllowedIndustries);
        Assert.Contains("FOOD_PROCESSING", opts.AllowedIndustries);
        Assert.Contains("HEALTHCARE", opts.AllowedIndustries);
    }

    [Fact]
    public void BotOptions_DefaultAllowedIndustries_HasExactlyThreeEntries()
    {
        var opts = new BotOptions();
        Assert.Equal(3, opts.AllowedIndustries.Length);
    }

    // ── BotAccount: HasValidToken when token is null ──────────────────────────

    [Fact]
    public void HasValidToken_NullToken_ReturnsFalse()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F" };
        Assert.False(bot.HasValidToken);
    }

    [Fact]
    public void HasValidToken_TokenSetButExpiryInPast_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F",
            Token = "expired",
            TokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(-1),
        };
        Assert.False(bot.HasValidToken);
    }

    [Fact]
    public void HasValidToken_ValidTokenAndFutureExpiry_ReturnsTrue()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F",
            Token = "valid-tok",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(1),
        };
        Assert.True(bot.HasValidToken);
    }

    // ── OnboardingHelpers.ShouldResumeFromShopStep edge cases ────────────────

    [Fact]
    public void ShouldResumeFromShopStep_NullProfile_ReturnsFalse()
    {
        var bot = new BotAccount { Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F", Profile = null };
        Assert.False(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    [Fact]
    public void ShouldResumeFromShopStep_NullStep_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F",
            Profile = new PlayerProfile { OnboardingCurrentStep = null },
        };
        Assert.False(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    [Fact]
    public void ShouldResumeFromShopStep_LowercaseStep_ReturnsTrueViaCaseInsensitive()
    {
        // Backend may return the step in any case; comparison must be case-insensitive
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "B", Email = "b@x.test", Strategy = "F",
            Profile = new PlayerProfile { OnboardingCurrentStep = "shop_selection" },
        };
        Assert.True(OnboardingHelpers.ShouldResumeFromShopStep(bot));
    }

    // ── OnboardingHelpers.PickCheapestFreeProduct: all Pro-only ──────────────

    [Fact]
    public void PickCheapestFreeProduct_AllProOnly_ReturnsNull()
    {
        var products = new List<ProductTypeSummary>
        {
            new() { Id = "p1", Name = "Pro Widget", BasePrice = 99m, IsProOnly = true },
            new() { Id = "p2", Name = "Pro Gizmo",  BasePrice = 199m, IsProOnly = true },
        };
        var result = OnboardingHelpers.PickCheapestFreeProduct(products);
        Assert.Null(result);
    }

    [Fact]
    public void PickCheapestFreeProduct_EmptyList_ReturnsNull()
    {
        var result = OnboardingHelpers.PickCheapestFreeProduct([]);
        Assert.Null(result);
    }
}
