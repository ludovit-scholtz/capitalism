using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

/// <summary>
/// Integration tests for the getMineExtractionHistory and getMineDepletionForecast GraphQL queries
/// and for MiningPhase's per-tick extraction record persistence and 90-day pruning.
/// </summary>
public sealed class MineExtractionHistoryTests
{
    // ── Helpers ────────────────────────────────────────────────────────────────

    private static async Task<JsonElement> ExecuteGraphQlAsync(
        HttpClient client,
        string query,
        object? variables = null,
        string? token = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(
            JsonSerializer.Serialize(new { query, variables }),
            Encoding.UTF8,
            "application/json");

        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(body);
    }

    /// <summary>Registers a player and returns a JWT token.</summary>
    private static async Task<string> RegisterAndGetTokenAsync(HttpClient client, string suffix = "")
    {
        var email = $"mine-hist-{suffix}{Guid.NewGuid():N}@test.com";
        const string password = "TestPass123!";
        var result = await ExecuteGraphQlAsync(client, """
            mutation Register($input: RegisterInput!) {
              register(input: $input) { token }
            }
            """, new { input = new { email, displayName = "MineHistTester", password } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    /// <summary>Seeds a MINE building with a finite deposit lot, returns its ID.</summary>
    private static async Task<(Guid buildingId, Guid lotId)> SeedMineBuildingAsync(
        AppDbContext db,
        Guid playerId,
        decimal quantity = 1000m,
        decimal? originalQuantity = null)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Name = $"MineCo {Guid.NewGuid():N}",
            Cash = 0m
        };
        db.Companies.Add(company);

        var resourceType = await db.ResourceTypes.FirstAsync();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Mine,
            Latitude = 48.15,
            Longitude = 17.11,
            Name = "Test Mine",
            PowerStatus = PowerStatus.Powered,
            PowerConsumption = 2m,
        };
        db.Buildings.Add(building);

        var miningUnit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Mining,
            GridX = 0,
            GridY = 0,
            Level = 1,
            ResourceTypeId = resourceType.Id,
        };
        db.BuildingUnits.Add(miningUnit);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            CityId = city.Id,
            Latitude = 48.15,
            Longitude = 17.11,
            Price = 75_000m,
            BasePrice = 75_000m,
            District = "Test",
            SuitableTypes = "MINE",
            ResourceTypeId = resourceType.Id,
            MaterialQuantity = quantity,
            OriginalMaterialQuantity = originalQuantity ?? quantity,
            MaterialQuality = 0.8m,
            OwnerCompanyId = company.Id,
        };
        db.BuildingLots.Add(lot);

        await db.SaveChangesAsync();
        return (building.Id, lot.Id);
    }

    /// <summary>Seeds explicit extraction records for a building.</summary>
    private static async Task SeedExtractionRecordsAsync(
        AppDbContext db,
        Guid buildingId,
        IEnumerable<(long tick, decimal amount, decimal efficiency, decimal reserve)> rows)
    {
        foreach (var (tick, amount, efficiency, reserve) in rows)
        {
            db.MineExtractionRecords.Add(new MineExtractionRecord
            {
                Id = Guid.NewGuid(),
                BuildingId = buildingId,
                Tick = tick,
                ExtractedAmount = amount,
                EfficiencyPercent = efficiency,
                ReserveRemaining = reserve,
            });
        }
        await db.SaveChangesAsync();
    }

    private static Guid GetPlayerIdFromToken(string token)
    {
        var parts = token.Split('.');
        if (parts.Length < 2) throw new InvalidOperationException("Invalid JWT");
        var payload = parts[1];
        // Pad base64url
        var padded = payload + new string('=', (4 - payload.Length % 4) % 4);
        var bytes = Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/'));
        var json = JsonSerializer.Deserialize<JsonElement>(bytes);
        // JWT NameIdentifier serializes to its full XML schema URI as key
        const string nameIdKey = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier";
        if (json.TryGetProperty(nameIdKey, out var idEl))
            return Guid.Parse(idEl.GetString()!);
        // Fallback: check short form
        if (json.TryGetProperty("nameid", out var shortEl))
            return Guid.Parse(shortEl.GetString()!);
        // Fallback: check "sub"
        if (json.TryGetProperty("sub", out var subEl))
            return Guid.Parse(subEl.GetString()!);
        throw new InvalidOperationException($"No player ID claim found in token. Keys: {string.Join(", ", json.EnumerateObject().Select(p => p.Name))}");
    }

    // ── Tests ──────────────────────────────────────────────────────────────────

    [Fact]
    public void MineExtractionIntelligenceCalculator_BurnRateAndDepletionCalculations_WorkAcrossEdgeCases()
    {
        var uniformRate = MineExtractionIntelligenceCalculator.ComputeBurnRatePerTick([8m, 8m, 8m, 8m]);
        Assert.Equal(8m, uniformRate);

        var zeroRate = MineExtractionIntelligenceCalculator.ComputeBurnRatePerTick([0m, 0m, 0m]);
        Assert.Equal(0m, zeroRate);

        var mixedRate = MineExtractionIntelligenceCalculator.ComputeBurnRatePerTick([0m, 10m, 20m, 30m]);
        Assert.Equal(15m, mixedRate);

        var expectedTick = MineExtractionIntelligenceCalculator.ComputeExpectedDepletionTick(
            currentTick: 1000,
            currentReserve: 500m,
            burnRatePerTick: 10m);
        Assert.Equal(1050, expectedTick);

        var depletedTick = MineExtractionIntelligenceCalculator.ComputeExpectedDepletionTick(
            currentTick: 2000,
            currentReserve: 0m,
            burnRatePerTick: 5m);
        Assert.Equal(1999, depletedTick);
    }

    [Fact]
    public void MineExtractionIntelligenceCalculator_QualityInflectionTick_IsComputedFromThreshold()
    {
        var inflectionTick = MineExtractionIntelligenceCalculator.ComputeQualityDecayInflectionTick(
            currentTick: 100,
            currentReserve: 900m,
            originalReserve: 1000m,
            burnRatePerTick: 10m);

        Assert.Equal(120, inflectionTick);

        var alreadyPastInflection = MineExtractionIntelligenceCalculator.ComputeQualityDecayInflectionTick(
            currentTick: 400,
            currentReserve: 650m,
            originalReserve: 1000m,
            burnRatePerTick: 10m);
        Assert.Equal(400, alreadyPastInflection);
    }

    [Fact]
    public async Task GetMineExtractionHistory_ReturnsRecordsSortedByTickDescending()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "sort");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId);
        await SeedExtractionRecordsAsync(db, buildingId, [
            (tick: 100, amount: 5m, efficiency: 0.8m, reserve: 995m),
            (tick: 200, amount: 6m, efficiency: 0.79m, reserve: 989m),
            (tick: 300, amount: 7m, efficiency: 0.78m, reserve: 982m),
        ]);

        var result = await ExecuteGraphQlAsync(client, """
            query GetHist($buildingId: UUID!, $days: Int!) {
              getMineExtractionHistory(buildingId: $buildingId, days: $days) {
                tick extractedAmount efficiencyPercent reserveRemaining
              }
            }
            """, new { buildingId, days = 30 }, token);

        var records = result.GetProperty("data").GetProperty("getMineExtractionHistory").EnumerateArray().ToList();
        Assert.Equal(3, records.Count);
        // Ordered descending
        Assert.True(records[0].GetProperty("tick").GetInt64() > records[1].GetProperty("tick").GetInt64());
        Assert.True(records[1].GetProperty("tick").GetInt64() > records[2].GetProperty("tick").GetInt64());
    }

    [Fact]
    public async Task GetMineExtractionIntelligence_ReturnsDailySeriesAndForecast()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "intel");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId, quantity: 900m, originalQuantity: 1000m);
        await SeedExtractionRecordsAsync(db, buildingId, [
            (tick: 12, amount: 12m, efficiency: 1.0m, reserve: 988m),
            (tick: 24, amount: 8m, efficiency: 1.0m, reserve: 980m),
            (tick: 36, amount: 10m, efficiency: 0.95m, reserve: 970m),
            (tick: 48, amount: 10m, efficiency: 0.95m, reserve: 960m),
        ]);

        var result = await ExecuteGraphQlAsync(client, """
            query GetIntelligence($buildingId: UUID!, $days: Int!) {
              getMineExtractionIntelligence(buildingId: $buildingId, days: $days) {
                burnRatePerTick
                burnRatePerDay
                expectedDepletionTick
                qualityDecayInflectionTick
                estimatedGameDaysRemaining
                currentReserve
                originalReserve
                dailyExtraction {
                  dayIndex
                  extractedAmount
                  efficiencyPercent
                  reserveRemaining
                }
              }
            }
            """, new { buildingId, days = 30 }, token);

        var intelligence = result.GetProperty("data").GetProperty("getMineExtractionIntelligence");
        Assert.True(intelligence.GetProperty("burnRatePerTick").GetDecimal() > 0m);
        Assert.True(intelligence.GetProperty("burnRatePerDay").GetDecimal() > 0m);
        Assert.True(intelligence.GetProperty("expectedDepletionTick").GetInt64() > 0);
        Assert.True(intelligence.GetProperty("qualityDecayInflectionTick").GetInt64() > 0);

        var dailyExtraction = intelligence.GetProperty("dailyExtraction").EnumerateArray().ToList();
        Assert.NotEmpty(dailyExtraction);
        Assert.All(dailyExtraction, item => Assert.True(item.GetProperty("extractedAmount").GetDecimal() > 0m));
    }

    [Fact]
    public async Task GetMineExtractionIntelligence_DifferentOwnerGetsNull()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var ownerToken = await RegisterAndGetTokenAsync(client, "intel-owner");
        var otherToken = await RegisterAndGetTokenAsync(client, "intel-other");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = GetPlayerIdFromToken(ownerToken);

        var (buildingId, _) = await SeedMineBuildingAsync(db, ownerId, quantity: 900m, originalQuantity: 1000m);
        await SeedExtractionRecordsAsync(db, buildingId, [
            (tick: 12, amount: 12m, efficiency: 1.0m, reserve: 988m),
        ]);

        var result = await ExecuteGraphQlAsync(client, """
            query GetIntelligence($buildingId: UUID!, $days: Int!) {
              getMineExtractionIntelligence(buildingId: $buildingId, days: $days) {
                burnRatePerTick
              }
            }
            """, new { buildingId, days = 30 }, otherToken);

        Assert.Equal(JsonValueKind.Null, result.GetProperty("data").GetProperty("getMineExtractionIntelligence").ValueKind);
    }

    [Fact]
    public async Task GetMineExtractionIntelligence_UnauthenticatedReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var result = await ExecuteGraphQlAsync(client, """
            query GetIntelligence($buildingId: UUID!, $days: Int!) {
              getMineExtractionIntelligence(buildingId: $buildingId, days: $days) {
                burnRatePerTick
              }
            }
            """, new { buildingId = Guid.NewGuid(), days = 30 });

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    [Fact]
    public async Task GetMineExtractionIntelligence_DepletedMineReportsPastDepletionTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "intel-depleted");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId, quantity: 0m, originalQuantity: 1000m);
        await SeedExtractionRecordsAsync(db, buildingId, [
            (tick: 10, amount: 12m, efficiency: 1.0m, reserve: 988m),
            (tick: 11, amount: 15m, efficiency: 1.0m, reserve: 973m),
        ]);

        var result = await ExecuteGraphQlAsync(client, """
            query GetIntelligence($buildingId: UUID!, $days: Int!) {
              getMineExtractionIntelligence(buildingId: $buildingId, days: $days) {
                currentTick
                expectedDepletionTick
              }
            }
            """, new { buildingId, days = 30 }, token);

        var intelligence = result.GetProperty("data").GetProperty("getMineExtractionIntelligence");
        var currentTick = intelligence.GetProperty("currentTick").GetInt64();
        var depletionTick = intelligence.GetProperty("expectedDepletionTick").GetInt64();
        Assert.True(depletionTick < currentTick);
    }

    [Fact]
    public async Task GetMineExtractionHistory_ReturnsEmptyForDifferentOwner()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var ownerToken = await RegisterAndGetTokenAsync(client, "owner");
        var otherToken = await RegisterAndGetTokenAsync(client, "other");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ownerId = GetPlayerIdFromToken(ownerToken);

        var (buildingId, _) = await SeedMineBuildingAsync(db, ownerId);
        await SeedExtractionRecordsAsync(db, buildingId, [
            (tick: 100, amount: 5m, efficiency: 0.8m, reserve: 995m),
        ]);

        // Query as different player — should get empty list
        var result = await ExecuteGraphQlAsync(client, """
            query GetHist($buildingId: UUID!, $days: Int!) {
              getMineExtractionHistory(buildingId: $buildingId, days: $days) { tick }
            }
            """, new { buildingId, days = 30 }, otherToken);

        var records = result.GetProperty("data").GetProperty("getMineExtractionHistory").EnumerateArray().ToList();
        Assert.Empty(records);
    }

    [Fact]
    public async Task GetMineExtractionHistory_UnauthenticatedReturnsError()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var buildingId = Guid.NewGuid();

        var result = await ExecuteGraphQlAsync(client, """
            query GetHist($buildingId: UUID!, $days: Int!) {
              getMineExtractionHistory(buildingId: $buildingId, days: $days) { tick }
            }
            """, new { buildingId, days = 30 }, token: null);

        Assert.True(result.TryGetProperty("errors", out var errors));
        Assert.NotEmpty(errors.EnumerateArray());
    }

    [Fact]
    public async Task GetMineDepletionForecast_ReturnsLinearProjection()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "forecast");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        // 1000 units remaining, 1000 original, 10 units/tick average → 100 ticks to depletion
        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId, quantity: 1000m, originalQuantity: 1000m);
        // Seed records with consistent 10 units/tick
        await SeedExtractionRecordsAsync(db, buildingId, Enumerable.Range(1, 20).Select(i =>
            ((long)i, 10m, 1.0m, 1000m - i * 10m)));

        var result = await ExecuteGraphQlAsync(client, """
            query GetForecast($buildingId: UUID!) {
              getMineDepletionForecast(buildingId: $buildingId) {
                averageExtractionRatePerTick
                estimatedGameDaysRemaining
                depletionTick
                critical20PctTick
                critical5PctTick
                currentReserve
              }
            }
            """, new { buildingId }, token);

        var forecast = result.GetProperty("data").GetProperty("getMineDepletionForecast");
        var avgRate = forecast.GetProperty("averageExtractionRatePerTick").GetDecimal();
        Assert.InRange(avgRate, 9.9m, 10.1m);

        var currentReserve = forecast.GetProperty("currentReserve").GetDecimal();
        Assert.True(currentReserve > 0m);

        // Depletion tick should be set
        Assert.NotNull(forecast.TryGetProperty("depletionTick", out var depTickEl) ? depTickEl : (JsonElement?)null);
    }

    [Fact]
    public async Task GetMineDepletionForecast_ReturnsNullForecastFieldsWhenNoHistory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "nohist");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId, quantity: 500m, originalQuantity: 1000m);
        // No extraction records seeded

        var result = await ExecuteGraphQlAsync(client, """
            query GetForecast($buildingId: UUID!) {
              getMineDepletionForecast(buildingId: $buildingId) {
                averageExtractionRatePerTick
                depletionTick
                currentReserve
              }
            }
            """, new { buildingId }, token);

        var forecast = result.GetProperty("data").GetProperty("getMineDepletionForecast");
        Assert.Equal(JsonValueKind.Null, forecast.GetProperty("averageExtractionRatePerTick").ValueKind);
        Assert.Equal(JsonValueKind.Null, forecast.GetProperty("depletionTick").ValueKind);
        var currentReserve = forecast.GetProperty("currentReserve").GetDecimal();
        Assert.Equal(500m, currentReserve);
    }

    [Fact]
    public async Task GetMineDepletionForecast_ReturnsZeroEstimateWhenAlreadyDepleted()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var token = await RegisterAndGetTokenAsync(client, "depleted");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var playerId = GetPlayerIdFromToken(token);

        // Reserve = 0 → already depleted
        var (buildingId, _) = await SeedMineBuildingAsync(db, playerId, quantity: 0m, originalQuantity: 1000m);

        var result = await ExecuteGraphQlAsync(client, """
            query GetForecast($buildingId: UUID!) {
              getMineDepletionForecast(buildingId: $buildingId) {
                estimatedGameDaysRemaining
                currentReserve
              }
            }
            """, new { buildingId }, token);

        var forecast = result.GetProperty("data").GetProperty("getMineDepletionForecast");
        var remaining = forecast.GetProperty("estimatedGameDaysRemaining").GetDecimal();
        Assert.Equal(0m, remaining);
        var currentReserve = forecast.GetProperty("currentReserve").GetDecimal();
        Assert.Equal(0m, currentReserve);
    }

    [Fact]
    public async Task MiningPhase_WritesExtractionRecordEachTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Seed: create a player and build a mine
        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"phase-test-{Guid.NewGuid():N}@test.com",
            DisplayName = "Phase Tester",
            PasswordHash = "hash",
            Role = PlayerRole.Player
        };
        db.Players.Add(player);

        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Phase Co", Cash = 0m };
        db.Companies.Add(company);

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var resourceType = await db.ResourceTypes.FirstAsync();

        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Mine,
            Latitude = 48.15,
            Longitude = 17.11,
            Name = "Phase Mine",
            PowerStatus = PowerStatus.Powered,
            PowerConsumption = 2m,
        };
        db.Buildings.Add(building);

        // Bank account required to avoid OperatingCostPhase suspending the building.
        var bankAccount = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = $"7701{Guid.NewGuid():N}"[..16],
            CompanyId = company.Id,
            CurrencyCode = city.CurrencyCode,
            Balance = 10_000_000m,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.BankAccounts.Add(bankAccount);
        building.BankAccountId = bankAccount.Id;

        var unit = new BuildingUnit
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            UnitType = UnitType.Mining,
            GridX = 0,
            GridY = 0,
            Level = 2,
            ResourceTypeId = resourceType.Id,
        };
        db.BuildingUnits.Add(unit);

        var lot = new BuildingLot
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            CityId = city.Id,
            Latitude = 48.15,
            Longitude = 17.11,
            Price = 75_000m,
            BasePrice = 75_000m,
            District = "Test",
            SuitableTypes = "MINE",
            ResourceTypeId = resourceType.Id,
            MaterialQuantity = 10_000m,
            OriginalMaterialQuantity = 10_000m,
            MaterialQuality = 0.9m,
            OwnerCompanyId = company.Id,
        };
        db.BuildingLots.Add(lot);
        await db.SaveChangesAsync();

        // Run the full tick processor (includes MiningPhase)
        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var logger = NullLogger<TickProcessor>.Instance;
        var processor = new TickProcessor(db, phases, logger);
        await processor.ProcessTickAsync();

        // After 1 tick, a MineExtractionRecord should have been written
        var newRecord = await db.MineExtractionRecords
            .Where(r => r.BuildingId == building.Id)
            .FirstOrDefaultAsync();
        Assert.NotNull(newRecord);
        Assert.True(newRecord.ExtractedAmount > 0m, "MiningPhase should have written a positive extraction amount.");
        Assert.True(newRecord.ReserveRemaining < 10_000m, "Reserve should have decreased after extraction.");
    }
}
