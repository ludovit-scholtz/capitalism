using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="PriceAdjustmentService"/>.
///
/// <para>
/// <b>Early-return paths</b> (tests 1–9) exercise every guard that prevents HTTP calls from being
/// made: no-act recommendation, missing token, null profile, empty company/building/unit lists,
/// null or zero MinPrice, and no-op adjustments (price already at floor).  None of these paths
/// should invoke the HTTP client.
/// </para>
/// <para>
/// <b>Successful paths</b> (tests 10–12) use an in-process <see cref="FakeHttpHandler"/> that
/// returns a well-formed GraphQL JSON response and verify the returned update count.
/// </para>
/// <para>
/// <b>Resilience paths</b> (tests 13–15) verify that a pre-cancelled token breaks the loop
/// without any HTTP calls, that an HTTP error on the first unit does not prevent the second unit
/// from being updated, and that units spread across two separate companies are all adjusted.
/// </para>
/// </summary>
public sealed class PriceAdjustmentServiceTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    /// <summary>
    /// Minimal <see cref="HttpMessageHandler"/> whose response is supplied by a factory so that
    /// every call receives a fresh <see cref="HttpResponseMessage"/> with a fresh content stream
    /// (the stream can only be read once per response instance).
    /// </summary>
    private sealed class FakeHttpHandler(Func<HttpResponseMessage> factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken __) => Task.FromResult(factory());
    }

    /// <summary>Builds the JSON body the game API returns after a successful price update.</summary>
    private static string OkJson(string id = "u1", decimal price = 47.5m) =>
        "{\"data\":{\"updatePublicSalesPrice\":{\"id\":\"" + id +
        "\",\"unitType\":\"PUBLIC_SALES\",\"minPrice\":" + price + "}}}";

    /// <summary>
    /// Creates a <see cref="PriceAdjustmentService"/> wired to a fake HTTP handler.
    /// If <paramref name="httpFactory"/> is null the handler always returns a 200 OK
    /// with a valid <c>updatePublicSalesPrice</c> response.
    /// </summary>
    private static PriceAdjustmentService CreateService(Func<HttpResponseMessage>? httpFactory = null)
    {
        httpFactory ??= () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(OkJson(), Encoding.UTF8, "application/json"),
        };

        var options = Options.Create(new BotOptions());
        var http = new HttpClient(new FakeHttpHandler(httpFactory));
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var accounts = new AccountService(api, options, NullLogger<AccountService>.Instance);
        return new PriceAdjustmentService(accounts, NullLogger<PriceAdjustmentService>.Instance);
    }

    /// <summary>Helper: creates a "ShouldAct = true" recommendation with the given factor.</summary>
    private static StrategyRecommendation ActionRec(decimal factor = 0.95m) =>
        new() { ShouldAct = true, Reason = "test", PriceAdjustmentFactor = factor };

    /// <summary>
    /// Helper: builds a single-company, single-building bot whose shop contains the supplied
    /// PUBLIC_SALES units.  The bot has a valid token so early-token/profile guards don't fire.
    /// </summary>
    private static BotAccount BotWithUnits(params (string id, decimal? price)[] units)
    {
        var botUnits = units
            .Select(u => new UnitSummary { Id = u.id, UnitType = "PUBLIC_SALES", MinPrice = u.price })
            .ToList();

        return new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Name = "Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Downtown Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units = botUnits,
                            },
                        ],
                    },
                ],
            },
        };
    }

    // ── Early return: recommendation.ShouldAct = false ────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_ShouldNotAct_ReturnsZeroWithNoHttpCall()
    {
        var httpCalls = 0;
        var service = CreateService(() =>
        {
            httpCalls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OkJson(), Encoding.UTF8, "application/json"),
            };
        });

        var result = await service.ApplyAdjustmentAsync(
            BotWithUnits(("u1", 50m)), StrategyRecommendation.NoAction, CancellationToken.None);

        Assert.Equal(0, result);
        Assert.Equal(0, httpCalls); // no HTTP call was made
    }

    // ── Early return: null token ──────────────────────────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_NullToken_ReturnsZero()
    {
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = null,
            Profile = new PlayerProfile { Companies = [] },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    // ── Early return: null profile ────────────────────────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_NullProfile_ReturnsZero()
    {
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = null,
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    // ── Early return: no adjustable units ────────────────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_EmptyCompanyList_ReturnsZero()
    {
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile { Companies = [] },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_CompanyWithNoBuildings_ReturnsZero()
    {
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies = [new CompanySummary { Id = "c1", Name = "Corp", Buildings = [] }],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_BuildingWithNoPublicSalesUnits_ReturnsZero()
    {
        // Building contains only MANUFACTURING units — none qualify for price adjustment.
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Factory",
                                Type = "FACTORY",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "MANUFACTURING", MinPrice = 50m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_PublicSalesUnitWithNullMinPrice_ReturnsZero()
    {
        // Null MinPrice means unit was never configured — excluded by SelectAdjustableUnits.
        var service = CreateService();
        var bot = BotWithUnits(("u1", null));

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_PublicSalesUnitWithZeroMinPrice_ReturnsZero()
    {
        // Zero MinPrice is treated the same as unset — not > 0m, so excluded.
        var service = CreateService();
        var bot = BotWithUnits(("u1", 0m));

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(0, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_AllUnitsAtFloor_NoMeaningfulChange_ReturnsZero()
    {
        // 0.01 × 0.85 = 0.0085 → rounded to 0.01 → IsAdjustmentMeaningful(0.01, 0.01) = false
        // The unit is technically adjustable but the computed change is below 1 cent, so it is skipped.
        var service = CreateService();
        var bot = BotWithUnits(("u1", PriceAdjustmentHelper.MinimumAllowedPrice));

        var result = await service.ApplyAdjustmentAsync(
            bot,
            ActionRec(BotProfitCalculator.AggressivePriceReductionFactor),
            CancellationToken.None);

        Assert.Equal(0, result);
    }

    // ── Successful path with fake HTTP ────────────────────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_OneValidUnit_ReturnsOne()
    {
        var service = CreateService();
        var bot = BotWithUnits(("u1", 50m));

        var result = await service.ApplyAdjustmentAsync(
            bot,
            ActionRec(BotProfitCalculator.MildPriceReductionFactor),
            CancellationToken.None);

        Assert.Equal(1, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_TwoValidUnitsInSameBuilding_ReturnsTwo()
    {
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Name = "Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Main Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                                    new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 30m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(2, result);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_MixedValidAndZeroPriceUnits_UpdatesOnlyValidOnes()
    {
        // 2 valid units + 1 zero-price (excluded) → only 2 should be updated.
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                                    new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 0m },
                                    new UnitSummary { Id = "u3", UnitType = "PUBLIC_SALES", MinPrice = 30m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(2, result);
    }

    // ── Resilience paths ─────────────────────────────────────────────────────

    [Fact]
    public async Task ApplyAdjustmentAsync_CancelledTokenBeforeLoop_NoHttpCallAndReturnsZero()
    {
        // Cancellation is requested BEFORE the loop starts; the loop guard fires on the first
        // iteration and breaks without issuing any HTTP call.
        var httpCalls = 0;
        var service = CreateService(() =>
        {
            httpCalls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OkJson(), Encoding.UTF8, "application/json"),
            };
        });

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                                    new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 30m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync(); // cancel BEFORE the call

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), cts.Token);

        Assert.Equal(0, result);
        Assert.Equal(0, httpCalls);
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_FirstUnitHttpError_ContinuesToSecondUnit_ReturnsOne()
    {
        // First HTTP call returns 500 (service throws) — the catch block logs and continues.
        // Second HTTP call returns 200 — unit is updated and the count is 1.
        var callCount = 0;
        var service = CreateService(() =>
        {
            callCount++;
            if (callCount == 1)
                return new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("server error", Encoding.UTF8, "text/plain"),
                };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(OkJson("u2", 28.5m), Encoding.UTF8, "application/json"),
            };
        });

        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
                                    new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 30m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(1, result);    // second unit succeeded
        Assert.Equal(2, callCount); // both HTTP calls were made
    }

    [Fact]
    public async Task ApplyAdjustmentAsync_UnitsAcrossTwoCompanies_UpdatesAllEligibleUnits()
    {
        // Bot owns two companies each with one PUBLIC_SALES unit.
        // Both units should be updated, so the return value should be 2.
        var service = CreateService();
        var bot = new BotAccount
        {
            Index = 1,
            DisplayName = "NPC_Test_01",
            Email = "npc@test.example",
            Strategy = "Trading",
            Token = "valid-token",
            Profile = new PlayerProfile
            {
                Companies =
                [
                    new CompanySummary
                    {
                        Id = "c1",
                        Name = "Alpha Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b1",
                                Name = "Alpha Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 80m },
                                ],
                            },
                        ],
                    },
                    new CompanySummary
                    {
                        Id = "c2",
                        Name = "Beta Corp",
                        Buildings =
                        [
                            new BuildingSummary
                            {
                                Id = "b2",
                                Name = "Beta Shop",
                                Type = "SALES_SHOP",
                                CityId = "ba",
                                Units =
                                [
                                    new UnitSummary { Id = "u2", UnitType = "PUBLIC_SALES", MinPrice = 40m },
                                ],
                            },
                        ],
                    },
                ],
            },
        };

        var result = await service.ApplyAdjustmentAsync(bot, ActionRec(), CancellationToken.None);

        Assert.Equal(2, result);
    }
}
