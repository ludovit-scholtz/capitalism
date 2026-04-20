using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace Api.Tests;

/// <summary>
/// Tests for global city seeding (New York, London, Beijing, Delhi) and FX rate infrastructure.
/// Covers: city currency metadata, NBS CSV parsing, fallback rates, and the fxRates GraphQL query.
/// </summary>
public sealed class GlobalCitiesAndFxRatesTests
{
    #region City seeding — currency metadata

    [Fact]
    public async Task AllSevenCities_HaveCorrectCurrencyCodes()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cities = await db.Cities.OrderBy(c => c.Name).ToListAsync();

        Assert.Equal(7, cities.Count);

        var byCurrency = cities.ToDictionary(c => c.Name, c => c.CurrencyCode);

        Assert.Equal("CNY", byCurrency["Beijing"]);
        Assert.Equal("EUR", byCurrency["Bratislava"]);
        Assert.Equal("INR", byCurrency["Delhi"]);
        Assert.Equal("GBP", byCurrency["London"]);
        Assert.Equal("USD", byCurrency["New York"]);
        Assert.Equal("CZK", byCurrency["Prague"]);
        Assert.Equal("EUR", byCurrency["Vienna"]);
    }

    [Fact]
    public async Task AllSevenCities_HaveResourceAbundances()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cities = await db.Cities.Include(c => c.Resources).ToListAsync();

        foreach (var city in cities)
        {
            Assert.True(city.Resources.Count > 0,
                $"City '{city.Name}' must have at least one resource abundance seeded.");
        }
    }

    [Fact]
    public async Task AllSevenCities_HaveBuildingLots()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var lotsByCityName = await db.BuildingLots
            .Include(l => l.City)
            .GroupBy(l => l.City!.Name)
            .Select(g => new { CityName = g.Key, Count = g.Count() })
            .ToListAsync();

        var dict = lotsByCityName.ToDictionary(x => x.CityName, x => x.Count);

        // Each of the 4 new cities must have at least 6 lots (2 industrial, 2 commercial, 1 residential, 1 energy)
        Assert.True(dict.GetValueOrDefault("New York") >= 6, "New York must have ≥6 building lots.");
        Assert.True(dict.GetValueOrDefault("London") >= 6, "London must have ≥6 building lots.");
        Assert.True(dict.GetValueOrDefault("Beijing") >= 6, "Beijing must have ≥6 building lots.");
        Assert.True(dict.GetValueOrDefault("Delhi") >= 6, "Delhi must have ≥6 building lots.");
    }

    [Fact]
    public async Task NewGlobalCities_HaveCorrectCoordinates()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ny = await db.Cities.FirstAsync(c => c.Name == "New York");
        var london = await db.Cities.FirstAsync(c => c.Name == "London");
        var beijing = await db.Cities.FirstAsync(c => c.Name == "Beijing");
        var delhi = await db.Cities.FirstAsync(c => c.Name == "Delhi");

        // New York: roughly 40–41°N, 74–73°W
        Assert.InRange(ny.Latitude, 40.0, 41.5);
        Assert.InRange(ny.Longitude, -75.0, -73.0);

        // London: roughly 51.4–51.6°N, -0.5–0.2°E
        Assert.InRange(london.Latitude, 51.4, 51.7);
        Assert.InRange(london.Longitude, -0.5, 0.2);

        // Beijing: roughly 39.5–40.5°N, 115–117°E
        Assert.InRange(beijing.Latitude, 39.5, 40.5);
        Assert.InRange(beijing.Longitude, 115.0, 118.0);

        // Delhi: roughly 28.4–29.0°N, 76.8–77.5°E
        Assert.InRange(delhi.Latitude, 28.4, 29.0);
        Assert.InRange(delhi.Longitude, 76.8, 77.5);
    }

    #endregion

    #region FX rate seeding

    [Fact]
    public async Task FxRates_AreSeeded_WithAllGameCurrencies()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rates = await db.FxRates.ToListAsync();

        Assert.NotEmpty(rates);

        var quoteCurrencies = rates.Select(r => r.QuoteCurrencyCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // All city currencies must have an FX rate
        Assert.Contains("USD", quoteCurrencies);
        Assert.Contains("GBP", quoteCurrencies);
        Assert.Contains("CNY", quoteCurrencies);
        Assert.Contains("INR", quoteCurrencies);
        Assert.Contains("CZK", quoteCurrencies);
    }

    [Fact]
    public async Task FxRates_AllRates_ArePositive_AndEurBased()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rates = await db.FxRates.ToListAsync();

        Assert.All(rates, rate =>
        {
            Assert.Equal("EUR", rate.BaseCurrencyCode);
            Assert.True(rate.Rate > 0m, $"Rate for {rate.QuoteCurrencyCode} must be positive, got {rate.Rate}");
        });
    }

    [Fact]
    public async Task FxRates_GraphQL_ReturnsAllCurrencies()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var doc = await ExecuteGraphQlAsync(client,
            "{ fxRates { baseCurrencyCode quoteCurrencyCode rate rateDate source quoteCurrencySymbol } }");

        Assert.False(doc.TryGetProperty("errors", out _), "fxRates query must not return errors");
        var rates = doc.GetProperty("data").GetProperty("fxRates").EnumerateArray().ToList();

        Assert.NotEmpty(rates);

        var quoteCurrencies = rates.Select(r => r.GetProperty("quoteCurrencyCode").GetString()!).ToHashSet();
        Assert.Contains("USD", quoteCurrencies);
        Assert.Contains("GBP", quoteCurrencies);
        Assert.Contains("CNY", quoteCurrencies);
        Assert.Contains("INR", quoteCurrencies);
        Assert.Contains("CZK", quoteCurrencies);

        // All rates must be positive
        Assert.All(rates, r => Assert.True(r.GetProperty("rate").GetDecimal() > 0m));

        // Source must be "NBS" or "FALLBACK"
        Assert.All(rates, r => Assert.Contains(r.GetProperty("source").GetString()!, new[] { "NBS", "FALLBACK" }));

        // Currency symbols must be non-empty
        Assert.All(rates, r => Assert.NotEmpty(r.GetProperty("quoteCurrencySymbol").GetString()!));
    }

    [Fact]
    public async Task FxRates_GraphQL_IsPublicQuery_WorksWithoutAuthentication()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();
        // No Authorization header — the fxRates query must be publicly accessible
        var doc = await ExecuteGraphQlAsync(client, "{ fxRates { quoteCurrencyCode rate } }", token: null);

        Assert.False(doc.TryGetProperty("errors", out _),
            "fxRates must be publicly accessible without authentication");
        var rates = doc.GetProperty("data").GetProperty("fxRates").EnumerateArray().ToList();
        Assert.NotEmpty(rates);
    }

    #endregion

    #region NBS CSV parsing — unit tests via isolated service

    [Fact]
    public async Task NbsService_FallbackRates_ContainAllGameCurrencies()
    {
        var service = TestHelpers.CreateFallbackNbsService();
        var rates = await service.FetchLatestRatesAsync();

        var quoteCurrencies = rates.Select(r => r.QuoteCurrencyCode).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("USD", quoteCurrencies);
        Assert.Contains("GBP", quoteCurrencies);
        Assert.Contains("CNY", quoteCurrencies);
        Assert.Contains("INR", quoteCurrencies);
        Assert.Contains("CZK", quoteCurrencies);
    }

    [Fact]
    public async Task NbsService_FallbackRates_ArePositive_AndEurBased()
    {
        var service = TestHelpers.CreateFallbackNbsService();
        var rates = await service.FetchLatestRatesAsync();

        Assert.All(rates, rate =>
        {
            Assert.Equal("EUR", rate.BaseCurrencyCode);
            Assert.True(rate.Rate > 0m, $"Fallback rate for {rate.QuoteCurrencyCode} must be positive");
            Assert.Equal("FALLBACK", rate.Source);
        });
    }

    [Fact]
    public async Task NbsService_ParsesValidCsv_ReturnsCorrectRates()
    {
        // Real NBS CSV sample — semicolon separated, date;name;amount;code;rate
        const string sampleCsv =
            """
            17.04.2026;Czech koruna;1;CZK;0.03970
            17.04.2026;US dollar;1;USD;1.13500
            17.04.2026;Pound sterling;1;GBP;0.85900
            17.04.2026;Chinese yuan renminbi;1;CNY;0.12680
            17.04.2026;Indian rupee;100;INR;0.01120
            """;

        var service = TestHelpers.CreateCsvParsingService(sampleCsv);
        var rates = await service.FetchLatestRatesAsync();

        var byCode = rates.ToDictionary(r => r.QuoteCurrencyCode, StringComparer.OrdinalIgnoreCase);

        Assert.True(byCode.ContainsKey("CZK"), "CZK must be parsed");
        Assert.True(byCode.ContainsKey("USD"), "USD must be parsed");
        Assert.True(byCode.ContainsKey("GBP"), "GBP must be parsed");
        Assert.True(byCode.ContainsKey("CNY"), "CNY must be parsed");
        Assert.True(byCode.ContainsKey("INR"), "INR must be parsed");

        // CZK: NBS says 1 CZK = 0.03970 EUR → stored rate (EUR→CZK) = 1/0.03970 ≈ 25.19
        Assert.True(byCode["CZK"].Rate > 20m && byCode["CZK"].Rate < 30m,
            $"CZK rate should be ~25, got {byCode["CZK"].Rate}");

        // USD: NBS says 1 USD = 1.13500 EUR → stored rate (EUR→USD) = 1/1.13500 ≈ 0.881
        Assert.True(byCode["USD"].Rate > 0.8m && byCode["USD"].Rate < 1.0m,
            $"USD rate should be ~0.88, got {byCode["USD"].Rate}");

        // INR has amount=100: NBS says 100 INR = 0.01120 EUR → stored rate (EUR→INR) = 100/0.01120 ≈ 8928
        Assert.True(byCode["INR"].Rate > 8000m && byCode["INR"].Rate < 10000m,
            $"INR rate should be ~8928, got {byCode["INR"].Rate}");

        Assert.All(rates, r => Assert.Equal("NBS", r.Source));
    }

    [Fact]
    public async Task NbsService_MalformedLines_AreSkipped_ValidLinesStillParsed()
    {
        // Mix of malformed and valid lines
        const string csvWithGarbage =
            """
            HEADER_LINE_SHOULD_BE_SKIPPED
            17.04.2026;Czech koruna;1;CZK;0.03970
            ;;MISSING_FIELDS;;
            17.04.2026;US dollar;NOTANUMBER;USD;1.13500
            17.04.2026;Pound sterling;1;GBP;0.85900
            17.04.2026;bad-rate;1;EUR;NOTANUMBER
            """;

        var service = TestHelpers.CreateCsvParsingService(csvWithGarbage);
        var rates = await service.FetchLatestRatesAsync();

        var byCode = rates.ToDictionary(r => r.QuoteCurrencyCode, StringComparer.OrdinalIgnoreCase);

        // Only CZK and GBP are fully valid; USD has non-numeric amount; EUR is 3-char but rate invalid
        Assert.True(byCode.ContainsKey("CZK"), "Valid CZK line must be parsed");
        Assert.True(byCode.ContainsKey("GBP"), "Valid GBP line must be parsed");
        Assert.False(byCode.ContainsKey("USD"), "USD line with non-numeric amount must be skipped");
        // EUR is 3 chars but it is the base; it is not excluded by code length — rate parse fails so it is skipped too
    }

    [Fact]
    public async Task NbsService_EmptyCsv_FallsBackToHardcodedRates()
    {
        // Empty content → parse returns 0 rates → service falls back to hardcoded
        var service = TestHelpers.CreateCsvParsingService(string.Empty);
        var rates = await service.FetchLatestRatesAsync();

        Assert.NotEmpty(rates);
        Assert.All(rates, r => Assert.Equal("FALLBACK", r.Source));
    }

    [Fact]
    public async Task NbsService_ZeroRateLines_AreSkipped()
    {
        const string csvWithZero =
            """
            17.04.2026;Czech koruna;1;CZK;0.00000
            17.04.2026;US dollar;1;USD;1.13500
            """;

        var service = TestHelpers.CreateCsvParsingService(csvWithZero);
        var rates = await service.FetchLatestRatesAsync();

        var byCode = rates.ToDictionary(r => r.QuoteCurrencyCode, StringComparer.OrdinalIgnoreCase);

        // CZK has rate=0 → would cause division by zero → must be skipped
        Assert.False(byCode.ContainsKey("CZK"), "Zero-rate line must be skipped");
        Assert.True(byCode.ContainsKey("USD"), "Valid USD line must be parsed");
    }

    #endregion

    #region Multi-city onboarding — new global cities as starter cities

    [Fact]
    public async Task Onboarding_NewYork_CanStartOnboardingCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "ny-onboard@test.com", "NyPlayer");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var newYork = await db.Cities.FirstAsync(c => c.Name == "New York");
        // Create an affordable test lot (lot prices in real cities are in local currency and can be much higher than starter cash)
        var testLot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = newYork.Id,
            Name = "New York Test Factory Lot", Description = "Test lot for New York onboarding.",
            District = "Industrial Zone", Latitude = newYork.Latitude + 0.01, Longitude = newYork.Longitude + 0.01,
            Price = 75_000m, SuitableTypes = "FACTORY,MINE", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(testLot);
        await db.SaveChangesAsync();

        var startResult = await ExecuteGraphQlAsync(client,
            """
            mutation StartOnboarding($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id name }
                factory { id cityId }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId = newYork.Id.ToString(), companyName = "NY Empire Co", factoryLotId = testLot.Id.ToString() } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _),
            "startOnboardingCompany must succeed for New York");

        var building = startResult.GetProperty("data").GetProperty("startOnboardingCompany")
            .GetProperty("factory");
        Assert.Equal(newYork.Id.ToString(), building.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task Onboarding_London_CanStartOnboardingCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "ld-onboard@test.com", "LdPlayer");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var london = await db.Cities.FirstAsync(c => c.Name == "London");
        // Create an affordable test lot (lot prices in real cities are in local currency and can be much higher than starter cash)
        var testLot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = london.Id,
            Name = "London Test Factory Lot", Description = "Test lot for London onboarding.",
            District = "Industrial Zone", Latitude = london.Latitude + 0.01, Longitude = london.Longitude + 0.01,
            Price = 75_000m, SuitableTypes = "FACTORY,MINE", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(testLot);
        await db.SaveChangesAsync();

        var startResult = await ExecuteGraphQlAsync(client,
            """
            mutation StartOnboarding($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id name }
                factory { id cityId }
              }
            }
            """,
            new { input = new { industry = "FOOD_PROCESSING", cityId = london.Id.ToString(), companyName = "London Bread Co", factoryLotId = testLot.Id.ToString() } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _),
            "startOnboardingCompany must succeed for London");

        var building = startResult.GetProperty("data").GetProperty("startOnboardingCompany")
            .GetProperty("factory");
        Assert.Equal(london.Id.ToString(), building.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task Onboarding_Beijing_CanStartOnboardingCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "bj-onboard@test.com", "BjPlayer");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var beijing = await db.Cities.FirstAsync(c => c.Name == "Beijing");
        // Create an affordable test lot (lot prices in real cities are in local currency and can be much higher than starter cash)
        var testLot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = beijing.Id,
            Name = "Beijing Test Factory Lot", Description = "Test lot for Beijing onboarding.",
            District = "Industrial Zone", Latitude = beijing.Latitude + 0.01, Longitude = beijing.Longitude + 0.01,
            Price = 75_000m, SuitableTypes = "FACTORY,MINE", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(testLot);
        await db.SaveChangesAsync();

        var startResult = await ExecuteGraphQlAsync(client,
            """
            mutation StartOnboarding($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id name }
                factory { id cityId }
              }
            }
            """,
            new { input = new { industry = "HEALTHCARE", cityId = beijing.Id.ToString(), companyName = "Beijing Pharma Co", factoryLotId = testLot.Id.ToString() } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _),
            "startOnboardingCompany must succeed for Beijing");

        var building = startResult.GetProperty("data").GetProperty("startOnboardingCompany")
            .GetProperty("factory");
        Assert.Equal(beijing.Id.ToString(), building.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task Onboarding_Delhi_CanStartOnboardingCompany()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAndGetTokenAsync(client, "dl-onboard@test.com", "DlPlayer");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var delhi = await db.Cities.FirstAsync(c => c.Name == "Delhi");
        // Create an affordable test lot (lot prices in real cities are in local currency and can be much higher than starter cash)
        var testLot = new BuildingLot
        {
            Id = Guid.NewGuid(), CityId = delhi.Id,
            Name = "Delhi Test Factory Lot", Description = "Test lot for Delhi onboarding.",
            District = "Industrial Zone", Latitude = delhi.Latitude + 0.01, Longitude = delhi.Longitude + 0.01,
            Price = 75_000m, SuitableTypes = "FACTORY,MINE", ConcurrencyToken = Guid.NewGuid()
        };
        db.BuildingLots.Add(testLot);
        await db.SaveChangesAsync();

        var startResult = await ExecuteGraphQlAsync(client,
            """
            mutation StartOnboarding($input: StartOnboardingCompanyInput!) {
              startOnboardingCompany(input: $input) {
                company { id name }
                factory { id cityId }
              }
            }
            """,
            new { input = new { industry = "FURNITURE", cityId = delhi.Id.ToString(), companyName = "Delhi Furniture Co", factoryLotId = testLot.Id.ToString() } },
            token);

        Assert.False(startResult.TryGetProperty("errors", out _),
            "startOnboardingCompany must succeed for Delhi");

        var building = startResult.GetProperty("data").GetProperty("startOnboardingCompany")
            .GetProperty("factory");
        Assert.Equal(delhi.Id.ToString(), building.GetProperty("cityId").GetString());
    }

    [Fact]
    public async Task Cities_GraphQL_ReturnsAllSevenCitiesWithCurrencyCode()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var doc = await ExecuteGraphQlAsync(client, "{ cities { id name countryCode currencyCode population } }");
        Assert.False(doc.TryGetProperty("errors", out _));

        var cities = doc.GetProperty("data").GetProperty("cities").EnumerateArray().ToList();
        Assert.Equal(7, cities.Count);

        var byCurrency = cities.ToDictionary(
            c => c.GetProperty("name").GetString()!,
            c => c.GetProperty("currencyCode").GetString()!);

        Assert.Equal("EUR", byCurrency["Bratislava"]);
        Assert.Equal("CZK", byCurrency["Prague"]);
        Assert.Equal("EUR", byCurrency["Vienna"]);
        Assert.Equal("USD", byCurrency["New York"]);
        Assert.Equal("GBP", byCurrency["London"]);
        Assert.Equal("CNY", byCurrency["Beijing"]);
        Assert.Equal("INR", byCurrency["Delhi"]);
    }

    #endregion

    #region Helpers

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email, string displayName)
    {
        var doc = await ExecuteGraphQlAsync(client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token player { id } }
            }
            """,
            new { input = new { email, password = "TestPass123!", displayName } });
        return doc.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            System.Text.Encoding.UTF8, "application/json");
        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    #endregion
}
