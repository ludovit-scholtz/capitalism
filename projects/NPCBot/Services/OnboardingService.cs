using Capitalism.NPCBot.Models;
using Microsoft.Extensions.Logging;

namespace Capitalism.NPCBot.Services;

/// <summary>
/// Drives the full NPC onboarding flow: city selection → industry selection →
/// IPO → factory lot → shop lot.
/// Uses <see cref="StartOnboardingCompany"/> followed by <see cref="FinishOnboarding"/>.
/// </summary>
public sealed class OnboardingService : IOnboardingService
{
    // ── Queries / mutations ───────────────────────────────────────────────────

    private const string CitiesQuery = """
        { cities { id name countryCode population } }
        """;

    private const string CityLotsQuery = """
        query CityLots($cityId: UUID!) {
          cityLots(cityId: $cityId) {
            id district price suitableTypes buildingId
          }
        }
        """;

    private const string ProductsQuery = """
        query Products($industry: String!) {
          productTypes(industry: $industry) {
            id name slug industry basePrice isProOnly
          }
        }
        """;

    private const string StartOnboardingMutation = """
        mutation StartOnboardingCompany($input: StartOnboardingCompanyInput!) {
          startOnboardingCompany(input: $input) {
            company { id name }
            factory { id name type }
            factoryLot { id district price }
            nextStep
          }
        }
        """;

    private const string FinishOnboardingMutation = """
        mutation FinishOnboarding($input: FinishOnboardingInput!) {
          finishOnboarding(input: $input) {
            company { id name cash }
            factory { id name type }
            salesShop { id name type }
            selectedProduct { id name slug basePrice }
          }
        }
        """;

    private readonly GameApiClient _api;
    private readonly ILogger<OnboardingService> _logger;
    private readonly Random _rng = new();

    public OnboardingService(GameApiClient api, ILogger<OnboardingService> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>
    /// Runs the complete onboarding flow for a bot that has not yet started.
    /// If onboarding is already in the ShopSelection step, resumes with FinishOnboarding.
    /// </summary>
    public async Task RunAsync(
        BotAccount bot,
        string[] allowedIndustries,
        CancellationToken ct)
    {
        if (bot.Profile is null)
            throw new InvalidOperationException($"{bot}: Profile must be loaded before onboarding.");

        // Resume from shop step if partially done
        if (OnboardingHelpers.ShouldResumeFromShopStep(bot))
        {
            _logger.LogInformation("{Bot} Resuming from shop selection step.", bot);
            await FinishOnboardingAsync(bot, bot.Profile.OnboardingIndustry!, ct);
            return;
        }

        // Pick a random city
        var cities = await FetchCitiesAsync(ct);
        if (cities.Count == 0)
            throw new InvalidOperationException("No cities available.");
        var city = cities[_rng.Next(cities.Count)];
        _logger.LogInformation("{Bot} Selected city: {City} ({Country})", bot, city.Name, city.CountryCode);

        // Pick a random allowed industry
        if (allowedIndustries.Length == 0)
            throw new InvalidOperationException("No allowed industries configured.");
        var industry = allowedIndustries[_rng.Next(allowedIndustries.Length)];
        _logger.LogInformation("{Bot} Selected industry: {Industry}", bot, industry);

        // Start onboarding (company + factory lot)
        var factoryLotId = await StartOnboardingAsync(bot, city.Id, industry, ct);

        // Finish onboarding (product selection + shop lot)
        await FinishOnboardingAsync(bot, industry, ct, factoryLotId, city.Id);
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<string> StartOnboardingAsync(
        BotAccount bot,
        string cityId,
        string industry,
        CancellationToken ct)
    {
        var lots = await FetchCityLotsAsync(cityId, ct);
        var factoryLot = OnboardingHelpers.PickCheapestAvailableLot(lots, "FACTORY")
            ?? throw new InvalidOperationException($"{bot}: No available factory lot in city {cityId}.");

        var companyName = $"{bot.DisplayName} Corp";
        _logger.LogInformation("{Bot} Starting onboarding — company: {Company}, lot: {Lot}",
            bot, companyName, factoryLot.Id);

        var result = await _api.ExecuteAsync<StartOnboardingWrapper>(
            StartOnboardingMutation,
            new
            {
                input = new
                {
                    companyName,
                    industry,
                    cityId,
                    factoryLotId = factoryLot.Id,
                    ipoRaiseTarget = 200_000m,
                }
            },
            bearerToken: bot.Token,
            ct: ct);

        _logger.LogInformation("{Bot} StartOnboardingCompany succeeded. Factory: {Factory}",
            bot, result.StartOnboardingCompany.Factory.Name);
        return factoryLot.Id;
    }

    private async Task FinishOnboardingAsync(
        BotAccount bot,
        string industry,
        CancellationToken ct,
        string? _factoryLotId = null,
        string? cityId = null)
    {
        // Determine which city to query for shop lots
        var shopCityId = cityId ?? bot.Profile?.OnboardingCityId
            ?? throw new InvalidOperationException($"{bot}: City ID unknown for shop lot selection.");

        var lots = await FetchCityLotsAsync(shopCityId, ct);
        var shopLot = OnboardingHelpers.PickCheapestAvailableLot(lots, "SALES_SHOP")
            ?? throw new InvalidOperationException($"{bot}: No available shop lot in city {shopCityId}.");

        // Pick a starter product for the industry
        var products = await FetchStarterProductsAsync(industry, ct);
        var product = OnboardingHelpers.PickCheapestFreeProduct(products)
            ?? throw new InvalidOperationException($"{bot}: No free starter product for industry {industry}.");

        _logger.LogInformation("{Bot} Finishing onboarding — product: {Product}, shop lot: {Lot}",
            bot, product.Name, shopLot.Id);

        var result = await _api.ExecuteAsync<FinishOnboardingWrapper>(
            FinishOnboardingMutation,
            new { input = new { productTypeId = product.Id, shopLotId = shopLot.Id } },
            bearerToken: bot.Token,
            ct: ct);

        _logger.LogInformation("{Bot} FinishOnboarding succeeded. Shop: {Shop}, Product: {Product}",
            bot, result.FinishOnboarding.SalesShop.Name, result.FinishOnboarding.SelectedProduct.Name);
    }

    private async Task<List<CitySummary>> FetchCitiesAsync(CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<CitiesWrapper>(CitiesQuery, ct: ct);
        return result.Cities;
    }

    private async Task<List<BuildingLotSummary>> FetchCityLotsAsync(string cityId, CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<CityLotsWrapper>(
            CityLotsQuery,
            new { cityId },
            ct: ct);
        return result.CityLots;
    }

    private async Task<List<ProductTypeSummary>> FetchStarterProductsAsync(string industry, CancellationToken ct)
    {
        var result = await _api.ExecuteAsync<ProductsWrapper>(
            ProductsQuery,
            new { industry },
            ct: ct);
        return result.ProductTypes;
    }

    // ── Wrapper types ─────────────────────────────────────────────────────────

    private sealed record CitiesWrapper(List<CitySummary> Cities);
    private sealed record CityLotsWrapper(List<BuildingLotSummary> CityLots);
    private sealed record ProductsWrapper(List<ProductTypeSummary> ProductTypes);

    private sealed record StartOnboardingCompanyResult(
        CompanySummary Company,
        BuildingSummary Factory,
        BuildingLotSummary FactoryLot,
        string NextStep);

    private sealed record FinishOnboardingResult(
        CompanySummary Company,
        BuildingSummary Factory,
        BuildingSummary SalesShop,
        ProductTypeSummary SelectedProduct);

    private sealed record StartOnboardingWrapper(StartOnboardingCompanyResult StartOnboardingCompany);
    private sealed record FinishOnboardingWrapper(FinishOnboardingResult FinishOnboarding);
}
