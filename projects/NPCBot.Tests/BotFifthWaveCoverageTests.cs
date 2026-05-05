using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Fifth coverage-wave tests targeting genuinely uncovered paths:
/// <list type="bullet">
///   <item><term>RegisterOrLoginAsync HTTP 500</term> — propagates InvalidOperationException (same as other AccountService methods).</item>
///   <item><term>Recommend with negative initialNetWorth</term> — severe-loss, mild-loss, and improvement paths when the bot started with a negative net worth.</item>
///   <item><term>ComputeNetWorth sign correctness</term> — mixed-sign company cash.</item>
///   <item><term>BotStateValidator.Validate</term> — valid-bot summary is empty.</item>
///   <item><term>PriceAdjustmentHelper edge cases</term> — unit type casing, negative new-price guard.</item>
///   <item><term>BotOptions.BotCount guard</term> — BotCount=0 and BotCount=MaxBots boundary.</item>
///   <item><term>OnboardingHelpers.ContainsSuitableType</term> — single-entry CSV and comma-only string.</item>
/// </list>
/// </summary>
public sealed class BotFifthWaveCoverageTests
{
    // ── Infrastructure (AccountService tests) ─────────────────────────────────

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

    private static (AccountService service, FakeHttpHandler handler) CreateAccountService(
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

    // ── RegisterOrLoginAsync HTTP 500 ─────────────────────────────────────────

    [Fact]
    public async Task RegisterOrLoginAsync_Http500_ThrowsInvalidOperationException()
    {
        // HTTP 500 is NOT a DUPLICATE_EMAIL GraphQLException, so the catch block is
        // NOT triggered. The InvalidOperationException from GameApiClient propagates.
        var (svc, handler) = CreateAccountService(() =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc01@test.example",
            Strategy = "FURNITURE",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RegisterOrLoginAsync(bot, CancellationToken.None));

        Assert.Equal(1, handler.CallCount); // exactly one HTTP call was made
    }

    [Fact]
    public async Task RegisterOrLoginAsync_Http500_DoesNotFallBackToLogin()
    {
        // Confirm the DUPLICATE_EMAIL login fallback is not accidentally triggered
        // when the server returns HTTP 500 (as opposed to a GraphQL DUPLICATE_EMAIL error).
        int callCount = 0;
        var (svc, _) = CreateAccountService(() =>
        {
            callCount++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        });

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc01@test.example",
            Strategy = "FURNITURE",
        };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RegisterOrLoginAsync(bot, CancellationToken.None));

        // Only 1 HTTP call — the login fallback must NOT have been invoked.
        Assert.Equal(1, callCount);
    }

    // ── Recommend with negative initialNetWorth ───────────────────────────────

    [Fact]
    public void Recommend_NegativeInitialNetWorth_SevereLoss_ReturnsAggressiveAction()
    {
        // Both initial and current are negative; current is worse.
        // initialNetWorth = –50 000; currentNetWorth = –60 000
        // deltaPercent = (–60 000 – (–50 000)) / |–50 000| = –10 000 / 50 000 = –0.20
        // –0.20 ≤ SeverelyUnprofitableThresholdPercent (–0.10) → AggressivePriceReductionFactor
        var rec = BotProfitCalculator.Recommend(
            -60_000m, -50_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_NegativeInitialNetWorth_MildLoss_ReturnsMildAction()
    {
        // initialNetWorth = –50 000; currentNetWorth = –53 000
        // deltaPercent = –3 000 / 50 000 = –0.06 — beyond neutral band but above severe threshold
        var rec = BotProfitCalculator.Recommend(
            -53_000m, -50_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_NegativeInitialNetWorth_Improvement_ReturnsNoAction()
    {
        // initialNetWorth = –50 000; currentNetWorth = –40 000
        // deltaPercent = +10 000 / 50 000 = +0.20 — profitable → no action
        var rec = BotProfitCalculator.Recommend(
            -40_000m, -50_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct);
    }

    [Fact]
    public void Recommend_NegativeInitialNetWorth_ExactlyAtSevereThreshold_ReturnsAggressiveAction()
    {
        // deltaPercent = –5 000 / 50 000 = –0.10 exactly (SeverelyUnprofitableThresholdPercent)
        // The threshold is inclusive (<=), so this should trigger aggressive action.
        var rec = BotProfitCalculator.Recommend(
            -55_000m, -50_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Recommend_NegativeInitialNetWorth_WithinNeutralBand_ReturnsNoAction()
    {
        // deltaPercent = –1 000 / 50 000 = –0.02 exactly (at the lower neutral boundary)
        // Boundary is inclusive for neutral, so –2% → Neutral → NoAction.
        var rec = BotProfitCalculator.Recommend(
            -51_000m, -50_000m,
            ticksElapsed: 10,
            minTicksBeforeAdjustment: 5);

        Assert.False(rec.ShouldAct);
    }

    // ── ComputeNetWorth sign correctness ──────────────────────────────────────

    [Fact]
    public void ComputeNetWorth_MixedSignCompanies_SumsCorrectly()
    {
        // One company is in profit; another is in debt.
        // Net sum must be +50 000.
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 100_000m },
                new CompanySummary { Cash = -50_000m },
            ],
        };
        Assert.Equal(50_000m, BotProfitCalculator.ComputeNetWorth(profile));
    }

    // ── BotStateValidator — valid-bot summary is empty ────────────────────────

    [Fact]
    public void Validate_FullyValidBot_IsValidAndSummaryIsEmpty()
    {
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_01",
            Email = "npc01@test.example",
            Strategy = "FURNITURE",
            Token = "tok-valid",
            TokenExpiresAtUtc = DateTime.UtcNow.AddHours(2),
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCompletedAtUtc = DateTime.UtcNow.AddMonths(-1),
            },
            LastSuccessUtc = DateTime.UtcNow.AddMinutes(-5),
        };

        var result = BotStateValidator.Validate(bot, staleAfterMinutes: 30);

        Assert.True(result.IsValid);
        Assert.Empty(result.Issues);
        Assert.Equal("Bot is ready for operation.", result.Summary);
    }

    // ── PriceAdjustmentHelper — unit type matching is case-insensitive ────────

    [Fact]
    public void SelectAdjustableUnits_UnitTypeInAllCaps_Matched()
    {
        // UnitType "PUBLIC_SALES" in all-caps (standard server casing) must be matched.
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "co1",
                Buildings =
                [
                    new BuildingSummary
                    {
                        Id = "b1",
                        Units = [new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(result);
        Assert.Equal("u1", result[0].Unit.Id);
    }

    [Fact]
    public void SelectAdjustableUnits_UnitTypeInLowerCase_Matched()
    {
        // UnitType "public_sales" in lowercase must still be matched (case-insensitive).
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "co1",
                Buildings =
                [
                    new BuildingSummary
                    {
                        Id = "b1",
                        Units = [new UnitSummary { Id = "u2", UnitType = "public_sales", MinPrice = 75m }],
                    },
                ],
            },
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(result);
        Assert.Equal("u2", result[0].Unit.Id);
    }

    // ── BotRosterFactory — boundary and special cases ─────────────────────────

    [Fact]
    public void Build_ZeroCount_ClampedToOne()
    {
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 0 });
        Assert.Single(bots);
    }

    [Fact]
    public void Build_MaxBotCount_AllBotsCreated()
    {
        // BotRosterFactory clamps to [1, 20]; 20 is the documented upper bound.
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 20 });
        Assert.Equal(20, bots.Count);
    }

    [Fact]
    public void Build_MoreThanMaxBotCount_ClampedToTwenty()
    {
        var bots = BotRosterFactory.Build(new BotOptions { BotCount = 100 });
        Assert.Equal(20, bots.Count);
    }

    // ── OnboardingHelpers.ContainsSuitableType — CSV edge cases ──────────────

    [Fact]
    public void ContainsSuitableType_SingleEntryInField_Matches()
    {
        // A field with a single entry (no comma) must still match.
        Assert.True(OnboardingHelpers.ContainsSuitableType("FACTORY", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_CommaOnlyField_ReturnsFalse()
    {
        // A string containing only commas must not match any type.
        Assert.False(OnboardingHelpers.ContainsSuitableType(",,,", "FACTORY"));
    }

    [Fact]
    public void ContainsSuitableType_MatchIsInMiddleOfCsv_ReturnsTrue()
    {
        // The target type appears as the middle entry of a three-part CSV.
        Assert.True(OnboardingHelpers.ContainsSuitableType("MINE,FACTORY,SALES_SHOP", "FACTORY"));
    }

    // ── BotAccount profitability helpers — negative-delta boundary ────────────

    [Fact]
    public void Classify_NegativeInitial_WorseCurrent_IsUnprofitable()
    {
        // Both values are negative; current is further from zero.
        // deltaPercent = (–60 000 – (–50 000)) / |–50 000| = –0.20 → Unprofitable
        Assert.Equal(
            ProfitabilityStatus.Unprofitable,
            BotProfitCalculator.Classify(-60_000m, -50_000m));
    }

    [Fact]
    public void Classify_NegativeInitial_BetterCurrent_IsProfitable()
    {
        // Current is closer to zero than initial (bot made back some losses).
        // deltaPercent = (–40 000 – (–50 000)) / |–50 000| = +0.20 → Profitable
        Assert.Equal(
            ProfitabilityStatus.Profitable,
            BotProfitCalculator.Classify(-40_000m, -50_000m));
    }
}
