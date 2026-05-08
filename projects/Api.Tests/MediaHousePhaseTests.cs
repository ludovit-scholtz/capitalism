using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public sealed class MediaHousePhaseTests
{
    private const string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            player { id }
          }
        }
        """;

    private static Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return Task.FromResult(new TickProcessor(db, phases, new NullLogger<TickProcessor>()));
    }

    private static async Task<(Player player, Company mediaCompany, Company targetCompany, Building mediaBuilding, BankAccount fundingAccount)> SeedMediaHouseScenarioAsync(
        AppDbContext db,
        decimal initialBalance = 10_000m,
        bool underConstruction = false,
        int buildingLevel = 1)
    {
        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()
            ?? throw new InvalidOperationException("Game state missing in test database.");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"media-player-{Guid.NewGuid():N}@test.com",
            DisplayName = "Media Player",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var mediaCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Media Company",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState.CurrentTick,
        };
        var targetCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Target Company",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState.CurrentTick,
        };
        db.Companies.AddRange(mediaCompany, targetCompany);

        var fundingAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "9001000000000001",
            CurrencyCode = city.CurrencyCode,
            Balance = initialBalance,
            CompanyId = mediaCompany.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(fundingAccount);

        var mediaBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = mediaCompany.Id,
            CityId = city.Id,
            Type = BuildingType.MediaHouse,
            Name = "Ad Tower",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = buildingLevel,
            MediaType = MediaType.Newspaper,
            BankAccountId = fundingAccount.Id,
            IsUnderConstruction = underConstruction,
            ConstructionCompletesAtTick = underConstruction ? gameState.CurrentTick + 5 : null,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(mediaBuilding);

        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = targetCompany.Id,
            Name = "Target Brand",
            Scope = BrandScope.Company,
            Awareness = 0.1m,
            Quality = 0.1m,
            MarketingQuality = 0.2m,
        });

        await db.SaveChangesAsync();
        return (player, mediaCompany, targetCompany, mediaBuilding, fundingAccount);
    }

    [Fact]
    public async Task ConstructionPhase_ChargesOnlyLaborAndEnergy_WithoutBoost()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, targetCompany, mediaBuilding, fundingAccount) =
            await SeedMediaHouseScenarioAsync(db, underConstruction: true);

        db.MediaHouseUnits.Add(new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Newspaper,
            CampaignBudgetPerTick = 500m,
            BrandQualityBoostPerTick = 0.5m,
            IsActive = true,
            LaborCostPerTick = 40m,
            EnergyCostPerTick = 20m,
        });
        await db.SaveChangesAsync();

        var originalBrandQuality = (await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id)).MarketingQuality;
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(fundingAccount).ReloadAsync();
        var brand = await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id);
        Assert.Equal(10_000m - 60m, fundingAccount.Balance);
        Assert.InRange(brand.MarketingQuality, originalBrandQuality - 0.001m, originalBrandQuality + 0.001m);
        Assert.Empty(await db.BrandQualityRecords.Where(r => r.BuildingId == mediaBuilding.Id).ToListAsync());
    }

    [Fact]
    public async Task ActiveCampaign_AppliesBoostAndCreatesLedgerExpense()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, targetCompany, mediaBuilding, fundingAccount) =
            await SeedMediaHouseScenarioAsync(db, underConstruction: false);

        db.MediaHouseUnits.Add(new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Newspaper,
            CampaignBudgetPerTick = 200m,
            BrandQualityBoostPerTick = 0.2m,
            IsActive = true,
            LaborCostPerTick = 40m,
            EnergyCostPerTick = 20m,
        });
        await db.SaveChangesAsync();

        var originalBrandQuality = (await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id)).MarketingQuality;
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(fundingAccount).ReloadAsync();
        var brand = await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id);
        Assert.Equal(10_000m - 260m, fundingAccount.Balance);
        Assert.True(brand.MarketingQuality > originalBrandQuality, "Active media campaign should increase brand marketing quality.");

        var expense = await db.LedgerEntries.FirstOrDefaultAsync(e => e.Category == LedgerCategory.MediaHouseExpense && e.BuildingId == mediaBuilding.Id);
        Assert.NotNull(expense);
        Assert.Equal(-260m, expense!.Amount);
        Assert.NotEmpty(await db.BrandQualityRecords.Where(r => r.BuildingId == mediaBuilding.Id).ToListAsync());
    }

    [Fact]
    public async Task InactiveUnit_SkipsBoostAndCampaignSpend()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, targetCompany, mediaBuilding, fundingAccount) =
            await SeedMediaHouseScenarioAsync(db, underConstruction: false);

        db.MediaHouseUnits.Add(new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Newspaper,
            CampaignBudgetPerTick = 300m,
            BrandQualityBoostPerTick = 0.3m,
            IsActive = false,
            LaborCostPerTick = 40m,
            EnergyCostPerTick = 20m,
        });
        await db.SaveChangesAsync();

        var originalBrandQuality = (await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id)).MarketingQuality;
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(fundingAccount).ReloadAsync();
        var brand = await db.Brands.FirstAsync(b => b.CompanyId == targetCompany.Id);
        Assert.Equal(10_000m, fundingAccount.Balance);
        Assert.InRange(brand.MarketingQuality, originalBrandQuality - 0.001m, originalBrandQuality + 0.001m);
        Assert.Empty(await db.BrandQualityRecords.Where(r => r.BuildingId == mediaBuilding.Id).ToListAsync());
    }

    [Fact]
    public async Task MediaTypeMultipliers_TvAndRadioScaleAgainstNewspaper()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (_, _, targetCompany, mediaBuilding, _) =
            await SeedMediaHouseScenarioAsync(db, underConstruction: false);

        var newspaper = new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Newspaper,
            CampaignBudgetPerTick = 0.01m,
            BrandQualityBoostPerTick = 0.01m,
            IsActive = true,
            LaborCostPerTick = 0m,
            EnergyCostPerTick = 0m,
        };
        var radio = new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Radio,
            CampaignBudgetPerTick = 0.01m,
            BrandQualityBoostPerTick = 0.018m,
            IsActive = true,
            LaborCostPerTick = 0m,
            EnergyCostPerTick = 0m,
        };
        var tv = new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = mediaBuilding.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Tv,
            CampaignBudgetPerTick = 0.01m,
            BrandQualityBoostPerTick = 0.03m,
            IsActive = true,
            LaborCostPerTick = 0m,
            EnergyCostPerTick = 0m,
        };
        db.MediaHouseUnits.AddRange(newspaper, radio, tv);
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        var records = await db.BrandQualityRecords
            .Where(r => r.BuildingId == mediaBuilding.Id)
            .OrderBy(r => r.BoostApplied)
            .ToListAsync();

        Assert.Equal(3, records.Count);
        var np = records[0].BoostApplied;
        var rd = records[1].BoostApplied;
        var tvBoost = records[2].BoostApplied;
        Assert.Equal(0.01m, np);
        Assert.Equal(0.018m, rd);
        Assert.Equal(0.03m, tvBoost);
        Assert.Equal(np * 1.8m, rd);
        Assert.Equal(np * 3m, tvBoost);
    }

    [Fact]
    public async Task ConfigureMediaHouseUnit_Mutation_CreatesUnitWithComputedBoost()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"media-config-{Guid.NewGuid():N}@test.com";
        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email, displayName = "Configurator", password = "TestPass123!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var userId = Guid.Parse(register.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync() ?? throw new InvalidOperationException("Game state missing.");
        var companyA = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Media A", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        var companyB = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Target B", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        db.Companies.AddRange(companyA, companyB);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = companyA.Id,
            CityId = city.Id,
            Type = BuildingType.MediaHouse,
            Name = "Config House",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 2,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        const string mutation = """
            mutation ConfigureMediaHouseUnit($input: ConfigureMediaHouseUnitInput!) {
              configureMediaHouseUnit(input: $input) {
                id
                mediaType
                targetCompanyId
                campaignBudgetPerTick
                brandQualityBoostPerTick
                laborCostPerTick
                energyCostPerTick
                isActive
              }
            }
            """;

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            mutation,
            new
            {
                input = new
                {
                    buildingId = building.Id,
                    targetCompanyId = companyB.Id,
                    mediaType = "TV",
                    campaignBudgetPerTick = 100m,
                    isActive = true,
                }
            },
            token);

        var unit = result.GetProperty("data").GetProperty("configureMediaHouseUnit");
        Assert.Equal("TV", unit.GetProperty("mediaType").GetString());
        Assert.Equal(companyB.Id.ToString(), unit.GetProperty("targetCompanyId").GetString());
        Assert.True(unit.GetProperty("brandQualityBoostPerTick").GetDecimal() > 0m);
        Assert.True(unit.GetProperty("laborCostPerTick").GetDecimal() > 0m);
        Assert.True(unit.GetProperty("energyCostPerTick").GetDecimal() > 0m);
    }

    [Fact]
    public async Task ConfigureMediaHouseUnit_Mutation_AllowsConfigurationWhileUpgradePlanIsPending()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"media-upgrading-{Guid.NewGuid():N}@test.com";
        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email, displayName = "Upgrading Configurator", password = "TestPass123!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var userId = Guid.Parse(register.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync() ?? throw new InvalidOperationException("Game state missing.");
        var mediaCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Media Upgrading Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        var targetCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Target Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        db.Companies.AddRange(mediaCompany, targetCompany);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = mediaCompany.Id,
            CityId = city.Id,
            Type = BuildingType.MediaHouse,
            Name = "Upgrading Media House",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 2,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        db.BuildingConfigurationPlans.Add(new BuildingConfigurationPlan
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            SubmittedAtUtc = DateTime.UtcNow,
            SubmittedAtTick = gameState.CurrentTick,
            AppliesAtTick = gameState.CurrentTick + 5,
            TotalTicksRequired = 5,
        });
        await db.SaveChangesAsync();

        const string mutation = """
            mutation ConfigureMediaHouseUnit($input: ConfigureMediaHouseUnitInput!) {
              configureMediaHouseUnit(input: $input) {
                id
                targetCompanyId
                campaignBudgetPerTick
              }
            }
            """;

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            mutation,
            new
            {
                input = new
                {
                    buildingId = building.Id,
                    targetCompanyId = targetCompany.Id,
                    mediaType = "TV",
                    campaignBudgetPerTick = 250m,
                    isActive = true,
                }
            },
            token);

        var unit = result.GetProperty("data").GetProperty("configureMediaHouseUnit");
        Assert.Equal(targetCompany.Id.ToString(), unit.GetProperty("targetCompanyId").GetString());
        Assert.Equal(250m, unit.GetProperty("campaignBudgetPerTick").GetDecimal());
    }

    [Fact]
    public async Task ConfigureMediaHouseUnit_Mutation_DestroyedBuilding_ReturnsBuildingAlreadyDestroyed()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"media-destroyed-{Guid.NewGuid():N}@test.com";
        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email, displayName = "Destroyed Configurator", password = "TestPass123!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var userId = Guid.Parse(register.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync() ?? throw new InvalidOperationException("Game state missing.");
        var mediaCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Destroyed Media Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        var targetCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Target Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        db.Companies.AddRange(mediaCompany, targetCompany);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = mediaCompany.Id,
            CityId = city.Id,
            Type = BuildingType.MediaHouse,
            Name = "Destroyed Media House",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 2,
            DestroyedAtUtc = DateTime.UtcNow,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        const string mutation = """
            mutation ConfigureMediaHouseUnit($input: ConfigureMediaHouseUnitInput!) {
              configureMediaHouseUnit(input: $input) { id }
            }
            """;

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            mutation,
            new
            {
                input = new
                {
                    buildingId = building.Id,
                    targetCompanyId = targetCompany.Id,
                    mediaType = "RADIO",
                    campaignBudgetPerTick = 100m,
                    isActive = true,
                }
            },
            token);

        var errors = result.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal(
            "BUILDING_ALREADY_DESTROYED",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MediaHouseStats_Query_ReturnsBoostAndSpend()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var email = $"media-stats-{Guid.NewGuid():N}@test.com";
        var register = await TestHelpers.ExecuteGraphQlAsync(
            client,
            RegisterMutation,
            new { input = new { email, displayName = "StatsUser", password = "TestPass123!" } });
        var token = register.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
        var userId = Guid.Parse(register.GetProperty("data").GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync() ?? throw new InvalidOperationException("Game state missing.");
        var mediaCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Stats Media", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        var targetCompany = new Company { Id = Guid.NewGuid(), PlayerId = userId, Name = "Stats Target", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = gameState.CurrentTick };
        db.Companies.AddRange(mediaCompany, targetCompany);
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "9001000000000002",
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000m,
            CompanyId = mediaCompany.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = mediaCompany.Id,
            CityId = city.Id,
            Type = BuildingType.MediaHouse,
            Name = "Stats House",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BankAccountId = account.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        db.Brands.Add(new Brand
        {
            Id = Guid.NewGuid(),
            CompanyId = targetCompany.Id,
            Name = "Stats Brand",
            Scope = BrandScope.Company,
            Awareness = 0.1m,
            Quality = 0.1m,
            MarketingQuality = 0.1m,
        });
        db.MediaHouseUnits.Add(new MediaHouseUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            TargetCompanyId = targetCompany.Id,
            MediaType = MediaType.Newspaper,
            CampaignBudgetPerTick = 100m,
            BrandQualityBoostPerTick = 0.1m,
            IsActive = true,
            LaborCostPerTick = 20m,
            EnergyCostPerTick = 10m,
        });
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        const string query = """
            query MediaHouseStats($buildingId: UUID!) {
              mediaHouseStats(buildingId: $buildingId) {
                buildingId
                currentBoostDelivered
                campaignCostThisTaxCycle
                estimatedSalesImpact
                boostHistory { tick boost }
                units { id mediaType campaignBudgetPerTick isActive }
              }
            }
            """;

        var result = await TestHelpers.ExecuteGraphQlAsync(
            client,
            query,
            new { buildingId = building.Id },
            token);

        var stats = result.GetProperty("data").GetProperty("mediaHouseStats");
        Assert.Equal(building.Id.ToString(), stats.GetProperty("buildingId").GetString());
        Assert.True(stats.GetProperty("campaignCostThisTaxCycle").GetDecimal() > 0m);
        Assert.True(stats.GetProperty("units").GetArrayLength() > 0);
    }
}
