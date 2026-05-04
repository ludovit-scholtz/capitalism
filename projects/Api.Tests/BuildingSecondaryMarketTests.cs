using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Tests.Infrastructure;
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
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
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
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        var acceptResult = await ExecAsync(client,
            """
            mutation Accept($input: AcceptBuildingOfferInput!) {
                acceptBuildingOffer(input: $input) {
                    building { id companyId isForSale askingPrice }
                    offer { id status resolvedAtUtc }
                }
            }
            """,
            new { input = new { offerId } },
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
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId = buyer1CompanyId, offeredPrice = 1_800_000m } },
            buyer1Token);
        var offer1Id = Guid.Parse(offer1Result.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        var offer2Result = await ExecAsync(client,
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId = buyer2CompanyId, offeredPrice = 2_000_000m } },
            buyer2Token);
        var offer2Id = Guid.Parse(offer2Result.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        // Accept offer1
        await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId = offer1Id } },
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
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_800_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        var rejectResult = await ExecAsync(client,
            """
            mutation Reject($input: RejectBuildingOfferInput!) {
                rejectBuildingOffer(input: $input) { id status resolvedAtUtc }
            }
            """,
            new { input = new { offerId } },
            sellerToken);

        var rejectedOffer = rejectResult.GetProperty("data").GetProperty("rejectBuildingOffer");
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
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        // Drain buyer's bank account
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var buyerAccount = await db.BankAccounts.FirstAsync(a => a.CompanyId == buyerCompanyId);
        buyerAccount.Balance = 0m;
        await db.SaveChangesAsync();

        var result = await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id } } }",
            new { input = new { offerId } },
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
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
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
            "mutation MakeOffer($input: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $input) { id } }",
            new { input = new { buildingId = building.Id, buyerCompanyId, offeredPrice = 1_000_000m } },
            buyerToken);
        var offerId = Guid.Parse(offerResult.GetProperty("data").GetProperty("makeOfferOnBuilding").GetProperty("id").GetString()!);

        await ExecAsync(client,
            "mutation Accept($input: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $input) { building { id companyId } } }",
            new { input = new { offerId } },
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
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
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
            AccountNumber = $"{Random.Shared.NextInt64(1_000_000_000_000_000L, 9_999_999_999_999_999L)}",
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
}
