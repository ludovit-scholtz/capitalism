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

public sealed class EconomicCycleAndMarketEventTests
{
    private static async Task<JsonElement> ExecuteGraphQlAsync(HttpClient client, string query, object? variables = null, string? token = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/graphql");
        request.Content = new StringContent(JsonSerializer.Serialize(new { query, variables }), Encoding.UTF8, "application/json");
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JsonElement>(payload);
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
            new { input = new { email, displayName = "Macro Tester", password = "TestPass123!" } });

        return result.GetProperty("data").GetProperty("register").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task EconomicCycle_Transitions_FromExpansionToPeak_OnMonthlyBoundary()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cycle = await db.EconomicCycles.FirstAsync();
        var gameState = await db.GameStates.FirstAsync();

        cycle.Phase = EconomicCyclePhase.Expansion;
        cycle.PhaseStartedTick = 0;
        cycle.ExpectedDurationTicks = GameConstants.TicksPerMonth;
        cycle.IntensityFactor = 1.2m;
        gameState.CurrentTick = GameConstants.TicksPerMonth - 1;
        await db.SaveChangesAsync();

        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();

        var reloaded = await db.EconomicCycles.AsNoTracking().FirstAsync();
        Assert.Equal(EconomicCyclePhase.Peak, reloaded.Phase);
        Assert.Equal(1.05m, reloaded.IntensityFactor);
    }

    [Fact]
    public async Task EconomicCycle_Peak_EmitsRecessionWarning48TicksBeforeStart()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cycle = await db.EconomicCycles.FirstAsync();
        var gameState = await db.GameStates.FirstAsync();

        cycle.Phase = EconomicCyclePhase.Peak;
        cycle.PhaseStartedTick = 0;
        cycle.ExpectedDurationTicks = 100;
        cycle.RecessionWarningSentForTick = null;
        gameState.CurrentTick = 52; // 48 ticks before phase end
        await db.SaveChangesAsync();

        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();

        var notifications = await db.PlayerNotifications
            .Where(notification => notification.Type == PlayerNotificationType.EconomicAlert && notification.Title == "Recession warning")
            .ToListAsync();
        Assert.NotEmpty(notifications);
    }

    [Fact]
    public async Task ActiveInterestRateEvent_RepricesVariableLoanOnTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        _ = factory.CreateClient();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var gameState = await db.GameStates.FirstAsync();
        gameState.CurrentTick = 200;

        var bankBuilding = await db.Buildings.FirstAsync(b => b.Type == BuildingType.Bank);
        bankBuilding.LendingInterestRatePercent = 10m;

        var borrowerPlayer = new Player
        {
            Id = Guid.NewGuid(),
            Email = $"macro-loan-{Guid.NewGuid():N}@example.com",
            DisplayName = "Loan Borrower",
            PasswordHash = "hash",
            Role = PlayerRole.Player,
        };
        var borrowerCompany = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = borrowerPlayer.Id,
            Name = "Borrower Co",
            Cash = 1_000_000m,
        };
        db.Players.Add(borrowerPlayer);
        db.Companies.Add(borrowerCompany);

        var offer = new LoanOffer
        {
            Id = Guid.NewGuid(),
            BankBuildingId = bankBuilding.Id,
            LenderCompanyId = bankBuilding.CompanyId,
            AnnualInterestRatePercent = 10m,
            MaxPrincipalPerLoan = 100_000m,
            TotalCapacity = 100_000m,
            UsedCapacity = 50_000m,
            DurationTicks = 100,
            IsActive = true,
            CreatedAtTick = gameState.CurrentTick,
            CreatedAtUtc = DateTime.UtcNow,
        };
        db.LoanOffers.Add(offer);

        var loan = new Loan
        {
            Id = Guid.NewGuid(),
            LoanOfferId = offer.Id,
            BorrowerCompanyId = borrowerCompany.Id,
            BankBuildingId = bankBuilding.Id,
            LenderCompanyId = bankBuilding.CompanyId,
            OriginalPrincipal = 50_000m,
            RemainingPrincipal = 50_000m,
            AnnualInterestRatePercent = 10m,
            DurationTicks = 100,
            StartTick = gameState.CurrentTick,
            DueTick = gameState.CurrentTick + 100,
            NextPaymentTick = gameState.CurrentTick + 1,
            PaymentAmount = 500m,
            PaymentsMade = 0,
            TotalPayments = 100,
            Status = LoanStatus.Active,
            AcceptedAtUtc = DateTime.UtcNow,
        };
        db.Loans.Add(loan);

        db.MarketEvents.Add(new MarketEvent
        {
            Id = Guid.NewGuid(),
            EventType = MarketEventType.InterestRateChange,
            Title = "Rate shock",
            Description = "Rate multiplier 1.2x",
            MagnitudeMultiplier = 1.2m,
            StartsAtTick = gameState.CurrentTick,
            ExpiresAtTick = gameState.CurrentTick + 24,
            CreatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var phases = scope.ServiceProvider.GetServices<ITickPhase>();
        var processor = new TickProcessor(db, phases, new NullLogger<TickProcessor>());
        await processor.ProcessTickAsync();

        var repriced = await db.Loans.AsNoTracking().FirstAsync(l => l.Id == loan.Id);
        Assert.Equal(12m, repriced.AnnualInterestRatePercent);
    }

    [Fact]
    public async Task EconomyQueries_ReturnCurrentCycle_ActiveEvents_AndHistory()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var gameState = await db.GameStates.FirstAsync();
            gameState.CurrentTick = 150;

            var cycle = await db.EconomicCycles.FirstAsync();
            cycle.Phase = EconomicCyclePhase.Recession;
            cycle.PhaseStartedTick = 120;
            cycle.ExpectedDurationTicks = 200;
            cycle.IntensityFactor = 0.7m;

            var wood = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");
            db.MarketEvents.Add(new MarketEvent
            {
                Id = Guid.NewGuid(),
                EventType = MarketEventType.CommodityShock,
                Title = "Wood shock",
                Description = "Wood prices are elevated",
                AffectedResourceTypeId = wood.Id,
                MagnitudeMultiplier = 1.25m,
                StartsAtTick = 140,
                ExpiresAtTick = 220,
                CreatedAtUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var token = await RegisterAndGetTokenAsync(client, $"macro-query-{Guid.NewGuid():N}@example.com");
        var result = await ExecuteGraphQlAsync(
            client,
            """
            query EconomySnapshot {
              getCurrentEconomicCycle { phase intensityFactor ticksRemaining }
              getActiveMarketEvents { eventType title magnitudeMultiplier affectedResourceSlug }
              getEconomicHistory(last: 100) { tick phase intensityFactor }
            }
            """,
            token: token);

        var data = result.GetProperty("data");
        Assert.Equal("RECESSION", data.GetProperty("getCurrentEconomicCycle").GetProperty("phase").GetString());
        Assert.NotEqual(0m, data.GetProperty("getCurrentEconomicCycle").GetProperty("intensityFactor").GetDecimal());
        Assert.NotEmpty(data.GetProperty("getActiveMarketEvents").EnumerateArray());
        Assert.NotEmpty(data.GetProperty("getEconomicHistory").EnumerateArray());
    }

    [Fact]
    public async Task LedgerDrillDown_IncludesEventTag_WhenEntryFallsWithinCommodityShock()
    {
        await using var factory = new ApiWebApplicationFactory();
        var client = factory.CreateClient();
        var email = $"macro-ledger-{Guid.NewGuid():N}@example.com";
        var token = await RegisterAndGetTokenAsync(client, email);

        Guid companyId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var player = await db.Players.FirstAsync(p => p.Email == email);
            var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
            var resource = await db.ResourceTypes.FirstAsync(r => r.Slug == "wood");

            var company = new Company
            {
                Id = Guid.NewGuid(),
                PlayerId = player.Id,
                Name = "Ledger Event Co",
                Cash = 1_000_000m,
            };
            companyId = company.Id;
            db.Companies.Add(company);

            var building = new Building
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                CityId = city.Id,
                Type = BuildingType.Factory,
                Name = "Event Factory",
                Level = 1,
            };
            db.Buildings.Add(building);

            var gs = await db.GameStates.FirstAsync();
            gs.CurrentTick = 300;

            db.MarketEvents.Add(new MarketEvent
            {
                Id = Guid.NewGuid(),
                EventType = MarketEventType.CommodityShock,
                Title = "Wood shock",
                Description = "Wood prices surged",
                AffectedResourceTypeId = resource.Id,
                MagnitudeMultiplier = 1.25m,
                StartsAtTick = 250,
                ExpiresAtTick = 350,
            });

            db.LedgerEntries.Add(new LedgerEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                BuildingId = building.Id,
                Category = LedgerCategory.PurchasingCost,
                Description = "Purchase: raw material",
                Amount = -120m,
                RecordedAtTick = 300,
                RecordedAtUtc = DateTime.UtcNow,
                ResourceTypeId = resource.Id,
            });
            await db.SaveChangesAsync();
        }

        var result = await ExecuteGraphQlAsync(
            client,
            """
            query LedgerWithEvent($companyId: UUID!, $category: String!) {
              ledgerDrillDown(companyId: $companyId, category: $category) {
                id
                eventTag
                eventDescription
              }
            }
            """,
            new { companyId, category = LedgerCategory.PurchasingCost },
            token);

        var entries = result.GetProperty("data").GetProperty("ledgerDrillDown").EnumerateArray().ToList();
        Assert.NotEmpty(entries);
        Assert.Contains(entries, entry =>
            entry.TryGetProperty("eventTag", out var tag)
            && tag.GetString() is { } tagText
            && tagText.Contains("Commodity shock", StringComparison.OrdinalIgnoreCase));
    }
}
