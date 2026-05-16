using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class SupplyContractsIntegrationTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName = "Supply Test", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetMeIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task ProposeSupplyContract_CreatesPendingContractVisibleInMyContracts()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAndGetTokenAsync(client, $"supply-seller-{Guid.NewGuid():N}@example.com");
        var buyerToken = await RegisterAndGetTokenAsync(client, $"supply-buyer-{Guid.NewGuid():N}@example.com");
        var sellerPlayerId = await GetMeIdAsync(client, sellerToken);
        var buyerPlayerId = await GetMeIdAsync(client, buyerToken);

        var seeded = await SeedSupplyContextAsync(factory, sellerPlayerId, buyerPlayerId, withBuyerPurchaseUnit: true, sellerInventoryQuantity: 1000m);

        var proposeResult = await ExecuteGraphQlAsync(
            client,
            """
            mutation($input: ProposeSupplyContractInput!) {
              proposeSupplyContract(input: $input) { success contract { id status sellerCompanyId buyerCompanyId } }
            }
            """,
            new
            {
                input = new
                {
                    sellerCompanyId = seeded.SellerCompanyId,
                    buyerCompanyId = seeded.BuyerCompanyId,
                    sellerBuildingUnitId = seeded.SellerB2BUnitId,
                    resourceTypeId = seeded.ResourceTypeId,
                    quantityPerTick = 100m,
                    pricePerUnit = 12m,
                    durationTicks = 100,
                    penaltyRatePercent = 10m
                }
            },
            sellerToken);

        var created = proposeResult.GetProperty("data").GetProperty("proposeSupplyContract").GetProperty("contract");
        Assert.Equal("PENDING", created.GetProperty("status").GetString());

        var buyerContracts = await ExecuteGraphQlAsync(
            client,
            "query { myContracts(take: 50, skip: 0, status: \"PENDING\") { id sellerCompanyId buyerCompanyId status } }",
            token: buyerToken);
        var rows = buyerContracts.GetProperty("data").GetProperty("myContracts").EnumerateArray().ToList();
        Assert.Contains(rows, row => row.GetProperty("id").GetString() == created.GetProperty("id").GetString());
    }

    [Fact]
    public async Task AcceptSupplyContract_TransitionsPendingToActive()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAndGetTokenAsync(client, $"supply-accept-seller-{Guid.NewGuid():N}@example.com");
        var buyerToken = await RegisterAndGetTokenAsync(client, $"supply-accept-buyer-{Guid.NewGuid():N}@example.com");
        var sellerPlayerId = await GetMeIdAsync(client, sellerToken);
        var buyerPlayerId = await GetMeIdAsync(client, buyerToken);

        var seeded = await SeedSupplyContextAsync(factory, sellerPlayerId, buyerPlayerId, withBuyerPurchaseUnit: true, sellerInventoryQuantity: 1000m);
        var contractId = await ProposeContractAsync(client, sellerToken, seeded);

        var acceptResult = await ExecuteGraphQlAsync(
            client,
            "mutation($id: UUID!) { acceptSupplyContract(id: $id) { success contract { status startTick } } }",
            new { id = contractId },
            buyerToken);

        var accepted = acceptResult.GetProperty("data").GetProperty("acceptSupplyContract").GetProperty("contract");
        Assert.Equal("ACTIVE", accepted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task SupplyContractFulfillmentPhase_DeliversInventoryAndCreatesLedgerRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAndGetTokenAsync(client, $"supply-fulfill-seller-{Guid.NewGuid():N}@example.com");
        var buyerToken = await RegisterAndGetTokenAsync(client, $"supply-fulfill-buyer-{Guid.NewGuid():N}@example.com");
        var sellerPlayerId = await GetMeIdAsync(client, sellerToken);
        var buyerPlayerId = await GetMeIdAsync(client, buyerToken);

        var seeded = await SeedSupplyContextAsync(factory, sellerPlayerId, buyerPlayerId, withBuyerPurchaseUnit: true, sellerInventoryQuantity: 1000m);
        var contractId = await ProposeContractAsync(client, sellerToken, seeded);
        await ExecuteGraphQlAsync(client, "mutation($id: UUID!) { acceptSupplyContract(id: $id) { success } }", new { id = contractId }, buyerToken);

        await ProcessTickAsync(factory);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contract = await db.SupplyContracts.FirstAsync(item => item.Id == contractId);
        Assert.True(contract.TotalDeliveredQuantity > 0m);
        Assert.True(contract.RemainingTicks < contract.DurationTicks);

        var sellerRevenue = await db.LedgerEntries.AnyAsync(entry => entry.CompanyId == seeded.SellerCompanyId && entry.Category == LedgerCategory.SupplyContractRevenue);
        var buyerPayment = await db.LedgerEntries.AnyAsync(entry => entry.CompanyId == seeded.BuyerCompanyId && entry.Category == LedgerCategory.SupplyContractPayment);
        Assert.True(sellerRevenue);
        Assert.True(buyerPayment);
    }

    [Fact]
    public async Task SupplyContractFulfillmentPhase_UnderdeliveryAppliesPenalty()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var sellerToken = await RegisterAndGetTokenAsync(client, $"supply-penalty-seller-{Guid.NewGuid():N}@example.com");
        var buyerToken = await RegisterAndGetTokenAsync(client, $"supply-penalty-buyer-{Guid.NewGuid():N}@example.com");
        var sellerPlayerId = await GetMeIdAsync(client, sellerToken);
        var buyerPlayerId = await GetMeIdAsync(client, buyerToken);

        var seeded = await SeedSupplyContextAsync(factory, sellerPlayerId, buyerPlayerId, withBuyerPurchaseUnit: true, sellerInventoryQuantity: 10m);
        var contractId = await ProposeContractAsync(client, sellerToken, seeded);
        await ExecuteGraphQlAsync(client, "mutation($id: UUID!) { acceptSupplyContract(id: $id) { success } }", new { id = contractId }, buyerToken);

        await ProcessTickAsync(factory);

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var db = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var contract = await db.SupplyContracts.FirstAsync(item => item.Id == contractId);
        Assert.True(contract.PenaltyCount > 0);
        Assert.True(contract.TotalPenaltyAmount > 0m);
        var penaltyRows = await db.LedgerEntries
            .Where(entry => entry.Category == LedgerCategory.SupplyContractPenalty && (entry.CompanyId == seeded.SellerCompanyId || entry.CompanyId == seeded.BuyerCompanyId))
            .ToListAsync();
        Assert.NotEmpty(penaltyRows);
    }

    private static async Task<Guid> ProposeContractAsync(HttpClient client, string sellerToken, SeededSupplyContext seeded)
    {
        var result = await ExecuteGraphQlAsync(
            client,
            "mutation($input: ProposeSupplyContractInput!) { proposeSupplyContract(input: $input) { contract { id } } }",
            new
            {
                input = new
                {
                    sellerCompanyId = seeded.SellerCompanyId,
                    buyerCompanyId = seeded.BuyerCompanyId,
                    sellerBuildingUnitId = seeded.SellerB2BUnitId,
                    resourceTypeId = seeded.ResourceTypeId,
                    quantityPerTick = 100m,
                    pricePerUnit = 12m,
                    durationTicks = 25,
                    penaltyRatePercent = 10m
                }
            },
            sellerToken);
        return Guid.Parse(result.GetProperty("data").GetProperty("proposeSupplyContract").GetProperty("contract").GetProperty("id").GetString()!);
    }

    private static async Task<SeededSupplyContext> SeedSupplyContextAsync(ApiWebApplicationFactory factory, Guid sellerPlayerId, Guid buyerPlayerId, bool withBuyerPurchaseUnit, decimal sellerInventoryQuantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var resource = await db.ResourceTypes.OrderBy(r => r.Name).FirstAsync();

        var sellerCompany = new Company { Id = Guid.NewGuid(), PlayerId = sellerPlayerId, Name = "Seller Co", FoundedAtTick = 0, FoundedAtUtc = DateTime.UtcNow };
        var buyerCompany = new Company { Id = Guid.NewGuid(), PlayerId = buyerPlayerId, Name = "Buyer Co", FoundedAtTick = 0, FoundedAtUtc = DateTime.UtcNow };
        db.Companies.AddRange(sellerCompany, buyerCompany);

        var sellerBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Seller Plant",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BuiltAtUtc = DateTime.UtcNow
        };
        var buyerBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = buyerCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Buyer Plant",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BuiltAtUtc = DateTime.UtcNow
        };
        db.Buildings.AddRange(sellerBuilding, buyerBuilding);

        var sellerUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = sellerBuilding.Id,
            UnitType = UnitType.B2BSales,
            GridX = 0,
            GridY = 0,
            ResourceTypeId = resource.Id,
            Level = 1
        };
        db.BuildingUnits.Add(sellerUnit);

        if (withBuyerPurchaseUnit)
        {
            db.BuildingUnits.Add(new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = buyerBuilding.Id,
                UnitType = UnitType.Purchase,
                GridX = 0,
                GridY = 0,
                ResourceTypeId = resource.Id,
                Level = 1
            });
        }

        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = sellerBuilding.Id,
            BuildingUnitId = sellerUnit.Id,
            ResourceTypeId = resource.Id,
            Quantity = sellerInventoryQuantity,
            Quality = 0.7m,
            SourcingCostTotal = sellerInventoryQuantity * 3m
        });

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = sellerCompany.Id,
            CurrencyCode = city.CurrencyCode,
            Balance = 1_000_000m,
            AccountNumber = GenerateTestAccountNumber(),
            CreatedAtUtc = DateTime.UtcNow
        });
        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            CompanyId = buyerCompany.Id,
            CurrencyCode = city.CurrencyCode,
            Balance = 1_000_000m,
            AccountNumber = GenerateTestAccountNumber(),
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        return new SeededSupplyContext(sellerCompany.Id, buyerCompany.Id, sellerUnit.Id, resource.Id);
    }

    private static string GenerateTestAccountNumber()
    {
        const long max = 1_000_000_000_000_000L;
        var value = DateTime.UtcNow.Ticks % max;
        return value.ToString("D16");
    }

    private static async Task ProcessTickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();
    }

    private sealed record SeededSupplyContext(Guid SellerCompanyId, Guid BuyerCompanyId, Guid SellerB2BUnitId, Guid ResourceTypeId);
}
