using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class NpcCompanyIntegrationTests
{
    private static async Task<(NpcCompany Npc, Company Company, City City)> CreateControlledNpcAsync(
        AppDbContext db,
        string archetype,
        decimal startingBalance = 500_000m,
        City? homeCityOverride = null)
    {
        var city = homeCityOverride ?? await db.Cities.AsNoTracking().FirstAsync();
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"npc-test-{Guid.NewGuid():N}@test.local",
            DisplayName = "NPC Test",
            PasswordHash = "mock",
            Role = PlayerRole.Player,
            ActiveAccountType = AccountContextType.Company,
            CreatedAtUtc = DateTime.UtcNow,
            OnboardingCompletedAtUtc = DateTime.UtcNow,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = $"NPC {archetype}",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = 1,
        };
        db.Companies.Add(company);
        player.ActiveCompanyId = company.Id;

        var account = await BuildingBankAccountProvisioning.EnsureCompanyCurrencyAccountAsync(db, company.Id, city.CurrencyCode);
        account.Balance += startingBalance;

        var npc = new NpcCompany
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            HomeCityId = city.Id,
            Name = company.Name,
            Archetype = archetype,
            DifficultyLevel = 2,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.NpcCompanies.Add(npc);
        await db.SaveChangesAsync();
        return (npc, company, city);
    }

    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        }

        var response = await client.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(content);
    }

    private static async Task<string> LoginAsSeedAdminAsync(HttpClient client)
    {
        var login = await ExecuteGraphQlAsync(
            client,
            """
            mutation Login($input: LoginInput!) {
              login(input: $input) { token }
            }
            """,
            new { input = new { email = "admin@capitalism.local", password = ApiWebApplicationFactory.TestSeedAdminPassword } });

        return login.GetProperty("data").GetProperty("login").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Seeder_CreatesAtLeastThreeNpcCompaniesPerCity()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cityIds = await db.Cities.AsNoTracking().Select(city => city.Id).ToListAsync();
        var countsByCity = await db.NpcCompanies
            .AsNoTracking()
            .GroupBy(npc => npc.HomeCityId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Key, item => item.Count);

        Assert.NotEmpty(cityIds);
        foreach (var cityId in cityIds)
        {
            Assert.True(countsByCity.GetValueOrDefault(cityId) >= 3, $"Expected at least 3 NPC companies in city {cityId}");
        }
    }

    [Fact]
    public async Task NpcCompanies_QueryReturnsFilteredCityRows()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.AsNoTracking().FirstOrDefaultAsync();
        Assert.NotNull(city);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query NpcCompanies($cityId: UUID) {
              npcCompanies(cityId: $cityId) { id homeCityId name archetype isActive }
            }
            """,
            new { cityId = city!.Id });

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");
        var entries = result.GetProperty("data").GetProperty("npcCompanies");
        Assert.True(entries.GetArrayLength() >= 3);
        foreach (var entry in entries.EnumerateArray())
        {
            Assert.Equal(city.Id.ToString(), entry.GetProperty("homeCityId").GetString());
        }
    }

    [Fact]
    public async Task Admin_PauseAndResumeNpcCompany_UpdatesActiveState()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await LoginAsSeedAdminAsync(client);

        var listing = await ExecuteGraphQlAsync(client, "{ npcCompanies { id isActive } }");
        var npcId = listing.GetProperty("data").GetProperty("npcCompanies")[0].GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(npcId));

        var paused = await ExecuteGraphQlAsync(
            client,
            """
            mutation PauseNpc($input: ManageNpcCompanyActivityInput!) {
              pauseNpcCompany(input: $input) { id isActive }
            }
            """,
            new { input = new { npcCompanyId = npcId } },
            token);
        Assert.False(paused.GetProperty("data").GetProperty("pauseNpcCompany").GetProperty("isActive").GetBoolean());

        var resumed = await ExecuteGraphQlAsync(
            client,
            """
            mutation ResumeNpc($input: ManageNpcCompanyActivityInput!) {
              resumeNpcCompany(input: $input) { id isActive }
            }
            """,
            new { input = new { npcCompanyId = npcId } },
            token);
        Assert.True(resumed.GetProperty("data").GetProperty("resumeNpcCompany").GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task NpcDecisionLogs_AdminQuery_ReturnsEntries()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await LoginAsSeedAdminAsync(client);

        var result = await ExecuteGraphQlAsync(
            client,
            """
            {
              npcDecisionLogs(limit: 20) {
                id
                npcCompanyName
                actionType
                tick
              }
            }
            """,
            token: token);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");
        var logs = result.GetProperty("data").GetProperty("npcDecisionLogs");
        Assert.True(logs.GetArrayLength() > 0);
    }

    [Fact]
    public async Task TickProcessor_WithNpcCompanies_RunsWithoutThrowing()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var beforeCount = await db.NpcDecisionLogs.CountAsync();
        _ = await tickProcessor.ProcessTickAsync();
        var afterCount = await db.NpcDecisionLogs.CountAsync();

        Assert.True(afterCount >= beforeCount);
    }

    [Fact]
    public async Task MarketOverview_IncludesTopCompetitorFields()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.AsNoTracking().FirstAsync();
        var product = await db.ProductTypes.AsNoTracking().FirstAsync();
        var npc = await db.NpcCompanies.AsNoTracking().FirstAsync();
        var humanCompany = await db.Companies.AsNoTracking().FirstAsync(company => company.Id != npc.CompanyId);
        var tick = await db.GameStates.AsNoTracking().Select(state => state.CurrentTick).FirstOrDefaultDeterministicAsync();

        db.PublicSalesRecords.AddRange(
            new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = npc.CompanyId,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = tick,
                QuantitySold = 100m,
                PricePerUnit = 20m,
                Revenue = 2_000m,
                Demand = 110m,
                SalesCapacity = 120m,
            },
            new PublicSalesRecord
            {
                Id = Guid.NewGuid(),
                BuildingUnitId = Guid.NewGuid(),
                BuildingId = Guid.NewGuid(),
                CompanyId = humanCompany.Id,
                CityId = city.Id,
                ProductTypeId = product.Id,
                Tick = tick,
                QuantitySold = 20m,
                PricePerUnit = 20m,
                Revenue = 400m,
                Demand = 30m,
                SalesCapacity = 50m,
            });
        await db.SaveChangesAsync();

        var result = await ExecuteGraphQlAsync(
            client,
            """
            {
              marketOverview(topN: 5, lastNTicks: 100) {
                cityId
                products {
                  productTypeId
                  topCompetitorCompanyName
                  topCompetitorMarketSharePercent
                }
              }
            }
            """);

        Assert.False(result.TryGetProperty("errors", out _), "Expected no GraphQL errors.");
        var cities = result.GetProperty("data").GetProperty("marketOverview");
        Assert.True(cities.GetArrayLength() > 0);
        var firstCityProducts = cities.EnumerateArray()
            .First(cityRow => cityRow.GetProperty("cityId").GetString() == city.Id.ToString())
            .GetProperty("products");
        Assert.True(firstCityProducts.GetArrayLength() > 0);
        var matchingProduct = firstCityProducts.EnumerateArray()
            .First(productRow => productRow.GetProperty("productTypeId").GetString() == product.Id.ToString());
        Assert.False(string.IsNullOrWhiteSpace(matchingProduct.GetProperty("topCompetitorCompanyName").GetString()));
        Assert.True(matchingProduct.GetProperty("topCompetitorMarketSharePercent").GetDecimal() > 0m);
    }

    [Fact]
    public async Task NpcPhase_RawMaterialsArchetype_PrefersMineExpansion()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "Npc Mine Test City",
            CountryCode = "TS",
            CurrencyCode = "EUR",
            Latitude = 48.3,
            Longitude = 17.3,
            Population = 50_000,
            AverageRentPerSqm = 20m,
            BaseSalaryPerManhour = 10m,
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        var (npc, company, _) = await CreateControlledNpcAsync(db, NpcArchetype.RawMaterials, 120_000_000m, city);
        db.BuildingLots.Add(new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "NPC Mine Lot",
            Description = "Test mine lot",
            District = "Industrial Zone",
            Latitude = city.Latitude + 0.001,
            Longitude = city.Longitude + 0.001,
            PopulationIndex = 1m,
            BasePrice = 20_000m,
            Price = 20_000m,
            SuitableTypes = "MINE",
            ResourceTypeId = (await db.ResourceTypes.AsNoTracking().FirstAsync()).Id,
            MaterialQuality = 0.7m,
            MaterialQuantity = 10_000m,
            OriginalMaterialQuantity = 10_000m,
        });
        await db.SaveChangesAsync();

        _ = await tickProcessor.ProcessTickAsync();

        var hasMine = await db.Buildings.AnyAsync(building => building.CompanyId == company.Id && building.Type == BuildingType.Mine);
        var debugLogs = await db.NpcDecisionLogs
            .Where(log => log.NpcCompanyId == npc.Id)
            .OrderByDescending(log => log.CreatedAtUtc)
            .Take(5)
            .Select(log => $"{log.ActionType}:{log.Outcome}")
            .ToListAsync();
        Assert.True(hasMine, $"Expected mine expansion. Logs: {string.Join(" | ", debugLogs)}");
        var decision = await db.NpcDecisionLogs
            .Where(log => log.NpcCompanyId == npc.Id && log.ActionType == "EXPAND")
            .OrderByDescending(log => log.CreatedAtUtc)
            .FirstOrDefaultAsync();
        Assert.NotNull(decision);
    }

    [Fact]
    public async Task NpcPhase_ManufacturerArchetype_PrefersFactoryExpansion()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();

        var city = new City
        {
            Id = Guid.NewGuid(),
            Name = "Npc Factory Test City",
            CountryCode = "TS",
            CurrencyCode = "EUR",
            Latitude = 48.31,
            Longitude = 17.31,
            Population = 50_000,
            AverageRentPerSqm = 20m,
            BaseSalaryPerManhour = 10m,
        };
        db.Cities.Add(city);
        await db.SaveChangesAsync();

        var (_, company, _) = await CreateControlledNpcAsync(db, NpcArchetype.Manufacturer, 120_000_000m, city);
        db.BuildingLots.Add(new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "NPC Factory Lot",
            Description = "Test factory lot",
            District = "Industrial Zone",
            Latitude = city.Latitude + 0.001,
            Longitude = city.Longitude + 0.001,
            PopulationIndex = 1m,
            BasePrice = 20_000m,
            Price = 20_000m,
            SuitableTypes = "FACTORY,SALES_SHOP",
        });
        await db.SaveChangesAsync();

        _ = await tickProcessor.ProcessTickAsync();

        var hasFactory = await db.Buildings.AnyAsync(building => building.CompanyId == company.Id && building.Type == BuildingType.Factory);
        Assert.True(hasFactory);
    }

    [Fact]
    public async Task NpcPhase_ExpansionDebitsNpcBankBalance()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();

        var (_, company, city) = await CreateControlledNpcAsync(db, NpcArchetype.Retailer, 600_000m);
        db.BuildingLots.Add(new BuildingLot
        {
            Id = Guid.NewGuid(),
            CityId = city.Id,
            Name = "NPC Retail Lot",
            Description = "Test retail lot",
            District = "Retail District",
            Latitude = city.Latitude + 0.002,
            Longitude = city.Longitude + 0.002,
            PopulationIndex = 1.4m,
            BasePrice = 50_000m,
            Price = 50_000m,
            SuitableTypes = "SALES_SHOP,COMMERCIAL",
        });
        await db.SaveChangesAsync();

        var beforeBalance = await db.BankAccounts.Where(account => account.CompanyId == company.Id && account.ClosedAtUtc == null).SumAsync(account => account.Balance);
        _ = await tickProcessor.ProcessTickAsync();
        var afterBalance = await db.BankAccounts.Where(account => account.CompanyId == company.Id && account.ClosedAtUtc == null).SumAsync(account => account.Balance);

        Assert.True(afterBalance < beforeBalance);
    }

    [Fact]
    public async Task NpcPhase_DoesNotExceedPerCompanyBuildingLimit()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tickProcessor = scope.ServiceProvider.GetRequiredService<TickProcessor>();

        var (_, company, city) = await CreateControlledNpcAsync(db, NpcArchetype.Conglomerate, 900_000m);
        var existingLots = await db.BuildingLots
            .Where(lot => lot.CityId == city.Id && lot.OwnerCompanyId == null)
            .OrderBy(lot => lot.Price)
            .Take(20)
            .ToListAsync();
        foreach (var lot in existingLots)
        {
            lot.SuitableTypes = "MINE,FACTORY,SALES_SHOP";
        }

        for (var i = 0; i < 10; i++)
        {
            _ = await tickProcessor.ProcessTickAsync();
        }

        var buildingCount = await db.Buildings.CountAsync(building => building.CompanyId == company.Id && building.DestroyedAtUtc == null);
        Assert.True(buildingCount <= 6);
    }
}
