using System.Net.Http.Json;
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
/// Integration tests for the building secondary market:
/// setBuildingForSale, buildingMarket query, makeOfferOnBuilding,
/// acceptBuildingOffer, rejectBuildingOffer.
/// </summary>
public sealed class BuildingSecondaryMarketTests
{
    private static string NewAccountNumber() => Guid.NewGuid().ToString("N")[..16];

    // ──────────────────────────────────────────────────────────────────────────
    // GraphQL helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
        {
            req.Headers.Authorization = new("Bearer", token);
        }

        var resp = await client.SendAsync(req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email, string displayName = "Test User")
    {
        var result = await ExecAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Shared setup helpers using a shared factory instance
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Seeds a company with a building and a bank account with funds.</summary>
    private static async Task<(Guid BuildingId, Guid CompanyId, Guid BankAccountId)> SeedOwnerWithBuildingAsync(
        ApiWebApplicationFactory factory, string token, decimal initialBalance = 5_000_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Resolve the player from the token by registering and looking them up
        var playerResult = await ExecAsync(factory.CreateClient(), "query { me { id email } }", token: token);
        var playerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Market Test Corp",
            PlayerId = playerId,
        };
        db.Companies.Add(company);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Market Test Factory",
            Latitude = 48.1,
            Longitude = 17.1,
            Level = 1,
        };
        db.Buildings.Add(building);

        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = initialBalance,
            CompanyId = company.Id,
        };
        db.BankAccounts.Add(bankAccount);

        await db.SaveChangesAsync();
        return (building.Id, company.Id, bankAccount.Id);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Tests
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetBuildingForSale_ListsBuilding_WithAskingPrice()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "seller1@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        var result = await ExecAsync(client,
            """
            mutation SetForSale($input: SetBuildingForSaleInput!) {
                setBuildingForSale(input: $input) { id isForSale askingPrice }
            }
            """,
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var bld = result.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.True(bld.GetProperty("isForSale").GetBoolean());
        Assert.Equal(1_000_000m, bld.GetProperty("askingPrice").GetDecimal());
    }

    [Fact]
    public async Task SetBuildingForSale_UnlistsBuilding_WhenIsForSaleFalse()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "seller2@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        // First list it
        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 500_000m } },
            token);

        // Then unlist
        var result = await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id isForSale askingPrice } }",
            new { input = new { buildingId, isForSale = false, askingPrice = (decimal?)null } },
            token);

        var bld = result.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.False(bld.GetProperty("isForSale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, bld.GetProperty("askingPrice").ValueKind);
    }

    [Fact]
    public async Task SetBuildingForSale_ReturnsError_WhenNotOwner()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller3@market.test");
        var otherToken = await RegisterAsync(client, "other3@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);

        var result = await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 500_000m } },
            otherToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BuildingMarket_Query_ReturnsListedBuildings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "seller4@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 750_000m } },
            token);

        var result = await ExecAsync(client,
            """
            query {
                buildingMarket {
                    pendingOfferCount
                    building { id name isForSale askingPrice type city { name } company { name player { email } } }
                }
            }
            """);

        var market = result.GetProperty("data").GetProperty("buildingMarket");
        Assert.True(market.GetArrayLength() >= 1);

        var found = market.EnumerateArray()
            .Select(e => e.GetProperty("building"))
            .FirstOrDefault(b => b.GetProperty("id").GetString() == buildingId.ToString());
        Assert.NotEqual(default, found);
        Assert.True(found.GetProperty("isForSale").GetBoolean());
        Assert.Equal(750_000m, found.GetProperty("askingPrice").GetDecimal());
    }

    [Fact]
    public async Task BuildingMarket_ReturnsEmpty_WhenNoBuildingsForSale()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecAsync(client,
            "query { buildingMarket { building { id } } }");

        var market = result.GetProperty("data").GetProperty("buildingMarket");
        Assert.Equal(0, market.GetArrayLength());
    }

    [Fact]
    public async Task MakeOfferOnBuilding_CreatesOffer_WhenValid()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller5@market.test");
        var buyerToken = await RegisterAsync(client, "buyer5@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 2_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var result = await ExecAsync(client,
            """
            mutation MakeOffer($input: MakeOfferOnBuildingInput!) {
                makeOfferOnBuilding(input: $input) {
                    id offeredPrice status negotiationNote
                    building { id name }
                    buyerCompany { id name }
                }
            }
            """,
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 900_000m, negotiationNote = "Great deal!" } },
            buyerToken);

        var offer = result.GetProperty("data").GetProperty("makeOfferOnBuilding");
        Assert.Equal("PENDING", offer.GetProperty("status").GetString());
        Assert.Equal(900_000m, offer.GetProperty("offeredPrice").GetDecimal());
        Assert.Equal("Great deal!", offer.GetProperty("negotiationNote").GetString());
    }

    [Fact]
    public async Task MakeOfferOnBuilding_ReturnsError_WhenBuyingOwnBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "seller6@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId = companyId, offeredPrice = 900_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Contains("CANNOT_BUY_OWN_BUILDING",
            errors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task MakeOfferOnBuilding_ReturnsError_WhenInsufficientFunds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller7@market.test");
        var buyerToken = await RegisterAsync(client, "buyer7@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 100m); // tiny balance

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 900_000m } },
            buyerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Contains("INSUFFICIENT_FUNDS",
            errors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AcceptBuildingOffer_TransfersBuildingOwnershipAndDebitsCredits()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller8@market.test");
        var buyerToken = await RegisterAsync(client, "buyer8@market.test");
        var (buildingId, sellerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var acceptResult = await ExecAsync(client,
            """
            mutation Accept($input: AcceptBuildingOfferInput!) {
                acceptBuildingOffer(input: $input) {
                    building { id companyId isForSale askingPrice }
                    offer { id status resolvedAtUtc }
                }
            }
            """,
            new { input = new { offerId, offerVersion } },
            sellerToken);

        var accData = acceptResult.GetProperty("data").GetProperty("acceptBuildingOffer");
        var bld = accData.GetProperty("building");
        var offer = accData.GetProperty("offer");

        // Building ownership transferred
        Assert.Equal(buyerCompanyId.ToString(), bld.GetProperty("companyId").GetString());
        Assert.False(bld.GetProperty("isForSale").GetBoolean());
        Assert.Equal(JsonValueKind.Null, bld.GetProperty("askingPrice").ValueKind);

        // Offer marked accepted
        Assert.Equal("ACCEPTED", offer.GetProperty("status").GetString());

        // Verify ledger entries in DB
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var buyerLedger = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.CompanyId == buyerCompanyId && e.Category == LedgerCategory.BuildingAcquisition);
        Assert.NotNull(buyerLedger);
        Assert.Equal(-1_000_000m, buyerLedger.Amount);

        var sellerLedger = await db.LedgerEntries
            .FirstOrDefaultAsync(e => e.CompanyId == sellerCompanyId && e.Category == LedgerCategory.BuildingSale);
        Assert.NotNull(sellerLedger);
        Assert.Equal(1_000_000m, sellerLedger.Amount);
    }

    [Fact]
    public async Task AcceptBuildingOffer_RejectsOtherPendingOffers()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller9@market.test");
        var buyer1Token = await RegisterAsync(client, "buyer9a@market.test");
        var buyer2Token = await RegisterAsync(client, "buyer9b@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyer1CompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyer1Token, 5_000_000m);
        var (_, buyer2CompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyer2Token, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 2_000_000m } },
            sellerToken);

        var offer1Result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId = buyer1CompanyId, offeredPrice = 1_800_000m } },
            buyer1Token);
        var offer1Id = Guid.Parse(offer1Result.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offer1Version = Guid.Parse(offer1Result.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var offer2Result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId = buyer2CompanyId, offeredPrice = 2_000_000m } },
            buyer2Token);
        var offer2Id = Guid.Parse(offer2Result.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        // Accept offer1
        await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId = offer1Id, offerVersion = offer1Version } },
            sellerToken);

        // Verify offer2 was auto-rejected
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rejectedOffer = await db.BuildingSaleOffers.FindAsync(offer2Id);
        Assert.NotNull(rejectedOffer);
        Assert.Equal(BuildingSaleOfferStatus.Rejected, rejectedOffer.Status);
    }

    [Fact]
    public async Task RejectBuildingOffer_MarksOfferRejected()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller10@market.test");
        var buyerToken = await RegisterAsync(client, "buyer10@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 3_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 2_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_800_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var rejectResult = await ExecAsync(client,
            """
            mutation Reject($input: CancelBuildingOfferInput!) {
                cancelBuildingOffer(input: $input) { id status resolvedAtUtc }
            }
            """,
            new { input = new { offerId, offerVersion } },
            sellerToken);

        var rejectedOffer = rejectResult.GetProperty("data").GetProperty("cancelBuildingOffer");
        Assert.Equal("REJECTED", rejectedOffer.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, rejectedOffer.GetProperty("resolvedAtUtc").ValueKind);

        // Building still for sale after rejection
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = await db.Buildings.FindAsync(buildingId);
        Assert.NotNull(building);
        Assert.True(building.IsForSale);
    }

    [Fact]
    public async Task AcceptBuildingOffer_ReturnsError_WhenBuyerHasInsufficientFunds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller11@market.test");
        var buyerToken = await RegisterAsync(client, "buyer11@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        // Drain buyer's bank account
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buyerAccount = await db.BankAccounts.FirstAsync(a => a.CompanyId == buyerCompanyId);
        buyerAccount.Balance = 0m;
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Contains("INSUFFICIENT_FUNDS",
            errors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task MakeOfferOnBuilding_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller12@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 500_000m } },
            sellerToken);

        var result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId = Guid.NewGuid(), offeredPrice = 400_000m } });
        // No token — should fail with auth error
        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task MyBuildingListings_ReturnsOwnListingsWithOffers()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller13@market.test");
        var buyerToken = await RegisterAsync(client, "buyer13@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_500_000m } },
            sellerToken);

        await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_300_000m } },
            buyerToken);

        var result = await ExecAsync(client,
            """
            query {
                myBuildingListings {
                    building { id isForSale askingPrice }
                    offers { id status offeredPrice buyerCompany { name } }
                }
            }
            """,
            token: sellerToken);

        var listings = result.GetProperty("data").GetProperty("myBuildingListings");
        Assert.True(listings.GetArrayLength() >= 1);
        var listing = listings.EnumerateArray()
            .First(l => l.GetProperty("building").GetProperty("id").GetString() == buildingId.ToString());
        Assert.Equal(1, listing.GetProperty("offers").GetArrayLength());
        Assert.Equal("PENDING", listing.GetProperty("offers")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task MakeOfferOnBuilding_ReturnsError_WhenDuplicatePendingOffer()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller14@market.test");
        var buyerToken = await RegisterAsync(client, "buyer14@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        // First offer — must succeed
        var first = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 900_000m } },
            buyerToken);
        Assert.False(first.TryGetProperty("errors", out _), "First offer should succeed");

        // Second offer from the same buyer — must fail with DUPLICATE_OFFER
        var second = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 950_000m } },
            buyerToken);
        var errors = second.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Contains("DUPLICATE_OFFER",
            errors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()));
    }

    [Fact]
    public async Task AcceptBuildingOffer_TransfersUnitsWithBuilding()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller15@market.test");
        var buyerToken = await RegisterAsync(client, "buyer15@market.test");

        // Seed seller with a building that has a unit
        var playerResult = await ExecAsync(client, "query { me { id } }", token: sellerToken);
        var sellerPlayerId = Guid.Parse(playerResult.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var sellerCompany = new Company { Id = Guid.NewGuid(), Name = "Seller15 Corp", PlayerId = sellerPlayerId };
        db.Companies.Add(sellerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Transfer Unit Factory",
            Latitude = 48.1,
            Longitude = 17.1,
            Level = 1,
        };
        db.Buildings.Add(building);

        // Add a unit to the building
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Storage,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(unit);

        var sellerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = city.CurrencyCode,
            Balance = 1_000m,
            CompanyId = sellerCompany.Id,
        };
        db.BankAccounts.Add(sellerAccount);
        await db.SaveChangesAsync();

        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation SetForSale($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId = building.Id, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = building.Id, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id companyId } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        // Verify the unit is now owned by the buyer's building
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transferredBuilding = await verifyDb.Buildings
            .Include(b => b.Units)
            .FirstAsync(b => b.Id == building.Id);
        Assert.Equal(buyerCompanyId, transferredBuilding.CompanyId);
        Assert.Single(transferredBuilding.Units);
        Assert.Equal(unit.Id, transferredBuilding.Units.First().Id);
    }

    [Fact]
    public async Task BuildingMarket_FiltersByCity_ReturnsOnlyMatchingListings()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var seller1Token = await RegisterAsync(client, "seller16a@market.test");
        var seller2Token = await RegisterAsync(client, "seller16b@market.test");

        // Get city IDs
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");

        // Seed a seller building in Bratislava
        var player1Result = await ExecAsync(client, "query { me { id } }", token: seller1Token);
        var p1Id = Guid.Parse(player1Result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
        var co1 = new Company { Id = Guid.NewGuid(), Name = "BA Corp", PlayerId = p1Id };
        db.Companies.Add(co1);
        var baBldg = new Building
        {
            Id = Guid.NewGuid(), CompanyId = co1.Id, CityId = bratislava.Id,
            Type = BuildingType.Factory, Name = "BA Factory", Latitude = 48.1, Longitude = 17.1, Level = 1,
        };
        db.Buildings.Add(baBldg);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = bratislava.CurrencyCode, Balance = 500m, CompanyId = co1.Id,
        });

        // Seed a seller building in Prague
        var player2Result = await ExecAsync(client, "query { me { id } }", token: seller2Token);
        var p2Id = Guid.Parse(player2Result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
        var co2 = new Company { Id = Guid.NewGuid(), Name = "PR Corp", PlayerId = p2Id };
        db.Companies.Add(co2);
        var prBldg = new Building
        {
            Id = Guid.NewGuid(), CompanyId = co2.Id, CityId = prague.Id,
            Type = BuildingType.Factory, Name = "PR Factory", Latitude = 50.1, Longitude = 14.4, Level = 1,
        };
        db.Buildings.Add(prBldg);
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = NewAccountNumber(),
            CurrencyCode = prague.CurrencyCode, Balance = 500m, CompanyId = co2.Id,
        });
        await db.SaveChangesAsync();

        // List both for sale
        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId = baBldg.Id, isForSale = true, askingPrice = 200_000m } }, seller1Token);
        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId = prBldg.Id, isForSale = true, askingPrice = 300_000m } }, seller2Token);

        // Query filtered to Bratislava only
        var result = await ExecAsync(client,
            "query GetBM($cityId: UUID) { buildingMarket(cityId: $cityId) { building { id name city { name } } } }",
            new { cityId = bratislava.Id });
        var listings = result.GetProperty("data").GetProperty("buildingMarket");
        // All returned listings must be in Bratislava
        foreach (var listing in listings.EnumerateArray())
        {
            Assert.Equal("Bratislava", listing.GetProperty("building").GetProperty("city").GetProperty("name").GetString());
        }
        Assert.True(listings.GetArrayLength() >= 1);
        Assert.DoesNotContain(listings.EnumerateArray(), l =>
            l.GetProperty("building").GetProperty("name").GetString() == "PR Factory");
    }

    [Fact]
    public async Task SetBuildingForSale_SetsListedAtUtc_WhenListing()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "listed-at1@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        var before = DateTime.UtcNow.AddSeconds(-2);
        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = await db.Buildings.FindAsync(buildingId);
        Assert.NotNull(building!.ListedAtUtc);
        Assert.True(building.ListedAtUtc.Value > before);
    }

    [Fact]
    public async Task SetBuildingForSale_ClearsListedAtUtc_WhenUnlisting()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "listed-at2@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        // First list it
        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 500_000m } },
            token);

        // Then unlist
        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = false, askingPrice = (decimal?)null } },
            token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = await db.Buildings.FindAsync(buildingId);
        Assert.Null(building!.ListedAtUtc);
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsListing_WhenBuildingIsActiveCollateral()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "collateral1@market.test");
        var (buildingId, companyId, bankAccountId) = await SeedOwnerWithBuildingAsync(factory, token);

        // Seed an active loan with this building as collateral
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),      // not enforced by InMemory
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),   // not enforced by InMemory
            LenderCompanyId = Guid.NewGuid(),  // not enforced by InMemory
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 500_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 720L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Active,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BUILDING_LOCKED_AS_COLLATERAL", code);

        var auditLogs = await db.LoanCollateralSecurityAuditLogs
            .Where(log => log.BuildingId == buildingId && log.RejectionReason == "BUILDING_LOCKED_AS_COLLATERAL")
            .ToListAsync();
        Assert.NotEmpty(auditLogs);
    }

    [Fact]
    public async Task SetBuildingForSale_AllowsListing_WhenPriorLoanIsRepaid()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "collateral2@market.test");
        var (buildingId, companyId, bankAccountId) = await SeedOwnerWithBuildingAsync(factory, token);

        // Seed a REPAID loan (should not block listing)
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),      // not enforced by InMemory
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),   // not enforced by InMemory
            LenderCompanyId = Guid.NewGuid(),  // not enforced by InMemory
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 0m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1440L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Repaid,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-30),
            ClosedAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id isForSale } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var bld = result.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.True(bld.GetProperty("isForSale").GetBoolean());
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsListing_WhenAskingPriceIsZero()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "price-zero@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 0m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_ASKING_PRICE", code);
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsListing_WhenAskingPriceIsNegative()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "price-neg@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, token);

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = -1000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("INVALID_ASKING_PRICE", code);
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsListing_WhenAskingPriceIsBelowSeventyPercentOfMarketValue()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "price-minimum-below@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);

            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = building.CityId,
                Name = "Sale valuation lot",
                Description = "Valuation test lot",
                District = "Test",
                Latitude = 48.15,
                Longitude = 17.11,
                BasePrice = 50_000m,
                Price = 50_000m,
                SuitableTypes = "FACTORY",
                OwnerCompanyId = companyId,
                BuildingId = buildingId,
            });
            db.BuildingUnits.AddRange(
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Purchase,
                    GridX = 0,
                    GridY = 0,
                    Level = 1,
                },
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Storage,
                    GridX = 1,
                    GridY = 0,
                    Level = 2,
                });
            await db.SaveChangesAsync();
        }

        // Market value = land 50k + structure 200k + units (1+2)*20k = 310k
        // Minimum allowed sale price = 217k
        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 216_999m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("ASKING_PRICE_BELOW_MINIMUM", code);
    }

    [Fact]
    public async Task SetBuildingForSale_AllowsListing_WhenAskingPriceIsExactlySeventyPercentOfMarketValue()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "price-minimum-at@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);

            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = building.CityId,
                Name = "Sale valuation lot 2",
                Description = "Valuation test lot 2",
                District = "Test",
                Latitude = 48.15,
                Longitude = 17.11,
                BasePrice = 50_000m,
                Price = 50_000m,
                SuitableTypes = "FACTORY",
                OwnerCompanyId = companyId,
                BuildingId = buildingId,
            });
            db.BuildingUnits.AddRange(
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Purchase,
                    GridX = 0,
                    GridY = 0,
                    Level = 1,
                },
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Storage,
                    GridX = 1,
                    GridY = 0,
                    Level = 2,
                });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id isForSale askingPrice } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 217_000m } },
            token);

        var listed = result.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.True(listed.GetProperty("isForSale").GetBoolean());
        Assert.Equal(217_000m, listed.GetProperty("askingPrice").GetDecimal());
    }

    [Fact]
    public async Task BuildingMarketValuation_ReturnsLandStructureAndUnitBreakdown()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "market-valuation-breakdown@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);

            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = building.CityId,
                Name = "Breakdown lot",
                Description = "Breakdown",
                District = "Test",
                Latitude = 48.15,
                Longitude = 17.11,
                BasePrice = 50_000m,
                Price = 50_000m,
                SuitableTypes = "FACTORY",
                OwnerCompanyId = companyId,
                BuildingId = buildingId,
            });
            db.BuildingUnits.AddRange(
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Purchase,
                    GridX = 0,
                    GridY = 0,
                    Level = 1,
                },
                new BuildingUnit
                {
                    Id = Guid.NewGuid(),
                    BuildingId = buildingId,
                    UnitType = UnitType.Storage,
                    GridX = 1,
                    GridY = 0,
                    Level = 2,
                });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            query {
              myCompanies {
                buildings {
                  id
                  marketValuation {
                    landValue
                    structureValue
                    unitsValue
                    totalValue
                    minimumSalePrice
                    currencyCode
                  }
                }
              }
            }
            """,
            token: token);

        var buildings = result.GetProperty("data").GetProperty("myCompanies")[0].GetProperty("buildings");
        var valuation = buildings.EnumerateArray()
            .First(candidate => candidate.GetProperty("id").GetString() == buildingId.ToString())
            .GetProperty("marketValuation");

        Assert.Equal(50_000m, valuation.GetProperty("landValue").GetDecimal());
        Assert.Equal(200_000m, valuation.GetProperty("structureValue").GetDecimal());
        Assert.Equal(60_000m, valuation.GetProperty("unitsValue").GetDecimal());
        Assert.Equal(310_000m, valuation.GetProperty("totalValue").GetDecimal());
        Assert.Equal(217_000m, valuation.GetProperty("minimumSalePrice").GetDecimal());
        Assert.Equal("EUR", valuation.GetProperty("currencyCode").GetString());
    }

    [Fact]
    public async Task SetBuildingForSale_AllowsListing_WhenLoanIsDefaulted()
    {
        // Defaulted loans must NOT block listing — the player may need to sell the
        // building to repay a defaulted debt. Only Active and Overdue loans block.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "defaulted-collateral@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),
            LenderCompanyId = Guid.NewGuid(),
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 500_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1440L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Defaulted,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-60),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id isForSale } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var bld = result.GetProperty("data").GetProperty("setBuildingForSale");
        Assert.True(bld.GetProperty("isForSale").GetBoolean());
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsListing_WhenBuildingIsOverdueCollateral()
    {
        // Overdue loans (past due but not yet defaulted) must block listing,
        // same as Active loans.
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "overdue-collateral@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),
            LenderCompanyId = Guid.NewGuid(),
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 500_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1440L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Overdue,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BUILDING_LOCKED_AS_COLLATERAL", code);

        var auditLogs = await db.LoanCollateralSecurityAuditLogs
            .Where(log => log.BuildingId == buildingId && log.RejectionReason == "BUILDING_LOCKED_AS_COLLATERAL")
            .ToListAsync();
        Assert.NotEmpty(auditLogs);
    }

    [Fact]
    public async Task SetBuildingForSale_RejectsCancel_WhenBuildingIsForeclosureCollateral()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "cancel-foreclosure@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),
            LenderCompanyId = Guid.NewGuid(),
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 450_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1440L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Overdue,
            MissedPayments = 1,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId, isForSale = false, askingPrice = (decimal?)null } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BUILDING_LOCKED_AS_COLLATERAL", code);
    }

    [Fact]
    public async Task DestroyBuilding_DestroysBuilding_ReleasesLot_AndCreditsRefund()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "destroy-flow@market.test");
        var (buildingId, companyId, accountId) = await SeedOwnerWithBuildingAsync(factory, token, initialBalance: 0m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);
            db.BuildingUnits.Add(new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                GridX = 0,
                GridY = 0,
                UnitType = UnitType.Storage,
                Level = 1,
            });

            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                CityId = building.CityId,
                Name = "Destroy Test Lot",
                Description = "Lot used in destruction integration test",
                District = "Industrial Zone",
                Latitude = building.Latitude,
                Longitude = building.Longitude,
                SuitableTypes = "FACTORY",
                BasePrice = 80_000m,
                Price = 80_000m,
                OwnerCompanyId = companyId,
                BuildingId = building.Id,
            });
            await db.SaveChangesAsync();
        }

        var result = await ExecAsync(client,
            """
            mutation D($input: DestroyBuildingInput!) {
              destroyBuilding(input: $input) {
                buildingId
                refundAmount
                currencyCode
              }
            }
            """,
            new { input = new { buildingId } },
            token);

        var payload = result.GetProperty("data").GetProperty("destroyBuilding");
        Assert.Equal(buildingId.ToString(), payload.GetProperty("buildingId").GetString());
        Assert.Equal("EUR", payload.GetProperty("currencyCode").GetString());
        Assert.Equal(240_000m, payload.GetProperty("refundAmount").GetDecimal());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buildingAfter = await verifyDb.Buildings.FirstAsync(b => b.Id == buildingId);
        Assert.NotNull(buildingAfter.DestroyedAtUtc);
        Assert.False(buildingAfter.IsForSale);

        var lotAfter = await verifyDb.BuildingLots.SingleAsync(l => l.Name == "Destroy Test Lot");
        Assert.Null(lotAfter.BuildingId);
        Assert.Null(lotAfter.OwnerCompanyId);
        Assert.NotNull(lotAfter);

        var accountAfter = await verifyDb.BankAccounts.FirstAsync(a => a.Id == accountId);
        Assert.Equal(240_000m, accountAfter.Balance);
    }

    [Fact]
    public async Task DestroyBuilding_RejectsWhenBuildingHasUnpaidCollateralLoan()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAsync(client, "destroy-blocked@market.test");
        var (buildingId, companyId, _) = await SeedOwnerWithBuildingAsync(factory, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),
            BorrowerCompanyId = companyId,
            BankBuildingId = Guid.NewGuid(),
            LenderCompanyId = Guid.NewGuid(),
            OriginalPrincipal = 500_000m,
            RemainingPrincipal = 350_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 0L,
            DueTick = 1440L,
            NextPaymentTick = 1440L,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = LoanStatus.Active,
            CollateralBuildingId = buildingId,
            CollateralAppraisedValue = 1_000_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-5),
        });
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation D($input: DestroyBuildingInput!) { destroyBuilding(input: $input) { buildingId } }",
            new { input = new { buildingId } },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        var code = errors[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BUILDING_LOCKED_AS_COLLATERAL", code);
    }

    [Fact]
    public async Task AcceptBuildingOffer_DefaultedCollateral_SettlesDebtWithFx_AndReturnsSurplusToSeller()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, "seller-fx-settlement@market.test");
        var buyerToken = await RegisterAsync(client, "buyer-fx-settlement@market.test");

        var (sellerBuildingId, sellerCompanyId, sellerAccountId) = await SeedOwnerWithBuildingAsync(factory, sellerToken, initialBalance: 0m);
        var (_, buyerCompanyId, buyerAccountId) = await SeedOwnerWithBuildingAsync(factory, buyerToken, initialBalance: 20_000_000m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");

            var sellerBuilding = await db.Buildings.FirstAsync(b => b.Id == sellerBuildingId);
            sellerBuilding.CityId = prague.Id;

            var sellerAccount = await db.BankAccounts.FirstAsync(a => a.Id == sellerAccountId);
            sellerAccount.CurrencyCode = prague.CurrencyCode;
            sellerAccount.Balance = 0m;

            var buyerAccount = await db.BankAccounts.FirstAsync(a => a.Id == buyerAccountId);
            buyerAccount.CurrencyCode = prague.CurrencyCode;
            buyerAccount.Balance = 20_000_000m;

            var lenderPlayer = new Player
            {
                Id = Guid.NewGuid(),
                Email = $"lender-fx-{Guid.NewGuid():N}@test.com",
                DisplayName = "FX Lender",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            };
            var lenderCompany = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = lenderPlayer.Id,
                Name = "FX Lender Corp",
            };
            var bankBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = lenderCompany.Id,
                CityId = bratislava.Id,
                Type = BuildingType.Bank,
                Name = "FX Bank",
                Latitude = 48.15,
                Longitude = 17.11,
                Level = 1,
            };
            var lenderAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = NewAccountNumber(),
                CurrencyCode = bratislava.CurrencyCode,
                Balance = 0m,
                CompanyId = lenderCompany.Id,
            };
            var loanOffer = new LoanOffer
            {
                Id = Guid.NewGuid(),
                BankBuildingId = bankBuilding.Id,
                LenderCompanyId = lenderCompany.Id,
                AnnualInterestRatePercent = 8m,
                MaxPrincipalPerLoan = 500_000m,
                TotalCapacity = 1_000_000m,
                UsedCapacity = 100_000m,
                DurationTicks = 1440L,
                IsActive = false,
                CreatedAtTick = 1,
                CreatedAtUtc = DateTime.UtcNow,
            };
            var collateralLoan = new Loan
            {
                Id = Guid.NewGuid(),
                LoanOfferId = loanOffer.Id,
                BorrowerCompanyId = sellerCompanyId,
                BankBuildingId = bankBuilding.Id,
                LenderCompanyId = lenderCompany.Id,
                OriginalPrincipal = 100_000m,
                RemainingPrincipal = 100_000m,
                AnnualInterestRatePercent = 8m,
                DurationTicks = 1440L,
                StartTick = 0,
                DueTick = 1440,
                NextPaymentTick = 1440,
                PaymentAmount = 10_000m,
                TotalPayments = 10,
                Status = LoanStatus.Defaulted,
                MissedPayments = 3,
                DefaultedAtTick = 10,
                CollateralBuildingId = sellerBuildingId,
                CollateralAppraisedValue = 300_000m,
                AcceptedAtUtc = DateTime.UtcNow.AddDays(-10),
            };

            db.Players.Add(lenderPlayer);
            db.Companies.Add(lenderCompany);
            db.Buildings.Add(bankBuilding);
            db.BankAccounts.Add(lenderAccount);
            db.LoanOffers.Add(loanOffer);
            db.Loans.Add(collateralLoan);
            await db.SaveChangesAsync();
        }

        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId = sellerBuildingId, isForSale = true, askingPrice = 10_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId = sellerBuildingId, buyerCompanyId, offeredPrice = 10_000_000m, negotiationNote = "FX settlement offer" } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pragueCurrencyCode = (await verifyDb.Cities.FirstAsync(c => c.Name == "Prague")).CurrencyCode;
        var bratislavaCurrencyCode = (await verifyDb.Cities.FirstAsync(c => c.Name == "Bratislava")).CurrencyCode;
        var fxRates = await FxRateHelper.BuildEurRatesLookupAsync(verifyDb, [pragueCurrencyCode, bratislavaCurrencyCode]);
        var expectedDebtInSaleCurrency = decimal.Round(
            FxRateHelper.ConvertAmount(100_000m, bratislavaCurrencyCode, pragueCurrencyCode, fxRates),
            2,
            MidpointRounding.AwayFromZero);
        var expectedSellerNet = decimal.Round(10_000_000m - expectedDebtInSaleCurrency, 2, MidpointRounding.AwayFromZero);

        var sellerAccountAfter = await verifyDb.BankAccounts.FirstAsync(a => a.Id == sellerAccountId);
        Assert.Equal(expectedSellerNet, sellerAccountAfter.Balance);

        var loanAfter = await verifyDb.Loans.FirstAsync(l => l.BorrowerCompanyId == sellerCompanyId);
        var lenderAccountAfter = await verifyDb.BankAccounts.FirstAsync(a => a.CompanyId == loanAfter.LenderCompanyId && a.CurrencyCode == bratislavaCurrencyCode);
        Assert.Equal(100_000m, lenderAccountAfter.Balance);

        Assert.Equal(0m, loanAfter.RemainingPrincipal);
        Assert.Equal(LoanStatus.Repaid, loanAfter.Status);

        var sellerSaleEntry = await verifyDb.LedgerEntries.FirstAsync(e =>
            e.CompanyId == sellerCompanyId
            && e.Category == LedgerCategory.BuildingSale
            && e.BuildingId == sellerBuildingId);
        Assert.Equal(sellerAccountId, sellerSaleEntry.BankAccountId);
        Assert.Equal(10_000_000m, sellerSaleEntry.Amount);

        var sellerSettlementEntry = await verifyDb.LedgerEntries.FirstAsync(e =>
            e.CompanyId == sellerCompanyId
            && e.Category == LedgerCategory.LoanRepaymentPrincipal
            && e.BuildingId == sellerBuildingId);
        Assert.Equal(sellerAccountId, sellerSettlementEntry.BankAccountId);
        Assert.Equal(-expectedDebtInSaleCurrency, sellerSettlementEntry.Amount);
        Assert.Contains("Forced-sale FX swap", sellerSettlementEntry.Description);
        Assert.Contains(pragueCurrencyCode, sellerSettlementEntry.Description);
        Assert.Contains(bratislavaCurrencyCode, sellerSettlementEntry.Description);

        var lenderSettlementEntry = await verifyDb.LedgerEntries.FirstAsync(e =>
            e.CompanyId == loanAfter.LenderCompanyId
            && e.Category == LedgerCategory.LoanRepaymentPrincipal
            && e.BuildingId == sellerBuildingId);
        Assert.Equal(lenderAccountAfter.Id, lenderSettlementEntry.BankAccountId);
        Assert.Equal(100_000m, lenderSettlementEntry.Amount);
        Assert.Contains("Forced-sale FX swap", lenderSettlementEntry.Description);
    }

    [Fact]
    public async Task MyLoans_DefaultedCollateral_ReturnsLoanCurrencyAndCurrentCollateralListingPrice()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var sellerToken = await RegisterAsync(client, "seller-loan-summary@market.test");
        var (sellerBuildingId, sellerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken, initialBalance: 0m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");

            var sellerBuilding = await db.Buildings.FirstAsync(b => b.Id == sellerBuildingId);
            sellerBuilding.CityId = prague.Id;

            var lenderPlayer = new Player
            {
                Id = Guid.NewGuid(),
                Email = $"loan-summary-lender-{Guid.NewGuid():N}@test.com",
                DisplayName = "Loan Summary Lender",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            };
            var lenderCompany = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = lenderPlayer.Id,
                Name = "Loan Summary Bank",
            };
            var bankBuilding = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = lenderCompany.Id,
                CityId = bratislava.Id,
                Type = BuildingType.Bank,
                Name = "Loan Summary Bank Building",
                Latitude = 48.15,
                Longitude = 17.11,
                Level = 1,
            };
            var lenderAccount = new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = NewAccountNumber(),
                CurrencyCode = bratislava.CurrencyCode,
                Balance = 0m,
                CompanyId = lenderCompany.Id,
            };
            var loanOffer = new LoanOffer
            {
                Id = Guid.NewGuid(),
                BankBuildingId = bankBuilding.Id,
                LenderCompanyId = lenderCompany.Id,
                AnnualInterestRatePercent = 8m,
                MaxPrincipalPerLoan = 500_000m,
                TotalCapacity = 1_000_000m,
                UsedCapacity = 100_000m,
                DurationTicks = 1440L,
                IsActive = false,
                CreatedAtTick = 1,
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.Players.Add(lenderPlayer);
            db.Companies.Add(lenderCompany);
            db.Buildings.Add(bankBuilding);
            db.BankAccounts.Add(lenderAccount);
            db.LoanOffers.Add(loanOffer);
            db.Loans.Add(new Loan
            {
                Id = Guid.NewGuid(),
                LoanOfferId = loanOffer.Id,
                BorrowerCompanyId = sellerCompanyId,
                BankBuildingId = bankBuilding.Id,
                LenderCompanyId = lenderCompany.Id,
                OriginalPrincipal = 100_000m,
                RemainingPrincipal = 100_000m,
                AnnualInterestRatePercent = 8m,
                DurationTicks = 1440L,
                StartTick = 0,
                DueTick = 1440,
                NextPaymentTick = 1440,
                PaymentAmount = 10_000m,
                TotalPayments = 10,
                Status = LoanStatus.Defaulted,
                MissedPayments = 2,
                DefaultedAtTick = 10,
                CollateralBuildingId = sellerBuildingId,
                CollateralAppraisedValue = 300_000m,
                AcceptedAtUtc = DateTime.UtcNow.AddDays(-10),
            });
            await db.SaveChangesAsync();
        }

        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId = sellerBuildingId, isForSale = true, askingPrice = 10_000_000m } },
            sellerToken);

        var result = await ExecAsync(client,
            """
            query {
              myLoans {
                id
                remainingPrincipal
                loanCurrencyCode
                collateralBuildingId
                collateralListingPrice
                collateralListingCurrencyCode
              }
            }
            """,
            token: sellerToken);

        var loan = result.GetProperty("data").GetProperty("myLoans").EnumerateArray().Single();
        Assert.Equal(100_000m, loan.GetProperty("remainingPrincipal").GetDecimal());
        Assert.Equal("EUR", loan.GetProperty("loanCurrencyCode").GetString());
        Assert.Equal(sellerBuildingId.ToString(), loan.GetProperty("collateralBuildingId").GetString());
        Assert.Equal(10_000_000m, loan.GetProperty("collateralListingPrice").GetDecimal());
        Assert.Equal("CZK", loan.GetProperty("collateralListingCurrencyCode").GetString());
    }

    [Fact]
    public async Task AcceptBuildingOffer_ConcurrentRequests_TransfersAtMostOnce_AndLoserReturnsConflictLikeError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller-race-accept@market.test");
        var buyerToken = await RegisterAsync(client, "buyer-race-accept@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var acceptMutation = "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { offer { id status } } }";
        var acceptTask1 = ExecAsync(client, acceptMutation, new { input = new { offerId, offerVersion } }, sellerToken);
        var acceptTask2 = ExecAsync(client, acceptMutation, new { input = new { offerId, offerVersion } }, sellerToken);
        var results = await Task.WhenAll(acceptTask1, acceptTask2);
        static IEnumerable<string?> GetCodes(JsonElement response) =>
            response.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
                ? errors.EnumerateArray()
                    .Select(e =>
                        e.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var code)
                            ? code.GetString()
                            : null)
                : [];

        var successCount = results.Count(r =>
            r.TryGetProperty("data", out var data)
            && data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("acceptBuildingOffer", out var accepted)
            && accepted.ValueKind == JsonValueKind.Object);
        var conflictLikeCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "OFFER_VERSION_CONFLICT",
            "BUILDING_NOT_FOR_SALE",
            "OFFER_NOT_FOUND",
            "BUILDING_NOT_FOUND",
        };
        var conflictCount = results.Count(r => GetCodes(r).Any(code => code is not null && conflictLikeCodes.Contains(code)));

        Assert.InRange(successCount, 0, 1);
        Assert.InRange(conflictCount, 1, 2);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);
        Assert.False(building.IsForSale);
        Assert.Equal(buyerCompanyId, building.CompanyId);
        var acceptedOffer = await db.BuildingSaleOffers
            .AsNoTracking()
            .Where(o => o.Id == offerId)
            .Select(o => o.Status)
            .SingleAsync();
        Assert.Equal(BuildingSaleOfferStatus.Accepted, acceptedOffer);

        var securityLogs = await db.BuildingOfferSecurityAuditLogs
            .Where(log => log.OfferId == offerId && log.Action == "ACCEPT")
            .ToListAsync();
        Assert.NotEmpty(securityLogs);
    }

    [Fact]
    public async Task AcceptLoan_AndAcceptBuildingOffer_InParallel_PreventsDoubleCollateralSpend()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller-race-collateral@market.test");
        var buyerToken = await RegisterAsync(client, "buyer-race-collateral@market.test");
        var lenderToken = await RegisterAsync(client, "lender-race-collateral@market.test");

        var (buildingId, sellerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);
        var (_, lenderCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, lenderToken, 5_000_000m);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var lenderBank = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = lenderCompanyId,
                CityId = city.Id,
                Type = BuildingType.Bank,
                Name = "Race Lender Bank",
                Level = 1,
                BaseCapitalDeposited = true,
                TotalDeposits = 2_000_000m,
                LendingInterestRatePercent = 8m,
            };
            db.Buildings.Add(lenderBank);
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = NewAccountNumber(),
                CurrencyCode = city.CurrencyCode,
                Balance = 2_000_000m,
                CompanyId = lenderCompanyId,
            });
            await db.SaveChangesAsync();

            var lenderBankId = lenderBank.Id;
            await ExecAsync(client,
                "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
                new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
                sellerToken);

            var offerResult = await ExecAsync(client,
                "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
                new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
                buyerToken);
            var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
            var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

            var acceptLoanTask = ExecAsync(client,
                """
                mutation A($input: AcceptLoanInput!) {
                  acceptLoan(input: $input) { id status collateralBuildingId }
                }
                """,
                new
                {
                    input = new
                    {
                        loanOfferId = lenderBankId,
                        borrowerCompanyId = sellerCompanyId,
                        principalAmount = 200_000m,
                        collateralBuildingId = buildingId,
                    }
                },
                sellerToken);

            var acceptOfferTask = ExecAsync(client,
                "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { offer { id status } } }",
                new { input = new { offerId, offerVersion } },
                sellerToken);

            var results = await Task.WhenAll(acceptLoanTask, acceptOfferTask);

            var loanSuccess = results[0].TryGetProperty("data", out var loanData)
                && loanData.ValueKind == JsonValueKind.Object
                && loanData.TryGetProperty("acceptLoan", out var acceptLoanData)
                && acceptLoanData.ValueKind == JsonValueKind.Object;
            var offerSuccess = results[1].TryGetProperty("data", out var offerData)
                && offerData.ValueKind == JsonValueKind.Object
                && offerData.TryGetProperty("acceptBuildingOffer", out var acceptOfferData)
                && acceptOfferData.ValueKind == JsonValueKind.Object;

            Assert.NotEqual(loanSuccess, offerSuccess);

            var offerErrorCodes = results[1].TryGetProperty("errors", out var offerErrors)
                ? offerErrors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()).ToList()
                : [];
            var loanErrorCodes = results[0].TryGetProperty("errors", out var loanErrors)
                ? loanErrors.EnumerateArray().Select(e => e.GetProperty("extensions").GetProperty("code").GetString()).ToList()
                : [];

            Assert.True(
                offerSuccess
                || offerErrorCodes.Contains("BUILDING_LOCKED_AS_COLLATERAL")
                || loanErrorCodes.Contains("COLLATERAL_OWNERSHIP_CONFLICT")
                || loanErrorCodes.Contains("COLLATERAL_NOT_OWNED"));

            var building = await db.Buildings.FirstAsync(b => b.Id == buildingId);
            var activeLoan = await db.Loans
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.CollateralBuildingId == buildingId && (l.Status == LoanStatus.Active || l.Status == LoanStatus.Overdue));

            if (activeLoan is not null)
            {
                Assert.Equal(sellerCompanyId, building.CompanyId);
            }
            else
            {
                Assert.Equal(buyerCompanyId, building.CompanyId);
            }
        }
    }

    [Fact]
    public async Task AcceptBuildingOffer_StaleVersion_DoesNotTransferOwnershipOrFunds()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller-stale-version@market.test");
        var buyer1Token = await RegisterAsync(client, "buyer-stale-version-a@market.test");
        var buyer2Token = await RegisterAsync(client, "buyer-stale-version-b@market.test");
        var (buildingId, sellerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyer1CompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyer1Token, 5_000_000m);
        var (_, buyer2CompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyer2Token, 5_000_000m);

        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offer1 = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId = buyer1CompanyId, offeredPrice = 1_000_000m } },
            buyer1Token);
        var offer1Id = Guid.Parse(offer1.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offer1Version = Guid.Parse(offer1.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var offer2 = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId = buyer2CompanyId, offeredPrice = 1_050_000m } },
            buyer2Token);
        var offer2Id = Guid.Parse(offer2.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var staleOffer2Version = Guid.Parse(offer2.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        await using (var preScope = factory.Services.CreateAsyncScope())
        {
            var db = preScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var buyer2AccountBefore = await db.BankAccounts.FirstAsync(a => a.CompanyId == buyer2CompanyId && a.CurrencyCode == "EUR");
            Assert.Equal(5_000_000m, buyer2AccountBefore.Balance);
        }

        await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { offer { id status } } }",
            new { input = new { offerId = offer1Id, offerVersion = offer1Version } },
            sellerToken);

        var staleAccept = await ExecAsync(client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { offer { id status } } }",
            new { input = new { offerId = offer2Id, offerVersion = staleOffer2Version } },
            sellerToken);
        Assert.True(staleAccept.TryGetProperty("errors", out var staleErrors));
        Assert.Contains(
            "OFFER_VERSION_CONFLICT",
            staleErrors.EnumerateArray().Select(e =>
                e.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var code)
                    ? code.GetString()
                    : null));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var building = await verifyDb.Buildings.FirstAsync(b => b.Id == buildingId);
        Assert.Equal(buyer1CompanyId, building.CompanyId);

        var buyer2AccountAfter = await verifyDb.BankAccounts.FirstAsync(a => a.CompanyId == buyer2CompanyId && a.CurrencyCode == "EUR");
        Assert.Equal(5_000_000m, buyer2AccountAfter.Balance);

        var buyer2AcquisitionEntries = await verifyDb.LedgerEntries
            .Where(e => e.CompanyId == buyer2CompanyId && e.Category == LedgerCategory.BuildingAcquisition)
            .ToListAsync();
        Assert.Empty(buyer2AcquisitionEntries);
        var sellerSaleEntries = await verifyDb.LedgerEntries
            .Where(e => e.CompanyId == sellerCompanyId && e.Category == LedgerCategory.BuildingSale && e.BuildingId == buildingId)
            .ToListAsync();
        Assert.Single(sellerSaleEntries);
    }

    [Fact]
    public async Task CancelBuildingOffer_ConcurrentWithAccept_SecondCommitConflicts()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var sellerToken = await RegisterAsync(client, "seller-cancel-race@market.test");
        var buyerToken = await RegisterAsync(client, "buyer-cancel-race@market.test");
        var (buildingId, _, _) = await SeedOwnerWithBuildingAsync(factory, sellerToken);
        var (_, buyerCompanyId, _) = await SeedOwnerWithBuildingAsync(factory, buyerToken, 5_000_000m);

        await ExecAsync(client,
            "mutation S($input: SetBuildingForSaleInput!) { setBuildingForSale(input: $input) { id } }",
            new { input = new { buildingId, isForSale = true, askingPrice = 1_000_000m } },
            sellerToken);

        var offerResult = await ExecAsync(client,
            "mutation O($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id offerVersion } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 980_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);
        var offerVersion = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("offerVersion").GetString()!);

        var acceptTask = ExecAsync(
            client,
            "mutation A($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { offer { id status } } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);
        var cancelTask = ExecAsync(
            client,
            "mutation C($input: CancelBuildingOfferInput!) { cancelBuildingOffer(input: $input) { id status } }",
            new { input = new { offerId, offerVersion } },
            sellerToken);
        var raceResults = await Task.WhenAll(acceptTask, cancelTask);
        static IEnumerable<string?> GetCodes(JsonElement response) =>
            response.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array
                ? errors.EnumerateArray()
                    .Select(e =>
                        e.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var code)
                            ? code.GetString()
                            : null)
                : [];

        var successCount = raceResults.Count(r =>
            (r.TryGetProperty("data", out var data)
             && data.ValueKind == JsonValueKind.Object
             && (
                 (data.TryGetProperty("acceptBuildingOffer", out var a) && a.ValueKind == JsonValueKind.Object)
                 || (data.TryGetProperty("cancelBuildingOffer", out var c) && c.ValueKind == JsonValueKind.Object))));
        var conflictCount = raceResults.Count(r => GetCodes(r).Contains("OFFER_VERSION_CONFLICT"));

        Assert.Equal(1, successCount);
        Assert.Equal(1, conflictCount);
    }
}
