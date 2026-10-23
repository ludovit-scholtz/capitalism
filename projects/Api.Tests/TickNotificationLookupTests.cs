using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

/// <summary>
/// Tick phases ask "does this player already have this unread notification?" for every building or item they might notify about.
/// That used to be one synchronous database query per question (650 ms of a tick for the power grid alone, #646); the answer now
/// comes from one snapshot per notification type per tick. These pin the answers.
/// </summary>
public sealed class TickNotificationLookupTests
{
    // ── The lookup itself ────────────────────────────────────────────────────

    private static AppDbContext NewDb()
        => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase($"tick-notification-{Guid.NewGuid():N}").Options);

    private static TickContext ContextFor(AppDbContext db)
        => new() { Db = db, GameState = new GameState { CurrentTick = 10 } };

    private static PlayerNotification Notification(
        Guid playerId,
        string type = PlayerNotificationType.ProductionHalted,
        Guid? relatedEntityId = null,
        Guid? companyId = null,
        Guid? buildingId = null,
        Guid? loanId = null,
        bool isRead = false,
        DateTime? expiresAtUtc = null)
        => new()
        {
            Id = Guid.NewGuid(), PlayerId = playerId, Type = type, Title = "t", Message = "m", RelatedEntityId = relatedEntityId, CompanyId = companyId,
            BuildingId = buildingId, LoanId = loanId, IsRead = isRead, ExpiresAtUtc = expiresAtUtc,
        };

    [Fact]
    public async Task AnUnreadNotificationWithTheSameKey_IsFound()
    {
        await using var db = NewDb();
        var player = Guid.NewGuid();
        var (company, building, related) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        db.PlayerNotifications.Add(Notification(player, relatedEntityId: related, companyId: company, buildingId: building));
        await db.SaveChangesAsync();
        var context = ContextFor(db);

        Assert.True(context.HasUnreadNotification(player, PlayerNotificationType.ProductionHalted, related, company, building));
    }

    [Fact]
    public async Task EveryPartOfTheKeyMatters()
    {
        await using var db = NewDb();
        var player = Guid.NewGuid();
        var (company, building, related, loan) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        db.PlayerNotifications.Add(Notification(player, relatedEntityId: related, companyId: company, buildingId: building, loanId: loan));
        await db.SaveChangesAsync();
        var context = ContextFor(db);
        const string type = PlayerNotificationType.ProductionHalted;

        Assert.True(context.HasUnreadNotification(player, type, related, company, building, loan));
        Assert.False(context.HasUnreadNotification(Guid.NewGuid(), type, related, company, building, loan));
        Assert.False(context.HasUnreadNotification(player, type, Guid.NewGuid(), company, building, loan));
        Assert.False(context.HasUnreadNotification(player, type, related, Guid.NewGuid(), building, loan));
        Assert.False(context.HasUnreadNotification(player, type, related, company, Guid.NewGuid(), loan));
        Assert.False(context.HasUnreadNotification(player, type, related, company, building, Guid.NewGuid()));
        Assert.False(context.HasUnreadNotification(player, type, related, company, building)); // a missing part is a different key
    }

    [Fact]
    public async Task OnlyUnreadNotUnexpiredNotificationsOfTheSameTypeCount()
    {
        await using var db = NewDb();
        var player = Guid.NewGuid();
        var building = Guid.NewGuid();
        db.PlayerNotifications.AddRange(
            Notification(player, buildingId: building, isRead: true),
            Notification(player, buildingId: building, expiresAtUtc: DateTime.UtcNow.AddMinutes(-1)),
            Notification(player, PlayerNotificationType.PriceSpike, buildingId: building));
        await db.SaveChangesAsync();
        var context = ContextFor(db);

        Assert.False(context.HasUnreadNotification(player, PlayerNotificationType.ProductionHalted, buildingId: building));
        Assert.True(context.HasUnreadNotification(player, PlayerNotificationType.PriceSpike, buildingId: building));
    }

    [Fact]
    public async Task ANotificationThatHasNotExpiredYet_StillCounts()
    {
        await using var db = NewDb();
        var player = Guid.NewGuid();
        db.PlayerNotifications.Add(Notification(player, expiresAtUtc: DateTime.UtcNow.AddDays(7)));
        await db.SaveChangesAsync();

        Assert.True(ContextFor(db).HasUnreadNotification(player, PlayerNotificationType.ProductionHalted));
    }

    [Fact]
    public async Task NotificationsAddedDuringTheTick_AreNotVisibleToTheLookup()
    {
        // Same as the database query it replaces: only saved rows are seen, so two phases may still notify the same player once each.
        await using var db = NewDb();
        var player = Guid.NewGuid();
        var context = ContextFor(db);
        Assert.False(context.HasUnreadNotification(player, PlayerNotificationType.ProductionHalted));

        db.PlayerNotifications.Add(Notification(player));
        await db.SaveChangesAsync();

        Assert.False(context.HasUnreadNotification(player, PlayerNotificationType.ProductionHalted));
        Assert.True(ContextFor(db).HasUnreadNotification(player, PlayerNotificationType.ProductionHalted)); // the next tick sees it
    }

    // ── The power grid phase, end to end ─────────────────────────────────────

    private const int Consumers = 6;

    private static async Task<(Guid PlayerId, List<Guid> BuildingIds)> SeedStarvedCityAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = new City
        {
            Id = Guid.NewGuid(), Name = $"Starved {Guid.NewGuid():N}", CountryCode = "TS", CurrencyCode = "EUR", Latitude = 48.3, Longitude = 17.3,
            Population = 50_000, AverageRentPerSqm = 20m, BaseSalaryPerManhour = 10m,
        };
        var player = new Player { Id = Guid.NewGuid(), Email = $"starved-{Guid.NewGuid():N}@test.local", DisplayName = "Starved", PasswordHash = "x", Role = PlayerRole.Player, OnboardingCompletedAtUtc = DateTime.UtcNow };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Starved Co", FoundedAtUtc = DateTime.UtcNow, FoundedAtTick = 1 };
        var account = new BankAccount { Id = Guid.NewGuid(), CompanyId = company.Id, CurrencyCode = "EUR", Balance = 10_000_000m, AccountNumber = Random.Shared.NextInt64(7_300_000_000_000_000, 7_399_999_999_999_999).ToString(), CreatedAtUtc = DateTime.UtcNow };
        db.Cities.Add(city);
        db.Players.Add(player);
        db.Companies.Add(company);
        db.BankAccounts.Add(account);

        // A plant far too small for the city: one consumer is constrained, all the others go offline.
        db.Buildings.Add(new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.PowerPlant, PowerPlantType = PowerPlantType.Solar, PowerOutput = 0.001m,
            Name = "Tiny Plant", Latitude = city.Latitude, Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, Level = 1, BankAccountId = account.Id,
        });
        var buildingIds = new List<Guid>();
        for (var i = 0; i < Consumers; i++)
        {
            var building = new Building
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = $"Starved Factory {i}", Latitude = city.Latitude,
                Longitude = city.Longitude, BuiltAtUtc = DateTime.UtcNow, Level = 1, PowerConsumption = 10m, BankAccountId = account.Id,
            };
            db.Buildings.Add(building);
            buildingIds.Add(building.Id);
        }

        await db.SaveChangesAsync();
        return (player.Id, buildingIds);
    }

    private static async Task<List<PlayerNotification>> HaltNoticesAsync(ApiWebApplicationFactory factory, Guid playerId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().PlayerNotifications.AsNoTracking()
            .Where(n => n.PlayerId == playerId && n.Type == PlayerNotificationType.ProductionHalted).ToListAsync();
    }

    private static async Task RunTickAsync(ApiWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        _ = await scope.ServiceProvider.GetRequiredService<TickProcessor>().ProcessTickAsync();
    }

    [Fact]
    public async Task EachOfflineBuilding_GetsOneProductionHaltedNotice_AndNoDuplicatesOnTheNextTick()
    {
        await using var factory = new ApiWebApplicationFactory();
        var (playerId, buildingIds) = await SeedStarvedCityAsync(factory);

        await RunTickAsync(factory);
        var first = await HaltNoticesAsync(factory, playerId);

        Assert.Equal(Consumers - 1, first.Count); // the best-supplied consumer is only constrained
        Assert.Equal(first.Count, first.Select(n => n.BuildingId).Distinct().Count());
        Assert.All(first, notice => Assert.Contains(notice.BuildingId!.Value, buildingIds));

        await RunTickAsync(factory);

        Assert.Equal(first.Count, (await HaltNoticesAsync(factory, playerId)).Count);
    }
}
