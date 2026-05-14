using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Integration tests for the global exchange buy/sell mutations:
/// <c>buyFromExchange</c> and <c>sellToExchange</c>.
/// </summary>
public sealed class GlobalExchangeMutationTests
{
    // ── Shared helpers ─────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8, "application/json");

        if (token is not null)
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string email)
    {
        var result = await ExecuteGraphQlAsync(client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName = "ExchangeUser", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetPlayerIdAsync(AppDbContext db, string email)
        => await db.Players.Where(p => p.Email == email).Select(p => p.Id).FirstAsync();

    private static BankAccount SeedCompanyBankAccount(AppDbContext db, Guid companyId, string currencyCode, decimal balance)
    {
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = (Math.Abs(Guid.NewGuid().GetHashCode()) % 1_000_000_000_000_000L).ToString("D16"),
            CurrencyCode = currencyCode,
            CompanyId = companyId,
            Balance = balance,
            CreatedAtUtc = DateTime.UtcNow,
            IsGovernmentAccount = false,
        };
        db.BankAccounts.Add(account);
        return account;
    }

    private static Company SeedCompany(AppDbContext db, Guid playerId, string name = "TestCo")
    {
        var company = new Company { Id = Guid.NewGuid(), PlayerId = playerId, Name = name, Cash = 0m };
        db.Companies.Add(company);
        return company;
    }

    private static Building SeedBuilding(AppDbContext db, Guid companyId, Guid cityId)
    {
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            CityId = cityId,
            Name = "Test Building",
            Type = "FACTORY",
            Level = 1,
            Latitude = 48.15,
            Longitude = 17.11,
        };
        db.Buildings.Add(building);
        return building;
    }

    private static BuildingUnit SeedBuildingUnit(AppDbContext db, Guid buildingId, string unitType = "STORAGE")
    {
        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = buildingId,
            UnitType = unitType,
            GridX = 0,
            GridY = 0,
            Level = 1,
        };
        db.BuildingUnits.Add(unit);
        return unit;
    }

    // ── BuyFromExchange tests ──────────────────────────────────────────────────

    [Fact]
    public async Task BuyFromExchange_ValidInput_PurchasesResourcesAndDebitsAccount()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "buy-exchange@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "buy-exchange@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var storageUnit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var bankAccount = SeedCompanyBankAccount(db, company.Id, "EUR", 100_000m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) {
                    success
                    errorCode
                    errorMessage
                    resourceName
                    quantityPurchased
                    exchangePricePerUnit
                    transitCostPerUnit
                    deliveredPricePerUnit
                    totalCost
                    qualityDelivered
                    currencyCode
                    newBankBalance
                }
            }
            """,
            new
            {
                input = new
                {
                    sourceCityId = bratislava.Id,
                    resourceTypeId = wood.Id,
                    quantity = 10m,
                    targetBuildingUnitId = storageUnit.Id,
                    bankAccountId = bankAccount.Id,
                }
            },
            token);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.True(payload.GetProperty("success").GetBoolean(), payload.GetProperty("errorMessage").GetString());
        Assert.Equal("Wood", payload.GetProperty("resourceName").GetString());
        Assert.Equal(10m, payload.GetProperty("quantityPurchased").GetDecimal());
        Assert.Equal("EUR", payload.GetProperty("currencyCode").GetString());
        Assert.True(payload.GetProperty("totalCost").GetDecimal() > 0m);
        Assert.True(payload.GetProperty("newBankBalance").GetDecimal() < 100_000m);
        Assert.True(payload.GetProperty("qualityDelivered").GetDecimal() is > 0m and <= 1m);

        // Verify inventory was created
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventory = await verifyDb.Inventories
            .FirstOrDefaultAsync(i => i.BuildingUnitId == storageUnit.Id && i.ResourceTypeId == wood.Id);
        Assert.NotNull(inventory);
        Assert.Equal(10m, inventory.Quantity);

        // Verify ledger entry was recorded
        var ledgerEntry = await verifyDb.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.PurchasingCost)
            .FirstOrDefaultAsync();
        Assert.NotNull(ledgerEntry);
        Assert.True(ledgerEntry.Amount < 0m);
        Assert.Equal(wood.Id, ledgerEntry.ResourceTypeId);
    }

    [Fact]
    public async Task BuyFromExchange_CrossCity_IncludesTransitCost()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "buy-transit@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "buy-transit@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var prague = await db.Cities.FirstAsync(c => c.Name == "Prague");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        // Building is in Prague, but buying from Bratislava exchange → transit cost applies
        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, prague.Id);
        var storageUnit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var bankAccount = SeedCompanyBankAccount(db, company.Id, "CZK", 5_000_000m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) {
                    success errorCode transitCostPerUnit deliveredPricePerUnit exchangePricePerUnit
                }
            }
            """,
            new
            {
                input = new
                {
                    sourceCityId = bratislava.Id,
                    resourceTypeId = wood.Id,
                    quantity = 5m,
                    targetBuildingUnitId = storageUnit.Id,
                    bankAccountId = bankAccount.Id,
                }
            },
            token);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.True(payload.GetProperty("success").GetBoolean(), payload.GetProperty("errorCode").GetString());
        // Cross-city purchase must have a positive transit cost
        Assert.True(payload.GetProperty("transitCostPerUnit").GetDecimal() > 0m, "Transit cost must be positive for cross-city purchase");
        // Delivered = exchange + transit
        var exchange = payload.GetProperty("exchangePricePerUnit").GetDecimal();
        var transit = payload.GetProperty("transitCostPerUnit").GetDecimal();
        var delivered = payload.GetProperty("deliveredPricePerUnit").GetDecimal();
        Assert.Equal(exchange + transit, delivered);
    }

    [Fact]
    public async Task BuyFromExchange_InsufficientFunds_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "buy-insuf@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "buy-insuf@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var storageUnit = SeedBuildingUnit(db, building.Id, "STORAGE");
        // Intentionally tiny balance
        var bankAccount = SeedCompanyBankAccount(db, company.Id, "EUR", 0.01m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) { success errorCode errorMessage }
            }
            """,
            new
            {
                input = new
                {
                    sourceCityId = bratislava.Id,
                    resourceTypeId = wood.Id,
                    quantity = 100m,
                    targetBuildingUnitId = storageUnit.Id,
                    bankAccountId = bankAccount.Id,
                }
            },
            token);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("INSUFFICIENT_FUNDS", payload.GetProperty("errorCode").GetString());
        Assert.DoesNotContain("0.01", payload.GetProperty("errorMessage").GetString());
        Assert.DoesNotContain("Required:", payload.GetProperty("errorMessage").GetString());
    }

    [Fact]
    public async Task BuyFromExchange_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) { success errorCode }
            }
            """,
            new
            {
                input = new
                {
                    sourceCityId = Guid.NewGuid(),
                    resourceTypeId = Guid.NewGuid(),
                    quantity = 10m,
                    targetBuildingUnitId = Guid.NewGuid(),
                    bankAccountId = Guid.NewGuid(),
                }
            });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task BuyFromExchange_InvalidQuantity_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "buy-qty0@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "buy-qty0@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var unit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var account = SeedCompanyBankAccount(db, company.Id, "EUR", 50_000m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) { success errorCode }
            }
            """,
            new { input = new { sourceCityId = bratislava.Id, resourceTypeId = wood.Id, quantity = -5m, targetBuildingUnitId = unit.Id, bankAccountId = account.Id } },
            token);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("INVALID_QUANTITY", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task BuyFromExchange_WrongCompanyBankAccount_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "buy-wrongacct@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "buy-wrongacct@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        // Two companies: building owned by company A, bank account belongs to company B
        var companyA = SeedCompany(db, playerId, "Company A");
        var companyB = SeedCompany(db, playerId, "Company B");
        var building = SeedBuilding(db, companyA.Id, bratislava.Id);
        var unit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var accountB = SeedCompanyBankAccount(db, companyB.Id, "EUR", 50_000m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) { success errorCode }
            }
            """,
            new { input = new { sourceCityId = bratislava.Id, resourceTypeId = wood.Id, quantity = 5m, targetBuildingUnitId = unit.Id, bankAccountId = accountB.Id } },
            token);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task BuyFromExchange_ForeignBuildingUnit_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerEmail = $"buy-foreign-owner-{Guid.NewGuid():N}@test.com";
        var probeEmail = $"buy-foreign-probe-{Guid.NewGuid():N}@test.com";
        var ownerToken = await RegisterAndGetTokenAsync(client, ownerEmail);
        var probeToken = await RegisterAndGetTokenAsync(client, probeEmail);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await GetPlayerIdAsync(db, ownerEmail);
        var probeId = await GetPlayerIdAsync(db, probeEmail);

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var ownerCompany = SeedCompany(db, ownerId, "Owner Company");
        var ownerBuilding = SeedBuilding(db, ownerCompany.Id, bratislava.Id);
        var foreignUnit = SeedBuildingUnit(db, ownerBuilding.Id, "STORAGE");

        var probeCompany = SeedCompany(db, probeId, "Probe Company");
        var probeAccount = SeedCompanyBankAccount(db, probeCompany.Id, "EUR", 50_000m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation BuyFromExchange($input: BuyFromExchangeInput!) {
                buyFromExchange(input: $input) { success errorCode errorMessage }
            }
            """,
            new
            {
                input = new
                {
                    sourceCityId = bratislava.Id,
                    resourceTypeId = wood.Id,
                    quantity = 5m,
                    targetBuildingUnitId = foreignUnit.Id,
                    bankAccountId = probeAccount.Id,
                }
            },
            probeToken);

        var payload = result.GetProperty("data").GetProperty("buyFromExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", payload.GetProperty("errorCode").GetString());
    }

    // ── SellToExchange tests ───────────────────────────────────────────────────

    [Fact]
    public async Task SellToExchange_ValidInput_CreditsAccountAndRemovesInventory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "sell-exchange@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "sell-exchange@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var storageUnit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var bankAccount = SeedCompanyBankAccount(db, company.Id, "EUR", 0m);

        // Seed inventory
        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingUnitId = storageUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = 20m,
            Quality = 0.7m,
            SourcingCostTotal = 100m,
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation SellToExchange($input: SellToExchangeInput!) {
                sellToExchange(input: $input) {
                    success
                    errorCode
                    errorMessage
                    resourceName
                    quantitySold
                    exchangePricePerUnit
                    totalProceeds
                    currencyCode
                    newBankBalance
                }
            }
            """,
            new
            {
                input = new
                {
                    sourceBuildingUnitId = storageUnit.Id,
                    resourceTypeId = wood.Id,
                    quantity = 10m,
                    bankAccountId = bankAccount.Id,
                }
            },
            token);

        var payload = result.GetProperty("data").GetProperty("sellToExchange");
        Assert.True(payload.GetProperty("success").GetBoolean(), payload.GetProperty("errorMessage").GetString());
        Assert.Equal("Wood", payload.GetProperty("resourceName").GetString());
        Assert.Equal(10m, payload.GetProperty("quantitySold").GetDecimal());
        Assert.Equal("EUR", payload.GetProperty("currencyCode").GetString());
        Assert.True(payload.GetProperty("totalProceeds").GetDecimal() > 0m);
        Assert.True(payload.GetProperty("newBankBalance").GetDecimal() > 0m);

        // Verify inventory was reduced (not removed since 20 - 10 = 10 remain)
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventory = await verifyDb.Inventories
            .FirstOrDefaultAsync(i => i.BuildingUnitId == storageUnit.Id && i.ResourceTypeId == wood.Id);
        Assert.NotNull(inventory);
        Assert.Equal(10m, inventory.Quantity);

        // Verify revenue ledger entry
        var ledger = await verifyDb.LedgerEntries
            .Where(e => e.CompanyId == company.Id && e.Category == LedgerCategory.Revenue)
            .FirstOrDefaultAsync();
        Assert.NotNull(ledger);
        Assert.True(ledger.Amount > 0m);
    }

    [Fact]
    public async Task SellToExchange_InsufficientInventory_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "sell-insuf@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "sell-insuf@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var unit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var account = SeedCompanyBankAccount(db, company.Id, "EUR", 0m);

        // Seed only 5 units, try to sell 50
        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(), BuildingId = building.Id, BuildingUnitId = unit.Id,
            ResourceTypeId = wood.Id, Quantity = 5m, Quality = 0.5m,
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation SellToExchange($input: SellToExchangeInput!) {
                sellToExchange(input: $input) { success errorCode }
            }
            """,
            new { input = new { sourceBuildingUnitId = unit.Id, resourceTypeId = wood.Id, quantity = 50m, bankAccountId = account.Id } },
            token);

        var payload = result.GetProperty("data").GetProperty("sellToExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("INSUFFICIENT_INVENTORY", payload.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task SellToExchange_Unauthenticated_ReturnsAuthError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation SellToExchange($input: SellToExchangeInput!) {
                sellToExchange(input: $input) { success errorCode }
            }
            """,
            new
            {
                input = new
                {
                    sourceBuildingUnitId = Guid.NewGuid(),
                    resourceTypeId = Guid.NewGuid(),
                    quantity = 10m,
                    bankAccountId = Guid.NewGuid(),
                }
            });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.True(errors.GetArrayLength() > 0);
    }

    [Fact]
    public async Task SellToExchange_FullSell_RemovesInventoryRow()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "sell-full@test.com");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = await GetPlayerIdAsync(db, "sell-full@test.com");

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var company = SeedCompany(db, playerId);
        var building = SeedBuilding(db, company.Id, bratislava.Id);
        var unit = SeedBuildingUnit(db, building.Id, "STORAGE");
        var account = SeedCompanyBankAccount(db, company.Id, "EUR", 0m);
        var inventoryId = Guid.NewGuid();
        db.Inventories.Add(new Inventory
        {
            Id = inventoryId, BuildingId = building.Id, BuildingUnitId = unit.Id,
            ResourceTypeId = wood.Id, Quantity = 15m, Quality = 0.6m,
        });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation SellToExchange($input: SellToExchangeInput!) {
                sellToExchange(input: $input) { success totalProceeds }
            }
            """,
            new { input = new { sourceBuildingUnitId = unit.Id, resourceTypeId = wood.Id, quantity = 15m, bankAccountId = account.Id } },
            token);

        Assert.True(result.GetProperty("data").GetProperty("sellToExchange").GetProperty("success").GetBoolean());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventory = await verifyDb.Inventories.FindAsync(inventoryId);
        Assert.Null(inventory); // row must be deleted
    }

    [Fact]
    public async Task SellToExchange_ForeignBuildingUnit_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        var ownerEmail = $"sell-foreign-owner-{Guid.NewGuid():N}@test.com";
        var probeEmail = $"sell-foreign-probe-{Guid.NewGuid():N}@test.com";
        var ownerToken = await RegisterAndGetTokenAsync(client, ownerEmail);
        var probeToken = await RegisterAndGetTokenAsync(client, probeEmail);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = await GetPlayerIdAsync(db, ownerEmail);
        var probeId = await GetPlayerIdAsync(db, probeEmail);

        var bratislava = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

        var ownerCompany = SeedCompany(db, ownerId, "Owner Sell Company");
        var ownerBuilding = SeedBuilding(db, ownerCompany.Id, bratislava.Id);
        var foreignUnit = SeedBuildingUnit(db, ownerBuilding.Id, "STORAGE");
        db.Inventories.Add(new Inventory
        {
            Id = Guid.NewGuid(),
            BuildingId = ownerBuilding.Id,
            BuildingUnitId = foreignUnit.Id,
            ResourceTypeId = wood.Id,
            Quantity = 10m,
            Quality = 0.7m,
        });

        var probeCompany = SeedCompany(db, probeId, "Probe Sell Company");
        var probeAccount = SeedCompanyBankAccount(db, probeCompany.Id, "EUR", 0m);
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(client,
            """
            mutation SellToExchange($input: SellToExchangeInput!) {
                sellToExchange(input: $input) { success errorCode errorMessage }
            }
            """,
            new
            {
                input = new
                {
                    sourceBuildingUnitId = foreignUnit.Id,
                    resourceTypeId = wood.Id,
                    quantity = 5m,
                    bankAccountId = probeAccount.Id,
                }
            },
            probeToken);

        var payload = result.GetProperty("data").GetProperty("sellToExchange");
        Assert.False(payload.GetProperty("success").GetBoolean());
        Assert.Equal("FORBIDDEN", payload.GetProperty("errorCode").GetString());
    }
}
