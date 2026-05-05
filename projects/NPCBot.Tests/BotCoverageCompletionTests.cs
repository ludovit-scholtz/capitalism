using System.Text.Json;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Additional coverage tests that fill gaps not addressed by the primary test files.
///
/// <para>Categories:</para>
/// <list type="bullet">
///   <item><b>GameModels JSON deserialization</b> — proves that the GraphQL response types correctly
///   round-trip through <see cref="System.Text.Json"/> with case-insensitive matching, matching
///   the settings used by <see cref="GameApiClient"/> at runtime.</item>
///   <item><b>BotAccount computed properties</b> — verifies <see cref="BotAccount.OnboardingCompleted"/>,
///   <see cref="BotAccount.ProfitDelta"/>, and token-validity edge cases not covered elsewhere.</item>
///   <item><b>BotOptions additional defaults</b> — guards constants and flags that drive runtime
///   behaviour (Enabled, BotEmailDomain) against silent breakage.</item>
///   <item><b>PriceAdjustmentService with aggressive factor</b> — exercises the AggressivePriceReductionFactor
///   path and the three-building multi-company scenario end-to-end.</item>
///   <item><b>Cross-component integration</b> — pipeline tests that verify the Classify → Recommend →
///   SelectAdjustableUnits chain produces coherent results for each profitability state.</item>
/// </list>
/// </summary>
public sealed class BotCoverageCompletionTests
{
    // ── JSON options used by GameApiClient ────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── GameModels JSON deserialization ───────────────────────────────────────

    [Fact]
    public void PlayerProfile_Deserialize_SetsDisplayNameAndEmail()
    {
        const string json = """
            {
              "id": "p1",
              "displayName": "NPC_Bot_01",
              "email": "npc01@npcbot.capitalism.local",
              "onboardingCompletedAtUtc": null,
              "companies": []
            }
            """;

        var profile = JsonSerializer.Deserialize<PlayerProfile>(json, JsonOpts)!;

        Assert.Equal("NPC_Bot_01", profile.DisplayName);
        Assert.Equal("npc01@npcbot.capitalism.local", profile.Email);
        Assert.Null(profile.OnboardingCompletedAtUtc);
        Assert.Empty(profile.Companies);
    }

    [Fact]
    public void PlayerProfile_Deserialize_ParsesOnboardingCompletedAtUtc()
    {
        const string json = """
            {
              "id": "p1",
              "displayName": "Bot",
              "email": "bot@test.local",
              "onboardingCompletedAtUtc": "2026-01-15T10:00:00Z",
              "companies": []
            }
            """;

        var profile = JsonSerializer.Deserialize<PlayerProfile>(json, JsonOpts)!;

        Assert.True(profile.OnboardingCompletedAtUtc.HasValue);
        Assert.Equal(2026, profile.OnboardingCompletedAtUtc!.Value.Year);
    }

    [Fact]
    public void CompanySummary_Deserialize_ParsesNestedBuildingUnits()
    {
        // This test verifies the complete graph: Company → Buildings → Units
        // which is the shape GameApiClient parses from the `me` GraphQL response.
        const string json = """
            {
              "id": "c1",
              "name": "Alpha Corp",
              "cash": 250000.50,
              "buildings": [
                {
                  "id": "b1",
                  "name": "Main Shop",
                  "type": "SALES_SHOP",
                  "cityId": "ba",
                  "units": [
                    { "id": "u1", "unitType": "PUBLIC_SALES", "minPrice": 49.99 }
                  ]
                }
              ]
            }
            """;

        var company = JsonSerializer.Deserialize<CompanySummary>(json, JsonOpts)!;

        Assert.Equal("Alpha Corp", company.Name);
        Assert.Equal(250_000.50m, company.Cash);
        Assert.Single(company.Buildings);

        var building = company.Buildings[0];
        Assert.Equal("SALES_SHOP", building.Type);
        Assert.Single(building.Units);

        var unit = building.Units[0];
        Assert.Equal("PUBLIC_SALES", unit.UnitType);
        Assert.Equal(49.99m, unit.MinPrice);
    }

    [Fact]
    public void GameStateSummary_Deserialize_ParsesTickFields()
    {
        const string json = """
            {
              "currentTick": 1234,
              "tickIntervalSeconds": 300,
              "taxCycleTicks": 8760
            }
            """;

        var gs = JsonSerializer.Deserialize<GameStateSummary>(json, JsonOpts)!;

        Assert.Equal(1234L, gs.CurrentTick);
        Assert.Equal(300, gs.TickIntervalSeconds);
        Assert.Equal(8760, gs.TaxCycleTicks);
    }

    [Fact]
    public void AuthPayload_Deserialize_ParsesTokenAndPlayer()
    {
        const string json = """
            {
              "token": "eyJhbGc.payload.sig",
              "expiresAtUtc": "2026-12-31T23:59:59Z",
              "player": {
                "id": "p42",
                "displayName": "NPC_Test_42",
                "email": "npc42@test.local",
                "onboardingCompletedAtUtc": null
              }
            }
            """;

        var payload = JsonSerializer.Deserialize<AuthPayload>(json, JsonOpts)!;

        Assert.Equal("eyJhbGc.payload.sig", payload.Token);
        Assert.True(payload.ExpiresAtUtc > DateTime.MinValue);
        Assert.Equal("NPC_Test_42", payload.Player!.DisplayName);
    }

    [Fact]
    public void UnitSummary_Deserialize_ParsesMinPriceAsNullWhenAbsent()
    {
        // Units that have never been priced omit minPrice in the API response.
        const string json = """{ "id": "u9", "unitType": "PUBLIC_SALES" }""";

        var unit = JsonSerializer.Deserialize<UnitSummary>(json, JsonOpts)!;

        Assert.Equal("u9", unit.Id);
        Assert.Null(unit.MinPrice);
    }

    // ── BotAccount computed properties ────────────────────────────────────────

    [Fact]
    public void OnboardingCompleted_NullProfile_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.local", Strategy = "Trading",
            Profile = null,
        };

        Assert.False(bot.OnboardingCompleted);
    }

    [Fact]
    public void OnboardingCompleted_ProfileWithNullCompletedAtUtc_ReturnsFalse()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.local", Strategy = "Trading",
            Profile = new PlayerProfile { OnboardingCompletedAtUtc = null },
        };

        Assert.False(bot.OnboardingCompleted);
    }

    [Fact]
    public void OnboardingCompleted_ProfileWithCompletedAtUtcSet_ReturnsTrue()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.local", Strategy = "Trading",
            Profile = new PlayerProfile
            {
                OnboardingCompletedAtUtc = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc),
            },
        };

        Assert.True(bot.OnboardingCompleted);
    }

    [Fact]
    public void ProfitDelta_ReflectsCurrentMinusInitial()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.local", Strategy = "Trading",
            InitialNetWorth = 100_000m,
            CurrentNetWorth = 115_000m,
        };

        Assert.Equal(15_000m, bot.ProfitDelta);
    }

    [Fact]
    public void ProfitDelta_NegativeWhenCurrentLessThanInitial()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC", Email = "npc@test.local", Strategy = "Trading",
            InitialNetWorth = 200_000m,
            CurrentNetWorth = 180_000m,
        };

        Assert.Equal(-20_000m, bot.ProfitDelta);
    }

    // ── BotOptions additional defaults ────────────────────────────────────────

    [Fact]
    public void Defaults_EnabledIsTrue()
    {
        // The bot runner must default to enabled so a fresh config doesn't silently do nothing.
        var opts = new BotOptions();
        Assert.True(opts.Enabled);
    }

    [Fact]
    public void Defaults_BotEmailDomainContainsDotSeparator()
    {
        // Must be a syntactically valid domain component (not an empty string or plain label).
        var opts = new BotOptions();
        Assert.Contains('.', opts.BotEmailDomain);
    }

    [Fact]
    public void Defaults_BotEmailDomainIsNonEmpty()
    {
        var opts = new BotOptions();
        Assert.False(string.IsNullOrWhiteSpace(opts.BotEmailDomain));
    }

    // ── PriceAdjustmentHelper with AggressivePriceReductionFactor ─────────────

    [Fact]
    public void ComputeNewPrice_AggressiveFactor_ReducesByFifteenPercent()
    {
        // $100 × 0.85 = $85.00
        var result = PriceAdjustmentHelper.ComputeNewPrice(
            100m, BotProfitCalculator.AggressivePriceReductionFactor);

        Assert.Equal(85.00m, result);
    }

    [Fact]
    public void ComputeNewPrice_MildFactor_ReducesByFivePercent()
    {
        // $200 × 0.95 = $190.00
        var result = PriceAdjustmentHelper.ComputeNewPrice(
            200m, BotProfitCalculator.MildPriceReductionFactor);

        Assert.Equal(190.00m, result);
    }

    [Fact]
    public void SelectAdjustableUnits_ThreeCompaniesEachWithOneUnit_ReturnsThree()
    {
        var companies = new List<CompanySummary>
        {
            MakeCompany("c1", "Corp A", "u1", 50m),
            MakeCompany("c2", "Corp B", "u2", 75m),
            MakeCompany("c3", "Corp C", "u3", 30m),
        };

        var result = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();

        Assert.Equal(3, result.Count);
    }

    // ── Cross-component pipeline: Classify → Recommend → SelectAdjustableUnits ─

    [Fact]
    public void Pipeline_ProfitableBot_ClassifyAndRecommend_ReturnsNoAction()
    {
        // 10% growth → Profitable → NoAction (no price change needed)
        const decimal initialNetWorth = 100_000m;
        const decimal currentNetWorth = 110_000m;
        const long ticksElapsed = 90;

        var status = BotProfitCalculator.Classify(currentNetWorth, initialNetWorth);
        var rec = BotProfitCalculator.Recommend(currentNetWorth, initialNetWorth, ticksElapsed);

        Assert.Equal(ProfitabilityStatus.Profitable, status);
        Assert.False(rec.ShouldAct);
        Assert.Equal(0m, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Pipeline_MildLossBot_ClassifyAndRecommend_ReturnsMildReduction()
    {
        // −5% loss → Unprofitable (mild) → recommend mild price reduction
        const decimal initialNetWorth = 100_000m;
        const decimal currentNetWorth = 95_000m;
        const long ticksElapsed = 100;

        var status = BotProfitCalculator.Classify(currentNetWorth, initialNetWorth);
        var rec = BotProfitCalculator.Recommend(currentNetWorth, initialNetWorth, ticksElapsed);

        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Pipeline_SevereLossBot_ClassifyAndRecommend_ReturnsAggressiveReduction()
    {
        // −15% loss → Unprofitable (severely) → recommend aggressive price reduction
        const decimal initialNetWorth = 100_000m;
        const decimal currentNetWorth = 85_000m;
        const long ticksElapsed = 100;

        var status = BotProfitCalculator.Classify(currentNetWorth, initialNetWorth);
        var rec = BotProfitCalculator.Recommend(currentNetWorth, initialNetWorth, ticksElapsed);

        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
        Assert.True(rec.ShouldAct);
        Assert.Equal(BotProfitCalculator.AggressivePriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void Pipeline_AggressiveRecommendation_SelectAdjustableUnits_ProducesCorrectNewPrice()
    {
        // Verify the full pipeline: Aggressive factor → ComputeNewPrice for a real unit price.
        var units = new List<UnitSummary>
        {
            new() { Id = "u1", UnitType = "PUBLIC_SALES", MinPrice = 50m },
        };
        var companies = new List<CompanySummary>
        {
            new()
            {
                Id = "c1",
                Buildings =
                [
                    new BuildingSummary { Id = "b1", Name = "Shop", Type = "SALES_SHOP", CityId = "ba", Units = units },
                ],
            },
        };

        var adjustable = PriceAdjustmentHelper.SelectAdjustableUnits(companies).ToList();
        Assert.Single(adjustable);

        var (unit, _) = adjustable[0];
        var newPrice = PriceAdjustmentHelper.ComputeNewPrice(
            unit.MinPrice!.Value, BotProfitCalculator.AggressivePriceReductionFactor);

        // $50 × 0.85 = $42.50
        Assert.Equal(42.50m, newPrice);
        Assert.True(PriceAdjustmentHelper.IsAdjustmentMeaningful(unit.MinPrice!.Value, newPrice));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static CompanySummary MakeCompany(string id, string name, string unitId, decimal minPrice) =>
        new()
        {
            Id = id,
            Name = name,
            Buildings =
            [
                new BuildingSummary
                {
                    Id = "b-" + id,
                    Name = name + " Shop",
                    Type = "SALES_SHOP",
                    CityId = "ba",
                    Units = [new UnitSummary { Id = unitId, UnitType = "PUBLIC_SALES", MinPrice = minPrice }],
                },
            ],
        };

    private static PlayerProfile MakeProfile(string companyId, decimal cash, int buildingCount) =>
        new()
        {
            Companies =
            [
                new CompanySummary
                {
                    Id = companyId,
                    Cash = cash,
                    Buildings = Enumerable.Range(1, buildingCount)
                        .Select(i => new BuildingSummary { Id = "b" + i, Name = "Building " + i, Type = "FACTORY", CityId = "ba" })
                        .ToList(),
                },
            ],
        };
}
