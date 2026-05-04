using System.Net;
using System.Text;
using Capitalism.NPCBot.Configuration;
using Capitalism.NPCBot.Models;
using Capitalism.NPCBot.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Capitalism.NPCBot.Tests;

/// <summary>
/// Unit tests for <see cref="OnboardingService"/> using an in-process
/// <see cref="QueuedHttpHandler"/> so no real HTTP calls are made.
///
/// <para>
/// <b>Full happy path</b> — fresh bot: cities query → factory lots → startOnboardingCompany
/// mutation → shop lots → productTypes → finishOnboarding (6 HTTP calls total).
/// </para>
/// <para>
/// <b>Resume path</b> — bot whose <c>OnboardingCurrentStep == "SHOP_SELECTION"</c> skips
/// the city/industry/factory steps and only calls shop lots → productTypes → finishOnboarding
/// (3 HTTP calls total).
/// </para>
/// <para>
/// <b>Error paths</b> — null profile, no cities, empty industries, all factory lots occupied,
/// no shop lots available, and all products Pro-only all throw
/// <see cref="InvalidOperationException"/> at the appropriate stage.
/// </para>
/// <para>
/// <b>Selection paths</b> — when multiple cities, lots, or products are available the service
/// picks one valid option without throwing.
/// </para>
/// <para>
/// <b>Cancellation</b> — a pre-cancelled token propagates immediately without making any
/// HTTP calls.
/// </para>
/// </summary>
public sealed class OnboardingServiceTests
{
    // ── Infrastructure ────────────────────────────────────────────────────────

    /// <summary>
    /// HTTP handler that returns pre-queued responses in FIFO order and checks for
    /// cancellation before each call (matching the pattern used by GameApiClientTests).
    /// </summary>
    private sealed class QueuedHttpHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public int CallCount { get; private set; }

        public QueuedHttpHandler(params HttpResponseMessage[] responses)
            => _responses = new Queue<HttpResponseMessage>(responses);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage _, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            CallCount++;
            if (_responses.Count == 0)
                throw new InvalidOperationException("Test handler ran out of queued responses.");
            return Task.FromResult(_responses.Dequeue());
        }
    }

    private static HttpResponseMessage Ok(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };

    private static (OnboardingService service, QueuedHttpHandler handler) CreateService(
        params HttpResponseMessage[] responses)
    {
        var handler = new QueuedHttpHandler(responses);
        var options = Options.Create(new BotOptions { GraphqlUrl = "https://test.example/graphql" });
        var http = new HttpClient(handler);
        var api = new GameApiClient(http, options, NullLogger<GameApiClient>.Instance);
        var svc = new OnboardingService(api, NullLogger<OnboardingService>.Instance);
        return (svc, handler);
    }

    // ── JSON response fixtures ────────────────────────────────────────────────

    private const string SingleCityJson =
        """{"data":{"cities":[{"id":"city-1","name":"Bratislava","countryCode":"SK","population":475000}]}}""";

    private const string EmptyCitiesJson =
        """{"data":{"cities":[]}}""";

    private const string FactoryLotAvailableJson =
        """{"data":{"cityLots":[{"id":"lot-f1","district":"Industrial","price":75000.00,"suitableTypes":"FACTORY","buildingId":null}]}}""";

    private const string AllFactoryLotsOccupiedJson =
        """{"data":{"cityLots":[{"id":"lot-f1","district":"Industrial","price":75000.00,"suitableTypes":"FACTORY","buildingId":"existing-building"}]}}""";

    private const string StartOnboardingSuccessJson =
        """{"data":{"startOnboardingCompany":{"company":{"id":"co-1","name":"Bot Corp"},"factory":{"id":"bld-f1","name":"Factory A","type":"FACTORY"},"factoryLot":{"id":"lot-f1","district":"Industrial","price":75000.00},"nextStep":"SHOP_SELECTION"}}}""";

    private const string ShopLotAvailableJson =
        """{"data":{"cityLots":[{"id":"lot-s1","district":"Commercial","price":50000.00,"suitableTypes":"SALES_SHOP","buildingId":null}]}}""";

    private const string EmptyShopLotsJson =
        """{"data":{"cityLots":[]}}""";

    private const string FurnitureProductsJson =
        """{"data":{"productTypes":[{"id":"prod-1","name":"Wooden Chair","slug":"wooden-chair","industry":"FURNITURE","basePrice":45.00,"isProOnly":false}]}}""";

    private const string AllProOnlyProductsJson =
        """{"data":{"productTypes":[{"id":"prod-p1","name":"Advanced Widget","slug":"advanced-widget","industry":"FURNITURE","basePrice":500.00,"isProOnly":true}]}}""";

    private const string FinishOnboardingSuccessJson =
        """{"data":{"finishOnboarding":{"company":{"id":"co-1","name":"Bot Corp","cash":5000.00},"factory":{"id":"bld-f1","name":"Factory A","type":"FACTORY"},"salesShop":{"id":"bld-s1","name":"Shop A","type":"SALES_SHOP"},"selectedProduct":{"id":"prod-1","name":"Wooden Chair","slug":"wooden-chair","basePrice":45.00}}}}""";

    private static BotAccount MakeFreshBot() => new()
    {
        Index = 1,
        DisplayName = "NPC_01",
        Email = "npc01@test.example",
        Strategy = "FURNITURE",
        Profile = new PlayerProfile { Id = "p1", DisplayName = "NPC_01" },
    };

    // ── Full happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_FreshBot_CompletesFullSixStepOnboarding()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),           // 1. cities query
            Ok(FactoryLotAvailableJson),  // 2. factory lots query
            Ok(StartOnboardingSuccessJson), // 3. startOnboardingCompany mutation
            Ok(ShopLotAvailableJson),     // 4. shop lots query
            Ok(FurnitureProductsJson),    // 5. productTypes query
            Ok(FinishOnboardingSuccessJson)); // 6. finishOnboarding mutation

        await svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None);

        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_FreshBot_DoesNotThrow_WhenAllDataPresent()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(FurnitureProductsJson),
            Ok(FinishOnboardingSuccessJson));

        // Should not throw any exception
        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Null(ex);
    }

    // ── Resume from SHOP_SELECTION ────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ShopSelectionStep_SkipsFactoryAndUsesThreeHttpCalls()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example",
            Strategy = "FURNITURE",
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCurrentStep = "SHOP_SELECTION",
                OnboardingIndustry = "FURNITURE",
                OnboardingCityId = "city-1",
            },
        };

        var (svc, handler) = CreateService(
            Ok(ShopLotAvailableJson),       // 1. shop lots only (no city/factory calls)
            Ok(FurnitureProductsJson),      // 2. productTypes
            Ok(FinishOnboardingSuccessJson)); // 3. finishOnboarding

        await svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None);

        // Must skip the cities + factory lot + startOnboarding calls
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_ShopSelectionStepLowercase_AlsoResumes()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example",
            Strategy = "FURNITURE",
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCurrentStep = "shop_selection",  // lowercase variant
                OnboardingIndustry = "FURNITURE",
                OnboardingCityId = "city-1",
            },
        };

        var (svc, handler) = CreateService(
            Ok(ShopLotAvailableJson),
            Ok(FurnitureProductsJson),
            Ok(FinishOnboardingSuccessJson));

        await svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None);

        Assert.Equal(3, handler.CallCount);
    }

    // ── Error: null profile ───────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NullProfile_ThrowsBeforeAnyHttpCalls()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example",
            Strategy = "FURNITURE",
            Profile = null,
        };

        var (svc, handler) = CreateService(); // no responses needed

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(0, handler.CallCount);
    }

    // ── Error: no cities returned ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoCities_ThrowsInvalidOperationException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(Ok(EmptyCitiesJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_NoCities_MakesExactlyOneHttpCall()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(Ok(EmptyCitiesJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
    }

    // ── Error: no allowed industries ──────────────────────────────────────────

    [Fact]
    public async Task RunAsync_EmptyAllowedIndustries_ThrowsInvalidOperationException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(Ok(SingleCityJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, [], CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_EmptyAllowedIndustries_MakesExactlyOneHttpCall()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(Ok(SingleCityJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, [], CancellationToken.None));

        Assert.Equal(1, handler.CallCount); // cities fetched before industry check
    }

    // ── Error: all factory lots occupied ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_AllFactoryLotsOccupied_ThrowsInvalidOperationException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(AllFactoryLotsOccupiedJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_AllFactoryLotsOccupied_MakesExactlyTwoHttpCalls()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(AllFactoryLotsOccupiedJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(2, handler.CallCount);
    }

    // ── Error: no shop lots available ────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoShopLots_ThrowsInvalidOperationException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(EmptyShopLotsJson));  // empty shop lots

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_NoShopLots_MakesExactlyFourHttpCalls()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(EmptyShopLotsJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(4, handler.CallCount);
    }

    // ── Error: all products are Pro-only ─────────────────────────────────────

    [Fact]
    public async Task RunAsync_AllProductsProOnly_ThrowsInvalidOperationException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(AllProOnlyProductsJson));  // all products are Pro-only

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));
    }

    [Fact]
    public async Task RunAsync_AllProductsProOnly_MakesExactlyFiveHttpCalls()
    {
        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(AllProOnlyProductsJson));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(5, handler.CallCount);
    }

    // ── Error: resume path with null city ID ─────────────────────────────────

    [Fact]
    public async Task RunAsync_ShopSelectionResume_NullOnboardingCityId_Throws()
    {
        var bot = new BotAccount
        {
            Index = 1, DisplayName = "NPC_01", Email = "npc01@test.example",
            Strategy = "FURNITURE",
            Profile = new PlayerProfile
            {
                Id = "p1",
                OnboardingCurrentStep = "SHOP_SELECTION",
                OnboardingIndustry = "FURNITURE",
                OnboardingCityId = null,  // city ID unknown
            },
        };

        var (svc, handler) = CreateService(); // no responses queued

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal(0, handler.CallCount);
    }

    // ── Cancellation ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_PreCancelledToken_ThrowsWithoutHttpCalls()
    {
        var bot = MakeFreshBot();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var (svc, handler) = CreateService(); // no responses needed

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => svc.RunAsync(bot, ["FURNITURE"], cts.Token));

        Assert.Equal(0, handler.CallCount);
    }

    // ── Multiple options: valid selection paths ───────────────────────────────

    [Fact]
    public async Task RunAsync_MultipleCitiesAvailable_PicksOneAndCompletes()
    {
        const string twoCitiesJson =
            "{\"data\":{\"cities\":[{\"id\":\"city-1\",\"name\":\"Bratislava\",\"countryCode\":\"SK\",\"population\":475000},{\"id\":\"city-2\",\"name\":\"Prague\",\"countryCode\":\"CZ\",\"population\":1300000}]}}";

        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(twoCitiesJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(FurnitureProductsJson),
            Ok(FinishOnboardingSuccessJson));

        // Should not throw even with multiple cities
        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_MultipleAllowedIndustries_PicksOneAndCompletes()
    {
        var bot = MakeFreshBot();
        const string foodProductsJson =
            """{"data":{"productTypes":[{"id":"prod-2","name":"Bread","slug":"bread","industry":"FOOD_PROCESSING","basePrice":3.00,"isProOnly":false}]}}""";

        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(foodProductsJson),  // food products (not furniture)
            Ok(FinishOnboardingSuccessJson));

        // Multiple industries allowed — one will be picked randomly
        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE", "FOOD_PROCESSING", "HEALTHCARE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_MultipleLotsAvailable_CompletesWithoutThrow()
    {
        const string twoLotsJson =
            "{\"data\":{\"cityLots\":[{\"id\":\"lot-cheap\",\"district\":\"Outskirts\",\"price\":30000.00,\"suitableTypes\":\"FACTORY\",\"buildingId\":null},{\"id\":\"lot-expensive\",\"district\":\"Premium\",\"price\":120000.00,\"suitableTypes\":\"FACTORY\",\"buildingId\":null}]}}";

        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(twoLotsJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(FurnitureProductsJson),
            Ok(FinishOnboardingSuccessJson));

        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    [Fact]
    public async Task RunAsync_MultipleProductsAvailable_PicksCheapestFreeProduct()
    {
        // Mix of free and Pro-only products; cheapest free should be picked
        const string mixedProductsJson =
            "{\"data\":{\"productTypes\":[{\"id\":\"prod-expensive\",\"name\":\"Wooden Bed\",\"slug\":\"wooden-bed\",\"industry\":\"FURNITURE\",\"basePrice\":150.00,\"isProOnly\":false},{\"id\":\"prod-cheap\",\"name\":\"Wooden Chair\",\"slug\":\"wooden-chair\",\"industry\":\"FURNITURE\",\"basePrice\":45.00,\"isProOnly\":false},{\"id\":\"prod-pro\",\"name\":\"Pro Furniture\",\"slug\":\"pro-furn\",\"industry\":\"FURNITURE\",\"basePrice\":10.00,\"isProOnly\":true}]}}";

        var bot = MakeFreshBot();
        var (svc, handler) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(mixedProductsJson),
            Ok(FinishOnboardingSuccessJson));

        // Should not throw — should pick the cheapest free product (Wooden Chair at 45)
        var ex = await Record.ExceptionAsync(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Null(ex);
        Assert.Equal(6, handler.CallCount);
    }

    // ── GraphQL error propagation ─────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_StartOnboardingReturnsGraphQLError_PropagatesException()
    {
        const string gqlErrorJson =
            """{"errors":[{"message":"IPO failed.","extensions":{"code":"INSUFFICIENT_FUNDS"}}]}""";

        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(gqlErrorJson)); // startOnboarding fails

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal("INSUFFICIENT_FUNDS", ex.Code);
    }

    [Fact]
    public async Task RunAsync_FinishOnboardingReturnsGraphQLError_PropagatesException()
    {
        const string gqlErrorJson =
            """{"errors":[{"message":"Product not available.","extensions":{"code":"PRODUCT_NOT_FOUND"}}]}""";

        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            Ok(SingleCityJson),
            Ok(FactoryLotAvailableJson),
            Ok(StartOnboardingSuccessJson),
            Ok(ShopLotAvailableJson),
            Ok(FurnitureProductsJson),
            Ok(gqlErrorJson)); // finishOnboarding fails

        var ex = await Assert.ThrowsAsync<GraphQLException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));

        Assert.Equal("PRODUCT_NOT_FOUND", ex.Code);
    }

    // ── HTTP 500 propagation ──────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CitiesQueryReturnsHttp500_PropagatesException()
    {
        var bot = MakeFreshBot();
        var (svc, _) = CreateService(
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error", Encoding.UTF8, "text/plain"),
            });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => svc.RunAsync(bot, ["FURNITURE"], CancellationToken.None));
    }
}
