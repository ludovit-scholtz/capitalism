using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Entity Framework Core database context for the Capitalism game.
/// Manages all game entities including players, companies, buildings, resources, and products.
/// </summary>
public sealed partial class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>Building-level bank accounts used to fund operating costs.</summary>
    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    /// <summary>Registered game players.</summary>
    public DbSet<Player> Players => Set<Player>();

    /// <summary>Companies owned by players.</summary>
    public DbSet<Company> Companies => Set<Company>();

    /// <summary>Issued share positions held by players or companies.</summary>
    public DbSet<Shareholding> Shareholdings => Set<Shareholding>();

    /// <summary>Annual dividend settlement records.</summary>
    public DbSet<DividendPayment> DividendPayments => Set<DividendPayment>();

    /// <summary>Quoted share-price history recorded for the stock exchange.</summary>
    public DbSet<SharePriceHistoryEntry> SharePriceHistoryEntries => Set<SharePriceHistoryEntry>();

    /// <summary>Per-city salary settings selected by company owners.</summary>
    public DbSet<CompanyCitySalarySetting> CompanyCitySalarySettings => Set<CompanyCitySalarySetting>();

    /// <summary>Buildings placed on the game map.</summary>
    public DbSet<Building> Buildings => Set<Building>();

    /// <summary>Units within building 4x4 grids.</summary>
    public DbSet<BuildingUnit> BuildingUnits => Set<BuildingUnit>();

    /// <summary>Queued building configuration upgrades.</summary>
    public DbSet<BuildingConfigurationPlan> BuildingConfigurationPlans => Set<BuildingConfigurationPlan>();

    /// <summary>Queued unit snapshots for building configuration upgrades.</summary>
    public DbSet<BuildingConfigurationPlanUnit> BuildingConfigurationPlanUnits => Set<BuildingConfigurationPlanUnit>();

    /// <summary>Queued empty-slot transitions for building configuration upgrades.</summary>
    public DbSet<BuildingConfigurationPlanRemoval> BuildingConfigurationPlanRemovals => Set<BuildingConfigurationPlanRemoval>();

    /// <summary>Cities on the game map.</summary>
    public DbSet<City> Cities => Set<City>();

    /// <summary>Natural resources available near cities.</summary>
    public DbSet<CityResource> CityResources => Set<CityResource>();

    /// <summary>Raw material type definitions.</summary>
    public DbSet<ResourceType> ResourceTypes => Set<ResourceType>();

    /// <summary>Manufactured product type definitions.</summary>
    public DbSet<ProductType> ProductTypes => Set<ProductType>();

    /// <summary>Manufacturing recipes linking products to resources.</summary>
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();

    /// <summary>Inventory stored in buildings.</summary>
    public DbSet<Inventory> Inventories => Set<Inventory>();

    /// <summary>Per-tick unit resource/product movement history.</summary>
    public DbSet<BuildingUnitResourceHistory> BuildingUnitResourceHistories => Set<BuildingUnitResourceHistory>();

    /// <summary>Product brands owned by companies.</summary>
    public DbSet<Brand> Brands => Set<Brand>();

    /// <summary>Global game state (singleton row).</summary>
    public DbSet<GameState> GameStates => Set<GameState>();

    /// <summary>Exchange buy/sell orders.</summary>
    public DbSet<ExchangeOrder> ExchangeOrders => Set<ExchangeOrder>();

    /// <summary>Purchasable building lots within cities.</summary>
    public DbSet<BuildingLot> BuildingLots => Set<BuildingLot>();

    /// <summary>Financial event records for company ledger.</summary>
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    /// <summary>Per-tick public sales snapshots for analytics.</summary>
    public DbSet<PublicSalesRecord> PublicSalesRecords => Set<PublicSalesRecord>();

    /// <summary>Persisted market-trend state keyed by (city, item) pair.</summary>
    public DbSet<MarketTrendState> MarketTrendStates => Set<MarketTrendState>();

    /// <summary>Loan offers published by bank buildings.</summary>
    public DbSet<LoanOffer> LoanOffers => Set<LoanOffer>();

    /// <summary>Active and historical loans between companies.</summary>
    public DbSet<Loan> Loans => Set<Loan>();

    /// <summary>Audit trail for administrator actions performed while impersonating players.</summary>
    public DbSet<AdminActionAuditLog> AdminActionAuditLogs => Set<AdminActionAuditLog>();

    /// <summary>Shared in-game chat messages authored by players.</summary>
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    /// <summary>Player-facing in-game notifications shown in the navbar bell.</summary>
    public DbSet<PlayerNotification> PlayerNotifications => Set<PlayerNotification>();

    /// <summary>Buyer offers on buildings listed for player-to-player sale.</summary>
    public DbSet<BuildingSaleOffer> BuildingSaleOffers => Set<BuildingSaleOffer>();

    /// <summary>Records stock-exchange buy/sell executions from the player's personal account.</summary>
    public DbSet<PersonTradeRecord> PersonTradeRecords => Set<PersonTradeRecord>();

    /// <summary>Accumulated R&amp;D research budget per company per product type.</summary>
    public DbSet<ProductResearchBudget> ProductResearchBudgets => Set<ProductResearchBudget>();

    /// <summary>Persisted foreign exchange rates fetched from the NBS daily CSV feed.</summary>
    public DbSet<FxRate> FxRates => Set<FxRate>();

    /// <summary>Completed forex currency swap audit trail.</summary>
    public DbSet<ForexTradeRecord> ForexTradeRecords => Set<ForexTradeRecord>();

    /// <summary>Per-player XAU (gold token) balance on this game server.</summary>
    public DbSet<PlayerGoldBalance> PlayerGoldBalances => Set<PlayerGoldBalance>();

    /// <summary>AMM liquidity pools for fiat/XAU currency pairs.</summary>
    public DbSet<GoldAmmPool> GoldAmmPools => Set<GoldAmmPool>();

    /// <summary>Player LP positions in gold AMM pools.</summary>
    public DbSet<GoldAmmPosition> GoldAmmPositions => Set<GoldAmmPosition>();

    /// <summary>Audit trail for gold AMM swap executions.</summary>
    public DbSet<GoldAmmTradeRecord> GoldAmmTradeRecords => Set<GoldAmmTradeRecord>();

    /// <summary>Generated weekly and monthly city market reports.</summary>
    public DbSet<CityMarketReport> CityMarketReports => Set<CityMarketReport>();

    /// <summary>Per-product seasonal demand multipliers for Q1–Q4.</summary>
    public DbSet<DemandSeasonality> DemandSeasonalities => Set<DemandSeasonality>();

    /// <summary>Scheduled, in-transit, and completed inter-city trade routes.</summary>
    public DbSet<InterCityTradeRoute> InterCityTradeRoutes => Set<InterCityTradeRoute>();

    /// <summary>Audit records created when a mine lot is fully depleted.</summary>
    public DbSet<MineDepletionRecord> MineDepletionRecords => Set<MineDepletionRecord>();

    /// <summary>Per-city scheduling rows for the annual resource replenishment cycle.</summary>
    public DbSet<ResourceReplenishmentSchedule> ResourceReplenishmentSchedules => Set<ResourceReplenishmentSchedule>();

    /// <summary>Per-player tutorial milestone completion tracking.</summary>
    public DbSet<TutorialProgress> TutorialProgresses => Set<TutorialProgress>();

    /// <summary>Tax-cycle economic health snapshots for each city.</summary>
    public DbSet<CityEconomicReport> CityEconomicReports => Set<CityEconomicReport>();

    /// <summary>Achievement badges earned by players for reaching milestones.</summary>
    public DbSet<PlayerAchievementBadge> PlayerAchievementBadges => Set<PlayerAchievementBadge>();

    /// <summary>Weekly leaderboard rank snapshots per player.</summary>
    public DbSet<PlayerRankSnapshot> PlayerRankSnapshots => Set<PlayerRankSnapshot>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureIdentityEntities(modelBuilder);
        ConfigureBuildingEntities(modelBuilder);
        ConfigureEconomyEntities(modelBuilder);
        ConfigureWeatherEntities(modelBuilder);
    }
}
