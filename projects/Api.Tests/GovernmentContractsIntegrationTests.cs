using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class GovernmentContractsIntegrationTests : IClassFixture<ApiWebApplicationFactory>
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GovernmentContractsIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

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

    private async Task<string> RegisterAndGetTokenAsync(string email)
    {
        var result = await ExecuteGraphQlAsync(
            _client,
            """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """,
            new { input = new { email, displayName = "Contract Tester", password = "TestPass123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<Guid> GetMeIdAsync(HttpClient client, string token)
    {
        var result = await ExecuteGraphQlAsync(client, "{ me { id } }", token: token);
        return Guid.Parse(result.GetProperty("data").GetProperty("me").GetProperty("id").GetString()!);
    }

    private async Task<(Guid CityId, Guid ProductId, Guid CompanyId, Guid BuildingId)> SeedCompanyContractContextAsync(
        Guid playerId,
        bool createEligibleBuilding = true,
        decimal qualityLevel = 7m)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.OrderBy(c => c.Name).FirstAsync();
        var product = await db.ProductTypes.FirstAsync();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = $"Company-{Guid.NewGuid():N}"[..16],
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 0,
        };
        db.Companies.Add(company);

        Guid buildingId = Guid.Empty;
        if (createEligibleBuilding)
        {
            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Contract Factory",
                Latitude = city.Latitude,
                Longitude = city.Longitude,
                BuiltAtUtc = DateTime.UtcNow,
            };
            db.Buildings.Add(building);
            buildingId = building.Id;
        }

        var combinedQuality = Math.Clamp(qualityLevel / 10m, 0m, 1m);
        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            Name = $"{product.Name} Brand",
            Scope = BrandScope.Product,
            ProductTypeId = product.Id,
            Quality = combinedQuality,
            MarketingQuality = 0m,
            Awareness = 0m,
        });

        var player = await db.Players.FirstAsync(p => p.Id == playerId);
        player.ActiveCompanyId = company.Id;
        player.ActiveAccountType = AccountContextType.Company;

        await db.SaveChangesAsync();
        return (city.Id, product.Id, company.Id, buildingId);
    }

    private async Task<GovernmentContract> SeedContractAsync(Guid cityId, Guid productId, decimal budgetCap = 100m, decimal minimumQuality = 5m, string status = GovernmentContractStatus.Open, Guid? winnerCompanyId = null, long? deadlineTick = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gameTick = await db.GameStates.Select(g => g.CurrentTick).FirstAsync();
        var contract = new GovernmentContract
        {
            Id = Guid.NewGuid(),
            CityId = cityId,
            Title = "Test contract",
            Description = "Supply product",
            ProductTypeId = productId,
            QuantityRequired = 100m,
            MinimumQuality = minimumQuality,
            BudgetCap = budgetCap,
            DeadlineTick = deadlineTick ?? (gameTick + 5),
            Status = status,
            WinnerCompanyId = winnerCompanyId,
            CreatedAtTick = gameTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.GovernmentContracts.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }

    private async Task ProcessTickAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();
    }

    private async Task<long> GetCurrentTickAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.GameStates.Select(state => state.CurrentTick).FirstAsync();
    }

    private async Task SetCurrentTickAsync(long tick)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gameState = await db.GameStates.FirstAsync();
        gameState.CurrentTick = tick;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CityGovernmentContracts_ReturnsOnlyOpenContractsForCity()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-open-{Guid.NewGuid():N}@example.com");
        var playerId = await GetMeIdAsync(_client, token);
        var (cityId, productId, _, _) = await SeedCompanyContractContextAsync(playerId);
        await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Open);
        await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Awarded);

        var result = await ExecuteGraphQlAsync(
            _client,
            "query($cityId: UUID!) { cityGovernmentContracts(cityId: $cityId, status: \"OPEN\") { id status } }",
            new { cityId });

        var rows = result.GetProperty("data").GetProperty("cityGovernmentContracts").EnumerateArray().ToList();
        Assert.Single(rows);
        Assert.Equal("OPEN", rows[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task SubmitContractBid_HappyPath_Succeeds()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-bid-ok-{Guid.NewGuid():N}@example.com");
        var playerId = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(playerId);
        var contract = await SeedContractAsync(cityId, productId);

        var result = await ExecuteGraphQlAsync(
            _client,
            """
            mutation($input: SubmitContractBidInput!) {
              submitContractBid(input: $input) { id companyId contractId }
            }
            """,
            new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 80m, estimatedDeliveryTick = contract.DeadlineTick - 1 } },
            token);

        var bid = result.GetProperty("data").GetProperty("submitContractBid");
        Assert.Equal(companyId.ToString(), bid.GetProperty("companyId").GetString());
    }

    [Fact]
    public async Task SubmitContractBid_RejectsCompanyWithoutBuildingInCity()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-bid-city-{Guid.NewGuid():N}@example.com");
        var playerId = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(playerId, createEligibleBuilding: false);
        var contract = await SeedContractAsync(cityId, productId);

        var result = await ExecuteGraphQlAsync(
            _client,
            "mutation($input: SubmitContractBidInput!) { submitContractBid(input: $input) { id } }",
            new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 80m, estimatedDeliveryTick = contract.DeadlineTick - 1 } },
            token);

        var code = result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("MISSING_CITY_OPERATION", code);
    }

    [Fact]
    public async Task SubmitContractBid_RejectsBidAboveBudgetCap()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-bid-budget-{Guid.NewGuid():N}@example.com");
        var playerId = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(playerId);
        var contract = await SeedContractAsync(cityId, productId, budgetCap: 50m);

        var result = await ExecuteGraphQlAsync(
            _client,
            "mutation($input: SubmitContractBidInput!) { submitContractBid(input: $input) { id } }",
            new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 80m, estimatedDeliveryTick = contract.DeadlineTick - 1 } },
            token);

        var code = result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("BID_PRICE_INVALID", code);
    }

    [Fact]
    public async Task SubmitContractBid_RejectsLowQualityCompany()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-bid-quality-{Guid.NewGuid():N}@example.com");
        var playerId = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(playerId, qualityLevel: 2m);
        var contract = await SeedContractAsync(cityId, productId, minimumQuality: 5m);

        var result = await ExecuteGraphQlAsync(
            _client,
            "mutation($input: SubmitContractBidInput!) { submitContractBid(input: $input) { id } }",
            new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 40m, estimatedDeliveryTick = contract.DeadlineTick - 1 } },
            token);

        var code = result.GetProperty("errors")[0].GetProperty("extensions").GetProperty("code").GetString();
        Assert.Equal("QUALITY_TOO_LOW", code);
    }

    [Fact]
    public async Task GovernmentContractPhase_AwardsLowestCompliantBidAtDeadline()
    {
        var tokenA = await RegisterAndGetTokenAsync($"contracts-phase-a-{Guid.NewGuid():N}@example.com");
        var tokenB = await RegisterAndGetTokenAsync($"contracts-phase-b-{Guid.NewGuid():N}@example.com");
        var playerA = await GetMeIdAsync(_client, tokenA);
        var playerB = await GetMeIdAsync(_client, tokenB);
        var (cityId, productId, companyA, _) = await SeedCompanyContractContextAsync(playerA);
        var (_, _, companyB, _) = await SeedCompanyContractContextAsync(playerB);
        var currentTick = await GetCurrentTickAsync();
        var contract = await SeedContractAsync(cityId, productId, deadlineTick: currentTick + 1);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.ContractBids.Add(new ContractBid
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                CompanyId = companyA,
                BidPricePerUnit = 90m,
                EstimatedDeliveryTick = currentTick + 3,
                SubmittedAtTick = currentTick,
                SubmittedAtUtc = DateTime.UtcNow,
            });
            db.ContractBids.Add(new ContractBid
            {
                Id = Guid.NewGuid(),
                ContractId = contract.Id,
                CompanyId = companyB,
                BidPricePerUnit = 70m,
                EstimatedDeliveryTick = currentTick + 4,
                SubmittedAtTick = currentTick,
                SubmittedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await ProcessTickAsync();
        await ProcessTickAsync();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await verifyDb.GovernmentContracts.FindAsync(contract.Id);
        Assert.NotNull(saved);
        Assert.Equal(GovernmentContractStatus.Awarded, saved!.Status);
        Assert.Equal(companyB, saved.WinnerCompanyId);
    }

    [Fact]
    public async Task GovernmentContractPhase_ExpiresWhenNoCompliantBid()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-phase-expire-{Guid.NewGuid():N}@example.com");
        var player = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(player, qualityLevel: 2m);
        var contract = await SeedContractAsync(cityId, productId, minimumQuality: 9m, deadlineTick: 1);

        await ExecuteGraphQlAsync(_client, "mutation($input: SubmitContractBidInput!) { submitContractBid(input: $input) { id } }", new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 40m, estimatedDeliveryTick = 5 } }, token);
        await ProcessTickAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var saved = await db.GovernmentContracts.FindAsync(contract.Id);
        Assert.NotNull(saved);
        Assert.Equal(GovernmentContractStatus.Expired, saved!.Status);
    }

    [Fact]
    public async Task FulfillContractShipment_DeductsInventoryAndIncrementsProgress()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-fulfill-progress-{Guid.NewGuid():N}@example.com");
        var player = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, buildingId) = await SeedCompanyContractContextAsync(player);
        var contract = await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Awarded, winnerCompanyId: companyId, deadlineTick: 30);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Inventories.Add(new Inventory
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                ProductTypeId = productId,
                Quantity = 100m,
                Quality = 0.8m,
            });
            db.ContractBids.Add(new ContractBid { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, BidPricePerUnit = 40m, EstimatedDeliveryTick = 20, SubmittedAtTick = 1 });
            db.ContractFulfillments.Add(new ContractFulfillment { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, QuantityRequired = 100m, QuantityDelivered = 0m });
            await db.SaveChangesAsync();
        }

        await ExecuteGraphQlAsync(
            _client,
            "mutation($input: FulfillContractShipmentInput!) { fulfillContractShipment(input: $input) { quantityDelivered fulfillmentPercent } }",
            new { input = new { contractId = contract.Id, quantity = 40m } },
            token);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var inventory = await verifyDb.Inventories.FirstAsync(i => i.BuildingId == buildingId && i.ProductTypeId == productId);
        var fulfillment = await verifyDb.ContractFulfillments.FirstAsync(f => f.ContractId == contract.Id);
        Assert.Equal(60m, inventory.Quantity);
        Assert.Equal(40m, fulfillment.QuantityDelivered);
    }

    [Fact]
    public async Task FulfillContractShipment_CompletesAndCreditsCompany()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-fulfill-credit-{Guid.NewGuid():N}@example.com");
        var player = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, buildingId) = await SeedCompanyContractContextAsync(player);
        var contract = await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Awarded, winnerCompanyId: companyId, deadlineTick: 60);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = buildingId, ProductTypeId = productId, Quantity = 100m, Quality = 0.9m });
            db.ContractBids.Add(new ContractBid { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, BidPricePerUnit = 55m, EstimatedDeliveryTick = 50, SubmittedAtTick = 1 });
            db.ContractFulfillments.Add(new ContractFulfillment { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, QuantityRequired = 100m, QuantityDelivered = 0m });
            await db.SaveChangesAsync();
        }

        await ExecuteGraphQlAsync(_client, "mutation($input: FulfillContractShipmentInput!) { fulfillContractShipment(input: $input) { status settledRevenue } }", new { input = new { contractId = contract.Id, quantity = 100m } }, token);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var updated = await verifyDb.GovernmentContracts.FindAsync(contract.Id);
        var account = await verifyDb.BankAccounts.FirstOrDefaultAsync(account => account.CompanyId == companyId);
        Assert.NotNull(updated);
        Assert.Equal(GovernmentContractStatus.Fulfilled, updated!.Status);
        Assert.NotNull(account);
        Assert.True(account!.Balance >= 5_500m);
    }

    [Fact]
    public async Task FulfillContractShipment_AppliesLatePenalty()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-fulfill-late-{Guid.NewGuid():N}@example.com");
        var player = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, buildingId) = await SeedCompanyContractContextAsync(player);
        await SetCurrentTickAsync(50);
        var contract = await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Awarded, winnerCompanyId: companyId, deadlineTick: 10);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = buildingId, ProductTypeId = productId, Quantity = 100m, Quality = 0.8m });
            db.ContractBids.Add(new ContractBid { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, BidPricePerUnit = 100m, EstimatedDeliveryTick = 2, SubmittedAtTick = 1 });
            db.ContractFulfillments.Add(new ContractFulfillment { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, QuantityRequired = 100m, QuantityDelivered = 0m });
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(_client, "mutation($input: FulfillContractShipmentInput!) { fulfillContractShipment(input: $input) { settledRevenue latePenaltyApplied } }", new { input = new { contractId = contract.Id, quantity = 100m } }, token);
        var payload = result.GetProperty("data").GetProperty("fulfillContractShipment");
        Assert.True(payload.GetProperty("latePenaltyApplied").GetBoolean());
        Assert.Equal(9000m, payload.GetProperty("settledRevenue").GetDecimal());
    }

    [Fact]
    public async Task GovernmentContractRevenue_LedgerEntryCreatedOnFulfillment()
    {
        var token = await RegisterAndGetTokenAsync($"contracts-ledger-{Guid.NewGuid():N}@example.com");
        var player = await GetMeIdAsync(_client, token);
        var (cityId, productId, companyId, buildingId) = await SeedCompanyContractContextAsync(player);
        var contract = await SeedContractAsync(cityId, productId, status: GovernmentContractStatus.Awarded, winnerCompanyId: companyId, deadlineTick: 100);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Inventories.Add(new Inventory { Id = Guid.NewGuid(), BuildingId = buildingId, ProductTypeId = productId, Quantity = 100m, Quality = 0.8m });
            db.ContractBids.Add(new ContractBid { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, BidPricePerUnit = 70m, EstimatedDeliveryTick = 10, SubmittedAtTick = 1 });
            db.ContractFulfillments.Add(new ContractFulfillment { Id = Guid.NewGuid(), ContractId = contract.Id, CompanyId = companyId, QuantityRequired = 100m, QuantityDelivered = 0m });
            await db.SaveChangesAsync();
        }

        await ExecuteGraphQlAsync(_client, "mutation($input: FulfillContractShipmentInput!) { fulfillContractShipment(input: $input) { status } }", new { input = new { contractId = contract.Id, quantity = 100m } }, token);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ledger = await verifyDb.LedgerEntries.Where(entry => entry.CompanyId == companyId).OrderByDescending(entry => entry.RecordedAtUtc).FirstOrDefaultAsync();
        Assert.NotNull(ledger);
        Assert.Equal(LedgerCategory.GovernmentContractRevenue, ledger!.Category);
    }

    [Fact]
    public async Task GovernmentContractPhase_SeedsReplacementContractAfterAwardOrExpiry()
    {
        var tokenA = await RegisterAndGetTokenAsync($"contracts-seed-a-{Guid.NewGuid():N}@example.com");
        var playerA = await GetMeIdAsync(_client, tokenA);
        var (cityId, productId, companyId, _) = await SeedCompanyContractContextAsync(playerA);
        var currentTick = await GetCurrentTickAsync();
        var contract = await SeedContractAsync(cityId, productId, deadlineTick: currentTick + 1);
        await ExecuteGraphQlAsync(_client, "mutation($input: SubmitContractBidInput!) { submitContractBid(input: $input) { id } }", new { input = new { contractId = contract.Id, companyId, bidPricePerUnit = 50m, estimatedDeliveryTick = 10 } }, tokenA);

        await ProcessTickAsync();
        await ProcessTickAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var openContracts = await db.GovernmentContracts.Where(item => item.CityId == cityId && item.Status == GovernmentContractStatus.Open).ToListAsync();
        Assert.NotEmpty(openContracts);
    }
}
