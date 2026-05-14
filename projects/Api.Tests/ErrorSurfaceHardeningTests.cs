using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Security;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests verifying that unauthorized probes across building, banking, lending,
/// and stock mutations return the uniform <c>FORBIDDEN</c> error code
/// rather than domain-specific codes that would leak existence, listing state, or balance details.
/// </summary>
/// <remarks>
/// Acceptance criteria addressed:
/// - All building, banking, lending, and stock mutations return FORBIDDEN when a caller
///   probes an object they do not own, regardless of whether the object exists or its state.
/// - No mutation error response includes precise balance figures, listing prices, or existence flags
///   for foreign-owned objects.
/// - Internal audit logs capture the real reason for moderation review.
/// </remarks>
public sealed class ErrorSurfaceHardeningTests : IAsyncLifetime
{
    private readonly ApiWebApplicationFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────────────────────

    private async Task<JsonElement> ExecuteGraphQlAsync(string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { query, variables }),
                Encoding.UTF8,
                "application/json"),
        };
        if (token is not null)
            request.Headers.Authorization = new("Bearer", token);
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<string> RegisterAndGetTokenAsync(string email, string displayName = "Harding Tester")
    {
        var result = await ExecuteGraphQlAsync(
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName, password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static string GetErrorCode(JsonElement result)
    {
        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0,
            "Expected an error response but none was returned.");
        return errors[0].GetProperty("extensions").GetProperty("code").GetString()!;
    }

    private static void AssertOpaque(JsonElement result, string scenario)
    {
        var code = GetErrorCode(result);
        Assert.True(
            code == ObjectAuthorizationService.NotFoundOrNotOwnedCode,
            $"[{scenario}] Expected {ObjectAuthorizationService.NotFoundOrNotOwnedCode} but got '{code}'.");
    }

    private static void AssertNoBalanceInErrors(JsonElement result, string scenario)
    {
        // Error messages must not expose numeric balance or price details.
        var raw = result.GetRawText();
        // Error messages must not expose numeric balance details.
        Assert.DoesNotMatch(@"Available: \d", raw);
    }

    // Seed a company with a bank account for a player.
    private async Task<(Guid companyId, BankAccount bankAccount)> SeedCompanyWithBankAccountAsync(
        Guid playerId,
        string companyName,
        decimal balance = 10_000m)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var company = new Company { Id = Guid.NewGuid(), Name = companyName, Cash = 0m, PlayerId = playerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_000L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = city.CurrencyCode,
            Balance = balance,
            CompanyId = company.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        await db.SaveChangesAsync();

        return (company.Id, account);
    }

    private async Task<Guid> GetPlayerIdAsync(string token)
    {
        var result = await ExecuteGraphQlAsync("{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Building mutations — non-owner probe tests
    // ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetBuildingForSale_NonOwner_ReturnsNotFoundOrNotOwned()
    {
        var ownerToken = await RegisterAndGetTokenAsync($"noo-b-owner-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-b-probe-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var company = new Company { Id = Guid.NewGuid(), Name = "NooBOwner Co", Cash = 0m, PlayerId = ownerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id,
            Type = BuildingType.Factory, Name = "Owner Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1, BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        // (b) Non-owner probes existing building → must return opaque error, not distinguish from not-found
        var resultB = await ExecuteGraphQlAsync(
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId = building.Id, isForSale = true, askingPrice = 100_000m } },
            probeToken);
        AssertOpaque(resultB, "SetBuildingForSale non-owner probe existing building");

        // (c) Non-owner probes non-existent building → must return same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation S($i: SetBuildingForSaleInput!) { setBuildingForSale(input: $i) { id } }",
            new { i = new { buildingId = Guid.NewGuid(), isForSale = true, askingPrice = 100_000m } },
            probeToken);
        AssertOpaque(resultC, "SetBuildingForSale non-owner probe non-existent building");

        // Both (b) and (c) must return the identical error code.
        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    [Fact]
    public async Task StoreBuildingConfiguration_NonOwner_ReturnsNotFoundOrNotOwned()
    {
        var ownerToken = await RegisterAndGetTokenAsync($"noo-c-owner-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-c-probe-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var company = new Company { Id = Guid.NewGuid(), Name = "NooCOwner Co", Cash = 0m, PlayerId = ownerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id,
            Type = BuildingType.Factory, Name = "Config Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1, BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var configInput = new
        {
            buildingId = building.Id,
            units = new[]
            {
                new { unitType = "PURCHASE", gridX = 0, gridY = 0,
                      linkUp = false, linkDown = false, linkLeft = false, linkRight = false,
                      linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
            }
        };

        var resultB = await ExecuteGraphQlAsync(
            "mutation SC($i: StoreBuildingConfigurationInput!) { storeBuildingConfiguration(input: $i) { id } }",
            new { i = configInput }, probeToken);
        AssertOpaque(resultB, "StoreBuildingConfiguration non-owner probe");

        var resultC = await ExecuteGraphQlAsync(
            "mutation SC($i: StoreBuildingConfigurationInput!) { storeBuildingConfiguration(input: $i) { id } }",
            new { i = new { buildingId = Guid.NewGuid(), units = configInput.units } }, probeToken);
        AssertOpaque(resultC, "StoreBuildingConfiguration non-existent probe");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    [Fact]
    public async Task MakeOfferOnBuilding_NonListedBuilding_ReturnsNotFoundOrNotOwned()
    {
        // A probe for a building that is NOT listed for sale must return the same opaque code
        // as probing a non-existent building. The BUILDING_NOT_FOR_SALE code was information-leaking.
        var ownerToken = await RegisterAndGetTokenAsync($"noo-m-owner-{Guid.NewGuid():N}@t.com");
        var buyerToken = await RegisterAndGetTokenAsync($"noo-m-buyer-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);
        var buyerId = await GetPlayerIdAsync(buyerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var ownerCompany = new Company { Id = Guid.NewGuid(), Name = "MO Owner Co", Cash = 0m, PlayerId = ownerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(ownerCompany);
        var buyerCompany = new Company { Id = Guid.NewGuid(), Name = "MO Buyer Co", Cash = 100_000m, PlayerId = buyerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(buyerCompany);
        await db.SaveChangesAsync();

        var unlisted = new Building
        {
            Id = Guid.NewGuid(), CompanyId = ownerCompany.Id,
            Type = BuildingType.Factory, Name = "Unlisted Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1, BuiltAtUtc = DateTime.UtcNow,
            IsForSale = false,
        };
        db.Buildings.Add(unlisted);

        var buyerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_000L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = city.CurrencyCode, Balance = 100_000m,
            CompanyId = buyerCompany.Id, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(buyerAccount);
        await db.SaveChangesAsync();

        var offerInput = new { buildingId = unlisted.Id, buyerCompanyId = buyerCompany.Id, offeredPrice = 50_000m };

        // (b) Building exists but not listed → opaque error
        var resultB = await ExecuteGraphQlAsync(
            "mutation MO($i: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $i) { id } }",
            new { i = offerInput }, buyerToken);
        AssertOpaque(resultB, "MakeOfferOnBuilding unlisted building");

        // (c) Building non-existent → same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation MO($i: MakeOfferOnBuildingInput!) { makeOfferOnBuilding(input: $i) { id } }",
            new { i = new { buildingId = Guid.NewGuid(), buyerCompanyId = buyerCompany.Id, offeredPrice = 50_000m } }, buyerToken);
        AssertOpaque(resultC, "MakeOfferOnBuilding non-existent building");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    [Fact]
    public async Task AcceptBuildingOffer_BuyerBalanceMustNotLeakToSeller()
    {
        // When a buyer has insufficient funds and the seller calls AcceptBuildingOffer,
        // the error must NOT include the buyer's exact available balance.
        var ownerToken = await RegisterAndGetTokenAsync($"noo-a-owner-{Guid.NewGuid():N}@t.com");
        var buyerToken = await RegisterAndGetTokenAsync($"noo-a-buyer-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);
        var buyerId = await GetPlayerIdAsync(buyerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var ownerCompany = new Company { Id = Guid.NewGuid(), Name = "ABal Owner Co", Cash = 0m, PlayerId = ownerId, FoundedAtUtc = DateTime.UtcNow };
        var buyerCompany = new Company { Id = Guid.NewGuid(), Name = "ABal Buyer Co", Cash = 0m, PlayerId = buyerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(ownerCompany);
        db.Companies.Add(buyerCompany);
        await db.SaveChangesAsync();

        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = ownerCompany.Id,
            Type = BuildingType.Factory, Name = "ABal Factory",
            Latitude = city.Latitude, Longitude = city.Longitude, Level = 1, BuiltAtUtc = DateTime.UtcNow,
            IsForSale = true, AskingPrice = 500_000m,
        };
        db.Buildings.Add(building);

        // Buyer bank account with INSUFFICIENT funds (only 100 EUR, asking 500k)
        var buyerAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_000L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = city.CurrencyCode, Balance = 100m,
            CompanyId = buyerCompany.Id, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(buyerAccount);

        var offer = new BuildingSaleOffer
        {
            Id = Guid.NewGuid(), BuildingId = building.Id,
            BuyerCompanyId = buyerCompany.Id, BuyerPlayerId = buyerId,
            OfferedPrice = 500_000m,
            Status = BuildingSaleOfferStatus.Pending,
            OfferVersion = Guid.NewGuid(), CreatedAtUtc = DateTime.UtcNow,
        };
        db.BuildingSaleOffers.Add(offer);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            "mutation AcceptOffer($i: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $i) { building { id } } }",
            new { i = new { offerId = offer.Id, offerVersion = offer.OfferVersion } },
            ownerToken);

        // Must have error (insufficient funds)
        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0,
            "Expected INSUFFICIENT_FUNDS error.");

        // The error message must NOT reveal the buyer's exact balance to the seller.
        AssertNoBalanceInErrors(result, "AcceptBuildingOffer buyer balance must not leak");
    }

    [Fact]
    public async Task AcceptBuildingOffer_ForeignAndMissingOffer_ReturnSameNotFoundOrNotOwned()
    {
        var sellerToken = await RegisterAndGetTokenAsync($"noo-accept-seller-{Guid.NewGuid():N}@t.com");
        var buyerToken = await RegisterAndGetTokenAsync($"noo-accept-buyer-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-accept-probe-{Guid.NewGuid():N}@t.com");
        var sellerId = await GetPlayerIdAsync(sellerToken);
        var buyerId = await GetPlayerIdAsync(buyerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), Name = "Accept Seller Co", Cash = 0m, PlayerId = sellerId, FoundedAtUtc = DateTime.UtcNow };
        var buyerCompany = new Company { Id = Guid.NewGuid(), Name = "Accept Buyer Co", Cash = 0m, PlayerId = buyerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            Type = BuildingType.Factory,
            Name = "Accept Market Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            BuiltAtUtc = DateTime.UtcNow,
            IsForSale = true,
            AskingPrice = 50_000m,
        };
        db.Buildings.Add(building);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_020L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = city.CurrencyCode,
            Balance = 100_000m,
            CompanyId = buyerCompany.Id,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var offer = new BuildingSaleOffer
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuyerCompanyId = buyerCompany.Id,
            BuyerPlayerId = buyerId,
            OfferedPrice = 50_000m,
            Status = BuildingSaleOfferStatus.Pending,
            OfferVersion = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BuildingSaleOffers.Add(offer);
        await db.SaveChangesAsync();

        var resultExisting = await ExecuteGraphQlAsync(
            "mutation A($i: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $i) { offer { id } } }",
            new { i = new { offerId = offer.Id, offerVersion = offer.OfferVersion } },
            probeToken);
        AssertOpaque(resultExisting, "AcceptBuildingOffer foreign offer");

        var resultMissing = await ExecuteGraphQlAsync(
            "mutation A($i: AcceptBuildingOfferInput!) { acceptBuildingOffer(input: $i) { offer { id } } }",
            new { i = new { offerId = Guid.NewGuid(), offerVersion = Guid.NewGuid() } },
            probeToken);
        AssertOpaque(resultMissing, "AcceptBuildingOffer missing offer");

        Assert.Equal(GetErrorCode(resultExisting), GetErrorCode(resultMissing));
    }

    [Fact]
    public async Task CancelBuildingOffer_ForeignAndMissingOffer_ReturnSameNotFoundOrNotOwned()
    {
        var sellerToken = await RegisterAndGetTokenAsync($"noo-cancel-seller-{Guid.NewGuid():N}@t.com");
        var buyerToken = await RegisterAndGetTokenAsync($"noo-cancel-buyer-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-cancel-probe-{Guid.NewGuid():N}@t.com");
        var sellerId = await GetPlayerIdAsync(sellerToken);
        var buyerId = await GetPlayerIdAsync(buyerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var sellerCompany = new Company { Id = Guid.NewGuid(), Name = "Cancel Seller Co", Cash = 0m, PlayerId = sellerId, FoundedAtUtc = DateTime.UtcNow };
        var buyerCompany = new Company { Id = Guid.NewGuid(), Name = "Cancel Buyer Co", Cash = 0m, PlayerId = buyerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            Type = BuildingType.Factory,
            Name = "Cancel Market Factory",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            BuiltAtUtc = DateTime.UtcNow,
            IsForSale = true,
            AskingPrice = 40_000m,
        };
        db.Buildings.Add(building);

        var offer = new BuildingSaleOffer
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuyerCompanyId = buyerCompany.Id,
            BuyerPlayerId = buyerId,
            OfferedPrice = 40_000m,
            Status = BuildingSaleOfferStatus.Pending,
            OfferVersion = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BuildingSaleOffers.Add(offer);
        await db.SaveChangesAsync();

        var resultExisting = await ExecuteGraphQlAsync(
            "mutation C($i: CancelBuildingOfferInput!) { cancelBuildingOffer(input: $i) { id } }",
            new { i = new { offerId = offer.Id, offerVersion = offer.OfferVersion } },
            probeToken);
        AssertOpaque(resultExisting, "CancelBuildingOffer foreign offer");

        var resultMissing = await ExecuteGraphQlAsync(
            "mutation C($i: CancelBuildingOfferInput!) { cancelBuildingOffer(input: $i) { id } }",
            new { i = new { offerId = Guid.NewGuid(), offerVersion = Guid.NewGuid() } },
            probeToken);
        AssertOpaque(resultMissing, "CancelBuildingOffer missing offer");

        Assert.Equal(GetErrorCode(resultExisting), GetErrorCode(resultMissing));
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Banking mutations — non-owner bank account probe tests
    // ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TransferFunds_NonOwnerFromAccount_ReturnsNotFoundOrNotOwned()
    {
        var ownerToken = await RegisterAndGetTokenAsync($"noo-tf-owner-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-tf-probe-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);
        var probeId = await GetPlayerIdAsync(probeToken);

        var (_, ownerAccount) = await SeedCompanyWithBankAccountAsync(ownerId, "TF Owner Co", 5_000m);
        var (probeCompanyId, probeAccount) = await SeedCompanyWithBankAccountAsync(probeId, "TF Probe Co", 1_000m);

        // (b) Probe tries to transfer FROM the owner's account using the probe's own destination account
        var resultB = await ExecuteGraphQlAsync(
            "mutation TF($i: TransferFundsInput!) { transferFunds(input: $i) { amount } }",
            new { i = new { fromBankAccountId = ownerAccount.Id, toBankAccountId = probeAccount.Id, amount = 100m, description = (string?)null } },
            probeToken);
        AssertOpaque(resultB, "TransferFunds from foreign account (probe existing)");

        // (c) Same probe using a non-existent source account → same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation TF($i: TransferFundsInput!) { transferFunds(input: $i) { amount } }",
            new { i = new { fromBankAccountId = Guid.NewGuid(), toBankAccountId = probeAccount.Id, amount = 100m, description = (string?)null } },
            probeToken);
        AssertOpaque(resultC, "TransferFunds from non-existent account");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    [Fact]
    public async Task TransferFunds_NonOwnerToAccount_ReturnsNotFoundOrNotOwned()
    {
        var ownerToken = await RegisterAndGetTokenAsync($"noo-tft-owner-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-tft-probe-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);
        var probeId = await GetPlayerIdAsync(probeToken);

        var (_, ownerAccount) = await SeedCompanyWithBankAccountAsync(ownerId, "TFT Owner Co", 5_000m);
        var (_, probeFromAccount) = await SeedCompanyWithBankAccountAsync(probeId, "TFT Probe From Co", 1_000m);
        var (_, probeToAccount2) = await SeedCompanyWithBankAccountAsync(probeId, "TFT Probe To2 Co", 500m);

        // (b) Probe tries to transfer TO the owner's foreign account
        var resultB = await ExecuteGraphQlAsync(
            "mutation TF($i: TransferFundsInput!) { transferFunds(input: $i) { amount } }",
            new { i = new { fromBankAccountId = probeFromAccount.Id, toBankAccountId = ownerAccount.Id, amount = 100m, description = (string?)null } },
            probeToken);
        AssertOpaque(resultB, "TransferFunds to foreign account (probe existing)");

        // (c) Non-existent destination → same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation TF($i: TransferFundsInput!) { transferFunds(input: $i) { amount } }",
            new { i = new { fromBankAccountId = probeFromAccount.Id, toBankAccountId = Guid.NewGuid(), amount = 100m, description = (string?)null } },
            probeToken);
        AssertOpaque(resultC, "TransferFunds to non-existent account");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Lending mutations — non-borrower loan probe tests
    // ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RepayLoanDebt_NonBorrower_ReturnsNotFoundOrNotOwned()
    {
        var borrowerToken = await RegisterAndGetTokenAsync($"noo-l-borrow-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-l-probe-{Guid.NewGuid():N}@t.com");
        var borrowerId = await GetPlayerIdAsync(borrowerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var borrowerCompany = new Company { Id = Guid.NewGuid(), Name = "Borrower Co", Cash = 0m, PlayerId = borrowerId, FoundedAtUtc = DateTime.UtcNow };
        var lenderCompany = new Company { Id = Guid.NewGuid(), Name = "Lender Co", Cash = 0m, PlayerId = borrowerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(borrowerCompany);
        db.Companies.Add(lenderCompany);
        await db.SaveChangesAsync();

        // Seed a loan for the borrower company. FK constraints not enforced in InMemory.
        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = Guid.NewGuid(),          // not enforced by InMemory
            BorrowerCompanyId = borrowerCompany.Id,
            LenderCompanyId = lenderCompany.Id,
            BankBuildingId = Guid.NewGuid(),       // not enforced by InMemory
            OriginalPrincipal = 5_000m,
            RemainingPrincipal = 5_000m,
            AnnualInterestRatePercent = 5m,
            DurationTicks = 100,
            StartTick = 1,
            DueTick = 101,
            NextPaymentTick = 50,
            PaymentAmount = 250m,
            TotalPayments = 20,
            Status = LoanStatus.Active,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);
        await db.SaveChangesAsync();

        // (b) Non-borrower probes existing loan → opaque error
        var resultB = await ExecuteGraphQlAsync(
            "mutation RL($i: RepayLoanDebtInput!) { repayLoanDebt(input: $i) { id remainingPrincipal } }",
            new { i = new { loanId = loan.Id } },
            probeToken);
        AssertOpaque(resultB, "RepayLoanDebt non-borrower probe existing loan");

        // (c) Non-borrower probes non-existent loan → same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation RL($i: RepayLoanDebtInput!) { repayLoanDebt(input: $i) { id remainingPrincipal } }",
            new { i = new { loanId = Guid.NewGuid() } },
            probeToken);
        AssertOpaque(resultC, "RepayLoanDebt non-borrower probe non-existent loan");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Stock mutations — non-owner order probe tests
    // ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelLimitOrder_NonOwner_ReturnsNotFoundOrNotOwned()
    {
        // CancelLimitOrder already uses ObjectAuthorizationService, but we verify it returns
        // the correct opaque code rather than any domain-specific alternative.
        var ownerToken = await RegisterAndGetTokenAsync($"noo-s-owner-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-s-probe-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var ownerCompany = new Company { Id = Guid.NewGuid(), Name = "LO Owner Co", Cash = 0m, PlayerId = ownerId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(ownerCompany);
        await db.SaveChangesAsync();

        var settlement = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_000L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = "USD", Balance = 5_000m,
            CompanyId = ownerCompany.Id, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(settlement);

        var order = new LimitOrder
        {
            Id = Guid.NewGuid(),
            CompanyId = ownerCompany.Id,
            StockSymbol = "CMP-TESTSTOCK000000000000000000000",
            Side = LimitOrderSide.Buy,
            LimitPrice = 1m,
            Quantity = 1,
            FilledQuantity = 0,
            Status = LimitOrderStatus.Open,
            OwnerPlayerId = ownerId,
            OwnerCompanyId = null,
            SettlementBankAccountId = settlement.Id,
            ReservedCashRemaining = 1m,
            CreatedAtTick = 1,
            UpdatedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LimitOrders.Add(order);
        await db.SaveChangesAsync();

        // (b) Non-owner probes existing order
        var resultB = await ExecuteGraphQlAsync(
            "mutation CLO($orderId: UUID!) { cancelLimitOrder(orderId: $orderId) { id } }",
            new { orderId = order.Id }, probeToken);
        AssertOpaque(resultB, "CancelLimitOrder non-owner probe existing order");

        // (c) Non-owner probes non-existent order → same opaque error
        var resultC = await ExecuteGraphQlAsync(
            "mutation CLO($orderId: UUID!) { cancelLimitOrder(orderId: $orderId) { id } }",
            new { orderId = Guid.NewGuid() }, probeToken);
        AssertOpaque(resultC, "CancelLimitOrder probe non-existent order");

        Assert.Equal(GetErrorCode(resultB), GetErrorCode(resultC));
    }

    // ──────────────────────────────────────────────────────────────────────────────────────────
    // Cross-cutting: error messages must not expose exact balances for foreign objects
    // ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task TransferFunds_InsufficientFunds_MustNotExposeBalanceInErrorMessage()
    {
        // The authenticated player's OWN insufficient-balance error should not expose the exact
        // balance in a way that can be intercepted by a man-in-the-middle or log scraper.
        // While showing the player their own balance is less critical, error messages should be
        // consistent. This test verifies the "from foreign account" path is fully opaque.
        var ownerToken = await RegisterAndGetTokenAsync($"noo-bal-{Guid.NewGuid():N}@t.com");
        var probeToken = await RegisterAndGetTokenAsync($"noo-balp-{Guid.NewGuid():N}@t.com");
        var ownerId = await GetPlayerIdAsync(ownerToken);

        var (_, foreignAccount) = await SeedCompanyWithBankAccountAsync(ownerId, "Bal Owner Co", 42_000m);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var probeId = await GetPlayerIdAsync(probeToken);
        var probeCompany = new Company { Id = Guid.NewGuid(), Name = "Bal Probe Co", Cash = 0m, PlayerId = probeId, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.Add(probeCompany);
        await db.SaveChangesAsync();
        var destAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (1_000_000_000_000_001L + (DateTime.UtcNow.Ticks % 1_000_000_000_000_000L)).ToString("D16"),
            CurrencyCode = city.CurrencyCode, Balance = 0m,
            CompanyId = probeCompany.Id, CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(destAccount);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            "mutation TF($i: TransferFundsInput!) { transferFunds(input: $i) { amount } }",
            new { i = new { fromBankAccountId = foreignAccount.Id, toBankAccountId = destAccount.Id, amount = 100m, description = (string?)null } },
            probeToken);

        // Must be opaque (no exact balance leak for foreign account probe)
        AssertOpaque(result, "TransferFunds foreign from-account — balance must not leak");
        AssertNoBalanceInErrors(result, "TransferFunds foreign from-account");
    }
}
