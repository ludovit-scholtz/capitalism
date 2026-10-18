using Api.Security;
using Api.Engine.Phases;
using Api.Types;
using Api.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Engine;

/// <summary>Registers the tick processor, its phases and the background services that run alongside it.</summary>
internal static class GameTickEngineExtensions
{
    internal static IServiceCollection AddGameTickEngine(this IServiceCollection services)
    {
        services.AddSingleton<TickTimingRecorder>();
        services.AddScoped<TickProcessor>();
        services.AddScoped<ITickPhase, WeatherUpdatePhase>();
        services.AddScoped<ITickPhase, LaborMarketPhase>();
        services.AddScoped<ITickPhase, FuelProcurementPhase>();
        services.AddScoped<ITickPhase, PowerDistributionPhase>();
        services.AddScoped<ITickPhase, EnergySpotMarketPhase>();
        services.AddScoped<ITickPhase, PowerGridEconomicsPhase>();
        services.AddScoped<ITickPhase, ConstructionPhase>();
        services.AddScoped<ITickPhase, BuildingUpgradePhase>();
        services.AddScoped<ITickPhase, LandMarketPhase>();
        services.AddScoped<ITickPhase, GlobalEventPhase>();
        services.AddScoped<ITickPhase, MediaHousePhase>();
        services.AddScoped<ITickPhase, Api.Engine.Phases.EconomicCyclePhase>();
        services.AddScoped<ITickPhase, GovernmentContractPhase>();
        services.AddScoped<ITickPhase, PublicSalesPhase>();
        services.AddScoped<ITickPhase, ResourceMovementPhase>();
        services.AddScoped<ITickPhase, ManufacturingPhase>();
        services.AddScoped<ITickPhase, OperatingCostPhase>();
        services.AddScoped<ITickPhase, MiningPhase>();
        services.AddScoped<ITickPhase, ResourceReplenishmentPhase>();
        services.AddScoped<ITickPhase, PurchasingPhase>();
        services.AddScoped<ITickPhase, SupplyContractFulfillmentPhase>();
        services.AddScoped<ITickPhase, TradeRoutePhase>();
        services.AddScoped<ITickPhase, MediaHouseContentPhase>();
        services.AddScoped<ITickPhase, MarketingPhase>();
        services.AddScoped<ITickPhase, ResearchPhase>();
        services.AddScoped<ITickPhase, QualityDecayPhase>();
        services.AddScoped<ITickPhase, RentPhase>();
        services.AddScoped<ITickPhase, NpcCompanyPhase>();
        services.AddScoped<ITickPhase, StockLimitOrderMatchingPhase>();
        services.AddScoped<ITickPhase, LoanRepaymentPhase>();
        services.AddScoped<ITickPhase, BuildingDestructionPhase>();
        services.AddScoped<ITickPhase, PlayerAlertPhase>();
        services.AddScoped<ITickPhase, BankInterestPhase>();
        services.AddScoped<ITickPhase, TaxPhase>();
        services.AddScoped<ITickPhase, DividendGovernanceSettlementPhase>();
        services.AddScoped<ITickPhase, DividendPhase>();
        services.AddScoped<ITickPhase, TelemetryBountyPhase>();
        services.AddScoped<ITickPhase, EconomicReportPhase>();
        services.AddScoped<ITickPhase, MarketReportPhase>();
        services.AddScoped<ITickPhase, CityExpansionUnlockPhase>();
        services.AddScoped<ITickPhase, RankHistoryPhase>();
        services.AddScoped<ITickPhase, EndgamePhase>();
        services.AddScoped<ITickPhase, VictoryCheckPhase>();
        services.AddScoped<ITickPhase, FxRateHistoryPhase>();
        services.AddHostedService<GameTickHostedService>();
        services.AddHostedService<MasterServerRegistrationHostedService>();
        services.AddHostedService<MarketReportPublisherHostedService>();
        services.AddHostedService<JwtSessionCleanupHostedService>();
        services.AddSingleton<BridgedChatMessageTracker>();
        services.AddHostedService<IngameChatToDiscordForwarderHostedService>();

        return services;
    }
}
