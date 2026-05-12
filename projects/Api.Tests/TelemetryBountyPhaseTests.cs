using Api.Configuration;
using Api.Data;
using Api.Data.Entities;
using Api.Engine;
using Api.Engine.Phases;
using Api.Tests.Infrastructure;
using Api.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.Tests;

public sealed class TelemetryBountyPhaseTests
{
    private sealed class CapturingTelemetryService : IMasterRankingTelemetryService
    {
        public List<(string EventType, string PlayerEmail, string? UniqueScopeKey)> Calls { get; } = [];

        public Task ReportEventAsync(
            string eventType,
            string playerEmail,
            string? uniqueScopeKey = null,
            string? externalEventId = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((eventType, playerEmail, uniqueScopeKey));
            return Task.CompletedTask;
        }
    }

    private sealed class BlockingTelemetryService : IMasterRankingTelemetryService
    {
        private readonly TaskCompletionSource<bool> _firstCallStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _canceled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstCallStarted => _firstCallStarted.Task;
        public Task Canceled => _canceled.Task;

        public async Task ReportEventAsync(
            string eventType,
            string playerEmail,
            string? uniqueScopeKey = null,
            string? externalEventId = null,
            CancellationToken cancellationToken = default)
        {
            _firstCallStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _canceled.TrySetResult(true);
                throw;
            }
        }
    }

    [Fact]
    public async Task TelemetryBountyPhase_GovernmentCompany_DoesNotEmitCompetitiveTelemetryOrBadges()
    {
        var telemetry = new CapturingTelemetryService();
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var gameState = await db.GameStates.FirstAsync();
        var governmentCompanyId = (await GovernmentCompanyQueries.GetGovernmentCompanyIdsAsync(db)).Single();
        var governmentCompany = await db.Companies
            .AsNoTracking()
            .FirstAsync(company => company.Id == governmentCompanyId);
        var governmentPlayer = await db.Players
            .AsNoTracking()
            .FirstAsync(player => player.Id == governmentCompany.PlayerId);
        var governmentAccount = await db.BankAccounts
            .AsNoTracking()
            .FirstAsync(account => account.CompanyId == governmentCompany.Id && account.Balance > 0m);

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            CompaniesById = new Dictionary<Guid, Company> { [governmentCompany.Id] = governmentCompany },
            BankAccountsById = new Dictionary<Guid, BankAccount> { [governmentAccount.Id] = governmentAccount },
        };

        var phase = CreatePhase(telemetry, scope.ServiceProvider);
        await phase.ProcessAsync(context);

        Assert.Empty(telemetry.Calls);
        Assert.False(await db.PlayerAchievementBadges.AnyAsync(badge => badge.PlayerId == governmentPlayer.Id));
    }

    [Fact]
    public async Task TelemetryBountyPhase_BankerOwnDeposit_DoesNotCountAsExternalDeposit()
    {
        var telemetry = new CapturingTelemetryService();
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = "banker-owner@example.com",
            DisplayName = "Bank Owner",
            PasswordHash = "hashed",
            Role = PlayerRole.Player,
        };
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Bank Owner Co",
        };
        var bankBuilding = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Bank,
            Name = "Owner Bank",
            Level = 1,
        };
        var ownDeposit = new BankAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = Guid.NewGuid().ToString("N")[..16],
            CurrencyCode = city.CurrencyCode,
            CompanyId = company.Id,
            BankBuildingId = bankBuilding.Id,
            Balance = 250_000m,
            CreatedAtUtc = DateTime.UtcNow,
            IsBaseCapitalDeposit = true,
        };

        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(bankBuilding);
        db.BankAccounts.Add(ownDeposit);
        await db.SaveChangesAsync();

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [bankBuilding.Id] = bankBuilding },
            BuildingsByType = new Dictionary<string, List<Building>> { [BuildingType.Bank] = [bankBuilding] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };

        var phase = CreatePhase(telemetry, scope.ServiceProvider);
        await phase.ProcessAsync(context);

        Assert.DoesNotContain(telemetry.Calls, call =>
            call.EventType == MasterRankingBountyCodes.Banker
            && call.PlayerEmail == player.Email);
    }

    [Fact]
    public async Task TelemetryBountyPhase_DoesNotWaitForTelemetryDispatch()
    {
        var telemetry = new BlockingTelemetryService();
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.GameStates.FirstAsync();

        var player = new Player
        {
            Id = Guid.NewGuid(),
            Email = "nonblocking-telemetry@example.com",
            DisplayName = "Non Blocking",
            PasswordHash = "hashed",
            Role = PlayerRole.Player,
        };
        var company = new Company
        {
            Id = Guid.NewGuid(),
            PlayerId = player.Id,
            Name = "Non Blocking Co",
        };
        var building = new Building
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            CityId = city.Id,
            Type = BuildingType.Factory,
            Name = "Fast Factory",
            Level = 1,
        };

        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);
        await db.SaveChangesAsync();

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = new Dictionary<Guid, Building> { [building.Id] = building },
            BuildingsByType = new Dictionary<string, List<Building>> { [BuildingType.Factory] = [building] },
            CompaniesById = new Dictionary<Guid, Company> { [company.Id] = company },
        };
        context.NewUnitResourceHistories.Add(new BuildingUnitResourceHistory
        {
            Id = Guid.NewGuid(),
            BuildingId = building.Id,
            BuildingUnitId = Guid.NewGuid(),
            Tick = gameState.CurrentTick,
            ProducedQuantity = 5m,
        });

        var phase = CreatePhase(telemetry, scope.ServiceProvider);
        var processTask = phase.ProcessAsync(context);

        await processTask.WaitAsync(TimeSpan.FromSeconds(1));
        await telemetry.FirstCallStarted.WaitAsync(TimeSpan.FromSeconds(1));
        await telemetry.Canceled.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static TelemetryBountyPhase CreatePhase(
        IMasterRankingTelemetryService telemetry,
        IServiceProvider serviceProvider)
    {
        return new TelemetryBountyPhase(
            telemetry,
            serviceProvider.GetRequiredService<IOptions<MasterServerRegistrationOptions>>());
    }
}