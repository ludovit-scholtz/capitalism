using Api.Configuration;
using Api.Data.Entities;
using Api.Engine;
using Api.Types;
using Api.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Api.Data;

/// <summary>
/// Seeds the database with initial game data: admin player, cities, resources, products, and recipes.
/// Called at application startup.
/// </summary>
public sealed partial class AppDbInitializer(
    AppDbContext dbContext,
    IOptions<SeedDataOptions> seedOptions,
    NbsExchangeRateService nbsExchangeRateService)
{
    /// <summary>
    /// Ensures the schema is up to date and seeds initial data if missing.
    /// Runtime relational providers apply migrations; in-memory test providers use EnsureCreated.
    /// </summary>
    public async Task InitializeAsync()
    {
        await SafelyApplyMigrationsAsync();

        if (!await dbContext.Players.AnyAsync(p => p.Email == seedOptions.Value.AdminEmail))
        {
            var hasher = new PasswordHasher<Player>();
            var admin = new Player
            {
                Id = Guid.NewGuid(),
                Email = seedOptions.Value.AdminEmail,
                DisplayName = seedOptions.Value.AdminDisplayName,
                Role = PlayerRole.Admin,
                ActiveAccountType = AccountContextType.Person,
                CreatedAtUtc = DateTime.UtcNow
            };
            admin.PasswordHash = hasher.HashPassword(admin, seedOptions.Value.AdminPassword);
            dbContext.Players.Add(admin);
            await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(dbContext, admin, 200_000m);
        }

        if (!await dbContext.GameStates.AnyAsync())
        {
            dbContext.GameStates.Add(new GameState
            {
                Id = 1,
                CurrentTick = 0,
                TickIntervalSeconds = seedOptions.Value.TickIntervalSeconds,
                GameStartedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            var gameState = await dbContext.GameStates.FirstDeterministicAsync();
            if (gameState.TickIntervalSeconds <= 0)
            {
                gameState.TickIntervalSeconds = seedOptions.Value.TickIntervalSeconds;
            }

            if (gameState.TaxCycleTicks != GameConstants.TicksPerYear)
            {
                gameState.TaxCycleTicks = GameConstants.TicksPerYear;
            }

            if (gameState.GameStartedAtUtc == default)
            {
                gameState.GameStartedAtUtc = DateTime.UtcNow;
            }
        }

        await EnsureRealWorldBillionaireBenchmarksAsync();

        if (!await dbContext.ResourceTypes.AnyAsync())
        {
            SeedResources();
        }
        else
        {
            await EnsureResourceCatalogBackfillAsync();
        }

        if (!await dbContext.Cities.AnyAsync())
        {
            SeedCities();
        }

        if (!await dbContext.ProductTypes.AnyAsync())
        {
            SeedProducts();
        }

        await dbContext.SaveChangesAsync();

        await EnsureCityResourceCoverageBackfillAsync();
        await dbContext.SaveChangesAsync();

        if (!await dbContext.ProductRecipes.AnyAsync())
        {
            await SeedRecipesAsync();
            await dbContext.SaveChangesAsync();
        }

        // Seed FX rates before lot generation so LandService can apply correct currency multipliers.
        if (!await dbContext.FxRates.AnyAsync())
        {
            await SeedFxRatesAsync();
        }

        await EnsureRequiredFxRatesAsync();

        if (!await dbContext.BuildingLots.AnyAsync())
        {
            await SeedBuildingLotsAsync();
            await SeedNewCityLotsAsync();
            await dbContext.SaveChangesAsync();
        }

        // Idempotent: ensure the Carpathian Gold Seam lot is present (added in the
        // mining premium increment).  Databases seeded before this change will not have
        // the lot; databases created after the initial seed will have it already.
        await EnsureCarpathianGoldSeamLotAsync();
        await EnsureBratislavaMiningLotsAsync();
        await EnsurePragueMiningLotsAsync();
        await EnsureViennaMiningLotsAsync();

        // Idempotent: ensure curated building lots for Berlin and Warsaw exist.
        // Databases seeded before this change will rely on auto-generated lots only;
        // this adds the hand-crafted lots with realistic district names and GPS coordinates.
        await EnsureBerlinWarsawLotsAsync();
        await dbContext.SaveChangesAsync();

        // Idempotent: ensure Electronics starter products (Basic Electronics, LED Screen,
        // and Circuit Board with direct Silicon recipe) exist.  Databases seeded before
        // this change will not have basic-electronics / led-screen and may have the old
        // product-ingredient circuit-board recipe.
        await EnsureElectronicsStarterProductsAsync();

        // Idempotent: ensure Construction starter products (Residential Block, Commercial Block,
        // and Industrial Block with direct Iron Ore recipe) exist.
        await EnsureConstructionStarterProductsAsync();

        // Idempotent: ensure Pharmaceuticals starter products (Aspirin, Vitamin Capsule,
        // and Antibiotic with direct Gold recipe) exist.
        await EnsurePharmaceuticalsStarterProductsAsync();

        // Idempotent: ensure Energy starter products (Coal Briquette, Heating Oil,
        // and Industrial Fuel with direct Coal recipe) exist.
        await EnsureEnergyStarterProductsAsync();

        // Idempotent: ensure Logistics starter products (Shipping Bag, Storage Sack,
        // and Cargo Pack with direct Cotton recipe) exist.
        await EnsureLogisticsStarterProductsAsync();

        var currentTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        await LandService.EnsureMinimumAvailableLotsAsync(dbContext, currentTick);
        await dbContext.SaveChangesAsync();

        // Seed initial weather forecasts for cities that don't have any yet.
        var citiesWithoutForecast = await dbContext.Cities
            .Where(c => !dbContext.CityWeatherForecasts.Any(f => f.CityId == c.Id))
            .Select(c => c.Id)
            .ToListAsync();
        if (citiesWithoutForecast.Count > 0)
        {
            foreach (var cityId in citiesWithoutForecast)
            {
                var forecasts = WeatherService.SeedForecast(cityId, currentTick, WeatherService.ForecastWindow);
                dbContext.CityWeatherForecasts.AddRange(forecasts);
            }
            await dbContext.SaveChangesAsync();
        }

        var hasLegacyCompanyCashConstraint = false;

        // Seed government-owned baseline media houses in every city (idempotent).
        if (!hasLegacyCompanyCashConstraint)
        {
            await SeedGovernmentMediaHousesAsync();
        }

        // Seed one government bank building in every city so each local market has
        // an immediately visible default bank on the public banking page.
        if (!hasLegacyCompanyCashConstraint)
        {
            await EnsureGovernmentBankBuildingsAsync();
        }

        // Ensure one government bank account exists for each unique city currency (idempotent).
        await EnsureGovernmentBankAccountsAsync();

        // Personal money is stored only in settlement bank accounts.
        await EnsurePlayerSettlementAccountsAsync();

        // Existing buildings created before bank-account provisioning must be linked to
        // a company-owned account in their city currency on startup.
        await EnsureBuildingBankAccountsAsync();

        // Property buildings must always have numeric occupancy and known area.
        await EnsurePropertyBuildingDefaultsAsync();

        // Idempotent: ensure seasonal demand multipliers exist for all product types.
        await EnsureDemandSeasonalitySeedAsync();
        await EnsureEconomicCycleSeedAsync();

        // Idempotent: ensure resource replenishment schedules exist for all cities.
        await EnsureResourceReplenishmentSchedulesAsync();

        // Idempotent: backfill OriginalMaterialQuantity for lots that pre-date depletion tracking.
        await EnsureLotOriginalMaterialQuantityBackfillAsync();

        // Idempotent: ensure FoodProcessing and Healthcare products are flagged as perishable.
        await EnsurePerishableProductsAsync();
    }

    private async Task SeedFxRatesAsync()
    {
        var rates = await nbsExchangeRateService.FetchLatestRatesAsync();
        dbContext.FxRates.AddRange(rates);
        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureRequiredFxRatesAsync()
    {
        var cityCurrencyCodes = (await dbContext.Cities
                .AsNoTracking()
                .Select(city => city.CurrencyCode)
                .ToListAsync())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.ToUpperInvariant());

        var requiredCodes = cityCurrencyCodes
            .Concat(FxRateHelper.FallbackEurRates.Keys)
            .Where(code => !string.Equals(code, "EUR", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requiredCodes.Count == 0)
        {
            return;
        }

        var existingCodes = (await dbContext.FxRates
                .AsNoTracking()
                .Where(rate => rate.BaseCurrencyCode == "EUR")
                .Select(rate => rate.QuoteCurrencyCode)
                .ToListAsync())
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.ToUpperInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingCodes = requiredCodes
            .Where(code => !existingCodes.Contains(code))
            .ToList();

        if (missingCodes.Count == 0)
        {
            return;
        }

        var fetchedRatesByCode = (await nbsExchangeRateService.FetchLatestRatesAsync())
            .Where(rate => rate.BaseCurrencyCode == "EUR")
            .GroupBy(rate => rate.QuoteCurrencyCode, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(rate => rate.RateDate).First(),
                StringComparer.OrdinalIgnoreCase);

        var fallbackRateDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var code in missingCodes)
        {
            if (fetchedRatesByCode.TryGetValue(code, out var fetchedRate))
            {
                dbContext.FxRates.Add(new FxRate
                {
                    Id = Guid.NewGuid(),
                    BaseCurrencyCode = fetchedRate.BaseCurrencyCode,
                    QuoteCurrencyCode = code,
                    Rate = fetchedRate.Rate,
                    RateDate = fetchedRate.RateDate,
                    FetchedAtUtc = fetchedRate.FetchedAtUtc,
                    Source = fetchedRate.Source
                });

                continue;
            }

            if (FxRateHelper.FallbackEurRates.TryGetValue(code, out var fallbackRate))
            {
                dbContext.FxRates.Add(new FxRate
                {
                    Id = Guid.NewGuid(),
                    BaseCurrencyCode = "EUR",
                    QuoteCurrencyCode = code,
                    Rate = fallbackRate,
                    RateDate = fallbackRateDate,
                    FetchedAtUtc = DateTime.UtcNow,
                    Source = "FALLBACK"
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent: backfills <see cref="BuildingLot.OriginalMaterialQuantity"/> for any lot
    /// that has a raw-material deposit but was created before depletion tracking was introduced.
    /// Sets <c>OriginalMaterialQuantity = MaterialQuantity</c> when the original value is still null.
    /// </summary>
    private async Task EnsureLotOriginalMaterialQuantityBackfillAsync()
    {
        var lotsToBackfill = await dbContext.BuildingLots
            .Where(lot => lot.MaterialQuantity.HasValue
                && lot.MaterialQuantity > 0m
                && !lot.OriginalMaterialQuantity.HasValue)
            .ToListAsync();

        foreach (var lot in lotsToBackfill)
        {
            lot.OriginalMaterialQuantity = lot.MaterialQuantity;
        }

        if (lotsToBackfill.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Idempotent: ensures one <see cref="ResourceReplenishmentSchedule"/> row exists for every city.
    /// Inserts missing rows with <see cref="GameConstants.ReplenishmentIntervalTicks"/> as the first interval.
    /// </summary>
    private async Task EnsureResourceReplenishmentSchedulesAsync()
    {
        var cities = await dbContext.Cities.ToListAsync();
        var existingCityIds = await dbContext.ResourceReplenishmentSchedules
            .Select(s => s.CityId)
            .ToListAsync();

        var currentTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(g => g.CurrentTick)
            .FirstOrDefaultDeterministicAsync();

        foreach (var city in cities)
        {
            if (existingCityIds.Contains(city.Id))
                continue;

            dbContext.ResourceReplenishmentSchedules.Add(new Entities.ResourceReplenishmentSchedule
            {
                Id = Guid.NewGuid(),
                CityId = city.Id,
                LastReplenishmentTick = 0,
                NextReplenishmentTick = currentTick + GameConstants.ReplenishmentIntervalTicks,
            });
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Idempotent: sets <see cref="ProductType.IsPerishable"/> = true for all FoodProcessing
    /// and Healthcare industry products.  Safe to run multiple times — only updates rows that
    /// are not already marked perishable.
    /// </summary>
    private async Task EnsurePerishableProductsAsync()
    {
        var perishableIndustries = new[] { Industry.FoodProcessing, Industry.Healthcare };

        var productsToMark = await dbContext.ProductTypes
            .Where(p => perishableIndustries.Contains(p.Industry) && !p.IsPerishable)
            .ToListAsync();

        foreach (var product in productsToMark)
        {
            product.IsPerishable = true;
        }

        if (productsToMark.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }
    }

}
