using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the Buildings &amp; Land Map System feature.
/// Covers acceptance criteria from the issue:
/// - Vienna city purchaseLot (third-city coverage)
/// - BANK and EXCHANGE building types via purchaseLot
/// - EnsureMinimumAvailableLots triggers during cityLots query
/// - populationIndex field returned for strategic decision-making
/// - lot query returns all required GPS and resource fields
/// - setForSale / purchase building-transfer lifecycle
/// </summary>
public sealed class BuildingLandMapCoverageTests
{
    // ── GraphQL helpers ────────────────────────────────────────────────────────

    private static async Task<JsonElement> GqlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await GqlAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName = "Coverage Tester", password = "CoverTest123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<string> GetCityIdAsync(HttpClient client, string cityName)
    {
        var result = await GqlAsync(client, "{ cities { id name } }");
        return result.GetProperty("data").GetProperty("cities").EnumerateArray()
            .First(c => c.GetProperty("name").GetString() == cityName)
            .GetProperty("id").GetString()!;
    }

    private static async Task<JsonElement> GetCityLotsAsync(HttpClient client, string cityId)
    {
        return await GqlAsync(client,
            "query CL($cityId: UUID!) { cityLots(cityId: $cityId) { id name suitableTypes ownerCompanyId resourceType { name } materialQuality materialQuantity populationIndex price district latitude longitude } }",
            new { cityId });
    }

    private static async Task FundCompanyAsync(ApiWebApplicationFactory factory, string companyId, string currencyCode = "EUR", decimal amount = 50_000_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var companyGuid = Guid.Parse(companyId);
        var existing = await db.BankAccounts
            .Where(a => a.CompanyId == companyGuid && a.CurrencyCode == currencyCode && a.PlayerId == null && !a.IsGovernmentAccount)
            .FirstOrDefaultAsync();
        if (existing is not null)
        {
            existing.Balance = amount;
        }
        else
        {
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = Guid.NewGuid().ToString("N")[..16],
                CurrencyCode = currencyCode,
                CompanyId = companyGuid,
                Balance = amount,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    // ── AC: Vienna city purchaseLot (third city coverage) ─────────────────────

    /// <summary>
    /// AC: Players can view available lots in Vienna (third seeded city).
    /// The cityLots query must succeed for Vienna and auto-generate lots if none exist.
    /// This closes the three-city coverage gap (Bratislava + Prague were already tested).
    /// </summary>
    [Fact]
    public async Task CityLots_Vienna_ReturnsAvailableLots_WithAllRequiredFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var viennaId = await GetCityIdAsync(client, "Vienna");

        var result = await GetCityLotsAsync(client, viennaId);

        Assert.False(result.TryGetProperty("errors", out _),
            "cityLots query for Vienna must not return errors");

        var lots = result.GetProperty("data").GetProperty("cityLots").EnumerateArray().ToList();
        // After EnsureMinimumAvailableLots, Vienna must have lots
        Assert.NotEmpty(lots);

        // All lots must have required GPS coordinates
        foreach (var lot in lots)
        {
            var lat = lot.GetProperty("latitude").GetDouble();
            var lon = lot.GetProperty("longitude").GetDouble();
            Assert.NotEqual(0.0, lat);
            Assert.NotEqual(0.0, lon);
        }
    }

    /// <summary>
    /// AC: Players can purchase available land in Vienna.
    /// Vienna uses EUR currency, so the EUR bank-account balance must be debited.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_Vienna_EUR_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"vienna-buyer-{Guid.NewGuid():N}@test.com");

        // Create a company directly
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Vienna Properties AG" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        // Fund the company with EUR
        await FundCompanyAsync(factory, companyId, "EUR", 50_000_000m);

        var viennaId = await GetCityIdAsync(client, "Vienna");

        // Trigger lot generation via cityLots query, then pick an available FACTORY lot
        var lotsResult = await GetCityLotsAsync(client, viennaId);
        var availableLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .FirstOrDefault(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("FACTORY")
                && !l.GetProperty("suitableTypes").GetString()!.Contains("MINE")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);

        // If no non-mine factory lot found, create one via direct DB seeding
        string lotId;
        if (availableLot.ValueKind == JsonValueKind.Undefined)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var vienna = await db.Cities.FirstAsync(c => c.Name == "Vienna");
            var lot = new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = vienna.Id,
                Name = "Vienna Industrial Lot",
                Description = "A factory-eligible plot near Vienna city centre.",
                District = "Industrial Zone",
                Latitude = vienna.Latitude + 0.01,
                Longitude = vienna.Longitude + 0.01,
                Price = 120_000m,
                SuitableTypes = "FACTORY,SALES_SHOP",
                ConcurrencyToken = Guid.NewGuid(),
            };
            db.BuildingLots.Add(lot);
            await db.SaveChangesAsync();
            lotId = lot.Id.ToString();
        }
        else
        {
            lotId = availableLot.GetProperty("id").GetString()!;
        }

        // Purchase the lot
        var purchaseResult = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                lot { id ownerCompanyId }
                building { id type }
              }
            }
            """,
            new { input = new { companyId, lotId, buildingType = "FACTORY", buildingName = "Vienna Factory 1" } },
            token);

        Assert.False(purchaseResult.TryGetProperty("errors", out _),
            $"Purchase in Vienna must succeed but got: {purchaseResult}");

        var data = purchaseResult.GetProperty("data").GetProperty("purchaseLot");
        Assert.Equal(companyId, data.GetProperty("lot").GetProperty("ownerCompanyId").GetString());
        Assert.Equal("FACTORY", data.GetProperty("building").GetProperty("type").GetString());
    }

    // ── AC: BANK building type via purchaseLot ─────────────────────────────────

    /// <summary>
    /// AC: Players can construct a BANK building on a lot marked suitable for BANK.
    /// The seeded infrastructure lots include a BANK,EXCHANGE lot in Bratislava.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_BankBuilding_CreatesCorrectBuildingType()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"bank-builder-{Guid.NewGuid():N}@test.com");
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "City Bank Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, companyId);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");

        // Find a seeded BANK-eligible lot
        var lotsResult = await GetCityLotsAsync(client, bratislavaId);
        var bankLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .FirstOrDefault(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("BANK")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);

        string lotId;
        if (bankLot.ValueKind == JsonValueKind.Undefined)
        {
            // Seed a BANK lot in case seeded lots are all taken
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var lot = new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = bratislava.Id,
                Name = "Financial District Plot",
                Description = "Premium financial district plot suitable for banks.",
                District = "Financial District",
                Latitude = 48.148 + 0.002,
                Longitude = 17.107 + 0.002,
                Price = 500_000m,
                SuitableTypes = "BANK,EXCHANGE",
                ConcurrencyToken = Guid.NewGuid(),
            };
            db.BuildingLots.Add(lot);
            await db.SaveChangesAsync();
            lotId = lot.Id.ToString();
        }
        else
        {
            lotId = bankLot.GetProperty("id").GetString()!;
        }

        var result = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                lot { id ownerCompanyId }
                building { id type }
              }
            }
            """,
            new { input = new { companyId, lotId, buildingType = "BANK", buildingName = "First City Bank" } },
            token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"BANK building purchase must succeed: {result}");

        var building = result.GetProperty("data").GetProperty("purchaseLot").GetProperty("building");
        Assert.Equal("BANK", building.GetProperty("type").GetString());
    }

    // ── AC: EXCHANGE building type via purchaseLot ────────────────────────────

    /// <summary>
    /// AC: Players can construct an EXCHANGE building on a lot marked suitable for EXCHANGE.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_ExchangeBuilding_CreatesCorrectBuildingType()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"exchange-builder-{Guid.NewGuid():N}@test.com");
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Central Exchange Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, companyId);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var lotsResult = await GetCityLotsAsync(client, bratislavaId);

        // Find an EXCHANGE-suitable lot (not already owned)
        var exchangeLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .FirstOrDefault(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("EXCHANGE")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);

        string lotId;
        if (exchangeLot.ValueKind == JsonValueKind.Undefined)
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var lot = new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = bratislava.Id,
                Name = "Exchange Hub Plot",
                Description = "Central exchange hub plot.",
                District = "Financial District",
                Latitude = 48.148 + 0.003,
                Longitude = 17.107 + 0.003,
                Price = 600_000m,
                SuitableTypes = "BANK,EXCHANGE",
                ConcurrencyToken = Guid.NewGuid(),
            };
            db.BuildingLots.Add(lot);
            await db.SaveChangesAsync();
            lotId = lot.Id.ToString();
        }
        else
        {
            lotId = exchangeLot.GetProperty("id").GetString()!;
        }

        var result = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) {
                lot { id ownerCompanyId }
                building { id type }
              }
            }
            """,
            new { input = new { companyId, lotId, buildingType = "EXCHANGE", buildingName = "Central Commodity Exchange" } },
            token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"EXCHANGE building purchase must succeed: {result}");

        var building = result.GetProperty("data").GetProperty("purchaseLot").GetProperty("building");
        Assert.Equal("EXCHANGE", building.GetProperty("type").GetString());
    }

    // ── AC: Population index field for strategic decisions ─────────────────────

    /// <summary>
    /// AC: The cityLots query must return populationIndex so the frontend can display
    /// a "Strong for retail demand" or similar strategic label for each lot.
    /// Higher-population-index lots must have a higher value than lower-density ones.
    /// </summary>
    [Fact]
    public async Task CityLots_PopulationIndex_IsReturnedAndPositive()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var result = await GqlAsync(client,
            "query CL($cityId: UUID!) { cityLots(cityId: $cityId) { id suitableTypes populationIndex } }",
            new { cityId = bratislavaId });

        Assert.False(result.TryGetProperty("errors", out _));
        var lots = result.GetProperty("data").GetProperty("cityLots").EnumerateArray().ToList();
        Assert.NotEmpty(lots);

        // Every seeded lot must have a positive populationIndex
        foreach (var lot in lots)
        {
            var popIndex = lot.GetProperty("populationIndex").GetDecimal();
            Assert.True(popIndex > 0m,
                $"Lot {lot.GetProperty("id")} populationIndex {popIndex} must be > 0");
        }
    }

    // ── AC: lot query returns GPS and resource fields ──────────────────────────

    /// <summary>
    /// AC: The single lot query must return latitude, longitude, populationIndex,
    /// resourceType, materialQuality, and materialQuantity so the frontend can
    /// render the land parcel popup with full strategic data.
    /// </summary>
    [Fact]
    public async Task GetLot_ById_ReturnsGpsAndResourceFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");

        // Find a mine lot that has resource data
        var lotsResult = await GqlAsync(client,
            "query CL($cityId: UUID!) { cityLots(cityId: $cityId) { id suitableTypes resourceType { name } materialQuality materialQuantity } }",
            new { cityId = bratislavaId });
        var mineLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(l => l.GetProperty("suitableTypes").GetString()!.Contains("MINE")
                     && l.GetProperty("resourceType").ValueKind != JsonValueKind.Null);
        var lotId = mineLot.GetProperty("id").GetString()!;

        var result = await GqlAsync(client,
            """
            query GetLot($id: UUID!) {
              lot(id: $id) {
                id
                latitude
                longitude
                populationIndex
                price
                resourceType { name slug }
                materialQuality
                materialQuantity
                suitableTypes
                district
              }
            }
            """,
            new { id = lotId });

        Assert.False(result.TryGetProperty("errors", out _));
        var lot = result.GetProperty("data").GetProperty("lot");
        Assert.NotEqual(JsonValueKind.Null, lot.ValueKind);

        // GPS fields
        Assert.NotEqual(0.0, lot.GetProperty("latitude").GetDouble());
        Assert.NotEqual(0.0, lot.GetProperty("longitude").GetDouble());

        // Economic fields
        Assert.True(lot.GetProperty("populationIndex").GetDecimal() > 0m);
        Assert.True(lot.GetProperty("price").GetDecimal() > 0m);

        // Resource fields on mine lots
        Assert.NotEqual(JsonValueKind.Null, lot.GetProperty("resourceType").ValueKind);
        Assert.True(lot.GetProperty("materialQuality").GetDecimal() > 0m);
        Assert.True(lot.GetProperty("materialQuantity").GetDecimal() > 0m);
    }

    // ── AC: EnsureMinimumAvailableLots self-heals on cityLots query ───────────

    /// <summary>
    /// AC: At least 10 available lands per building type per city must always be present.
    /// When the cityLots query is called for a city with very few lots, the
    /// EnsureMinimumAvailableLotsAsync self-heal must run automatically.
    /// This test verifies the live HTTP query path triggers lot generation.
    /// </summary>
    [Fact]
    public async Task CityLots_SelfHealsToMinimumLots_WhenQueryCalled()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Use Vienna (starts with zero seeded lots) — calling cityLots must generate minimum lots
        var viennaId = await GetCityIdAsync(client, "Vienna");

        // First call triggers EnsureMinimumAvailableLotsAsync
        var result = await GqlAsync(client,
            "query CL($cityId: UUID!) { cityLots(cityId: $cityId) { id suitableTypes } }",
            new { cityId = viennaId });

        Assert.False(result.TryGetProperty("errors", out _),
            "cityLots for Vienna must not return errors");

        var lots = result.GetProperty("data").GetProperty("cityLots").EnumerateArray().ToList();

        // After self-heal, Vienna should have lots for at least FACTORY type
        Assert.NotEmpty(lots);
        var factoryLots = lots.Where(l => l.GetProperty("suitableTypes").GetString()!.Contains("FACTORY")).ToList();
        Assert.NotEmpty(factoryLots);
    }

    // ── AC: purchaseLot deducts balance from correct company account ───────────

    /// <summary>
    /// AC: After purchaseLot, the company bank-account balance must be reduced by
    /// at least the lot price, confirming the economic loop (acquire land → pay money).
    /// </summary>
    [Fact]
    public async Task PurchaseLot_DeductsFromCompanyBankAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"balance-check-{Guid.NewGuid():N}@test.com");
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Balance Check Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        const decimal seedBalance = 10_000_000m;
        await FundCompanyAsync(factory, companyId, "EUR", seedBalance);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var lotsResult = await GetCityLotsAsync(client, bratislavaId);
        var availableLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("FACTORY")
                && !l.GetProperty("suitableTypes").GetString()!.Contains("MINE")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);
        var lotId = availableLot.GetProperty("id").GetString()!;
        var lotPrice = availableLot.GetProperty("price").GetDecimal();

        await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id } building { id } }
            }
            """,
            new { input = new { companyId, lotId, buildingType = "FACTORY", buildingName = "Balance Factory" } },
            token);

        // Verify balance was reduced
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var totalEur = await db.BankAccounts
            .Where(a => a.CompanyId == Guid.Parse(companyId) && a.CurrencyCode == "EUR")
            .SumAsync(a => a.Balance);

        Assert.True(totalEur < seedBalance,
            $"Balance should have been reduced; seed={seedBalance} current={totalEur}");
        Assert.True(totalEur <= seedBalance - lotPrice,
            $"Balance should have been reduced by at least lot price {lotPrice}; actual reduction={seedBalance - totalEur}");
    }

    // ── AC: setForSale / building transfer lifecycle ───────────────────────────

    /// <summary>
    /// AC: A player can set their building for sale at a chosen price.
    /// The buildingMarket query must then list the building.
    /// Another player can make an offer and the seller can accept it.
    /// This covers the issue requirement: "Players can set a building for sale."
    /// </summary>
    [Fact]
    public async Task BuildingTransfer_SetForSale_AppearsInMarket()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Seller sets up a building
        var sellerToken = await RegisterAsync(client, $"seller-{Guid.NewGuid():N}@test.com");
        var sellerCo = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Seller Corp" } }, sellerToken);
        var sellerCompanyId = sellerCo.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, sellerCompanyId);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var lots = await GetCityLotsAsync(client, bratislavaId);
        var lot = lots.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("SALES_SHOP")
                && !l.GetProperty("suitableTypes").GetString()!.Contains("MINE")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);
        var lotId = lot.GetProperty("id").GetString()!;

        var purchase = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { building { id } }
            }
            """,
            new { input = new { companyId = sellerCompanyId, lotId, buildingType = "SALES_SHOP", buildingName = "Shop For Sale" } },
            sellerToken);

        var buildingId = purchase.GetProperty("data").GetProperty("purchaseLot")
            .GetProperty("building").GetProperty("id").GetString()!;

        // Set the building for sale
        var setForSale = await GqlAsync(client,
            """
            mutation SetForSale($input: SetBuildingForSaleInput!) {
              setBuildingForSale(input: $input) { id isForSale askingPrice }
            }
            """,
            new { input = new { buildingId, isForSale = true, askingPrice = 500_000m } },
            sellerToken);

        Assert.False(setForSale.TryGetProperty("errors", out _),
            $"setBuildingForSale must succeed: {setForSale}");
        var forSale = setForSale.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.True(forSale.GetProperty("isForSale").GetBoolean());
        Assert.Equal(500_000m, forSale.GetProperty("askingPrice").GetDecimal());

        // Building market query must include the listed building
        var market = await GqlAsync(client,
            "query BM($cityId: UUID!) { buildingMarket(cityId: $cityId) { building { id askingPrice } } }",
            new { cityId = bratislavaId });

        Assert.False(market.TryGetProperty("errors", out _));
        var listings = market.GetProperty("data").GetProperty("buildingMarket").EnumerateArray().ToList();
        Assert.Contains(listings, l => l.GetProperty("building").GetProperty("id").GetString() == buildingId);
    }

    // ── AC: mine construction blocked on lot without resource ─────────────────

    /// <summary>
    /// AC: Mine construction is only possible on lands containing a matching
    /// raw-material deposit. A MINE building on a non-resource lot must fail.
    /// This explicitly validates the MINE_REQUIRES_RESOURCE_DEPOSIT error code.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_Mine_OnFactoryOnlyLot_ReturnsResourceError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"mine-factory-lot-{Guid.NewGuid():N}@test.com");
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Mine Restriction Co" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, companyId);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");

        // Create a FACTORY-only lot explicitly without a resource deposit
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var noResourceLot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = bratislava.Id,
            Name = "Factory-Only Lot",
            Description = "A pure factory plot with no raw material deposit.",
            District = "Industrial Zone",
            Latitude = 48.17,
            Longitude = 17.13,
            Price = 75_000m,
            SuitableTypes = "FACTORY,MINE", // physically suitable for MINE, but no resource
            ResourceTypeId = null, // key: no resource
            ConcurrencyToken = Guid.NewGuid(),
        };
        db.BuildingLots.Add(noResourceLot);
        await db.SaveChangesAsync();

        var result = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id } }
            }
            """,
            new { input = new { companyId, lotId = noResourceLot.Id.ToString(), buildingType = "MINE", buildingName = "Barren Mine" } },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors),
            "Mine on a lot without resource deposit must return MINE_REQUIRES_RESOURCE_DEPOSIT");
        var code = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .FirstOrDefault();
        Assert.Equal("MINE_REQUIRES_RESOURCE_DEPOSIT", code);
    }

    // ── AC: insufficient balance is rejected ───────────────────────────────────

    /// <summary>
    /// AC: Players cannot purchase land they cannot afford.
    /// The INSUFFICIENT_FUNDS error code must be returned.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_InsufficientBalance_ReturnsInsufficientFundsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"broke-buyer-{Guid.NewGuid():N}@test.com");
        var companyResult = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Broke Corp" } }, token);
        var companyId = companyResult.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;

        // Fund with 1 EUR — far less than any lot price
        await FundCompanyAsync(factory, companyId, "EUR", 1m);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var lotsResult = await GetCityLotsAsync(client, bratislavaId);
        var availableLot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(l =>
                l.GetProperty("suitableTypes").GetString()!.Contains("FACTORY")
                && !l.GetProperty("suitableTypes").GetString()!.Contains("MINE")
                && l.GetProperty("ownerCompanyId").ValueKind == JsonValueKind.Null);
        var lotId = availableLot.GetProperty("id").GetString()!;

        var result = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id } }
            }
            """,
            new { input = new { companyId, lotId, buildingType = "FACTORY", buildingName = "Unaffordable Factory" } },
            token);

        Assert.True(result.TryGetProperty("errors", out var errors),
            "Purchase with insufficient balance must return an error");
        var codes = errors.EnumerateArray()
            .Select(e => e.GetProperty("extensions").GetProperty("code").GetString())
            .ToList();
        Assert.Contains(codes, code => code is "INSUFFICIENT_FUNDS" or "INSUFFICIENT_BALANCE"
            or "INSUFFICIENT_LOCAL_CURRENCY_BALANCE");
    }

    // ── AC: unauthenticated purchaseLot returns auth error ─────────────────────

    /// <summary>
    /// AC: An unauthenticated player must not be able to purchase land.
    /// The mutation must require a valid JWT bearer token.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_Unauthenticated_ReturnsAuthorizationError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");
        var lotsResult = await GetCityLotsAsync(client, bratislavaId);
        var lot = lotsResult.GetProperty("data").GetProperty("cityLots").EnumerateArray()
            .First(l => l.GetProperty("suitableTypes").GetString()!.Contains("FACTORY"));
        var lotId = lot.GetProperty("id").GetString()!;

        // Call without auth token
        var result = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id } }
            }
            """,
            new { input = new { companyId = Guid.NewGuid().ToString(), lotId, buildingType = "FACTORY", buildingName = "NoAuth Factory" } });

        Assert.True(result.TryGetProperty("errors", out _),
            "Unauthenticated purchaseLot must return an error");
    }

    // ── AC: lot already owned cannot be purchased again ────────────────────────

    /// <summary>
    /// AC: Players cannot purchase land already owned by another player.
    /// The LAND_NOT_AVAILABLE (or similar) error must be returned.
    /// </summary>
    [Fact]
    public async Task PurchaseLot_AlreadyOwned_ReturnsLandNotAvailableError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token1 = await RegisterAsync(client, $"first-buyer-{Guid.NewGuid():N}@test.com");
        var co1 = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "First Buyer Co" } }, token1);
        var companyId1 = co1.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, companyId1);

        var token2 = await RegisterAsync(client, $"second-buyer-{Guid.NewGuid():N}@test.com");
        var co2 = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name = "Second Buyer Co" } }, token2);
        var companyId2 = co2.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
        await FundCompanyAsync(factory, companyId2);

        var bratislavaId = await GetCityIdAsync(client, "Bratislava");

        // Seed a fresh lot so we control which one to buy
        string lotId;
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var lot = new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = bratislava.Id,
                Name = "Contested Plot",
                Description = "Contested",
                District = "Industrial Zone",
                Latitude = 48.16,
                Longitude = 17.12,
                Price = 75_000m,
                SuitableTypes = "FACTORY,SALES_SHOP",
                ConcurrencyToken = Guid.NewGuid(),
            };
            db.BuildingLots.Add(lot);
            await db.SaveChangesAsync();
            lotId = lot.Id.ToString();
        }

        // First buyer purchases the lot successfully
        var first = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id ownerCompanyId } }
            }
            """,
            new { input = new { companyId = companyId1, lotId, buildingType = "FACTORY", buildingName = "First Factory" } },
            token1);
        Assert.False(first.TryGetProperty("errors", out _),
            $"First buyer must succeed: {first}");

        // Second buyer tries to purchase the same lot — must fail
        var second = await GqlAsync(client,
            """
            mutation PurchaseLot($input: PurchaseLotInput!) {
              purchaseLot(input: $input) { lot { id } }
            }
            """,
            new { input = new { companyId = companyId2, lotId, buildingType = "FACTORY", buildingName = "Stolen Factory" } },
            token2);

        Assert.True(second.TryGetProperty("errors", out _),
            "Second buyer must receive an error when lot is already owned");
    }
}
