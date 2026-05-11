using System.Net;
using System.Text.Json;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Capitalism.NPCBot.Configuration;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Final pass of gap coverage addressing genuinely untested paths:
/// JSON deserialization of <see cref="RankingEntry"/>, <see cref="BuildingLotSummary"/>,
/// <see cref="ProductTypeSummary"/>, and <see cref="CitySummary"/>; network-failure
/// propagation in <see cref="GameApiClient"/>; and additional <see cref="BotOrchestrator"/>
/// and <see cref="BotProfitCalculator"/> edge cases.
/// </summary>
public sealed class BotJsonAndNetworkTests
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // ── RankingEntry JSON deserialization ──────────────────────────────────────

    [Fact]
    public void RankingEntry_Deserialize_ParsesAllFields()
    {
        const string json = """
            {"rank":3,"displayName":"Alpha_Bot","netWorth":1234567.89}
            """;
        var entry = JsonSerializer.Deserialize<RankingEntry>(json, JsonOpts)!;
        Assert.Equal(3, entry.Rank);
        Assert.Equal("Alpha_Bot", entry.DisplayName);
        Assert.Equal(1234567.89m, entry.NetWorth);
    }

    [Fact]
    public void RankingEntry_Deserialize_EmptyDisplayName_DefaultsToEmpty()
    {
        const string json = """{"rank":1,"displayName":"","netWorth":0}""";
        var entry = JsonSerializer.Deserialize<RankingEntry>(json, JsonOpts)!;
        Assert.Equal(1, entry.Rank);
        Assert.Equal(string.Empty, entry.DisplayName);
        Assert.Equal(0m, entry.NetWorth);
    }

    [Fact]
    public void RankingEntry_Array_Deserialize_ParsesList()
    {
        const string json = """
            [
              {"rank":1,"displayName":"Top","netWorth":9000},
              {"rank":2,"displayName":"Second","netWorth":8000}
            ]
            """;
        var list = JsonSerializer.Deserialize<List<RankingEntry>>(json, JsonOpts)!;
        Assert.Equal(2, list.Count);
        Assert.Equal("Top", list[0].DisplayName);
        Assert.Equal(2, list[1].Rank);
    }

    // ── BuildingLotSummary JSON deserialization ────────────────────────────────

    [Fact]
    public void BuildingLotSummary_Deserialize_AvailableLot_BuildingIdIsNull()
    {
        const string json = """
            {"id":"lot-1","suitableTypes":"FACTORY,MINE","price":75000,"buildingId":null}
            """;
        var lot = JsonSerializer.Deserialize<BuildingLotSummary>(json, JsonOpts)!;
        Assert.Equal("lot-1", lot.Id);
        Assert.Equal("FACTORY,MINE", lot.SuitableTypes);
        Assert.Equal(75000m, lot.Price);
        Assert.Null(lot.BuildingId);
    }

    [Fact]
    public void BuildingLotSummary_Deserialize_OccupiedLot_BuildingIdIsSet()
    {
        const string json = """
            {"id":"lot-2","suitableTypes":"SALES_SHOP","price":50000,"buildingId":"bld-abc"}
            """;
        var lot = JsonSerializer.Deserialize<BuildingLotSummary>(json, JsonOpts)!;
        Assert.Equal("bld-abc", lot.BuildingId);
        Assert.NotNull(lot.BuildingId);
    }

    [Fact]
    public void BuildingLotSummary_Deserialize_MissingBuildingId_DefaultsToNull()
    {
        const string json = """{"id":"lot-3","suitableTypes":"FACTORY","price":10000}""";
        var lot = JsonSerializer.Deserialize<BuildingLotSummary>(json, JsonOpts)!;
        Assert.Null(lot.BuildingId);
    }

    // ── ProductTypeSummary JSON deserialization ────────────────────────────────

    [Fact]
    public void ProductTypeSummary_Deserialize_FreeProduct_IsProOnlyFalse()
    {
        const string json = """
            {"id":"prod-1","name":"Wooden Chair","basePrice":45,"isProOnly":false,"industry":"FURNITURE"}
            """;
        var prod = JsonSerializer.Deserialize<ProductTypeSummary>(json, JsonOpts)!;
        Assert.Equal("prod-1", prod.Id);
        Assert.Equal("Wooden Chair", prod.Name);
        Assert.Equal(45m, prod.BasePrice);
        Assert.False(prod.IsProOnly);
    }

    [Fact]
    public void ProductTypeSummary_Deserialize_ProProduct_IsProOnlyTrue()
    {
        const string json = """
            {"id":"prod-pro","name":"Smartphone","basePrice":500,"isProOnly":true,"industry":"ELECTRONICS"}
            """;
        var prod = JsonSerializer.Deserialize<ProductTypeSummary>(json, JsonOpts)!;
        Assert.True(prod.IsProOnly);
        Assert.Equal("ELECTRONICS", prod.Industry);
    }

    [Fact]
    public void ProductTypeSummary_Deserialize_MissingIsProOnly_DefaultsFalse()
    {
        const string json = """{"id":"p","name":"Bread","basePrice":3}""";
        var prod = JsonSerializer.Deserialize<ProductTypeSummary>(json, JsonOpts)!;
        Assert.False(prod.IsProOnly);
    }

    // ── CitySummary JSON deserialization ──────────────────────────────────────

    [Fact]
    public void CitySummary_Deserialize_ParsesAllFields()
    {
        const string json = """
            {"id":"city-br","name":"Bratislava","countryCode":"SK","population":475000}
            """;
        var city = JsonSerializer.Deserialize<CitySummary>(json, JsonOpts)!;
        Assert.Equal("city-br", city.Id);
        Assert.Equal("Bratislava", city.Name);
        Assert.Equal("SK", city.CountryCode);
        Assert.Equal(475000, city.Population);
    }

    [Fact]
    public void CitySummary_Deserialize_MissingOptionalFields_UseDefaults()
    {
        const string json = """{"id":"city-x","name":"Unknown"}""";
        var city = JsonSerializer.Deserialize<CitySummary>(json, JsonOpts)!;
        Assert.Equal("city-x", city.Id);
        Assert.Equal("Unknown", city.Name);
        Assert.Equal(string.Empty, city.CountryCode);
        Assert.Equal(0, city.Population);
    }

    // ── GameApiClient network-failure propagation ─────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NetworkError_PropagatesHttpRequestException()
    {
        // Arrange: handler that throws HttpRequestException (simulates DNS failure / refused connection)
        var handler = new ThrowingHttpHandler(new HttpRequestException("Connection refused"));
        var http = new HttpClient(handler);
        var opts = Options.Create(new BotOptions { GraphqlUrl = "http://invalid.test/graphql" });
        var client = new GameApiClient(http, opts, NullLogger<GameApiClient>.Instance);

        // Act / Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            client.ExecuteAsync<object>("{ ping }"));
    }

    [Fact]
    public async Task ExecuteAsync_TaskCancelledMidRequest_PropagatesOperationCanceledException()
    {
        // Arrange: handler that throws TaskCanceledException (simulates request timeout)
        var handler = new ThrowingHttpHandler(new TaskCanceledException("Request timed out"));
        var http = new HttpClient(handler);
        var opts = Options.Create(new BotOptions { GraphqlUrl = "http://invalid.test/graphql" });
        var client = new GameApiClient(http, opts, NullLogger<GameApiClient>.Instance);

        // Act / Assert
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            client.ExecuteAsync<object>("{ ping }"));
    }

    // ── BotProfitCalculator additional edge cases ─────────────────────────────

    [Fact]
    public void Classify_VeryLargeGain_ReturnsProfitable()
    {
        // 1000% gain should definitely be Profitable
        var status = BotProfitCalculator.Classify(currentNetWorth: 1_000_000m, initialNetWorth: 100_000m);
        Assert.Equal(ProfitabilityStatus.Profitable, status);
    }

    [Fact]
    public void Classify_VeryLargeLoss_ReturnsUnprofitable()
    {
        // 50% loss should be Unprofitable
        var status = BotProfitCalculator.Classify(currentNetWorth: 50_000m, initialNetWorth: 100_000m);
        Assert.Equal(ProfitabilityStatus.Unprofitable, status);
    }

    [Fact]
    public void ComputeAnnualisedRatePercent_NegativeTicksElapsed_ReturnsZero()
    {
        // Negative ticksElapsed (tracking start in future) must return 0, not throw
        var rate = BotProfitCalculator.ComputeAnnualisedRatePercent(
            currentNetWorth: 110_000m,
            initialNetWorth: 100_000m,
            ticksElapsed: -5);
        Assert.Equal(0m, rate);
    }

    [Fact]
    public void ComputeNetWorth_CompanyWithZeroCash_ContributesZero()
    {
        var profile = new PlayerProfile
        {
            Companies =
            [
                new CompanySummary { Cash = 0m },
                new CompanySummary { Cash = 50_000m },
            ],
        };
        var worth = BotProfitCalculator.ComputeNetWorth(profile);
        Assert.Equal(50_000m, worth);
    }

    // ── BotOptions additional validation ─────────────────────────────────────

    [Fact]
    public void BotOptions_BotPassword_DefaultIsEmpty()
    {
        // The committed placeholder was removed. Default is empty so operators must
        // set a real credential via environment variable before production use.
        var opts = new BotOptions();
        Assert.Equal("", opts.BotPassword);
    }

    [Fact]
    public void BotOptions_GraphqlUrl_DoesNotHaveTrailingSlash()
    {
        var opts = new BotOptions();
        Assert.False(opts.GraphqlUrl.EndsWith("/", StringComparison.Ordinal),
            $"GraphqlUrl should not end with '/' but was '{opts.GraphqlUrl}'");
    }

    // ── StrategyRecommendation additional properties ──────────────────────────

    [Fact]
    public void StrategyRecommendation_MildAction_HasExpectedFields()
    {
        var rec = new StrategyRecommendation
        {
            ShouldAct = true,
            Reason = "mild loss detected",
            PriceAdjustmentFactor = BotProfitCalculator.MildPriceReductionFactor,
        };
        Assert.True(rec.ShouldAct);
        Assert.NotEmpty(rec.Reason);
        Assert.Equal(BotProfitCalculator.MildPriceReductionFactor, rec.PriceAdjustmentFactor);
    }

    [Fact]
    public void StrategyRecommendation_AggressiveAction_FactorIsLowerThanMild()
    {
        var mild = BotProfitCalculator.MildPriceReductionFactor;
        var aggressive = BotProfitCalculator.AggressivePriceReductionFactor;
        Assert.True(aggressive < mild, $"Aggressive ({aggressive}) should be lower than mild ({mild})");
    }

    // ── BotAccount default helpers ────────────────────────────────────────────

    [Fact]
    public void BotAccount_Index_DefaultsToZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0, bot.Index);
    }

    [Fact]
    public void BotAccount_ConsecutiveErrors_DefaultsToZero()
    {
        var bot = new BotAccount();
        Assert.Equal(0, bot.ConsecutiveErrors);
    }

    [Fact]
    public void BotAccount_IsSkipped_DefaultsToFalse()
    {
        var bot = new BotAccount();
        Assert.False(bot.IsSkipped);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// An <see cref="HttpMessageHandler"/> that always throws the provided exception,
    /// simulating a network-level failure before a response is received.
    /// </summary>
    private sealed class ThrowingHttpHandler(Exception exceptionToThrow) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw exceptionToThrow;
    }
}
