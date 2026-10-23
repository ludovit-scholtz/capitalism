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

        var gameState = await db.AlignedToTelemetryCadenceAsync();
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
        var gameState = await db.AlignedToTelemetryCadenceAsync();

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
        var gameState = await db.AlignedToTelemetryCadenceAsync();

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

    /// <summary>Runs the bounty phase for one company that has a salary setting in Bratislava (optionally with a rival company).</summary>
    private static async Task<CapturingTelemetryService> RunGoodEmployerAsync(
        decimal salaryMultiplier,
        bool hasBuildingWithUnits,
        bool onStrike = false,
        bool powered = true,
        bool secondBuildingOnStrike = false,
        decimal? rivalSalary = null,
        bool rivalEmploys = true)
    {
        var telemetry = new CapturingTelemetryService();
        await using var factory = new ApiWebApplicationFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var city = await db.Cities.FirstAsync(c => c.Name == "Bratislava");
        var gameState = await db.AlignedToTelemetryCadenceAsync();
        gameState.CurrentTick = 102; // a telemetry run tick (multiple of the cadence)

        var player = new Player
        {
            Id = Guid.NewGuid(), Email = $"employer-{Guid.NewGuid():N}@example.com",
            DisplayName = "Employer", PasswordHash = "hashed", Role = PlayerRole.Player,
        };
        var company = new Company { Id = Guid.NewGuid(), PlayerId = player.Id, Name = "Employer Co" };
        var building = new Building
        {
            Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Plant", Level = 1,
            PowerStatus = powered ? PowerStatus.Powered : PowerStatus.Offline,
            StrikeStartedAtTick = onStrike ? 90 : null, StrikeUntilTick = onStrike ? 120 : null,
        };
        db.Players.Add(player);
        db.Companies.Add(company);
        db.Buildings.Add(building);

        var buildings = new Dictionary<Guid, Building> { [building.Id] = building };
        var units = new Dictionary<Guid, List<BuildingUnit>>
        {
            [building.Id] = hasBuildingWithUnits
                ? [new BuildingUnit { Id = Guid.NewGuid(), BuildingId = building.Id, UnitType = UnitType.Manufacturing, Level = 1 }]
                : [],
        };
        var companies = new Dictionary<Guid, Company> { [company.Id] = company };
        var settings = new Dictionary<Guid, List<CompanyCitySalarySetting>>
        {
            [company.Id] = [new CompanyCitySalarySetting { Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, SalaryMultiplier = salaryMultiplier }],
        };

        if (secondBuildingOnStrike)
        {
            var second = new Building
            {
                Id = Guid.NewGuid(), CompanyId = company.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Second", Level = 1,
                PowerStatus = PowerStatus.Powered, StrikeStartedAtTick = 90, StrikeUntilTick = 120,
            };
            db.Buildings.Add(second);
            buildings[second.Id] = second;
            units[second.Id] = [new BuildingUnit { Id = Guid.NewGuid(), BuildingId = second.Id, UnitType = UnitType.Manufacturing, Level = 1 }];
        }

        if (rivalSalary.HasValue)
        {
            var rivalPlayer = new Player
            {
                Id = Guid.NewGuid(), Email = $"rival-{Guid.NewGuid():N}@example.com", DisplayName = "Rival", PasswordHash = "hashed", Role = PlayerRole.Player,
            };
            var rival = new Company { Id = Guid.NewGuid(), PlayerId = rivalPlayer.Id, Name = "Rival Co" };
            var rivalBuilding = new Building
            {
                Id = Guid.NewGuid(), CompanyId = rival.Id, CityId = city.Id, Type = BuildingType.Factory, Name = "Rival", Level = 1,
                PowerStatus = PowerStatus.Powered,
            };
            db.Players.Add(rivalPlayer);
            db.Companies.Add(rival);
            db.Buildings.Add(rivalBuilding);
            companies[rival.Id] = rival;
            settings[rival.Id] = [new CompanyCitySalarySetting { Id = Guid.NewGuid(), CompanyId = rival.Id, CityId = city.Id, SalaryMultiplier = rivalSalary.Value }];
            buildings[rivalBuilding.Id] = rivalBuilding;
            units[rivalBuilding.Id] = rivalEmploys
                ? [new BuildingUnit { Id = Guid.NewGuid(), BuildingId = rivalBuilding.Id, UnitType = UnitType.Manufacturing, Level = 1 }]
                : [];
        }

        await db.SaveChangesAsync();

        var context = new TickContext
        {
            Db = db,
            GameState = gameState,
            BuildingsById = buildings,
            BuildingsByType = new Dictionary<string, List<Building>> { [BuildingType.Factory] = buildings.Values.ToList() },
            CompaniesById = companies,
            CitiesById = new Dictionary<Guid, City> { [city.Id] = city },
            CitySalarySettingsByCompany = settings,
            UnitsByBuilding = units,
        };

        await CreatePhase(telemetry, scope.ServiceProvider).ProcessAsync(context);
        return telemetry;
    }

    private static int GoodEmployerCalls(CapturingTelemetryService telemetry)
        => telemetry.Calls.Count(c => c.EventType == MasterRankingBountyCodes.GoodEmployer);

    [Fact]
    public async Task GoodEmployer_AboveTheBaselineWithRealEmployees_FiresTheBounty()
    {
        Assert.Equal(1, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.4m, hasBuildingWithUnits: true)));
    }

    [Fact]
    public async Task GoodEmployer_SoleEmployerPayingClearlyAboveBaseline_FiresTheBounty()
    {
        Assert.Equal(1, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 2.0m, hasBuildingWithUnits: true)));
    }

    [Fact]
    public async Task GoodEmployer_ExactlyAtTheBar_FiresTheBounty()
    {
        Assert.Equal(1, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.05m, hasBuildingWithUnits: true)));
    }

    [Fact]
    public async Task GoodEmployer_JustBelowTheBar_DoesNotFireTheBounty()
    {
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.04m, hasBuildingWithUnits: true)));
    }

    [Fact]
    public async Task GoodEmployer_SalarySettingInACityWithoutEmployees_DoesNotFireTheBounty()
    {
        // A company cannot farm the bounty by typing a high multiplier for a city where it employs nobody.
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 2.0m, hasBuildingWithUnits: false)));
    }

    [Fact]
    public async Task GoodEmployer_WhileTheWorkforceIsOnStrike_DoesNotFireTheBounty()
    {
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.5m, hasBuildingWithUnits: true, onStrike: true)));
    }

    [Fact]
    public async Task GoodEmployer_WhenAnyOfTheCompanysBuildingsInTheCityIsOnStrike_DoesNotFireTheBounty()
    {
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.8m, hasBuildingWithUnits: true, secondBuildingOnStrike: true)));
    }

    [Fact]
    public async Task GoodEmployer_BuildingWithoutPower_DoesNotCountAsAnEmployer()
    {
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.8m, hasBuildingWithUnits: true, powered: false)));
    }

    [Fact]
    public async Task GoodEmployer_OnlyMarginallyAboveTheRival_DoesNotFireTheBounty()
    {
        // The only rival pays 1.3: 1.32 is just average, not a good employer.
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.32m, hasBuildingWithUnits: true, rivalSalary: 1.3m)));
    }

    [Fact]
    public async Task GoodEmployer_AHigherPaidSettingWithoutEmployeesNeverBeatsARealEmployer()
    {
        // The rival has the higher salary setting but employs nobody; the real employer (1.4) still wins its city.
        Assert.Equal(1, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.4m, hasBuildingWithUnits: true, rivalSalary: 2.5m, rivalEmploys: false)));
    }

    [Fact]
    public async Task GoodEmployer_TwoEmployersPayingTheSame_NobodyHasAClearLead()
    {
        // Equal pay is not a lead over the rival, so the bounty is not awarded to an arbitrary one of them.
        Assert.Equal(0, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.6m, hasBuildingWithUnits: true, rivalSalary: 1.6m)));
    }

    [Fact]
    public async Task GoodEmployer_ClearLeadOverARival_AwardsExactlyOneWinnerPerCity()
    {
        Assert.Equal(1, GoodEmployerCalls(await RunGoodEmployerAsync(salaryMultiplier: 1.6m, hasBuildingWithUnits: true, rivalSalary: 1.4m)));
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