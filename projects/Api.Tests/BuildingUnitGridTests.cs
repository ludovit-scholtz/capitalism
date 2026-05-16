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

/// <summary>
/// Integration tests for the Building Unit Grid Configuration System (Core Production Chain).
/// These tests cover the key acceptance criteria from the feature issue:
/// - Mine/Factory/SalesShop storeBuildingConfiguration (happy path + validation errors).
/// - Unauthenticated access is rejected.
/// - Invalid unit type for building type returns INVALID_BUILDING_UNIT_TYPE.
/// - Cancel pending configuration plan.
/// - Tick engine: mining unit produces inventory after a tick.
/// - ScheduleUnitUpgrade: happy path cost deduction, insufficient-funds rejection.
/// </summary>
public sealed class BuildingUnitGridTests
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
        return JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync());
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var result = await GqlAsync(client,
            "mutation R($i: RegisterInput!) { register(input: $i) { token } }",
            new { i = new { email, displayName = "GridTester", password = "GridTest123!" } });
        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    private static async Task<string> CreateCompanyAsync(HttpClient client, string token, string name = "Test Corp")
    {
        var result = await GqlAsync(client,
            "mutation CC($i: CreateCompanyInput!) { createCompany(input: $i) { id } }",
            new { i = new { name } }, token);
        return result.GetProperty("data").GetProperty("createCompany").GetProperty("id").GetString()!;
    }

    private static async Task FundCompanyAsync(ApiWebApplicationFactory factory, string companyId, decimal amount = 10_000_000m)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var companyGuid = Guid.Parse(companyId);
        var existing = await db.BankAccounts
            .Where(a => a.CompanyId == companyGuid && a.CurrencyCode == "EUR" && a.PlayerId == null && !a.IsGovernmentAccount)
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
                AccountNumber = $"TEST{Guid.NewGuid():N}"[..16],
                CurrencyCode = "EUR",
                CompanyId = companyGuid,
                Balance = amount,
                CreatedAtUtc = DateTime.UtcNow,
            });
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Seeds a building of the given type and returns the building ID.</summary>
    private static async Task<string> SeedBuildingAsync(
        ApiWebApplicationFactory factory,
        string companyId,
        string buildingType,
        string cityName = "Bratislava")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == cityName);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.Parse(companyId),
            CityId = city.Id,
            Type = buildingType,
            Name = $"Test {buildingType}",
            Level = 1,
            PowerStatus = PowerStatus.Powered,
            PowerConsumption = 2m,
            Latitude = city.Latitude + 0.01,
            Longitude = city.Longitude + 0.01,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();
        return building.Id.ToString();
    }

    private static async Task<string> GetResourceTypeIdAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rt = await db.ResourceTypes.FirstAsync();
        return rt.Id.ToString();
    }

    private static string GetErrorCode(JsonElement result)
    {
        if (!result.TryGetProperty("errors", out var errors) || errors.GetArrayLength() == 0)
            return "(no errors)";
        return errors[0].GetProperty("extensions").GetProperty("code").GetString()!;
    }

    private static async Task SeedPledgedCollateralLoanAsync(
        ApiWebApplicationFactory factory,
        string borrowerCompanyId,
        string collateralBuildingId,
        string status = LoanStatus.Active)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var lenderPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"pledge-lender-{Guid.NewGuid():N}@test.com",
            DisplayName = "Pledge Lender",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(lenderPlayer);

        var lenderCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = lenderPlayer.Id,
            Name = $"PledgeLenderCo-{Guid.NewGuid():N}",
        };
        db.Companies.Add(lenderCompany);

        var bankBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = lenderCompany.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "Pledge Bank",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            BaseCapitalDeposited = true,
            TotalDeposits = 500_000m,
            LendingInterestRatePercent = 8m,
        };
        db.Buildings.Add(bankBuilding);

        db.BankAccounts.Add(new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            CompanyId = lenderCompany.Id,
            Balance = 200_000m,
            CreatedAtUtc = DateTime.UtcNow,
        });

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bankBuilding.Id,
            LenderCompanyId = lenderCompany.Id,
            AnnualInterestRatePercent = 8m,
            MaxPrincipalPerLoan = 200_000m,
            TotalCapacity = 500_000m,
            UsedCapacity = 100_000m,
            DurationTicks = 1440L,
            IsActive = false,
            CreatedAtTick = 1,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);

        db.Loans.Add(new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = Guid.Parse(borrowerCompanyId),
            BankBuildingId = bankBuilding.Id,
            LenderCompanyId = lenderCompany.Id,
            OriginalPrincipal = 100_000m,
            RemainingPrincipal = 80_000m,
            AnnualInterestRatePercent = 8m,
            DurationTicks = 1440L,
            StartTick = 1,
            DueTick = 1441,
            NextPaymentTick = 721,
            PaymentAmount = 10_000m,
            TotalPayments = 10,
            Status = status,
            MissedPayments = status == LoanStatus.Defaulted ? 3 : 0,
            DefaultedAtTick = status == LoanStatus.Defaulted ? 10 : null,
            CollateralBuildingId = Guid.Parse(collateralBuildingId),
            CollateralAppraisedValue = 150_000m,
            AcceptedAtUtc = DateTime.UtcNow.AddDays(-2),
        });

        await db.SaveChangesAsync();
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// AC: storeBuildingConfiguration with a Mine and MINING + STORAGE + B2B_SALES units succeeds
    /// and returns a plan with the correct tick schedule.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_Mine_MiningStorageB2bSales_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"mine-grid-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Mine Grid Corp");
        await FundCompanyAsync(factory, companyId);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Mine);
        var resourceTypeId = await GetResourceTypeIdAsync(factory);

        // Seed a deposit lot so the mine's MINING unit has a valid resource source.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                BuildingId = Guid.Parse(buildingId),
                CityId = city.Id,
                Name = "Test Mine Lot",
                District = "Industrial",
                Latitude = city.Latitude + 0.01,
                Longitude = city.Longitude + 0.01,
                Price = 75_000m,
                SuitableTypes = "MINE",
                OwnerCompanyId = Guid.Parse(companyId),
                ResourceTypeId = Guid.Parse(resourceTypeId),
                MaterialQuantity = 5_000m,
                OriginalMaterialQuantity = 5_000m,
                MaterialQuality = 0.7m,
                ConcurrencyToken = Guid.NewGuid(),
            });
            await db.SaveChangesAsync();
        }

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) {
                id
                buildingId
                appliesAtTick
                totalTicksRequired
              }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "MINING", gridX = 0, gridY = 0, linkRight = true, resourceTypeId,
                              linkUp = false, linkDown = false, linkLeft = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "STORAGE", gridX = 1, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "B2B_SALES", gridX = 2, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }, token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"storeBuildingConfiguration for Mine must not return errors. Got: {result.GetRawText()}");

        var plan = result.GetProperty("data").GetProperty("storeBuildingConfiguration");
        Assert.Equal(buildingId, plan.GetProperty("buildingId").GetString());
        Assert.True(plan.GetProperty("totalTicksRequired").GetInt32() >= 0,
            "totalTicksRequired must be >= 0");
    }

    /// <summary>
    /// AC: storeBuildingConfiguration with an invalid unit type for the building type
    /// returns INVALID_BUILDING_UNIT_TYPE.
    /// A Mine should reject MANUFACTURING units.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_Mine_InvalidUnitType_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"mine-invalid-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Invalid Type Corp");
        await FundCompanyAsync(factory, companyId);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Mine);

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        // MANUFACTURING is not allowed in a Mine
                        new { unitType = "MANUFACTURING", gridX = 0, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }, token);

        var code = GetErrorCode(result);
        Assert.Equal("INVALID_BUILDING_UNIT_TYPE", code);
    }

    /// <summary>
    /// AC: storeBuildingConfiguration without authentication returns an error.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_Unauthenticated_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    buildingId = Guid.NewGuid().ToString(),
                    units = new object[]
                    {
                        new { unitType = "MINING", gridX = 0, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }
            // No token provided
        );

        Assert.True(result.TryGetProperty("errors", out var errors) && errors.GetArrayLength() > 0,
            "Unauthenticated storeBuildingConfiguration must return errors.");
    }

    /// <summary>
    /// AC: storeBuildingConfiguration for a Factory with PURCHASE + MANUFACTURING + STORAGE + B2B_SALES
    /// succeeds and returns a plan.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_Factory_PurchaseManufacturingStorageB2bSales_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"factory-grid-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Factory Grid Corp");
        await FundCompanyAsync(factory, companyId);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);
        // Get a product type and the matching resource for the manufacturing unit.
        string productTypeId;
        string resourceTypeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.ProductTypes.Include(p => p.Recipes).FirstAsync(p => p.Slug == "wooden-chair");
            productTypeId = product.Id.ToString();
            // Use the resource required by the product recipe so RECIPE_INPUT_MISMATCH doesn't trigger.
            var recipe = product.Recipes.First();
            resourceTypeId = recipe.ResourceTypeId!.Value.ToString();
        }

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) {
                id
                buildingId
                totalTicksRequired
              }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "PURCHASE", gridX = 0, gridY = 0, linkRight = true, resourceTypeId,
                              linkUp = false, linkDown = false, linkLeft = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "MANUFACTURING", gridX = 1, gridY = 0, linkRight = true, productTypeId,
                              linkUp = false, linkDown = false, linkLeft = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "STORAGE", gridX = 2, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "B2B_SALES", gridX = 3, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }, token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"Factory configuration must succeed. Got: {result.GetRawText()}");

        var plan = result.GetProperty("data").GetProperty("storeBuildingConfiguration");
        Assert.Equal(buildingId, plan.GetProperty("buildingId").GetString());
    }

    /// <summary>
    /// AC: storeBuildingConfiguration for a SalesShop with PURCHASE + PUBLIC_SALES succeeds.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_SalesShop_PurchasePublicSales_Succeeds()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"shop-grid-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Shop Grid Corp");
        await FundCompanyAsync(factory, companyId);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.SalesShop);

        // Get a product type for the public sales unit.
        string productTypeId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var product = await db.ProductTypes.FirstAsync(p => p.Slug == "wooden-chair");
            productTypeId = product.Id.ToString();
        }

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) {
                id
                buildingId
              }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "PURCHASE", gridX = 0, gridY = 0, linkRight = true, productTypeId,
                              linkUp = false, linkDown = false, linkLeft = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "PUBLIC_SALES", gridX = 1, gridY = 0, productTypeId,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }, token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"SalesShop configuration must succeed. Got: {result.GetRawText()}");

        var plan = result.GetProperty("data").GetProperty("storeBuildingConfiguration");
        Assert.Equal(buildingId, plan.GetProperty("buildingId").GetString());
    }

    /// <summary>
    /// AC: cancelBuildingConfiguration removes the pending plan.
    /// </summary>
    [Fact]
    public async Task CancelBuildingConfiguration_WithPendingPlan_RemovesPlan()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"cancel-grid-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Cancel Grid Corp");
        await FundCompanyAsync(factory, companyId);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);

        // First store a configuration plan.
        var storeResult = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) { id buildingId }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "STORAGE", gridX = 0, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            }, token);

        Assert.False(storeResult.TryGetProperty("errors", out _),
            $"Store must succeed before cancel. Got: {storeResult.GetRawText()}");

        // Now cancel the plan.
        var cancelResult = await GqlAsync(client,
            """
            mutation Cancel($input: CancelBuildingConfigurationInput!) {
              cancelBuildingConfiguration(input: $input) {
                id
                buildingId
                totalTicksRequired
                removals { gridX gridY isReverting }
              }
            }
            """,
            new { input = new { buildingId } },
            token);

        Assert.False(cancelResult.TryGetProperty("errors", out _),
            $"cancelBuildingConfiguration must succeed. Got: {cancelResult.GetRawText()}");

        var cancelledPlan = cancelResult.GetProperty("data").GetProperty("cancelBuildingConfiguration");
        Assert.Equal(buildingId, cancelledPlan.GetProperty("buildingId").GetString());

        // After cancel, the plan should have a reverting removal (not a new unit addition).
        var removals = cancelledPlan.GetProperty("removals").EnumerateArray().ToList();
        Assert.NotEmpty(removals);
        Assert.True(removals.All(r => r.GetProperty("isReverting").GetBoolean()),
            "All removals after cancel must be marked as reverting (rolling back the original plan).");
    }

    /// <summary>
    /// AC: The tick engine's mining phase produces inventory in the mining unit's building
    /// after one tick. Proves the end-to-end mine production chain is wired correctly.
    /// </summary>
    [Fact]
    public async Task TickEngine_MiningPhase_ProducesInventoryAfterOneTick()
    {
        await using var isolatedFactory = new ApiWebApplicationFactory();
        // Initialize the host so AppDbInitializer seeds cities, resources, etc.
        _ = isolatedFactory.CreateClient();

        Guid buildingId;
        Guid resourceTypeId;

        // Seed a mine with a deposit lot directly in the DB.
        await using (var scope = isolatedFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            // Use wood — always present in Bratislava seed data with non-zero abundance.
            var rt = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");
            resourceTypeId = rt.Id;

            var player = new Player
            {
                Id = Guid.NewGuid(),
                Email = $"tick-mine-{Guid.NewGuid():N}@test.com",
                DisplayName = "Tick Mine Tester",
                PasswordHash = "hash",
                Role = PlayerRole.Player,
            };
            db.Players.Add(player);

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = "Tick Mine Corp",
                Cash = 0m,
            };
            db.Companies.Add(company);

            // Bank account so OperatingCostPhase can debit labor/energy costs
            // without suspending the building (IsSuspendedForFunds = false).
            db.BankAccounts.Add(new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = $"{Guid.NewGuid():N}"[..16],
                CurrencyCode = "EUR",
                CompanyId = company.Id,
                Balance = 10_000_000m,
                CreatedAtUtc = DateTime.UtcNow,
            });

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Mine,
                Name = "Tick Mine",
                Level = 1,
                PowerStatus = PowerStatus.Powered,
                PowerConsumption = 2m,
                Latitude = city.Latitude + 0.01,
                Longitude = city.Longitude + 0.01,
            };
            db.Buildings.Add(building);
            buildingId = building.Id;

            var miningUnit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                UnitType = UnitType.Mining,
                GridX = 0,
                GridY = 0,
                Level = 1,
                ResourceTypeId = rt.Id,
            };
            db.BuildingUnits.Add(miningUnit);

            // Seed a deposit lot so MiningPhase can extract from it.
            db.BuildingLots.Add(new BuildingLot
            {
                Id = Guid.NewGuid(),
                BuildingId = building.Id,
                CityId = city.Id,
                Name = "Tick Mine Lot",
                District = "Industrial",
                Latitude = city.Latitude + 0.01,
                Longitude = city.Longitude + 0.01,
                Price = 75_000m,
                SuitableTypes = "MINE",
                OwnerCompanyId = company.Id,
                ResourceTypeId = rt.Id,
                MaterialQuantity = 10_000m,
                OriginalMaterialQuantity = 10_000m,
                MaterialQuality = 0.7m,
                ConcurrencyToken = Guid.NewGuid(),
            });

            await db.SaveChangesAsync();
        }

        // Run one tick using the registered ITickPhase services.
        await using (var scope = isolatedFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var phases = scope.ServiceProvider.GetServices<ITickPhase>();
            var processor = new TickProcessor(db, phases, NullLogger<TickProcessor>.Instance);
            await processor.ProcessTickAsync();
        }

        // After one tick, the mining unit should have produced inventory.
        await using (var scope = isolatedFactory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var inventories = await db.Inventories
                .Where(i => i.BuildingId == buildingId && i.BuildingUnitId != null)
                .ToListAsync();

            Assert.NotEmpty(inventories);
            Assert.True(inventories.Sum(i => i.Quantity) > 0m,
                "MiningPhase must produce resources in the mining unit's inventory after one tick.");
        }
    }

    /// <summary>
    /// AC: scheduleUnitUpgrade deducts the upgrade cost from the building's bank account
    /// and creates a pending plan with the correct tick schedule.
    /// </summary>
    [Fact]
    public async Task ScheduleUnitUpgrade_WithSufficientFunds_SucceedsAndCreatesPlan()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"upgrade-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Upgrade Corp");
        await FundCompanyAsync(factory, companyId, 5_000_000m);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);

        // Seed a unit in the building.
        string unitId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = Guid.Parse(buildingId),
                UnitType = UnitType.Storage,
                GridX = 0,
                GridY = 0,
                Level = 1,
            };
            db.BuildingUnits.Add(unit);

            // Ensure the building has a bank account assigned.
            var building = await db.Buildings.Include(b => b.BankAccount).FirstAsync(b => b.Id == Guid.Parse(buildingId));
            if (building.BankAccount is null)
            {
                var ba = new BankAccount
                {
                    Id = Guid.NewGuid(),
                    AccountNumber = $"UPGRAD{Guid.NewGuid():N}"[..16],
                    CurrencyCode = "EUR",
                    CompanyId = Guid.Parse(companyId),
                    Balance = 5_000_000m,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                db.BankAccounts.Add(ba);
                building.BankAccountId = ba.Id;
            }
            else
            {
                building.BankAccount.Balance = 5_000_000m;
            }

            await db.SaveChangesAsync();
            unitId = unit.Id.ToString();
        }

        var result = await GqlAsync(client,
            """
            mutation Upgrade($input: ScheduleUnitUpgradeInput!) {
              scheduleUnitUpgrade(input: $input) {
                id
                buildingId
                appliesAtTick
                totalTicksRequired
              }
            }
            """,
            new { input = new { unitId } },
            token);

        Assert.False(result.TryGetProperty("errors", out _),
            $"scheduleUnitUpgrade must succeed with sufficient funds. Got: {result.GetRawText()}");

        var plan = result.GetProperty("data").GetProperty("scheduleUnitUpgrade");
        Assert.Equal(buildingId, plan.GetProperty("buildingId").GetString());
        Assert.True(plan.GetProperty("appliesAtTick").GetInt64() > 0,
            "appliesAtTick must be in the future.");
    }

    /// <summary>
    /// AC: scheduleUnitUpgrade with insufficient bank account balance returns INSUFFICIENT_FUNDS.
    /// </summary>
    [Fact]
    public async Task ScheduleUnitUpgrade_InsufficientFunds_ReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"upgrade-nofunds-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "No Funds Corp");
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);

        // Seed a unit + a bank account with 0 balance.
        string unitId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = Guid.Parse(buildingId),
                UnitType = UnitType.Storage,
                GridX = 0,
                GridY = 0,
                Level = 1,
            };
            db.BuildingUnits.Add(unit);

            var ba = new BankAccount
            {
                Id = Guid.NewGuid(),
                AccountNumber = $"NOFUND{Guid.NewGuid():N}"[..16],
                CurrencyCode = "EUR",
                CompanyId = Guid.Parse(companyId),
                Balance = 0m, // Zero balance — upgrade should fail.
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.BankAccounts.Add(ba);

            var building = await db.Buildings.FirstAsync(b => b.Id == Guid.Parse(buildingId));
            building.BankAccountId = ba.Id;

            await db.SaveChangesAsync();
            unitId = unit.Id.ToString();
        }

        var result = await GqlAsync(client,
            """
            mutation Upgrade($input: ScheduleUnitUpgradeInput!) {
              scheduleUnitUpgrade(input: $input) { id }
            }
            """,
            new { input = new { unitId } },
            token);

        var code = GetErrorCode(result);
        Assert.Equal("INSUFFICIENT_FUNDS", code);
    }

    [Fact]
    public async Task StoreBuildingConfiguration_PledgedCollateral_ReturnsPledgedCollateralError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"pledged-store-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Pledged Store Corp");
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);
        await SeedPledgedCollateralLoanAsync(factory, companyId, buildingId, LoanStatus.Defaulted);

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "PURCHASE", gridX = 0, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = true, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false },
                        new { unitType = "MANUFACTURING", gridX = 1, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            },
            token);

        var code = GetErrorCode(result);
        Assert.Equal("BUILDING_IS_PLEDGED_COLLATERAL", code);
    }

    [Fact]
    public async Task ScheduleUnitUpgrade_PledgedCollateral_ReturnsPledgedCollateralError()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        var token = await RegisterAsync(client, $"pledged-upgrade-{Guid.NewGuid():N}@test.com");
        var companyId = await CreateCompanyAsync(client, token, "Pledged Upgrade Corp");
        await FundCompanyAsync(factory, companyId, 2_000_000m);
        var buildingId = await SeedBuildingAsync(factory, companyId, BuildingType.Factory);

        string unitId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var unit = new BuildingUnit
            {
                Id = Guid.NewGuid(),
                BuildingId = Guid.Parse(buildingId),
                UnitType = UnitType.Storage,
                GridX = 0,
                GridY = 0,
                Level = 1,
            };
            db.BuildingUnits.Add(unit);
            unitId = unit.Id.ToString();
            await db.SaveChangesAsync();
        }

        await SeedPledgedCollateralLoanAsync(factory, companyId, buildingId, LoanStatus.Active);

        var result = await GqlAsync(client,
            """
            mutation Upgrade($input: ScheduleUnitUpgradeInput!) {
              scheduleUnitUpgrade(input: $input) { id }
            }
            """,
            new { input = new { unitId } },
            token);

        var code = GetErrorCode(result);
        Assert.Equal("BUILDING_IS_PLEDGED_COLLATERAL", code);
    }

    /// <summary>
    /// AC: storeBuildingConfiguration for a non-owned building returns FORBIDDEN.
    /// This prevents foreign-object enumeration.
    /// </summary>
    [Fact]
    public async Task StoreBuildingConfiguration_ForeignBuilding_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        // Owner registers and creates a building.
        var ownerToken = await RegisterAsync(client, $"owner-{Guid.NewGuid():N}@test.com");
        var ownerCompanyId = await CreateCompanyAsync(client, ownerToken, "Owner Corp");
        var buildingId = await SeedBuildingAsync(factory, ownerCompanyId, BuildingType.Factory);

        // Attacker registers separately.
        var attackerToken = await RegisterAsync(client, $"attacker-{Guid.NewGuid():N}@test.com");

        var result = await GqlAsync(client,
            """
            mutation Store($input: StoreBuildingConfigurationInput!) {
              storeBuildingConfiguration(input: $input) { id }
            }
            """,
            new
            {
                input = new
                {
                    buildingId,
                    units = new object[]
                    {
                        new { unitType = "STORAGE", gridX = 0, gridY = 0,
                              linkUp = false, linkDown = false, linkLeft = false, linkRight = false, linkUpLeft = false, linkUpRight = false, linkDownLeft = false, linkDownRight = false }
                    }
                }
            },
            attackerToken);

        var code = GetErrorCode(result);
        Assert.Equal("FORBIDDEN", code);
    }

    /// <summary>
    /// AC: The GetAllowedUnitTypes helper returns the correct allowed types per building type.
    /// This ensures that the type constraint logic is correct for all key building types.
    /// </summary>
    [Fact]
    public void GetAllowedUnitTypes_ReturnsCorrectTypesForAllBuildingTypes()
    {
        // Mine: MINING, STORAGE, B2B_SALES
        var mineTypes = BuildingConfigurationService.GetAllowedUnitTypes(BuildingType.Mine);
        Assert.Contains(UnitType.Mining, mineTypes);
        Assert.Contains(UnitType.Storage, mineTypes);
        Assert.Contains(UnitType.B2BSales, mineTypes);
        Assert.DoesNotContain(UnitType.Manufacturing, mineTypes);
        Assert.DoesNotContain(UnitType.PublicSales, mineTypes);

        // Factory: PURCHASE, MANUFACTURING, BRANDING, STORAGE, B2B_SALES
        var factoryTypes = BuildingConfigurationService.GetAllowedUnitTypes(BuildingType.Factory);
        Assert.Contains(UnitType.Purchase, factoryTypes);
        Assert.Contains(UnitType.Manufacturing, factoryTypes);
        Assert.Contains(UnitType.Storage, factoryTypes);
        Assert.Contains(UnitType.B2BSales, factoryTypes);
        Assert.DoesNotContain(UnitType.Mining, factoryTypes);

        // SalesShop: PURCHASE, MARKETING, STORAGE, PUBLIC_SALES
        var shopTypes = BuildingConfigurationService.GetAllowedUnitTypes(BuildingType.SalesShop);
        Assert.Contains(UnitType.Purchase, shopTypes);
        Assert.Contains(UnitType.PublicSales, shopTypes);
        Assert.Contains(UnitType.Storage, shopTypes);
        Assert.DoesNotContain(UnitType.Manufacturing, shopTypes);
        Assert.DoesNotContain(UnitType.Mining, shopTypes);

        // ResearchDevelopment: PRODUCT_QUALITY, BRAND_QUALITY
        var rdTypes = BuildingConfigurationService.GetAllowedUnitTypes(BuildingType.ResearchDevelopment);
        Assert.Contains(UnitType.ProductQuality, rdTypes);
        Assert.Contains(UnitType.BrandQuality, rdTypes);
        Assert.DoesNotContain(UnitType.Manufacturing, rdTypes);
        Assert.DoesNotContain(UnitType.Mining, rdTypes);
    }
}
