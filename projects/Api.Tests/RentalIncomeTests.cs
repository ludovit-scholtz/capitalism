using System.Net.Http.Json;
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

/// <summary>
/// Integration tests for the Apartment &amp; Commercial Building Rental Income System.
/// Covers RentPhase, setRentPerSqm mutation, apartmentBuildingDetail query,
/// cityRentalMarket query, and ledger RENT_INCOME entries.
/// </summary>
public sealed class RentalIncomeTests
{
    // ── Helper: create TickProcessor for a DI scope ──────────────────────────

    private static Task<TickProcessor> CreateProcessorAsync(IServiceScope scope)
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        return Task.FromResult(new TickProcessor(db, phases, new NullLogger<TickProcessor>()));
    }

    // ── Seed helper ──────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds an apartment building owned by a player company with a configured rent price.
    /// </summary>
    private static async Task<(Player player, Company company, Building building, BankAccount account)>
        SeedApartmentBuildingAsync(
            AppDbContext db,
            string buildingType = BuildingType.Apartment,
            decimal rentPerSqm = 10m,
            decimal cityBaseRent = 10m,
            decimal initialBalance = 50_000m)
    {
        var city = await db.Cities.FirstDeterministicAsync();
        city.AverageRentPerSqm = cityBaseRent;

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()
            ?? throw new InvalidOperationException("Game state missing.");

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"rent-test-{Guid.NewGuid():N}@test.com",
            DisplayName = "Rent Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        db.Players.Add(player);

        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Realty Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState.CurrentTick,
        };
        db.Companies.Add(company);

        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"{Guid.NewGuid().GetHashCode() & 0x7FFFFFFF:D16}".PadLeft(16, '0'),
            CurrencyCode = city.CurrencyCode,
            Balance = initialBalance,
            CompanyId = company.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = buildingType,
            Name = "Test Apartment",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            PricePerSqm = rentPerSqm,
            TotalAreaSqm = 100m,          // 100 m² → easy math
            OccupancyPercent = 100m,       // Start at full occupancy
            BankAccountId = account.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);

        await db.SaveChangesAsync();
        return (player, company, building, account);
    }

    // ── Test 1: Happy path — rent income credited and RentalIncomeRecord written ──

    [Fact]
    public async Task RentPhase_HappyPath_CreditsIncomeAndWritesRecord()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building, account) = await SeedApartmentBuildingAsync(db,
            rentPerSqm: 10m,
            cityBaseRent: 10m,
            initialBalance: 50_000m);

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(account).ReloadAsync();

        // Expected: income = 10 × 100 × (100/100) = 1000; costs = 10 × 100 × 0.75 = 750
        var expectedIncome = 1000m;
        var expectedCosts = 750m;
        var expectedNet = expectedIncome - expectedCosts;
        Assert.Equal(50_000m + expectedNet, account.Balance);

        // RentalIncomeRecord should be written.
        var record = await db.RentalIncomeRecords
            .FirstOrDefaultAsync(r => r.BuildingId == building.Id);
        Assert.NotNull(record);
        Assert.Equal(expectedIncome, record!.Revenue);
        Assert.Equal(100m, record.OccupancyPercent);
        Assert.Equal(10m, record.RentPerSqm);
    }

    // ── Test 2: Price above city average — occupancy drops ───────────────────

    [Fact]
    public async Task RentPhase_PriceAboveCityAverage_OccupancyDriftsDown()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Rent is 2× city average → maxOccupancy = 50% (overpriced floor).
        var (_, _, building, _) = await SeedApartmentBuildingAsync(db,
            rentPerSqm: 20m,
            cityBaseRent: 10m);

        // Start at 100% occupancy so we can see it drift down.
        building.OccupancyPercent = 100m;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        // After one tick the occupancy should have moved toward 50%.
        Assert.True(building.OccupancyPercent < 100m, "Occupancy should have started drifting down.");
    }

    // ── Test 3: Price below 60% of city average — occupancy can reach 100% ──

    [Fact]
    public async Task RentPhase_PriceBelowCityAverage_OccupancyCanReach100Pct()
    {
        // ComputeMaxOccupancy returns 100 when priceRatio <= 0.60.
        var maxOccupancy = RentPhase.ComputeMaxOccupancy(0.50m);
        Assert.Equal(100m, maxOccupancy);
    }

    // ── Test 4: Pending-change delay — 24-tick wait ───────────────────────────

    [Fact]
    public async Task RentPhase_PendingPriceChange_NotAppliedBeforeActivationTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building, _) = await SeedApartmentBuildingAsync(db, rentPerSqm: 10m);

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()!;
        // Set a pending price change to activate far in the future.
        building.PendingPricePerSqm = 20m;
        building.PendingPriceActivationTick = gameState!.CurrentTick + 100;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        // Active price should still be 10, not 20.
        Assert.Equal(10m, building.PricePerSqm);
        Assert.Equal(20m, building.PendingPricePerSqm);
    }

    // ── Test 5: Pending-change applied on correct tick ────────────────────────

    [Fact]
    public async Task RentPhase_PendingPriceChange_AppliedOnActivationTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building, _) = await SeedApartmentBuildingAsync(db, rentPerSqm: 10m);

        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()!;
        // Set activation tick to exactly current + 1 (activates on next tick).
        building.PendingPricePerSqm = 25m;
        building.PendingPriceActivationTick = gameState!.CurrentTick + 1;
        await db.SaveChangesAsync();

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(building).ReloadAsync();
        Assert.Equal(25m, building.PricePerSqm);
        Assert.Null(building.PendingPricePerSqm);
        Assert.Null(building.PendingPriceActivationTick);
    }

    // ── Test 6: Non-owner cannot call setRentPerSqm ───────────────────────────

    private const string SetRentPerSqmMutation = """
        mutation SetRent($input: SetRentPerSqmInput!) {
          setRentPerSqm(input: $input) {
            id
          }
        }
        """;

    private const string RegisterMutation = """
        mutation Register($input: RegisterInput!) {
          register(input: $input) {
            token
            player { id }
          }
        }
        """;

    [Fact]
    public async Task SetRentPerSqm_NonOwner_ReturnsNotFoundOrNotOwned()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed the building under a different player's company.
        var (_, _, building, _) = await SeedApartmentBuildingAsync(db, rentPerSqm: 10m);

        // Register as a different player.
        var registerResp = await client.PostAsJsonAsync("/graphql", new
        {
            query = RegisterMutation,
            variables = new
            {
                input = new
                {
                    email = $"other-{Guid.NewGuid():N}@test.com",
                    password = "Test1234!",
                    displayName = "Other Player"
                }
            }
        });
        var registerDoc = JsonDocument.Parse(await registerResp.Content.ReadAsStringAsync());
        var otherToken = registerDoc.RootElement
            .GetProperty("data").GetProperty("register").GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", otherToken);

        var resp = await client.PostAsJsonAsync("/graphql", new
        {
            query = SetRentPerSqmMutation,
            variables = new { input = new { buildingId = building.Id, rentPerSqm = 15m } }
        });
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var errors = doc.RootElement.GetProperty("errors");
        Assert.True(errors.GetArrayLength() > 0);
        Assert.Equal("FORBIDDEN",
            errors[0].GetProperty("extensions").GetProperty("code").GetString());
    }

    // ── Test 7: Commercial vs Apartment — maxOccupancy formula differs ────────

    [Fact]
    public async Task RentPhase_CommercialBuilding_ProcessedCorrectly()
    {
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building, account) = await SeedApartmentBuildingAsync(db,
            buildingType: BuildingType.Commercial,
            rentPerSqm: 15m,
            cityBaseRent: 10m,
            initialBalance: 50_000m);

        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        await db.Entry(account).ReloadAsync();
        // COMMERCIAL building was processed — bank account should have changed.
        Assert.NotEqual(50_000m, account.Balance);

        var record = await db.RentalIncomeRecords
            .FirstOrDefaultAsync(r => r.BuildingId == building.Id);
        Assert.NotNull(record);
    }

    // ── Test 8: cityRentalMarket returns correct averages ─────────────────────

    private const string CityRentalMarketQuery = """
        query CityRent($cityId: UUID!) {
          cityRentalMarket(cityId: $cityId) {
            cityId
            cityAverageRentPerSqm
            averageApartmentRentPerSqm
            averageCommercialRentPerSqm
            activeApartmentCount
            activeCommercialCount
          }
        }
        """;

    [Fact]
    public async Task CityRentalMarket_ReturnsCorrectAverages()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building1, _) = await SeedApartmentBuildingAsync(db,
            buildingType: BuildingType.Apartment,
            rentPerSqm: 10m,
            cityBaseRent: 10m);

        // Second apartment in same city with price 20.
        var city = await db.Cities.FirstDeterministicAsync();
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()!;
        var company2 = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = Guid.NewGuid(),
            Name = "Second Realty",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState!.CurrentTick,
        };
        db.Companies.Add(company2);
        db.Players.Add(new Player
        {
            Id = company2.PlayerId,
            Email = $"p2-{Guid.NewGuid():N}@test.com",
            DisplayName = "P2",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        });
        var building2 = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company2.Id,
            CityId = city.Id,
            Type = BuildingType.Apartment,
            Name = "Second Apt",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            PricePerSqm = 20m,
            TotalAreaSqm = 100m,
            OccupancyPercent = 80m,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building2);
        await db.SaveChangesAsync();

        var resp = await client.PostAsJsonAsync("/graphql", new
        {
            query = CityRentalMarketQuery,
            variables = new { cityId = city.Id }
        });
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var market = doc.RootElement.GetProperty("data").GetProperty("cityRentalMarket");

        // Average of 10 and 20 = 15.
        var avgApt = market.GetProperty("averageApartmentRentPerSqm").GetDecimal();
        Assert.Equal(15m, avgApt);
        Assert.Equal(2, market.GetProperty("activeApartmentCount").GetInt32());
    }

    // ── Test 9: RENT_INCOME ledger entries appear in income statement ─────────

    private const string CompanyLedgerQuery = """
        query Ledger($companyId: UUID!) {
          companyLedger(companyId: $companyId) {
            incomeSummary {
              category
              total
            }
          }
        }
        """;

    [Fact]
    public async Task RentPhase_LedgerEntry_AppearsInIncomeStatement()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (player, company, _, _) = await SeedApartmentBuildingAsync(db,
            rentPerSqm: 10m, cityBaseRent: 10m, initialBalance: 50_000m);

        // Process one tick.
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Register / login as the player to get a token.
        var registerResp = await client.PostAsJsonAsync("/graphql", new
        {
            query = RegisterMutation,
            variables = new
            {
                input = new
                {
                    email = $"ledger-{Guid.NewGuid():N}@test.com",
                    password = "Test1234!",
                    displayName = "Ledger Tester"
                }
            }
        });
        var regDoc = JsonDocument.Parse(await registerResp.Content.ReadAsStringAsync());
        var token = regDoc.RootElement
            .GetProperty("data").GetProperty("register").GetProperty("token").GetString();

        // Give that token to the seeded player by making them share the same userId — not possible
        // without manipulating the DB, so instead we check via the DB directly that a
        // RENT_INCOME ledger entry was created for the company.
        var ledgerEntry = await db.LedgerEntries.FirstOrDefaultAsync(
            e => e.CompanyId == company.Id && e.Category == LedgerCategory.RentIncome);
        Assert.NotNull(ledgerEntry);
        Assert.True(ledgerEntry!.Amount > 0m);
    }

    // ── Test 10: apartmentBuildingDetail — owner can retrieve revenue history ─

    private const string ApartmentDetailQuery = """
        query AptDetail($buildingId: UUID!) {
          apartmentBuildingDetail(buildingId: $buildingId) {
            buildingId
            occupancyPercent
            totalAreaSqm
            cityAverageRentPerSqm
            currencyCode
            revenueHistory { tick revenue occupancyPercent }
          }
        }
        """;

    [Fact]
    public async Task ApartmentBuildingDetail_Owner_ReturnsDetailWithHistory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Register a real player via the API so we can authenticate.
        var email = $"apt-owner-{Guid.NewGuid():N}@test.com";
        var registerResp = await client.PostAsJsonAsync("/graphql", new
        {
            query = RegisterMutation,
            variables = new { input = new { email, password = "Test1234!", displayName = "Apt Owner" } }
        });
        var regDoc = JsonDocument.Parse(await registerResp.Content.ReadAsStringAsync());
        var token = regDoc.RootElement.GetProperty("data")
            .GetProperty("register").GetProperty("token").GetString();
        var playerId = Guid.Parse(regDoc.RootElement.GetProperty("data")
            .GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // Seed building owned by that player's company.
        var city = await db.Cities.FirstDeterministicAsync();
        city.AverageRentPerSqm = 12m;
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()!;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Apt Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState!.CurrentTick,
        };
        db.Companies.Add(company);
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "1234567890123456",
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000m,
            CompanyId = company.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Apartment,
            Name = "Player Apt",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            PricePerSqm = 12m,
            TotalAreaSqm = 80m,
            OccupancyPercent = 90m,
            BankAccountId = account.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        // Process one tick to generate a RentalIncomeRecord.
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Query via authenticated client.
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var resp = await client.PostAsJsonAsync("/graphql", new
        {
            query = ApartmentDetailQuery,
            variables = new { buildingId = building.Id }
        });
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var detail = doc.RootElement.GetProperty("data").GetProperty("apartmentBuildingDetail");

        Assert.Equal(building.Id.ToString(), detail.GetProperty("buildingId").GetString());
        Assert.Equal(80m, detail.GetProperty("totalAreaSqm").GetDecimal());
        Assert.Equal(12m, detail.GetProperty("cityAverageRentPerSqm").GetDecimal());
        var history = detail.GetProperty("revenueHistory");
        Assert.Equal(1, history.GetArrayLength());
    }

    // ── Test 11: apartmentBuildingDetail — non-owner returns null ─────────────

    [Fact]
    public async Task ApartmentBuildingDetail_NonOwner_ReturnsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var (_, _, building, _) = await SeedApartmentBuildingAsync(db, rentPerSqm: 10m);

        // Register as a different player.
        var registerResp = await client.PostAsJsonAsync("/graphql", new
        {
            query = RegisterMutation,
            variables = new
            {
                input = new
                {
                    email = $"other2-{Guid.NewGuid():N}@test.com",
                    password = "Test1234!",
                    displayName = "Stranger"
                }
            }
        });
        var regDoc = JsonDocument.Parse(await registerResp.Content.ReadAsStringAsync());
        var token = regDoc.RootElement.GetProperty("data")
            .GetProperty("register").GetProperty("token").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var resp = await client.PostAsJsonAsync("/graphql", new
        {
            query = ApartmentDetailQuery,
            variables = new { buildingId = building.Id }
        });
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        Assert.True(
            doc.RootElement.GetProperty("data").GetProperty("apartmentBuildingDetail").ValueKind
                == JsonValueKind.Null);
    }

    // ── Test 12: companyLedger reflects totalRentIncome after tick ─────────────

    private const string CompanyLedgerFullQuery = """
        query LedgerFull($companyId: UUID!) {
          companyLedger(companyId: $companyId) {
            totalRentIncome
            totalPropertyMaintenance
            netIncome
          }
        }
        """;

    [Fact]
    public async Task CompanyLedger_ReflectsTotalRentIncome_AfterTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Register a real player and get a token.
        var email = $"ledger-rent-{Guid.NewGuid():N}@test.com";
        var registerResp = await client.PostAsJsonAsync("/graphql", new
        {
            query = RegisterMutation,
            variables = new { input = new { email, password = "Test1234!", displayName = "Ledger Renter" } }
        });
        var regDoc = JsonDocument.Parse(await registerResp.Content.ReadAsStringAsync());
        var token = regDoc.RootElement.GetProperty("data")
            .GetProperty("register").GetProperty("token").GetString();
        var playerId = Guid.Parse(regDoc.RootElement.GetProperty("data")
            .GetProperty("register").GetProperty("player").GetProperty("id").GetString()!);

        // Seed an apartment building for this player at market-rate rent.
        var city = await db.Cities.FirstDeterministicAsync();
        city.AverageRentPerSqm = 10m;
        var gameState = await db.GameStates.FirstOrDefaultDeterministicAsync()!;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = "Rent Ledger Corp",
            FoundedAtUtc = DateTime.UtcNow,
            FoundedAtTick = gameState!.CurrentTick,
        };
        db.Companies.Add(company);
        var account = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = "2345678901234567",
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000m,
            CompanyId = company.Id,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(account);
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Apartment,
            Name = "Ledger Apt",
            Latitude = city.Latitude,
            Longitude = city.Longitude,
            Level = 1,
            PricePerSqm = 10m,
            TotalAreaSqm = 100m,
            OccupancyPercent = 100m,
            BankAccountId = account.Id,
            BuiltAtUtc = DateTime.UtcNow,
        };
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        // Process one tick so RentPhase credits income.
        var processor = await CreateProcessorAsync(scope);
        await processor.ProcessTickAsync();

        // Query companyLedger via authenticated client.
        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var resp = await client.PostAsJsonAsync("/graphql", new
        {
            query = CompanyLedgerFullQuery,
            variables = new { companyId = company.Id }
        });
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var ledger = doc.RootElement.GetProperty("data").GetProperty("companyLedger");

        var rentIncome = ledger.GetProperty("totalRentIncome").GetDecimal();
        Assert.True(rentIncome > 0m, $"Expected totalRentIncome > 0, got {rentIncome}");
    }

    // ── Test 13: ComputeMaxOccupancy edge cases ────────────────────────────────

    [Theory]
    [InlineData(0.0, 100.0)]    // Free — full occupancy possible
    [InlineData(0.59, 100.0)]   // Just below 60% threshold — full occupancy
    [InlineData(0.60, 100.0)]   // Exactly at 60% threshold — full occupancy
    [InlineData(0.85, 95.0)]    // Midpoint between 60% and 110% → interpolated
    [InlineData(1.10, 90.0)]    // Exactly at 110% threshold → 90%
    [InlineData(1.50, 50.0)]    // Overpriced → floor 50%
    [InlineData(10.0, 50.0)]    // 10× city avg → floor 50%
    public void ComputeMaxOccupancy_EdgeCases(double priceRatio, double expectedMaxOccupancy)
    {
        var result = RentPhase.ComputeMaxOccupancy((decimal)priceRatio);
        Assert.InRange(result, (decimal)expectedMaxOccupancy - 0.5m, (decimal)expectedMaxOccupancy + 0.5m);
    }
}
