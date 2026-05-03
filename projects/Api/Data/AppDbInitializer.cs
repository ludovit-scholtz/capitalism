using Api.Configuration;
using Api.Data.Entities;
using Api.Engine;
using Api.Types;
using Api.Utilities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;

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
            dbContext.GameStates.Add(new GameState { Id = 1, CurrentTick = 0, TickIntervalSeconds = seedOptions.Value.TickIntervalSeconds });
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
        }

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

        // Idempotent: ensure Electronics starter products (Basic Electronics, LED Screen,
        // and Circuit Board with direct Silicon recipe) exist.  Databases seeded before
        // this change will not have basic-electronics / led-screen and may have the old
        // product-ingredient circuit-board recipe.
        await EnsureElectronicsStarterProductsAsync();

        // Idempotent: ensure Construction starter products (Residential Block, Commercial Block,
        // and Industrial Block with direct Iron Ore recipe) exist.
        await EnsureConstructionStarterProductsAsync();

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
    }

    private async Task SeedFxRatesAsync()
    {
        var rates = await nbsExchangeRateService.FetchLatestRatesAsync();
        dbContext.FxRates.AddRange(rates);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds one government-owned media house of each type (NEWSPAPER, RADIO, TV) in every city.
    /// Idempotent: ensures the government actor exists, then inserts only missing outlets.
    /// Government outlets provide a baseline media market from day one so players always have
    /// something to route their marketing budgets through.
    /// </summary>
    private async Task SeedGovernmentMediaHousesAsync()
    {
        var (_, govCompany) = await EnsureGovernmentActorAsync();
        var govCompanyId = govCompany.Id;

        var cities = await dbContext.Cities.ToListAsync();

        // Baseline initial content for government outlets.
        // Higher than 0 so they display at a non-zero ranking until players invest more.
        const decimal InitialContentValue = 1_000m;

        foreach (var city in cities)
        {
            // NEWSPAPER
            var newspaperId = CreateDeterministicGuid($"gov-media:{city.Id}:newspaper");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == newspaperId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = newspaperId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} Gazette",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Newspaper,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }

            // RADIO
            var radioId = CreateDeterministicGuid($"gov-media:{city.Id}:radio");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == radioId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = radioId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} Radio",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Radio,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }

            // TV
            var tvId = CreateDeterministicGuid($"gov-media:{city.Id}:tv");
            if (!await dbContext.Buildings.AnyAsync(b => b.Id == tvId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = tvId,
                    CompanyId = govCompanyId,
                    CityId = city.Id,
                    Type = BuildingType.MediaHouse,
                    Name = $"{city.Name} TV",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    MediaType = Entities.MediaType.Tv,
                    ContentValue = InitialContentValue,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Seeds one government-owned bank building in every city with baseline public rates.
    /// These banks are immediately visible on the public banking page after restart.
    /// </summary>
    private async Task EnsureGovernmentBankBuildingsAsync()
    {
        var (_, govCompany) = await EnsureGovernmentActorAsync();
        var currentTick = await dbContext.GameStates
            .AsNoTracking()
            .Select(state => state.CurrentTick)
            .FirstOrDefaultDeterministicAsync();
        var cities = await dbContext.Cities.AsNoTracking().ToListAsync();

        foreach (var city in cities)
        {
            var bankId = CreateDeterministicGuid($"gov-bank-building:{city.Id}");
            var baseCapitalRequirement = Mutation.GetBaseCapitalRequirement(city.CurrencyCode ?? "EUR");

            if (!await dbContext.Buildings.AnyAsync(b => b.Id == bankId))
            {
                dbContext.Buildings.Add(new Building
                {
                    Id = bankId,
                    CompanyId = govCompany.Id,
                    CityId = city.Id,
                    Type = BuildingType.Bank,
                    Name = $"{city.Name} Government Bank",
                    Latitude = city.Latitude,
                    Longitude = city.Longitude,
                    Level = 1,
                    DepositInterestRatePercent = 0m,
                    LendingInterestRatePercent = 20m,
                    TotalDeposits = baseCapitalRequirement,
                    BaseCapitalDeposited = true,
                    IsGovernmentOwned = true,
                    PowerStatus = Entities.PowerStatus.Powered,
                    BuiltAtUtc = DateTime.UtcNow,
                });
            }

            var baseDepositId = CreateDeterministicGuid($"gov-bank-base-deposit:{city.Id}");
            if (!await dbContext.BankAccounts.AnyAsync(a => a.Id == baseDepositId))
            {
                dbContext.BankAccounts.Add(new BankAccount
                {
                    Id = baseDepositId,
                    AccountNumber = GenerateDeterministicAccountNumber($"gov-bank-base-deposit:{city.Id}"),
                    CurrencyCode = city.CurrencyCode ?? "EUR",
                    CompanyId = govCompany.Id,
                    BankBuildingId = bankId,
                    Balance = baseCapitalRequirement,
                    DepositInterestRatePercent = 0m,
                    IsBaseCapitalDeposit = true,
                    DepositedAtTick = currentTick,
                    CreatedAtUtc = DateTime.UtcNow,
                    TotalInterestPaid = 0m,
                    IsGovernmentAccount = false,
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task<(Player Player, Company Company)> EnsureGovernmentActorAsync()
    {
        const string GovEmail = "government@capitalism.game";
        const string GovDisplayName = "Government";

        var govPlayer = await dbContext.Players
            .FirstOrDefaultAsync(player => player.Email == GovEmail);

        if (govPlayer is null)
        {
            var hasher = new PasswordHasher<Player>();
            govPlayer = new Player
            {
                Id = CreateDeterministicGuid("player:government"),
                Email = GovEmail,
                DisplayName = GovDisplayName,
                Role = PlayerRole.Player,
                ActiveAccountType = AccountContextType.Person,
                CreatedAtUtc = DateTime.UtcNow,
            };
            govPlayer.PasswordHash = hasher.HashPassword(govPlayer, Guid.NewGuid().ToString());
            dbContext.Players.Add(govPlayer);
            await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(dbContext, govPlayer, 0m);
        }

        Company? govCompany;
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true)
        {
            var companiesByName = await dbContext.Companies
                .Where(company => company.Name == GovDisplayName)
                .ToListAsync();
            govCompany = companiesByName.FirstOrDefault(company => company.PlayerId == govPlayer.Id);
        }
        else
        {
            govCompany = await dbContext.Companies
                .FirstOrDefaultAsync(company => company.PlayerId == govPlayer.Id && company.Name == GovDisplayName);
        }

        if (govCompany is null)
        {
            govCompany = new Company
            {
                Id = CreateDeterministicGuid("company:government"),
                PlayerId = govPlayer.Id,
                Name = GovDisplayName,
                FoundedAtUtc = DateTime.UtcNow,
            };
            dbContext.Companies.Add(govCompany);
        }

        return (govPlayer, govCompany);
    }

    /// <summary>
    /// Ensures exactly one government-owned bank account exists for each unique city currency.
    /// Called at startup to guarantee every city has a default bank for auto-assigning buildings.
    /// Idempotent: creates only accounts that do not yet exist.
    /// </summary>
    private async Task EnsureGovernmentBankAccountsAsync()
    {
        var currencies = await dbContext.Cities
            .Select(c => c.CurrencyCode)
            .Distinct()
            .ToListAsync();

        foreach (var currencyCode in currencies)
        {
            var exists = await dbContext.BankAccounts
                .AnyAsync(a => a.CurrencyCode == currencyCode && a.IsGovernmentAccount);

            if (!exists)
            {
                var govAccountId = CreateDeterministicGuid($"gov-bank:{currencyCode}");
                dbContext.BankAccounts.Add(new BankAccount
                {
                    Id = govAccountId,
                    AccountNumber = GenerateDeterministicAccountNumber($"gov-bank:{currencyCode}"),
                    CurrencyCode = currencyCode,
                    Balance = 0m,
                    CompanyId = null,
                    IsGovernmentAccount = true,
                    CreatedAtUtc = DateTime.UtcNow,
                });
            }
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsurePlayerSettlementAccountsAsync()
    {
        var players = await dbContext.Players.ToListAsync();

        foreach (var player in players)
        {
            await PersonalBankAccountService.EnsureTrackedSettlementAccountAsync(dbContext, player);
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task EnsureBuildingBankAccountsAsync()
    {
        var buildingsMissingAccounts = await dbContext.Buildings
            .Include(building => building.City)
            .Where(building => building.BankAccountId == null)
            .ToListAsync();

        if (buildingsMissingAccounts.Count == 0)
        {
            return;
        }

        foreach (var building in buildingsMissingAccounts)
        {
            await BuildingBankAccountProvisioning.EnsureBuildingAssignedAccountAsync(
                dbContext,
                building,
                building.City?.CurrencyCode);
        }

        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Generates a deterministic 16-digit account number from a seed string.
    /// The result is always exactly 16 decimal digits, unique per seed within a server.
    /// </summary>
    private static string GenerateDeterministicAccountNumber(string seed)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        var value = BitConverter.ToUInt64(bytes, 0);
        return (value % 10_000_000_000_000_000UL).ToString("D16");
    }

    private void SeedResources()
    {
        dbContext.ResourceTypes.AddRange(GetResourceSeeds().Select(seed => new ResourceType
        {
            Id = CreateDeterministicGuid($"resource:{seed.Slug}"),
            Name = seed.Name,
            Slug = seed.Slug,
            Category = seed.Category,
            BasePrice = seed.BasePrice,
            WeightPerUnit = seed.WeightPerUnit,
            UnitName = seed.UnitName,
            UnitSymbol = seed.UnitSymbol,
            Description = seed.Description,
            ImageUrl = CreateEmojiImageDataUrl(seed.Icon, seed.BackgroundColor, seed.AccentColor)
        }));
    }

    private async Task EnsureResourceCatalogBackfillAsync()
    {
        var existingSlugs = await dbContext.ResourceTypes
            .Select(resource => resource.Slug)
            .ToListAsync();

        var existingSlugSet = existingSlugs
            .Where(slug => !string.IsNullOrWhiteSpace(slug))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingSeeds = GetResourceSeeds()
            .Where(seed => !existingSlugSet.Contains(seed.Slug))
            .ToList();

        if (missingSeeds.Count == 0)
        {
            return;
        }

        dbContext.ResourceTypes.AddRange(missingSeeds.Select(seed => new ResourceType
        {
            Id = CreateDeterministicGuid($"resource:{seed.Slug}"),
            Name = seed.Name,
            Slug = seed.Slug,
            Category = seed.Category,
            BasePrice = seed.BasePrice,
            WeightPerUnit = seed.WeightPerUnit,
            UnitName = seed.UnitName,
            UnitSymbol = seed.UnitSymbol,
            Description = seed.Description,
            ImageUrl = CreateEmojiImageDataUrl(seed.Icon, seed.BackgroundColor, seed.AccentColor)
        }));

        await dbContext.SaveChangesAsync();
    }

    private void SeedCities()
    {
        dbContext.Cities.AddRange(
            new City { Id = CreateDeterministicGuid("city:bratislava"), Name = "Bratislava", CountryCode = "SK", CurrencyCode = "EUR", Latitude = 48.1486, Longitude = 17.1077, Population = 475_000, AverageRentPerSqm = 14m, BaseSalaryPerManhour = 18m, FuelPriceIndex = 1.00m },
            new City { Id = CreateDeterministicGuid("city:prague"), Name = "Prague", CountryCode = "CZ", CurrencyCode = "CZK", Latitude = 50.0755, Longitude = 14.4378, Population = 1_350_000, AverageRentPerSqm = 18m, BaseSalaryPerManhour = 22m, FuelPriceIndex = 0.95m },
            new City { Id = CreateDeterministicGuid("city:vienna"), Name = "Vienna", CountryCode = "AT", CurrencyCode = "EUR", Latitude = 48.2082, Longitude = 16.3738, Population = 1_900_000, AverageRentPerSqm = 22m, BaseSalaryPerManhour = 28m, FuelPriceIndex = 1.05m },
            new City { Id = CreateDeterministicGuid("city:new-york"), Name = "New York", CountryCode = "US", CurrencyCode = "USD", Latitude = 40.7128, Longitude = -74.0060, Population = 8_336_000, AverageRentPerSqm = 55m, BaseSalaryPerManhour = 35m, FuelPriceIndex = 0.80m },
            new City { Id = CreateDeterministicGuid("city:london"), Name = "London", CountryCode = "GB", CurrencyCode = "GBP", Latitude = 51.5074, Longitude = -0.1278, Population = 8_982_000, AverageRentPerSqm = 62m, BaseSalaryPerManhour = 32m, FuelPriceIndex = 1.25m },
            new City { Id = CreateDeterministicGuid("city:beijing"), Name = "Beijing", CountryCode = "CN", CurrencyCode = "CNY", Latitude = 39.9042, Longitude = 116.4074, Population = 21_540_000, AverageRentPerSqm = 30m, BaseSalaryPerManhour = 20m, FuelPriceIndex = 0.70m },
            new City { Id = CreateDeterministicGuid("city:delhi"), Name = "Delhi", CountryCode = "IN", CurrencyCode = "INR", Latitude = 28.6139, Longitude = 77.2090, Population = 32_000_000, AverageRentPerSqm = 8m, BaseSalaryPerManhour = 6m, FuelPriceIndex = 0.65m });
    }


    private async Task SeedCityResourcesAsync()
    {
        await EnsureCityResourceCoverageBackfillAsync();
    }

    private async Task EnsureCityResourceCoverageBackfillAsync()
    {
        var cities = await dbContext.Cities.ToDictionaryAsync(city => city.Name);
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(resource => resource.Slug);
        var existingKeys = await dbContext.CityResources
            .Select(cityResource => new { cityResource.CityId, cityResource.ResourceTypeId })
            .ToListAsync();

        var existingKeySet = existingKeys
            .Select(key => $"{key.CityId:N}:{key.ResourceTypeId:N}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var seed in GetCityResourceSeeds())
        {
            if (!cities.TryGetValue(seed.CityName, out var city))
            {
                continue;
            }

            if (!resources.TryGetValue(seed.ResourceSlug, out var resource))
            {
                continue;
            }

            var key = $"{city.Id:N}:{resource.Id:N}";
            if (existingKeySet.Contains(key))
            {
                continue;
            }

            dbContext.CityResources.Add(new CityResource
            {
                Id = CreateDeterministicGuid($"city-resource:{seed.CityName}:{seed.ResourceSlug}"),
                CityId = city.Id,
                ResourceTypeId = resource.Id,
                Abundance = seed.Abundance,
            });

            existingKeySet.Add(key);
        }
    }

    private async Task SeedRecipesAsync()
    {
        var resources = await dbContext.ResourceTypes.ToDictionaryAsync(r => r.Slug);
        var products = await dbContext.ProductTypes.ToDictionaryAsync(p => p.Slug);

        foreach (var seed in GetProductSeeds())
        {
            foreach (var ingredient in seed.Ingredients)
            {
                dbContext.ProductRecipes.Add(new ProductRecipe
                {
                    Id = Guid.NewGuid(),
                    ProductTypeId = products[seed.Slug].Id,
                    ResourceTypeId = ingredient.ResourceSlug is not null ? resources[ingredient.ResourceSlug].Id : null,
                    InputProductTypeId = ingredient.ProductSlug is not null ? products[ingredient.ProductSlug].Id : null,
                    Quantity = ingredient.Quantity
                });
            }
        }
    }

    private static IReadOnlyList<ResourceSeed> GetResourceSeeds() =>
    [
        Resource("Wood", "wood", "ORGANIC", 10m, 5m, "Ton", "t", "Harvested timber used in furniture, packaging, and construction.", "🪵", "#8B5A2B", "#D4A373"),
        Resource("Iron Ore", "iron-ore", "MINERAL", 25m, 10m, "Ton", "t", "Raw iron ore used to smelt metal components, fasteners, and structural goods.", "⛏️", "#6B7280", "#9CA3AF"),
        Resource("Coal", "coal", "MINERAL", 8m, 8m, "Ton", "t", "Industrial fuel used in heat-intensive processing, metallurgy, and battery chemistry.", "🪨", "#1F2937", "#4B5563"),
        Resource("Gold", "gold", "MINERAL", 500m, 0.1m, "Kilogram", "kg", "Precious conductive metal used in premium electronics and contact surfaces.", "🥇", "#B45309", "#F59E0B"),
        Resource("Chemical Minerals", "chemical-minerals", "MINERAL", 30m, 3m, "Ton", "t", "Industrial mineral feedstock used in chemicals, coatings, polymers, and medicine.", "🧪", "#7C3AED", "#A78BFA"),
        Resource("Cotton", "cotton", "ORGANIC", 15m, 1m, "Ton", "t", "Soft natural fibre used in healthcare textiles, insulation, and consumer fabric goods.", "🧵", "#E5E7EB", "#94A3B8"),
        Resource("Grain", "grain", "ORGANIC", 5m, 2m, "Ton", "t", "Agricultural staple milled into flour and processed into packaged foods.", "🌾", "#CA8A04", "#FCD34D"),
        Resource("Silicon", "silicon", "MINERAL", 40m, 2m, "Kilogram", "kg", "High-purity mineral used for wafers, glass, and electronics manufacturing.", "💠", "#0EA5E9", "#67E8F9")
    ];

    private static IReadOnlyList<CityResourceSeed> GetCityResourceSeeds() =>
    [
        CityResource("Bratislava", "wood", 0.7m),
        CityResource("Bratislava", "grain", 0.6m),
        CityResource("Bratislava", "iron-ore", 0.4m),
        CityResource("Bratislava", "chemical-minerals", 0.3m),

        CityResource("Prague", "wood", 0.7m),
        CityResource("Prague", "grain", 0.6m),
        CityResource("Prague", "coal", 0.6m),
        CityResource("Prague", "silicon", 0.3m),

        CityResource("Vienna", "wood", 0.7m),
        CityResource("Vienna", "grain", 0.6m),
        CityResource("Vienna", "cotton", 0.5m),
        CityResource("Vienna", "gold", 0.1m),

        CityResource("New York", "wood", 0.7m),
        CityResource("New York", "grain", 0.6m),
        CityResource("New York", "silicon", 0.5m),
        CityResource("New York", "coal", 0.4m),
        CityResource("New York", "iron-ore", 0.3m),

        CityResource("London", "wood", 0.7m),
        CityResource("London", "grain", 0.6m),
        CityResource("London", "cotton", 0.4m),
        CityResource("London", "gold", 0.2m),
        CityResource("London", "coal", 0.5m),

        CityResource("Beijing", "wood", 0.7m),
        CityResource("Beijing", "grain", 0.6m),
        CityResource("Beijing", "coal", 0.8m),
        CityResource("Beijing", "iron-ore", 0.7m),
        CityResource("Beijing", "silicon", 0.6m),

        CityResource("Delhi", "wood", 0.7m),
        CityResource("Delhi", "grain", 0.6m),
        CityResource("Delhi", "chemical-minerals", 0.5m),
        CityResource("Delhi", "cotton", 0.7m),
        CityResource("Delhi", "iron-ore", 0.4m),
    ];

    private static CityResourceSeed CityResource(string cityName, string resourceSlug, decimal abundance) =>
        new(cityName, resourceSlug, abundance);


    private sealed record ResourceSeed(
        string Name,
        string Slug,
        string Category,
        decimal BasePrice,
        decimal WeightPerUnit,
        string UnitName,
        string UnitSymbol,
        string Description,
        string Icon,
        string BackgroundColor,
        string AccentColor);

    private sealed record CityResourceSeed(
        string CityName,
        string ResourceSlug,
        decimal Abundance);

    private sealed record ProductSeed(
        string Name,
        string Slug,
        string Industry,
        decimal BasePrice,
        int BaseCraftTicks,
        string Description,
        string UnitName,
        string UnitSymbol,
        decimal OutputQuantity,
        decimal EnergyConsumptionMwh,
        decimal BasicLaborHours,
        decimal PriceElasticity,
        IReadOnlyList<RecipeSeed> Ingredients);

    private sealed record RecipeSeed(string? ResourceSlug, string? ProductSlug, decimal Quantity);
}
